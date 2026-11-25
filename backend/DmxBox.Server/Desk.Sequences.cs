namespace DmxBox.Server;

// The part of the desk that deals with sequences: a memory can hold one in place of a look.
// A sequence runs while the fader of its memory is up, from the moment the fader leaves zero.
// It is a layer like a look: scaled by the fader, merged highest-wins, and it moves no fader.
internal sealed partial class Desk
{
    // The sequence stored in each memory. Null where the memory holds a look or nothing.
    private readonly Sequence?[] _sequences = new Sequence?[MemoryCount];

    // When the sequence of each memory started, in desk seconds, as if it had always run at the
    // speed it has now: then the time of the sequence is (now - start) x speed. A change of the
    // speed moves this, so that the sequence goes on from where it is.
    private readonly double[] _sequenceStarts = new double[MemoryCount];

    // The seed of each start, which chooses the orders of a sequence played at random: every
    // start plays other orders.
    private readonly int[] _sequenceSeeds = new int[MemoryCount];

    // The rate: a tempo for all sequences, in beats per minute. While RATE is lit every sequence
    // takes one step per beat (its own speed counts the steps of a beat); dark, every sequence
    // plays its own times. Like the master fader it belongs to the evening, not to the project.
    private double _bpm = DefaultBpm;
    private bool _rateOn;

    /// <summary>The slowest tempo of the rate, in beats per minute.</summary>
    public const double MinBpm = 30;

    /// <summary>The fastest tempo of the rate, in beats per minute.</summary>
    public const double MaxBpm = 300;

    /// <summary>The tempo of the rate on a new desk.</summary>
    public const double DefaultBpm = 120;

    /// <summary>
    /// Stores a sequence in a memory that is empty or holds a sequence already, played in turn
    /// and at its own speed. See <see cref="SetSequence(int, Sequence?)"/>.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="loop">True to start again after the last step, false to play once.</param>
    /// <param name="steps">The steps, at most 99. None at all is allowed: such a sequence gives nothing.</param>
    /// <exception cref="DeskException">The memory holds a look, a step is out of range, or the stack is live.</exception>
    public void SetSequence(int memory, bool loop, Step[]? steps)
    {
        SetSequence(memory, new Sequence(loop, steps!));
    }

    /// <summary>
    /// Stores a sequence in a memory that is empty or holds a sequence already: its steps, and
    /// how it is played. If the memory's fader is up, the sequence starts from its first step.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="sequence">The sequence: at most 99 steps (none at all is allowed: such a sequence gives nothing), and a speed from 0.1 to 10.</param>
    /// <exception cref="DeskException">
    /// The memory holds a look, a step or the speed is out of range, or the stack is live.
    /// </exception>
    public void SetSequence(int memory, Sequence? sequence)
    {
        RequireMemory(memory);
        if (sequence is null)
            throw new DeskException("The sequence is missing.");

        Step[] steps = sequence.Steps ?? [];
        if (steps.Length > Sequence.MaxSteps)
            throw TooManySteps();

        Sequence checkedSequence = sequence with
        {
            Steps = Array.ConvertAll(steps, step => (step ?? throw new DeskException("A step is missing.")).CheckedForSequence()),
            Speed = Sequence.CheckedSpeed(sequence.Speed),
        };

        lock (_lock)
        {
            RequireUnlocked();
            RequireNoLook(memory);
            Array.ForEach(checkedSequence.Steps, RequireStepFits);
            StoreSequence(memory, checkedSequence);
            CountLookChange();
        }
    }

    /// <summary>
    /// Changes how fast the sequence of a memory is played. A sequence that runs goes on from
    /// where it is, at the new speed: it does not start again.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="speed">From 0.1 (ten times slower) to 10 (ten times faster); 1 plays the times as they are.</param>
    /// <returns>The speed as the desk keeps it, in steps of 0.05.</returns>
    /// <exception cref="DeskException">The memory holds no sequence, the speed is out of range, or the stack is live.</exception>
    public double SetSequenceSpeed(int memory, double speed)
    {
        RequireMemory(memory);
        double checkedSpeed = Sequence.CheckedSpeed(speed);

        lock (_lock)
        {
            RequireUnlocked();
            Sequence sequence = _sequences[memory - 1] ?? throw new DeskException($"Memory {memory} holds no sequence.");
            if (sequence.Speed == checkedSpeed)
                return checkedSpeed;

            // The sequence stands where it stands; from here on its time runs at the new speed.
            double now = Now;
            double elapsed = SequenceTime(memory - 1, now);
            _sequences[memory - 1] = sequence with { Speed = checkedSpeed };
            _sequenceStarts[memory - 1] = now - elapsed / SpeedOf(memory - 1);
            _projectChanges++;
            _outputChanges++;
            return checkedSpeed;
        }
    }

    /// <summary>The rate: whether it is lit, and its tempo in beats per minute.</summary>
    public (bool On, double Bpm) Rate
    {
        get
        {
            lock (_lock)
            {
                return (_rateOn, _bpm);
            }
        }
    }

    /// <summary>
    /// Sets the rate, a tempo for all sequences: while it is lit, every sequence takes one step
    /// per beat, its steps keeping their proportions (on average a step and its wait last one
    /// beat), and a sequence's own speed counts the steps of a beat (2: two steps per beat). Dark,
    /// every sequence plays its own times. Effects run at their speed times the tempo over 120 BPM
    /// while it is lit. Every sequence and effect goes on from where it is. It is no part of the
    /// project, so it can be changed while the stack is live.
    /// </summary>
    /// <param name="on">True: the rate counts.</param>
    /// <param name="bpm">The tempo, from 30 to 300 beats per minute.</param>
    /// <exception cref="DeskException">The tempo is out of range.</exception>
    public void SetRate(bool on, double bpm)
    {
        // "Not a number" fails both comparisons, so it is refused as well. The numbers are written
        // with a point, as on the UI, whatever the language of the PC.
        if (!(bpm >= MinBpm && bpm <= MaxBpm))
            throw new DeskException(FormattableString.Invariant($"The tempo must be from {MinBpm} to {MaxBpm} beats per minute, not {bpm}."));

        lock (_lock)
        {
            if (on == _rateOn && bpm == _bpm)
                return;

            // Every sequence and every effect stands where it stands; from here on its time runs at the new speed.
            double now = Now;
            double[] elapsed = new double[MemoryCount];
            for (int index = 0; index < MemoryCount; index++)
                elapsed[index] = SequenceTime(index, now);

            RebaseEffects(now, () =>
            {
                _rateOn = on;
                _bpm = bpm;
            });
            for (int index = 0; index < MemoryCount; index++)
                _sequenceStarts[index] = now - elapsed[index] / SpeedOf(index);

            _outputChanges++;
        }
    }

    /// <summary>
    /// Puts one more step at the end of the sequence a memory holds. An empty memory gets a new
    /// sequence with this one step, played in a loop. This is what recording a sequence key by
    /// key does. If the memory's fader is up, the sequence starts from its first step.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="step">The step.</param>
    /// <exception cref="DeskException">
    /// The memory holds a look, the sequence is full, the step is out of range, or the stack is live.
    /// </exception>
    public void AddSequenceStep(int memory, Step? step)
    {
        RequireMemory(memory);
        Step checkedStep = (step ?? throw new DeskException("A step is missing.")).CheckedForSequence();

        lock (_lock)
        {
            RequireUnlocked();
            RequireNoLook(memory);
            RequireStepFits(checkedStep);

            Sequence old = _sequences[memory - 1] ?? new Sequence(Sequence.DefaultLoop, []);
            if (old.Steps.Length >= Sequence.MaxSteps)
                throw TooManySteps();

            StoreSequence(memory, old with { Steps = [.. old.Steps, checkedStep] });
            CountLookChange();
        }
    }

    /// <summary>Copies the sequences of all memories that hold one, for the UI.</summary>
    /// <returns>For each such memory, by its number (1-128): its sequence.</returns>
    public Dictionary<int, Sequence> GetSequences()
    {
        lock (_lock)
        {
            var sequences = new Dictionary<int, Sequence>();
            for (int index = 0; index < MemoryCount; index++)
            {
                if (_sequences[index] is { } sequence)
                    sequences[index + 1] = sequence with { Steps = Copy(sequence.Steps) };
            }

            return sequences;
        }
    }

    // Call with the lock held. A sequence that is put into a memory whose fader is up starts now.
    private void StoreSequence(int memory, Sequence sequence)
    {
        _sequences[memory - 1] = sequence;
        StartSequence(memory - 1);
    }

    // Call with the lock held. The sequence of the memory at "index" starts now, with new
    // random orders if it is played at random.
    private void StartSequence(int index)
    {
        _sequenceStarts[index] = Now;
        _sequenceSeeds[index] = System.Random.Shared.Next();
    }

    // Call with the lock held. The time of the sequence at "index": seconds since its start,
    // counted at the speed it plays at.
    private double SequenceTime(int index, double now)
    {
        return (now - _sequenceStarts[index]) * SpeedOf(index);
    }

    // Call with the lock held. The speed the sequence at "index" plays at: its own; while the rate
    // is lit, so fast that its average step lasts one beat of the tempo, times its own speed (the
    // steps it takes in a beat).
    private double SpeedOf(int index)
    {
        Sequence? sequence = _sequences[index];
        double own = sequence?.Speed ?? 1;
        return _rateOn && sequence is not null ? own * SequenceTimeline.AverageStep(sequence) * _bpm / 60 : own;
    }

    // Call with the lock held. A look is not turned into a sequence unasked: its owner empties
    // the memory first.
    private void RequireNoLook(int memory)
    {
        if (_memories[memory - 1] is not null)
            throw new DeskException($"Memory {memory} holds a look. Empty it first.");
    }

    private static DeskException TooManySteps()
    {
        return new DeskException($"A sequence can have at most {Sequence.MaxSteps} steps.");
    }

    // Call with the lock held. Every move of a memory fader goes through here, by hand or by
    // the stack: a fader that leaves zero starts the sequence of its memory, and takes the
    // attributes of the lights the memory holds (Desk.Attributes.cs).
    private void MoveMemoryFader(int memory, byte level)
    {
        if (_memoryLevels[memory - 1] == 0 && level > 0)
        {
            StartSequence(memory - 1);
            ClaimMemory(memory);
        }

        _memoryLevels[memory - 1] = level;
    }

    // Call with the lock held. Raises "levels" to what the running sequences give at "now".
    // "stepsOn", if given, receives the step each running sequence is on, counted from 1. The
    // memories in "leftOut" (held with SOLO DOWN) give nothing.
    private void AddSequences(byte[] levels, double now, Dictionary<int, int>? stepsOn, HashSet<Fader>? leftOut = null)
    {
        for (int index = 0; index < MemoryCount; index++)
        {
            if (_memoryLevels[index] > 0 && leftOut?.Contains(new Fader(true, index + 1)) != true)
                AddSequence(levels, index, now, _memoryLevels[index], stepsOn);
        }
    }

    // Call with the lock held. Raises "levels" to what the sequence of one memory gives at "now",
    // under a fader at "fader".
    //
    // Going from one step to the next is a crossfade without a dip: every channel moves from its
    // level in the step before to its level in the step that comes. A channel both steps hold at
    // the same level stays where it is; one that rises follows the up time of the step that comes,
    // one that falls the down time of the step that goes (to 0 if the next step does not hold it).
    // A step played earlier that is still on its way down (a down time longer than the steps
    // after it) holds its channels up the same way, from where the newer steps have them.
    private void AddSequence(byte[] levels, int index, double now, byte fader, Dictionary<int, int>? stepsOn)
    {
        if (_sequences[index] is not { Steps.Length: > 0 } sequence)
            return;

        double elapsed = SequenceTime(index, now);
        int seed = _sequenceSeeds[index];
        List<SequenceTimeline.Play> plays = SequenceTimeline.Recent(sequence, elapsed, seed);
        stepsOn?.Add(index + 1, plays[0].Step + 1);
        if (plays.Count == 1)
        {
            // The first step of a run: nothing before it, it comes up from dark.
            AddStep(levels, sequence.Steps[plays[0].Step], plays[0].Fraction, fader);
            return;
        }

        var looks = new Dictionary<int, byte[]>();
        byte[] LookOf(int step) => looks.TryGetValue(step, out byte[]? look) ? look : looks[step] = FullLook(sequence.Steps[step], levels.Length);

        byte[][] stepLooks = plays.Select(play => LookOf(play.Step)).ToArray();
        byte[] coming = stepLooks[0], before = stepLooks[1];
        double up = plays[0].Fraction;
        for (int channel = 0; channel < levels.Length; channel++)
        {
            // Rising: from the level of the step before, with the step that comes. Falling: the
            // steps that go hold it up, each with how far up it still is.
            double value = coming[channel] >= before[channel]
                ? before[channel] + (coming[channel] - before[channel]) * up
                : coming[channel];
            for (int at = 1; at < plays.Count; at++)
            {
                if (stepLooks[at][channel] > value)
                    value += (stepLooks[at][channel] - value) * plays[at].Fraction;
            }

            if (value > 0)
                levels[channel] = Math.Max(levels[channel], Scale((byte)Math.Round(value), fader));
        }
    }

    // What one step gives when it is all the way up, before the fader of its sequence: a channel
    // its value, a memory its look at the step's value, a fixture or a group the light of its parts.
    private byte[] FullLook(Step step, int channels)
    {
        byte[] look = new byte[channels];
        if (step.Value == 0)
            return look;

        if (step.IsLights)
        {
            foreach (FixtureLooks.Part part in LookOfLights(step))
            {
                if (!part.Attribute && part.Channel < channels)
                    look[part.Channel] = Math.Max(look[part.Channel], part.Value);
            }
        }
        else if (!step.IsMemory)
        {
            if (step.Number - 1 < channels)
                look[step.Number - 1] = (byte)step.Value;
        }
        else if (_memories[step.Number - 1] is { } stored)
        {
            for (int channel = 0; channel < channels; channel++)
                look[channel] = Scale(stored[channel], (byte)step.Value);
        }

        return look;
    }

    // What one step gives while it is "fraction" of the way up, under the fader of its sequence.
    // A fixture or a group gives its light here; its attributes (a shutter, a color wheel) come
    // from the step that is on, see Desk.Attributes.cs.
    private void AddStep(byte[] levels, Step step, double fraction, byte fader)
    {
        byte level = (byte)Math.Round(step.Value * fraction);
        if (level == 0)
            return;

        if (step.IsLights)
        {
            foreach (FixtureLooks.Part part in LookOfLights(step))
            {
                if (!part.Attribute && part.Channel < levels.Length)
                    levels[part.Channel] = Math.Max(levels[part.Channel], Scale((byte)Math.Round(part.Value * fraction), fader));
            }

            return;
        }

        if (!step.IsMemory)
        {
            levels[step.Number - 1] = Math.Max(levels[step.Number - 1], Scale(level, fader));
            return;
        }

        // A memory step plays that memory's look. An empty memory, or one that holds a
        // sequence itself, gives nothing: sequences are not played inside sequences.
        if (_memories[step.Number - 1] is not { } look)
            return;

        for (int channel = 0; channel < levels.Length; channel++)
        {
            if (look[channel] > 0)
                levels[channel] = Math.Max(levels[channel], Scale(Scale(look[channel], level), fader));
        }
    }

    // Call with the lock held. True while some sequence still changes what it gives.
    private bool SequencesMove(double now)
    {
        for (int index = 0; index < MemoryCount; index++)
        {
            if (_memoryLevels[index] > 0 && _sequences[index] is { } sequence
                && SequenceTimeline.StillMoves(sequence, SequenceTime(index, now)))
            {
                return true;
            }
        }

        return false;
    }
}
