using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace DmxBox.Sim;

/// <summary>
/// A simulated Art-Net node: at an address of its own on this PC (127.0.0.2 and on), at the
/// Art-Net port, with 1 to 4 ports that put out DMX. It answers every ArtPoll with its ArtPollReply,
/// so Find nodes in the desk lists it, and keeps the frames of the Port-Addresses of its ports;
/// it writes the levels of each port when they change, and when the frames stop.
/// </summary>
internal sealed class ArtNetNodeSimulator : ISimulator
{
    // A port that gets no frame for this long has lost its source (the desk sends at least every 0.8 s).
    private static readonly TimeSpan Silence = TimeSpan.FromSeconds(2);

    // At most this often a line for a port.
    private static readonly TimeSpan LineEvery = TimeSpan.FromMilliseconds(200);

    private readonly int _port;
    private readonly int _show;
    private readonly TextWriter _output;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Lock _lock = new();
    private readonly PortState[] _ports;
    private readonly SimulatorClaim _claim;
    private Socket? _socket;
    private Thread? _thread;
    private int _polls;
    private int _pollsShown;

    /// <summary>Prepares the node. Nothing runs until it is plugged in.</summary>
    /// <param name="folder">The registry of the simulators.</param>
    /// <param name="name">Its short name, as Find nodes shows it.</param>
    /// <param name="ports">The Port-Addresses of its ports, 1 to 4, all with the Net and Sub-Net of the first.</param>
    /// <param name="address">Its address; null for the first free one from 127.0.0.2 on.</param>
    /// <param name="port">The Art-Net port it listens at: 6454, the one of the specification.</param>
    /// <param name="show">How many channels of each port it writes when they change.</param>
    /// <param name="output">Where the lines go.</param>
    public ArtNetNodeSimulator(
        string folder, string name, int[] ports, IPAddress? address, int port, int show, TextWriter output)
    {
        _port = port;
        _show = Math.Clamp(show, 1, 512);
        _output = output;
        ArtNetDiscovery.BuildPollReply(new ArtNetNode("127.0.0.2", name, "", ports[0], "1.0", "") { Ports = ports });   // refuses ports that one reply cannot say
        Title = $"Art-Net node \"{name}\", ports at {string.Join(", ", ports.Select(ArtNetSender.PortAddressText))}";
        (Address, _claim) = Shown.ClaimLoopbackAddress(folder, SimulatedDevice.ArtNet, $"{Title} (simulator)", address);
        Node = new ArtNetNode(Address.ToString(), name, $"{name}, a simulated Art-Net node of Lumenet", ports[0], "1.0", "02:4c:75:6d:00:01")
        {
            Ports = ports,
        };
        _ports = [.. ports.Select(portAddress => new PortState(portAddress))];
    }

    /// <summary>Its address on this PC.</summary>
    public IPAddress Address { get; }

    /// <summary>The node as its ArtPollReply says it.</summary>
    public ArtNetNode Node { get; }

    /// <inheritdoc />
    public string Title { get; }

    /// <inheritdoc />
    public string Finding => string.Create(
        CultureInfo.InvariantCulture,
        $"It is at {Address}, port {_port}. In the desk: Outputs, Art-Net, Find nodes lists it; Add plays on it.");

    /// <inheritdoc />
    public IReadOnlyList<(char Key, string What)> Keys { get; } = [];

    /// <inheritdoc />
    public bool PluggedIn => _socket is not null;

    /// <summary>The levels the port at a Port-Address got last; null before the first frame, or for no such port.</summary>
    /// <param name="portAddress">The Port-Address.</param>
    public byte[]? LevelsOf(int portAddress)
    {
        lock (_lock)
        {
            return _ports.FirstOrDefault(port => port.Address == portAddress)?.Levels;
        }
    }

    /// <summary>How many polls it has answered.</summary>
    public int Polls
    {
        get
        {
            lock (_lock)
            {
                return _polls;
            }
        }
    }

    /// <inheritdoc />
    public void PlugIn()
    {
        if (PluggedIn)
            return;

        _socket = Shown.SharedSocket(Address, _port);
        Socket socket = _socket;
        _thread = new Thread(() => Listen(socket)) { IsBackground = true, Name = "Art-Net node" };
        _thread.Start();
        _claim.PluggedIn = true;
    }

    /// <inheritdoc />
    public void PullOut()
    {
        _claim.PluggedIn = false;
        Socket? socket = _socket;
        _socket = null;
        socket?.Dispose();
        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
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
            if (_polls > 0 && _pollsShown == 0)
                lines.Add("Polled: it answers with its name and its ports.");
            _pollsShown = _polls;

            foreach (PortState port in _ports)
            {
                if (port.Levels is null)
                    continue;

                if (now - port.At > Silence)
                {
                    if (!port.SilenceShown)
                        lines.Add($"Port {ArtNetSender.PortAddressText(port.Address)}: no frame for {Silence.TotalSeconds} seconds.");
                    port.SilenceShown = true;
                    continue;
                }

                port.SilenceShown = false;
                bool soon = port.ShownAt is { } shownAt && now - shownAt < LineEvery;
                bool same = port.Shown is not null && port.Levels.AsSpan(0, _show).SequenceEqual(port.Shown.AsSpan(0, _show));
                if (soon || same)
                    continue;

                port.Shown = port.Levels;
                port.ShownAt = now;
                lines.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Port {ArtNetSender.PortAddressText(port.Address)}, frame {port.Frames} from {port.From}, 1-{_show}:{Shown.Levels(port.Levels, _show)}"));
            }
        }

        foreach (string line in lines)
            _output.WriteLine(line);
    }

    /// <summary>Pulls the node out for good, and gives its address back.</summary>
    public void Dispose()
    {
        PullOut();
        _claim.Dispose();
    }

    // Answers the polls and keeps the frames, until the socket is closed.
    private void Listen(Socket socket)
    {
        byte[] buffer = new byte[2048];
        byte[] reply = ArtNetDiscovery.BuildPollReply(Node);
        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
        while (true)
        {
            int length;
            try
            {
                length = socket.ReceiveFrom(buffer, ref from);
            }
            catch (SocketException)
            {
                continue;   // nothing came within the read's time
            }
            catch (ObjectDisposedException)
            {
                return;     // pulled out
            }

            Take(buffer.AsSpan(0, length), (IPEndPoint)from, socket, reply);
        }
    }

    private void Take(ReadOnlySpan<byte> datagram, IPEndPoint from, Socket socket, byte[] reply)
    {
        if (datagram.Length < 12 || !datagram[..8].SequenceEqual("Art-Net\0"u8))
            return;

        ushort opCode = BinaryPrimitives.ReadUInt16LittleEndian(datagram[8..]);
        if (opCode == 0x2000)
        {
            lock (_lock)
            {
                _polls++;
            }

            try
            {
                // To the address and port the poll came from: that is where the desk listens.
                socket.SendTo(reply, from);
            }
            catch (Exception e) when (e is SocketException or ObjectDisposedException)
            {
                // Pulled out meanwhile.
            }
        }
        else if (opCode == 0x5000 && datagram.Length >= 20)
        {
            int portAddress = BinaryPrimitives.ReadUInt16LittleEndian(datagram[14..]) & 0x7FFF;
            int length = Math.Min(BinaryPrimitives.ReadUInt16BigEndian(datagram[16..]), datagram.Length - 18);
            lock (_lock)
            {
                PortState? port = _ports.FirstOrDefault(state => state.Address == portAddress);
                if (port is null)
                    return;   // a Port-Address none of its ports listens to

                byte[] levels = new byte[512];
                datagram.Slice(18, Math.Min(length, 512)).CopyTo(levels);
                port.Levels = levels;
                port.Frames++;
                port.At = _clock.Elapsed;
                port.From = from.Address.ToString();
            }
        }
    }

    // What one port got.
    private sealed class PortState(int address)
    {
        public int Address { get; } = address;

        public byte[]? Levels { get; set; }

        public byte[]? Shown { get; set; }

        public long Frames { get; set; }

        public TimeSpan At { get; set; }

        public TimeSpan? ShownAt { get; set; }

        public string From { get; set; } = "";

        public bool SilenceShown { get; set; }
    }
}
