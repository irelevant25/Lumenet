namespace DmxBox.Server.Midi;

/// <summary>
/// The DMX inputs of the boxes as MIDI devices, so that a console plays the desk as a controller
/// does: a box that answers and has one (dmxbox 4) lists its DMX IN as "DMX IN (box 3)", an input
/// without an output. Opened, its box link asks the box for what came in, in every round of its
/// own (40 a second), and every channel whose level changed comes as a message
/// (<see cref="MidiKind.Dmx"/>: the channel, 1-512, as the number, the level as the value). The
/// mappings, Learn, the pickup of a fader and the keys are those of any controller.
/// </summary>
internal sealed class DmxInPorts : IMidiPorts
{
    private readonly BoxNetwork _boxes;

    /// <summary>The DMX inputs of these boxes.</summary>
    /// <param name="boxes">The boxes the desk plays.</param>
    public DmxInPorts(BoxNetwork boxes)
    {
        _boxes = boxes;
    }

    /// <inheritdoc />
    public MidiPortList List()
    {
        return new MidiPortList(_boxes.InputNames(), []);
    }

    /// <inheritdoc />
    public IMidiConnection Open(string name, Action<MidiMessage> received)
    {
        InputListener listener = _boxes.ListenToInput(name, (channel, level) => received(new MidiMessage(MidiKind.Dmx, 1, channel, level)))
            ?? throw new MidiPortException($"There is no \"{name}\" now: no box with that DMX IN answers.");
        return new Connection(listener);
    }

    // A DMX IN, open: it only listens.
    private sealed class Connection(InputListener listener) : IMidiConnection
    {
        public bool CanSend => false;

        public bool Failed => listener.Gone;

        public bool Send(MidiMessage message) => false;

        public void Dispose() => listener.Stop();
    }
}
