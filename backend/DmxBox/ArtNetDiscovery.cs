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
                        && ParseReply(buffer.AsSpan(0, length)) is { } node
                        && !nodes.Contains(node))
                    {
                        nodes.Add(node);
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

    private static byte[] BuildPoll()
    {
        // The header, then two zero bytes (Flags and DiagPriority): "just answer this poll".
        byte[] poll = new byte[ArtNetPacket.HeaderLength + 2];
        ArtNetPacket.WriteHeader(poll, ArtNetPacket.OpPoll);
        return poll;
    }

    // Returns null for anything that is not an ArtPollReply.
    private static ArtNetNode? ParseReply(ReadOnlySpan<byte> reply)
    {
        if (reply.Length < MinReplyLength || !ArtNetPacket.HasOpCode(reply, ArtNetPacket.OpPollReply))
            return null;

        // The universe is spread over three fields: NetSwitch (7 bits), SubSwitch (4 bits)
        // and the low 4 bits of SwOut[0].
        int universe = (reply[18] << 8) | (reply[19] << 4) | (reply[190] & 0x0F);

        return new ArtNetNode(
            Ip: new IPAddress(reply.Slice(10, 4)).ToString(),
            ShortName: ReadText(reply.Slice(26, 18)),
            LongName: ReadText(reply.Slice(44, 64)),
            Universe: universe,
            Firmware: $"{reply[16]}.{reply[17]}",
            Mac: FormatMac(reply.Slice(201, 6)));
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
