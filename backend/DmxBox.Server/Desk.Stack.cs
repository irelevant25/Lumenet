namespace DmxBox.Server;

// The part of the desk that is the stack: one list of steps per project. Switched live, it is
// gone through with Play, Pause, Next and Previous, round and round: after the last step comes
// the first. The stack moves the real faders: going to a step moves the fader of its channel or
// memory to the step's value over the step's "up" time, and leaving a step moves that fader to
// zero over its "down" time. Everything else follows from the faders.
//
// The stack runs only while it is live. Then nothing a project holds can be changed, its own
// steps included (RequireUnlocked in Desk.cs). While it is not live no step is on, and Play,
// Pause, Next and Previous are refused: that is the time for changing the steps.
internal sealed partial class Desk
{
    /// <summary>The most steps the stack can have.</summary>
    public const int MaxStackSteps = 999;

    // How many steps the stack makes up for when it finds itself behind, see AdvanceStack.
    private const int MaxStepsAtOnce = 1000;

    private const string Idle = "idle";         // no step is on
    private const string Fading = "fading";     // the current step is on its way up
    private const string Waiting = "waiting";   // it is up; the stack goes on when its wait is over
    private const string Holding = "holding";   // it is up; the stack waits for Play

    // The steps in the order of their numbers.
    private readonly List<StackEntry> _stack = [];
    private int _lastStackId;

    // The faders the stack is moving right now.
    private readonly Dictionary<Fader, FaderMove> _moves = [];

    // The faders the stack has moved since the last Tick, for the UIs.
    private readonly List<FaderMoved> _moved = [];

    private bool _live;               // the stack runs, and the desk is locked
    private int? _currentId;          // the step that is on or on its way up
    private string _phase = Idle;
    private double _phaseEnd;         // when the fade or the wait is over, in desk seconds
    private bool _paused;
    private double _pausedAt;

    /// <summary>
    /// True while something on the desk changes by itself: the stack moves a fader or counts
    /// down a wait, or a sequence runs.
    /// </summary>
    public bool Animating
    {
        get
        {
            lock (_lock)
            {
                bool stackMoves = !_paused && (_moves.Count > 0 || _phase is Fading or Waiting);
                return stackMoves || SequencesMove(Now);
            }
        }
    }

    /// <summary>
    /// Lets time pass: moves the faders the stack is moving, and takes the stack on when a wait
    /// is over. To be called many times a second.
    /// </summary>
    /// <returns>The faders the stack has moved since the last call, the newest level of each last.</returns>
    public IReadOnlyList<FaderMoved> Tick()
    {
        lock (_lock)
        {
            double now = Now;
            AdvanceStack(now);
            if (_moved.Count > 0 || SequencesMove(now))
                _outputChanges++;

            FaderMoved[] moved = [.. _moved];
            _moved.Clear();
            return moved;
        }
    }

    /// <summary>Copies the stack for the UI.</summary>
    public StackState GetStack()
    {
        lock (_lock)
        {
            return new StackState(_stack.Select(entry => entry.ForUi()).ToArray());
        }
    }

    /// <summary>What changes with time, for the UI: what the sequences give, and where the stack stands.</summary>
    public Playback GetPlayback()
    {
        lock (_lock)
        {
            double now = Now;
            byte[] levels = new byte[ChannelCount];
            var stepsOn = new Dictionary<int, int>();
            AddSequences(levels, now, stepsOn);

            double until = _phase is Fading or Waiting ? _phaseEnd - (_paused ? _pausedAt : now) : 0;
            var stack = new StackPlayback(
                _currentId, EntryBeside(+1)?.Id, _phase, _paused, Math.Round(Math.Max(0, until), 2), _live);
            return new Playback(Array.ConvertAll(levels, level => (int)level), stepsOn, stack);
        }
    }

    /// <summary>
    /// Switches the stack live or off. Going live brings every channel fader and memory fader
    /// down to 0 and goes to the first step; from then on nothing that is stored can be
    /// changed. Switching it off stops the stack where it is: no step is on any more, and the
    /// faders stay as they are.
    /// </summary>
    /// <param name="on">True for live. Asking for what the stack is already changes nothing.</param>
    /// <exception cref="DeskException">The stack has no step to go live with.</exception>
    public void SetStackLive(bool on)
    {
        lock (_lock)
        {
            if (on == _live)
                return;

            if (on && _stack.Count == 0)
                throw new DeskException("The stack has no steps yet. Add a step first.");

            double now = Now;
            if (on)
                ZeroFaders();
            else
                AdvanceStack(now);   // the faders stop where they are at this moment, not where they were a tick ago

            StopStack();
            _live = on;
            _outputChanges++;
            if (on)
                GoTo(_stack[0], now);
        }
    }

    /// <summary>Puts a new step at the end of the stack, with the next whole number.</summary>
    /// <param name="step">The step.</param>
    /// <returns>The name of the new step.</returns>
    /// <exception cref="DeskException">The step is out of range, the stack is full, or it is live.</exception>
    public int AddStackStep(Step? step)
    {
        Step checkedStep = (step ?? throw new DeskException("A step is missing.")).Checked();

        lock (_lock)
        {
            RequireUnlocked();
            if (_stack.Count >= MaxStackSteps)
                throw new DeskException($"The stack can have at most {MaxStackSteps} steps.");

            var entry = new StackEntry(++_lastStackId, CueNumbers.ForNew(Numbers()), checkedStep);
            _stack.Add(entry);
            _projectChanges++;
            return entry.Id;
        }
    }

    /// <summary>Changes what a step stands for, its value or its times. Its number stays.</summary>
    /// <param name="id">The name of the step.</param>
    /// <param name="step">The step as it is to be.</param>
    /// <exception cref="DeskException">The step is not in the stack or out of range, or the stack is live.</exception>
    public void UpdateStackStep(int id, Step? step)
    {
        Step checkedStep = (step ?? throw new DeskException("A step is missing.")).Checked();

        lock (_lock)
        {
            RequireUnlocked();
            EntryWithId(id).Step = checkedStep;
            _projectChanges++;
        }
    }

    /// <summary>Takes a step out of the stack.</summary>
    /// <param name="id">The name of the step.</param>
    /// <exception cref="DeskException">The step is not in the stack, or the stack is live.</exception>
    public void DeleteStackStep(int id)
    {
        lock (_lock)
        {
            RequireUnlocked();
            _stack.Remove(EntryWithId(id));
            _projectChanges++;
        }
    }

    /// <summary>Puts a step right after another one. It gets a number that fits there, see <see cref="CueNumbers.Move"/>.</summary>
    /// <param name="id">The name of the step to move.</param>
    /// <param name="afterId">The step it goes behind; null for the very top.</param>
    /// <exception cref="DeskException">
    /// A step is not in the stack, no number is free at that place, or the stack is live.
    /// </exception>
    public void MoveStackStep(int id, int? afterId)
    {
        lock (_lock)
        {
            RequireUnlocked();
            Renumber(CueNumbers.Move(Numbers(), id, afterId));
        }
    }

    /// <summary>Gives a step the number its owner typed, see <see cref="CueNumbers.Renumber"/>.</summary>
    /// <param name="id">The name of the step.</param>
    /// <param name="cue">The number as text: "3", "3.0", "2.1".</param>
    /// <exception cref="DeskException">
    /// The step is not in the stack, the text is no step number, or the stack is live.
    /// </exception>
    public void RenumberStackStep(int id, string? cue)
    {
        CueNumber wanted = CueNumber.Parse(cue);

        lock (_lock)
        {
            RequireUnlocked();
            Renumber(CueNumbers.Renumber(Numbers(), id, wanted));
        }
    }

    /// <summary>
    /// Play: goes on after a pause. Otherwise, if no step is on or the current one is up and
    /// waits, it goes to the next step; after the last step that is the first one. While a fade
    /// or a wait is running it does nothing.
    /// </summary>
    /// <exception cref="DeskException">The stack is not live.</exception>
    public void StackPlay()
    {
        lock (_lock)
        {
            RequireLive();
            double now = Now;
            if (_paused)
            {
                Resume(now);
                return;
            }

            AdvanceStack(now);
            if (_phase is Idle or Holding)
                GoTo(EntryBeside(+1), now);
        }
    }

    /// <summary>Pause: holds a running fade or wait where it is, until Play.</summary>
    /// <exception cref="DeskException">The stack is not live.</exception>
    public void StackPause()
    {
        lock (_lock)
        {
            RequireLive();
            double now = Now;
            AdvanceStack(now);
            if (!_paused && (_moves.Count > 0 || _phase is Fading or Waiting))
            {
                _paused = true;
                _pausedAt = now;
            }
        }
    }

    /// <summary>
    /// Next: goes to the next step now, whatever the stack is doing. After the last step that
    /// is the first one.
    /// </summary>
    /// <exception cref="DeskException">The stack is not live.</exception>
    public void StackNext()
    {
        lock (_lock)
        {
            RequireLive();
            double now = Now;
            Resume(now);
            AdvanceStack(now);
            GoTo(EntryBeside(+1), now);
        }
    }

    /// <summary>
    /// Previous: goes to the step before the current one now. Before the first step that is the
    /// last one. While no step is on it does nothing.
    /// </summary>
    /// <exception cref="DeskException">The stack is not live.</exception>
    public void StackPrevious()
    {
        lock (_lock)
        {
            RequireLive();
            double now = Now;
            Resume(now);
            AdvanceStack(now);
            GoTo(EntryBeside(-1), now);
        }
    }

    // Call with the lock held: no step is on, nothing is paused, and no fader is being moved.
    private void StopStack()
    {
        _moves.Clear();
        _currentId = null;
        _phase = Idle;
        _paused = false;
    }

    // Call with the lock held. Puts in the steps of a project, with new names.
    private void ReplaceStack(ProjectStep[] steps)
    {
        _stack.Clear();
        foreach (ProjectStep step in steps.OrderBy(step => step.Cue))
            _stack.Add(new StackEntry(++_lastStackId, step.Cue, step.Step));

        StopStack();
    }

    // Call with the lock held. Leaves the current step and goes to "target" now. Without a
    // target nothing happens.
    private void GoTo(StackEntry? target, double now)
    {
        if (target is null)
            return;

        Enter(target, now);
        AdvanceStack(now);   // a fade of no length is done at once
    }

    // Call with the lock held. The step "target" starts at the moment "at": the fader of the
    // step that was on starts its way down, the fader of the new one its way up.
    private void Enter(StackEntry target, double at)
    {
        StackEntry? old = _stack.Find(entry => entry.Id == _currentId);
        if (old is not null && old.Fader != target.Fader)
            StartMove(old.Fader, to: 0, old.Step.Down, at);

        StartMove(target.Fader, target.Step.Value, target.Step.Up, at);
        _currentId = target.Id;
        _phase = Fading;
        _phaseEnd = at + target.Step.Up;
    }

    private void StartMove(Fader fader, int to, double seconds, double now)
    {
        _moves[fader] = new FaderMove(now, seconds, LevelOf(fader), to);
    }

    // Call with the lock held. Brings the faders the stack is moving to where they belong at
    // "now", and takes the stack on when a fade or a wait is over.
    private void AdvanceStack(double now)
    {
        if (_paused)
            return;

        // Times are sums of fractions of seconds, which a computer does not add exactly: a fade
        // of 4 s can seem to be 3.9999999 s old at its end. A millionth of a second settles that.
        now += 0.000001;

        // Nearly always this goes round once. After a long time without a call (a PC that
        // slept) many steps are due at once: they are gone through one after the other.
        for (int due = 0; ; due++)
        {
            MoveFaders(now);

            if (_phase == Fading && now >= _phaseEnd)
            {
                // Up. With a wait and another step to go to, the stack goes on by itself; else it stays.
                StackEntry? current = _stack.Find(entry => entry.Id == _currentId);
                bool goesOn = current is { Step.Wait: > 0 } && EntryBeside(+1) is not null;
                _phase = goesOn ? Waiting : Holding;
                if (goesOn)
                    _phaseEnd += current!.Step.Wait;
            }

            if (_phase != Waiting || now < _phaseEnd)
                return;

            if (EntryBeside(+1) is not { } next)
            {
                _phase = Holding;
                return;
            }

            // The wait is over. The next step starts at the moment the wait ended, not at the
            // moment this is noticed. But a stack that goes round by itself could be hours
            // behind: after this many steps the next one simply starts now.
            Enter(next, due < MaxStepsAtOnce ? _phaseEnd : now);
        }
    }

    // Brings the faders the stack is moving to where they belong at "now".
    private void MoveFaders(double now)
    {
        if (_moves.Count == 0)
            return;

        foreach ((Fader fader, FaderMove move) in _moves.ToArray())
        {
            double done = move.Seconds <= 0 ? 1 : Math.Clamp((now - move.Start) / move.Seconds, 0, 1);
            SetByStack(fader, (byte)Math.Round(move.From + (move.To - move.From) * done));
            if (done >= 1)
                _moves.Remove(fader);
        }
    }

    private void SetByStack(Fader fader, byte level)
    {
        if (LevelOf(fader) == level)
            return;

        if (fader.Memory)
            MoveMemoryFader(fader.Number, level);
        else
            _channels[fader.Number - 1] = level;

        // Only the newest level of a fader is of interest to the UIs.
        _moved.RemoveAll(moved => moved.Memory == fader.Memory && moved.Number == fader.Number);
        _moved.Add(new FaderMoved(fader.Memory, fader.Number, level));
    }

    private byte LevelOf(Fader fader)
    {
        return fader.Memory ? _memoryLevels[fader.Number - 1] : _channels[fader.Number - 1];
    }

    // After Pause: every running fade and the wait go on as if no time had passed.
    private void Resume(double now)
    {
        if (!_paused)
            return;

        double gap = now - _pausedAt;
        foreach ((Fader fader, FaderMove move) in _moves.ToArray())
            _moves[fader] = move with { Start = move.Start + gap };

        _phaseEnd += gap;
        _paused = false;
    }

    // The step one place behind (+1) or before (-1) the current one. The stack goes round: behind
    // the last step stands the first, before the first the last. Null if there is no other step
    // to go to. While no step is on, the step "behind" is the first one and there is none "before".
    private StackEntry? EntryBeside(int by)
    {
        int at = _stack.FindIndex(entry => entry.Id == _currentId);
        if (at < 0)
            return by > 0 ? _stack.FirstOrDefault() : null;

        StackEntry other = _stack[(at + by + _stack.Count) % _stack.Count];
        return other.Id == _currentId ? null : other;
    }

    private StackEntry EntryWithId(int id)
    {
        return _stack.Find(entry => entry.Id == id) ?? throw new DeskException("That step is not in the stack any more.");
    }

    private List<(int Id, CueNumber Cue)> Numbers()
    {
        return _stack.Select(entry => (entry.Id, entry.Cue)).ToList();
    }

    // Call with the lock held. Gives steps their new numbers and puts the list back in order.
    private void Renumber(Dictionary<int, CueNumber> changes)
    {
        if (changes.Count == 0)
            return;

        foreach (StackEntry entry in _stack)
        {
            if (changes.TryGetValue(entry.Id, out CueNumber cue))
                entry.Cue = cue;
        }

        _stack.Sort((left, right) => left.Cue.CompareTo(right.Cue));
        _projectChanges++;
    }

    // Call with the lock held, before the stack is gone through.
    private void RequireLive()
    {
        if (!_live)
            throw new DeskException("The stack is not live. Press Stack first.");
    }

    // A fader on the desk: of a channel or of a memory.
    private readonly record struct Fader(bool Memory, int Number);

    // A fader on its way: from one level to another, over some seconds.
    private readonly record struct FaderMove(double Start, double Seconds, int From, int To);

    // A step of the stack with its name and its number.
    private sealed class StackEntry(int id, CueNumber cue, Step step)
    {
        public int Id { get; } = id;

        public CueNumber Cue { get; set; } = cue;

        public Step Step { get; set; } = step;

        public Fader Fader => new(Step.IsMemory, Step.Number);

        public StackStep ForUi()
        {
            return new StackStep(Id, Cue.ToString(), Step.Target, Step.Number, Step.Value, Step.Up, Step.Wait, Step.Down);
        }
    }
}
