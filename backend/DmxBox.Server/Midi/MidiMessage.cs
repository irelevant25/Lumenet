using System.Globalization;

namespace DmxBox.Server.Midi;

/// <summary>The kinds of MIDI messages the desk takes from a controller and sends back to it.</summary>
internal enum MidiKind
{
    /// <summary>A key or pad pressed: a note with a velocity above zero.</summary>
    NoteOn,

    /// <summary>A key or pad let go: Note Off, or Note On with velocity zero.</summary>
    NoteOff,

    /// <summary>A controller (CC): a fader, a knob, an encoder or a button, 0-127.</summary>
    ControlChange,

    /// <summary>Pitch Bend: 14 bits, 0-16383; the faders of Mackie controllers send it.</summary>
    PitchBend,

    /// <summary>Program Change: a program number, 0-127.</summary>
    ProgramChange,
}

/// <summary>
/// One MIDI message from a controller, or to it: its kind, the MIDI channel (1-16), the note,
/// controller or program (0-127; 0 for a pitch bend), and its value (the velocity or the
/// controller's value, 0-127; 0-16383 for a pitch bend; 0 for a program change).
/// </summary>
/// <param name="Kind">What kind of message.</param>
/// <param name="Channel">The MIDI channel, 1-16.</param>
/// <param name="Number">The note, controller or program, 0-127.</param>
/// <param name="Value">The velocity or value.</param>
internal readonly record struct MidiMessage(MidiKind Kind, int Channel, int Number, int Value)
{
    /// <summary>The highest value of a pitch bend: 14 bits.</summary>
    public const int MaxPitchBend = 16383;

    /// <summary>True for a press: Note On, a controller above zero (a button), a program change.</summary>
    public bool IsPress => Kind switch
    {
        MidiKind.NoteOn => true,
        MidiKind.ControlChange => Value > 0,
        MidiKind.ProgramChange => true,
        _ => false,
    };

    /// <summary>The message as the bytes on the cable: a status byte and one or two data bytes.</summary>
    public byte[] ToBytes()
    {
        int channel = Math.Clamp(Channel, 1, 16) - 1;
        int number = Math.Clamp(Number, 0, 127);
        return Kind switch
        {
            MidiKind.NoteOn => [(byte)(0x90 | channel), (byte)number, (byte)Math.Clamp(Value, 0, 127)],
            MidiKind.NoteOff => [(byte)(0x80 | channel), (byte)number, (byte)Math.Clamp(Value, 0, 127)],
            MidiKind.ControlChange => [(byte)(0xB0 | channel), (byte)number, (byte)Math.Clamp(Value, 0, 127)],
            MidiKind.PitchBend => [(byte)(0xE0 | channel), (byte)(Math.Clamp(Value, 0, MaxPitchBend) & 0x7F), (byte)(Math.Clamp(Value, 0, MaxPitchBend) >> 7)],
            MidiKind.ProgramChange => [(byte)(0xC0 | channel), (byte)number],
            _ => [],
        };
    }

    /// <summary>The message as the list of incoming messages shows it: "Note On 56, velocity 127, channel 1".</summary>
    public override string ToString()
    {
        return Kind switch
        {
            MidiKind.NoteOn => Text($"Note On {Number}, velocity {Value}, channel {Channel}"),
            MidiKind.NoteOff => Text($"Note Off {Number}, channel {Channel}"),
            MidiKind.ControlChange => Text($"CC {Number} = {Value}, channel {Channel}"),
            MidiKind.PitchBend => Text($"Pitch Bend {Value}, channel {Channel}"),
            MidiKind.ProgramChange => Text($"Program Change {Number}, channel {Channel}"),
            _ => "?",
        };
    }

    /// <summary>
    /// Reads one message from its bytes: a status byte and its data bytes. Note On with velocity
    /// zero is a Note Off, as MIDI has it. Null for any other message (system messages, aftertouch).
    /// </summary>
    /// <param name="bytes">The status byte first.</param>
    public static MidiMessage? FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2 || bytes[0] < 0x80 || bytes[0] >= 0xF0)
            return null;

        int channel = (bytes[0] & 0x0F) + 1;
        int first = bytes[1] & 0x7F;
        int second = bytes.Length > 2 ? bytes[2] & 0x7F : 0;
        return (bytes[0] & 0xF0) switch
        {
            0x90 when bytes.Length > 2 => second > 0
                ? new MidiMessage(MidiKind.NoteOn, channel, first, second)
                : new MidiMessage(MidiKind.NoteOff, channel, first, 0),
            0x80 when bytes.Length > 2 => new MidiMessage(MidiKind.NoteOff, channel, first, second),
            0xB0 when bytes.Length > 2 => new MidiMessage(MidiKind.ControlChange, channel, first, second),
            0xE0 when bytes.Length > 2 => new MidiMessage(MidiKind.PitchBend, channel, 0, first | (second << 7)),
            0xC0 => new MidiMessage(MidiKind.ProgramChange, channel, first, 0),
            _ => null,
        };
    }

    private static string Text(FormattableString text)
    {
        return text.ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Reads MIDI messages from a stream of bytes, as a cable or a pipe carries them: with running
/// status (a message that leaves its status byte out has the one before), passing over system
/// exclusive messages and the one-byte real-time messages that may come between.
/// </summary>
internal sealed class MidiStreamReader
{
    private readonly byte[] _message = new byte[3];
    private int _status;
    private int _have;
    private bool _inSysEx;

    /// <summary>Takes the next bytes; the messages they finish go to <paramref name="received"/>.</summary>
    /// <param name="bytes">The bytes, as they came.</param>
    /// <param name="received">Called with each message that is complete.</param>
    public void Take(ReadOnlySpan<byte> bytes, Action<MidiMessage> received)
    {
        foreach (byte value in bytes)
        {
            if (value >= 0xF8)
                continue;   // real time (clock, start, stop): it may come anywhere, and means nothing here

            if (value == 0xF0)
            {
                // System exclusive: its bytes are passed over, and it ends running status.
                _inSysEx = true;
                _status = 0;
                _have = 0;
                continue;
            }

            if (_inSysEx)
            {
                if (value < 0x80)
                    continue;
                _inSysEx = false;   // F7 ends it, and so does any other status byte
                if (value == 0xF7)
                    continue;
            }

            if (value >= 0x80)
            {
                _status = value < 0xF0 ? value : 0;   // a system common message ends running status
                _have = 0;
                continue;
            }

            if (_status == 0)
                continue;   // data without a status: passed over

            if (_have == 0)
                _message[0] = (byte)_status;
            _message[++_have] = value;
            if (_have < DataBytes(_status))
                continue;

            _have = 0;
            if (MidiMessage.FromBytes(_message.AsSpan(0, DataBytes(_status) + 1)) is { } message)
                received(message);
        }
    }

    // How many data bytes follow a status byte.
    private static int DataBytes(int status)
    {
        return (status & 0xF0) is 0xC0 or 0xD0 ? 1 : 2;
    }
}
