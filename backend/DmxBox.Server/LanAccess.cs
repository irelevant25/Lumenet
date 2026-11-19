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
/// gives it a cookie with the password, so that it is asked once only. The password is made
/// once and kept in a file, so a device that knows it gets in again after the next start.
/// </para>
/// <para>Thread-safe.</para>
/// </remarks>
internal sealed class LanAccess : IDisposable
{
    /// <summary>How many characters the password has.</summary>
    public const int PasswordLength = 5;

    /// <summary>The cookie a device gets for the right password.</summary>
    public const string CookieName = "lumenet-lan";

    /// <summary>The SignalR group of the UIs on this PC: the only ones that hear about LAN.</summary>
    public const string PcGroup = "pc";

    // Letters and digits that cannot be taken for one another when read off a screen:
    // no 0 and O, no 1, I and L.
    private const string PasswordCharacters = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    // Wrong passwords a device may give within a minute. More than that is guessing.
    private const int WrongPasswordsPerMinute = 5;
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

    // The wrong passwords of each device: since when it has been counted, and how many.
    private readonly Dictionary<IPAddress, (DateTimeOffset Since, int Count)> _wrong = [];

    private bool _on;
    private string? _password;

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

    /// <summary>True if a connection from this address comes from this PC itself.</summary>
    /// <param name="remote">The address of the other end; null when it is not known.</param>
    public bool IsLocal(IPAddress? remote)
    {
        return remote is not null && _pc.Owns(remote);
    }

    /// <summary>True if a connection from this address may be served at all: from this PC always, from another device while LAN is on.</summary>
    /// <param name="remote">The address of the other end; null when it is not known.</param>
    public bool MayConnect(IPAddress? remote)
    {
        return IsLocal(remote) || On;
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
    /// A device sent a password. A device that has sent too many wrong ones within a minute
    /// gets <see cref="Answer.TooMany"/> until the minute is over.
    /// </summary>
    /// <param name="remote">The device's address.</param>
    /// <param name="typed">What it sent. Spaces, dashes and lower case do not matter.</param>
    public Answer LogIn(IPAddress? remote, string? typed)
    {
        IPAddress device = remote ?? IPAddress.None;
        lock (_lock)
        {
            DateTimeOffset now = _time.GetUtcNow();
            (DateTimeOffset Since, int Count) wrong = _wrong.GetValueOrDefault(device);
            if (wrong.Count > 0 && now - wrong.Since >= WrongPasswordWindow)
                wrong = default;

            if (wrong.Count >= WrongPasswordsPerMinute)
                return Answer.TooMany;

            if (Matches(Normalize(typed), PasswordLocked()))
            {
                _wrong.Remove(device);
                return Answer.Welcome;
            }

            _wrong[device] = wrong.Count == 0 ? (now, 1) : (wrong.Since, wrong.Count + 1);
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
            return Matches(cookie, PasswordLocked());
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

            return new LanState(Local: true, On: _on, Addresses: AddressesForDevices(), Password: _on ? PasswordLocked() : null);
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
        _password ??= ReadPassword() ?? KeepPassword(NewPassword());
        return _password;
    }

    private string? ReadPassword()
    {
        try
        {
            string text = File.ReadAllText(_passwordFile).Trim();
            return text.Length == PasswordLength && text.All(PasswordCharacters.Contains) ? text : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;   // no file yet, or one that cannot be read: a new password is made
        }
    }

    private string KeepPassword(string password)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_passwordFile)!);
            File.WriteAllText(_passwordFile, password + Environment.NewLine);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.LogWarning(
                "The LAN password could not be kept in {File}: {Message} It will be another one after the next start.",
                _passwordFile,
                e.Message);
        }

        return password;
    }

    private static string NewPassword()
    {
        char[] characters = new char[PasswordLength];
        for (int index = 0; index < characters.Length; index++)
            characters[index] = PasswordCharacters[RandomNumberGenerator.GetInt32(PasswordCharacters.Length)];

        return new string(characters);
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
