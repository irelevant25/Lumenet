using System.Diagnostics;
using System.Net.Sockets;

namespace DmxBox.Server.Boxes;

/// <summary>
/// Keeps one box showing what the desk says, for as long as the server runs. It looks for the
/// box until it answers, sends it every change of the desk's output, and watches for the two
/// things the box does by itself: its blackout button, and a restart. <see cref="BoxNetwork"/>
/// has a link for every box it looks for.
/// </summary>
/// <remarks>
/// A box has an output for each universe it sends: its channels, as its answer says them, divided
/// by 512. Each output gets the levels of the universe the wiring gives it (<see cref="BoxWiring"/>),
/// and nothing (zeros) where it plays none.
/// </remarks>
/// <remarks>
/// The desk's blackout is part of its output: it darkens the light and leaves the attributes of
/// the lights, such as the pan of a moving head, where they are, on every kind of output alike.
/// The box has a blackout of its own, which its button switches: that one makes everything dark,
/// the attributes too, and it is what the button does while no desk program runs. While the box
/// is connected its own blackout stays off: a press of the button, which the link sees at the
/// next ping (10 a second), switches the desk's blackout over, and once the box has the desk's
/// output with it, the link takes the box's own blackout off again.
/// </remarks>
/// <remarks>
/// The link has a thread of its own, because a request to a box that does not answer blocks
/// for most of a second. All requests come from that thread, one after the other.
/// </remarks>
internal sealed class BoxLink : IDisposable
{
    // 40 rounds a second. The box sends about 43 DMX frames a second, so more would be wasted.
    private static readonly TimeSpan RoundInterval = TimeSpan.FromMilliseconds(25);

    // How often a connected box is asked for its state: 10 times a second, so that its blackout
    // button works at once, and a box that restarted gets its look back within a moment.
    private static readonly TimeSpan PingInterval = TimeSpan.FromMilliseconds(100);

    // How often a missing box is looked for.
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(1);

    private readonly Desk _desk;
    private readonly IBox _box;
    private readonly IDeskClient _clients;
    private readonly ILogger<BoxLink> _logger;
    private readonly Func<int[]> _universesOf;
    private readonly Action? _changed;

    // Read by the network and the hub, replaced by the link's thread.
    private volatile BoxStatus _status;

    // 1 when the wiring changed: the next round sends the output again.
    private int _resend;

    // Everything below belongs to the link's thread alone.
    private bool _connected;
    private TimeSpan _nextPing;     // when to ping next: to find the box, or to check on it
    private TimeSpan _lastUptime;   // the box's uptime at the last ping
    private long _sentOutput;       // the desk's OutputChanges that belong to the last frame sent
    private string? _lastFailure;   // why the box was not reached the last time

    /// <summary>Prepares the link. Nothing is sent until it is run.</summary>
    /// <param name="desk">The desk whose output the box has to show.</param>
    /// <param name="box">The box. The link closes it when it is disposed.</param>
    /// <param name="clients">All connected UIs. They are told when the box's blackout button switched the desk's blackout.</param>
    /// <param name="host">The address of the box, as in <see cref="BoxPlan.Address"/>, for the UI and the log.</param>
    /// <param name="logger">Gets a line when the box comes or goes.</param>
    /// <param name="universesOf">
    /// The universe each output of the box plays now, output 1 first, 0 for none. Null for a box
    /// alone: output 1 plays U1, output 2 U2, as far as the desk has universes.
    /// </param>
    /// <param name="changed">Called on the link's thread when the box comes or goes.</param>
    public BoxLink(
        Desk desk,
        IBox box,
        IDeskClient clients,
        string host,
        ILogger<BoxLink> logger,
        Func<int[]>? universesOf = null,
        Action? changed = null)
    {
        _desk = desk;
        _box = box;
        _clients = clients;
        _logger = logger;
        _universesOf = universesOf ?? AloneOf;
        _changed = changed;
        _status = new BoxStatus(Connected: false, host, Firmware: null);
    }

    /// <summary>Whether the box is connected right now, with its outputs.</summary>
    public BoxStatus Status => _status;

    /// <summary>The wiring changed: the box gets its output again in the next round. Any thread may call this.</summary>
    public void ResendSoon()
    {
        Interlocked.Exchange(ref _resend, 1);
    }

    // A box alone plays the first universes, one per output.
    private int[] AloneOf()
    {
        int universes = _desk.Universes;
        return [.. Enumerable.Range(1, _status.Outputs).Select(universe => universe <= universes ? universe : 0)];
    }

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

    /// <summary>Closes the connection to the box. Stop the link's thread first.</summary>
    public void Dispose()
    {
        _box.Dispose();
    }

    /// <summary>
    /// The link's thread: 40 rounds a second until it is told to stop. <see cref="BoxNetwork"/>
    /// runs it on a thread of its own for every box.
    /// </summary>
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
                // Step handles every failure of the box itself. What gets here failed while it was
                // being handled: writing the log line, most likely, because the server is shutting
                // down and its log is closed. An exception that left this thread would end the
                // whole program, so it ends here.
                if (stopping.IsCancellationRequested)
                    return;
            }

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
        int outputs = OutputsOf(info);

        // Connected before the first frame goes out: the wiring gives a box its universes while
        // it is connected, and the first frame has to carry them. Should the frame fail, the box
        // is reported gone again at once.
        _connected = true;
        _lastUptime = info.Uptime;
        _nextPing = now + PingInterval;
        _lastFailure = null;
        _logger.LogInformation(
            "The box at {Host} is connected, firmware {Firmware}, outputs: {Outputs}.", _status.Host, info.Firmware, outputs);
        Report(new BoxStatus(Connected: true, _status.Host, info.Firmware) { Outputs = outputs });
        PushEverything();
    }

    // 512 channels for each output; a box that says fewer has one.
    private static int OutputsOf(PingInfo info)
    {
        return Math.Clamp(info.Channels / Desk.UniverseSize, 1, BoxPlan.MaxOutputs);
    }

    private void Serve(TimeSpan now)
    {
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

        // The link keeps the box's own blackout off. If it is on, the button was pressed.
        if (info.Blackout)
            TakeButtonPress();
    }

    // What a box needs after it was found, or after it restarted: it may hold anything, or nothing.
    private void PushEverything()
    {
        // The desk's master and blackout are already part of the output. The box's own master
        // must not dim it once more.
        _box.Master(Desk.MaxLevel);
        SendOutput();

        // First the right look, then the light: the box's own blackout goes off after the output.
        _box.Blackout(false);
    }

    // The box's button was pressed: it made the box dark by itself, the attributes too. The desk's
    // blackout switches over and the UIs hear of it. The box gets the output with the desk's
    // blackout in it, or without, and only then lets go of its own: dark lights stay dark, and a
    // moving head gets its pan and tilt back.
    private void TakeButtonPress()
    {
        bool on = _desk.ToggleBlackout();
        _ = _clients.BlackoutChanged(on);   // not awaited, see Report
        SendOutput();
        _box.Blackout(false);
    }

    private void SendOutputIfChanged()
    {
        if (_desk.OutputChanges != _sentOutput || Interlocked.Exchange(ref _resend, 0) == 1)
            SendOutput();
    }

    private void SendOutput()
    {
        // The count first, then the output: a change in between is sent once more than
        // needed, but never missed.
        long changes = _desk.OutputChanges;
        _box.SetLevels(FrameOf(_desk.GetOutput()));
        _sentOutput = changes;
    }

    // The box's levels: for each of its outputs, the 512 levels of the universe it plays; zeros
    // where it plays none, or a universe the project does not have.
    private byte[] FrameOf(byte[] output)
    {
        int outputs = _status.Outputs;
        int[] universes = _universesOf();
        byte[] frame = new byte[outputs * Desk.UniverseSize];
        for (int index = 0; index < outputs && index < universes.Length; index++)
        {
            int universe = universes[index];
            if (universe >= 1 && universe * Desk.UniverseSize <= output.Length)
                Array.Copy(output, (universe - 1) * Desk.UniverseSize, frame, index * Desk.UniverseSize, Desk.UniverseSize);
        }

        return frame;
    }

    private void Disconnect(TimeSpan now, Exception reason)
    {
        _nextPing = now + RetryInterval;
        LogFailure(reason);
        if (!_connected)
            return;

        _connected = false;
        Report(new BoxStatus(Connected: false, _status.Host, Firmware: null) { Outputs = _status.Outputs });
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

    // The network tells the UIs, with all boxes at once.
    private void Report(BoxStatus status)
    {
        _status = status;
        _changed?.Invoke();
    }
}
