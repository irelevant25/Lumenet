using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace DmxBox.Server;

/// <summary>
/// The one socket of the desk program for Art-Net: the frames and the polls go out through it, and
/// the nodes' answers to the polls come in on it. A thread of its own reads the answers and keeps
/// the latest of each node.
/// </summary>
/// <remarks>
/// <para>
/// Nodes answer a poll at the Art-Net port 6454, whatever port it came from, so the socket listens
/// there. Another Art-Net program on this PC may listen there too: the socket shares the port. If
/// the port cannot be had at all, it takes any free one, and only nodes that answer to the port
/// of the poll are heard.
/// </para>
/// <para>Thread-safe.</para>
/// </remarks>
internal sealed class ArtNetSocket : IDisposable
{
    // A node that answered a poll within this time counts as there. Nodes are polled every 2 seconds.
    public static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(5);

    // The answers of a node with more than four ports, which come one after the other, are put together.
    private static readonly TimeSpan SameAnswer = TimeSpan.FromSeconds(1);

    private readonly Socket _socket;
    private readonly Thread _thread;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Lock _lock = new();
    private readonly Dictionary<string, (ArtNetNode Node, TimeSpan At)> _answers = new(StringComparer.Ordinal);
    private volatile bool _stopping;

    /// <summary>Opens the socket and starts reading the answers.</summary>
    /// <param name="bind">The address of this PC to send from: any (0.0.0.0), or 127.0.0.1 for a test desk.</param>
    /// <param name="listenPort">The port to listen at: 6454, or 0 for any free one.</param>
    /// <param name="logger">Gets a line when the port cannot be had.</param>
    public ArtNetSocket(IPAddress bind, int listenPort, ILogger logger)
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { EnableBroadcast = true };
        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        try
        {
            _socket.Bind(new IPEndPoint(bind, listenPort));
        }
        catch (SocketException e) when (listenPort != 0)
        {
            logger.LogWarning(
                "The Art-Net port {Port} is taken ({Reason}); the desk listens at another one, and may not hear every node.",
                listenPort, e.Message);
            _socket.Bind(new IPEndPoint(bind, 0));
        }

        // Half a second at most in a read, so that the thread sees the end in time.
        _socket.ReceiveTimeout = 500;
        _thread = new Thread(Listen) { IsBackground = true, Name = "Art-Net answers" };
        _thread.Start();
    }

    /// <summary>The socket, for the frames and the polls.</summary>
    public Socket Socket => _socket;

    /// <summary>Time on the socket's own clock, which only runs forward.</summary>
    public TimeSpan Now => _clock.Elapsed;

    /// <summary>The latest answer of the node at an address, if it answered within <see cref="AnswerTimeout"/>.</summary>
    /// <param name="ip">The node's IPv4 address.</param>
    public ArtNetNode? AnswerOf(string ip)
    {
        lock (_lock)
        {
            return _answers.TryGetValue(ip, out var answer) && Now - answer.At <= AnswerTimeout ? answer.Node : null;
        }
    }

    /// <summary>Every node that answered since a moment of <see cref="Now"/>, in the order of their addresses.</summary>
    /// <param name="since">The moment the search began.</param>
    public ArtNetNode[] AnswersSince(TimeSpan since)
    {
        lock (_lock)
        {
            return [.. _answers.Values.Where(answer => answer.At >= since).Select(answer => answer.Node)];
        }
    }

    /// <summary>Sends a poll: every node it reaches answers.</summary>
    /// <param name="target">A node, or a broadcast address, at the Art-Net port.</param>
    public void Poll(IPEndPoint target)
    {
        _socket.SendTo(ArtNetDiscovery.BuildPoll(), target);
    }

    /// <summary>Stops reading and closes the socket.</summary>
    public void Dispose()
    {
        _stopping = true;
        _thread.Join(TimeSpan.FromSeconds(2));
        _socket.Dispose();
    }

    // The thread: reads until the socket is closed, and keeps each node's latest answer.
    private void Listen()
    {
        byte[] buffer = new byte[2048];
        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
        while (!_stopping)
        {
            int length;
            try
            {
                length = _socket.ReceiveFrom(buffer, ref from);
            }
            catch (SocketException)
            {
                // Nothing came within the half second, or (on Windows) an earlier datagram found no
                // listener and the system says so here. Neither is an answer.
                continue;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (ArtNetDiscovery.TryParseReply(buffer.AsSpan(0, length), out ArtNetNode? node))
                Keep(node, ((IPEndPoint)from).Address.ToString());
        }
    }

    // A node's answer, under the address it came from: that is where the node is reached, also
    // when the address it gives is another one (behind a router that rewrites addresses).
    private void Keep(ArtNetNode node, string from)
    {
        lock (_lock)
        {
            TimeSpan now = Now;
            ArtNetNode kept = node with { Ip = from };
            if (_answers.TryGetValue(from, out var earlier) && now - earlier.At <= SameAnswer)
            {
                var both = new List<ArtNetNode> { earlier.Node };
                ArtNetDiscovery.Add(both, kept);
                kept = both[0];
            }

            _answers[from] = (kept, now);
        }
    }
}
