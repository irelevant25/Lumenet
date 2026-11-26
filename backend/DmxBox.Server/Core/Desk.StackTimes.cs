namespace DmxBox.Server.Core;

// The cues that come at moments of sounds (Step.At): while the stack is live it follows a sound that
// plays, cue after cue, and a jump in the sound takes it to the cue of the moment it jumped to. Learn
// stores those moments with GO, in a rehearsal with the music.
internal partial class Desk
{
    // How far back a sound has to go from one round to the next to count as sent back (a double click
    // on its wave, a loop that began again): a sound that plays goes on, and its position as the player
    // tells it may only waver a little.
    private const double JumpBack = 0.25;

    private bool _learning;   // GO stores the moment of the sound that plays into the cue it goes to
    private long _soundStarts;  // how many times a sound started: the order of the starts (SoundRun.StartOrder)
    private readonly Dictionary<int, double> _heard = [];   // where each sound that plays was in the round before

    /// <summary>True while GO learns the moments of sounds (<see cref="SetStackLearning"/>).</summary>
    public bool StackLearning
    {
        get
        {
            lock (_lock)
            {
                return _learning;
            }
        }
    }

    /// <summary>
    /// Learn: while on, every GO stores the moment of the sound that started last and plays into the
    /// cue it goes to (<see cref="Step.At"/>), and goes there also while a fade or a wait runs: for a
    /// rehearsal with the music, GO pressed on the beat. Off, GO only plays. The stack going off ends it.
    /// </summary>
    /// <param name="on">True to learn.</param>
    /// <returns>True if it changed.</returns>
    public bool SetStackLearning(bool on)
    {
        lock (_lock)
        {
            if (_learning == on)
                return false;
            _learning = on;
            return true;
        }
    }

    /// <summary>
    /// The moment of a sound a cue comes at (<see cref="Step.At"/>), or none: also while the stack is
    /// live, to set the moments in a rehearsal.
    /// </summary>
    /// <param name="id">The name of the cue.</param>
    /// <param name="at">The moment; null for a cue that waits for GO again.</param>
    /// <returns>True if it changed.</returns>
    /// <exception cref="DeskException">The cue is not in the stack, or the moment is out of range.</exception>
    public bool SetStackStepAt(int id, SoundTime? at)
    {
        SoundTime? kept = at?.Checked();
        lock (_lock)
        {
            StackEntry entry = EntryWithId(id);
            if (entry.Step.At == kept)
                return false;

            entry.Step = entry.Step with { At = kept };
            _projectChanges++;
            return true;
        }
    }

    // A step of a sequence: it has no moment of a sound, which only the cues of the stack come at, and
    // is not muted, which only a cue of the stack is.
    private static Step AsSequenceStep(Step step)
    {
        if (step.At is not null)
            throw new DeskException("A step of a sequence cannot come at a moment of a sound: only a cue of the stack can.");
        if (step.Muted)
            throw new DeskException("A step of a sequence cannot be muted: only a cue of the stack can.");
        return step;
    }

    // Call with the lock held, after the sounds are settled (their positions are those of now).
    private void FollowSoundTimes(double now)
    {
        // the sounds that went back since the round before; GO or Next ahead of the music sends none back
        (int Sound, double Position)? sentBack = null;
        for (int sound = 1; sound <= _soundRuns.Length; sound++)
        {
            if (PositionOf(sound) is not { } heard)
            {
                _heard.Remove(sound);
                continue;
            }

            if (_heard.TryGetValue(sound, out double before) && heard < before - JumpBack)
                sentBack = (sound, heard);
            _heard[sound] = heard;
        }

        if (!_live || _paused || _stack.Count == 0)
            return;

        // back: to the cue of the moment the sound is at now, unless the cue that is on belongs to it already
        StackEntry? current = _stack.Find(entry => entry.Id == _currentId);
        if (current is not null && sentBack is { } back
            && CueOfMoment(current, back.Sound, back.Position) is { } cue && !BelongsTo(current, cue))
        {
            GoTo(cue, now, rebuild: true, atOnce: true);
            return;
        }

        // on: the moment of the next cue has come; past more than one moment, straight to the last of them
        StackEntry? next = EntryBeside(+1);
        if (next?.Step.At is not { } at || PositionOf(at.Sound) is not { } position || position < at.Seconds || !Follows(next))
            return;

        StackEntry target = next;
        for (StackEntry? after = EntryAfter(target);
             after?.Step.At is { } later && later.Sound == at.Sound && position >= later.Seconds;
             after = EntryAfter(after))
        {
            target = after;
        }

        if (target == next)
        {
            GoOn(now);
        }
        else
        {
            EndMoves();
            GoTo(target, now, rebuild: true, atOnce: true);
        }
    }

    // Call with the lock held. Where the stack belongs while "sound" is at "position", seen from "from":
    // the latest cue at or before "from" whose moment of the sound "position" has reached (the cues after
    // it without a moment come by themselves or with GO); before the first of those moments, the cue they
    // follow. The moments of another sound end the search. Null when no cue at or before "from" comes at
    // a moment of the sound.
    private StackEntry? CueOfMoment(StackEntry from, int sound, double position)
    {
        StackEntry? firstPassed = null;
        for (int index = _stack.IndexOf(from); index >= 0; index--)
        {
            StackEntry entry = _stack[index];
            if (entry.Step.Disabled || entry.Step.At is not { } moment)
                continue;
            if (moment.Sound != sound)
                break;
            if (moment.Seconds <= position)
                return entry;
            firstPassed = entry;
        }

        return firstPassed is null ? null : EntryBefore(firstPassed);
    }

    // Call with the lock held. True if "current" is "cue" or one of the cues after it that come without a
    // moment of their own (by a wait or GO): the stack is where the sound belongs already.
    private bool BelongsTo(StackEntry current, StackEntry cue)
    {
        int from = _stack.IndexOf(cue), to = _stack.IndexOf(current);
        if (to < from)
            return false;
        for (int index = from + 1; index <= to; index++)
        {
            if (!_stack[index].Step.Disabled && _stack[index].Step.At is not null)
                return false;
        }

        return true;
    }

    // Call with the lock held. The cue before "entry" in the stack, the skipped ones passed over; none before the first.
    private StackEntry? EntryBefore(StackEntry entry)
    {
        for (int index = _stack.IndexOf(entry) - 1; index >= 0; index--)
        {
            if (!_stack[index].Step.Disabled)
                return _stack[index];
        }

        return null;
    }

    // Call with the lock held. The cue that follows "entry" in the stack, the skipped ones passed
    // over; none after the last (a sound does not take the stack round).
    private StackEntry? EntryAfter(StackEntry entry)
    {
        for (int index = _stack.IndexOf(entry) + 1; index < _stack.Count; index++)
        {
            if (!_stack[index].Step.Disabled)
                return _stack[index];
        }

        return null;
    }

    // Call with the lock held. Where a sound that plays is, in seconds of its file; null while it is silent.
    private double? PositionOf(int sound)
    {
        SoundRun run = _soundRuns[sound - 1];
        return run.Voice ? run.Position : null;
    }

    // Call with the lock held. Learn: the moment of the sound that started last and plays, into the cue
    // GO went to. Without a sound that plays the cue keeps what it had.
    private void LearnMoment(StackEntry cue)
    {
        int sound = 0;
        long latest = 0;
        for (int index = 0; index < _soundRuns.Length; index++)
        {
            SoundRun run = _soundRuns[index];
            if (run.Voice && run.StartOrder > latest)
            {
                latest = run.StartOrder;
                sound = index + 1;
            }
        }

        if (sound == 0)
            return;

        cue.Step = cue.Step with { At = new SoundTime(sound, _soundRuns[sound - 1].Position).Checked() };
        _projectChanges++;
    }
}
