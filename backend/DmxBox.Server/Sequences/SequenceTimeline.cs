namespace DmxBox.Server.Sequences;

/// <summary>
/// What a sequence gives at a moment, counted in seconds of the sequence from its start (at a
/// speed other than 1 the desk counts those faster or slower than real seconds).
/// </summary>
/// <remarks>
/// <para>
/// A sequence plays itself the way the stack is played: one step after the other, each starting
/// when the one before has come in and waited. A step comes up over its "up" time. A step without
/// "add" takes down every step that is on, each over its own "down" time; a step with "add" comes
/// up on top of them and takes nothing down. A release comes in over its "down" time: it takes
/// down the step it names (by its number in the list, from 1) and leaves the rest. With "loop"
/// the first step follows the last and starts afresh: everything that is on goes down, as at a
/// step without "add". Played once, what is on after the last step stays on. A step of a sound that
/// plays on ("continue") is the one exception: no step but a release that names it takes it down,
/// not at a new round either; its sound ends it (Desk.Sounds.cs). A step that is skipped
/// ("disabled") is not played at all: the rounds are made of the other steps.
/// </para>
/// <para>
/// A sequence played at random goes through all its steps on every round, each round in an
/// order of its own, and "add" and the releases follow that order. The order follows from a
/// number (the seed) and the number of the round, so it is the same however often it is asked
/// for. The desk takes a new seed whenever the sequence starts.
/// </para>
/// <para>
/// So everything follows from the clock, and nothing is kept between two moments: what is on at a
/// moment is found by going through the steps of its round from the start of the round, and
/// through the rounds before as far as a step of them can still be on its way down.
/// </para>
/// </remarks>
internal static class SequenceTimeline
{
    /// <summary>
    /// A step takes at least this long before the next one starts, even with "up" and "wait"
    /// both at 0. Otherwise a sequence of such steps would spin without time passing.
    /// </summary>
    public const double ShortestStep = 0.05;

    /// <summary>
    /// The steps of a sequence at a moment: the step played last and how far it has come in, the
    /// steps that were on when it started and those that are on now, and the steps on their way
    /// down. Desk.Sequences.cs makes the light of it.
    /// </summary>
    /// <param name="sequence">The sequence.</param>
    /// <param name="elapsed">Seconds since the sequence started.</param>
    /// <param name="seed">Chooses the orders of a sequence played at random.</param>
    public static Moment At(Sequence sequence, double elapsed, int seed = 0)
    {
        Step[] steps = sequence.Steps;
        int[] played = InTurn(steps);
        if (played.Length == 0)
            return Moment.None;

        elapsed = Math.Max(0, elapsed);
        var replay = new Replay(steps, elapsed);
        if (!sequence.Loops)
        {
            replay.Round(played, start: 0, PlaceAt(steps, played, elapsed), round: 0);
            return replay.Moment();
        }

        // The rounds before this one, as far back as a step of theirs can still be on its way
        // down: the first of them starts afresh, and what was on before it is down by now.
        (long round, double inRound, double length) = Where(steps, played, elapsed);
        long first = Math.Min(round - 1, (long)Math.Floor((elapsed - played.Max(step => steps[step].Down)) / length));
        first = Math.Max(0, Math.Max(first, round - MaxRoundsBack(played.Length)));
        for (long at = first; at <= round; at++)
        {
            int[] order = OrderOf(sequence, seed, at);
            replay.Round(order, at * length, at == round ? PlaceAt(steps, order, inRound) : order.Length - 1, at);
        }

        return replay.Moment();
    }

    /// <summary>
    /// How far each step is up, from 0 (not at all) to 1 (at its value): the step played last as
    /// far as it has come in (a release: 0, it gives nothing itself), every other step that is
    /// on 1, a step on its way down as far as it still is up.
    /// </summary>
    /// <param name="sequence">The sequence.</param>
    /// <param name="elapsed">Seconds since the sequence started.</param>
    /// <param name="seed">Chooses the orders of a sequence played at random.</param>
    public static double[] Fractions(Sequence sequence, double elapsed, int seed = 0)
    {
        double[] fractions = new double[sequence.Steps.Length];
        if (fractions.Length == 0)
            return fractions;

        Moment moment = At(sequence, elapsed, seed);
        foreach (int on in moment.On)
            fractions[on] = on == moment.Current ? moment.In : 1;
        foreach (Going going in moment.Going)
            fractions[going.Step] = Math.Max(fractions[going.Step], going.Fraction);

        return fractions;
    }

    /// <summary>The step that is on its way up or staying, counted from 0; -1 if the sequence has no steps.</summary>
    /// <param name="sequence">The sequence.</param>
    /// <param name="elapsed">Seconds since the sequence started.</param>
    /// <param name="seed">Chooses the orders of a sequence played at random.</param>
    public static int CurrentStep(Sequence sequence, double elapsed, int seed = 0)
    {
        Step[] steps = sequence.Steps;
        int[] order = InTurn(steps);
        if (order.Length == 0)
            return -1;

        double time = elapsed;
        if (sequence.Loops)
        {
            (long round, double inRound, _) = Where(steps, order, elapsed);
            order = OrderOf(sequence, seed, round);
            time = inRound;
        }

        return order[PlaceAt(steps, order, time)];
    }

    /// <summary>True while what the sequence gives still changes with time.</summary>
    /// <param name="sequence">The sequence.</param>
    /// <param name="elapsed">Seconds since the sequence started.</param>
    public static bool StillMoves(Sequence sequence, double elapsed)
    {
        Step[] steps = sequence.Steps;
        int[] played = InTurn(steps);
        if (played.Length == 0)
            return false;
        if (sequence.Loops)
            return true;

        // Played once: it rests when the last step has come in and every step that went is down.
        double start = 0, end = 0;
        List<int> on = [];
        foreach (int index in played)
        {
            Step step = steps[index];
            (List<int> after, bool tookAll) = OnAfter(steps, on, index, step, afresh: index == played[0]);
            end = Math.Max(end, start + ComeIn(step));
            if (tookAll)
            {
                foreach (int went in on.Where(went => !after.Contains(went)))
                    end = Math.Max(end, start + DownOf(steps[went], step));
            }

            on = after;
            start += LengthOf(step);
        }

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
        int[] order = InTurn(sequence.Steps);
        int count = order.Length;
        if (!sequence.Random || count < 2)
            return order;

        Shuffle(order, seed, round);
        if (count >= 3 && round > 0)
        {
            // The round before as it was shuffled. Its last place is never changed by the rule
            // here, which only swaps the first two places.
            int[] before = InTurn(sequence.Steps);
            Shuffle(before, seed, round - 1);
            if (order[0] == before[^1])
                (order[0], order[1]) = (order[1], order[0]);
        }

        return order;
    }

    /// <summary>
    /// How long a step of the sequence lasts on average, from its start to the start of the next,
    /// at speed 1: what one beat of the rate's tempo stands for. A sequence without steps: 1 s.
    /// </summary>
    public static double AverageStep(Sequence sequence)
    {
        Step[] played = [.. sequence.Steps.Where(step => !step.Disabled)];
        return played.Length == 0 ? 1 : played.Sum(LengthOf) / played.Length;
    }

    // How many rounds a moment goes back through at most. A step played longer ago is taken for
    // down, even if its down time is longer still: that happens only with down times far longer
    // than whole rounds of many steps.
    private static long MaxRoundsBack(int steps)
    {
        return Math.Max(1, (4 * steps + 64) / steps);
    }

    // Which round "elapsed" falls in, how far into it, and how long a round is. Every round is
    // equally long whatever its order: it plays every step that is played once.
    private static (long Round, double InRound, double Length) Where(Step[] steps, int[] played, double elapsed)
    {
        double length = 0;
        foreach (int step in played)
            length += LengthOf(steps[step]);

        long round = (long)Math.Floor(elapsed / length);
        return (round, elapsed - round * length, length);
    }

    // The place in "order" of the step played last at "time" seconds into the round: the last one
    // that has started by then.
    private static int PlaceAt(Step[] steps, int[] order, double time)
    {
        int place = 0;
        double start = LengthOf(steps[order[0]]);
        for (int at = 1; at < order.Length && start <= time; at++)
        {
            place = at;
            start += LengthOf(steps[order[at]]);
        }

        return place;
    }

    // The steps a round plays, in turn, by their places in the list: every step but those skipped.
    private static int[] InTurn(Step[] steps)
    {
        return [.. Enumerable.Range(0, steps.Length).Where(index => !steps[index].Disabled)];
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

    // The steps that are on once the step at "index" has started while "before" were on, as the
    // stack has it: the first step of a run or a round starts afresh, with nothing on under it (a
    // release there leaves nothing on); a release takes its step out; a step that adds comes last;
    // any other step is alone. Steps of sounds that play on stay through all of that, unless a
    // release names them. "TookAll": every step that was on went down, but those that play on.
    private static (List<int> After, bool TookAll) OnAfter(Step[] steps, List<int> before, int index, Step step, bool afresh)
    {
        if (step.IsRelease && !afresh)
            return ([.. before.Where(on => on != step.Number - 1)], false);
        if (step.Add && !afresh)
            return ([.. before.Where(on => on != index), index], false);

        List<int> stay = [.. before.Where(on => on != index && steps[on].Continue && !(step.IsRelease && on == step.Number - 1))];
        if (!step.IsRelease)
            stay.Add(index);

        return (stay, true);
    }

    // How long the step "went" takes to go down when "taker" takes everything down: its own down
    // time, or the release's when a release does it (as the first step of a round).
    private static double DownOf(Step went, Step taker)
    {
        return taker.IsRelease ? taker.Down : went.Down;
    }

    // How long a step takes to come in: its up time, or for a release its down time (the time
    // its step takes to go down).
    private static double ComeIn(Step step)
    {
        return step.IsRelease ? step.Down : step.Up;
    }

    // From the start of a step to the start of the next one.
    private static double LengthOf(Step step)
    {
        return Math.Max(ComeIn(step) + step.Wait, ShortestStep);
    }

    /// <summary>The steps of a sequence at a moment, see <see cref="At"/>.</summary>
    /// <param name="Current">The step played last, by its place in the list; -1 for a sequence without steps.</param>
    /// <param name="In">
    /// How far it has come in, from 0 to 1: its way up, or for a release the way down of the step
    /// it takes down.
    /// </param>
    /// <param name="TookAll">
    /// True when it took down every step that was on: a step without "add", or the first step of
    /// a run or a round. Then what goes down is in <see cref="Going"/>. False when it came on top
    /// of what is on (add), or took one step down (a release): then it changes what is on itself,
    /// in its own time.
    /// </param>
    /// <param name="Before">The steps that were on when it started, in the order they came on.</param>
    /// <param name="On">The steps that are on now, in the order they came on.</param>
    /// <param name="Going">The steps on their way down, the latest to go first.</param>
    public sealed record Moment(int Current, double In, bool TookAll, int[] Before, int[] On, Going[] Going)
    {
        /// <summary>The round the step played last belongs to, counted from 0; always 0 for a sequence played once.</summary>
        public long Round { get; init; }

        /// <summary>The moment of a sequence without steps.</summary>
        public static readonly Moment None = new(-1, 0, TookAll: true, [], [], []);
    }

    /// <summary>A step on its way down.</summary>
    /// <param name="Step">The step, by its place in the list.</param>
    /// <param name="Fraction">How far up it still is, from 1 (it just went) to 0 (down).</param>
    /// <param name="Over">
    /// The steps that were on when it went and had come on after it: where they use the same
    /// fader as this step, that fader was theirs, so this step takes nothing of it down.
    /// </param>
    public readonly record struct Going(int Step, double Fraction, int[] Over);

    // Goes through the steps in the order they are played, up to the moment, and keeps what the
    // moment needs.
    private sealed class Replay(Step[] steps, double elapsed)
    {
        private readonly List<Going[]> _going = [];   // for each step that took everything down: what is still going down
        private List<int> _on = [];
        private List<int> _before = [];
        private int _current = -1;
        private double _in;
        private bool _tookAll = true;
        private long _round;

        // The steps of one round in "order", from the moment "start", through the place "last".
        // The first of them starts afresh.
        public void Round(int[] order, double start, int last, long round)
        {
            for (int place = 0; place <= last; place++)
            {
                Step step = steps[order[place]];
                Play(order[place], step, start, afresh: place == 0);
                _round = round;
                start += LengthOf(step);
            }
        }

        public Moment Moment()
        {
            Going[] going = [.. Enumerable.Reverse(_going).SelectMany(went => went)];
            return new Moment(_current, _in, _tookAll, [.. _before], [.. _on], going) { Round = _round };
        }

        // The step at "index" starts at the moment "start".
        private void Play(int index, Step step, double start, bool afresh)
        {
            (List<int> after, bool tookAll) = OnAfter(steps, _on, index, step, afresh);
            if (tookAll && _on.Count > 0)
            {
                // Every step that was on goes down from here (but those that play on): it was all
                // the way up when it went.
                var going = new List<Going>();
                for (int place = _on.Count - 1; place >= 0; place--)
                {
                    if (after.Contains(_on[place]))
                        continue;

                    double seconds = DownOf(steps[_on[place]], step);
                    double left = seconds > 0 ? Math.Clamp(1 - (elapsed - start) / seconds, 0, 1) : 0;
                    if (left > 0)
                        going.Add(new Going(_on[place], left, [.. _on.Skip(place + 1)]));
                }

                if (going.Count > 0)
                    _going.Add([.. going]);
            }

            double comeIn = ComeIn(step);
            _current = index;
            _in = comeIn > 0 ? Math.Clamp((elapsed - start) / comeIn, 0, 1) : 1;
            _tookAll = tookAll;
            _before = _on;
            _on = after;
        }
    }
}
