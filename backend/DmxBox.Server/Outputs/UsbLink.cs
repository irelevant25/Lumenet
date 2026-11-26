using System.Diagnostics;

namespace DmxBox.Server.Outputs;

/// <summary>
/// Keeps one DMX USB interface of the kind of the Enttec DMX USB Pro playing a universe of the
/// desk, for as long as the project lists it: it opens the interface's serial port when it is
/// there, sends it every change of the universe (the interface repeats the last frame on the
/// cable by itself), and closes it when the interface is pulled out, to open it again when it is
/// back. <see cref="OutputNetwork"/> has a link for every USB output.
/// </summary>
/// <remarks>
/// <para>
/// Every 2 seconds the link asks the interface for its parameters: an interface that answers is
/// connected. That is the one question that does not stop its DMX; the serial number, which does,
/// is asked once, right after the port opens and before the first frame.
/// </para>
/// <para>
/// The link has a thread of its own, because a write to an interface that hangs blocks. All of
/// its work is done on that thread, one step after the other.
/// </para>
/// </remarks>
internal sealed class UsbLink : IDisposable
{
    // 40 rounds a second, as for the boxes.
    private static readonly TimeSpan RoundInterval = TimeSpan.FromMilliseconds(25);

    // How often a missing interface is looked for, and whether an open one is still there.
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    // An interface that is not written to for a while gets the frame again: that is how a write
    // finds out that it was pulled out.
    private static readonly TimeSpan KeepAlive = TimeSpan.FromSeconds(1);

    // How often the interface is asked for its parameters, and how long an answer counts.
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(5);

    // How long after opening the port the interface has to answer before it counts as silent.
    private static readonly TimeSpan FirstAnswer = TimeSpan.FromSeconds(1);

    // More unread bytes than this are garbage: no answer of the interface is that long.
    private const int MaxReceived = 4096;

    private readonly Desk _desk;
    private readonly string _name;
    private readonly Func<string, IDmxPort> _portAt;
    private readonly Func<string[]> _names;
    private readonly Action<string, string> _report;
    private readonly ILogger _logger;
    private volatile int _universe;

    // Everything below belongs to the link's thread alone.
    private IDmxPort? _port;
    private TimeSpan _nextTry;
    private TimeSpan _openedAt;
    private TimeSpan _sentAt;
    private TimeSpan _nextCheck;
    private TimeSpan _nextPresence;
    private TimeSpan? _answeredAt;
    private string? _firmware;
    private string? _serialNumber;
    private byte[]? _last;
    private long _sentChanges = -1;
    private int _sentUniverse = -1;
    private readonly List<byte> _received = [];
    private readonly byte[] _buffer = new byte[1024];
    private (string State, string Detail)? _reported;
    private string? _lastFailure;

    /// <summary>Prepares the link. Nothing is opened until it is run.</summary>
    /// <param name="desk">The desk whose output the interface has to play.</param>
    /// <param name="name">The interface's serial port: "COM3", "/dev/ttyUSB0".</param>
    /// <param name="universe">The universe it plays, 1 to 8, or 0 for none: then its cable stays dark.</param>
    /// <param name="portAt">Makes the port of a name; the tests make pretend ones.</param>
    /// <param name="names">The serial ports of this PC now.</param>
    /// <param name="report">Takes the state when it changes, and what it means.</param>
    /// <param name="logger">Gets a line when the interface comes or goes.</param>
    public UsbLink(
        Desk desk,
        string name,
        int universe,
        Func<string, IDmxPort> portAt,
        Func<string[]> names,
        Action<string, string> report,
        ILogger logger)
    {
        _desk = desk;
        _name = name;
        _universe = universe;
        _portAt = portAt;
        _names = names;
        _report = report;
        _logger = logger;
    }

    /// <summary>The universe the interface plays now, 1 to 8, or 0 for none. Any thread may set it.</summary>
    public int Universe
    {
        set => _universe = value;
    }

    /// <summary>
    /// One round: open the interface, or serve the one that is open. The link's thread does 40
    /// rounds a second. The tests call this themselves instead of starting the thread.
    /// </summary>
    /// <param name="now">The time on a clock that only runs forward.</param>
    public void Step(TimeSpan now)
    {
        try
        {
            if (_port is not null)
                Serve(now);
            else if (now >= _nextTry)
                Open(now);
        }
        catch (Exception e)
        {
            // Whatever went wrong, the desk has to carry on: the interface counts as missing
            // until it opens again.
            Close(now, e);
        }
    }

    /// <summary>The link's thread: 40 rounds a second until it is told to stop.</summary>
    /// <param name="stopping">Signals that the link is to stop.</param>
    public void Run(CancellationToken stopping)
    {
        var clock = Stopwatch.StartNew();
        TimeSpan nextRound = TimeSpan.Zero;
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                Step(clock.Elapsed);
            }
            catch (Exception)
            {
                // Step handles the failures of the interface; this is the log failing while the
                // program shuts down. Nothing may leave the thread.
                if (stopping.IsCancellationRequested)
                    return;
            }

            nextRound += RoundInterval;
            TimeSpan wait = nextRound - clock.Elapsed;
            if (wait > TimeSpan.Zero)
                stopping.WaitHandle.WaitOne(wait);
            else
                nextRound = clock.Elapsed;
        }
    }

    /// <summary>Closes the port. Stop the link's thread first.</summary>
    public void Dispose()
    {
        _port?.Dispose();
        _port = null;
    }

    private void Open(TimeSpan now)
    {
        _nextTry = now + RetryInterval;
        if (!_names().Contains(_name, StringComparer.OrdinalIgnoreCase))
        {
            Report(OutputView.Missing, $"{_name} is not there: is the interface plugged in?");
            return;
        }

        IDmxPort port = _portAt(_name);
        try
        {
            port.Open();
        }
        catch
        {
            port.Dispose();
            throw;
        }

        _port = port;
        _openedAt = now;
        _answeredAt = null;
        _firmware = null;
        _serialNumber = null;
        _last = null;
        _sentChanges = -1;
        _received.Clear();
        _lastFailure = null;
        _logger.LogInformation("The USB interface at {Port} is open.", _name);

        // The serial number first: the interface stops its DMX at that question, until the next frame.
        port.Write(EnttecPro.SerialNumberRequest());
        port.Write(EnttecPro.ParametersRequest());
        _nextCheck = now + CheckInterval;
        _nextPresence = now + RetryInterval;
        SendFrame(now);
        Report(OutputView.Missing, $"{_name} is open; waiting for its answer.");
    }

    private void Serve(TimeSpan now)
    {
        IDmxPort port = _port!;
        ReadAnswers(port, now);
        SendFrame(now);

        if (now >= _nextCheck)
        {
            _nextCheck = now + CheckInterval;
            port.Write(EnttecPro.ParametersRequest());
        }

        // A port that went away can take its time to fail a write; the list of ports knows at once.
        if (now >= _nextPresence)
        {
            _nextPresence = now + RetryInterval;
            if (!_names().Contains(_name, StringComparer.OrdinalIgnoreCase))
                throw new IOException($"{_name} went away: the interface was pulled out.");
        }

        if (_answeredAt is { } at && now - at <= AnswerTimeout)
            Report(OutputView.Connected, About());
        else if (now - _openedAt >= FirstAnswer)
            Report(OutputView.Missing, $"{_name} does not answer as a DMX USB Pro does. The frames go to it anyway.");
    }

    // The interface's universe, when it changed or the universe it plays did, and again after a
    // second without a change.
    private void SendFrame(TimeSpan now)
    {
        long changes = _desk.OutputChanges;
        int universe = _universe;
        if (changes != _sentChanges || universe != _sentUniverse)
        {
            byte[] output = _desk.GetOutput();
            byte[] levels = new byte[Desk.UniverseSize];
            if (universe >= 1 && universe * Desk.UniverseSize <= output.Length)
                Array.Copy(output, (universe - 1) * Desk.UniverseSize, levels, 0, Desk.UniverseSize);

            _sentChanges = changes;
            _sentUniverse = universe;
            if (_last is null || !levels.AsSpan().SequenceEqual(_last))
            {
                Write(levels, now);
                return;
            }
        }

        if (_last is not null && now - _sentAt >= KeepAlive)
            Write(_last, now);
    }

    private void Write(byte[] levels, TimeSpan now)
    {
        _port!.Write(EnttecPro.DmxMessage(levels));
        _last = levels;
        _sentAt = now;
    }

    private void ReadAnswers(IDmxPort port, TimeSpan now)
    {
        int count;
        while ((count = port.Read(_buffer)) > 0)
            _received.AddRange(_buffer.AsSpan(0, count));

        if (_received.Count > MaxReceived)
            _received.Clear();

        while (EnttecPro.TryTake(_received, out byte label, out byte[] data))
        {
            if (label == EnttecPro.ParametersLabel && EnttecPro.FirmwareOf(data) is { } firmware)
            {
                _firmware = firmware;
                _answeredAt = now;
            }
            else if (label == EnttecPro.SerialNumberLabel && EnttecPro.SerialNumberOf(data) is { } serialNumber)
            {
                _serialNumber = serialNumber;
                _answeredAt = now;
            }
        }
    }

    // "Serial number 01234567, firmware 1.44", as far as the interface said them.
    private string About()
    {
        string[] parts =
        [
            _serialNumber is null ? "" : $"serial number {_serialNumber}",
            _firmware is null ? "" : $"firmware {_firmware}",
        ];
        string about = string.Join(", ", parts.Where(part => part.Length > 0));
        return about.Length == 0 ? "It answers as a DMX USB Pro does." : char.ToUpperInvariant(about[0]) + about[1..];
    }

    private void Close(TimeSpan now, Exception reason)
    {
        bool wasOpen = _port is not null;
        _port?.Dispose();
        _port = null;
        _nextTry = now + RetryInterval;

        string why = reason switch
        {
            UnauthorizedAccessException => $"{_name} is in use by another program.",
            TimeoutException => $"{_name} does not take the frames.",
            _ => reason.Message,
        };

        if (why != _lastFailure)
        {
            _lastFailure = why;
            Exception? fault = reason is IOException or UnauthorizedAccessException or TimeoutException or InvalidOperationException ? null : reason;
            if (wasOpen)
                _logger.LogWarning(fault, "The USB interface at {Port} is gone: {Reason}", _name, why);
            else
                _logger.LogWarning(fault, "The USB interface at {Port} does not open: {Reason}", _name, why);
        }

        Report(OutputView.Missing, why);
    }

    private void Report(string state, string detail)
    {
        if (_reported is { } reported && reported.State == state && reported.Detail == detail)
            return;

        _reported = (state, detail);
        _report(state, detail);
    }
}
