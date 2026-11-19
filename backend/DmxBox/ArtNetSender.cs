using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace DmxBox;

/// <summary>
/// Sends Art-Net (ArtDmx) frames for one universe. Art-Net is fire-and-forget: nothing is
/// acknowledged, so send the whole frame again about 30 to 44 times per second.
/// </summary>
/// <remarks>
/// <para>Not thread-safe: send from one thread, or use one sender per thread.</para>
/// <para>
/// After a single frame, keep the sender open for a moment (100 ms is plenty). On Windows the
/// first datagram of a new socket can be held back for some tens of milliseconds, and it is
/// lost if the sender is disposed, or the program ends, before the datagram has left.
/// A stream of frames does not have this problem.
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
    private readonly IPEndPoint _endPoint;
    private byte _sequence;

    /// <summary>Prepares a sender. Nothing is sent until <see cref="Send"/>.</summary>
    /// <param name="host">IPv4 address or host name of the node; a broadcast address is fine too.</param>
    /// <param name="universe">The universe to send, 0 to <see cref="MaxUniverse"/>. The box listens to universe 0.</param>
    /// <param name="port">Art-Net UDP port.</param>
    public ArtNetSender(string host, int universe = 0, int port = DefaultPort)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(universe);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(universe, MaxUniverse);

        Universe = universe;
        _endPoint = Udp.Resolve(host, port);
        _socket = Udp.CreateSocket(allowBroadcast: true);
    }

    /// <summary>The universe this sender sends.</summary>
    public int Universe { get; }

    /// <summary>
    /// Sends one frame: the first value is channel 1, the second channel 2, and so on.
    /// Channels after the last value are not part of the frame; values after channel 512 are dropped.
    /// </summary>
    /// <param name="values">Channel levels, starting at channel 1.</param>
    public void Send(ReadOnlySpan<byte> values)
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

        _socket.SendTo(packet, _endPoint);
    }

    /// <summary>Closes the socket.</summary>
    public void Dispose() => _socket.Dispose();
}
