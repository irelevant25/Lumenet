namespace DmxBox.Server.Core;

// The part of the desk that is the stack: one list of steps per project. Switched live, it is
// gone through with Play, Pause, Next and Previous, round and round: after the last step comes
// the first. The stack moves the real faders: going to a step moves the fader of its channel or
// memory to the step's value over the step's "up" time, and leaving a step moves that fader to
// zero over its "down" time. A step of a fixture or a group moves the channel faders of its
// lights the same way (see FixtureLooks), a step of an effect the effect's fader, which starts
// the effect when it leaves zero and stops it at zero (Desk.Effects.cs), and a step of a sound the
// sound's fader, likewise (Desk.Sounds.cs). Everything else follows from the faders, but for one
// thing: while the faders of two looks cross, their light fades from the one look to the other
// channel by channel, so that a light both give stays up (see _crossfade).
//
// More than one step can be on (see _on). A step that adds (Step.Add) comes up on top of what is
// on and takes nothing down. A step that does not takes down every step that is on, each in its
// own down time, unless it uses the same fader itself, and but for a step of a sound that plays on
// (Step.Continue): that one stays on until its sound ends by itself or a release names it. A
// release (Step.Release) takes one step that is on down, in the release's own down time, and leaves
// the rest. Where two steps that are on use the same fader, the later one's level counts; when it
// goes, the earlier one's counts again.
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

    // The phases of the stack, as StackPlayback.State tells the pages (internal: the page's contract
    // takes them from here).
    internal const string Idle = "idle";         // no step is on
    internal const string Fading = "fading";     // the current step is on its way up
    internal const string Waiting = "waiting";   // it is up; the stack goes on when its wait is over
    internal const string Holding = "holding";   // it is up; the stack waits for Play

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
                return stackMoves || SequencesMove(Now) || EffectsRun() || SoundsPlay();
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
            SettleSounds(now);
            FollowSoundTimes(now);
            LeaveEndedSounds();
            if (_moved.Any(moved => !moved.Sound) || (_crossfade is not null && !_paused) || SequencesMove(now) || effects)
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
                Learning = _learning,
            };
            return new Playback(Array.ConvertAll(levels, level => (int)level), stepsOn, stack)
            {
                SequenceOn = onSteps,
                Crossfade = CrossfadeForUi(),
                Effects = EffectPlaybacksNow(now),
                EffectLevels = EffectLevelsForUi(now),
                Sounds = SoundPlaybacksNow(),
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
                throw new DeskException("The stack has no cues yet. Add a cue first.");
            if (on && _stack.All(entry => entry.Step.Disabled))
                throw new DeskException("Every cue of the stack is skipped (Disabled). Take Disabled off a cue first.");

            double now = Now;
            if (on)
            {
                ZeroFaders();
            }
            else
            {
                AdvanceStack(now);   // the faders stop where they are at this moment, not where they were a tick ago
                StopContinuingSounds();
            }

            StopStack();
            _live = on;
            _learning &= on;
            _outputChanges++;
            if (on)
                GoTo(_stack.First(entry => !entry.Step.Disabled), now, rebuild: true);
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
        Step checkedStep = (step ?? throw new DeskException("A cue is missing.")).Checked();

        lock (_lock)
        {
            RequireUnlocked();
            RequireStepFits(checkedStep);
            RequireReleaseFits(checkedStep, self: null);
            if (_stack.Count >= MaxStackSteps)
                throw new DeskException($"The stack can have at most {MaxStackSteps} cues.");

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
        Step checkedStep = (step ?? throw new DeskException("A cue is missing.")).Checked();

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

    /// <summary>
    /// Skips a step of the stack, or takes it back (Disabled): the stack passes over a skipped step,
    /// which stays in the list. Unlike every other change of a step this works while the stack is
    /// live too, as a way out when something goes wrong; the step that is on just then cannot be
    /// skipped. A skipped step that is on stays on until a step takes it down.
    /// </summary>
    /// <param name="id">The name of the step.</param>
    /// <param name="disabled">True to skip it.</param>
    /// <returns>True when the step changed.</returns>
    /// <exception cref="DeskException">The step is not in the stack, or it is the step that is on.</exception>
    public bool SetStackStepDisabled(int id, bool disabled)
    {
        lock (_lock)
        {
            StackEntry entry = EntryWithId(id);
            if (entry.Step.Disabled == disabled)
                return false;
            if (disabled && _live && entry.Id == _currentId)
                throw new DeskException($"Cue {entry.Cue} is the cue that is on: it cannot be skipped now.");

            entry.Step = entry.Step with { Disabled = disabled };
            _projectChanges++;
            return true;
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
            throw new DeskException("A cue cannot release itself. Choose another cue of the stack.");
        if (!_stack.Any(entry => entry.Id == step.Number))
            throw new DeskException("The cue to release is not in the stack any more.");
    }

    // The step one place behind (+1) or before (-1) the current one, passing over the steps that are
    // skipped (Disabled). The stack goes round: behind the last step stands the first, before the
    // first the last. Null if there is no other step to go to. While no step is on, the step
    // "behind" is the first one that is not skipped, and there is none "before".
    private StackEntry? EntryBeside(int by)
    {
        int at = _stack.FindIndex(entry => entry.Id == _currentId);
        if (at < 0)
            return by > 0 ? _stack.FirstOrDefault(entry => !entry.Step.Disabled) : null;

        for (int step = 1; step < _stack.Count; step++)
        {
            StackEntry other = _stack[(((at + by * step) % _stack.Count) + _stack.Count) % _stack.Count];
            if (!other.Step.Disabled)
                return other.Id == _currentId ? null : other;
        }

        return null;
    }

    private StackEntry EntryWithId(int id)
    {
        return _stack.Find(entry => entry.Id == id) ?? throw new DeskException("That cue is not in the stack any more.");
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
}
