namespace DmxBox.Server;

// The part of the desk that deals with sequences: a memory can hold one in place of a look.
// A sequence runs while the fader of its memory is up, from the moment the fader leaves zero.
// It is a layer like a look: scaled by the fader, merged highest-wins, and it moves no fader.
internal sealed partial class Desk
{
    // The sequence stored in each memory. Null where the memory holds a look or nothing.
    private readonly Sequence?[] _sequences = new Sequence?[MemoryCount];

    // When the fader of each memory last left zero, in desk seconds: the start of its sequence.
    private readonly double[] _sequenceStarts = new double[MemoryCount];

    /// <summary>
    /// Stores a sequence in a memory that is empty or holds a sequence already. If the memory's
    /// fader is up, the sequence starts from its first step.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="loop">True to start again after the last step, false to play once.</param>
    /// <param name="steps">The steps, at most 99. None at all is allowed: such a sequence gives nothing.</param>
    /// <exception cref="DeskException">The memory holds a look, or a step is out of range.</exception>
    public void SetSequence(int memory, bool loop, Step[]? steps)
    {
        RequireMemory(memory);
        steps ??= [];
        if (steps.Length > Sequence.MaxSteps)
            throw new DeskException($"A sequence can have at most {Sequence.MaxSteps} steps.");

        Step[] checkedSteps = Array.ConvertAll(steps, step => (step ?? throw new DeskException("A step is missing.")).Checked());

        lock (_lock)
        {
            if (_memories[memory - 1] is not null)
                throw new DeskException($"Memory {memory} holds a look. Empty it first.");

            StoreSequence(memory, new Sequence(loop, checkedSteps));
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
        _sequenceStarts[memory - 1] = Now;
    }

    // Call with the lock held. Every move of a memory fader goes through here, by hand or by
    // the stack: a fader that leaves zero starts the sequence of its memory.
    private void MoveMemoryFader(int memory, byte level)
    {
        if (_memoryLevels[memory - 1] == 0 && level > 0)
            _sequenceStarts[memory - 1] = Now;

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

            double elapsed = now - _sequenceStarts[index];
            double[] fractions = SequenceTimeline.Fractions(sequence, elapsed);
            for (int step = 0; step < fractions.Length; step++)
                AddStep(levels, sequence.Steps[step], fractions[step], fader);

            stepsOn?.Add(index + 1, SequenceTimeline.CurrentStep(sequence, elapsed) + 1);
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
                && SequenceTimeline.StillMoves(sequence, now - _sequenceStarts[index]))
            {
                return true;
            }
        }

        return false;
    }
}
