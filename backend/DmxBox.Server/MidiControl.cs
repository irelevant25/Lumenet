using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Server;

/// <summary>The MIDI of the desk, as the pages on the PC show it in Settings.</summary>
/// <param name="Works">True on a system with MIDI devices for the desk (Windows, macOS); the simulators work everywhere.</param>
/// <param name="Devices">Every device there is, and every one the desk uses that is not there now.</param>
/// <param name="Mappings">What the controls do.</param>
/// <param name="Learning">The key or fader the next control that moves is to work; null while none.</param>
/// <param name="Presets">The ready-made mappings of controllers.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record MidiState(bool Works, MidiDeviceView[] Devices, MidiMapping[] Mappings, string? Learning, MidiPresetView[] Presets);

/// <summary>A MIDI device, as the list of devices shows it.</summary>
/// <param name="Name">Its name, as the PC gives it.</param>
/// <param name="Input">True when it has an input: its controls come in.</param>
/// <param name="Output">True when it has an output: its lights and motors can be set.</param>
/// <param name="Used">True when the desk uses it (opens it).</param>
/// <param name="State">"open", "missing" (used, but not plugged in), "failed" (it cannot be opened: <paramref name="Problem"/>) or "unused".</param>
/// <param name="Problem">Why it cannot be opened, for "failed".</param>
/// <param name="Simulated">True for a simulator of sim.cmd.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record MidiDeviceView(string Name, bool Input, bool Output, bool Used, string State, string? Problem, bool Simulated);

/// <summary>A ready-made mapping of a controller.</summary>
/// <param name="Id">Its name for the desk: "apc-mini-mk2".</param>
/// <param name="Name">Its name for people: "Akai APC mini mk2".</param>
/// <param name="Device">The name the controller usually has on a PC, to find it by.</param>
/// <param name="About">What it maps, in a sentence.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record MidiPresetView(string Id, string Name, string Device, string About);

/// <summary>One message that came in, and what became of it, for the list of incoming messages.</summary>
/// <param name="At">When, in milliseconds since 1970 (UTC).</param>
/// <param name="Device">The device it came from.</param>
/// <param name="Message">The message, "Note On 56, velocity 127, channel 1".</param>
/// <param name="Target">The key or fader it is mapped to; null for none.</param>
/// <param name="Result">What happened, see <see cref="MidiOutcome.Result"/>; also "learn-wait" (it cannot teach the target learned).</param>
/// <param name="Value">Where the control or the fader stands, 0-255.</param>
/// <param name="Desk">Where the desk's fader stands, 0-255, for "pickup".</param>
/// <param name="Reason">Why the desk refused, for "refused".</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record MidiMonitorLine(long At, string Device, string Message, string? Target, string Result, int? Value, int? Desk, string? Reason);

/// <summary>
/// Tells a page on the PC whether it is the one that presses the keys of the controllers, and which
/// keys' lamps the desk then needs from it.
/// </summary>
/// <param name="Controls">True for the page that presses the keys: the one on the PC used last.</param>
/// <param name="Keys">The keys the controllers have (their lights follow the lamps of these keys).</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record MidiRole(bool Controls, string[] Keys);

/// <summary>What the MIDI tells the pages; the desk program's goes through the hub, the tests' writes it down.</summary>
internal interface IMidiPagesOut
{
    /// <summary>The MIDI changed: every page on the PC gets it.</summary>
    void Changed(MidiState state);

    /// <summary>Messages came in: the pages that show the list get them.</summary>
    void Lines(IReadOnlyList<string> watchers, MidiMonitorLine[] lines);

    /// <summary>A key of a controller was pressed (true) or let go: the page presses the desk's key.</summary>
    void Key(string connection, string key, bool down);

    /// <summary>A page on the PC is now the one that presses the keys, or no more.</summary>
    void Role(string connection, MidiRole role);
}

/// <summary>
/// The MIDI controllers of the PC: the desk opens the devices it is told to use, does what their
/// controls are mapped to (<see cref="MidiMapper"/>), and lights their keys and moves their motors
/// (<see cref="MidiFeedback"/>). It looks for the devices every two seconds: a device pulled out and
/// plugged in again is found by its name and opened again.
/// <list type="bullet">
/// <item>The keys are pressed by the desk's page on the PC that was used last, as its keyboard
/// shortcuts press them: the same keys, the same locks, a Record function that waits, the page
/// that is shown. Phones and tablets over LAN never press them. Without a page on the PC the keys
/// do nothing; the faders still move.</item>
/// <item>Learn: the next control that moves is mapped to the key or fader chosen. A controller
/// (0-31) that the one 32 above it follows at once becomes a pair of 14 bits.</item>
/// </list>
/// The setup belongs to the PC (midi.json, <see cref="MidiSetupFile"/>); only the pages on the PC
/// change it.
/// </summary>
/// <remarks>Thread-safe: the devices' messages come on threads of their own.</remarks>
internal sealed class MidiControl : BackgroundService
{
    /// <summary>How many of the last messages a page that starts showing the list gets.</summary>
    public const int RecentLines = 40;

    // Seconds within which the fine controller of a pair must follow the coarse one just learned.
    private const double PairWithin = 0.3;

    // How often the devices are looked for, in seconds.
    private const double LookEvery = 2;

    private static readonly TimeSpan TickEvery = TimeSpan.FromMilliseconds(50);

    private readonly Lock _lock = new();
    private readonly Lock _looking = new();
    private readonly IMidiPorts _ports;
    private readonly MidiSetupFile _file;
    private readonly IMidiDesk _desk;
    private readonly IMidiPagesOut _pages;
    private readonly ILogger _log;
    private readonly Func<double> _now;
    private readonly MidiMapper _mapper;
    private readonly MidiFeedback _feedback = new();
    private readonly Dictionary<string, IMidiConnection> _open = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _problems = new(StringComparer.Ordinal);
    private readonly List<string> _pcPages = [];
    private readonly HashSet<string> _watchers = [];
    private readonly Queue<MidiMonitorLine> _recent = new();
    private readonly List<MidiMonitorLine> _unsent = [];
    private readonly Dictionary<string, MidiLamp> _lamps = new(StringComparer.Ordinal);
    private MidiSetup _setup;
    private MidiPortList _list = MidiPortList.None;
    private string? _learning;
    private (MidiMapping Mapping, double At)? _learned;
    private double _lookedAt = double.NegativeInfinity;

    /// <summary>Puts the MIDI together; nothing is opened until it starts (or <see cref="Look"/>).</summary>
    /// <param name="ports">The MIDI devices of the PC.</param>
    /// <param name="file">midi.json.</param>
    /// <param name="desk">The desk's faders.</param>
    /// <param name="pages">The pages.</param>
    /// <param name="log">The log.</param>
    /// <param name="now">Seconds on a clock that goes on; null for the real one (the tests pass theirs).</param>
    public MidiControl(IMidiPorts ports, MidiSetupFile file, IMidiDesk desk, IMidiPagesOut pages, ILogger log, Func<double>? now = null)
    {
        _ports = ports;
        _file = file;
        _desk = desk;
        _pages = pages;
        _log = log;
        Stopwatch clock = Stopwatch.StartNew();
        _now = now ?? (() => clock.Elapsed.TotalSeconds);
        _setup = file.Read();
        _mapper = new MidiMapper(desk, PressKey);
        _mapper.Use(_setup.Mappings);
    }

    /// <summary>The MIDI as the pages show it.</summary>
    public MidiState State
    {
        get
        {
            lock (_lock)
                return StateLocked();
        }
    }

    /// <summary>The setup of this PC, for Export.</summary>
    public MidiSetup Setup
    {
        get
        {
            lock (_lock)
                return _setup;
        }
    }

    /// <summary>Uses a device (opens it) or leaves it to other programs.</summary>
    /// <param name="device">Its name.</param>
    /// <param name="use">True to use it.</param>
    /// <exception cref="DeskException">The setup cannot be kept.</exception>
    public MidiState Use(string device, bool use)
    {
        string name = (device ?? "").Trim();
        if (name.Length == 0)
            throw new DeskException("Which MIDI device?");

        lock (_lock)
        {
            string[] devices = use
                ? [.. _setup.Devices.Append(name).Distinct(StringComparer.Ordinal)]
                : [.. _setup.Devices.Where(found => found != name)];
            Apply(_setup with { Devices = devices });
        }

        return AfterChange();
    }

    /// <summary>The mappings the page has now take the place of those before (a control taken away, an option changed).</summary>
    /// <param name="mappings">Every mapping.</param>
    /// <exception cref="DeskException">One of them is not possible, or the setup cannot be kept.</exception>
    public MidiState SetMappings(MidiMapping[] mappings)
    {
        lock (_lock)
            Apply(_setup with { Mappings = mappings ?? [] });

        return AfterChange();
    }

    /// <summary>A whole setup takes the place of this PC's (Import).</summary>
    /// <param name="setup">The setup.</param>
    /// <exception cref="DeskException">Something in it is not possible, or it cannot be kept.</exception>
    public MidiState SetSetup(MidiSetup setup)
    {
        lock (_lock)
            Apply(setup);

        return AfterChange();
    }

    /// <summary>The next control that moves is to work this key or fader; null stops learning.</summary>
    /// <param name="target">The key ("memory.5") or fader ("fader.master").</param>
    /// <exception cref="DeskException">It is no key or fader of the desk.</exception>
    public MidiState Learn(string? target)
    {
        if (target is not null && ControlTarget.Parse(target) is null)
            throw new DeskException($"\"{target}\" is no key or fader of the desk.");

        lock (_lock)
        {
            _learning = target;
            _learned = null;
            PublishLocked();
            return StateLocked();
        }
    }

    /// <summary>The ready-made mapping of a controller, for one device: its mappings before go, and the desk uses it.</summary>
    /// <param name="preset">The preset's <see cref="MidiPresetView.Id"/>.</param>
    /// <param name="device">The device, by its name.</param>
    /// <exception cref="DeskException">There is no such preset, or the setup cannot be kept.</exception>
    public MidiState UsePreset(string preset, string device)
    {
        MidiPreset found = MidiPresets.Find(preset) ?? throw new DeskException($"There is no ready-made MIDI mapping \"{preset}\".");
        string name = (device ?? "").Trim();
        if (name.Length == 0)
            throw new DeskException("For which MIDI device?");

        lock (_lock)
        {
            MidiMapping[] mappings =
            [
                .. _setup.Mappings.Where(mapping => mapping.Device != name),
                .. found.Mappings.Select(mapping => mapping with { Device = name }),
            ];
            Apply(new MidiSetup([.. _setup.Devices.Append(name).Distinct(StringComparer.Ordinal)], mappings));
        }

        return AfterChange();
    }

    /// <summary>A page on the PC was used (it got the focus, a click, a key): it presses the keys of the controllers from now on.</summary>
    /// <param name="connection">The page's connection.</param>
    public void Claim(string connection)
    {
        lock (_lock)
        {
            string? before = ControlPage;
            _pcPages.Remove(connection);
            _pcPages.Add(connection);
            if (before == connection)
                return;

            if (before is not null)
                _pages.Role(before, new MidiRole(false, []));
            _pages.Role(connection, RoleLocked());
        }
    }

    /// <summary>A page went away.</summary>
    /// <param name="connection">Its connection.</param>
    public void Leave(string connection)
    {
        lock (_lock)
        {
            _watchers.Remove(connection);
            bool controlled = ControlPage == connection;
            _pcPages.Remove(connection);
            if (controlled && ControlPage is { } next)
                _pages.Role(next, RoleLocked());
        }
    }

    /// <summary>The page that presses the keys tells how the desk's keys are lit; another page's word does not count.</summary>
    /// <param name="connection">The page's connection.</param>
    /// <param name="lamps">The lamps that changed, or all of them.</param>
    public void ReportLamps(string connection, MidiLamp[] lamps)
    {
        lock (_lock)
        {
            if (connection != ControlPage)
                return;
            foreach (MidiLamp lamp in lamps ?? [])
            {
                if (lamp?.Key is not null)
                    _lamps[lamp.Key] = lamp;
            }
        }
    }

    /// <summary>The page that presses the keys tells which pages of faders it shows: the slots follow them.</summary>
    /// <param name="connection">The page's connection.</param>
    /// <param name="pages">The pages.</param>
    public void ReportPages(string connection, MidiPages pages)
    {
        if (pages is null)
            return;

        lock (_lock)
        {
            if (connection != ControlPage)
                return;
            _desk.Pages = new MidiPages(
                Math.Clamp(pages.FirstChannel, 1, Desk.MaxChannels),
                Math.Clamp(pages.ChannelsPerPage, 1, Desk.UniverseSize),
                Math.Clamp(pages.FirstMemory, 1, Desk.MemoryCount),
                Math.Clamp(pages.MemoriesPerPage, 1, Desk.MemoryCount),
                Math.Clamp(pages.FirstEffect, 1, Effect.MaxEffects),
                Math.Clamp(pages.EffectsPerPage, 1, Effect.MaxEffects));
        }
    }

    /// <summary>A page shows the list of incoming messages, or no more: it gets the last ones at once.</summary>
    /// <param name="connection">The page's connection.</param>
    /// <param name="on">True while it shows the list.</param>
    public MidiMonitorLine[] Watch(string connection, bool on)
    {
        lock (_lock)
        {
            if (on)
                _watchers.Add(connection);
            else
                _watchers.Remove(connection);
            return on ? [.. _recent] : [];
        }
    }

    /// <summary>
    /// Looks for the devices: opens those the desk uses that came, closes those that went or failed.
    /// The desk does it every two seconds by itself.
    /// </summary>
    public void Look()
    {
        lock (_looking)
        {
            MidiPortList list = _ports.List();
            var closing = new List<IMidiConnection>();
            lock (_lock)
            {
                _lookedAt = _now();
                bool changed = !Same(_list, list);
                _list = list;
                var there = new HashSet<string>(list.Names, StringComparer.Ordinal);
                foreach (var (name, connection) in _open.ToArray())
                {
                    if (_setup.Devices.Contains(name) && there.Contains(name) && !connection.Failed)
                        continue;

                    closing.Add(connection);
                    _open.Remove(name);
                    _mapper.LetGo(name);
                    changed = true;
                }

                foreach (string name in _setup.Devices)
                {
                    if (_open.ContainsKey(name))
                        continue;
                    if (!there.Contains(name))
                    {
                        changed |= _problems.Remove(name);
                        continue;
                    }

                    try
                    {
                        _open[name] = _ports.Open(name, message => OnMessage(name, message));
                        _feedback.Forget(name);   // everything goes to it again
                        _problems.Remove(name);
                        changed = true;
                        _log.LogInformation("MIDI: {Device} is open.", name);
                    }
                    catch (MidiPortException e)
                    {
                        if (_problems.GetValueOrDefault(name) != e.Message)
                        {
                            _problems[name] = e.Message;
                            changed = true;
                            _log.LogWarning("MIDI: {Reason}", e.Message);
                        }
                    }
                }

                if (changed)
                    PublishLocked();
            }

            // Outside the lock: closing a device may wait for a message of it that waits for the lock.
            foreach (IMidiConnection connection in closing)
                connection.Dispose();
        }
    }

    /// <summary>Brings the controllers up to date and sends the incoming messages to the pages; the desk does it 20 times a second.</summary>
    public void Tick()
    {
        lock (_lock)
        {
            double now = _now();
            if (_open.Count > 0)
            {
                _desk.Refresh();
                bool blinkOn = now % 0.5 < 0.25;
                IEnumerable<MidiMapping> mappings = _setup.Mappings.Where(mapping => _open.ContainsKey(mapping.Device));
                foreach (var (device, message) in _feedback.Changes(mappings, _lamps, _mapper, _desk, now, blinkOn))
                {
                    if (_open.TryGetValue(device, out IMidiConnection? connection))
                        connection.Send(message);
                }
            }

            if (_learned is { } learned && now - learned.At > PairWithin)
                _learned = null;

            if (_unsent.Count > 0)
            {
                if (_watchers.Count > 0)
                    _pages.Lines([.. _watchers], [.. _unsent]);
                _unsent.Clear();
            }
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        IMidiConnection[] closing;
        lock (_lock)
        {
            closing = [.. _open.Values];
            _open.Clear();
        }

        foreach (IMidiConnection connection in closing)
            connection.Dispose();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Look();
        using var timer = new PeriodicTimer(TickEvery);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    Tick();
                    if (_now() - _lookedAt >= LookEvery)
                        Look();
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _log.LogError(e, "MIDI: a round of the controllers failed; the next one tries again.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The desk program ends.
        }
    }

    // The page that presses the keys: the one on the PC used last.
    private string? ControlPage => _pcPages.Count > 0 ? _pcPages[^1] : null;

    // A message of a device, on a thread of its own.
    private void OnMessage(string device, MidiMessage message)
    {
        lock (_lock)
        {
            if (!_open.ContainsKey(device))
                return;   // closed meanwhile

            double now = _now();
            _desk.Refresh();
            MidiOutcome outcome = _learning is not null
                ? LearnFrom(device, message, now)
                : PairOrHandle(device, message, now);
            _feedback.Touched(device, message);
            Remember(new MidiMonitorLine(
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), device, message.ToString(), outcome.Target, outcome.Result, outcome.Value, outcome.Desk, outcome.Reason));
        }
    }

    // The control just learned (a coarse controller) is followed by the one 32 above it: a pair of 14 bits.
    private MidiOutcome PairOrHandle(string device, MidiMessage message, double now)
    {
        if (_learned is { Mapping: { Message: MidiMapping.Controller } coarse, At: var at }
            && now - at <= PairWithin
            && coarse.Number < 32
            && message.Kind == MidiKind.ControlChange
            && message.Number == coarse.Number + 32
            && message.Channel == coarse.Channel
            && device == coarse.Device
            && ControlTarget.Parse(coarse.Target) is { IsKey: false })
        {
            _learned = null;
            MidiMapping pair = coarse with { Message = MidiMapping.Controller14 };
            TryApplyLearned([.. _setup.Mappings.Where(mapping => mapping.Control != coarse.Control && mapping.Control != pair.Control), pair]);
        }

        return _mapper.Handle(device, message, now);
    }

    // While learning: the control becomes the target's, if it can (a note for a key, a controller or pitch bend for a fader).
    private MidiOutcome LearnFrom(string device, MidiMessage message, double now)
    {
        string target = _learning!;
        bool key = ControlTarget.Parse(target) is { IsKey: true };
        string? kind = (key, message.Kind) switch
        {
            (true, MidiKind.NoteOn) => MidiMapping.Note,
            (true, MidiKind.ControlChange) when message.Value > 0 => MidiMapping.Controller,
            (true, MidiKind.ProgramChange) => MidiMapping.Program,
            (false, MidiKind.ControlChange) => MidiMapping.Controller,
            (false, MidiKind.PitchBend) => MidiMapping.PitchBend,
            _ => null,
        };
        if (kind is null)
            return new MidiOutcome(target, "learn-wait");

        var mapping = new MidiMapping
        {
            Device = device,
            Channel = message.Channel,
            Message = kind,
            Number = kind == MidiMapping.PitchBend ? 0 : message.Number,
            Target = target,
            Pickup = !key,
            Feedback = key,   // a key's light follows the desk's key; a fader has no motor unless the panel says so
        };

        // The control does this one thing from now on: what it did before goes.
        _learning = null;
        _learned = (mapping, now);
        TryApplyLearned([.. _setup.Mappings.Where(found => found.Control != mapping.Control), mapping]);
        return new MidiOutcome(target, "learned");
    }

    // A learned mapping is kept; if the file cannot be written it works all the same, until the desk ends.
    private void TryApplyLearned(MidiMapping[] mappings)
    {
        try
        {
            Apply(_setup with { Mappings = mappings });
        }
        catch (DeskException e)
        {
            _log.LogWarning("MIDI: {Reason}", e.Message);
            _setup = MidiSetupCheck.Checked(_setup with { Mappings = mappings });
            _mapper.Use(_setup.Mappings);
            PublishLocked();
        }
    }

    // Presses a key on the page that presses them; false when there is none on the PC.
    private bool PressKey(string key, bool down)
    {
        if (ControlPage is not { } page)
            return false;

        _pages.Key(page, key, down);
        return true;
    }

    // A changed setup: checked, kept in midi.json, and used. Call with the lock held.
    private void Apply(MidiSetup setup)
    {
        MidiSetup checkedSetup = MidiSetupCheck.Checked(setup);
        try
        {
            _file.Write(checkedSetup);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new DeskException($"The MIDI setup cannot be kept in {_file.Path}: {e.Message}");
        }

        _setup = checkedSetup;
        _mapper.Use(_setup.Mappings);
        if (ControlPage is { } page)
            _pages.Role(page, RoleLocked());
        PublishLocked();
    }

    // After a change of the setup: the devices are looked for at once (one used now is opened).
    private MidiState AfterChange()
    {
        Look();
        return State;
    }

    private void Remember(MidiMonitorLine line)
    {
        _recent.Enqueue(line);
        while (_recent.Count > RecentLines)
            _recent.Dequeue();

        if (_watchers.Count == 0)
            return;
        _unsent.Add(line);
        if (_unsent.Count > RecentLines * 5)
            _unsent.RemoveAt(0);   // a flood: the pages get the newest
    }

    private void PublishLocked()
    {
        _pages.Changed(StateLocked());
    }

    private MidiRole RoleLocked()
    {
        string[] keys =
        [
            .. _setup.Mappings
                .Where(mapping => _setup.Devices.Contains(mapping.Device) && ControlTarget.Parse(mapping.Target) is { IsKey: true })
                .Select(mapping => mapping.Target)
                .Distinct(StringComparer.Ordinal),
        ];
        return new MidiRole(true, keys);
    }

    private MidiState StateLocked()
    {
        var inputs = new HashSet<string>(_list.Inputs, StringComparer.Ordinal);
        var outputs = new HashSet<string>(_list.Outputs, StringComparer.Ordinal);
        // A device that only takes messages (the synthesizer of Windows) is no controller: it is not
        // listed, unless the desk was told to use it.
        MidiDeviceView[] devices =
        [
            .. _list.Inputs.Concat(_setup.Devices).Distinct(StringComparer.Ordinal).Select(name =>
            {
                bool used = _setup.Devices.Contains(name);
                bool there = inputs.Contains(name) || outputs.Contains(name);
                string state = !used ? "unused" : _open.ContainsKey(name) ? "open" : there ? "failed" : "missing";
                return new MidiDeviceView(
                    name, inputs.Contains(name), outputs.Contains(name), used, state, _problems.GetValueOrDefault(name), name.EndsWith(" (simulator)", StringComparison.Ordinal));
            }),
        ];
        return new MidiState(DryWetMidiPorts.Works, devices, _setup.Mappings, _learning, MidiPresets.Views);
    }

    private static bool Same(MidiPortList a, MidiPortList b)
    {
        return a.Inputs.SequenceEqual(b.Inputs, StringComparer.Ordinal) && a.Outputs.SequenceEqual(b.Outputs, StringComparer.Ordinal);
    }
}

/// <summary>
/// What the MIDI tells the pages, through the hub. A page gets its messages in the order they were
/// sent: a key's letting go never overtakes its press.
/// </summary>
internal sealed class HubMidiPages : IMidiPagesOut
{
    private readonly IHubContext<DeskHub, IDeskClient> _hub;
    private readonly ILogger _log;
    private readonly ConcurrentDictionary<string, Task> _queues = new(StringComparer.Ordinal);

    /// <summary>Talks to the pages through the hub.</summary>
    /// <param name="hub">The hub.</param>
    /// <param name="log">Where a message that could not go out is written.</param>
    public HubMidiPages(IHubContext<DeskHub, IDeskClient> hub, ILogger log)
    {
        _hub = hub;
        _log = log;
    }

    /// <inheritdoc />
    public void Changed(MidiState state)
    {
        Queue("pc", () => _hub.Clients.Group(LanAccess.PcGroup).MidiChanged(state));
    }

    /// <inheritdoc />
    public void Lines(IReadOnlyList<string> watchers, MidiMonitorLine[] lines)
    {
        Queue("lines", () => _hub.Clients.Clients(watchers).MidiMessages(lines));
    }

    /// <inheritdoc />
    public void Key(string connection, string key, bool down)
    {
        Queue(connection, () => _hub.Clients.Client(connection).MidiKey(key, down));
    }

    /// <inheritdoc />
    public void Role(string connection, MidiRole role)
    {
        Queue(connection, () => _hub.Clients.Client(connection).MidiRole(role));
    }

    // Each queue sends one message after the other; one that fails is written down and the next goes.
    private void Queue(string queue, Func<Task> send)
    {
        _queues.AddOrUpdate(queue, _ => Send(send), (_, tail) => tail.ContinueWith(_ => Send(send), TaskScheduler.Default).Unwrap());
    }

    private async Task Send(Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _log.LogDebug(e, "MIDI: a page could not be told.");
        }
    }
}
