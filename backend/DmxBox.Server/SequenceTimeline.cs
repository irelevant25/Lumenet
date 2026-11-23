namespace DmxBox.Server;

/// <summary>
/// What a sequence gives at a moment, counted in seconds of the sequence from its start (at a
/// speed other than 1 the desk counts those faster or slower than real seconds).
/// </summary>
/// <remarks>
/// <para>
/// A step goes up over its "up" time and then stays for its "wait" time. Then the next step
/// starts going up, and at that same moment this one starts going down over its "down" time.
/// With "loop" the first step follows the last; without it the last step stays up.
/// </para>
/// <para>
/// A sequence played at random goes through all its steps on every round, each round in an
/// order of its own. The order follows from a number (the seed) and the number of the round,
/// so it is the same however often it is asked for, and needs to be kept nowhere. The desk
/// takes a new seed whenever the sequence starts.
/// </para>
/// </remarks>
internal static class SequenceTimeline
{
    /// <summary>
    /// A step takes at least this long before the next one starts, even with "up" and "wait"
    /// both at 0. Otherwise a sequence of such steps would spin without time passing.
    /// </summary>
    public const double ShortestStep = 0.05;

    /// <summary>How far each step is up, from 0 (not at all) to 1 (at its value).</summary>
    /// <param name="sequence">The sequence.</param>
    /// <param name="elapsed">Seconds since the sequence started.</param>
    /// <param name="seed">Chooses the orders of a sequence played at random.</param>
    public static double[] Fractions(Sequence sequence, double elapsed, int seed = 0)
    {
        Step[] steps = sequence.Steps;
        double[] fractions = new double[steps.Length];
        if (steps.Length == 0)
            return fractions;

        if (!sequence.Loops)
        {
            double[] starts = StartsOf(steps, InTurn(steps.Length));
            for (int index = 0; index < steps.Length; index++)
                fractions[index] = OneRun(steps[index], elapsed - starts[index], stays: index == steps.Length - 1);

            return fractions;
        }

        (long round, double inRound, double roundLength) = Where(steps, elapsed);
        double[] now = StartsOf(steps, OrderOf(sequence, seed, round));
        double[]? before = round > 0 ? StartsOf(steps, OrderOf(sequence, seed, round - 1)) : null;
        for (int index = 0; index < steps.Length; index++)
        {
            // The run of this round, and the one from the round before, which may still be on
            // its way down.
            double current = OneRun(steps[index], inRound - now[index], stays: false);
            double previous = before is null ? 0 : OneRun(steps[index], inRound + roundLength - before[index], stays: false);
            fractions[index] = Math.Max(current, previous);
        }

        return fractions;
    }

    /// <summary>
    /// The steps that make what a sequence gives at a moment, for a crossfade without a dip (see
    /// Desk.Sequences.cs): first the step that is on, with how far up it is; then the steps played
    /// before it, the latest first, each with how far it still is up on its way down. The step
    /// played right before the one that is on is always there (its levels are where the rising
    /// channels start from), the older ones only while they are still on their way down. Empty
    /// if the sequence has no steps; only the step that is on for the first step of a run.
    /// </summary>
    /// <param name="sequence">The sequence.</param>
    /// <param name="elapsed">Seconds since the sequence started.</param>
    /// <param name="seed">Chooses the orders of a sequence played at random.</param>
    public static List<Play> Recent(Sequence sequence, double elapsed, int seed = 0)
    {
        Step[] steps = sequence.Steps;
        var plays = new List<Play>();
        if (steps.Length == 0)
            return plays;

        double longestDown = steps.Max(step => step.Down);
        if (!sequence.Loops)
        {
            double[] starts = StartsOf(steps, InTurn(steps.Length));
            int current = CurrentStep(sequence, elapsed);
            plays.Add(new Play(current, OneRun(steps[current], elapsed - starts[current], stays: current == steps.Length - 1)));
            for (int index = current - 1; index >= 0; index--)
            {
                double end = starts[index + 1];
                if (!Before(plays, steps[index], index, elapsed - end, longestDown))
                    break;
            }

            return plays;
        }

        (long round, double inRound, double length) = Where(steps, elapsed);
        int[] order = OrderOf(sequence, seed, round);
        double[] inThisRound = StartsOf(steps, order);
        int place = 0;
        for (int at = 1; at < order.Length && inThisRound[order[at]] <= inRound; at++)
            place = at;

        double start = round * length + inThisRound[order[place]];
        plays.Add(new Play(order[place], OneRun(steps[order[place]], elapsed - start, stays: false)));

        // Back through the steps played before it, into the rounds before as far as needed. A step
        // went down from the moment the step after it started.
        for (int guard = 0; guard < 4 * steps.Length + 64; guard++)
        {
            if (--place < 0)
            {
                if (--round < 0)
                    break;

                order = OrderOf(sequence, seed, round);
                inThisRound = StartsOf(steps, order);
                place = order.Length - 1;
            }

            int index = order[place];
            double end = start;
            start = round * length + inThisRound[index];
            if (!Before(plays, steps[index], index, elapsed - end, longestDown))
                break;
        }

        return plays;
    }

    /// <summary>The step that is on its way up or staying, counted from 0; -1 if the sequence has no steps.</summary>
    /// <param name="sequence">The sequence.</param>
    /// <param name="elapsed">Seconds since the sequence started.</param>
    /// <param name="seed">Chooses the orders of a sequence played at random.</param>
    public static int CurrentStep(Sequence sequence, double elapsed, int seed = 0)
    {
        Step[] steps = sequence.Steps;
        if (steps.Length == 0)
            return -1;

        int[] order = InTurn(steps.Length);
        double time = elapsed;
        if (sequence.Loops)
        {
            (long round, double inRound, _) = Where(steps, elapsed);
            order = OrderOf(sequence, seed, round);
            time = inRound;
        }

        int current = order[0];
        double start = 0;
        foreach (int index in order)
        {
            if (start > time)
                break;

            current = index;
            start += LengthOf(steps[index]);
        }

        return current;
    }

    /// <summary>True while what the sequence gives still changes with time.</summary>
    /// <param name="sequence">The sequence.</param>
    /// <param name="elapsed">Seconds since the sequence started.</param>
    public static bool StillMoves(Sequence sequence, double elapsed)
    {
        Step[] steps = sequence.Steps;
        if (steps.Length == 0)
            return false;
        if (sequence.Loops)
            return true;

        // Played once: it rests when the last step is up and every earlier one is down.
        double[] starts = StartsOf(steps, InTurn(steps.Length));
        double end = starts[^1] + steps[^1].Up;
        for (int index = 0; index < steps.Length - 1; index++)
            end = Math.Max(end, starts[index] + LengthOf(steps[index]) + steps[index].Down);

        return elapsed < end;
    }

    /// <summary>
    /// The order in which a round plays the steps, as their places in the list: in turn, or for
    /// a sequence played at random one of its own for every round. Two rounds never meet with
    /// the same step on both sides, when there are three steps or more: that would look like a
    /// hiccup.
    /// </summary>
    /// <param name="sequence">The sequence.</param>
    /// <param name="seed">Chooses the orders.</param>
    /// <param name="round">The round, counted from 0.</param>
    public static int[] OrderOf(Sequence sequence, int seed, long round)
    {
        int count = sequence.Steps.Length;
        int[] order = InTurn(count);
        if (!sequence.Random || count < 2)
            return order;

        Shuffle(order, seed, round);
        if (count >= 3 && round > 0)
        {
            // The round before as it was shuffled. Its last place is never changed by the rule
            // here, which only swaps the first two places.
            int[] before = InTurn(count);
            Shuffle(before, seed, round - 1);
            if (order[0] == before[^1])
                (order[0], order[1]) = (order[1], order[0]);
        }

        return order;
    }

    // Which round "elapsed" falls in, how far into it, and how long a round is. Every round is
    // equally long whatever its order: it plays every step once.
    private static (long Round, double InRound, double Length) Where(Step[] steps, double elapsed)
    {
        double length = 0;
        foreach (Step step in steps)
            length += LengthOf(step);

        long round = (long)Math.Floor(elapsed / length);
        return (round, elapsed - round * length, length);
    }

    // When each step starts within a round played in "order", by the step's place in the list.
    private static double[] StartsOf(Step[] steps, int[] order)
    {
        double[] starts = new double[steps.Length];
        double start = 0;
        foreach (int index in order)
        {
            starts[index] = start;
            start += LengthOf(steps[index]);
        }

        return starts;
    }

    private static int[] InTurn(int count)
    {
        return Enumerable.Range(0, count).ToArray();
    }

    // Fisher and Yates' shuffle, driven by SplitMix64: a small generator that gives the same
    // numbers for the same start, on every computer and in every version of .NET.
    private static void Shuffle(int[] order, int seed, long round)
    {
        ulong state = unchecked(((ulong)(uint)seed * 0x9E3779B97F4A7C15UL) ^ (ulong)round);
        for (int index = order.Length - 1; index > 0; index--)
        {
            state = unchecked(state + 0x9E3779B97F4A7C15UL);
            ulong mixed = state;
            mixed = unchecked((mixed ^ (mixed >> 30)) * 0xBF58476D1CE4E5B9UL);
            mixed = unchecked((mixed ^ (mixed >> 27)) * 0x94D049BB133111EBUL);
            mixed ^= mixed >> 31;
            int other = (int)(mixed % (ulong)(index + 1));
            (order[index], order[other]) = (order[other], order[index]);
        }
    }

    /// <summary>A step as it was played once.</summary>
    /// <param name="Step">The step's place in the list of the sequence.</param>
    /// <param name="Fraction">How far up it is: from 0 to 1 on its way up, from 1 to 0 on its way down.</param>
    public readonly record struct Play(int Step, double Fraction);

    // Adds a step played before the one that is on, "since" seconds after it began its way down:
    // the one right before the step that is on always, an older one only while it is not all the
    // way down. False once even the longest down time is over: the steps played before this one
    // are down for sure.
    private static bool Before(List<Play> plays, Step step, int index, double since, double longestDown)
    {
        double left = step.Down > 0 ? Math.Clamp(1 - since / step.Down, 0, 1) : 0;
        if (plays.Count == 1 || left > 0)
            plays.Add(new Play(index, left));

        return since <= longestDown;
    }

    // From the start of a step to the start of the next one.
    private static double LengthOf(Step step)
    {
        return Math.Max(step.Up + step.Wait, ShortestStep);
    }

    // One run of a step, "since" seconds after it started: up, staying, then down.
    // A step that "stays" never goes down: the last one of a sequence that is played once.
    private static double OneRun(Step step, double since, bool stays)
    {
        if (since < 0)
            return 0;

        double length = LengthOf(step);
        if (stays || since < length)
            return step.Up > 0 ? Math.Min(1, since / step.Up) : 1;

        double gone = since - length;
        return step.Down > 0 ? Math.Max(0, 1 - gone / step.Down) : 0;
    }
}
