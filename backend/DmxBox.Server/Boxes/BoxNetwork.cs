using System.Globalization;
using System.Net;

namespace DmxBox.Server.Boxes;

/// <summary>
/// All boxes the desk plays on: a <see cref="BoxLink"/> for every box it looks for, and the
/// universe every output of every box plays (<see cref="BoxWiring"/>). It looks for four kinds
/// of boxes: the box of the settings ("Box:Host", the box with the Pico at 192.168.7.1 unless they
/// say otherwise), the boxes the project lists, every box on a network adapter of this PC, and
/// every simulated box that runs (<see cref="Simulators"/>). A box with the number n on its switch
/// has the address 10.1.n.1, and gives the PC 10.1.n.2 on its USB cable: an adapter with an
/// address 10.1.n.x stands for that box.
/// </summary>
/// <remarks>
/// Every link has a thread of its own, see <see cref="BoxLink"/>. The UIs get the whole list of
/// boxes (<see cref="IDeskClient.BoxesChanged"/>) whenever a box comes or goes, or the wiring
/// changes.
/// </remarks>
internal sealed class BoxNetwork : BackgroundService
{
    // The first two numbers of a box's address.
    private const byte NetworkA = 10;
    private const byte NetworkB = 1;

    // How often the list of boxes to look for is made anew, besides the moments when the PC's
    // addresses or the project change. Only a safety net: those moments are reported.
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    // How long a link's thread gets to finish its request when the link stops.
    private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(3);

    private readonly Desk _desk;
    private readonly IDeskClient _clients;
    private readonly ThisPc _pc;
    private readonly string? _configured;
    private readonly IBox? _configuredBox;
    private readonly Func<string, IBox>? _boxAt;
    private readonly bool _discover;
    private readonly Simulators? _simulators;
    private readonly ILogger<BoxLink> _linkLogger;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, Running> _links = new(StringComparer.Ordinal);
    private readonly List<Task> _finishing = [];   // links that were stopped and are being waited for
    private bool _closed;

    // Replaced whole under the lock; read without it.
    private volatile BoxView[] _views = [];
    private volatile Dictionary<string, int[]> _plays = new(StringComparer.Ordinal);

    /// <summary>Prepares the network. No box is looked for until the server starts it.</summary>
    /// <param name="desk">The desk whose output the boxes play.</param>
    /// <param name="clients">All connected UIs. They get the list of boxes when it changes.</param>
    /// <param name="pc">This PC, for its addresses: an adapter in 10.1.n.x stands for the box 10.1.n.1.</param>
    /// <param name="host">The box of the settings: an IPv4 address or a host name.</param>
    /// <param name="port">The port of the box of the settings.</param>
    /// <param name="discover">False to leave the PC's adapters alone, for a test desk beside the real one.</param>
    /// <param name="box">The box of the settings; null for the real one. The tests pass a pretend box.</param>
    /// <param name="loggers">For the lines about the boxes.</param>
    /// <param name="boxAt">Makes the box for any other address; null for real ones. The tests make pretend boxes.</param>
    /// <param name="simulators">
    /// The simulated devices that run on this PC: a simulated box is looked for as long as it runs,
    /// also when <paramref name="discover"/> is false (a test desk has a folder of its own). Null for none.
    /// </param>
    public BoxNetwork(
        Desk desk,
        IDeskClient clients,
        ThisPc pc,
        string host,
        int port,
        bool discover,
        IBox? box,
        ILoggerFactory loggers,
        Func<string, IBox>? boxAt = null,
        Simulators? simulators = null)
    {
        _desk = desk;
        _clients = clients;
        _pc = pc;
        _discover = discover;
        _configuredBox = box;
        _boxAt = boxAt;
        _simulators = simulators;
        _linkLogger = loggers.CreateLogger<BoxLink>();
        _configured = BoxPlan.NormalAddress(port == BoxPlan.DefaultPort ? host : $"{host}:{port}");
        if (_configured is null)
        {
            loggers.CreateLogger<BoxNetwork>().LogWarning(
                "Box:Host \"{Host}\" is no address like 192.168.7.1 or a host name; the desk does not look for that box.", host);
        }
    }

    /// <summary>
    /// The boxes as the Boxes window shows them, with the universe each output plays: the project's
    /// boxes and the new ones, or every box that answers while the project lists none.
    /// </summary>
    public BoxView[] Boxes => _views;

    /// <summary>The DMX inputs of the boxes that answer and have one, by their names among the MIDI devices.</summary>
    public string[] InputNames()
    {
        lock (_lock)
        {
            return [.. _links.Values.Select(running => running.Link.InputName).OfType<string>().Distinct(StringComparer.Ordinal)];
        }
    }

    /// <summary>Listens to the DMX IN of that name (see <see cref="BoxLink.ListenToInput"/>); null when no box that answers has it.</summary>
    /// <param name="name">Its name, as <see cref="InputNames"/> has it.</param>
    /// <param name="changed">Called with each channel that changed and its level, on the box link's thread.</param>
    public InputListener? ListenToInput(string name, Action<int, byte> changed)
    {
        lock (_lock)
        {
            BoxLink? link = _links.Values.Select(running => running.Link).FirstOrDefault(found => found.InputName == name);
            return link?.ListenToInput(changed);
        }
    }

    /// <summary>
    /// Looks at what to look for again: the PC's adapters and the project's list. Starts a link for
    /// a box that is new to it, stops the link of a box it no longer has to look for, and tells the
    /// UIs when the list of boxes changed. Any thread may call this.
    /// </summary>
    public void Refresh()
    {
        lock (_lock)
        {
            if (_closed)
                return;

            HashSet<string> wanted = Wanted();
            _finishing.RemoveAll(task => task.IsCompleted);
            foreach (string address in _links.Keys.Where(address => !wanted.Contains(address)).ToList())
            {
                // A link that stops may be in the middle of a request: it is waited for on another
                // thread, and once more when the server stops.
                Running running = _links[address];
                _links.Remove(address);
                running.Stop.Cancel();
                _finishing.Add(Task.Run(() => Finish(running)));
            }

            foreach (string address in wanted.Where(address => !_links.ContainsKey(address)))
                Start(address);

            RewireLocked();
        }
    }

    /// <summary>Stops every link, waiting for each, and closes its box.</summary>
    public override void Dispose()
    {
        StopAll();
        base.Dispose();
    }

    /// <summary>Looks for the boxes until the server stops.</summary>
    /// <param name="stoppingToken">Signals that the server is shutting down.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _pc.Changed += Refresh;
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
            _pc.Changed -= Refresh;
            StopAll();
        }
    }

    /// <summary>
    /// The box on a network of this PC: 10.1.n.1 for the PC's address 10.1.n.x. Null for any
    /// other address, and for 10.1.n.1 itself, which would be the box and not the PC.
    /// </summary>
    /// <param name="pcAddress">An address of this PC.</param>
    internal static string? BoxBeside(IPAddress pcAddress)
    {
        byte[] bytes = ThisPc.Plain(pcAddress).GetAddressBytes();
        if (bytes.Length != 4 || bytes[0] != NetworkA || bytes[1] != NetworkB || bytes[3] == 1)
            return null;

        return string.Create(CultureInfo.InvariantCulture, $"{NetworkA}.{NetworkB}.{bytes[2]}.1");
    }

    /// <summary>
    /// True for an address on the USB cable of a box: 192.168.7.x, the network of the box with the
    /// Pico, or 10.1.n.x, the network of the box with the number n on its switch. A broadcast or a
    /// multicast of the outputs never goes out there.
    /// </summary>
    /// <param name="address">An IPv4 address, of this PC or of another device.</param>
    internal static bool IsBoxCable(IPAddress address)
    {
        byte[] bytes = ThisPc.Plain(address).GetAddressBytes();
        return bytes.Length == 4
            && ((bytes[0] == NetworkA && bytes[1] == NetworkB) || (bytes[0] == 192 && bytes[1] == 168 && bytes[2] == 7));
    }

    /// <summary>
    /// True for the address of a dmxbox, which the desk plays with its own protocol, so that no
    /// Art-Net may go there as well: 192.168.7.1 and 10.1.n.1, where the boxes always are, and the
    /// address of any box the desk looks for (the box of the settings, the project's boxes).
    /// </summary>
    /// <param name="host">An IPv4 address, "192.168.1.50".</param>
    public bool IsBoxAddress(string host)
    {
        if (IPAddress.TryParse(host, out IPAddress? ip) && IsBoxCable(ip) && ip.GetAddressBytes()[3] == 1)
            return true;

        lock (_lock)
        {
            if (_links.Keys.Any(address => HostOf(address) == host))
                return true;
        }

        return (_configured is not null && HostOf(_configured) == host) || _desk.Boxes.Any(plan => HostOf(plan.Address) == host);
    }

    // The host of a box's address, without the port: "127.0.0.1" of "127.0.0.1:7001".
    private static string HostOf(string address)
    {
        int colon = address.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? address : address[..colon];
    }

    // The box of the settings, the project's boxes and the boxes on the PC's adapters.
    private HashSet<string> Wanted()
    {
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        if (_configured is not null)
            wanted.Add(_configured);

        foreach (BoxPlan plan in _desk.Boxes)
            wanted.Add(plan.Address);

        if (_discover)
        {
            foreach (IPAddress address in _pc.LanAddresses)
            {
                if (BoxBeside(address) is { } box)
                    wanted.Add(box);
            }
        }

        // A simulated box is plugged in for as long as its simulator runs.
        foreach (string address in _simulators?.AddressesOf(SimulatedDevice.Dmxbox) ?? [])
        {
            if (BoxPlan.NormalAddress(address) is { } box)
                wanted.Add(box);
        }

        return wanted;
    }

    // Call with the lock held.
    private void Start(string address)
    {
        IBox box;
        if (address == _configured && _configuredBox is not null)
        {
            box = _configuredBox;
        }
        else if (_boxAt is not null)
        {
            box = _boxAt(address);
        }
        else
        {
            int colon = address.IndexOf(':', StringComparison.Ordinal);
            box = colon < 0
                ? new RealBox(address, BoxPlan.DefaultPort)
                : new RealBox(address[..colon], int.Parse(address[(colon + 1)..], CultureInfo.InvariantCulture));
        }

        var link = new BoxLink(_desk, box, _clients, address, _linkLogger, () => PlaysOf(address), Rewire);
        var stop = new CancellationTokenSource();
        var thread = new Thread(() => link.Run(stop.Token))
        {
            IsBackground = true,
            Name = $"Box {address}",
        };
        _links[address] = new Running(link, thread, stop);
        thread.Start();
    }

    // The universe each output of a box plays now; nothing for a box that plays none.
    private int[] PlaysOf(string address)
    {
        return _plays.TryGetValue(address, out int[]? plays) ? plays : [];
    }

    // A link's box came or went, or the project's universes or boxes may have changed. Any thread.
    private void Rewire()
    {
        lock (_lock)
        {
            if (!_closed)
                RewireLocked();
        }
    }

    // The links that play something else now send their output again; the UIs hear of a change.
    private void RewireLocked()
    {
        BoxStatus[] links = [.. _links.Values.Select(running => running.Link.Status)];
        BoxView[] views = BoxWiring.Views(_desk.Boxes, links, _desk.Universes);
        var plays = views.ToDictionary(view => view.Address, view => view.Universes, StringComparer.Ordinal);
        foreach ((string address, Running running) in _links)
        {
            int[] before = _plays.GetValueOrDefault(address) ?? [];
            int[] after = plays.GetValueOrDefault(address) ?? [];
            if (!before.AsSpan().SequenceEqual(after))
                running.Link.ResendSoon();
        }

        _plays = plays;
        if (views.Length == _views.Length && views.Zip(_views).All(pair => pair.First.SameAs(pair.Second)))
            return;

        _views = views;

        // Not awaited: a browser that is slow to read its messages must not hold up the lights.
        _ = _clients.BoxesChanged(views);
    }

    // Every link stops at once; then each is waited for, those stopped before too, so that none
    // writes to the log after the server has closed it.
    private void StopAll()
    {
        List<Running> stopped;
        Task[] finishing;
        lock (_lock)
        {
            if (_closed)
                return;

            _closed = true;
            stopped = [.. _links.Values];
            _links.Clear();
            foreach (Running running in stopped)
                running.Stop.Cancel();

            finishing = [.. _finishing];
        }

        foreach (Running running in stopped)
            Finish(running);

        Task.WaitAll(finishing, StopWait);
    }

    // Waits for a link's thread and closes its box.
    private static void Finish(Running running)
    {
        running.Thread.Join(StopWait);
        running.Link.Dispose();
        running.Stop.Dispose();
    }

    private sealed record Running(BoxLink Link, Thread Thread, CancellationTokenSource Stop);
}
