using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Server;

/// <summary>
/// Whether other devices in the same network (a phone, a tablet) may use the desk, and the
/// password they have to give. LAN is off whenever the program starts; the owner switches it
/// on at the PC, with the LAN key of the page.
/// </summary>
/// <remarks>
/// <para>
/// This PC is never asked for anything: the rules here are for connections that come from
/// another device. While LAN is off, such a connection is closed before the server reads a
/// byte of it, and devices that are connected lose their connection the moment it goes off.
/// </para>
/// <para>
/// While LAN is on, a device first gets a page that asks for the password. The right one
/// gives it a cookie, so that it is asked once only. The cookie is not the password but a
/// random key of 256 bits: a cookie can be sent as often as one likes, so it must be nothing
/// that can be guessed. Guessing the password itself is held up by a limit of wrong passwords
/// per device and one for all devices together. The password and the key are made once and
/// kept in a file, so a device that knows them gets in again after the next start; deleting
/// the file makes a new password and shuts every device out.
/// </para>
/// <para>
/// The owner can type a password of their own, or have a new one made. Either comes with a new
/// key, and the devices that are connected lose their connection: whoever knew the old password
/// is out. The owner can also choose the address of this PC at which devices may come in, when
/// the PC has more than one (a cable and Wi-Fi). It is kept in the same file.
/// </para>
/// <para>Thread-safe.</para>
/// </remarks>
internal sealed class LanAccess : IDisposable
{
    /// <summary>How many characters a password the desk makes has. One the owner types has at least as many.</summary>
    public const int PasswordLength = 5;

    /// <summary>The most characters a password the owner types can have.</summary>
    public const int MaxPasswordLength = 20;

    /// <summary>The cookie a device gets for the right password.</summary>
    public const string CookieName = "lumenet-lan";

    /// <summary>The SignalR group of the UIs on this PC: the only ones that hear about LAN.</summary>
    public const string PcGroup = "pc";

    // Letters and digits that cannot be taken for one another when read off a screen:
    // no 0 and O, no 1, I and L.
    private const string PasswordCharacters = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    // The key in the cookie: 32 random bytes, written as 64 hexadecimal digits.
    private const int CookieKeyBytes = 32;

    // Wrong passwords within a minute: from one device, and from all devices together. The second
    // limit holds a guesser that keeps taking new addresses, which is easy with IPv6. With it,
    // trying every password would take more than two years.
    //
    // The price, chosen knowingly: someone in the network who sends wrong passwords on purpose
    // keeps the door shut for new devices, the right password included (letting the right one
    // through would tell a guesser when it has found it). It shuts nothing else: a device that
    // got in before keeps its cookie and is never asked again, and this PC needs no password.
    private const int WrongPasswordsPerMinute = 5;
    private const int WrongPasswordsPerMinuteFromAll = 20;
    private static readonly TimeSpan WrongPasswordWindow = TimeSpan.FromMinutes(1);

    private readonly ThisPc _pc;
    private readonly string _passwordFile;
    private readonly IServer _server;
    private readonly IHubContext<DeskHub, IDeskClient> _hub;
    private readonly ILogger<LanAccess> _log;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();

    // The hub connections of other devices, so that they can be cut off when LAN goes off.
    private readonly ConcurrentDictionary<string, HubCallerContext> _devices = new();

    // The wrong passwords of each device, and of all together: since when counted, and how many.
    private readonly Dictionary<IPAddress, (DateTimeOffset Since, int Count)> _wrong = [];
    private (DateTimeOffset Since, int Count) _wrongFromAll;

    private bool _on;
    private string? _password;
    private string? _cookieKey;

    // The address of this PC the owner chose for LAN; null while the desk chooses by itself.
    private IPAddress? _chosen;

    /// <summary>Puts it together. LAN starts off.</summary>
    /// <param name="pc">This PC: which connections come from it, and its addresses in the network.</param>
    /// <param name="passwordFile">Where the password is kept.</param>
    /// <param name="server">The server, for the port it listens on.</param>
    /// <param name="hub">Reaches the UIs, to tell those on this PC about changes.</param>
    /// <param name="log">Gets a line when LAN is switched, and when the password cannot be kept.</param>
    /// <param name="time">The clock for counting wrong passwords; the tests may pass their own.</param>
    public LanAccess(
        ThisPc pc,
        string passwordFile,
        IServer server,
        IHubContext<DeskHub, IDeskClient> hub,
        ILogger<LanAccess> log,
        TimeProvider? time = null)
    {
        _pc = pc;
        _passwordFile = passwordFile;
        _server = server;
        _hub = hub;
        _log = log;
        _time = time ?? TimeProvider.System;
        _pc.Changed += OnNetworkChanged;
    }

    /// <summary>What a device that sent a password gets.</summary>
    public enum Answer
    {
        /// <summary>The right password.</summary>
        Welcome,

        /// <summary>Another text.</summary>
        Wrong,

        /// <summary>Too many wrong ones in a short time: this one is not even looked at.</summary>
        TooMany,
    }

    /// <summary>True while devices in the network may use the desk.</summary>
    public bool On
    {
        get
        {
            lock (_lock)
            {
                return _on;
            }
        }
    }

    /// <summary>The password. It is made the first time it is needed and kept in its file from then on.</summary>
    public string Password
    {
        get
        {
            lock (_lock)
            {
                return PasswordLocked();
            }
        }
    }

    /// <summary>What the cookie of a device that gave the right password holds: a random key, not the password.</summary>
    public string CookieValue
    {
        get
        {
            lock (_lock)
            {
                Load();
                return _cookieKey!;
            }
        }
    }

    /// <summary>True if a connection from this address comes from this PC itself.</summary>
    /// <param name="remote">The address of the other end; null when it is not known.</param>
    public bool IsLocal(IPAddress? remote)
    {
        return remote is not null && _pc.Owns(remote);
    }

    /// <summary>
    /// True if a connection may be served at all: from this PC always; from another device while
    /// LAN is on, and only at the address chosen for LAN if the owner chose one.
    /// </summary>
    /// <param name="remote">The address of the other end; null when it is not known.</param>
    /// <param name="local">The address of this PC the connection came in at; null when it is not known.</param>
    public bool MayConnect(IPAddress? remote, IPAddress? local)
    {
        if (IsLocal(remote))
            return true;

        lock (_lock)
        {
            if (!_on)
                return false;

            Load();
            return ChosenNow() is not { } chosen || (local is not null && ThisPc.Plain(local).Equals(chosen));
        }
    }

    /// <summary>
    /// Switches LAN on or off. Off cuts every other device off at once. On makes the password
    /// if there is none yet.
    /// </summary>
    /// <param name="on">True to let devices in the network in.</param>
    public void Switch(bool on)
    {
        HubCallerContext[] cut;
        lock (_lock)
        {
            if (_on == on)
                return;

            _on = on;
            if (on)
                PasswordLocked();

            cut = on ? [] : [.. _devices.Values];
        }

        foreach (HubCallerContext device in cut)
            device.Abort();

        if (on)
            _log.LogInformation("LAN is on: devices in the same network may use the desk, with the password.");
        else
            _log.LogInformation("LAN is off: only this PC can use the desk.");
    }

    /// <summary>
    /// Chooses the address of this PC at which devices may come in, or lets the desk choose by
    /// itself again. The devices that are connected lose their connection; those that may still
    /// come in connect again by themselves.
    /// </summary>
    /// <param name="address">One of <see cref="ThisPc.LanAddresses"/>, like "192.168.1.20"; null or empty for "choose by yourself".</param>
    /// <exception cref="DeskException">It is not an address of this PC in the network.</exception>
    public void Choose(string? address)
    {
        IPAddress? chosen = null;
        if (!string.IsNullOrWhiteSpace(address))
        {
            if (!IPAddress.TryParse(address.Trim(), out IPAddress? parsed) || !_pc.LanAddresses.Contains(ThisPc.Plain(parsed)))
                throw new DeskException($"{address.Trim()} is not an address of this PC in the network.");

            chosen = ThisPc.Plain(parsed);
        }

        HubCallerContext[] cut;
        lock (_lock)
        {
            Load();
            if (Equals(_chosen, chosen))
                return;

            _chosen = chosen;
            Keep();
            cut = [.. _devices.Values];
        }

        foreach (HubCallerContext device in cut)
            device.Abort();

        _log.LogInformation("LAN address: {Address}.", chosen?.ToString() ?? "chosen by the desk");
    }

    /// <summary>
    /// Makes the password the owner typed the password, with a new key: every device has to give
    /// the new one, and the connected ones lose their connection.
    /// </summary>
    /// <param name="typed">The new password. Spaces, dashes and lower case do not matter, as when a device gives it.</param>
    /// <exception cref="DeskException">It is too short, too long, or has other characters than letters and digits.</exception>
    public void ChangePassword(string? typed)
    {
        string password = Normalize(typed);
        if (!IsPassword(password))
        {
            throw new DeskException(
                $"A LAN password has {PasswordLength} to {MaxPasswordLength} characters, letters A to Z and digits only.");
        }

        Replace(password);
    }

    /// <summary>Makes a new random password, with a new key, as <see cref="ChangePassword"/> does.</summary>
    public void RenewPassword()
    {
        Replace(NewPassword());
    }

    /// <summary>
    /// A device sent a password. Once a device has sent too many wrong ones within a minute, or
    /// all devices together have, it gets <see cref="Answer.TooMany"/> until the minute is over.
    /// </summary>
    /// <param name="remote">The device's address.</param>
    /// <param name="typed">What it sent. Spaces, dashes and lower case do not matter.</param>
    public Answer LogIn(IPAddress? remote, string? typed)
    {
        IPAddress device = remote ?? IPAddress.None;
        lock (_lock)
        {
            DateTimeOffset now = _time.GetUtcNow();
            (DateTimeOffset Since, int Count) wrong = Current(_wrong.GetValueOrDefault(device), now);
            _wrongFromAll = Current(_wrongFromAll, now);
            if (wrong.Count >= WrongPasswordsPerMinute || _wrongFromAll.Count >= WrongPasswordsPerMinuteFromAll)
                return Answer.TooMany;

            if (Matches(Normalize(typed), PasswordLocked()))
            {
                _wrong.Remove(device);
                return Answer.Welcome;
            }

            ForgetOldTries(now);
            _wrong[device] = OneMore(wrong, now);
            _wrongFromAll = OneMore(_wrongFromAll, now);
            return Answer.Wrong;
        }
    }

    /// <summary>True for the cookie of a device that gave the right password.</summary>
    /// <param name="cookie">The cookie's value; null if the request has none.</param>
    public bool AcceptsCookie(string? cookie)
    {
        if (cookie is null)
            return false;

        lock (_lock)
        {
            Load();
            return Matches(cookie, _cookieKey!);
        }
    }

    /// <summary>Notes a hub connection of another device, so that LAN off can cut it.</summary>
    /// <param name="connection">The connection.</param>
    public void Track(HubCallerContext connection)
    {
        _devices[connection.ConnectionId] = connection;

        // LAN may have gone off between the device's request and this moment.
        if (!On)
            connection.Abort();
    }

    /// <summary>Forgets a hub connection that has ended.</summary>
    /// <param name="connectionId">The connection's name.</param>
    public void Untrack(string connectionId)
    {
        _devices.TryRemove(connectionId, out _);
    }

    /// <summary>LAN as a UI sees it.</summary>
    /// <param name="local">True for a UI on this PC: only that one gets the addresses and the password.</param>
    public LanState StateFor(bool local)
    {
        lock (_lock)
        {
            if (!local)
                return new LanState(Local: false, On: _on, Addresses: [], Password: null);

            Load();
            return new LanState(
                Local: true,
                On: _on,
                Addresses: AddressesForDevices(),
                Password: _on ? PasswordLocked() : null,
                Chosen: _chosen?.ToString());
        }
    }

    /// <summary>Tells the UIs on this PC how LAN stands now. A failure is only written to the log.</summary>
    public async Task TellPcAsync()
    {
        try
        {
            await _hub.Clients.Group(PcGroup).LanAccessChanged(StateFor(local: true));
        }
        catch (Exception e)
        {
            _log.LogWarning(e, "The UIs on this PC could not be told about LAN.");
        }
    }

    /// <summary>Stops following the changes of the network.</summary>
    public void Dispose()
    {
        _pc.Changed -= OnNetworkChanged;
    }

    // Each address of this PC in the network with the port the server listens on.
    private string[] AddressesForDevices()
    {
        int? port = ListeningPort();
        return _pc.LanAddresses.Select(address => port is int number ? $"{address}:{number}" : address.ToString()).ToArray();
    }

    private int? ListeningPort()
    {
        ICollection<string>? addresses = _server.Features.Get<IServerAddressesFeature>()?.Addresses;
        foreach (string address in addresses ?? [])
        {
            // Before the server has started the list holds what was asked for, like "http://*:5080".
            string plain = address.Replace("://*:", "://0.0.0.0:").Replace("://+:", "://0.0.0.0:");
            if (Uri.TryCreate(plain, UriKind.Absolute, out Uri? uri))
                return uri.Port;
        }

        return null;
    }

    // Call with the lock held.
    private string PasswordLocked()
    {
        Load();
        return _password!;
    }

    // Call with the lock held, after Load. The chosen address while this PC has it; null while
    // none is chosen, and while the network has given the PC another address: then devices come
    // in at any address, until the chosen one is back.
    private IPAddress? ChosenNow()
    {
        return _chosen is { } chosen && _pc.LanAddresses.Contains(chosen) ? chosen : null;
    }

    // A new password with a new key, kept in the file. Whoever knew the old one is out.
    private void Replace(string password)
    {
        HubCallerContext[] cut;
        lock (_lock)
        {
            Load();
            _password = password;
            _cookieKey = NewCookieKey();
            _wrong.Clear();
            _wrongFromAll = default;
            Keep();
            cut = [.. _devices.Values];
        }

        foreach (HubCallerContext device in cut)
            device.Abort();

        _log.LogInformation("The LAN password was changed: every device has to give the new one.");
    }

    // Call with the lock held. Reads the password, the cookie key and the chosen address from
    // their file, the first time they are needed. What is missing is made new and kept; a new
    // password always comes with a new key, so that the devices that knew the old one are out.
    private void Load()
    {
        if (_password is not null)
            return;

        string[] lines = ReadLines();
        string? password = lines.Length > 0 && IsPassword(lines[0]) ? lines[0] : null;
        string? key = password is not null && lines.Length > 1 && IsCookieKey(lines[1]) ? lines[1] : null;
        _password = password ?? NewPassword();
        _cookieKey = key ?? NewCookieKey();
        _chosen = lines.Length > 2 && IPAddress.TryParse(lines[2], out IPAddress? chosen) ? ThisPc.Plain(chosen) : null;
        if (password is null || key is null)
            Keep();
    }

    private static string NewCookieKey()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(CookieKeyBytes));
    }

    private string[] ReadLines()
    {
        try
        {
            return File.ReadAllLines(_passwordFile).Select(line => line.Trim()).ToArray();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];   // no file yet, or one that cannot be read: a new password is made
        }
    }

    // Call with the lock held. The password on the first line, for a person who looks into the
    // file; the key on the second; the chosen address, if there is one, on the third.
    private void Keep()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_passwordFile)!);
            string[] lines = _chosen is null ? [_password!, _cookieKey!] : [_password!, _cookieKey!, _chosen.ToString()];
            File.WriteAllLines(_passwordFile, lines);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.LogWarning(
                "The LAN password could not be kept in {File}: {Message} It will be another one after the next start.",
                _passwordFile,
                e.Message);
        }
    }

    // One the desk made, or one the owner typed: letters A to Z and digits, upper case, as Normalize gives them.
    private static bool IsPassword(string text)
    {
        return text.Length is >= PasswordLength and <= MaxPasswordLength
            && text.All(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character));
    }

    private static bool IsCookieKey(string text)
    {
        return text.Length == CookieKeyBytes * 2 && text.All(Uri.IsHexDigit);
    }

    private static string NewPassword()
    {
        char[] characters = new char[PasswordLength];
        for (int index = 0; index < characters.Length; index++)
            characters[index] = PasswordCharacters[RandomNumberGenerator.GetInt32(PasswordCharacters.Length)];

        return new string(characters);
    }

    // A count of wrong passwords that is older than its minute starts again from nothing.
    private static (DateTimeOffset Since, int Count) Current((DateTimeOffset Since, int Count) wrong, DateTimeOffset now)
    {
        return wrong.Count > 0 && now - wrong.Since >= WrongPasswordWindow ? default : wrong;
    }

    private static (DateTimeOffset Since, int Count) OneMore((DateTimeOffset Since, int Count) wrong, DateTimeOffset now)
    {
        return wrong.Count == 0 ? (now, 1) : (wrong.Since, wrong.Count + 1);
    }

    // Call with the lock held. A guesser with ever new addresses must not fill the memory: the
    // counts whose minute is over go.
    private void ForgetOldTries(DateTimeOffset now)
    {
        if (_wrong.Count < 1000)
            return;

        foreach (IPAddress device in _wrong.Where(entry => now - entry.Value.Since >= WrongPasswordWindow).Select(entry => entry.Key).ToArray())
            _wrong.Remove(device);
    }

    // What a person types: upper case, without spaces and dashes.
    private static string Normalize(string? typed)
    {
        return new string((typed ?? "").Where(character => !char.IsWhiteSpace(character) && character != '-')
            .Select(char.ToUpperInvariant)
            .ToArray());
    }

    // The same time for every text of the right length, so that the time of an answer does not
    // tell how many characters were right.
    private static bool Matches(string given, string password)
    {
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(password));
    }

    private void OnNetworkChanged()
    {
        _ = TellPcAsync();
    }
}
