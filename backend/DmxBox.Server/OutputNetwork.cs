using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace DmxBox.Server;

/// <summary>
/// The outputs of the project besides its boxes (see <see cref="OutputPlan"/>): the Art-Net nodes
/// and sACN, which <see cref="NetworkOutputs"/> plays on a thread of its own, and every DMX USB
/// interface, which a <see cref="UsbLink"/> plays on a thread of its own. It knows the state of
/// each, tells the UIs when one changes (<see cref="IDeskClient.OutputsChanged"/>), and searches
/// for Art-Net nodes for the Outputs window.
/// </summary>
/// <remarks>
/// Nothing goes out on the network unless the project lists an output: the sockets open with the
/// first output that needs them, or with the first search for nodes.
/// </remarks>
internal sealed class OutputNetwork : BackgroundService
{
    // How often the list of outputs is read again from the desk, besides the moments when the
    // project changes. Only a safety net: those moments are reported.
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    // How long a thread gets to finish when it stops.
    private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(3);

    // How long the search for nodes listens for their answers.
    private static readonly TimeSpan FindTime = TimeSpan.FromSeconds(1.5);

    private readonly Desk _desk;
    private readonly IDeskClient _clients;
    private readonly ThisPc _pc;
    private readonly OutputSettings _settings;
    private readonly Func<string, bool> _isBox;
    private readonly Func<string, IDmxPort> _usbAt;
    private readonly Func<string[]> _usbNames;
    private readonly Simulators? _simulators;
    private readonly ILogger _logger;
    private readonly ILogger<UsbLink> _usbLogger;
    private readonly NetworkOutputs _network;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, (string State, string Detail)> _states = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Running> _usb = new(StringComparer.Ordinal);
    private readonly List<Task> _finishing = [];
    private OutputPlan[] _plans = [];
    private ArtNetSocket? _artNet;
    private Running? _networkThread;
    private bool _closed;

    // Replaced whole under the lock; read without it.
    private volatile OutputView[] _views = [];

    /// <summary>Prepares the outputs. Nothing is opened or sent until the project lists an output.</summary>
    /// <param name="desk">The desk whose output the outputs play.</param>
    /// <param name="clients">All connected UIs. They get the list of outputs when a state changes.</param>
    /// <param name="pc">This PC, for its networks.</param>
    /// <param name="settings">Where to send from and to.</param>
    /// <param name="isBox">True for the address of a dmxbox, see <see cref="BoxNetwork.IsBoxAddress"/>.</param>
    /// <param name="loggers">For the lines about the outputs.</param>
    /// <param name="usbAt">Makes the serial port of a name; null for real ones. The tests make pretend ones.</param>
    /// <param name="usbNames">The serial ports of this PC; null for the real list.</param>
    /// <param name="simulators">
    /// The simulated devices that run on this PC: their Art-Net nodes answer Find nodes, and their
    /// USB interfaces are ports of the list ("SIM1"). Null for none.
    /// </param>
    public OutputNetwork(
        Desk desk,
        IDeskClient clients,
        ThisPc pc,
        OutputSettings settings,
        Func<string, bool> isBox,
        ILoggerFactory loggers,
        Func<string, IDmxPort>? usbAt = null,
        Func<string[]>? usbNames = null,
        Simulators? simulators = null)
    {
        _desk = desk;
        _clients = clients;
        _pc = pc;
        _settings = settings;
        _isBox = isBox;
        _simulators = simulators;
        _usbAt = usbAt ?? (name => PipeDmxPort.IsSimulated(name) ? new PipeDmxPort(name) : new SerialDmxPort(name));
        _usbNames = usbNames ?? (() => [.. SerialDmxPort.Names(), .. simulators?.AddressesOf(SimulatedDevice.Usb) ?? []]);
        _logger = loggers.CreateLogger<OutputNetwork>();
        _usbLogger = loggers.CreateLogger<UsbLink>();
        _network = new NetworkOutputs(desk, pc, settings, ArtNetSocketNow, isBox, Report, _logger);
    }

    /// <summary>The outputs as the Outputs window shows them, in the project's order, each with its state.</summary>
    public OutputView[] Outputs => _views;

    /// <summary>
    /// Reads the project's outputs again: starts what is new, stops what is gone, and tells the UIs
    /// when the list changed. Any thread may call this.
    /// </summary>
    public void Refresh()
    {
        lock (_lock)
        {
            if (_closed)
                return;

            OutputPlan[] plans = _desk.Outputs;
            if (!Same(plans, _plans))
            {
                _plans = plans;
                HashSet<string> listed = [.. plans.Select(plan => plan.Identity)];
                foreach (string gone in _states.Keys.Where(identity => !listed.Contains(identity)).ToList())
                    _states.Remove(gone);

                PlayUsb(plans);
                _network.Play(plans);
                if (_networkThread is null && plans.Any(plan => plan.Kind != OutputPlan.Usb))
                    _networkThread = Start("Outputs", stop => RunNetwork(stop));
            }

            RewireLocked();
        }
    }

    /// <summary>
    /// Asks every Art-Net node on the networks of this PC to answer, and gives those that did,
    /// within a second and a half, in the order of their addresses.
    /// </summary>
    /// <exception cref="DeskException">The desk cannot open its Art-Net socket.</exception>
    public async Task<FoundNode[]> FindArtNetNodesAsync()
    {
        ArtNetSocket socket;
        lock (_lock)
        {
            if (_closed)
                throw new DeskException("The desk program is stopping.");
            try
            {
                socket = ArtNetSocketLocked();
            }
            catch (SocketException e)
            {
                throw new DeskException($"The desk cannot open its Art-Net socket: {e.Message}");
            }
        }

        TimeSpan since = socket.Now;
        foreach (IPEndPoint target in PollTargets())
        {
            try
            {
                socket.Poll(target);
            }
            catch (SocketException)
            {
                // A network that went away, or a target that cannot be reached: the others count.
            }
        }

        await Task.Delay(FindTime);
        return [.. socket.AnswersSince(since)
            .OrderBy(node => AddressKey(node.Ip))
            .Select(node => new FoundNode(
                node.Ip, node.ShortName, node.LongName, node.Firmware, node.Ports, NetworkOutputs.IsDmxbox(node) || _isBox(node.Ip)))];
    }

    /// <summary>The serial ports of this PC, for a DMX USB interface: "COM3", "/dev/ttyUSB0".</summary>
    public string[] SerialPorts()
    {
        return _usbNames();
    }

    /// <summary>The networks of this PC a broadcast or a multicast can go out on: all but those of the boxes' cables.</summary>
    public NetworkChoice[] Networks()
    {
        return [.. _pc.Networks
            .Where(network => !BoxNetwork.IsBoxCable(network.Address))
            .Select(network => new NetworkChoice(network.Network, network.Address.ToString(), network.Adapter, network.Router))];
    }

    /// <summary>Stops every output, waiting for each: the sACN universes are ended, the ports closed.</summary>
    public override void Dispose()
    {
        StopAll();
        base.Dispose();
    }

    /// <summary>Plays the outputs until the server stops.</summary>
    /// <param name="stoppingToken">Signals that the server is shutting down.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            Refresh();
            using var timer = new PeriodicTimer(RefreshInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                Refresh();
        }
        catch (OperationCanceledException)
        {
            // The server stops.
        }
        finally
        {
            StopAll();
        }
    }

    private static bool Same(OutputPlan[] a, OutputPlan[] b)
    {
        return a.Length == b.Length && a.Zip(b).All(pair => pair.First.SameAs(pair.Second));
    }

    // Addresses as numbers, so that 192.168.1.9 comes before 192.168.1.10.
    private static long AddressKey(string ip)
    {
        return IPAddress.TryParse(ip, out IPAddress? address) && address.AddressFamily == AddressFamily.InterNetwork
            ? System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(address.GetAddressBytes())
            : long.MaxValue;
    }

    // Where the search for nodes polls: the broadcast of every network of this PC (the boxes'
    // own as well, so that a box shows as one), the nodes the project lists, which a router may
    // reach in another network, and the simulated nodes that run on this PC. A test desk, which
    // sends from one address only, polls that address and the simulated nodes.
    private List<IPEndPoint> PollTargets()
    {
        int port = _settings.ArtNetPort;
        var targets = new List<IPEndPoint>();
        if (_settings.Bind.Equals(IPAddress.Any))
        {
            targets.AddRange(_pc.Networks.Select(network => new IPEndPoint(network.Broadcast, port)));
            foreach (OutputPlan plan in _desk.Outputs.Where(plan => plan.Kind == OutputPlan.ArtNet && plan.Target != OutputPlan.Broadcast))
                targets.Add(new IPEndPoint(IPAddress.Parse(plan.Target), port));
        }
        else
        {
            targets.Add(new IPEndPoint(IPAddress.Loopback, port));
        }

        foreach (string address in _simulators?.AddressesOf(SimulatedDevice.ArtNet) ?? [])
        {
            if (OutputPlan.IPv4Of(address) is { } node)
                targets.Add(new IPEndPoint(node, port));
        }

        return targets;
    }

    private ArtNetSocket ArtNetSocketNow()
    {
        lock (_lock)
        {
            if (_closed)
                throw new SocketException((int)SocketError.Shutdown);
            return ArtNetSocketLocked();
        }
    }

    // Call with the lock held.
    private ArtNetSocket ArtNetSocketLocked()
    {
        return _artNet ??= new ArtNetSocket(_settings.Bind, _settings.ArtNetReplyPort, _logger);
    }

    // Call with the lock held. A link for every USB interface of the list; one that plays another
    // universe now is told so.
    private void PlayUsb(OutputPlan[] plans)
    {
        Dictionary<string, OutputPlan> wanted = plans
            .Where(plan => plan.Kind == OutputPlan.Usb)
            .ToDictionary(plan => plan.Identity, StringComparer.Ordinal);
        _finishing.RemoveAll(task => task.IsCompleted);
        foreach (string identity in _usb.Keys.Where(identity => !wanted.ContainsKey(identity)).ToList())
        {
            Running running = _usb[identity];
            _usb.Remove(identity);
            running.Stop.Cancel();
            _finishing.Add(Task.Run(() => Finish(running)));
        }

        foreach ((string identity, OutputPlan plan) in wanted)
        {
            int universe = plan.Ports[0].Universe;
            if (_usb.TryGetValue(identity, out Running? running))
            {
                ((UsbLink)running.Owner!).Universe = universe;
                continue;
            }

            var link = new UsbLink(
                _desk, plan.Target, universe, _usbAt, _usbNames, (state, detail) => Report(identity, state, detail), _usbLogger);
            _usb[identity] = Start($"USB {plan.Target}", link.Run, link);
        }
    }

    // The thread of the network outputs: 40 rounds a second until it is told to stop; then it
    // ends every sACN universe and closes its sockets.
    private void RunNetwork(CancellationToken stopping)
    {
        var clock = Stopwatch.StartNew();
        TimeSpan nextRound = TimeSpan.Zero;
        string? lastFault = null;
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                _network.Step(clock.Elapsed);
            }
            catch (Exception e)
            {
                // A fault in this program: the outputs carry on with the next round. The log gets
                // each fault once, not 40 times a second.
                if (stopping.IsCancellationRequested)
                    break;
                if (e.Message != lastFault)
                {
                    lastFault = e.Message;
                    _logger.LogWarning(e, "The network outputs failed a round: {Reason}", e.Message);
                }
            }

            nextRound += NetworkOutputs.RoundInterval;
            TimeSpan wait = nextRound - clock.Elapsed;
            if (wait > TimeSpan.Zero)
                stopping.WaitHandle.WaitOne(wait);
            else
                nextRound = clock.Elapsed;
        }

        _network.Dispose();
    }

    // A link's state changed. Any thread.
    private void Report(string identity, string state, string detail)
    {
        lock (_lock)
        {
            if (_closed || !_plans.Any(plan => plan.Identity == identity))
                return;

            _states[identity] = (state, detail);
            RewireLocked();
        }
    }

    // Call with the lock held. The views anew; the UIs hear of a change.
    private void RewireLocked()
    {
        OutputView[] views = [.. _plans.Select(plan =>
        {
            (string state, string detail) = _states.TryGetValue(plan.Identity, out var known) ? known : FirstState(plan);
            return new OutputView(plan, state, detail);
        })];

        if (views.Length == _views.Length && views.Zip(_views).All(pair => pair.First.SameAs(pair.Second)))
            return;

        _views = views;

        // Not awaited: a browser that is slow to read its messages must not hold up the lights.
        _ = _clients.OutputsChanged(views);
    }

    // What an output shows before its link has said anything, a moment after it was added.
    private (string, string) FirstState(OutputPlan plan)
    {
        if (plan.Kind == OutputPlan.ArtNet && plan.Target != OutputPlan.Broadcast && _isBox(plan.Target))
            return (OutputView.Conflict, "");

        return plan.Kind == OutputPlan.Sacn || plan.Target == OutputPlan.Broadcast ? (OutputView.Sending, "") : (OutputView.Missing, "");
    }

    // Call with the lock held.
    private static Running Start(string name, Action<CancellationToken> run, object? owner = null)
    {
        var stop = new CancellationTokenSource();
        var thread = new Thread(() => run(stop.Token)) { IsBackground = true, Name = name };
        var running = new Running(thread, stop, owner);
        thread.Start();
        return running;
    }

    // Every thread stops at once; then each is waited for, so that none writes to the log after the
    // server has closed it. The network thread ends the sACN universes on its way out.
    private void StopAll()
    {
        List<Running> stopped;
        Task[] finishing;
        ArtNetSocket? artNet;
        lock (_lock)
        {
            if (_closed)
                return;

            _closed = true;
            stopped = [.. _usb.Values];
            if (_networkThread is not null)
                stopped.Add(_networkThread);
            _usb.Clear();
            _networkThread = null;
            foreach (Running running in stopped)
                running.Stop.Cancel();

            finishing = [.. _finishing];
            artNet = _artNet;
        }

        foreach (Running running in stopped)
            Finish(running);

        Task.WaitAll(finishing, StopWait);
        artNet?.Dispose();
    }

    // Waits for a thread, and closes what it owned.
    private static void Finish(Running running)
    {
        running.Thread.Join(StopWait);
        (running.Owner as IDisposable)?.Dispose();
        running.Stop.Dispose();
    }

    private sealed record Running(Thread Thread, CancellationTokenSource Stop, object? Owner);
}
