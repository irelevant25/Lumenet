using System.Collections.Concurrent;
using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace DmxBox.Server.Sounds;

/// <summary>
/// The way to a Soundcraft Ui mixer (Ui12, Ui16, Ui24R) over the network, for the tracks of the
/// sounds (see <see cref="UiTrackPlayer"/>): the lists of the USB stick, the mixer's own player, and of
/// its channels only PLAYER L/R (their faders and mutes), both ways.
/// </summary>
/// <remarks>
/// <para>
/// The mixer speaks over a websocket at ws://address (port 80): text frames that start with "3:::",
/// each with lines like "SETD^p.0.mix^0.76" (a number) or "SETS^var.currentTrack^intro.mp3" (a text).
/// It sends its whole state when a client connects and every change after that, but not the client's
/// own changes back, and its meters many times a second. The client says "ALIVE" every second. The
/// lists of the stick come on request: "MEDIA_GET_PLISTS" gives "PLISTS^one^two",
/// "MEDIA_GET_PLIST_TRACKS^one" gives "PLIST_TRACKS^one^a.mp3^b.mp3". Names go back to the mixer
/// exactly as it gave them.
/// </para>
/// <para>
/// Worked out from soundcraft-ui-connection by Ferdinand Malcher (MIT, github.com/fmalcher/soundcraft-ui),
/// whose messages this follows, and checked against a Ui16 (firmware 1.0.7548), whose own web page jumps
/// in a track with "MEDIA_JUMP_TO^0.25" (a part of the track, 0 to 1).
/// </para>
/// <para>
/// Thread-safe. A mixer that is not there is looked for again every 2 seconds; one that says nothing
/// for 5 seconds (a Wi-Fi gone) is taken for lost.
/// </para>
/// </remarks>
internal sealed class UiMixer : IDisposable
{
    /// <summary>No mixer is asked for.</summary>
    public const string None = "";

    /// <summary>The mixer is being looked for, or was lost and is looked for again.</summary>
    public const string Connecting = "connecting";

    /// <summary>The mixer answers.</summary>
    public const string Connected = "connected";

    private static readonly TimeSpan ConnectTime = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan Retry = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Alive = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ListTime = TimeSpan.FromSeconds(4);

    // What the mixer tells of PLAYER L/R this soon after it was found again is its state as it was, not
    // a hand: the link set them before, and sets them again (see UiTrackPlayer).
    private readonly TimeSpan _tellTime;

    private readonly Func<IUiSocket> _sockets;
    private readonly ILogger _log;
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<string, string> _values = new();
    private readonly ConcurrentDictionary<string, string[]> _tracks = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string[]>> _asked = new();
    private TaskCompletionSource<string[]> _playlists = NewList();
    private Channel<string>? _outbox;
    private CancellationTokenSource? _stop;
    private string _address = "";
    private string _state = None;
    private double? _sentFader;   // what this link set the PLAYER fader to last; kept while the mixer is lost
    private bool? _sentMute;      // and PLAYER's mute
    private long _tellAfter;      // what the mixer tells of PLAYER L/R before then (Environment.TickCount64) is its state as it was

    /// <summary>A link to no mixer yet; <see cref="Connect"/> names one.</summary>
    /// <param name="sockets">Makes a websocket: a real one, or a pretend mixer in the tests.</param>
    /// <param name="log">Where the link writes what happens.</param>
    /// <param name="tellTime">How long after a mixer was found again what it tells of PLAYER L/R is its state, not a hand: 1.5 seconds unless the tests say.</param>
    public UiMixer(Func<IUiSocket> sockets, ILogger log, TimeSpan? tellTime = null)
    {
        _sockets = sockets;
        _log = log;
        _tellTime = tellTime ?? TimeSpan.FromSeconds(1.5);
    }

    /// <summary>Tells when the mixer was found or lost, or another one is asked for. It may come on any thread.</summary>
    public event Action? Changed;

    /// <summary>The mixer asked for: its address; empty for none.</summary>
    public string Address
    {
        get
        {
            lock (_lock)
                return _address;
        }
    }

    /// <summary><see cref="None"/>, <see cref="Connecting"/> or <see cref="Connected"/>.</summary>
    public string State
    {
        get
        {
            lock (_lock)
                return _state;
        }
    }

    /// <summary>True while the mixer answers.</summary>
    public bool IsConnected => State == Connected;

    /// <summary>What the mixer is, as it says: "ui16, firmware 1.0.7548"; empty until it said.</summary>
    public string Name
    {
        get
        {
            string model = _values.GetValueOrDefault("model", "");
            string firmware = _values.GetValueOrDefault("firmware", "");
            return model.Length == 0 ? "" : firmware.Length == 0 ? model : $"{model}, firmware {firmware}";
        }
    }

    /// <summary>What the mixer's USB player does, as the mixer told it last; null while it has not said.</summary>
    public UiPlayer? Player =>
        _values.TryGetValue("var.currentState", out string? state)
            ? new UiPlayer(
                (int)Number(state),
                _values.GetValueOrDefault("var.currentPlaylist", ""),
                _values.GetValueOrDefault("var.currentTrack", ""),
                Number(_values.GetValueOrDefault("var.currentLength", "-1")),
                Number(_values.GetValueOrDefault("var.currentTrackPos", "0")))
            : null;

    /// <summary>A value of the mixer's state, by its path ("p.0.mix"), as it told it last or as the link set it; null when neither.</summary>
    public string? Value(string path) => _values.TryGetValue(path, out string? value) ? value : null;

    /// <summary>
    /// Looks for the mixer at this address (an IP address or a name, with a port if not 80), in place of
    /// the one before; empty for none.
    /// </summary>
    public void Connect(string? address)
    {
        string wanted = (address ?? "").Trim();
        CancellationTokenSource? old;
        lock (_lock)
        {
            if (wanted == _address)
                return;

            old = _stop;
            _stop = null;
            _outbox = null;
            _address = wanted;
            _sentFader = null;   // another mixer: nothing set on it yet
            _sentMute = null;
            _state = wanted.Length == 0 ? None : Connecting;
            if (wanted.Length > 0)
            {
                _stop = new CancellationTokenSource();
                CancellationToken cancel = _stop.Token;
                _ = Task.Run(() => RunAsync(wanted, cancel), CancellationToken.None);
            }
        }

        old?.Cancel();
        Forget();
        Changed?.Invoke();
    }

    /// <summary>The playlists of the stick, as the mixer has them: its folders, "~all~" and "~root~". Empty without a mixer.</summary>
    public async Task<string[]> PlaylistsAsync(CancellationToken cancel)
    {
        Task<string[]> listed;
        lock (_lock)
        {
            if (_state != Connected)
                return [];
            listed = _playlists.Task;
        }

        if (!listed.IsCompleted)
            Send("MEDIA_GET_PLISTS");
        try
        {
            return await listed.WaitAsync(ListTime, cancel).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return [];
        }
    }

    /// <summary>The tracks of one playlist, by their names on the stick; asked once, then kept. Empty without a mixer.</summary>
    public async Task<string[]> TracksAsync(string playlist, CancellationToken cancel)
    {
        if (_tracks.TryGetValue(playlist, out string[]? known))
            return known;
        if (!IsConnected || playlist.Length == 0 || playlist.Contains('^') || playlist.Any(char.IsControl))
            return [];

        TaskCompletionSource<string[]> answer = _asked.GetOrAdd(playlist, _ => NewList());
        Send($"MEDIA_GET_PLIST_TRACKS^{playlist}");
        try
        {
            return await answer.Task.WaitAsync(ListTime, cancel).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            _asked.TryRemove(playlist, out _);
            return [];
        }
    }

    /// <summary>The player plays this track of this playlist, from its start, and stops at its end (manual, not one after the other).</summary>
    public void PlayTrack(string playlist, string track)
    {
        SetValue("settings.playMode", 0);
        Send($"MEDIA_SWITCH_TRACK^{playlist}^{track}");
        Send("MEDIA_PLAY");
    }

    /// <summary>The player stops.</summary>
    public void StopPlayer()
    {
        Send("MEDIA_STOP");
    }

    /// <summary>The player goes on from this part of its track, 0 (its start) to 1 (its end).</summary>
    public void JumpTo(double part)
    {
        Send($"MEDIA_JUMP_TO^{Math.Round(Math.Clamp(part, 0, 1), 5).ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>
    /// The fader of the PLAYER channel, both sides (PLAYER L and PLAYER R stand linked), 0 to 1 as the
    /// mixer's fader goes; see <see cref="UiFader"/>. The link keeps it as its own: when the mixer tells
    /// it back after a connection lost and found, that is no move by a hand (<see cref="PlayerMoved"/>).
    /// </summary>
    public void SetPlayerFader(double value)
    {
        value = Math.Round(Math.Clamp(value, 0, 1), 4);
        lock (_lock)
            _sentFader = value;
        SetValue("p.0.mix", value);
        SetValue("p.1.mix", value);
    }

    /// <summary>MUTE of the PLAYER channel, both sides.</summary>
    public void SetPlayerMute(bool mute)
    {
        lock (_lock)
            _sentMute = mute;
        SetValue("p.0.mute", mute ? 1 : 0);
        SetValue("p.1.mute", mute ? 1 : 0);
    }

    /// <summary>True once this link set PLAYER L/R on this mixer: after it is lost and found, it sets them again.</summary>
    public bool HasSetPlayer
    {
        get
        {
            lock (_lock)
                return _sentFader is not null || _sentMute is not null;
        }
    }

    /// <summary>
    /// Tells where the fader of PLAYER L or R stands when it was set on the mixer (by a hand, or by
    /// another program), not by this link: 0 to 1. Also what the mixer tells when it is found for the
    /// first time; not what it tells at once when it is found again (see the constructor). It may come on
    /// any thread.
    /// </summary>
    public event Action<double>? PlayerMoved;

    /// <summary>Tells when PLAYER L or R was muted or unmuted on the mixer, not by this link; as <see cref="PlayerMoved"/>.</summary>
    public event Action<bool>? PlayerMuted;

    public void Dispose()
    {
        Connect("");
    }

    private static TaskCompletionSource<string[]> NewList() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static double Number(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;

    // Sets a value of the mixer's state. The mixer does not tell it back to the one who set it.
    private void SetValue(string path, double value)
    {
        string text = value.ToString(CultureInfo.InvariantCulture);
        if (Send($"SETD^{path}^{text}"))
            _values[path] = text;
    }

    // Sends a line in its turn; false without a mixer: nothing waits for it.
    private bool Send(string line)
    {
        Channel<string>? outbox;
        lock (_lock)
            outbox = _state == Connected ? _outbox : null;

        return outbox is not null && outbox.Writer.TryWrite(line);
    }

    // What the state of a mixer was: gone with it.
    private void Forget()
    {
        _values.Clear();
        _tracks.Clear();
        foreach (string playlist in _asked.Keys)
        {
            if (_asked.TryRemove(playlist, out TaskCompletionSource<string[]>? asked))
                asked.TrySetResult([]);
        }

        lock (_lock)
        {
            _playlists.TrySetResult([]);
            _playlists = NewList();
        }
    }

    // Looks for the mixer until it is no longer asked for: connects, says ALIVE, reads what it sends.
    private async Task RunAsync(string address, CancellationToken cancel)
    {
        while (!cancel.IsCancellationRequested)
        {
            IUiSocket socket = _sockets();
            Channel<string> outbox = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
            bool connected = false;
            using var link = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            try
            {
                link.CancelAfter(ConnectTime);
                await socket.ConnectAsync(new Uri($"ws://{address}"), link.Token).ConfigureAwait(false);
                lock (_lock)
                {
                    if (cancel.IsCancellationRequested)
                        break;
                    _outbox = outbox;
                    _state = Connected;
                    _tellAfter = _sentFader is not null || _sentMute is not null
                        ? Environment.TickCount64 + (long)_tellTime.TotalMilliseconds   // found again: the link sets PLAYER L/R anew
                        : 0;
                    connected = true;
                }

                _log.LogInformation("The mixer at {Address} answers.", address);
                Changed?.Invoke();
                Send("MEDIA_GET_PLISTS");
                _ = SendAllAsync(socket, outbox.Reader, link);
                _ = KeepAliveAsync(link.Token);
                while (true)
                {
                    link.CancelAfter(Quiet);   // a mixer that says nothing for so long is gone
                    if (await socket.ReceiveAsync(link.Token).ConfigureAwait(false) is not { } frame)
                        break;
                    Take(frame);
                }
            }
            catch (Exception e) when (e is WebSocketException or OperationCanceledException or IOException or InvalidOperationException or UriFormatException or ArgumentException)
            {
                if (!cancel.IsCancellationRequested)
                    _log.LogDebug("No mixer at {Address}: {Message}", address, e.Message);
            }
            finally
            {
                outbox.Writer.TryComplete();
                await link.CancelAsync().ConfigureAwait(false);
                await socket.DisposeAsync().ConfigureAwait(false);
            }

            lock (_lock)
            {
                if (_outbox == outbox)
                    _outbox = null;
                if (cancel.IsCancellationRequested)
                    break;
                _state = Connecting;
            }

            if (connected)
            {
                _log.LogInformation("The mixer at {Address} was lost; it is looked for again.", address);
                Forget();
                Changed?.Invoke();
            }

            try
            {
                await Task.Delay(Retry, cancel).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // The lines to the mixer, one after the other, in their order. A line it does not take ends the link.
    private async Task SendAllAsync(IUiSocket socket, ChannelReader<string> lines, CancellationTokenSource link)
    {
        CancellationToken cancel = link.Token;
        try
        {
            await foreach (string line in lines.ReadAllAsync(cancel).ConfigureAwait(false))
                await socket.SendAsync($"3:::{line}", cancel).ConfigureAwait(false);
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException or IOException or InvalidOperationException or ObjectDisposedException)
        {
            if (cancel.IsCancellationRequested)
                return;

            _log.LogDebug("The mixer takes no more: {Message}", e.Message);
            try
            {
                link.Cancel();   // the reading ends too, and the mixer is looked for again
            }
            catch (ObjectDisposedException)
            {
                // the link was over already
            }
        }
    }

    private async Task KeepAliveAsync(CancellationToken cancel)
    {
        try
        {
            using var alive = new PeriodicTimer(Alive);
            while (await alive.WaitForNextTickAsync(cancel).ConfigureAwait(false))
                Send("ALIVE");
        }
        catch (OperationCanceledException)
        {
            // the link ends
        }
    }

    /// <summary>One frame from the mixer: its lines, each a value or an answer. Internal for the tests.</summary>
    internal void Take(string frame)
    {
        if (!frame.StartsWith("3:::", StringComparison.Ordinal))
            return;   // "2::" and "1::": the socket's own heartbeat and hello

        bool named = false;
        double? moved = null;
        bool? muted = null;
        bool told;   // what the mixer tells of PLAYER L/R now: a hand, or its state as it was when it was found again
        lock (_lock)
            told = Environment.TickCount64 >= _tellAfter;
        foreach (string line in frame[4..].Split('\n'))
        {
            string[] parts = line.TrimEnd('\r').Split('^');
            switch (parts[0])
            {
                case "SETD" or "SETS" when parts.Length >= 3:
                    string value = string.Join('^', parts[2..]);
                    if (parts[1] is "model" or "firmware" && _values.GetValueOrDefault(parts[1]) != value)
                        named = true;   // the pages show what the mixer is
                    if (told && parts[1] is "p.0.mix" or "p.1.mix" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double fader))
                        moved = Math.Clamp(fader, 0, 1);   // the mixer tells no client its own changes: a hand on the mixer
                    if (told && parts[1] is "p.0.mute" or "p.1.mute" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double mute))
                        muted = mute != 0;

                    _values[parts[1]] = value;
                    break;
                case "PLISTS":
                    string[] playlists = [.. parts[1..].Where(name => name.Length > 0)];
                    lock (_lock)
                    {
                        if (!_playlists.TrySetResult(playlists))
                        {
                            _playlists = NewList();
                            _playlists.TrySetResult(playlists);
                        }
                    }

                    break;
                case "PLIST_TRACKS" when parts.Length >= 2:
                    string[] tracks = [.. parts[2..].Where(name => name.Length > 0)];
                    _tracks[parts[1]] = tracks;
                    if (_asked.TryRemove(parts[1], out TaskCompletionSource<string[]>? asked))
                        asked.TrySetResult(tracks);
                    break;
                case "UPDATE_PLAYLIST":
                    // the stick may have changed: its lists are asked for anew when they are wanted
                    _tracks.Clear();
                    lock (_lock)
                    {
                        if (_playlists.Task.IsCompleted)
                            _playlists = NewList();
                    }

                    break;
            }
        }

        if (named)
            Changed?.Invoke();
        if (moved is { } hand)
            PlayerMoved?.Invoke(hand);
        if (muted is { } handMuted)
            PlayerMuted?.Invoke(handMuted);
    }
}

/// <summary>What the USB player of the mixer does.</summary>
/// <param name="State">0 stopped, 2 playing, 3 paused; other numbers the mixer uses for a player that plays nothing.</param>
/// <param name="Playlist">The playlist it is in.</param>
/// <param name="Track">The track it has, by its name on the stick.</param>
/// <param name="Length">How long the track is, in seconds; -1 without one.</param>
/// <param name="Part">How far it is, from 0 to 1.</param>
internal readonly record struct UiPlayer(int State, string Playlist, string Track, double Length, double Part)
{
    /// <summary>The player plays, or holds a pause.</summary>
    public bool Plays => State is 2 or 3;

    /// <summary>Where it is in its track, in seconds.</summary>
    public double Position => Length > 0 ? Math.Clamp(Part, 0, 1) * Length : 0;
}

/// <summary>
/// The law of a Soundcraft Ui fader: its place, 0 to 1, and the gain it gives (1 at 0 dB, at 0.765;
/// about 3.16, +10 dB, at the top). From soundcraft-ui-connection (MIT).
/// </summary>
internal static class UiFader
{
    /// <summary>What the bottom of a fader counts as, in decibels: nothing.</summary>
    public const double Silence = -100;

    /// <summary>The gain at a place of the fader: 0 at the bottom, 1 at 0 dB.</summary>
    public static double GainAt(double value)
    {
        double v = Math.Clamp(value, 0, 1);
        if (v == 0)
            return 0;
        return (v < 0.055 ? Math.Sin(28.559933214452666 * v) : 1)
            * Math.Exp((23.90844819639692 + (-26.23877598214595 + (12.195249692570245 - 0.4878099877028098 * v) * v) * v) * v)
            * 2.676529517952372e-4;
    }

    /// <summary>The place of the fader that gives this gain: 0 for none, 1 for +10 dB or more.</summary>
    public static double ValueFor(double gain)
    {
        if (!(gain > 0))
            return 0;
        if (gain >= GainAt(1))
            return 1;

        double low = 0, high = 1;
        for (int round = 0; round < 50; round++)
        {
            double middle = (low + high) / 2;
            if (GainAt(middle) < gain)
                low = middle;
            else
                high = middle;
        }

        return (low + high) / 2;
    }

    /// <summary>The decibels at a place of the fader: <see cref="Silence"/> at the bottom.</summary>
    public static double DecibelsAt(double value)
    {
        double gain = GainAt(value);
        return gain > 0 ? Math.Max(Silence, 20 * Math.Log10(gain)) : Silence;
    }

    /// <summary>The place of the fader that gives these decibels: the bottom for <see cref="Silence"/> and below.</summary>
    public static double ValueForDecibels(double decibels)
    {
        return decibels <= Silence ? 0 : ValueFor(Math.Pow(10, decibels / 20));
    }
}

/// <summary>A websocket to the mixer, one text frame at a time.</summary>
internal interface IUiSocket : IAsyncDisposable
{
    Task ConnectAsync(Uri address, CancellationToken cancel);

    Task SendAsync(string frame, CancellationToken cancel);

    /// <summary>The next whole text frame; null once the mixer closed the socket.</summary>
    Task<string?> ReceiveAsync(CancellationToken cancel);
}

/// <summary>The real websocket.</summary>
internal sealed class UiWebSocket : IUiSocket
{
    private readonly ClientWebSocket _socket = new();
    private readonly byte[] _buffer = new byte[64 * 1024];

    public Task ConnectAsync(Uri address, CancellationToken cancel) => _socket.ConnectAsync(address, cancel);

    public Task SendAsync(string frame, CancellationToken cancel) =>
        _socket.SendAsync(Encoding.UTF8.GetBytes(frame), WebSocketMessageType.Text, endOfMessage: true, cancel);

    public async Task<string?> ReceiveAsync(CancellationToken cancel)
    {
        using var text = new MemoryStream();
        while (true)
        {
            WebSocketReceiveResult result = await _socket.ReceiveAsync(_buffer, cancel).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            text.Write(_buffer, 0, result.Count);
            if (result.EndOfMessage)
                return Encoding.UTF8.GetString(text.GetBuffer(), 0, (int)text.Length);
        }
    }

    public ValueTask DisposeAsync()
    {
        _socket.Dispose();
        return ValueTask.CompletedTask;
    }
}
