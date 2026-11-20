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

    // The rate: a speed master for all sequences. While it is on, every sequence plays at its own
    // speed times the rate. Like the master fader it belongs to the evening, not to the project.
    private double _rate = 1;
    private bool _rateOn;

    /// <summary>The slowest rate: every sequence ten times slower.</summary>
    public const double MinRate = 0.1;

    /// <summary>The fastest rate: every sequence ten times faster.</summary>
    public const double MaxRate = 10;

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
            Steps = Array.ConvertAll(steps, step => (step ?? throw new DeskException("A step is missing.")).Checked()),
            Speed = Sequence.CheckedSpeed(sequence.Speed),
        };

        lock (_lock)
        {
            RequireUnlocked();
            RequireNoLook(memory);
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

    /// <summary>The rate, a speed master for all sequences, and whether it is on.</summary>
    public (bool On, double Rate) Rate
    {
        get
        {
            lock (_lock)
            {
                return (_rateOn, _rate);
            }
        }
    }

    /// <summary>
    /// Sets the rate, a speed master for all sequences: while it is on, every sequence plays at
    /// its own speed times the rate; off, at its own speed. Every sequence goes on from where it
    /// is. It is no part of the project, so it can be changed while the stack is live.
    /// </summary>
    /// <param name="on">True: the rate counts.</param>
    /// <param name="rate">From 0.1 (ten times slower) to 10 (ten times faster); 1 changes nothing.</param>
    /// <exception cref="DeskException">The rate is out of range.</exception>
    public void SetRate(bool on, double rate)
    {
        // "Not a number" fails both comparisons, so it is refused as well. The numbers are written
        // with a point, as on the UI, whatever the language of the PC.
        if (!(rate >= MinRate && rate <= MaxRate))
            throw new DeskException(FormattableString.Invariant($"The rate must be from {MinRate} to {MaxRate}, not {rate}."));

        lock (_lock)
        {
            if (on == _rateOn && rate == _rate)
                return;

            // Every sequence stands where it stands; from here on its time runs at the new speed.
            double now = Now;
            double[] elapsed = new double[MemoryCount];
            for (int index = 0; index < MemoryCount; index++)
                elapsed[index] = SequenceTime(index, now);

            _rateOn = on;
            _rate = rate;
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
        Step checkedStep = (step ?? throw new DeskException("A step is missing.")).Checked();

        lock (_lock)
        {
            RequireUnlocked();
            RequireNoLook(memory);

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

    // Call with the lock held. The speed the sequence at "index" plays at: its own, times the
    // rate while the rate is on.
    private double SpeedOf(int index)
    {
        return (_sequences[index]?.Speed ?? 1) * (_rateOn ? _rate : 1);
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
    // the stack: a fader that leaves zero starts the sequence of its memory.
    private void MoveMemoryFader(int memory, byte level)
    {
        if (_memoryLevels[memory - 1] == 0 && level > 0)
            StartSequence(memory - 1);

        _memoryLevels[memory - 1] = level;
    }

    // Call with the lock held. Raises "levels" to what the running sequences give at "now".
    // "stepsOn", if given, receives the step each running sequence is on, counted from 1.
    private void AddSequences(byte[] levels, double now, Dictionary<int, int>? stepsOn)
    {
        for (int index = 0; index < MemoryCount; index++)
        {
            byte fader = _memoryLevels[index];
            if (fader == 0 || _sequences[index] is not { Steps.Length: > 0 } sequence)
                continue;

            double elapsed = SequenceTime(index, now);
            int seed = _sequenceSeeds[index];
            double[] fractions = SequenceTimeline.Fractions(sequence, elapsed, seed);
            for (int step = 0; step < fractions.Length; step++)
                AddStep(levels, sequence.Steps[step], fractions[step], fader);

            stepsOn?.Add(index + 1, SequenceTimeline.CurrentStep(sequence, elapsed, seed) + 1);
        }
    }

    // What one step gives while it is "fraction" of the way up, under the fader of its sequence.
    private void AddStep(byte[] levels, Step step, double fraction, byte fader)
    {
        byte level = (byte)Math.Round(step.Value * fraction);
        if (level == 0)
            return;

        if (!step.IsMemory)
        {
            levels[step.Number - 1] = Math.Max(levels[step.Number - 1], Scale(level, fader));
            return;
        }

        // A memory step plays that memory's look. An empty memory, or one that holds a
        // sequence itself, gives nothing: sequences are not played inside sequences.
        if (_memories[step.Number - 1] is not { } look)
            return;

        for (int channel = 0; channel < ChannelCount; channel++)
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
