namespace DmxBox.Server.Core;

// The stack played: Play, Pause, Next and Prev, its steps entered and left with their crossfades, and the
// sounds of its steps.
internal partial class Desk
{
    /// <summary>
    /// GO: goes on after a pause. Otherwise, if no step is on or the current one is up and
    /// waits, it goes to the next step; after the last step that is the first one. While a fade
    /// or a wait is running it does nothing. While Learn is on (<see cref="SetStackLearning"/>) it
    /// goes on also then, and the cue it goes to takes the moment of the sound that plays.
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
            if (_phase is not (Idle or Holding) && !_learning)
                return;

            StackEntry? next = EntryBeside(+1);
            GoOn(now);
            if (_learning && next is not null && next.Id == _currentId)
                LearnMoment(next);
        }
    }

    /// <summary>
    /// Pause: holds a running fade or wait where it is, and the sounds of the cues that are on (or on
    /// their way down) where they are, silent, until Play. Sounds of the hand or of a sequence play on.
    /// </summary>
    /// <exception cref="DeskException">The stack is not live.</exception>
    public void StackPause()
    {
        lock (_lock)
        {
            RequireLive();
            double now = Now;
            AdvanceStack(now);
            int[] sounds = StackSoundsThatPlay();
            if (!_paused && (_moves.Count > 0 || _phase is Fading or Waiting || sounds.Length > 0))
            {
                _paused = true;
                _pausedAt = now;
                foreach (int number in sounds)
                {
                    _player.Pause(number, true);
                    _soundRuns[number - 1].Paused = true;
                }
            }
        }
    }

    // Call with the lock held. The sounds that play whose faders the stack has: those of the cues that
    // are on, and those it moves (on their way down after their cue went).
    private int[] StackSoundsThatPlay()
    {
        IEnumerable<int> held = HoldersOf(_on).Keys.Concat(_moves.Keys).Where(fader => fader.Sound).Select(fader => fader.Number);
        return [.. held.Distinct().Where(number => _soundRuns[number - 1].Voice)];
    }

    // Call with the lock held. The sounds Pause held go on from where they are.
    private void ResumeSounds()
    {
        for (int index = 0; index < _soundRuns.Length; index++)
        {
            if (!_soundRuns[index].Paused)
                continue;

            _soundRuns[index].Paused = false;
            _player.Pause(index + 1, false);
        }
    }

    // Call with the lock held. The sounds the cues that are on hold play unheard while the cue that holds
    // them is muted (Step.Muted), and aloud again under one that is not. A sound no cue holds any more
    // keeps what it had on its way down.
    private void MarkUnheardSounds()
    {
        foreach ((Fader fader, Holder holder) in HoldersOf(_on))
        {
            if (fader.Sound)
                _soundRuns[fader.Number - 1].Unheard = holder.Entry.Step.Muted;
        }
    }

    /// <summary>
    /// Next: goes to the next step now, whatever the stack is doing, and at once: the fades that run
    /// end where they go, the new step comes without its up time and the steps that go leave without
    /// their down times, so the sounds of those steps stop at once. After the last step that is the
    /// first one. A wait of the new step runs as usual.
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
            EndMoves();
            GoOn(now, atOnce: true);
            SettleSounds(now);
        }
    }

    /// <summary>
    /// Previous: goes to the step before the current one now, and at once, as Next does: a step
    /// played by mistake is gone at once, its sound with it. The stack stays on that step until Play,
    /// also when the step has a wait (which would take the stack on by itself into the step it came
    /// from). Before the first step that is the last one. While no step is on it does nothing. The
    /// steps that are on then are those that are on at that step when the stack is gone through from
    /// the top (see OnAt).
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
            EndMoves();
            GoTo(EntryBeside(-1), now, rebuild: true, atOnce: true);
            if (_phase == Waiting)
                _phase = Holding;
            SettleSounds(now);
        }
    }

    // Call with the lock held. Every fader the stack moves stands where its move goes, at once, and a
    // crossfade that runs is over: Prev and Next take no time.
    private void EndMoves()
    {
        foreach ((Fader fader, FaderMove move) in _moves.ToArray())
            SetByStack(fader, (byte)move.To);
        _moves.Clear();
        _crossfade = null;
    }

    // Call with the lock held: no step is on, nothing is paused, and no fader is being moved. The sounds
    // Pause held go on, where their faders keep them up.
    private void StopStack()
    {
        _moves.Clear();
        _crossfade = null;
        _currentId = null;
        _on.Clear();
        _phase = Idle;
        _paused = false;
        ResumeSounds();
    }

    // Call with the lock held. Goes on to the step behind the current one: on top of what is on,
    // as the stack goes; but round from the last step to the first, the first starts afresh.
    // "atOnce": without the times of the steps (Next).
    private void GoOn(double now, bool atOnce = false)
    {
        StackEntry? next = EntryBeside(+1);
        if (next is not null)
            GoTo(next, now, rebuild: !Follows(next), atOnce);
    }

    // True if "next" stands behind the current step in the stack (or no step is on): then the
    // stack goes there from the current step, without going round.
    private bool Follows(StackEntry next)
    {
        return _stack.IndexOf(next) > _stack.FindIndex(entry => entry.Id == _currentId);
    }

    // Call with the lock held. Leaves the current step and goes to "target" now. Without a
    // target nothing happens. "rebuild": the steps that are on there are worked out from the
    // top of the stack (OnAt), not from what is on now. "atOnce": every fader goes where it is to
    // be at once, without the up and down times of the steps (Prev, Next).
    private void GoTo(StackEntry? target, double now, bool rebuild, bool atOnce = false)
    {
        if (target is null)
            return;

        Enter(target, now, rebuild, atOnce);
        AdvanceStack(now);   // a fade of no length is done at once
    }

    // Call with the lock held. The steps that are on once "target" is gone to while "before" are on:
    // a release takes its step out, a step that adds comes last (the latest counts most), any other
    // step is alone but for the steps of sounds that play on (Continue).
    private List<int> OnAfter(List<int> before, StackEntry target)
    {
        if (target.Step.IsRelease)
            return [.. before.Where(id => id != target.Step.Number)];
        if (target.Step.Add)
            return [.. before.Where(id => id != target.Id), target.Id];

        return [.. before.Where(id => id != target.Id && Continues(id)), target.Id];
    }

    // Call with the lock held. True for a step of the stack that plays a sound on (Continue).
    private bool Continues(int id)
    {
        return _stack.Find(entry => entry.Id == id) is { Step: { IsSound: true, Continue: true } };
    }

    // Call with the lock held. The steps that are on at "target" when the stack is gone through
    // from the top, each step in turn, without going round from the last step to the first: those
    // from the last step at or before it that takes everything down, and the steps of sounds that
    // play on from before that. Going back starts no sound but that of "target": the step of another
    // sound comes along only while its sound still plays.
    private List<int> OnAt(StackEntry target)
    {
        int at = _stack.IndexOf(target);
        List<int> on = [];
        for (int index = 0; index <= at; index++)
        {
            if (!_stack[index].Step.Disabled || index == at)
                on = OnAfter(on, _stack[index]);
        }

        return [.. on.Where(id => id == target.Id || !IsSoundStep(id) || SoundOfStepPlays(id))];
    }

    // Call with the lock held. True for a step of the stack that plays a sound.
    private bool IsSoundStep(int id)
    {
        return _stack.Find(entry => entry.Id == id) is { Step.IsSound: true };
    }

    // Call with the lock held. True while the sound of a step of the stack plays.
    private bool SoundOfStepPlays(int id)
    {
        return _stack.Find(entry => entry.Id == id) is { Step.IsSound: true } entry && _soundRuns[entry.Step.Number - 1].Voice;
    }

    // Call with the lock held. The steps of sounds that play on (Continue) whose sound has ended by
    // itself are over: they leave the steps that are on, and the fader of their sound goes to zero at
    // once, unless a step that is on still holds it. Switching the stack off ends them all.
    private void LeaveEndedSounds()
    {
        if (!_live || _on.Count == 0)
            return;

        int[] ended = [.. _on.Where(id => Continues(id) && SoundEnded(id))];
        if (ended.Length == 0)
            return;

        Dictionary<Fader, Holder> was = HoldersOf(_on);
        _on.RemoveAll(ended.Contains);
        Dictionary<Fader, Holder> will = HoldersOf(_on);
        foreach (Fader fader in was.Keys.Where(fader => fader.Sound && !will.ContainsKey(fader)))
        {
            _moves.Remove(fader);
            SetByStack(fader, 0);
        }

        MarkUnheardSounds();
    }

    // Call with the lock held. True when the sound of a step has played to its end and is over.
    private bool SoundEnded(int id)
    {
        return _stack.Find(entry => entry.Id == id) is { Step.IsSound: true } entry
            && _soundRuns[entry.Step.Number - 1] is { Ended: true, Voice: false };
    }

    // Call with the lock held, as the stack goes off: the sounds of the steps that play on stop, their
    // faders at zero. Every other fader stays where it is.
    private void StopContinuingSounds()
    {
        foreach (int id in _on.Where(Continues).ToArray())
        {
            Step step = _stack.Find(entry => entry.Id == id)!.Step;
            var fader = new Fader(Memory: false, step.Number, Sound: true);
            _moves.Remove(fader);
            SetByStack(fader, 0);
        }
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
    private void Enter(StackEntry target, double at, bool rebuild, bool atOnce = false)
    {
        List<int> before = [.. _on];
        List<int> after = rebuild ? OnAt(target) : OnAfter(before, target);
        Dictionary<Fader, Holder> was = HoldersOf(before), will = HoldersOf(after);
        Step step = target.Step;
        double comeIn = atOnce ? 0 : step.IsRelease ? step.Down : step.Up;

        // A step of a sound that still plays does not start it again; one that is over, it does,
        // also when its fader stays up (the steps of two sounds in a row at the same level).
        if (step.IsSound && _soundRuns[step.Number - 1] is { Ended: true, Voice: false } over)
            over.Ended = false;

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

            StartMove(fader, holder.Level, holder.Jump || atOnce ? 0 : seconds, at);
            if (!holder.Jump && !atOnce)
                moving.Add(fader);
        }

        var leaving = new List<Fader>();
        foreach ((Fader fader, Holder then) in was)
        {
            if (will.ContainsKey(fader) || then.Jump)
                continue;

            leaving.Add(fader);
            StartMove(fader, to: 0, atOnce ? 0 : step.IsRelease ? step.Down : then.Entry.Step.Down, at);
        }

        Fader[] coming = [.. moving.Where(fader => !was.TryGetValue(fader, out Holder then) || then.Jump)];
        if (atOnce)
            _crossfade = null;
        else
            StartCrossfade(leaving, coming, [.. moving], comeIn, at);
        _on.Clear();
        _on.AddRange(after);
        MarkUnheardSounds();
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
    // what its sequence gives at each moment, which is no look to fade from or to; nor does an effect
    // or a sound.
    private bool GivesLook(Fader fader)
    {
        if (fader.Effect || fader.Sound)
            return false;

        return fader.Memory ? _memories[fader.Number - 1] is not null : fader.Number - 1 < Channels;
    }

    // Raises "look" by what a fader gives at "level", highest wins.
    private void Contribute(byte[] look, Fader fader, byte level)
    {
        if (level == 0 || fader.Effect || fader.Sound)
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
    // channel, memory, effect or sound; for a fixture or a group, the channel faders of its lights,
    // an attribute to be set at once ("jump").
    private (Fader Fader, int Level, bool Jump)[] StackTargets(Step step)
    {
        if (step.IsRelease)
            return [];   // it takes a step down, and moves no fader of its own

        if (step.IsEffect)
            return [(new Fader(Memory: false, step.Number, Effect: true), step.Value, false)];

        if (step.IsSound)
            return [(new Fader(Memory: false, step.Number, Sound: true), step.Value, false)];

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
                // A next cue that comes at a moment of a sound waits for that (FollowSoundTimes).
                StackEntry? current = _stack.Find(entry => entry.Id == _currentId);
                bool goesOn = current is { Step.Wait: > 0 } && EntryBeside(+1) is { Step.At: null };
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
        else if (fader.Sound)
            MoveSoundFader(fader.Number, level);
        else if (fader.Memory)
            MoveMemoryFader(fader.Number, level);
        else
            _channels[fader.Number - 1] = level;

        // Only the newest level of a fader is of interest to the UIs.
        _moved.RemoveAll(moved => moved.Memory == fader.Memory && moved.Effect == fader.Effect && moved.Sound == fader.Sound && moved.Number == fader.Number);
        _moved.Add(new FaderMoved(fader.Memory, fader.Number, level) { Effect = fader.Effect, Sound = fader.Sound });
    }

    private byte LevelOf(Fader fader)
    {
        if (fader.Effect)
            return _runs[fader.Number - 1].Level;
        if (fader.Sound)
            return _soundRuns[fader.Number - 1].Level;

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
        ResumeSounds();
    }
}
