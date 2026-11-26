using System.IO.Pipes;
using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;

namespace DmxBox.Server.Midi;

/// <summary>The MIDI ports there are now, by name: the inputs and the outputs.</summary>
/// <param name="Inputs">The names of the inputs (what a controller sends comes in there).</param>
/// <param name="Outputs">The names of the outputs (what goes to a controller: its lights, its motors).</param>
internal sealed record MidiPortList(IReadOnlyList<string> Inputs, IReadOnlyList<string> Outputs)
{
    /// <summary>No ports at all.</summary>
    public static readonly MidiPortList None = new([], []);

    /// <summary>Every device: a name that is an input, an output or both, once, in the order found.</summary>
    public IEnumerable<string> Names => Inputs.Concat(Outputs).Distinct(StringComparer.Ordinal);
}

/// <summary>
/// The MIDI devices of this PC, behind an interface of the desk's own, so that the rest of the desk
/// does not depend on one library: DryWetMIDI on Windows and macOS (<see cref="DryWetMidiPorts"/>),
/// the simulators of sim.cmd (<see cref="SimulatedMidiPorts"/>), and a pretend one in the tests. A
/// library for Linux would be one more of these. A device is known by its name: its input and its
/// output have the same name, as with most controllers.
/// </summary>
internal interface IMidiPorts
{
    /// <summary>The ports there are now.</summary>
    MidiPortList List();

    /// <summary>
    /// Opens the input and the output of a device, those of them it has. What it sends goes to
    /// <paramref name="received"/>, on a thread of the library's, one message after the other.
    /// </summary>
    /// <param name="name">The device's name, as <see cref="List"/> has it.</param>
    /// <param name="received">Called with each message from the device.</param>
    /// <exception cref="MidiPortException">It cannot be opened: it is gone, or another program has it.</exception>
    IMidiConnection Open(string name, Action<MidiMessage> received);
}

/// <summary>An open MIDI device.</summary>
internal interface IMidiConnection : IDisposable
{
    /// <summary>True when the device has an output: its lights can be lit and its motors moved.</summary>
    bool CanSend { get; }

    /// <summary>True once the device failed (it was pulled out): it is closed, and opened again when it is back.</summary>
    bool Failed { get; }

    /// <summary>Sends a message to the device. False when it cannot: no output, or the device failed.</summary>
    /// <param name="message">The message.</param>
    bool Send(MidiMessage message);
}

/// <summary>A MIDI device cannot be opened; the message says why.</summary>
internal sealed class MidiPortException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// The MIDI devices of the PC through DryWetMIDI: WinMM on Windows, CoreMIDI on macOS. Elsewhere
/// (Linux) it has none. On Windows a device can be open in one program at a time, unless Windows
/// MIDI Services shares it: the desk opens only the devices it is told to use.
/// </summary>
internal sealed class DryWetMidiPorts : IMidiPorts
{
    /// <summary>True on the systems DryWetMIDI has devices on.</summary>
    public static bool Works => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    /// <inheritdoc />
    public MidiPortList List()
    {
        if (!Works)
            return MidiPortList.None;

        try
        {
            return new MidiPortList(Names(InputDevice.GetAll()), Names(OutputDevice.GetAll()));
        }
        catch (Exception e) when (IsDeviceTrouble(e))
        {
            return MidiPortList.None;
        }
    }

    /// <inheritdoc />
    public IMidiConnection Open(string name, Action<MidiMessage> received)
    {
        if (!Works)
            throw new MidiPortException("This system has no MIDI devices for the desk (Windows and macOS do).");

        return new Connection(name, received);
    }

    // The names of the devices, which are let go again: listing opens nothing.
    private static string[] Names<T>(ICollection<T> devices)
        where T : MidiDevice
    {
        try
        {
            return [.. devices.Select(device => device.Name)];
        }
        finally
        {
            foreach (T device in devices)
                device.Dispose();
        }
    }

    private static bool IsDeviceTrouble(Exception e)
    {
        return e is MidiDeviceException or DllNotFoundException or BadImageFormatException or EntryPointNotFoundException
            or InvalidOperationException or ArgumentException or PlatformNotSupportedException;
    }

    // One device of the PC, open: its input listens, its output is ready to send.
    private sealed class Connection : IMidiConnection
    {
        private readonly Lock _sending = new();
        private readonly Action<MidiMessage> _received;
        private InputDevice? _input;
        private OutputDevice? _output;
        private volatile bool _failed;

        public Connection(string name, Action<MidiMessage> received)
        {
            _received = received;
            try
            {
                _input = Take(InputDevice.GetAll(), name);
                _output = Take(OutputDevice.GetAll(), name);
                if (_input is null && _output is null)
                    throw new MidiPortException($"There is no MIDI device \"{name}\" now.");

                if (_input is not null)
                {
                    _input.EventReceived += OnEvent;
                    _input.ErrorOccurred += OnError;
                    _input.StartEventsListening();
                }

                if (_output is not null)
                {
                    _output.ErrorOccurred += OnError;
                    _output.PrepareForEventsSending();
                }
            }
            catch (Exception e) when (IsDeviceTrouble(e))
            {
                Dispose();
                throw new MidiPortException($"\"{name}\" cannot be opened: {e.Message} Another program may have it.", e);
            }
            catch (MidiPortException)
            {
                Dispose();
                throw;
            }
        }

        public bool CanSend => _output is not null && !_failed;

        public bool Failed => _failed;

        public bool Send(MidiMessage message)
        {
            if (_output is null || _failed || ToEvent(message) is not { } midiEvent)
                return false;

            try
            {
                lock (_sending)
                    _output.SendEvent(midiEvent);
                return true;
            }
            catch (Exception e) when (IsDeviceTrouble(e))
            {
                _failed = true;
                return false;
            }
        }

        public void Dispose()
        {
            InputDevice? input = Interlocked.Exchange(ref _input, null);
            OutputDevice? output = Interlocked.Exchange(ref _output, null);
            try
            {
                if (input is not null)
                {
                    input.EventReceived -= OnEvent;
                    input.ErrorOccurred -= OnError;
                    if (input.IsListeningForEvents)
                        input.StopEventsListening();
                    input.Dispose();
                }

                if (output is not null)
                {
                    output.ErrorOccurred -= OnError;
                    output.Dispose();
                }
            }
            catch (Exception e) when (IsDeviceTrouble(e))
            {
                // A device that is gone cannot be closed properly: it is let go all the same.
            }
        }

        // The device with this name, kept; the others are let go.
        private static T? Take<T>(ICollection<T> devices, string name)
            where T : MidiDevice
        {
            T? found = null;
            foreach (T device in devices)
            {
                if (found is null && device.Name == name)
                    found = device;
                else
                    device.Dispose();
            }

            return found;
        }

        private void OnEvent(object? sender, MidiEventReceivedEventArgs e)
        {
            if (FromEvent(e.Event) is { } message)
                _received(message);
        }

        private void OnError(object? sender, ErrorOccurredEventArgs e)
        {
            _failed = true;
        }

        private static MidiMessage? FromEvent(MidiEvent midiEvent)
        {
            return midiEvent switch
            {
                NoteOnEvent note when note.Velocity > 0 => new MidiMessage(MidiKind.NoteOn, note.Channel + 1, note.NoteNumber, note.Velocity),
                NoteOnEvent note => new MidiMessage(MidiKind.NoteOff, note.Channel + 1, note.NoteNumber, 0),
                NoteOffEvent note => new MidiMessage(MidiKind.NoteOff, note.Channel + 1, note.NoteNumber, note.Velocity),
                ControlChangeEvent control => new MidiMessage(MidiKind.ControlChange, control.Channel + 1, control.ControlNumber, control.ControlValue),
                PitchBendEvent bend => new MidiMessage(MidiKind.PitchBend, bend.Channel + 1, 0, bend.PitchValue),
                ProgramChangeEvent program => new MidiMessage(MidiKind.ProgramChange, program.Channel + 1, program.ProgramNumber, 0),
                _ => null,
            };
        }

        private static MidiEvent? ToEvent(MidiMessage message)
        {
            var channel = (FourBitNumber)(byte)(Math.Clamp(message.Channel, 1, 16) - 1);
            var number = (SevenBitNumber)(byte)Math.Clamp(message.Number, 0, 127);
            var value = (SevenBitNumber)(byte)Math.Clamp(message.Value, 0, 127);
            return message.Kind switch
            {
                MidiKind.NoteOn => new NoteOnEvent(number, value) { Channel = channel },
                MidiKind.NoteOff => new NoteOffEvent(number, value) { Channel = channel },
                MidiKind.ControlChange => new ControlChangeEvent(number, value) { Channel = channel },
                MidiKind.PitchBend => new PitchBendEvent((ushort)Math.Clamp(message.Value, 0, MidiMessage.MaxPitchBend)) { Channel = channel },
                MidiKind.ProgramChange => new ProgramChangeEvent(number) { Channel = channel },
                _ => null,
            };
        }
    }
}

/// <summary>
/// The simulated MIDI controllers of sim.cmd (<see cref="SimulatedDevice.Midi"/>): each is a device
/// with an input and an output under its name, "APC mini mk2 (simulator)", played through a named
/// pipe (<see cref="SimulatedDevice.MidiPipeName"/>) that carries the MIDI bytes both ways.
/// </summary>
internal sealed class SimulatedMidiPorts : IMidiPorts
{
    private static readonly TimeSpan ConnectWithin = TimeSpan.FromSeconds(1);

    private readonly Simulators _simulators;

    /// <summary>Finds the simulators in the desk's registry.</summary>
    /// <param name="simulators">The registry of the simulators.</param>
    public SimulatedMidiPorts(Simulators simulators)
    {
        _simulators = simulators;
    }

    /// <inheritdoc />
    public MidiPortList List()
    {
        string[] names = [.. Devices().Select(device => device.Name)];
        return new MidiPortList(names, names);
    }

    /// <inheritdoc />
    public IMidiConnection Open(string name, Action<MidiMessage> received)
    {
        SimulatedDevice device = Devices().FirstOrDefault(found => found.Name == name)
            ?? throw new MidiPortException($"There is no simulated MIDI device \"{name}\" now.");

        var pipe = new NamedPipeClientStream(".", SimulatedDevice.MidiPipeName(device.Address), PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            pipe.Connect(ConnectWithin);
        }
        catch (Exception e) when (e is TimeoutException or IOException or UnauthorizedAccessException)
        {
            pipe.Dispose();
            throw new MidiPortException($"\"{name}\" cannot be opened: {e.Message} Another desk may have it.", e);
        }

        return new PipeConnection(pipe, received);
    }

    private IEnumerable<SimulatedDevice> Devices()
    {
        return _simulators.Devices.Where(device => device.Kind == SimulatedDevice.Midi);
    }

    // The pipe of one simulated controller: read on a task of its own until it closes.
    private sealed class PipeConnection : IMidiConnection
    {
        private readonly NamedPipeClientStream _pipe;
        private readonly Action<MidiMessage> _received;
        private readonly Lock _sending = new();
        private readonly CancellationTokenSource _stop = new();
        private volatile bool _failed;

        public PipeConnection(NamedPipeClientStream pipe, Action<MidiMessage> received)
        {
            _pipe = pipe;
            _received = received;
            _ = Task.Run(ReadAsync);
        }

        public bool CanSend => !_failed;

        public bool Failed => _failed;

        public bool Send(MidiMessage message)
        {
            if (_failed)
                return false;

            try
            {
                lock (_sending)
                {
                    _pipe.Write(message.ToBytes());
                    _pipe.Flush();
                }

                return true;
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException)
            {
                _failed = true;
                return false;
            }
        }

        public void Dispose()
        {
            _failed = true;
            _stop.Cancel();
            _pipe.Dispose();
        }

        private async Task ReadAsync()
        {
            var reader = new MidiStreamReader();
            byte[] buffer = new byte[256];
            try
            {
                int count;
                while ((count = await _pipe.ReadAsync(buffer, _stop.Token)) > 0)
                    reader.Take(buffer.AsSpan(0, count), _received);
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
            {
                // Pulled out, or closed here.
            }

            _failed = true;
        }
    }
}

/// <summary>The MIDI devices of several kinds as one list: those of the PC and the simulated ones.</summary>
internal sealed class CombinedMidiPorts : IMidiPorts
{
    private readonly IMidiPorts[] _parts;

    /// <summary>Puts the kinds together; a name found in two of them is the first one's.</summary>
    /// <param name="parts">The kinds.</param>
    public CombinedMidiPorts(params IMidiPorts[] parts)
    {
        _parts = parts;
    }

    /// <inheritdoc />
    public MidiPortList List()
    {
        MidiPortList[] lists = [.. _parts.Select(part => part.List())];
        return new MidiPortList(
            [.. lists.SelectMany(list => list.Inputs).Distinct(StringComparer.Ordinal)],
            [.. lists.SelectMany(list => list.Outputs).Distinct(StringComparer.Ordinal)]);
    }

    /// <inheritdoc />
    public IMidiConnection Open(string name, Action<MidiMessage> received)
    {
        foreach (IMidiPorts part in _parts)
        {
            if (part.List().Names.Contains(name, StringComparer.Ordinal))
                return part.Open(name, received);
        }

        throw new MidiPortException($"There is no MIDI device \"{name}\" now.");
    }
}
