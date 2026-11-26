using System.Net;
using System.Net.Sockets;

namespace DmxBox.Server.Osc;

/// <summary>
/// OSC from other programs (QLab, TouchOSC, a show controller): while it is on, the desk listens on a
/// UDP port, and each message works the key or fader its address names (<see cref="OscInput"/>), as a
/// control of a MIDI controller does: the keys through the page on the PC used last, the faders in
/// the desk itself. The messages go into the MIDI view's list of incoming messages.
/// <list type="bullet">
/// <item>A message from this PC always counts; one from another device only while LAN is on, as the
/// pages of phones and tablets. One that does not count is listed as refused.</item>
/// <item>The setup belongs to the PC (osc.json beside midi.json); only a page on the PC changes it.
/// OSC is off until it is switched on.</item>
/// </list>
/// </summary>
/// <remarks>Thread-safe. The messages come on a task of their own.</remarks>
internal sealed class OscControl : IHostedService, IDisposable
{
    /// <summary>Why a message of another device does not count while LAN is off.</summary>
    public const string LanOff = "From another device only while LAN is on.";

    private readonly Lock _lock = new();
    private readonly OscSetupFile _file;
    private readonly MidiControl _midi;
    private readonly Func<IPAddress, bool> _isLocal;
    private readonly Func<bool> _lanOn;
    private readonly IPAddress _bind;
    private readonly ILogger _log;
    private OscSetup _setup;
    private UdpClient? _udp;
    private CancellationTokenSource? _stop;
    private bool _started;

    /// <summary>Puts OSC together; nothing listens until it starts.</summary>
    /// <param name="file">osc.json.</param>
    /// <param name="midi">The MIDI, which does what the messages name and lists them.</param>
    /// <param name="isLocal">True for an address of this PC.</param>
    /// <param name="lanOn">True while devices in the network may use the desk.</param>
    /// <param name="bind">The address to listen at: every one of the PC, or 127.0.0.1 for a test desk.</param>
    /// <param name="log">The log.</param>
    public OscControl(OscSetupFile file, MidiControl midi, Func<IPAddress, bool> isLocal, Func<bool> lanOn, IPAddress bind, ILogger log)
    {
        _file = file;
        _midi = midi;
        _isLocal = isLocal;
        _lanOn = lanOn;
        _bind = bind;
        _log = log;
        _setup = file.Read();
        _midi.TakeOsc(new OscView(_setup.On, _setup.Port, OscView.Off, null));
    }

    /// <summary>The port it listens on while it does; 0 while it does not (a test that asked for port 0 learns the real one).</summary>
    public int ListeningPort
    {
        get
        {
            lock (_lock)
                return (_udp?.Client.LocalEndPoint as IPEndPoint)?.Port ?? 0;
        }
    }

    /// <summary>Switches OSC on or off, on a port, and keeps that in osc.json.</summary>
    /// <param name="on">True to receive OSC.</param>
    /// <param name="port">The UDP port, 1 to 65535.</param>
    /// <returns>The MIDI with OSC as it is now.</returns>
    /// <exception cref="DeskException">The port is no port, or the setup cannot be kept.</exception>
    public MidiState Set(bool on, int port)
    {
        OscSetup setup = new OscSetup(on, port).Checked();
        lock (_lock)
        {
            try
            {
                _file.Write(setup);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new DeskException($"The OSC setup cannot be kept in {_file.Path}: {e.Message}");
            }

            _setup = setup;
            if (_started)
                Listen();
            else
                _midi.TakeOsc(new OscView(setup.On, setup.Port, OscView.Off, null));
        }

        return _midi.State;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _started = true;
            Listen();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _started = false;
            Close();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
            Close();
    }

    /// <summary>A packet came in: each of its messages works what it names, if its sender may.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="from">The address it came from.</param>
    internal void Take(ReadOnlySpan<byte> packet, IPAddress from)
    {
        List<OscMessage> messages = OscPacket.Read(packet);
        if (messages.Count == 0)
            return;

        IPAddress sender = from.IsIPv4MappedToIPv6 ? from.MapToIPv4() : from;
        string? refused = _isLocal(sender) || _lanOn() ? null : LanOff;
        foreach (OscMessage message in messages)
            _midi.HandleOsc(sender.ToString(), message, refused);
    }

    // Opens the port of the setup, or closes it while OSC is off. Call with the lock held.
    private void Listen()
    {
        Close();
        if (!_setup.On)
        {
            _midi.TakeOsc(new OscView(false, _setup.Port, OscView.Off, null));
            return;
        }

        var udp = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            // No other program may listen on the same port beside the desk: messages would go to one of the two.
            udp.Client.ExclusiveAddressUse = true;
            udp.Client.Bind(new IPEndPoint(_bind, _setup.Port));
        }
        catch (SocketException e)
        {
            udp.Dispose();
            string problem = e.SocketErrorCode is SocketError.AddressAlreadyInUse or SocketError.AccessDenied
                ? $"UDP port {_setup.Port} is taken by another program: choose another port, in both programs."
                : $"UDP port {_setup.Port} cannot be used: {e.Message}";
            _log.LogWarning("OSC: {Problem}", problem);
            _midi.TakeOsc(new OscView(true, _setup.Port, OscView.Failed, problem));
            return;
        }

        var stop = new CancellationTokenSource();
        _udp = udp;
        _stop = stop;
        _ = Task.Run(() => ReceiveAsync(udp, stop.Token));
        _log.LogInformation("OSC: listening on UDP port {Port}.", _setup.Port);
        _midi.TakeOsc(new OscView(true, _setup.Port, OscView.Listening, null));
    }

    // Call with the lock held.
    private void Close()
    {
        _stop?.Cancel();
        _stop?.Dispose();
        _stop = null;
        _udp?.Dispose();
        _udp = null;
    }

    private async Task ReceiveAsync(UdpClient udp, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await udp.ReceiveAsync(stop);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
            {
                return;   // closed
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionReset)
            {
                continue;   // Windows tells of an earlier packet that found nobody; nothing to do
            }
            catch (SocketException e)
            {
                _log.LogWarning("OSC: receiving stopped: {Reason}", e.Message);
                return;
            }

            try
            {
                Take(received.Buffer, received.RemoteEndPoint.Address);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                _log.LogError(e, "OSC: a message could not be done; the next one is taken as usual.");
            }
        }
    }
}
