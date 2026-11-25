namespace DmxBox.Server;

// The part of the desk that is the stack: one list of steps per project. Switched live, it is
// gone through with Play, Pause, Next and Previous, round and round: after the last step comes
// the first. The stack moves the real faders: going to a step moves the fader of its channel or
// memory to the step's value over the step's "up" time, and leaving a step moves that fader to
// zero over its "down" time. A step of a fixture or a group moves the channel faders of its
// lights the same way (see FixtureLooks), and a step of an effect the effect's fader, which starts
// the effect when it leaves zero and stops it at zero (Desk.Effects.cs). Everything else follows from the faders, but for one
// thing: while the faders of two looks cross, their light fades from the one look to the other
// channel by channel, so that a light both give stays up (see _crossfade).
//
// More than one step can be on (see _on). A step that adds (Step.Add) comes up on top of what is
// on and takes nothing down. A step that does not takes down every step that is on, each in its
// own down time, unless it uses the same fader itself. A release (Step.Release) takes one step
// that is on down, in the release's own down time, and leaves the rest. Where two steps that are
// on use the same fader, the later one's level counts; when it goes, the earlier one's counts again.
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

    // While the stack goes from one step to the next, the light of the faders that take part in
    // it (those of the step that is left, on their way down, and those of the step that comes, on
    // their way up) goes from the one look to the other channel by channel, instead of the higher
    // of the two winning: a channel both steps light at the same level stays where it is, one
    // that rises follows the up time of the step that comes, one that falls the down time of the
    // step that is left. The faders themselves move as always. When the two looks share no
    // channel that comes out the same as the higher one winning, and no crossfade is kept.
    private Crossfade? _crossfade;

    // The steps that are on, in the order they came on: the current step and the steps it came
    // on top of. A release is never among them, not even while it is the current step.
    private readonly List<int> _on = [];

    private bool _live;               // the stack runs, and the desk is locked
    private int? _currentId;          // the step that is on or on its way up
    private string _phase = Idle;
    private double _phaseEnd;         // when the fade or the wait is over, in desk seconds
    private bool _paused;
    private double _pausedAt;

    /// <summary>
    /// True while something on the desk changes by itself: the stack moves a fader or counts
    /// down a wait, a sequence runs, or an effect.
    /// </summary>
    public bool Animating
    {
        get
        {
            lock (_lock)
            {
                bool stackMoves = !_paused && (_moves.Count > 0 || _crossfade is not null || _phase is Fading or Waiting);
                return stackMoves || SequencesMove(Now) || EffectsRun();
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
            bool effects = SettleEffects(now);
            if (_moved.Count > 0 || (_crossfade is not null && !_paused) || SequencesMove(now) || effects)
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
            byte[] levels = new byte[Channels];
            var stepsOn = new Dictionary<int, int>();
            var onSteps = new Dictionary<int, int[]>();
            AddSequences(levels, now, stepsOn, onSteps: onSteps);
            if (_roles.AttributeLights.Length > 0)
                PutSequenceAttributes(levels, now);

            double until = _phase is Fading or Waiting ? _phaseEnd - (_paused ? _pausedAt : now) : 0;
            var stack = new StackPlayback(
                _currentId, EntryBeside(+1)?.Id, _phase, _paused, Math.Round(Math.Max(0, until), 2), _live)
            {
                On = [.. _on],
            };
            return new Playback(Array.ConvertAll(levels, level => (int)level), stepsOn, stack)
            {
                SequenceOn = onSteps,
                Crossfade = CrossfadeForUi(),
                Effects = EffectPlaybacksNow(now),
                EffectLevels = EffectLevelsForUi(now),
            };
        }
    }

    // Call with the lock held. The crossfade as the UIs see it, so that they show what the desk
    // sends (GetOutput) and not the higher of the faders; null while none runs.
    private StackCrossfade? CrossfadeForUi()
    {
        if (_crossfade is not { } cross)
            return null;

        int[] levels = new int[Math.Min(Channels, cross.From.Length)];
        for (int channel = 0; channel < levels.Length; channel++)
            levels[channel] = cross.ValueAt(channel);

        Fader[] faders = [.. cross.Faders.Keys.OrderBy(fader => fader.Number)];
        return new StackCrossfade(
            [.. faders.Where(fader => fader.Channel).Select(fader => fader.Number)],
            [.. faders.Where(fader => fader.Memory).Select(fader => fader.Number)],
            levels);
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
                GoTo(_stack[0], now, rebuild: true);
        }
    }

    /// <summary>
    /// Puts a new step into the stack: at the end, with the next whole number, or right after
    /// another step, with a number that fits there (see <see cref="CueNumbers.Move"/>: after 2.0,
    /// with 3.0 behind it, the new step becomes 2.1).
    /// </summary>
    /// <param name="step">The step.</param>
    /// <param name="afterId">The step it goes behind; null for the end of the stack.</param>
    /// <returns>The name of the new step.</returns>
    /// <exception cref="DeskException">
    /// The step is out of range or releases a step the stack does not have, the step it goes
    /// behind is not in the stack, no number is free at that place, the stack is full, or it is live.
    /// </exception>
    public int AddStackStep(Step? step, int? afterId = null)
    {
        Step checkedStep = (step ?? throw new DeskException("A step is missing.")).Checked();

        lock (_lock)
        {
            RequireUnlocked();
            RequireStepFits(checkedStep);
            RequireReleaseFits(checkedStep, self: null);
            if (_stack.Count >= MaxStackSteps)
                throw new DeskException($"The stack can have at most {MaxStackSteps} steps.");

            List<(int Id, CueNumber Cue)> numbers = Numbers();
            int id = _lastStackId + 1;
            bool atEnd = afterId is null || (numbers.Count > 0 && numbers[^1].Id == afterId);
            if (!atEnd)
                EntryWithId(afterId!.Value);

            // In the middle the new step is numbered as if it had been put at the end and moved
            // there; nothing is changed before every number is known.
            CueNumber cue = atEnd ? CueNumbers.ForNew(numbers) : numbers[^1].Cue;
            Dictionary<int, CueNumber> changes = [];
            if (!atEnd)
            {
                numbers.Add((id, cue));
                changes = CueNumbers.Move(numbers, id, afterId);
            }

            _lastStackId = id;
            var entry = new StackEntry(id, changes.GetValueOrDefault(id, cue), checkedStep);
            _stack.Add(entry);
            Renumber(changes);
            _projectChanges++;
            return entry.Id;
        }
    }

    /// <summary>Changes what a step stands for, its value, its times or whether it adds. Its number stays.</summary>
    /// <param name="id">The name of the step.</param>
    /// <param name="step">The step as it is to be.</param>
    /// <exception cref="DeskException">
    /// The step is not in the stack, out of range or releases itself or a step the stack does not
    /// have, or the stack is live.
    /// </exception>
    public void UpdateStackStep(int id, Step? step)
    {
        Step checkedStep = (step ?? throw new DeskException("A step is missing.")).Checked();

        lock (_lock)
        {
            RequireUnlocked();
            RequireStepFits(checkedStep);
            StackEntry entry = EntryWithId(id);
            RequireReleaseFits(checkedStep, self: id);
            entry.Step = checkedStep;
            _projectChanges++;
        }
    }

    /// <summary>Takes a step out of the stack. A release of that step releases nothing from then on.</summary>
    /// <param name="id">The name of the step.</param>
    /// <exception cref="DeskException">The step is not in the stack, or the stack is live.</exception>
    public void DeleteStackStep(int id)
    {
        lock (_lock)
        {
            RequireUnlocked();
            _stack.Remove(EntryWithId(id));
            foreach (StackEntry release in _stack.Where(entry => entry.Step.IsRelease && entry.Step.Number == id))
                release.Step = release.Step with { Number = 0 };

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
                GoOn(now);
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
            GoOn(now);
        }
    }

    /// <summary>
    /// Previous: goes to the step before the current one now. Before the first step that is the
    /// last one. While no step is on it does nothing. The steps that are on then are those that
    /// are on at that step when the stack is gone through from the top (see OnAt).
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
            GoTo(EntryBeside(-1), now, rebuild: true);
        }
    }

    // Call with the lock held: no step is on, nothing is paused, and no fader is being moved.
    private void StopStack()
    {
        _moves.Clear();
        _crossfade = null;
        _currentId = null;
        _on.Clear();
        _phase = Idle;
        _paused = false;
    }

    // Call with the lock held. Puts in the steps of a project, with new names. A release names the
    // step it takes down by its number in the project, and here by that step's new name.
    private void ReplaceStack(ProjectStep[] steps)
    {
        _stack.Clear();
        ProjectStep[] ordered = [.. steps.OrderBy(step => step.Cue)];
        foreach (ProjectStep step in ordered)
            _stack.Add(new StackEntry(++_lastStackId, step.Cue, step.Step));

        Dictionary<CueNumber, int> names = _stack.ToDictionary(entry => entry.Cue, entry => entry.Id);
        for (int index = 0; index < ordered.Length; index++)
        {
            if (!ordered[index].Step.IsRelease)
                continue;

            int released = ordered[index].Releases is CueNumber cue && names.TryGetValue(cue, out int id) && id != _stack[index].Id ? id : 0;
            _stack[index].Step = ordered[index].Step with { Number = released };
        }

        StopStack();
    }

    // Call with the lock held. The number of the step a release takes down, for a project; null
    // for a release of none, and for every other step.
    private CueNumber? ReleasedCue(Step step)
    {
        return step.IsRelease ? _stack.Find(entry => entry.Id == step.Number)?.Cue : null;
    }

    // Call with the lock held: a release must name another step of the stack, or none (0).
    private void RequireReleaseFits(Step step, int? self)
    {
        if (!step.IsRelease || step.Number == 0)
            return;
        if (step.Number == self)
            throw new DeskException("A step cannot release itself. Choose another step of the stack.");
        if (!_stack.Any(entry => entry.Id == step.Number))
            throw new DeskException("The step to release is not in the stack any more.");
    }

    // Call with the lock held. Goes on to the step behind the current one: on top of what is on,
    // as the stack goes; but round from the last step to the first, the first starts afresh.
    private void GoOn(double now)
    {
        StackEntry? next = EntryBeside(+1);
        if (next is not null)
            GoTo(next, now, rebuild: !Follows(next));
    }

    // True if "next" stands behind the current step in the stack (or no step is on): then the
    // stack goes there from the current step, without going round.
    private bool Follows(StackEntry next)
    {
        return _stack.IndexOf(next) > _stack.FindIndex(entry => entry.Id == _currentId);
    }

    // Call with the lock held. Leaves the current step and goes to "target" now. Without a
    // target nothing happens. "rebuild": the steps that are on there are worked out from the
    // top of the stack (OnAt), not from what is on now.
    private void GoTo(StackEntry? target, double now, bool rebuild)
    {
        if (target is null)
            return;

        Enter(target, now, rebuild);
        AdvanceStack(now);   // a fade of no length is done at once
    }

    // The steps that are on once "target" is gone to while "before" are on: a release takes its
    // step out, a step that adds comes last (the latest counts most), any other step is alone.
    private static List<int> OnAfter(List<int> before, StackEntry target)
    {
        if (target.Step.IsRelease)
            return [.. before.Where(id => id != target.Step.Number)];
        if (target.Step.Add)
            return [.. before.Where(id => id != target.Id), target.Id];

        return [target.Id];
    }

    // Call with the lock held. The steps that are on at "target" when the stack is gone through
    // from the top: from the last step at or before it that takes everything down (or from the
    // first step), each step in turn, without going round from the last step to the first.
    private List<int> OnAt(StackEntry target)
    {
        int at = _stack.IndexOf(target);
        int from = at;
        while (from > 0 && (_stack[from].Step.Add || _stack[from].Step.IsRelease))
            from--;

        List<int> on = [];
        for (int index = from; index <= at; index++)
            on = OnAfter(on, _stack[index]);

        return on;
    }

    // Call with the lock held. Each fader the steps in "on" move, with the level it is to have and
    // the step it has that from: of two steps that use the same fader, the later one counts.
    private Dictionary<Fader, Holder> HoldersOf(List<int> on)
    {
        var holders = new Dictionary<Fader, Holder>();
        foreach (int id in on)
        {
            if (_stack.Find(entry => entry.Id == id) is not { } entry)
                continue;

            foreach ((Fader fader, int level, bool jump) in StackTargets(entry.Step))
                holders[fader] = new Holder(entry, level, jump);
        }

        return holders;
    }

    // Call with the lock held. The step "target" starts at the moment "at", and the steps that
    // are on change with it (OnAfter, or OnAt for "rebuild"). Every fader goes to where the steps
    // that are on then want it: those of "target" in its up time; those of a step that went off
    // to zero, or back to the level of an earlier step that uses them too, in the down time of
    // the step that went (a release: in its own down time); those of a step that comes back on
    // with "target" (Previous) in the up time of "target". A fader that keeps its step and level
    // is left alone. A fixture's attributes (a shutter, a color wheel) are set at once, and stay
    // when their step goes.
    private void Enter(StackEntry target, double at, bool rebuild)
    {
        List<int> before = [.. _on];
        List<int> after = rebuild ? OnAt(target) : OnAfter(before, target);
        Dictionary<Fader, Holder> was = HoldersOf(before), will = HoldersOf(after);
        Step step = target.Step;
        double comeIn = step.IsRelease ? step.Down : step.Up;

        var moving = new List<Fader>();
        foreach ((Fader fader, Holder holder) in will)
        {
            bool held = was.TryGetValue(fader, out Holder then);
            double seconds;
            if (holder.Entry == target)
                seconds = step.Up;
            else if (held && then.Entry == holder.Entry && then.Level == holder.Level)
                continue;
            else if (step.IsRelease)
                seconds = step.Down;
            else if (held && before.Contains(holder.Entry.Id))
                seconds = then.Entry.Step.Down;
            else
                seconds = comeIn;

            StartMove(fader, holder.Level, holder.Jump ? 0 : seconds, at);
            if (!holder.Jump)
                moving.Add(fader);
        }

        var leaving = new List<Fader>();
        foreach ((Fader fader, Holder then) in was)
        {
            if (will.ContainsKey(fader) || then.Jump)
                continue;

            leaving.Add(fader);
            StartMove(fader, to: 0, step.IsRelease ? step.Down : then.Entry.Step.Down, at);
        }

        Fader[] coming = [.. moving.Where(fader => !was.TryGetValue(fader, out Holder then) || then.Jump)];
        StartCrossfade(leaving, coming, [.. moving], comeIn, at);
        _on.Clear();
        _on.AddRange(after);
        _currentId = target.Id;
        _phase = Fading;
        _phaseEnd = at + comeIn;
    }

    // Call with the lock held, right after the faders of a new step started to move. "leaving":
    // the faders that go down to zero; "coming": those that come up from zero; "moving": all
    // faders that started to move. A fader that moves from one level to another needs no
    // crossfade, unless one is running already: then everything that took part in it goes on
    // taking part, from where its light is at this moment. A channel that rises does so in "up";
    // one that falls in the down time of the fader that holds it up highest.
    private void StartCrossfade(List<Fader> leaving, Fader[] coming, Fader[] moving, double up, double at)
    {
        Crossfade? running = _crossfade;
        var faders = new HashSet<Fader>(leaving.Concat(coming).Where(GivesLook));
        if (running is not null)
        {
            faders.UnionWith(running.Faders.Keys);
            faders.UnionWith(moving.Where(GivesLook));
        }
        else if (!ShareChannel(leaving, coming))
        {
            _crossfade = null;
            return;
        }

        double SecondsDown(Fader fader) => _moves.TryGetValue(fader, out FaderMove move) ? move.Seconds : 0;
        var cross = new Crossfade(at, up, leaving.Select(SecondsDown).DefaultIfEmpty(0).Max(), Channels);
        foreach (Fader fader in faders)
        {
            byte from = LevelOf(fader);
            cross.Faders[fader] = (from, _moves.TryGetValue(fader, out FaderMove move) ? (byte)move.To : from);
        }

        foreach ((Fader fader, (byte from, byte to)) in cross.Faders)
        {
            if (running is null || !running.Faders.ContainsKey(fader))
                Contribute(cross.From, fader, from);
            Contribute(cross.To, fader, to);
        }

        if (running is not null)
        {
            for (int channel = 0; channel < cross.From.Length && channel < running.From.Length; channel++)
                cross.From[channel] = Math.Max(cross.From[channel], running.ValueAt(channel));
        }

        // Steps that go can have down times of their own: a channel that falls takes that of the
        // fader holding it up highest (the longest, if two hold it as high).
        byte[] gives = new byte[Channels];
        var found = new bool[Channels];
        foreach (Fader fader in leaving.Where(cross.Faders.ContainsKey))
        {
            Array.Clear(gives);
            Contribute(gives, fader, cross.Faders[fader].From);
            double seconds = SecondsDown(fader);
            for (int channel = 0; channel < gives.Length && channel < cross.From.Length; channel++)
            {
                if (gives[channel] == 0 || gives[channel] < cross.From[channel])
                    continue;

                cross.DownOf[channel] = found[channel] ? Math.Max(cross.DownOf[channel], seconds) : seconds;
                found[channel] = true;
            }
        }

        _crossfade = cross;
    }

    // True if the faders that go down light a channel that the faders that come up light too:
    // only then does a crossfade give anything other than the higher one winning.
    private bool ShareChannel(List<Fader> leaving, Fader[] coming)
    {
        if (leaving.Count == 0 || coming.Length == 0)
            return false;

        byte[] going = new byte[Channels], arriving = new byte[Channels];
        foreach (Fader fader in leaving.Where(GivesLook))
            Contribute(going, fader, LevelOf(fader));
        foreach (Fader fader in coming.Where(GivesLook))
            Contribute(arriving, fader, _moves.TryGetValue(fader, out FaderMove move) ? (byte)move.To : LevelOf(fader));

        for (int channel = 0; channel < going.Length; channel++)
        {
            if (going[channel] > 0 && arriving[channel] > 0)
                return true;
        }

        return false;
    }

    // A channel fader, or the fader of a memory that holds a look. A memory with a sequence gives
    // what its sequence gives at each moment, which is no look to fade from or to; nor does an effect.
    private bool GivesLook(Fader fader)
    {
        if (fader.Effect)
            return false;

        return fader.Memory ? _memories[fader.Number - 1] is not null : fader.Number - 1 < Channels;
    }

    // Raises "look" by what a fader gives at "level", highest wins.
    private void Contribute(byte[] look, Fader fader, byte level)
    {
        if (level == 0 || fader.Effect)
            return;

        if (!fader.Memory)
        {
            if (fader.Number - 1 < look.Length)
                look[fader.Number - 1] = Math.Max(look[fader.Number - 1], level);
            return;
        }

        if (_memories[fader.Number - 1] is not { } stored)
            return;

        for (int channel = 0; channel < look.Length; channel++)
        {
            if (stored[channel] > 0)
                look[channel] = Math.Max(look[channel], Scale(stored[channel], level));
        }
    }

    // Call with the lock held. A fader moved by hand leaves the crossfade: from now on it gives
    // its light as it stands, and the rest of the crossfade goes on without it.
    private void LeaveCrossfade(Fader fader)
    {
        if (_crossfade is not { } cross || !cross.Faders.Remove(fader))
            return;

        if (cross.Faders.Count == 0)
        {
            _crossfade = null;
            return;
        }

        Array.Clear(cross.From);
        Array.Clear(cross.To);
        foreach ((Fader other, (byte from, byte to)) in cross.Faders)
        {
            Contribute(cross.From, other, from);
            Contribute(cross.To, other, to);
        }
    }

    // Call with the lock held. The faders a step of the stack moves, and to what: the fader of its
    // channel, memory or effect; for a fixture or a group, the channel faders of its lights, an
    // attribute to be set at once ("jump").
    private (Fader Fader, int Level, bool Jump)[] StackTargets(Step step)
    {
        if (step.IsRelease)
            return [];   // it takes a step down, and moves no fader of its own

        if (step.IsEffect)
            return [(new Fader(Memory: false, step.Number, Effect: true), step.Value, false)];

        if (!step.IsLights)
            return [(new Fader(step.IsMemory, step.Number), step.Value, false)];

        return [.. LookOfLights(step).Select(part => (new Fader(Memory: false, part.Channel + 1), (int)part.Value, part.Attribute))];
    }

    private void StartMove(Fader fader, int to, double seconds, double now)
    {
        _moves[fader] = new FaderMove(now, seconds, LevelOf(fader), to);

        // A channel fader the stack starts to move takes its channel, like one moved by hand.
        if (fader.Channel)
            ClaimChannel(fader.Number);
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
            Enter(next, due < MaxStepsAtOnce ? _phaseEnd : now, rebuild: !Follows(next));
        }
    }

    // Brings the faders the stack is moving to where they belong at "now", and the crossfade
    // with them. The crossfade ends when its times are over and none of its faders moves any more.
    private void MoveFaders(double now)
    {
        foreach ((Fader fader, FaderMove move) in _moves.ToArray())
        {
            double done = move.Seconds <= 0 ? 1 : Math.Clamp((now - move.Start) / move.Seconds, 0, 1);
            SetByStack(fader, (byte)Math.Round(move.From + (move.To - move.From) * done));
            if (done >= 1)
                _moves.Remove(fader);
        }

        if (_crossfade is not { } cross)
            return;

        cross.Elapsed = Math.Max(0, now - cross.Start);
        if (cross.Over && !cross.Faders.Keys.Any(_moves.ContainsKey))
            _crossfade = null;
    }

    private void SetByStack(Fader fader, byte level)
    {
        if (LevelOf(fader) == level)
            return;

        if (fader.Effect)
            MoveEffectFader(fader.Number, level, Now);
        else if (fader.Memory)
            MoveMemoryFader(fader.Number, level);
        else
            _channels[fader.Number - 1] = level;

        // Only the newest level of a fader is of interest to the UIs.
        _moved.RemoveAll(moved => moved.Memory == fader.Memory && moved.Effect == fader.Effect && moved.Number == fader.Number);
        _moved.Add(new FaderMoved(fader.Memory, fader.Number, level) { Effect = fader.Effect });
    }

    private byte LevelOf(Fader fader)
    {
        if (fader.Effect)
            return _runs[fader.Number - 1].Level;

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

        if (_crossfade is not null)
            _crossfade.Start += gap;

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

    // A fader on the desk: of a channel, of a memory, or of an effect ("Effect"; see Desk.Effects.cs).
    private readonly record struct Fader(bool Memory, int Number, bool Effect = false)
    {
        // True for the fader of a channel.
        public bool Channel => !Memory && !Effect;
    }

    // A fader on its way: from one level to another, over some seconds.
    private readonly record struct FaderMove(double Start, double Seconds, int From, int To);

    // A crossfade of the stack, see _crossfade: the faders that take part, each with the level it
    // starts from and the level it goes to, the light they give at the start and at the end, how
    // long a channel takes on its way up and on its way down, and how long the fade has run at
    // the last move of the faders.
    private sealed class Crossfade
    {
        public Crossfade(double start, double up, double down, int channels)
        {
            Start = start;
            Up = up;
            From = new byte[channels];
            To = new byte[channels];
            DownOf = new double[channels];
            Array.Fill(DownOf, down);
        }

        public Dictionary<Fader, (byte From, byte To)> Faders { get; } = [];

        public byte[] From { get; }

        public byte[] To { get; }

        // Seconds a channel that falls takes; that of the fader that held it up highest.
        public double[] DownOf { get; }

        public double Start { get; set; }

        // Seconds a channel that rises takes: the up time of the step that comes.
        public double Up { get; }

        public double Elapsed { get; set; }

        // True once every channel has arrived.
        public bool Over => Elapsed >= Up && DownOf.All(down => Elapsed >= down);

        // A channel that rises goes up with the faders that come, one that falls down with those that go.
        public byte ValueAt(int channel)
        {
            double from = From[channel], to = To[channel];
            if (to >= from)
            {
                double up = Up <= 0 ? 1 : Math.Clamp(Elapsed / Up, 0, 1);
                return (byte)Math.Round(from + (to - from) * up);
            }

            double down = DownOf[channel];
            double left = down <= 0 ? 0 : 1 - Math.Clamp(Elapsed / down, 0, 1);
            return (byte)Math.Round(to + (from - to) * left);
        }
    }

    // What a fader is to have while certain steps are on: its level, and the step it has that from.
    private readonly record struct Holder(StackEntry Entry, int Level, bool Jump);

    // A step of the stack with its name and its number.
    private sealed class StackEntry(int id, CueNumber cue, Step step)
    {
        public int Id { get; } = id;

        public CueNumber Cue { get; set; } = cue;

        public Step Step { get; set; } = step;

        public StackStep ForUi()
        {
            return new StackStep(Id, Cue.ToString(), Step.Target, Step.Number, Step.Value, Step.Up, Step.Wait, Step.Down)
            {
                Color = Step.Color,
                Add = Step.Add,
            };
        }
    }
}
