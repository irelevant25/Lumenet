using System.Net;
using System.Net.Sockets;

namespace DmxBox.Server;

/// <summary>
/// Plays the outputs of the project that go over the network: Art-Net nodes and sACN. Every
/// round it takes the desk's output, if it changed, and sends each port the 512 levels of the
/// universe it plays: at once when they change, twice more in the next rounds in case a packet is
/// lost, and then again every 0.8 seconds while they stay the same, so that no receiver takes
/// the silence for lost data (sACN after 2.5 seconds, Art-Net nodes after a few seconds). It polls
/// every Art-Net node it sends to, to know whether it answers.
/// </summary>
/// <remarks>
/// <para>
/// One thread does the rounds, 40 a second, and owns everything here; <see cref="OutputNetwork"/>
/// runs it, and hands it the outputs to play with <see cref="Play"/>, from any thread. The tests
/// call <see cref="Step"/> themselves instead.
/// </para>
/// <para>
/// A port that plays no universe sends nothing. An sACN universe that stops (its port plays none,
/// or its output was taken out, or the desk program ends) is ended with packets that say so, so
/// that its receivers let go of it at once.
/// </para>
/// </remarks>
internal sealed class NetworkOutputs : IDisposable
{
    /// <summary>40 rounds a second, as for the boxes: DMX itself carries about 44 frames a second.</summary>
    public static readonly TimeSpan RoundInterval = TimeSpan.FromMilliseconds(25);

    // How often a universe that does not change is sent again.
    private static readonly TimeSpan KeepAlive = TimeSpan.FromMilliseconds(800);

    // How often an Art-Net node is asked whether it is there.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    // How many times a change is sent again, in the rounds after it.
    private const int Repeats = 2;

    private readonly Desk _desk;
    private readonly ThisPc _pc;
    private readonly OutputSettings _settings;
    private readonly Func<ArtNetSocket> _artNet;
    private readonly Func<string, bool> _isBox;
    private readonly Action<string, string, string> _report;
    private readonly ILogger _logger;
    private SacnSender? _sacnSender;

    private volatile OutputPlan[] _wanted = [];
    private OutputPlan[]? _applied;
    private List<Link> _links = [];
    private long _seenChanges = -1;
    private byte[] _output = [];
    private Socket? _sacnSocket;
    private readonly Dictionary<string, Socket> _multicastSockets = new(StringComparer.Ordinal);

    /// <summary>Prepares the outputs. Nothing is sent until <see cref="Play"/> hands some over.</summary>
    /// <param name="desk">The desk whose output goes out.</param>
    /// <param name="pc">This PC, for its networks.</param>
    /// <param name="settings">Where the desk sends from, and the ports.</param>
    /// <param name="artNet">The Art-Net socket, opened when it is first needed.</param>
    /// <param name="isBox">True for the address of a dmxbox: no Art-Net goes there.</param>
    /// <param name="report">Takes the state of an output when it changes: its identity, the state, and what it means.</param>
    /// <param name="logger">Gets a line when the CID of the desk cannot be kept.</param>
    public NetworkOutputs(
        Desk desk,
        ThisPc pc,
        OutputSettings settings,
        Func<ArtNetSocket> artNet,
        Func<string, bool> isBox,
        Action<string, string, string> report,
        ILogger logger)
    {
        _desk = desk;
        _pc = pc;
        _settings = settings;
        _artNet = artNet;
        _isBox = isBox;
        _report = report;
        _logger = logger;
    }

    // The desk as an sACN source, called "Lumenet", with the CID of this PC: read, or made, when
    // the first sACN packet goes out.
    private SacnSender Sacn => _sacnSender ??= new SacnSender(SacnSource.Cid(_settings.CidFile, _logger), AppInfo.Name);

    /// <summary>The outputs to play from the next round on: those of the project that go over the network. Any thread.</summary>
    /// <param name="plans">Art-Net and sACN outputs; others are left out.</param>
    public void Play(OutputPlan[] plans)
    {
        _wanted = [.. plans.Where(plan => plan.Kind is OutputPlan.ArtNet or OutputPlan.Sacn)];
    }

    /// <summary>
    /// The network a broadcast or a multicast goes out on: the one the plan names, or for automatic
    /// the one that leads to a router, else the first. The networks of the boxes' USB cables never
    /// count. Null when this PC is not in such a network.
    /// </summary>
    /// <param name="plan">An output with <see cref="OutputPlan.Shared"/>.</param>
    /// <param name="networks">The networks of this PC.</param>
    public static PcNetwork? NetworkFor(OutputPlan plan, IReadOnlyList<PcNetwork> networks)
    {
        List<PcNetwork> usable = [.. networks.Where(network => !BoxNetwork.IsBoxCable(network.Address))];
        return plan.Network.Length == 0
            ? usable.FirstOrDefault(network => network.Router) ?? usable.FirstOrDefault()
            : usable.FirstOrDefault(network => network.Network == plan.Network);
    }

    /// <summary>
    /// One round: the outputs that changed are put in place, and every port gets its universe as
    /// far as it is due (see the class). Never throws for a network that fails: the output that
    /// failed is reported missing.
    /// </summary>
    /// <param name="now">The time on a clock that only runs forward.</param>
    public void Step(TimeSpan now)
    {
        OutputPlan[] wanted = _wanted;
        if (!ReferenceEquals(wanted, _applied))
            Apply(wanted);

        if (_links.Count == 0)
            return;

        // The count first, then the output: a change in between is sent once more than needed,
        // but never missed.
        long changes = _desk.OutputChanges;
        if (changes != _seenChanges)
        {
            _output = _desk.GetOutput();
            _seenChanges = changes;
        }

        foreach (Link link in _links)
            Serve(link, now);
    }

    /// <summary>Ends every sACN universe that went out, with the packets that say so, and closes the sockets.</summary>
    public void Dispose()
    {
        foreach (Link link in _links)
        {
            foreach (PortState port in link.Ports)
                End(link, port);
        }

        _links = [];
        _sacnSocket?.Dispose();
        foreach (Socket socket in _multicastSockets.Values)
            socket.Dispose();
        _multicastSockets.Clear();
    }

    // The outputs to play changed: a link for each. A port that plays on, by the same output and
    // address, keeps what it sent (an sACN universe its sequence, with a new priority if it has
    // one); an sACN universe that is gone is ended.
    private void Apply(OutputPlan[] plans)
    {
        Dictionary<string, Link> before = _links.ToDictionary(link => link.Plan.Identity, StringComparer.Ordinal);
        var handedOn = new HashSet<PortState>(ReferenceEqualityComparer.Instance);
        var links = new List<Link>();
        foreach (OutputPlan plan in plans)
        {
            before.TryGetValue(plan.Identity, out Link? old);
            var ports = new PortState[plan.Ports.Length];
            for (int index = 0; index < ports.Length; index++)
            {
                OutputPort port = plan.Ports[index];
                PortState? kept = old?.Ports.FirstOrDefault(state => state.Port.Address == port.Address && !handedOn.Contains(state));
                if (kept is null)
                {
                    ports[index] = new PortState(port);
                    continue;
                }

                handedOn.Add(kept);
                kept.Port = port;
                kept.Resend = old!.Plan.Priority != plan.Priority;   // a new priority goes out at once
                ports[index] = kept;
            }

            links.Add(new Link(plan, ports)
            {
                NextPoll = old?.NextPoll ?? TimeSpan.Zero,
                Reported = old?.Reported,
                Dmxbox = old?.Dmxbox ?? false,
            });
        }

        foreach (Link old in before.Values)
        {
            foreach (PortState port in old.Ports.Where(port => !handedOn.Contains(port)))
                End(old, port);
        }

        _links = links;
        _applied = plans;
    }

    private void Serve(Link link, TimeSpan now)
    {
        (string state, string detail) = link.Plan.Kind == OutputPlan.ArtNet ? ServeArtNet(link, now) : ServeSacn(link, now);
        if (link.Reported is { } reported && reported.State == state && reported.Detail == detail)
            return;

        link.Reported = (state, detail);
        _report(link.Plan.Identity, state, detail);
    }

    private (string State, string Detail) ServeArtNet(Link link, TimeSpan now)
    {
        OutputPlan plan = link.Plan;
        if (plan.Target != OutputPlan.Broadcast && _isBox(plan.Target))
            return (OutputView.Conflict, DmxboxSaid(plan.Target));

        ArtNetSocket socket;
        try
        {
            socket = _artNet();
        }
        catch (SocketException e)
        {
            return (OutputView.Missing, $"The desk cannot open its Art-Net socket: {e.Message}");
        }

        if (plan.Target == OutputPlan.Broadcast)
        {
            if (SharedNetwork(plan) is not { } network)
                return (OutputView.Missing, NoNetwork(plan));

            var target = new IPEndPoint(network.Broadcast, _settings.ArtNetPort);
            return SendArtNet(link, now, socket, target) ?? (OutputView.Sending, $"To {network.Broadcast}, the network of {Said(network)}");
        }

        var node = new IPEndPoint(IPAddress.Parse(plan.Target), _settings.ArtNetPort);
        if (now >= link.NextPoll)
        {
            link.NextPoll = now + PollInterval;
            try
            {
                socket.Poll(node);
            }
            catch (SocketException)
            {
                // The frames below fail the same way, and say why.
            }
        }

        // A node that calls itself a dmxbox gets nothing, for as long as it does not answer as
        // something else.
        ArtNetNode? answer = socket.AnswerOf(plan.Target);
        if (answer is not null)
            link.Dmxbox = IsDmxbox(answer);
        if (link.Dmxbox)
            return (OutputView.Conflict, DmxboxSaid(plan.Target));

        if (SendArtNet(link, now, socket, node) is { } failed)
            return failed;

        return answer is null
            ? (OutputView.Missing, "No answer to the Art-Net poll. The frames go out anyway; a firewall on this PC may hold the answers back.")
            : (OutputView.Connected, AboutNode(answer, plan));
    }

    // Sends each port of an Art-Net output as far as it is due. Null when all went out; the state
    // of a failure otherwise.
    private (string, string)? SendArtNet(Link link, TimeSpan now, ArtNetSocket socket, IPEndPoint target)
    {
        try
        {
            foreach (PortState port in link.Ports)
            {
                if (Due(link, port, now, target) is not { } levels)
                    continue;

                port.ArtNet ??= new ArtNetSender(socket.Socket, target, port.Port.Address);
                port.ArtNet.Target = target;
                port.ArtNet.Send(levels);
                Sent(port, now, target, levels);
            }

            return null;
        }
        catch (SocketException e)
        {
            return (OutputView.Missing, $"The frames cannot go out: {e.Message}");
        }
    }

    private (string State, string Detail) ServeSacn(Link link, TimeSpan now)
    {
        OutputPlan plan = link.Plan;
        Socket socket;
        string sending;
        if (plan.Target == OutputPlan.Multicast)
        {
            if (SharedNetwork(plan) is not { } network)
                return (OutputView.Missing, NoNetwork(plan));

            socket = MulticastSocket(network);
            sending = $"Multicast on the network of {Said(network)}";
        }
        else
        {
            socket = _sacnSocket ??= OwnSocket();
            sending = $"To {plan.Target}";
        }

        try
        {
            foreach (PortState port in link.Ports)
            {
                var target = new IPEndPoint(
                    plan.Target == OutputPlan.Multicast ? SacnSender.MulticastAddress(port.Port.Address) : IPAddress.Parse(plan.Target),
                    _settings.SacnPort);
                if (Due(link, port, now, target) is not { } levels)
                    continue;

                Sacn.Send(socket, target, port.Port.Address, levels, plan.Priority);
                port.Socket = socket;
                Sent(port, now, target, levels);
            }
        }
        catch (SocketException e)
        {
            return (OutputView.Missing, $"The packets cannot go out: {e.Message}");
        }

        return (OutputView.Sending, sending);
    }

    // The levels a port has to send now, or null when nothing is due. A port that plays no
    // universe sends nothing; an sACN universe it played is ended.
    private byte[]? Due(Link link, PortState port, TimeSpan now, IPEndPoint target)
    {
        int universe = port.Port.Universe;
        if (universe == 0)
        {
            if (port.SentTo is not null)
                End(link, port);
            return null;
        }

        byte[] levels = new byte[Desk.UniverseSize];
        if (universe * Desk.UniverseSize <= _output.Length)
            Array.Copy(_output, (universe - 1) * Desk.UniverseSize, levels, 0, Desk.UniverseSize);

        bool changed = port.Resend || port.Last is null || !levels.AsSpan().SequenceEqual(port.Last) || !target.Equals(port.SentTo);
        if (changed)
        {
            port.Resend = false;
            port.Repeats = Repeats;
            return levels;
        }

        if (port.Repeats > 0)
        {
            port.Repeats--;
            return levels;
        }

        return now - port.SentAt >= KeepAlive ? levels : null;
    }

    private static void Sent(PortState port, TimeSpan now, IPEndPoint target, byte[] levels)
    {
        port.Last = levels;
        port.SentAt = now;
        port.SentTo = target;
    }

    // A port stops: an sACN universe that went out is ended. Art-Net has no such packet; its node
    // keeps the last frame, or lets it go after a while, as it is set.
    private void End(Link link, PortState port)
    {
        if (link.Plan.Kind == OutputPlan.Sacn && port is { SentTo: { } target, Socket: { } socket, Last: { } levels })
        {
            try
            {
                Sacn.Terminate(socket, target, port.Port.Address, levels, link.Plan.Priority);
            }
            catch (Exception e) when (e is SocketException or ObjectDisposedException)
            {
                // The network is gone: the receivers let go of the universe after 2.5 seconds anyway.
            }
        }

        port.SentTo = null;
        port.Last = null;
    }

    // The network of a broadcast or multicast; null when there is none, or the desk sends from one
    // address only (a test desk).
    private PcNetwork? SharedNetwork(OutputPlan plan)
    {
        return _settings.Bind.Equals(IPAddress.Any) ? NetworkFor(plan, _pc.Networks) : null;
    }

    private string NoNetwork(OutputPlan plan)
    {
        if (!_settings.Bind.Equals(IPAddress.Any))
            return $"The desk sends from {_settings.Bind} only (the setting Outputs:Bind).";

        return plan.Network.Length == 0
            ? "This PC is in no network for it but those of the boxes' cables."
            : $"This PC is not in the network {plan.Network}.";
    }

    // A socket for the multicast of a network: it goes out through that network's adapter.
    private Socket MulticastSocket(PcNetwork network)
    {
        string key = network.Address.ToString();
        if (_multicastSockets.TryGetValue(key, out Socket? socket))
            return socket;

        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            socket.Bind(new IPEndPoint(network.Address, 0));
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, network.Address.GetAddressBytes());
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        _multicastSockets[key] = socket;
        return socket;
    }

    private Socket OwnSocket()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(_settings.Bind, 0));
        return socket;
    }

    /// <summary>True for a node that calls itself a dmxbox, as both kinds of the box do: "dmxbox", "dmxbox 2".</summary>
    public static bool IsDmxbox(ArtNetNode node)
    {
        return node.ShortName.StartsWith("dmxbox", StringComparison.OrdinalIgnoreCase);
    }

    private static string DmxboxSaid(string ip)
    {
        return $"{ip} is a dmxbox, which the desk plays with its own protocol, so nothing goes out here. "
            + "Remove this output, and give the box its universes in the dmxbox section.";
    }

    // "eDMX2 PRO, firmware 1.2", and the Port-Addresses this output sends to that no port of the
    // node listens to: the node would let those frames pass.
    private static string AboutNode(ArtNetNode node, OutputPlan plan)
    {
        string name = node.ShortName.Length > 0 ? node.ShortName : node.LongName;
        string about = $"{name}, firmware {node.Firmware}";
        int[] unheard = [.. plan.Ports.Where(port => port.Universe > 0 && !node.Ports.Contains(port.Address)).Select(port => port.Address).Distinct()];
        if (node.Ports.Length == 0 || unheard.Length == 0)
            return about;

        string addresses = string.Join(", ", unheard.Select(ArtNetSender.PortAddressText));
        string has = string.Join(", ", node.Ports.Select(ArtNetSender.PortAddressText));
        return $"{about}. No port of it listens to {addresses}; its ports are at {has}.";
    }

    // "192.168.1.20 (Wi-Fi)".
    private static string Said(PcNetwork network) => $"{network.Address} ({network.Adapter})";

    // An output being played.
    private sealed class Link(OutputPlan plan, PortState[] ports)
    {
        public OutputPlan Plan { get; } = plan;

        public PortState[] Ports { get; } = ports;

        public TimeSpan NextPoll { get; set; }

        public (string State, string Detail)? Reported { get; set; }

        // True once the node answered as a dmxbox, until it answers as something else.
        public bool Dmxbox { get; set; }
    }

    // What a port sent last, and where.
    private sealed class PortState(OutputPort port)
    {
        public OutputPort Port { get; set; } = port;

        public byte[]? Last { get; set; }

        public TimeSpan SentAt { get; set; }

        public IPEndPoint? SentTo { get; set; }

        public int Repeats { get; set; }

        public Socket? Socket { get; set; }

        public ArtNetSender? ArtNet { get; set; }

        // True when the port has to send in the next round, changed or not: its output got another priority.
        public bool Resend { get; set; }
    }
}
