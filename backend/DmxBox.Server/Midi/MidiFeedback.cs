namespace DmxBox.Server.Midi;

/// <summary>
/// The lamp of a key of the desk, as the page on the PC shows it: the light of a controller's key
/// mapped to it follows it.
/// </summary>
/// <param name="Key">The key, by its name ("memory.5").</param>
/// <param name="Level">1 faint, 2 half lit, 3 lit.</param>
/// <param name="Blink">True while it blinks (a Record function waits for it).</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record MidiLamp(string Key, int Level, bool Blink);

/// <summary>
/// What goes back to the controllers: the lights of their keys, lit as the desk's keys are (the
/// page on the PC says how, <see cref="MidiLamp"/>), and the values of the faders with feedback (a
/// motorized fader, the ring of lights of an encoder), from wherever they changed: a page, a
/// phone, the stack. Only what changed goes out. A fader moved by hand gets nothing for
/// <see cref="HandsOff"/> seconds, so that its motor does not fight the hand. A key's light that
/// has no blinking of its own is blinked by the desk, twice a second.
/// </summary>
/// <remarks>Not thread-safe: <see cref="MidiControl"/> holds its lock around it.</remarks>
internal sealed class MidiFeedback
{
    /// <summary>Seconds after a fader moved by hand before its feedback goes out again.</summary>
    public const double HandsOff = 0.4;

    // What each control was sent last: a light as its channel and value, a fader as its value.
    private readonly Dictionary<(string Device, int Channel, string Message, int Number), int> _sent = [];

    /// <summary>Forgets what a device was sent: everything goes to it again (it was plugged in, or opened).</summary>
    /// <param name="device">The device.</param>
    public void Forget(string device)
    {
        foreach (var control in _sent.Keys.Where(control => control.Device == device).ToArray())
            _sent.Remove(control);
    }

    /// <summary>
    /// A control was pressed or moved: what it was sent is forgotten, and it gets the desk's state
    /// again. A controller may light a key itself while it is held, and a motor may stand where the
    /// hand left it when the desk did not follow (pickup).
    /// </summary>
    /// <param name="device">The device.</param>
    /// <param name="message">What came from it.</param>
    public void Touched(string device, MidiMessage message)
    {
        switch (message.Kind)
        {
            case MidiKind.NoteOn or MidiKind.NoteOff:
                _sent.Remove((device, message.Channel, MidiMapping.Note, message.Number));
                break;
            case MidiKind.ControlChange:
                _sent.Remove((device, message.Channel, MidiMapping.Controller, message.Number));
                _sent.Remove((device, message.Channel, MidiMapping.Controller14, message.Number >= 32 ? message.Number - 32 : message.Number));
                break;
            case MidiKind.PitchBend:
                _sent.Remove((device, message.Channel, MidiMapping.PitchBend, 0));
                break;
        }
    }

    /// <summary>The messages that bring the controllers up to date.</summary>
    /// <param name="mappings">The mappings of the devices that are open.</param>
    /// <param name="lamps">The lamps of the desk's keys, by name; a key the page has not told of keeps its light.</param>
    /// <param name="mapper">For the faders: which fader a slot shows, and whether a hand moves one.</param>
    /// <param name="desk">Where the faders stand.</param>
    /// <param name="now">Seconds, on the mapper's clock.</param>
    /// <param name="blinkOn">True in the lit half of the desk's blinking.</param>
    public List<(string Device, MidiMessage Message)> Changes(
        IEnumerable<MidiMapping> mappings, IReadOnlyDictionary<string, MidiLamp> lamps, MidiMapper mapper, IMidiDesk desk, double now, bool blinkOn)
    {
        var changes = new List<(string, MidiMessage)>();
        foreach (MidiMapping mapping in mappings)
        {
            if (ControlTarget.Parse(mapping.Target) is not { } target)
                continue;

            if (!mapping.Feedback)
                continue;
            if (target.IsKey)
                Light(mapping, lamps, blinkOn, changes);
            else if (!mapper.MovedLately(mapping, now, HandsOff))
                Value(mapping, target, mapper, desk, changes);
        }

        return changes;
    }

    private void Light(MidiMapping mapping, IReadOnlyDictionary<string, MidiLamp> lamps, bool blinkOn, List<(string, MidiMessage)> changes)
    {
        if (mapping.Message == MidiMapping.Program || !lamps.TryGetValue(mapping.Target, out MidiLamp? lamp))
            return;

        MidiLights lights = mapping.Lights ?? MidiLights.Usual;
        MidiLight light = lamp switch
        {
            { Blink: true } => lights.Blink ?? (blinkOn ? lights.Full : lights.Off),
            { Level: >= 3 } => lights.Full,
            { Level: 2 } => lights.Half,
            _ => lights.Off,
        };

        int channel = light.Channel == 0 ? mapping.Channel : light.Channel;
        int sent = (channel * 128) + light.Value;
        if (_sent.TryGetValue(mapping.Control, out int before) && before == sent)
            return;

        _sent[mapping.Control] = sent;
        MidiKind kind = mapping.Message == MidiMapping.Note ? MidiKind.NoteOn : MidiKind.ControlChange;
        changes.Add((mapping.Device, new MidiMessage(kind, channel, lights.Number ?? mapping.Number, light.Value)));
    }

    private void Value(MidiMapping mapping, ControlTarget target, MidiMapper mapper, IMidiDesk desk, List<(string, MidiMessage)> changes)
    {
        // A slot beyond the page, or a fader that is not there: the motor goes down.
        int position = mapper.Resolve(target) is { } fader ? desk.PositionOf(fader) ?? 0 : 0;
        int value = mapping.Message switch
        {
            MidiMapping.Controller => (int)Math.Round(position * 127.0 / FaderScale.Top),
            _ => (int)Math.Round(position * (double)MidiMessage.MaxPitchBend / FaderScale.Top),
        };

        if (_sent.TryGetValue(mapping.Control, out int before) && before == value)
            return;

        _sent[mapping.Control] = value;
        switch (mapping.Message)
        {
            case MidiMapping.Controller:
                changes.Add((mapping.Device, new MidiMessage(MidiKind.ControlChange, mapping.Channel, mapping.Number, value)));
                break;
            case MidiMapping.Controller14:
                changes.Add((mapping.Device, new MidiMessage(MidiKind.ControlChange, mapping.Channel, mapping.Number, value >> 7)));
                changes.Add((mapping.Device, new MidiMessage(MidiKind.ControlChange, mapping.Channel, mapping.Number + 32, value & 0x7F)));
                break;
            case MidiMapping.PitchBend:
                changes.Add((mapping.Device, new MidiMessage(MidiKind.PitchBend, mapping.Channel, 0, value)));
                break;
        }
    }
}
