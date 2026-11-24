using System.Buffers.Binary;
using System.Globalization;

namespace DmxBox;

/// <summary>
/// The serial protocol of the Enttec DMX USB Pro, which compatible interfaces speak as well
/// (DMXking ultraDMX and others). Every message, both ways, is framed alike: 0x7E, a label that
/// says what it is, the length of its data (two bytes, the low one first), the data, and 0xE7.
/// The interface has a processor of its own: it puts the last frame it was given out on the DMX
/// cable, again and again, until it gets another.
/// </summary>
/// <remarks>
/// This class only makes and reads the messages; the program writes them to the interface's
/// serial port, and reads its answers from there.
/// </remarks>
public static class EnttecPro
{
    /// <summary>The first byte of every message.</summary>
    public const byte StartOfMessage = 0x7E;

    /// <summary>The last byte of every message.</summary>
    public const byte EndOfMessage = 0xE7;

    /// <summary>"Get Widget Parameters": the interface answers with its firmware version and its DMX timing.</summary>
    public const byte ParametersLabel = 3;

    /// <summary>"Output Only Send DMX Packet": a frame for the DMX cable, the start code first.</summary>
    public const byte SendDmxLabel = 6;

    /// <summary>"Get Widget Serial Number": the interface answers with its serial number.</summary>
    public const byte SerialNumberLabel = 10;

    /// <summary>The fewest slots a frame may have; a shorter one is filled up with zeros.</summary>
    public const int MinSlots = 24;

    /// <summary>The most slots a frame can have: one DMX universe.</summary>
    public const int MaxSlots = 512;

    // The longest data a message can carry (the frame with its start code, and room to spare).
    private const int MaxData = 600;

    // The framing around the data: start, label, two bytes of length, and the end.
    private const int HeaderLength = 4;

    /// <summary>A message: the framing around the label and the data.</summary>
    /// <param name="label">What the message is.</param>
    /// <param name="data">Its data, at most 600 bytes.</param>
    public static byte[] Message(byte label, ReadOnlySpan<byte> data)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(data.Length, MaxData);
        byte[] message = new byte[HeaderLength + data.Length + 1];
        message[0] = StartOfMessage;
        message[1] = label;
        BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(2), (ushort)data.Length);
        data.CopyTo(message.AsSpan(HeaderLength));
        message[^1] = EndOfMessage;
        return message;
    }

    /// <summary>
    /// The message that puts a frame on the DMX cable: the start code 0 and the levels, slot 1
    /// first. A frame of fewer than <see cref="MinSlots"/> slots is filled up with zeros; values
    /// after slot 512 are dropped.
    /// </summary>
    /// <param name="levels">The levels, slot 1 first.</param>
    public static byte[] DmxMessage(ReadOnlySpan<byte> levels)
    {
        if (levels.Length > MaxSlots)
            levels = levels[..MaxSlots];

        byte[] frame = new byte[1 + Math.Max(MinSlots, levels.Length)];
        levels.CopyTo(frame.AsSpan(1));   // frame[0] is the start code, 0 for levels
        return Message(SendDmxLabel, frame);
    }

    /// <summary>Asks for the firmware version and the DMX timing, with no user data: label 3, two zero bytes.</summary>
    public static byte[] ParametersRequest()
    {
        return Message(ParametersLabel, [0, 0]);
    }

    /// <summary>Asks for the serial number: label 10, no data.</summary>
    public static byte[] SerialNumberRequest()
    {
        return Message(SerialNumberLabel, []);
    }

    /// <summary>
    /// Takes the first whole message out of the bytes that came from the interface. Bytes before
    /// a start, and a message whose end is not where its length says, are thrown away.
    /// </summary>
    /// <param name="received">What came in and was not read yet. The message, and what came before it, is taken out.</param>
    /// <param name="label">The message's label.</param>
    /// <param name="data">The message's data.</param>
    /// <returns>False while no whole message is there yet; the bytes of a message that has begun are kept.</returns>
    public static bool TryTake(List<byte> received, out byte label, out byte[] data)
    {
        ArgumentNullException.ThrowIfNull(received);
        label = 0;
        data = [];
        while (true)
        {
            int start = received.IndexOf(StartOfMessage);
            if (start < 0)
            {
                received.Clear();
                return false;
            }

            received.RemoveRange(0, start);
            if (received.Count < HeaderLength)
                return false;

            int length = received[2] | (received[3] << 8);
            if (length > MaxData)
            {
                received.RemoveAt(0);   // no message is that long: this 0x7E was no start
                continue;
            }

            if (received.Count < HeaderLength + length + 1)
                return false;

            if (received[HeaderLength + length] != EndOfMessage)
            {
                received.RemoveAt(0);
                continue;
            }

            label = received[1];
            data = [.. received.GetRange(HeaderLength, length)];
            received.RemoveRange(0, HeaderLength + length + 1);
            return true;
        }
    }

    /// <summary>The firmware version in the answer to <see cref="ParametersRequest"/>: "1.44". Null for an answer too short.</summary>
    /// <param name="data">The answer's data.</param>
    public static string? FirmwareOf(ReadOnlySpan<byte> data)
    {
        return data.Length < 2 ? null : string.Create(CultureInfo.InvariantCulture, $"{data[1]}.{data[0]}");
    }

    /// <summary>
    /// The serial number in the answer to <see cref="SerialNumberRequest"/>: four bytes of two
    /// decimal digits each, the lowest first, as "01234567". Null for an answer that is not that.
    /// </summary>
    /// <param name="data">The answer's data.</param>
    public static string? SerialNumberOf(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
            return null;

        Span<char> digits = stackalloc char[8];
        for (int index = 0; index < 4; index++)
        {
            byte pair = data[3 - index];
            int high = pair >> 4;
            int low = pair & 0x0F;
            if (high > 9 || low > 9)
                return null;
            digits[index * 2] = (char)('0' + high);
            digits[index * 2 + 1] = (char)('0' + low);
        }

        return new string(digits);
    }
}
