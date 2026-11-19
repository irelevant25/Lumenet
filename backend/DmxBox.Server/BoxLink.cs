using System.Diagnostics;
using System.Net.Sockets;

namespace DmxBox.Server;

/// <summary>
/// Keeps the box showing what the desk says, for as long as the server runs. It looks for the
/// box until it answers, sends it every change of the desk's output, and watches for the two
/// things the box does by itself: its blackout button, and a restart.
/// </summary>
/// <remarks>
/// The link has a thread of its own, because a request to a box that does not answer blocks
/// for most of a second. All requests come from that thread, one after the other.
/// </remarks>
internal sealed class BoxLink : BackgroundService
{
    // 40 rounds a second. The box sends about 43 DMX frames a second, so more would be wasted.
    private static readonly TimeSpan RoundInterval = TimeSpan.FromMilliseconds(25);

    // How often a connected box is asked for its state.
    private static readonly TimeSpan PingInterval = TimeSpan.FromMilliseconds(500);

    // How often a missing box is looked for.
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    private readonly Desk _desk;
    private readonly IBox _box;
    private readonly IDeskClient _clients;
    private readonly ILogger<BoxLink> _logger;

    // Read by the hub, replaced by the link's thread.
    private volatile BoxStatus _status;

    // Everything below belongs to the link's thread alone.
    private bool _connected;
    private TimeSpan _nextPing;     // when to ping next: to find the box, or to check on it
    private TimeSpan _lastUptime;   // the box's uptime at the last ping
    private bool _boxBlackout;      // the blackout state the link last gave the box, or took from it
    private long _sentOutput;       // the desk's OutputChanges that belong to the last frame sent
    private string? _lastFailure;   // why the box was not reached the last time

    /// <summary>Prepares the link. Nothing is sent until the server starts it.</summary>
    /// <param name="desk">The desk whose output the box has to show.</param>
    /// <param name="box">The box. The link closes it when the server stops.</param>
    /// <param name="clients">
    /// All connected UIs. They are told when the box comes or goes, and when its blackout button was pressed.
    /// </param>
    /// <param name="host">The address of the box, for the UI and the log.</param>
    /// <param name="logger">Gets a line when the box comes or goes.</param>
    public BoxLink(Desk desk, IBox box, IDeskClient clients, string host, ILogger<BoxLink> logger)
    {
        _desk = desk;
        _box = box;
        _clients = clients;
        _logger = logger;
        _status = new BoxStatus(Connected: false, host, Firmware: null);
    }

    /// <summary>Whether the box is connected right now.</summary>
    public BoxStatus Status => _status;

    /// <summary>
    /// One round of the link's work: look for the box, or serve the box that is connected.
    /// The link's thread does 40 rounds a second. The tests call this themselves instead of
    /// starting the thread; never do both.
    /// </summary>
    /// <param name="now">
    /// The time on a clock that only runs forward, for example the time since the server started.
    /// </param>
    public void Step(TimeSpan now)
    {
        try
        {
            if (_connected)
                Serve(now);
            else if (now >= _nextPing)
                Connect(now);
        }
        catch (Exception e)
        {
            // Whatever went wrong, the server has to carry on. The box counts as not connected
            // until a ping gets through again.
            Disconnect(now, e);
        }
    }

    /// <summary>Stops the link's thread and closes the connection to the box.</summary>
    public override void Dispose()
    {
        base.Dispose();
        _box.Dispose();
    }

    /// <summary>Starts the link's thread. The host calls this once, when the server starts.</summary>
    /// <param name="stoppingToken">Signals that the server is shutting down.</param>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Factory.StartNew(
            () => Run(stoppingToken), stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    private void Run(CancellationToken stopping)
    {
        var clock = Stopwatch.StartNew();
        TimeSpan nextRound = TimeSpan.Zero;
        while (!stopping.IsCancellationRequested)
        {
            Step(clock.Elapsed);

            // Wait until the next round is due, not for a fixed 25 ms: Windows wakes a thread
            // in steps of about 16 ms, and this way the error does not add up.
            nextRound += RoundInterval;
            TimeSpan wait = nextRound - clock.Elapsed;
            if (wait > TimeSpan.Zero)
                stopping.WaitHandle.WaitOne(wait);
            else
                nextRound = clock.Elapsed;   // a request took long: carry on from here, do not catch up
        }
    }

    private void Connect(TimeSpan now)
    {
        PingInfo info = _box.Ping();
        PushEverything();

        _connected = true;
        _lastUptime = info.Uptime;
        _nextPing = now + PingInterval;
        _lastFailure = null;
        _logger.LogInformation("The box at {Host} is connected, firmware {Firmware}.", _status.Host, info.Firmware);
        Report(new BoxStatus(Connected: true, _status.Host, info.Firmware));
    }

    private void Serve(TimeSpan now)
    {
        // A blackout switched on the UI goes out first: going dark must not wait behind a
        // frame, and the ping below has to find the box in the state the desk asked for.
        SendBlackoutIfChanged();
        SendOutputIfChanged();

        if (now >= _nextPing)
        {
            _nextPing = now + PingInterval;
            CheckBox();
        }
    }

    private void CheckBox()
    {
        PingInfo info = _box.Ping();

        // An uptime that went backwards: the box started anew and forgot what it was told.
        bool restarted = info.Uptime < _lastUptime;
        _lastUptime = info.Uptime;
        if (restarted)
        {
            _logger.LogInformation("The box at {Host} restarted; it gets the desk's state again.", _status.Host);
            PushEverything();
            return;
        }

        // The link knows what it told the box. If the box says something else, its own
        // blackout button was pressed.
        if (info.Blackout != _boxBlackout)
            TakeBlackoutFromBox(info.Blackout);
    }

    // What a box needs after it was found, or after it restarted: it may hold anything, or nothing.
    private void PushEverything()
    {
        bool blackout = _desk.Blackout;

        // In a blackout the box has to be dark before it gets the look. The other way round,
        // the look would be on stage for the moment between the two requests.
        if (blackout)
            _box.Blackout(true);

        // The desk's master is already part of the output. The box's own master must not dim it once more.
        _box.Master(Desk.MaxLevel);
        SendOutput();

        // Without blackout it is the opposite: first the right look, then the light.
        if (!blackout)
            _box.Blackout(false);

        _boxBlackout = blackout;
    }

    private void SendBlackoutIfChanged()
    {
        bool blackout = _desk.Blackout;
        if (blackout == _boxBlackout)
            return;

        _box.Blackout(blackout);
        _boxBlackout = blackout;
    }

    private void SendOutputIfChanged()
    {
        if (_desk.OutputChanges != _sentOutput)
            SendOutput();
    }

    private void SendOutput()
    {
        // The count first, then the output: a change in between is sent once more than
        // needed, but never missed.
        long changes = _desk.OutputChanges;
        _box.SetLevels(_desk.GetOutput());
        _sentOutput = changes;
    }

    // The box's button has the last word: its state goes to the desk and to the UIs.
    private void TakeBlackoutFromBox(bool on)
    {
        _boxBlackout = on;
        _desk.SetBlackout(on);
        _ = _clients.BlackoutChanged(on);   // not awaited, see Report
    }

    private void Disconnect(TimeSpan now, Exception reason)
    {
        _nextPing = now + RetryInterval;
        LogFailure(reason);
        if (!_connected)
            return;

        _connected = false;
        Report(new BoxStatus(Connected: false, _status.Host, Firmware: null));
    }

    // A missing box fails every second, each time for the same reason. The log gets a reason
    // only when it is a new one.
    private void LogFailure(Exception reason)
    {
        if (reason.Message == _lastFailure)
            return;

        _lastFailure = reason.Message;

        // No answer, or no network to the box: everyday events that need no stack trace.
        // Anything else is a fault in this program, and there the trace helps.
        Exception? fault = reason is DmxBoxException or SocketException ? null : reason;
        _logger.LogWarning(fault, "The box at {Host} is not connected: {Reason}", _status.Host, reason.Message);
    }

    private void Report(BoxStatus status)
    {
        _status = status;

        // Not awaited: a browser that is slow to read its messages must not hold up the
        // lights. A UI whose connection fails is SignalR's business.
        _ = _clients.BoxChanged(status);
    }
}
