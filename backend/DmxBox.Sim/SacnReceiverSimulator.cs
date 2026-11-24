using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace DmxBox.Sim;

/// <summary>
/// A simulated sACN (E1.31) receiver: at an address of its own on this PC (127.0.0.2 and on), at
/// the sACN port, and if asked, in the multicast groups of its universes as well. It keeps every
/// source of each universe (name, CID, priority, sequence) and writes the levels of the source a
/// real receiver would take, the one with the highest priority; it says when a source ends its
/// stream, and when it falls silent for 2.5 seconds, after which receivers take it for lost.
/// </summary>
internal sealed class SacnReceiverSimulator : ISimulator
{
    // The silence after which a receiver gives a source up (E1.31's network data loss timeout).
    private static readonly TimeSpan Loss = TimeSpan.FromSeconds(2.5);

    private static readonly TimeSpan LineEvery = TimeSpan.FromMilliseconds(200);

    private readonly SimulatorClaim _claim;
    private readonly HashSet<int> _universes;
    private readonly bool _multicast;
    private readonly int _port;
    private readonly int _show;
    private readonly TextWriter _output;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Lock _lock = new();
    private readonly Dictionary<(int Universe, Guid Cid), Source> _sources = [];
    private readonly List<string> _news = [];
    private readonly List<Socket> _sockets = [];
    private readonly List<Thread> _threads = [];

    /// <summary>Prepares the receiver. Nothing runs until it is plugged in.</summary>
    /// <param name="folder">The registry of the simulators.</param>
    /// <param name="universes">The sACN universes it takes, 1 to 63999.</param>
    /// <param name="multicast">
    /// True to join the multicast groups of its universes on every network of this PC too. That
    /// listens on every address of the PC, and Windows may ask about its firewall.
    /// </param>
    /// <param name="address">Its address; null for the first free one from 127.0.0.2 on.</param>
    /// <param name="port">The sACN port, 5568.</param>
    /// <param name="show">How many slots of each universe it writes when they change.</param>
    /// <param name="output">Where the lines go.</param>
    public SacnReceiverSimulator(
        string folder, int[] universes, bool multicast, IPAddress? address, int port, int show, TextWriter output)
    {
        _universes = [.. universes];
        _multicast = multicast;
        _port = port;
        _show = Math.Clamp(show, 1, 512);
        _output = output;
        (Address, _claim) = Shown.ClaimLoopbackAddress(folder, SimulatedDevice.Sacn, $"{Title} (simulator)", address);
    }

    /// <summary>Its address on this PC, for sACN sent to it alone.</summary>
    public IPAddress Address { get; }

    /// <inheritdoc />
    public string Title => $"sACN receiver of universe{(_universes.Count == 1 ? "" : "s")} {UniversesText()}";

    /// <inheritdoc />
    public string Finding => string.Create(
        CultureInfo.InvariantCulture,
        $"It is at {Address}, port {_port}. In the desk: Outputs, sACN, Add with {Address} as the receiver. {(_multicast ? "It takes the desk's multicast too." : "With --multicast it takes multicast too.")}");

    /// <inheritdoc />
    public IReadOnlyList<(char Key, string What)> Keys { get; } = [];

    /// <inheritdoc />
    public bool PluggedIn => _sockets.Count > 0;

    /// <summary>The sources of a universe that sent within 2.5 seconds: name, priority, and the last levels.</summary>
    /// <param name="universe">The sACN universe.</param>
    public IReadOnlyList<(string Name, int Priority, byte Sequence, bool Terminated, byte[] Levels)> SourcesOf(int universe)
    {
        lock (_lock)
        {
            TimeSpan now = _clock.Elapsed;
            return [.. _sources.Where(pair => pair.Key.Universe == universe && now - pair.Value.At <= Loss)
                .Select(pair => (pair.Value.Name, pair.Value.Priority, pair.Value.Sequence, pair.Value.Terminated, pair.Value.Levels))];
        }
    }

    /// <inheritdoc />
    public void PlugIn()
    {
        if (PluggedIn)
            return;

        Listen(Shown.SharedSocket(Address, _port));
        if (_multicast)
        {
            Socket socket = Shown.SharedSocket(IPAddress.Any, _port);
            foreach (IPAddress adapter in AdapterAddresses())
            {
                foreach (int universe in _universes)
                {
                    try
                    {
                        socket.SetSocketOption(
                            SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(SacnSender.MulticastAddress(universe), adapter));
                    }
                    catch (SocketException)
                    {
                        // An adapter that takes no multicast: the others do.
                    }
                }
            }

            Listen(socket);
        }

        _claim.PluggedIn = true;
    }

    /// <inheritdoc />
    public void PullOut()
    {
        _claim.PluggedIn = false;
        foreach (Socket socket in _sockets)
            socket.Dispose();
        foreach (Thread thread in _threads)
            thread.Join(TimeSpan.FromSeconds(2));
        _sockets.Clear();
        _threads.Clear();
    }

    /// <inheritdoc />
    public void Press(char key)
    {
    }

    /// <inheritdoc />
    public void Tick()
    {
        TimeSpan now = _clock.Elapsed;
        var lines = new List<string>();
        lock (_lock)
        {
            lines.AddRange(_news);
            _news.Clear();
            foreach (((int universe, Guid cid), Source source) in _sources.OrderBy(pair => pair.Key.Universe))
            {
                if (now - source.At > Loss)
                {
                    if (!source.LossShown && !source.Terminated)
                        lines.Add($"Universe {universe}: nothing from \"{source.Name}\" for {Loss.TotalSeconds} seconds, so it counts as lost.");
                    source.LossShown = true;
                    continue;
                }

                // A receiver plays the source with the highest priority; another one is only listed.
                bool taken = !_sources.Any(other => other.Key.Universe == universe && other.Key.Cid != cid
                    && now - other.Value.At <= Loss && other.Value.Priority > source.Priority);
                bool soon = source.ShownAt is { } shownAt && now - shownAt < LineEvery;
                bool same = source.Shown is not null && source.Levels.AsSpan(0, _show).SequenceEqual(source.Shown.AsSpan(0, _show));
                if (!taken || soon || same || source.Terminated)
                    continue;

                source.Shown = source.Levels;
                source.ShownAt = now;
                lines.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Universe {universe} from \"{source.Name}\", priority {source.Priority}, sequence {source.Sequence}, 1-{_show}:{Shown.Levels(source.Levels, _show)}"));
            }
        }

        foreach (string line in lines)
            _output.WriteLine(line);
    }

    /// <summary>Pulls the receiver out for good, and gives its address back.</summary>
    public void Dispose()
    {
        PullOut();
        _claim.Dispose();
    }

    private void Listen(Socket socket)
    {
        _sockets.Add(socket);
        var thread = new Thread(() => Receive(socket)) { IsBackground = true, Name = "sACN receiver" };
        _threads.Add(thread);
        thread.Start();
    }

    private void Receive(Socket socket)
    {
        byte[] buffer = new byte[2048];
        while (true)
        {
            int length;
            try
            {
                length = socket.Receive(buffer);
            }
            catch (SocketException)
            {
                continue;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            Take(buffer.AsSpan(0, length));
        }
    }

    // A data packet of E1.31 (see DEV.md, section 6); anything else is passed over.
    private void Take(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 126 || !packet[4..16].SequenceEqual("ASC-E1.17\0\0\0"u8)
            || BinaryPrimitives.ReadUInt32BigEndian(packet[18..]) != 0x00000004
            || BinaryPrimitives.ReadUInt32BigEndian(packet[40..]) != 0x00000002
            || packet[117] != 0x02 || packet[125] != 0)
        {
            return;
        }

        int universe = BinaryPrimitives.ReadUInt16BigEndian(packet[113..]);
        if (!_universes.Contains(universe))
            return;

        var cid = new Guid(packet.Slice(22, 16), bigEndian: true);
        string name = Encoding.UTF8.GetString(packet.Slice(44, 64)).TrimEnd('\0');
        byte sequence = packet[111];
        bool terminated = (packet[112] & 0x40) != 0;
        int count = Math.Min(BinaryPrimitives.ReadUInt16BigEndian(packet[123..]) - 1, packet.Length - 126);
        byte[] levels = new byte[512];
        packet.Slice(126, Math.Clamp(count, 0, 512)).CopyTo(levels);

        lock (_lock)
        {
            TimeSpan now = _clock.Elapsed;
            if (!_sources.TryGetValue((universe, cid), out Source? source) || now - source.At > Loss || source.Terminated)
            {
                source = new Source(name);
                _sources[(universe, cid)] = source;
                if (!terminated)
                    _news.Add($"Universe {universe}: a source \"{name}\" (CID {cid}) begins.");
            }
            else if ((sbyte)(sequence - source.Sequence) is <= 0 and > -20)
            {
                return;   // an old packet that came late: a receiver drops it, as E1.31 says
            }

            source.Priority = packet[108];
            source.Sequence = sequence;
            source.At = now;
            source.Levels = levels;
            source.LossShown = false;
            if (terminated && !source.Terminated)
            {
                source.Terminated = true;
                _news.Add($"Universe {universe}: \"{name}\" ends its stream (Stream_Terminated); a receiver lets it go at once.");
            }
        }
    }

    private string UniversesText()
    {
        int[] sorted = [.. _universes.Order()];
        var parts = new List<string>();
        for (int index = 0; index < sorted.Length;)
        {
            int end = index;
            while (end + 1 < sorted.Length && sorted[end + 1] == sorted[end] + 1)
                end++;
            parts.Add(end == index ? $"{sorted[index]}" : $"{sorted[index]} to {sorted[end]}");
            index = end + 1;
        }

        return string.Join(", ", parts);
    }

    // The IPv4 address of every adapter that is up, the loopback one included.
    private static List<IPAddress> AdapterAddresses()
    {
        var addresses = new List<IPAddress>();
        foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up)
                continue;
            foreach (UnicastIPAddressInformation unicast in adapter.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                    addresses.Add(unicast.Address);
            }
        }

        return addresses;
    }

    // What one source of one universe sent last.
    private sealed class Source(string name)
    {
        public string Name { get; } = name;

        public int Priority { get; set; }

        public byte Sequence { get; set; }

        public TimeSpan At { get; set; }

        public byte[] Levels { get; set; } = new byte[512];

        public byte[]? Shown { get; set; }

        public TimeSpan? ShownAt { get; set; }

        public bool Terminated { get; set; }

        public bool LossShown { get; set; }
    }
}
