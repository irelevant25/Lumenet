using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace DmxBox.Server.Osc;

/// <summary>
/// One message of Open Sound Control (OSC 1.0): an address like "/lumenet/stack/play" and its
/// arguments. As read, a whole number is a <see cref="long"/>, a number with a point a
/// <see cref="double"/>, true and false a <see cref="bool"/>, nil null, an impulse
/// <see cref="Impulse"/>, a text a <see cref="string"/>; a blob, a time tag, a color or a MIDI message
/// is a byte array.
/// </summary>
/// <param name="Address">The address, beginning with "/".</param>
/// <param name="Arguments">The arguments, in order.</param>
internal sealed record OscMessage(string Address, IReadOnlyList<object?> Arguments)
{
    /// <summary>An impulse ('I'): an argument that has no value, like a press without a letting go.</summary>
    public static readonly object Impulse = new();

    /// <summary>The message as the list of incoming messages shows it: "/lumenet/fader/master 0.5".</summary>
    public override string ToString()
    {
        return Arguments.Count == 0 ? Address : $"{Address} {string.Join(' ', Arguments.Select(Text))}";
    }

    private static string Text(object? argument)
    {
        return argument switch
        {
            null => "nil",
            bool flag => flag ? "true" : "false",
            double number => number.ToString("0.###", CultureInfo.InvariantCulture),
            long number => number.ToString(CultureInfo.InvariantCulture),
            string text => $"\"{(text.Length > 40 ? text[..40] + "…" : text)}\"",
            byte[] bytes => $"({bytes.Length} bytes)",
            _ when ReferenceEquals(argument, Impulse) => "impulse",
            _ => "?",
        };
    }
}

/// <summary>
/// Packets of OSC as UDP carries them: one message, or a bundle of messages and bundles (whose time
/// tags are not waited for: everything in a packet counts at once). Read leniently: a packet that is no
/// OSC gives no messages, a message whose arguments cannot all be read is passed over, and a sender that
/// leaves out the zeros at the very end is understood.
/// </summary>
internal static class OscPacket
{
    // How deep bundles may lie in each other.
    private const int MaxDepth = 8;

    private static ReadOnlySpan<byte> BundleTag => "#bundle\0"u8;

    /// <summary>The messages of a packet, in order.</summary>
    /// <param name="packet">The bytes of one UDP packet.</param>
    public static List<OscMessage> Read(ReadOnlySpan<byte> packet)
    {
        var messages = new List<OscMessage>();
        ReadInto(packet, messages, 0);
        return messages;
    }

    /// <summary>A message as a packet: an int is 'i', a long 'h', a float 'f', a double 'd', a text 's', a bool 'T' or 'F', null 'N', <see cref="OscMessage.Impulse"/> 'I', bytes a blob.</summary>
    /// <param name="address">The address.</param>
    /// <param name="arguments">The arguments.</param>
    public static byte[] Write(string address, params object?[] arguments)
    {
        using var body = new MemoryStream();
        var tags = new StringBuilder(",");
        foreach (object? argument in arguments)
        {
            switch (argument)
            {
                case int number:
                    tags.Append('i');
                    WriteBig(body, BitConverter.GetBytes(number));
                    break;
                case long number:
                    tags.Append('h');
                    WriteBig(body, BitConverter.GetBytes(number));
                    break;
                case float number:
                    tags.Append('f');
                    WriteBig(body, BitConverter.GetBytes(number));
                    break;
                case double number:
                    tags.Append('d');
                    WriteBig(body, BitConverter.GetBytes(number));
                    break;
                case string text:
                    tags.Append('s');
                    WriteString(body, text);
                    break;
                case bool flag:
                    tags.Append(flag ? 'T' : 'F');
                    break;
                case null:
                    tags.Append('N');
                    break;
                case byte[] bytes:
                    tags.Append('b');
                    WriteBig(body, BitConverter.GetBytes(bytes.Length));
                    body.Write(bytes);
                    body.Write(new byte[Padding(bytes.Length)]);
                    break;
                default:
                    if (!ReferenceEquals(argument, OscMessage.Impulse))
                        throw new ArgumentException($"OSC has no argument of the kind {argument.GetType().Name}.", nameof(arguments));
                    tags.Append('I');
                    break;
            }
        }

        using var packet = new MemoryStream();
        WriteString(packet, address);
        WriteString(packet, tags.ToString());
        body.WriteTo(packet);
        return packet.ToArray();
    }

    /// <summary>A bundle of packets (messages or bundles), to be done at once.</summary>
    /// <param name="elements">The packets in it.</param>
    public static byte[] Bundle(params byte[][] elements)
    {
        using var packet = new MemoryStream();
        packet.Write(BundleTag);
        packet.Write([0, 0, 0, 0, 0, 0, 0, 1]);   // the time tag of "at once"
        foreach (byte[] element in elements)
        {
            WriteBig(packet, BitConverter.GetBytes(element.Length));
            packet.Write(element);
        }

        return packet.ToArray();
    }

    private static void ReadInto(ReadOnlySpan<byte> packet, List<OscMessage> messages, int depth)
    {
        if (packet.Length == 0)
            return;

        if (packet[0] != (byte)'#')
        {
            if (Message(packet) is { } message)
                messages.Add(message);
            return;
        }

        // A bundle: its tag and time, then each element with its size before it.
        if (depth >= MaxDepth || packet.Length < 16 || !packet[..8].SequenceEqual(BundleTag))
            return;

        int at = 16;
        while (at + 4 <= packet.Length)
        {
            int size = BinaryPrimitives.ReadInt32BigEndian(packet[at..]);
            at += 4;
            if (size <= 0 || size > packet.Length - at)
                return;

            ReadInto(packet.Slice(at, size), messages, depth + 1);
            at += size;
        }
    }

    // A message: its address, its type tags, its arguments. Null when it cannot be read as a whole.
    private static OscMessage? Message(ReadOnlySpan<byte> packet)
    {
        int at = 0;
        if (ReadString(packet, ref at) is not { } address || address.Length < 2 || address[0] != '/')
            return null;

        // A sender of before the type tags sends none: its message counts without its arguments.
        if (at >= packet.Length || packet[at] != (byte)',')
            return new OscMessage(address, []);

        if (ReadString(packet, ref at) is not { } tags)
            return null;

        var arguments = new List<object?>();
        foreach (char tag in tags.AsSpan(1))
        {
            object? argument;
            switch (tag)
            {
                case 'i':
                    if (!Has(packet, at, 4))
                        return null;
                    argument = (long)BinaryPrimitives.ReadInt32BigEndian(packet[at..]);
                    at += 4;
                    break;
                case 'c':
                    if (!Has(packet, at, 4))
                        return null;
                    argument = CharacterOf(BinaryPrimitives.ReadInt32BigEndian(packet[at..]));
                    at += 4;
                    break;
                case 'h':
                    if (!Has(packet, at, 8))
                        return null;
                    argument = BinaryPrimitives.ReadInt64BigEndian(packet[at..]);
                    at += 8;
                    break;
                case 'f':
                    if (!Has(packet, at, 4))
                        return null;
                    argument = (double)BinaryPrimitives.ReadSingleBigEndian(packet[at..]);
                    at += 4;
                    break;
                case 'd':
                    if (!Has(packet, at, 8))
                        return null;
                    argument = BinaryPrimitives.ReadDoubleBigEndian(packet[at..]);
                    at += 8;
                    break;
                case 's':
                case 'S':
                    if (ReadString(packet, ref at) is not { } text)
                        return null;
                    argument = text;
                    break;
                case 'b':
                    if (!Has(packet, at, 4))
                        return null;
                    int length = BinaryPrimitives.ReadInt32BigEndian(packet[at..]);
                    at += 4;
                    if (length < 0 || !Has(packet, at, length))
                        return null;
                    argument = packet.Slice(at, length).ToArray();
                    at = Math.Min(packet.Length, at + length + Padding(length));
                    break;
                case 't':
                    if (!Has(packet, at, 8))
                        return null;
                    argument = packet.Slice(at, 8).ToArray();
                    at += 8;
                    break;
                case 'r':
                case 'm':
                    if (!Has(packet, at, 4))
                        return null;
                    argument = packet.Slice(at, 4).ToArray();
                    at += 4;
                    break;
                case 'T':
                    argument = true;
                    break;
                case 'F':
                    argument = false;
                    break;
                case 'N':
                    argument = null;
                    break;
                case 'I':
                    argument = OscMessage.Impulse;
                    break;
                case '[':
                case ']':
                    continue;   // an array: its parts count as arguments of their own
                default:
                    return null;   // a kind this desk does not know: what follows cannot be found
            }

            arguments.Add(argument);
        }

        return new OscMessage(address, arguments);
    }

    // A character ('c') as a text of its own; a number that is no character is the sign for one that cannot be shown.
    private static string CharacterOf(int code)
    {
        return code is >= 0 and <= 0x10FFFF and (< 0xD800 or > 0xDFFF) ? char.ConvertFromUtf32(code) : "�";
    }

    // A text ending in a zero, with zeros after it up to a multiple of 4 bytes. Null when it has no end.
    private static string? ReadString(ReadOnlySpan<byte> packet, ref int at)
    {
        if (at >= packet.Length)
            return null;

        int end = packet[at..].IndexOf((byte)0);
        if (end < 0)
            return null;

        // OSC writes its texts in ASCII; UTF-8 reads those the same, and the texts of senders that use it.
        string text = Encoding.UTF8.GetString(packet.Slice(at, end));
        at = Math.Min(packet.Length, at + ((end + 4) & ~3));
        return text;
    }

    private static bool Has(ReadOnlySpan<byte> packet, int at, int count)
    {
        return count <= packet.Length - at;
    }

    // The zeros after something of this length, up to a multiple of 4.
    private static int Padding(int length)
    {
        return (4 - (length % 4)) % 4;
    }

    private static void WriteString(Stream stream, string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes);
        stream.Write(new byte[4 - (bytes.Length % 4)]);   // the zero at its end, and the zeros up to a multiple of 4
    }

    // A number of the system's own order, written big-endian as OSC has it.
    private static void WriteBig(Stream stream, byte[] bytes)
    {
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        stream.Write(bytes);
    }
}
