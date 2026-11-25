using System.Globalization;
using System.Reflection;
using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// Pretend MIDI devices for the tests: devices are plugged in and pulled out, a device plays
/// messages into the desk, and what the desk sends back is written down.
/// </summary>
internal sealed class PretendMidiPorts : IMidiPorts
{
    private readonly Lock _lock = new();
    private readonly List<PretendMidiDevice> _devices = [];

    /// <summary>A device plugged in, with an input and an output under its name unless said otherwise.</summary>
    public PretendMidiDevice Plug(string name, bool input = true, bool output = true)
    {
        lock (_lock)
        {
            PretendMidiDevice device = _devices.FirstOrDefault(found => found.Name == name) ?? new PretendMidiDevice(name);
            device.Input = input;
            device.Output = output;
            device.PluggedIn = true;
            if (!_devices.Contains(device))
                _devices.Add(device);
            return device;
        }
    }

    /// <summary>The device is pulled out: it is gone, and an open connection to it fails.</summary>
    public void Unplug(string name)
    {
        lock (_lock)
        {
            PretendMidiDevice? device = _devices.FirstOrDefault(found => found.Name == name);
            if (device is not null)
            {
                device.PluggedIn = false;
                device.Fail();
            }
        }
    }

    public MidiPortList List()
    {
        lock (_lock)
        {
            PretendMidiDevice[] there = [.. _devices.Where(device => device.PluggedIn)];
            return new MidiPortList(
                [.. there.Where(device => device.Input).Select(device => device.Name)],
                [.. there.Where(device => device.Output).Select(device => device.Name)]);
        }
    }

    public IMidiConnection Open(string name, Action<MidiMessage> received)
    {
        lock (_lock)
        {
            PretendMidiDevice device = _devices.FirstOrDefault(found => found.Name == name && found.PluggedIn)
                ?? throw new MidiPortException($"There is no MIDI device \"{name}\" now.");
            if (device.Busy)
                throw new MidiPortException($"\"{name}\" cannot be opened: another program has it.");
            return device.Open(received);
        }
    }
}

/// <summary>One pretend device.</summary>
internal sealed class PretendMidiDevice(string name)
{
    private readonly Lock _lock = new();
    private readonly List<MidiMessage> _sent = [];
    private Connection? _connection;

    public string Name { get; } = name;

    public bool Input { get; set; } = true;

    public bool Output { get; set; } = true;

    public bool PluggedIn { get; set; }

    /// <summary>True while another program has it: it cannot be opened.</summary>
    public bool Busy { get; set; }

    /// <summary>How many times the desk opened it.</summary>
    public int Opened { get; private set; }

    /// <summary>True while the desk has it open.</summary>
    public bool IsOpen => _connection is { Closed: false };

    /// <summary>What the desk sent to it, in order.</summary>
    public MidiMessage[] Sent
    {
        get
        {
            lock (_lock)
                return [.. _sent];
        }
    }

    /// <summary>Forgets what was sent so far.</summary>
    public void ClearSent()
    {
        lock (_lock)
            _sent.Clear();
    }

    /// <summary>The device sends a message to the desk.</summary>
    public void Play(MidiMessage message)
    {
        Connection? connection = _connection;
        if (connection is { Closed: false } && Input)
            connection.Received(message);
    }

    public void Note(int number, int velocity, int channel = 1) =>
        Play(new MidiMessage(velocity > 0 ? MidiKind.NoteOn : MidiKind.NoteOff, channel, number, velocity));

    public void Controller(int number, int value, int channel = 1) => Play(new MidiMessage(MidiKind.ControlChange, channel, number, value));

    public void Bend(int value, int channel = 1) => Play(new MidiMessage(MidiKind.PitchBend, channel, 0, value));

    public IMidiConnection Open(Action<MidiMessage> received)
    {
        Opened++;
        _connection = new Connection(this, received);
        return _connection;
    }

    public void Fail()
    {
        if (_connection is not null)
            _connection.Failed = true;
    }

    private void Note(MidiMessage message)
    {
        lock (_lock)
            _sent.Add(message);
    }

    private sealed class Connection(PretendMidiDevice device, Action<MidiMessage> received) : IMidiConnection
    {
        public Action<MidiMessage> Received { get; } = received;

        public bool Closed { get; private set; }

        public bool CanSend => device.Output && !Failed && !Closed;

        public bool Failed { get; set; }

        public bool Send(MidiMessage message)
        {
            if (!CanSend)
                return false;
            device.Note(message);
            return true;
        }

        public void Dispose() => Closed = true;
    }
}

/// <summary>What the MIDI tells the pages, written down.</summary>
internal sealed class PretendMidiPages : IMidiPagesOut
{
    private readonly Lock _lock = new();

    public List<MidiState> States { get; } = [];

    public List<(string Connection, string Key, bool Down)> Keys { get; } = [];

    public List<(string Connection, MidiRole Role)> Roles { get; } = [];

    public List<(string[] Watchers, MidiMonitorLine[] Lines)> Lines { get; } = [];

    public MidiState Last
    {
        get
        {
            lock (_lock)
                return States[^1];
        }
    }

    public void Changed(MidiState state)
    {
        lock (_lock)
            States.Add(state);
    }

    public void Key(string connection, string key, bool down)
    {
        lock (_lock)
            Keys.Add((connection, key, down));
    }

    public void Role(string connection, MidiRole role)
    {
        lock (_lock)
            Roles.Add((connection, role));
    }

    void IMidiPagesOut.Lines(IReadOnlyList<string> watchers, MidiMonitorLine[] lines)
    {
        lock (_lock)
            Lines.Add(([.. watchers], lines));
    }
}

/// <summary>A pretend desk of faders for the mapper: where each stands, and moves that can be refused.</summary>
internal sealed class PretendMidiDesk : IMidiDesk
{
    private readonly Dictionary<DeskFader, int> _positions = [];

    public MidiPages Pages { get; set; } = MidiPages.First;

    public int ChannelCount { get; set; } = 512;

    /// <summary>Why the desk refuses every move now; null while it takes them.</summary>
    public string? Refusing { get; set; }

    /// <summary>The faders moved, in order.</summary>
    public List<(DeskFader Fader, int Position)> Moves { get; } = [];

    public int this[FaderKind kind, int number = 0]
    {
        get => _positions.GetValueOrDefault(new DeskFader(kind, number));
        set => _positions[new DeskFader(kind, number)] = value;
    }

    /// <summary>Faders that are not there (an empty effect, a memory without a sequence).</summary>
    public HashSet<DeskFader> Missing { get; } = [];

    public void Refresh()
    {
        // Always fresh.
    }

    public int? PositionOf(DeskFader fader) => Missing.Contains(fader) ? null : _positions.GetValueOrDefault(fader);

    public FaderMove Move(DeskFader fader, int position)
    {
        if (Refusing is not null)
            return new FaderMove(null, Refusing);
        _positions[fader] = position;
        Moves.Add((fader, position));
        return new FaderMove(position, null);
    }
}

/// <summary>Stands in for every page: writes down each message the desk sends them, as "ChannelChanged 5 200".</summary>
public class NotedPages : DispatchProxy
{
    private readonly List<string> _notes = [];

    /// <summary>The messages, in order.</summary>
    public IReadOnlyList<string> Notes
    {
        get
        {
            lock (_notes)
                return [.. _notes];
        }
    }

    /// <summary>A new one, as the pages the desk talks to.</summary>
    public static (IDeskClient Pages, NotedPages Notes) Make()
    {
        IDeskClient pages = Create<IDeskClient, NotedPages>();
        return (pages, (NotedPages)(object)pages);
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        string note = string.Join(' ', new[] { targetMethod?.Name ?? "?" }.Concat((args ?? []).Select(Said)));
        lock (_notes)
            _notes.Add(note);
        return Task.CompletedTask;
    }

    private static string Said(object? value) => value switch
    {
        null => "null",
        double number => number.ToString(CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        EffectPlayback playback => FormattableString.Invariant($"effect {playback.Number} level {playback.Level} speed {playback.Speed} size {playback.Size} spread {playback.Spread}"),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "?",
    };
}
