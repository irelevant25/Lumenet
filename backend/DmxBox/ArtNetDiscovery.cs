using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace DmxBox;

/// <summary>Finds Art-Net nodes on the network. Works for any Art-Net node, not only for the box.</summary>
public static class ArtNetDiscovery
{
    /// <summary>The address that reaches every node on the local network.</summary>
    public const string BroadcastAddress = "255.255.255.255";

    // An ArtPollReply up to and including the MAC address. Shorter replies are ignored.
    private const int MinReplyLength = 207;

    // The fields of an ArtPollReply that say what the node's ports are: how many there are, what
    // each can do (bit 7: it puts out DMX), and the low four bits of each output's Port-Address.
    private const int NumPortsOffset = 173;
    private const int PortTypesOffset = 174;
    private const int GoodOutputOffset = 182;
    private const int SwOutOffset = 190;
    private const int MaxReplyPorts = 4;
    private const byte OutputsDmx = 0x80;

    // The whole ArtPollReply of Art-Net 4.
    private const int ReplyLength = 239;

    /// <summary>Sends an ArtPoll and collects the ArtPollReply packets that come back.</summary>
    /// <param name="timeout">How long to listen for replies. Default: 1 second.</param>
    /// <param name="target">
    /// Where to send the poll: a broadcast address, or the address of one node. With the default
    /// (<see cref="BroadcastAddress"/>) the poll goes out through every network adapter of the
    /// computer, so a box on its USB cable is found as well as nodes on the normal network.
    /// </param>
    /// <param name="port">Art-Net UDP port.</param>
    /// <returns>The nodes that answered, in the order of their replies, each one once.</returns>
    /// <exception cref="SocketException">The poll could not be sent at all.</exception>
    public static IReadOnlyList<ArtNetNode> Discover(
        TimeSpan? timeout = null,
        string target = BroadcastAddress,
        int port = ArtNetSender.DefaultPort)
    {
        TimeSpan listenTime = timeout ?? TimeSpan.FromSeconds(1);
        IPEndPoint endPoint = Udp.Resolve(target, port);

        List<Socket> sockets = SendPoll(endPoint);
        try
        {
            var nodes = new List<ArtNetNode>();
            byte[] buffer = new byte[Udp.MaxDatagramLength];
            var listened = Stopwatch.StartNew();
            for (TimeSpan rest = listenTime; rest > TimeSpan.Zero; rest = listenTime - listened.Elapsed)
            {
                // Wait until one of the sockets has something. Select() removes the others from the list.
                var readable = new List<Socket>(sockets);
                Socket.Select(readable, null, null, rest);

                foreach (Socket socket in readable)
                {
                    if (Udp.TryReceive(socket, buffer, TimeSpan.FromMilliseconds(1), out int length)
                        && TryParseReply(buffer.AsSpan(0, length), out ArtNetNode? node))
                    {
                        Add(nodes, node);
                    }
                }
            }

            return nodes;
        }
        finally
        {
            foreach (Socket socket in sockets)
                socket.Dispose();
        }
    }

    /// <summary>
    /// Puts a node that answered into a list of nodes: a node that answered before (by its
    /// address) gets the ports of this answer too, which is how a node with more than four ports
    /// answers; another node joins the list at its end.
    /// </summary>
    /// <param name="nodes">The nodes found so far, in the order of their first answers.</param>
    /// <param name="node">The node of one answer.</param>
    public static void Add(List<ArtNetNode> nodes, ArtNetNode node)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(node);
        int index = nodes.FindIndex(known => known.Ip == node.Ip);
        if (index < 0)
        {
            nodes.Add(node);
            return;
        }

        ArtNetNode known = nodes[index];
        nodes[index] = known with { Ports = [.. known.Ports, .. node.Ports.Where(port => !known.Ports.Contains(port))] };
    }

    /// <summary>
    /// An ArtPoll: "every node, answer this poll", for a program that sends it through a socket of
    /// its own, to a node or to a broadcast address, at the Art-Net port.
    /// </summary>
    public static byte[] BuildPoll()
    {
        // The header, then two zero bytes (Flags and DiagPriority): "just answer this poll".
        byte[] poll = new byte[ArtNetPacket.HeaderLength + 2];
        ArtNetPacket.WriteHeader(poll, ArtNetPacket.OpPoll);
        return poll;
    }

    /// <summary>
    /// The ArtPollReply of a node, as a node sends it when it is polled (239 bytes, Art-Net 4): for a
    /// program that plays a node, such as the simulator of sim.cmd. Its ports put out DMX; they
    /// all have the Net and Sub-Net of the first, as one reply can only say.
    /// </summary>
    /// <param name="node">The node: its address, names, firmware ("1.0"), MAC and the Port-Addresses of its ports, 1 to 4 of them.</param>
    /// <exception cref="ArgumentException">The node has no ports, more than four, or ports in different Nets or Sub-Nets.</exception>
    public static byte[] BuildPollReply(ArtNetNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        int[] ports = node.Ports;
        if (ports.Length is < 1 or > MaxReplyPorts)
            throw new ArgumentException($"A reply has 1 to {MaxReplyPorts} ports, not {ports.Length}.", nameof(node));
        if (ports.Any(port => port >> 4 != ports[0] >> 4))
            throw new ArgumentException("The ports of one reply have one Net and one Sub-Net.", nameof(node));

        byte[] reply = new byte[ReplyLength];
        ArtNetPacket.WriteHeader(reply, ArtNetPacket.OpPollReply);
        byte[] ip = IPAddress.Parse(node.Ip).GetAddressBytes();
        ip.CopyTo(reply, 10);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(reply.AsSpan(14), ArtNetSender.DefaultPort);

        string[] firmware = node.Firmware.Split('.');
        reply[16] = byte.TryParse(firmware[0], out byte major) ? major : (byte)0;
        reply[17] = firmware.Length > 1 && byte.TryParse(firmware[1], out byte minor) ? minor : (byte)0;
        reply[18] = (byte)(ports[0] >> 8);            // NetSwitch
        reply[19] = (byte)((ports[0] >> 4) & 0x0F);   // SubSwitch
        WriteText(reply.AsSpan(26, 18), node.ShortName);
        WriteText(reply.AsSpan(44, 64), node.LongName);
        WriteText(reply.AsSpan(108, 64), "#0001 [0000] OK");
        reply[NumPortsOffset] = (byte)ports.Length;
        for (int port = 0; port < ports.Length; port++)
        {
            reply[PortTypesOffset + port] = OutputsDmx;
            reply[GoodOutputOffset + port] = OutputsDmx;   // it puts out what it gets
            reply[SwOutOffset + port] = (byte)(ports[port] & 0x0F);
        }

        byte[] mac = node.Mac.Split(':').Select(part => byte.TryParse(part, System.Globalization.NumberStyles.HexNumber, null, out byte value) ? value : (byte)0).ToArray();
        mac.AsSpan(0, Math.Min(6, mac.Length)).CopyTo(reply.AsSpan(201));
        ip.CopyTo(reply, 207);            // BindIp: the node itself
        reply[211] = 1;                   // BindIndex: its first (only) reply
        reply[212] = 0x08;                // Status2: 15-bit Port-Addresses
        return reply;
    }

    /// <summary>
    /// Reads a datagram that came in on an Art-Net socket: the node, if it is an ArtPollReply.
    /// </summary>
    /// <param name="datagram">What came in.</param>
    /// <param name="node">The node that answered; null when the datagram is something else.</param>
    /// <returns>True for an ArtPollReply.</returns>
    public static bool TryParseReply(ReadOnlySpan<byte> datagram, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ArtNetNode? node)
    {
        node = ParseReply(datagram);
        return node is not null;
    }

    // Sends the poll and returns the sockets on which answers can arrive.
    private static List<Socket> SendPoll(IPEndPoint endPoint)
    {
        byte[] poll = BuildPoll();
        var sockets = new List<Socket>();
        SocketException? firstError = null;

        // The plain way: the operating system picks the network adapter.
        TrySend(Udp.CreateSocket(allowBroadcast: true), poll, endPoint, sockets, ref firstError);

        // A poll to "everybody" leaves the computer through one adapter only, the one the
        // operating system prefers (normally Wi-Fi or Ethernet). The box sits behind an adapter
        // of its own, the USB one. So send the poll once more through each adapter.
        if (endPoint.Address.Equals(IPAddress.Broadcast))
        {
            foreach (IPAddress address in AdapterAddresses())
            {
                Socket socket = Udp.CreateSocket(allowBroadcast: true);
                try
                {
                    socket.Bind(new IPEndPoint(address, 0));
                }
                catch (SocketException)
                {
                    socket.Dispose();      // an adapter that is going away: skip it
                    continue;
                }

                TrySend(socket, poll, endPoint, sockets, ref firstError);
            }
        }

        if (sockets.Count == 0)
            throw firstError ?? new SocketException((int)SocketError.NetworkUnreachable);

        return sockets;
    }

    // Sends the poll from one socket. A socket that cannot send is closed and left out.
    private static void TrySend(
        Socket socket, byte[] poll, IPEndPoint endPoint, List<Socket> sockets, ref SocketException? firstError)
    {
        try
        {
            socket.SendTo(poll, endPoint);
            sockets.Add(socket);
        }
        catch (SocketException e)
        {
            firstError ??= e;
            socket.Dispose();
        }
    }

    // The IPv4 address of every network adapter that is in use (not the loopback adapter).
    private static List<IPAddress> AdapterAddresses()
    {
        var addresses = new List<IPAddress>();
        try
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up
                    || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                foreach (UnicastIPAddressInformation info in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (info.Address.AddressFamily == AddressFamily.InterNetwork)
                        addresses.Add(info.Address);
                }
            }
        }
        catch (NetworkInformationException)
        {
            // The list of adapters cannot be read: the plain poll has to do.
        }

        return addresses;
    }

    // Returns null for anything that is not an ArtPollReply.
    private static ArtNetNode? ParseReply(ReadOnlySpan<byte> reply)
    {
        if (reply.Length < MinReplyLength || !ArtNetPacket.HasOpCode(reply, ArtNetPacket.OpPollReply))
            return null;

        // A universe is spread over three fields: NetSwitch (7 bits), SubSwitch (4 bits) and the
        // low 4 bits of the port's SwOut.
        int top = ((reply[18] & 0x7F) << 8) | ((reply[19] & 0x0F) << 4);
        int universe = top | (reply[SwOutOffset] & 0x0F);

        // The ports that put out DMX: their types say it. A node that gives no number of ports
        // leaves it to the types, as the specification allows.
        int count = reply[NumPortsOffset] is > 0 and <= MaxReplyPorts ? reply[NumPortsOffset] : MaxReplyPorts;
        var ports = new List<int>();
        for (int port = 0; port < count; port++)
        {
            if ((reply[PortTypesOffset + port] & OutputsDmx) != 0)
                ports.Add(top | (reply[SwOutOffset + port] & 0x0F));
        }

        return new ArtNetNode(
            Ip: new IPAddress(reply.Slice(10, 4)).ToString(),
            ShortName: ReadText(reply.Slice(26, 18)),
            LongName: ReadText(reply.Slice(44, 64)),
            Universe: universe,
            Firmware: $"{reply[16]}.{reply[17]}",
            Mac: FormatMac(reply.Slice(201, 6)))
        {
            Ports = [.. ports],
        };
    }

    // A text into a field of a fixed size, cut to leave room for its zero byte.
    private static void WriteText(Span<byte> field, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        bytes.AsSpan(0, Math.Min(bytes.Length, field.Length - 1)).CopyTo(field);
    }

    // Text fields have a fixed size and are padded with zero bytes.
    private static string ReadText(ReadOnlySpan<byte> field)
    {
        int end = field.IndexOf((byte)0);
        return Encoding.UTF8.GetString(end < 0 ? field : field[..end]);
    }

    // Six bytes become "02:00:00:00:00:01".
    private static string FormatMac(ReadOnlySpan<byte> mac)
    {
        return string.Join(':', mac.ToArray().Select(part => part.ToString("x2")));
    }
}
