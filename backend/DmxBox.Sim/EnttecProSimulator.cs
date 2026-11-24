using System.Globalization;
using System.IO.Pipes;

namespace DmxBox.Sim;

/// <summary>
/// A simulated Enttec DMX USB Pro: a serial port "SIM1" (or SIM2 and on) that the desk lists while
/// the simulator runs, played through a named pipe ("lumenet-sim-SIM1"), as no serial port can be
/// made without a driver. It speaks the interface's protocol: it answers the questions for its
/// parameters (label 3) and its serial number (label 10), takes frames (label 6) and writes their
/// levels, and like the real one it stops its DMX at any question but label 3, until the next frame.
/// </summary>
internal sealed class EnttecProSimulator : ISimulator
{
    private static readonly TimeSpan LineEvery = TimeSpan.FromMilliseconds(200);

    // Firmware 1.44, a break of 9 x 10.67 us, a mark after break of 1 x 10.67 us, 40 frames a second.
    private static readonly byte[] Parameters = [44, 1, 9, 1, 40];

    private readonly SimulatorClaim _claim;
    private readonly string _serialNumber;
    private readonly int _show;
    private readonly TextWriter _output;
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly Lock _lock = new();
    private readonly List<string> _news = [];
    private CancellationTokenSource? _stop;
    private Task? _serving;
    private byte[]? _frame;
    private byte[]? _shown;
    private TimeSpan? _shownAt;
    private long _frames;
    private bool _stopped;

    /// <summary>Prepares the interface. Nothing runs until it is plugged in.</summary>
    /// <param name="folder">The registry of the simulators.</param>
    /// <param name="port">Its port, "SIM1"; null for the first free one.</param>
    /// <param name="serialNumber">The serial number it answers with: eight digits.</param>
    /// <param name="show">How many slots it writes when they change.</param>
    /// <param name="output">Where the lines go.</param>
    public EnttecProSimulator(string folder, string? port, string serialNumber, int show, TextWriter output)
    {
        if (serialNumber.Length != 8 || !serialNumber.All(char.IsAsciiDigit))
            throw new ArgumentException($"A serial number has eight digits, not \"{serialNumber}\".", nameof(serialNumber));

        _serialNumber = serialNumber;
        _show = Math.Clamp(show, 1, 512);
        _output = output;
        (Port, _claim) = ClaimPort(folder, port, serialNumber);
    }

    /// <summary>Its serial port, as the desk lists it: "SIM1".</summary>
    public string Port { get; }

    /// <summary>The levels of the last frame, slot 1 first; null before the first.</summary>
    public byte[]? Frame
    {
        get
        {
            lock (_lock)
            {
                return _frame;
            }
        }
    }

    /// <inheritdoc />
    public string Title => $"Enttec DMX USB Pro at {Port}, serial number {_serialNumber}";

    /// <inheritdoc />
    public string Finding => $"In the desk: Outputs, USB interfaces lists {Port}; Add plays on it.";

    /// <inheritdoc />
    public IReadOnlyList<(char Key, string What)> Keys { get; } = [];

    /// <inheritdoc />
    public bool PluggedIn => _stop is not null;

    /// <summary>The name of the pipe of a port: "lumenet-sim-SIM1". The desk's PipeDmxPort opens the same.</summary>
    /// <param name="port">"SIM1".</param>
    public static string PipeName(string port) => $"lumenet-sim-{port.ToUpperInvariant()}";

    /// <inheritdoc />
    public void PlugIn()
    {
        if (PluggedIn)
            return;

        _stop = new CancellationTokenSource();
        CancellationToken stop = _stop.Token;
        _serving = Task.Run(() => ServeAsync(stop));
        _claim.PluggedIn = true;
    }

    /// <inheritdoc />
    public void PullOut()
    {
        _claim.PluggedIn = false;
        if (_stop is null)
            return;

        _stop.Cancel();
        try
        {
            _serving?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // It ended with the pipe.
        }

        _stop.Dispose();
        _stop = null;
        _serving = null;
    }

    /// <inheritdoc />
    public void Press(char key)
    {
    }

    /// <inheritdoc />
    public void Tick()
    {
        TimeSpan now = _clock.Elapsed;
        var lines = new List<string>();
        lock (_lock)
        {
            lines.AddRange(_news);
            _news.Clear();
            bool soon = _shownAt is { } shownAt && now - shownAt < LineEvery;
            bool same = _shown is not null && _frame is not null && _frame.AsSpan(0, _show).SequenceEqual(_shown.AsSpan(0, _show));
            if (_frame is not null && !_stopped && !soon && !same)
            {
                _shown = _frame;
                _shownAt = now;
                lines.Add(string.Create(CultureInfo.InvariantCulture, $"DMX, frame {_frames}, 1-{_show}:{Shown.Levels(_frame, _show)}"));
            }
        }

        foreach (string line in lines)
            _output.WriteLine(line);
    }

    /// <summary>Pulls the interface out for good, and gives its port back.</summary>
    public void Dispose()
    {
        PullOut();
        _claim.Dispose();
    }

    // Claims the port asked for, or else the first of SIM1, SIM2 ... that no other simulator has.
    private static (string Port, SimulatorClaim Claim) ClaimPort(string folder, string? asked, string serialNumber)
    {
        if (asked is not null)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(asked, "^SIM[1-9][0-9]{0,2}$"))
                throw new ArgumentException($"A simulated port is SIM1 to SIM999, not \"{asked}\".", nameof(asked));
            return (asked, Claim(folder, asked, serialNumber));
        }

        for (int number = 1; number < 1000; number++)
        {
            string port = string.Create(CultureInfo.InvariantCulture, $"SIM{number}");
            try
            {
                return (port, Claim(folder, port, serialNumber));
            }
            catch (AddressTakenException)
            {
                // Another simulator's: the next one.
            }
        }

        throw new InvalidOperationException("Every port from SIM1 to SIM999 has a simulator.");
    }

    private static SimulatorClaim Claim(string folder, string port, string serialNumber)
    {
        return SimulatorRegistry.Claim(folder, new SimulatedDevice(
            SimulatedDevice.Usb, port, $"Enttec DMX USB Pro at {port}, serial number {serialNumber} (simulator)", Environment.ProcessId));
    }

    // Takes one program at a time, as a serial port does, until it is pulled out.
    private async Task ServeAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(
                PipeName(Port), PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            try
            {
                await pipe.WaitForConnectionAsync(stop);
                Say("The desk opened the port.");
                await TalkAsync(pipe, stop);
                Say("The desk closed the port.");
            }
            catch (OperationCanceledException)
            {
                return;   // pulled out: the pipe closes, and the desk's next write fails
            }
            catch (IOException)
            {
                Say("The desk closed the port.");
            }
        }
    }

    private async Task TalkAsync(NamedPipeServerStream pipe, CancellationToken stop)
    {
        byte[] buffer = new byte[1024];
        var received = new List<byte>();
        int count;
        while ((count = await pipe.ReadAsync(buffer, stop)) > 0)
        {
            received.AddRange(buffer.AsSpan(0, count));
            while (EnttecPro.TryTake(received, out byte label, out byte[] data))
            {
                byte[]? answer = Answer(label, data);
                if (answer is not null)
                    await pipe.WriteAsync(answer, stop);
            }
        }
    }

    // What the interface does with one message; its answer, or null.
    private byte[]? Answer(byte label, byte[] data)
    {
        lock (_lock)
        {
            if (label == EnttecPro.SendDmxLabel && data.Length >= 1)
            {
                byte[] frame = new byte[512];
                data.AsSpan(1, Math.Min(data.Length - 1, 512)).CopyTo(frame);
                _frame = frame;
                _frames++;
                if (_stopped)
                    _news.Add("DMX goes out again: a frame came.");
                _stopped = false;
                return null;
            }

            if (label == EnttecPro.ParametersLabel)
                return EnttecPro.Message(EnttecPro.ParametersLabel, Parameters);

            // Every other question stops the DMX until the next frame, as on the real interface.
            if (!_stopped && _frame is not null)
                _news.Add($"DMX stops at a question of label {label}, until the next frame.");
            _stopped = true;
            return label == EnttecPro.SerialNumberLabel ? EnttecPro.Message(EnttecPro.SerialNumberLabel, SerialNumberBytes()) : null;
        }
    }

    // "12345678" as the interface sends it: four bytes of two decimal digits each, the lowest first.
    private byte[] SerialNumberBytes()
    {
        byte[] bytes = new byte[4];
        for (int index = 0; index < 4; index++)
        {
            int high = _serialNumber[6 - index * 2] - '0';
            int low = _serialNumber[7 - index * 2] - '0';
            bytes[index] = (byte)((high << 4) | low);
        }

        return bytes;
    }

    private void Say(string line)
    {
        lock (_lock)
        {
            _news.Add(line);
        }
    }
}
