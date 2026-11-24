using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace DmxBox;

/// <summary>
/// Sends Art-Net (ArtDmx) frames for one universe. Art-Net is fire-and-forget: nothing is
/// acknowledged, so send the whole frame again about 30 to 44 times per second, or at least once
/// a second while it does not change.
/// </summary>
/// <remarks>
/// <para>Not thread-safe: send from one thread, or use one sender per thread.</para>
/// <para>
/// After a single frame, keep the sender open for a moment (100 ms is plenty). On Windows the
/// first datagram of a new socket can be held back for some tens of milliseconds, and it is
/// lost if the sender is disposed, or the program ends, before the datagram has left.
/// A stream of frames does not have this problem.
/// </para>
/// <para>
/// The universe is Art-Net's 15-bit Port-Address: the Net (7 bits), the Sub-Net (4 bits) and the
/// Universe (4 bits) of the node's port, as <see cref="PortAddress"/> puts them together.
/// </para>
/// </remarks>
public sealed class ArtNetSender : IDisposable
{
    /// <summary>The UDP port that the Art-Net specification fixes.</summary>
    public const int DefaultPort = 6454;

    /// <summary>The highest universe number: Art-Net's Port-Address has 15 bits.</summary>
    public const int MaxUniverse = 0x7FFF;

    // One ArtDmx packet carries at most one DMX universe.
    private const int MaxChannels = 512;

    // The ArtDmx fields that follow the common header: sequence, physical port,
    // universe (u16, little-endian), number of channels (u16, big-endian).
    private const int SequenceOffset = 12;
    private const int UniverseOffset = 14;
    private const int LengthOffset = 16;
    private const int DataOffset = 18;

    private readonly Socket _socket;
    private readonly bool _ownsSocket;
    private byte _sequence;

    /// <summary>Prepares a sender with a socket of its own. Nothing is sent until <see cref="Send"/>.</summary>
    /// <param name="host">IPv4 address or host name of the node; a broadcast address is fine too.</param>
    /// <param name="universe">The universe to send, 0 to <see cref="MaxUniverse"/>. The box listens to universe 0.</param>
    /// <param name="port">Art-Net UDP port.</param>
    public ArtNetSender(string host, int universe = 0, int port = DefaultPort)
    {
        RequireUniverse(universe);
        Universe = universe;
        Target = Udp.Resolve(host, port);
        _socket = Udp.CreateSocket(allowBroadcast: true);
        _ownsSocket = true;
    }

    /// <summary>
    /// Prepares a sender that sends through a socket it shares with others, for a program that
    /// sends many universes, to many nodes, from one socket. Disposing the sender leaves the
    /// socket open.
    /// </summary>
    /// <param name="socket">An IPv4 UDP socket; one that may send to broadcast addresses for a broadcast target.</param>
    /// <param name="target">Where the frames go: the node, or a broadcast address, at its Art-Net port.</param>
    /// <param name="universe">The universe to send, 0 to <see cref="MaxUniverse"/>.</param>
    public ArtNetSender(Socket socket, IPEndPoint target, int universe)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(target);
        RequireUniverse(universe);
        Universe = universe;
        Target = target;
        _socket = socket;
    }

    /// <summary>The universe this sender sends.</summary>
    public int Universe { get; }

    /// <summary>Where the frames go. A sender with a broadcast target can follow the network it is on.</summary>
    public IPEndPoint Target { get; set; }

    /// <summary>
    /// The 15-bit Port-Address of a node's port, out of its three parts as nodes show them.
    /// </summary>
    /// <param name="net">The Net, 0 to 127.</param>
    /// <param name="subNet">The Sub-Net, 0 to 15.</param>
    /// <param name="universe">The Universe, 0 to 15.</param>
    public static int PortAddress(int net, int subNet, int universe)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(net);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(net, 127);
        ArgumentOutOfRangeException.ThrowIfNegative(subNet);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(subNet, 15);
        ArgumentOutOfRangeException.ThrowIfNegative(universe);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(universe, 15);
        return (net << 8) | (subNet << 4) | universe;
    }

    /// <summary>A Port-Address as nodes show it, Net, Sub-Net and Universe: 17 is "0:1:1".</summary>
    /// <param name="portAddress">0 to <see cref="MaxUniverse"/>.</param>
    public static string PortAddressText(int portAddress)
    {
        RequireUniverse(portAddress);
        return string.Create(
            CultureInfo.InvariantCulture, $"{portAddress >> 8}:{(portAddress >> 4) & 0x0F}:{portAddress & 0x0F}");
    }

    /// <summary>
    /// Sends one frame: the first value is channel 1, the second channel 2, and so on.
    /// Channels after the last value are not part of the frame; values after channel 512 are dropped.
    /// </summary>
    /// <param name="values">Channel levels, starting at channel 1.</param>
    public void Send(ReadOnlySpan<byte> values)
    {
        _socket.SendTo(Packet(values), Target);
    }

    /// <summary>
    /// The ArtDmx packet of the next frame, with the next sequence number, as <see cref="Send"/>
    /// sends it. For a program that sends it in a way of its own, and for the tests.
    /// </summary>
    /// <param name="values">Channel levels, starting at channel 1; values after channel 512 are dropped.</param>
    public byte[] Packet(ReadOnlySpan<byte> values)
    {
        if (values.Length > MaxChannels)
            values = values[..MaxChannels];

        // The specification wants an even number of channels, and at least two.
        // The padding channels are sent as 0.
        int channelCount = Math.Max(2, values.Length + values.Length % 2);

        // 1..255, then 1 again. 0 would mean "this sender does not number its frames".
        _sequence = (byte)(_sequence % 255 + 1);

        byte[] packet = new byte[DataOffset + channelCount];
        ArtNetPacket.WriteHeader(packet, ArtNetPacket.OpDmx);
        packet[SequenceOffset] = _sequence;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(UniverseOffset), (ushort)Universe);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(LengthOffset), (ushort)channelCount);
        values.CopyTo(packet.AsSpan(DataOffset));
        return packet;
    }

    /// <summary>Closes the socket, if it is the sender's own.</summary>
    public void Dispose()
    {
        if (_ownsSocket)
            _socket.Dispose();
    }

    private static void RequireUniverse(int universe)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(universe);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(universe, MaxUniverse);
    }
}
