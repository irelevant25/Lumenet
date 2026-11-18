using System.Buffers.Binary;

namespace DmxBox;

/// <summary>The start of an Art-Net packet, which ArtDmx, ArtPoll and ArtPollReply share.</summary>
internal static class ArtNetPacket
{
    public const ushort OpPoll = 0x2000;
    public const ushort OpPollReply = 0x2100;
    public const ushort OpDmx = 0x5000;

    /// <summary>Length of what <see cref="WriteHeader"/> writes.</summary>
    public const int HeaderLength = 12;

    private const ushort ProtocolVersion = 14;

    // Every Art-Net packet starts with these 8 bytes.
    private static ReadOnlySpan<byte> Id => "Art-Net\0"u8;

    /// <summary>
    /// Writes the ID, the opcode (little-endian) and the protocol version (big-endian)
    /// to the start of <paramref name="packet"/>.
    /// </summary>
    public static void WriteHeader(Span<byte> packet, ushort opCode)
    {
        Id.CopyTo(packet);
        BinaryPrimitives.WriteUInt16LittleEndian(packet[8..], opCode);
        BinaryPrimitives.WriteUInt16BigEndian(packet[10..], ProtocolVersion);
    }

    /// <summary>True if <paramref name="datagram"/> is an Art-Net packet with the given opcode.</summary>
    public static bool HasOpCode(ReadOnlySpan<byte> datagram, ushort opCode)
    {
        return datagram.Length >= 10
            && datagram[..8].SequenceEqual(Id)
            && BinaryPrimitives.ReadUInt16LittleEndian(datagram[8..]) == opCode;
    }
}
