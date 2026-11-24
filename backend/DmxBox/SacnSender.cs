using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DmxBox;

/// <summary>
/// Sends streaming ACN (sACN, ANSI E1.31) data packets: DMX universes over UDP, by multicast to
/// the address of each universe (<see cref="MulticastAddress"/>), or to one receiver. Like Art-Net
/// nothing is acknowledged: a receiver takes about 2.5 seconds of silence for lost data, so a
/// universe that does not change is sent again at least once a second.
/// </summary>
/// <remarks>
/// <para>
/// A source is known by its CID, a UUID that stays the same for as long as the source exists, and
/// a name people read. Each universe has its own sequence number, which grows with every packet.
/// When the source stops sending a universe, it says so with three packets marked
/// "stream terminated" (<see cref="Terminate"/>).
/// </para>
/// <para>Not thread-safe: send from one thread.</para>
/// </remarks>
public sealed class SacnSender
{
    /// <summary>The UDP port of sACN.</summary>
    public const int DefaultPort = 5568;

    /// <summary>The lowest universe number.</summary>
    public const int MinUniverse = 1;

    /// <summary>The highest universe number.</summary>
    public const int MaxUniverse = 63999;

    /// <summary>The priority of a source that does not say otherwise.</summary>
    public const int DefaultPriority = 100;

    /// <summary>The highest priority; 0 is the lowest.</summary>
    public const int MaxPriority = 200;

    /// <summary>How often a source says that it stops sending a universe.</summary>
    public const int TerminatePackets = 3;

    // The data packet of a whole universe: the root layer, the framing layer and the DMP layer
    // with the start code and 512 slots.
    private const int Slots = 512;
    private const int PacketLength = 126 + Slots;
    private const int RootFlagsOffset = 16;
    private const int CidOffset = 22;
    private const int FramingOffset = 38;
    private const int NameOffset = 44;
    private const int NameLength = 64;
    private const int PriorityOffset = 108;
    private const int SequenceOffset = 111;
    private const int OptionsOffset = 112;
    private const int UniverseOffset = 113;
    private const int DmpOffset = 115;
    private const int StartCodeOffset = 125;
    private const byte StreamTerminated = 0x40;

    // "ASC-E1.17" and three zero bytes: the ACN packet identifier.
    private static ReadOnlySpan<byte> PacketIdentifier => "ASC-E1.17\0\0\0"u8;

    private readonly byte[] _cid;
    private readonly byte[] _name = new byte[NameLength];
    private readonly Dictionary<int, byte> _sequences = [];

    /// <summary>Prepares a source. Nothing is sent until <see cref="Send"/>.</summary>
    /// <param name="cid">The source's CID, the same every time the program runs.</param>
    /// <param name="sourceName">The name receivers show for the source, at most 63 bytes of UTF-8; longer is cut.</param>
    public SacnSender(Guid cid, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(sourceName);

        // The CID goes into the packet in the order UUIDs are written in: big-endian.
        _cid = cid.ToByteArray(bigEndian: true);
        byte[] name = Encoding.UTF8.GetBytes(sourceName);
        name.AsSpan(0, Math.Min(name.Length, NameLength - 1)).CopyTo(_name);
    }

    /// <summary>The multicast address of a universe: 239.255.0.1 for universe 1, 239.255.1.0 for universe 256.</summary>
    /// <param name="universe"><see cref="MinUniverse"/> to <see cref="MaxUniverse"/>.</param>
    public static IPAddress MulticastAddress(int universe)
    {
        RequireUniverse(universe);
        return new IPAddress([239, 255, (byte)(universe >> 8), (byte)universe]);
    }

    /// <summary>
    /// Sends one data packet of a universe: the first value is slot 1. Slots after the last value
    /// are 0; values after slot 512 are dropped.
    /// </summary>
    /// <param name="socket">An IPv4 UDP socket, which may be shared with other senders.</param>
    /// <param name="target">The universe's multicast address, or a receiver, at the sACN port.</param>
    /// <param name="universe"><see cref="MinUniverse"/> to <see cref="MaxUniverse"/>.</param>
    /// <param name="levels">The levels, slot 1 first.</param>
    /// <param name="priority">0 to <see cref="MaxPriority"/>: of two sources of a universe, a receiver takes the higher.</param>
    public void Send(Socket socket, IPEndPoint target, int universe, ReadOnlySpan<byte> levels, int priority = DefaultPriority)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(target);
        socket.SendTo(Packet(universe, levels, priority, terminated: false), target);
    }

    /// <summary>
    /// Says that the source stops sending a universe: <see cref="TerminatePackets"/> packets with the
    /// last levels, marked "stream terminated". A receiver lets go of the source at once instead of
    /// waiting 2.5 seconds for it.
    /// </summary>
    /// <param name="socket">An IPv4 UDP socket.</param>
    /// <param name="target">Where the universe went.</param>
    /// <param name="universe"><see cref="MinUniverse"/> to <see cref="MaxUniverse"/>.</param>
    /// <param name="levels">The last levels sent.</param>
    /// <param name="priority">The priority it was sent with.</param>
    public void Terminate(Socket socket, IPEndPoint target, int universe, ReadOnlySpan<byte> levels, int priority = DefaultPriority)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(target);
        for (int packet = 0; packet < TerminatePackets; packet++)
            socket.SendTo(Packet(universe, levels, priority, terminated: true), target);
    }

    /// <summary>
    /// One data packet of a universe, with the universe's next sequence number, as <see cref="Send"/>
    /// sends it. For a program that sends it in a way of its own, and for the tests.
    /// </summary>
    /// <param name="universe"><see cref="MinUniverse"/> to <see cref="MaxUniverse"/>.</param>
    /// <param name="levels">The levels, slot 1 first.</param>
    /// <param name="priority">0 to <see cref="MaxPriority"/>.</param>
    /// <param name="terminated">True for a packet that says the source stops sending the universe.</param>
    public byte[] Packet(int universe, ReadOnlySpan<byte> levels, int priority, bool terminated)
    {
        RequireUniverse(universe);
        ArgumentOutOfRangeException.ThrowIfNegative(priority);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(priority, MaxPriority);
        if (levels.Length > Slots)
            levels = levels[..Slots];

        byte sequence = _sequences.GetValueOrDefault(universe);
        _sequences[universe] = unchecked((byte)(sequence + 1));

        byte[] packet = new byte[PacketLength];
        Span<byte> span = packet;

        // The root layer: the preamble, the identifier, and that a framing layer of E1.31 follows.
        BinaryPrimitives.WriteUInt16BigEndian(span, 0x0010);   // preamble size
        PacketIdentifier.CopyTo(span[4..]);
        WriteFlagsAndLength(span, RootFlagsOffset);
        BinaryPrimitives.WriteUInt32BigEndian(span[18..], 0x00000004);   // VECTOR_ROOT_E131_DATA
        _cid.CopyTo(span[CidOffset..]);

        // The framing layer: who sends, how important, which packet of which universe.
        WriteFlagsAndLength(span, FramingOffset);
        BinaryPrimitives.WriteUInt32BigEndian(span[40..], 0x00000002);   // VECTOR_E131_DATA_PACKET
        _name.CopyTo(span[NameOffset..]);
        packet[PriorityOffset] = (byte)priority;
        packet[SequenceOffset] = sequence;
        packet[OptionsOffset] = terminated ? StreamTerminated : (byte)0;
        BinaryPrimitives.WriteUInt16BigEndian(span[UniverseOffset..], (ushort)universe);

        // The DMP layer: the start code 0 and the 512 slots.
        WriteFlagsAndLength(span, DmpOffset);
        packet[117] = 0x02;   // VECTOR_DMP_SET_PROPERTY
        packet[118] = 0xa1;   // address type and data type
        BinaryPrimitives.WriteUInt16BigEndian(span[119..], 0x0000);   // first property address
        BinaryPrimitives.WriteUInt16BigEndian(span[121..], 0x0001);   // address increment
        BinaryPrimitives.WriteUInt16BigEndian(span[123..], 1 + Slots);   // the start code and the slots
        levels.CopyTo(span[(StartCodeOffset + 1)..]);
        return packet;
    }

    // A layer begins with 0x7 in its top four bits and its length, from there to the packet's end.
    private static void WriteFlagsAndLength(Span<byte> packet, int offset)
    {
        BinaryPrimitives.WriteUInt16BigEndian(packet[offset..], (ushort)(0x7000 | (PacketLength - offset)));
    }

    private static void RequireUniverse(int universe)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(universe, MinUniverse);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(universe, MaxUniverse);
    }
}
