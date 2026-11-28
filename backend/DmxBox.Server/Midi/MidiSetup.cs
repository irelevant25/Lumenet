using System.Text.Json;
using System.Text.Json.Serialization;

namespace DmxBox.Server.Midi;

/// <summary>
/// The MIDI of this PC: the controllers the desk uses, and what their controls do. It belongs to the
/// PC a controller is plugged into, not to a project: it is kept in midi.json beside the LAN
/// password (<see cref="MidiSetupFile"/>), and Export can take it to another PC.
/// </summary>
/// <param name="Devices">
/// The names of the controllers the desk opens. The others it leaves alone: on Windows a device can
/// be open in one program only.
/// </param>
/// <param name="Mappings">What the controls do.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record MidiSetup(string[] Devices, MidiMapping[] Mappings)
{
    /// <summary>No controller, nothing mapped.</summary>
    public static readonly MidiSetup Empty = new([], []);
}

/// <summary>
/// What one control of a controller does: it presses a key of the desk, or moves a fader. A
/// control is a message on a channel: a note, a controller (CC), a pair of controllers for 14
/// bits, the pitch bend, or a program change; or a channel of a box's DMX IN.
/// </summary>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record MidiMapping
{
    /// <summary>A note: Note On presses the key, Note Off lets it go.</summary>
    public const string Note = "note";

    /// <summary>A controller: a fader or knob (0-127), an encoder, or a button (above 0 is pressed).</summary>
    public const string Controller = "cc";

    /// <summary>Two controllers for 14 bits: the coarse one (0-31) and the fine one 32 above it.</summary>
    public const string Controller14 = "cc14";

    /// <summary>The pitch bend of the channel, 14 bits: the faders of Mackie controllers.</summary>
    public const string PitchBend = "pitchbend";

    /// <summary>A program change: it presses a key, and lets it go at once.</summary>
    public const string Program = "program";

    /// <summary>
    /// A channel of a box's DMX IN, 1-512: its level, 0-255, is the fader's; for a key, half or more
    /// presses it (<see cref="MidiMessage.DmxPress"/>). The device is the box's DMX IN, channel 1.
    /// </summary>
    public const string Dmx = "dmx";

    /// <summary>The highest channel of DMX IN.</summary>
    public const int DmxChannels = 512;

    /// <summary>A fader that sends where it stands, 0-127.</summary>
    public const string Absolute = "absolute";

    /// <summary>An endless encoder: 1 to 63 up, 127 down to 65 for -1 to -63 (two's complement).</summary>
    public const string RelativeTwos = "relative-twos";

    /// <summary>An endless encoder: 64 stands still, 65 is +1, 63 is -1 (binary offset).</summary>
    public const string RelativeOffset = "relative-offset";

    /// <summary>An endless encoder: 1 to 63 up, 65 to 127 down (sign and magnitude: 65 is -1).</summary>
    public const string RelativeSign = "relative-sign";

    /// <summary>The kinds of messages, as <see cref="Message"/> names them.</summary>
    public static readonly string[] Messages = [Note, Controller, Controller14, PitchBend, Program, Dmx];

    /// <summary>The ways a controller (CC) moves a fader, as <see cref="Mode"/> names them.</summary>
    public static readonly string[] Modes = [Absolute, RelativeTwos, RelativeOffset, RelativeSign];

    /// <summary>The controller, by the name the PC gives it.</summary>
    public string Device { get; init; } = "";

    /// <summary>The MIDI channel, 1-16.</summary>
    public int Channel { get; init; } = 1;

    /// <summary>The kind of message, one of <see cref="Messages"/>.</summary>
    public string Message { get; init; } = Note;

    /// <summary>
    /// The note, controller or program, 0-127; for <see cref="Controller14"/> the coarse controller,
    /// 0-31; 0 for the pitch bend; the channel of DMX IN, 1-512, for <see cref="Dmx"/>.
    /// </summary>
    public int Number { get; init; }

    /// <summary>What it works: a key of the desk ("memory.5") or a fader ("fader.master"), see <see cref="ControlTarget"/>.</summary>
    public string Target { get; init; } = "";

    /// <summary>How a controller (CC) moves a fader, one of <see cref="Modes"/>.</summary>
    public string Mode { get; init; } = Absolute;

    /// <summary>
    /// For a fader that stays where the hand left it (no motor): it takes the desk's fader over only
    /// once it reaches the desk's value, so that no light jumps (soft takeover).
    /// </summary>
    public bool Pickup { get; init; } = true;

    /// <summary>
    /// The desk sends back what it has: for a fader its value (a motorized fader, the ring of lights
    /// of an encoder), for a key its lamp (the light of the key, see <see cref="Lights"/>). A program
    /// change has nothing to send back.
    /// </summary>
    public bool Feedback { get; init; }

    /// <summary>For a key: how its light is lit for each state of the desk's lamp; null for the usual (off 0, full 127).</summary>
    public MidiLights? Lights { get; init; }

    /// <summary>The control, without what it does: two mappings with the same one are the same control.</summary>
    [JsonIgnore]
    public (string Device, int Channel, string Message, int Number) Control => (Device, Channel, Message, Number);
}

/// <summary>
/// How the light of a key is sent, for each state of the desk's lamp: off (faint on the page),
/// half, full, and blinking. A light without <see cref="Blink"/> is blinked by the desk itself.
/// </summary>
/// <param name="Off">The desk's key is faint.</param>
/// <param name="Half">The desk's key is half lit.</param>
/// <param name="Full">The desk's key is lit.</param>
/// <param name="Blink">The desk's key blinks; null to blink between <paramref name="Full"/> and <paramref name="Off"/>.</param>
/// <param name="Number">
/// The note or controller the light is sent on; null for the key's own. The X-Touch Mini and
/// Compact light their keys with other notes than the keys send.
/// </param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record MidiLights(MidiLight Off, MidiLight Half, MidiLight Full, MidiLight? Blink = null, int? Number = null)
{
    /// <summary>The usual light: one color, on at 127 when the desk's key is lit, off when it is not.</summary>
    public static readonly MidiLights Usual = new(new MidiLight(0), new MidiLight(0), new MidiLight(127));
}

/// <summary>
/// One state of a light: the value (a velocity or a controller's value) and, for a controller
/// that takes the brightness or blinking from the channel (the APC mini mk2), the channel.
/// </summary>
/// <param name="Value">0-127.</param>
/// <param name="Channel">1-16, or 0 for the channel of the control itself.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record MidiLight(int Value, int Channel = 0);

/// <summary>Checks a setup from a page or a file: what is wrong is refused with the reason.</summary>
internal static class MidiSetupCheck
{
    /// <summary>The most mappings a setup holds: room for every key and fader of several controllers.</summary>
    public const int MaxMappings = 4096;

    /// <summary>The most controllers a setup uses.</summary>
    public const int MaxDevices = 16;

    /// <summary>The setup, checked: devices without doubles, mappings without two for one control.</summary>
    /// <exception cref="DeskException">Something in it is not possible; the message says what.</exception>
    public static MidiSetup Checked(MidiSetup? setup)
    {
        if (setup is null)
            return MidiSetup.Empty;

        string[] devices = [.. (setup.Devices ?? []).Select(name => (name ?? "").Trim()).Where(name => name.Length > 0).Distinct(StringComparer.Ordinal)];
        if (devices.Length > MaxDevices)
            throw new DeskException($"The desk uses at most {MaxDevices} MIDI devices.");

        MidiMapping[] mappings = setup.Mappings ?? [];
        if (mappings.Length > MaxMappings)
            throw new DeskException($"A MIDI setup holds at most {MaxMappings} controls.");

        var kept = new Dictionary<(string, int, string, int), MidiMapping>();
        foreach (MidiMapping mapping in mappings)
        {
            MidiMapping checkedMapping = Checked(mapping);
            kept[checkedMapping.Control] = checkedMapping;   // the last one for a control counts
        }

        return new MidiSetup(devices, [.. kept.Values]);
    }

    /// <summary>One mapping, checked.</summary>
    /// <exception cref="DeskException">Something in it is not possible.</exception>
    public static MidiMapping Checked(MidiMapping? mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        string device = (mapping.Device ?? "").Trim();
        if (device.Length == 0)
            throw new DeskException("A MIDI control needs the name of its device.");
        if (mapping.Channel is < 1 or > 16)
            throw new DeskException($"A MIDI channel is 1 to 16, not {mapping.Channel}.");
        if (!MidiMapping.Messages.Contains(mapping.Message))
            throw new DeskException($"\"{mapping.Message}\" is no kind of MIDI message the desk takes.");

        if (mapping.Message == MidiMapping.Dmx)
        {
            if (mapping.Number is < 1 or > MidiMapping.DmxChannels)
                throw new DeskException($"A channel of DMX IN is 1 to {MidiMapping.DmxChannels}, not {mapping.Number}.");
        }
        else
        {
            int highest = mapping.Message switch
            {
                MidiMapping.Controller14 => 31,
                MidiMapping.PitchBend => 0,
                _ => 127,
            };
            if (mapping.Number < 0 || mapping.Number > highest)
                throw new DeskException($"The number of a MIDI {mapping.Message} is 0 to {highest}, not {mapping.Number}.");
        }

        ControlTarget target = ControlTarget.Parse(mapping.Target)
            ?? throw new DeskException($"\"{mapping.Target}\" is no key or fader of the desk.");
        if (!target.IsKey && mapping.Message is MidiMapping.Note or MidiMapping.Program)
            throw new DeskException("A fader takes a controller (CC), a pair of them or the pitch bend, not a note or a program.");
        if (target.IsKey && mapping.Message is MidiMapping.Controller14 or MidiMapping.PitchBend)
            throw new DeskException("A key takes a note, a controller (CC) or a program, not 14 bits.");
        if (!MidiMapping.Modes.Contains(mapping.Mode))
            throw new DeskException($"\"{mapping.Mode}\" is no way a MIDI controller moves a fader.");

        MidiLights? lights = mapping.Lights is null ? null : CheckedLights(mapping.Lights);
        bool relative = mapping.Message == MidiMapping.Controller && mapping.Mode != MidiMapping.Absolute;
        return mapping with
        {
            Device = device,
            Number = mapping.Message == MidiMapping.PitchBend ? 0 : mapping.Number,
            Target = target.Name,
            Mode = mapping.Message == MidiMapping.Controller && !target.IsKey ? mapping.Mode : MidiMapping.Absolute,
            Pickup = mapping.Pickup && !relative && !target.IsKey,
            Channel = mapping.Message == MidiMapping.Dmx ? 1 : mapping.Channel,
            Feedback = mapping.Feedback && mapping.Message is not (MidiMapping.Program or MidiMapping.Dmx),
            Lights = target.IsKey ? lights : null,
        };
    }

    private static MidiLights CheckedLights(MidiLights lights)
    {
        if (lights.Number is < 0 or > 127)
            throw new DeskException($"The note or controller of a MIDI light is 0 to 127, not {lights.Number}.");
        return new MidiLights(
            CheckedLight(lights.Off),
            CheckedLight(lights.Half),
            CheckedLight(lights.Full),
            lights.Blink is null ? null : CheckedLight(lights.Blink),
            lights.Number);
    }

    private static MidiLight CheckedLight(MidiLight? light)
    {
        if (light is null)
            throw new DeskException("A light of a MIDI key needs a value for each state.");
        if (light.Value is < 0 or > 127)
            throw new DeskException($"The value of a MIDI light is 0 to 127, not {light.Value}.");
        if (light.Channel is < 0 or > 16)
            throw new DeskException($"The channel of a MIDI light is 1 to 16 (0 for its own), not {light.Channel}.");
        return light;
    }
}

/// <summary>
/// midi.json: the MIDI setup of this PC, beside the LAN password (%LOCALAPPDATA%\Lumenet). A file
/// that is missing is no setup; one that cannot be read is put aside as midi.json.bad, and the
/// desk starts without one.
/// </summary>
internal sealed class MidiSetupFile
{
    /// <summary>The name of the file, beside the LAN password.</summary>
    public const string FileName = "midi.json";

    private const string Format = "lumenet-midi";
    private const int Version = 1;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path;
    private readonly ILogger _log;

    /// <summary>The file at this path.</summary>
    /// <param name="path">Usually midi.json beside the LAN password.</param>
    /// <param name="log">Where a file that cannot be read is told about.</param>
    public MidiSetupFile(string path, ILogger log)
    {
        _path = path;
        _log = log;
    }

    /// <summary>The path of the file.</summary>
    public string Path => _path;

    /// <summary>The setup in the file; none when there is no file or it cannot be read.</summary>
    public MidiSetup Read()
    {
        if (!File.Exists(_path))
            return MidiSetup.Empty;

        try
        {
            FileShape? shape = JsonSerializer.Deserialize<FileShape>(File.ReadAllText(_path), Options);
            if (shape is null || shape.Format != Format)
                throw new DeskException("It is no MIDI setup of Lumenet.");

            return MidiSetupCheck.Checked(new MidiSetup(shape.Devices ?? [], shape.Mappings ?? []));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or DeskException or ArgumentException)
        {
            _log.LogWarning("The MIDI setup {File} cannot be read ({Reason}): the desk starts without one, and keeps the file as {Bad}.", _path, e.Message, _path + ".bad");
            TryMoveAside();
            return MidiSetup.Empty;
        }
    }

    /// <summary>Writes the setup, written beside the file first and then put in its place.</summary>
    /// <param name="setup">The setup, checked.</param>
    public void Write(MidiSetup setup)
    {
        string? folder = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        string written = _path + ".new";
        File.WriteAllText(written, ToText(setup));
        File.Move(written, _path, overwrite: true);
    }

    /// <summary>A setup as the file and Export have it.</summary>
    /// <param name="setup">The setup.</param>
    public static string ToText(MidiSetup setup)
    {
        return JsonSerializer.Serialize(new FileShape(Format, Version, setup.Devices, setup.Mappings), Options);
    }

    private void TryMoveAside()
    {
        try
        {
            File.Move(_path, _path + ".bad", overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // It stays where it is; the next start says the same again.
        }
    }

    private sealed record FileShape(string? Format, int Version, string[]? Devices, MidiMapping[]? Mappings);
}
