namespace DmxBox.Server;

/// <summary>
/// What a sequence gives at a moment, counted in seconds from its start.
/// </summary>
/// <remarks>
/// A step goes up over its "up" time and then stays for its "wait" time. Then the next step
/// starts going up, and at that same moment this one starts going down over its "down" time.
/// With "loop" the first step follows the last; without it the last step stays up.
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
    public static double[] Fractions(Sequence sequence, double elapsed)
    {
        Step[] steps = sequence.Steps;
        double[] fractions = new double[steps.Length];
        if (steps.Length == 0)
            return fractions;

        double[] starts = StartsOf(steps, out double round);
        if (!sequence.Loop)
        {
            for (int index = 0; index < steps.Length; index++)
                fractions[index] = OneRun(steps[index], elapsed - starts[index], stays: index == steps.Length - 1);

            return fractions;
        }

        double inRound = elapsed % round;
        bool hasRunBefore = elapsed >= round;
        for (int index = 0; index < steps.Length; index++)
        {
            // The run that started in this round, and the one from the round before, which may
            // still be on its way down.
            double now = OneRun(steps[index], inRound - starts[index], stays: false);
            double before = hasRunBefore ? OneRun(steps[index], inRound - starts[index] + round, stays: false) : 0;
            fractions[index] = Math.Max(now, before);
        }

        return fractions;
    }

    /// <summary>The step that is on its way up or staying, counted from 0; -1 if the sequence has no steps.</summary>
    /// <param name="sequence">The sequence.</param>
    /// <param name="elapsed">Seconds since the sequence started.</param>
    public static int CurrentStep(Sequence sequence, double elapsed)
    {
        Step[] steps = sequence.Steps;
        if (steps.Length == 0)
            return -1;

        double[] starts = StartsOf(steps, out double round);
        double time = sequence.Loop ? elapsed % round : elapsed;
        int current = 0;
        for (int index = 1; index < steps.Length && starts[index] <= time; index++)
            current = index;

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
        if (sequence.Loop)
            return true;

        // Played once: it rests when the last step is up and every earlier one is down.
        double[] starts = StartsOf(steps, out _);
        double end = starts[^1] + steps[^1].Up;
        for (int index = 0; index < steps.Length - 1; index++)
            end = Math.Max(end, starts[index] + LengthOf(steps[index]) + steps[index].Down);

        return elapsed < end;
    }

    // When each step starts, and how long one round through all of them takes.
    private static double[] StartsOf(Step[] steps, out double round)
    {
        double[] starts = new double[steps.Length];
        round = 0;
        for (int index = 0; index < steps.Length; index++)
        {
            starts[index] = round;
            round += LengthOf(steps[index]);
        }

        return starts;
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
