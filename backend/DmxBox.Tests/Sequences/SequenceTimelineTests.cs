namespace DmxBox.Tests.Sequences;

/// <summary>
/// What a sequence gives when: a step goes up, stays for its wait, and goes down again from
/// the moment the next step starts.
/// </summary>
public sealed class SequenceTimelineTests
{
    private static readonly Step SlowA = new("channel", 1, 255, Up: 2, Wait: 1, Down: 4);
    private static readonly Step QuickB = new("channel", 2, 255, Up: 2, Wait: 0, Down: 1);

    [Fact]
    public void PlayedOnceEachStepGoesUpStaysAndGoesDownWhenTheNextStarts()
    {
        var sequence = new Sequence(Loop: false, [SlowA, QuickB]);

        AssertFractions(sequence, 0, 0, 0);
        AssertFractions(sequence, 1, 0.5, 0);        // A half way up
        AssertFractions(sequence, 2, 1, 0);          // A up, now it waits one second
        AssertFractions(sequence, 3, 1, 0);          // B starts, A starts going down
        AssertFractions(sequence, 4, 0.75, 0.5);
        AssertFractions(sequence, 5, 0.5, 1);
        AssertFractions(sequence, 7, 0, 1);          // A is down after its four seconds
        AssertFractions(sequence, 1000, 0, 1);       // played once: the last step stays
    }

    [Fact]
    public void PlayedOnceItRestsWhenTheLastStepIsUpAndTheOthersAreDown()
    {
        var sequence = new Sequence(Loop: false, [SlowA, QuickB]);

        Assert.Equal(0, SequenceTimeline.CurrentStep(sequence, 2.9));
        Assert.Equal(1, SequenceTimeline.CurrentStep(sequence, 3));
        Assert.Equal(1, SequenceTimeline.CurrentStep(sequence, 1000));
        Assert.True(SequenceTimeline.StillMoves(sequence, 6.9));
        Assert.False(SequenceTimeline.StillMoves(sequence, 7));
    }

    [Fact]
    public void InALoopTheFirstStepFollowsTheLast()
    {
        var step = new Step("channel", 1, 255, Up: 1, Wait: 1, Down: 1);
        var sequence = new Sequence(Loop: true, [step, step with { Number = 2 }]);   // one round takes 4 s

        AssertFractions(sequence, 0.5, 0.5, 0);      // the first round: the second step has never run
        AssertFractions(sequence, 2.5, 0.5, 0.5);    // the first goes down while the second goes up
        AssertFractions(sequence, 3.5, 0, 1);
        AssertFractions(sequence, 4.5, 0.5, 0.5);    // the second round: the second goes down, the first comes again
        AssertFractions(sequence, 400.5, 0.5, 0.5);

        Assert.Equal(1, SequenceTimeline.CurrentStep(sequence, 3.9));
        Assert.Equal(0, SequenceTimeline.CurrentStep(sequence, 4.1));
        Assert.True(SequenceTimeline.StillMoves(sequence, 100000));
    }

    [Fact]
    public void WaitOfZeroMeansTheNextStepStartsAsSoonAsThisOneIsUp()
    {
        var step = new Step("channel", 1, 255, Up: 2, Wait: 0, Down: 0);
        var sequence = new Sequence(Loop: false, [step, step with { Number = 2 }]);

        AssertFractions(sequence, 1.9, 0.95, 0);
        AssertFractions(sequence, 2, 0, 0);          // up, and gone at once: its down time is 0
        AssertFractions(sequence, 3, 0, 0.5);
    }

    [Fact]
    public void StepsWithoutAnyTimeStillTakeAMomentEach()
    {
        var flash = new Step("channel", 1, 255, Up: 0, Wait: 0, Down: 0);
        var sequence = new Sequence(Loop: true, [flash, flash with { Number = 2 }]);

        AssertFractions(sequence, 0.02, 1, 0);
        AssertFractions(sequence, 0.07, 0, 1);
        AssertFractions(sequence, 0.12, 1, 0);       // the next round
        Assert.Equal(1, SequenceTimeline.CurrentStep(sequence, 0.07));
    }

    [Fact]
    public void AtRandomEveryRoundPlaysEveryStepOnceInAnOrderOfItsOwn()
    {
        Step[] steps = Enumerable.Range(1, 6).Select(number => new Step("channel", number, 255, 0, 1, 0)).ToArray();
        var sequence = new Sequence(Loop: false, steps) { Random = true };   // at random it loops anyway

        var orders = new List<string>();
        for (int round = 0; round < 20; round++)
        {
            int[] order = SequenceTimeline.OrderOf(sequence, seed: 42, round);
            Assert.Equal([0, 1, 2, 3, 4, 5], order.Order());   // every step once
            orders.Add(string.Join(",", order));

            // What plays in the round is that order: one second per step here.
            for (int place = 0; place < 6; place++)
                Assert.Equal(order[place], SequenceTimeline.CurrentStep(sequence, round * 6 + place + 0.5, seed: 42));
        }

        Assert.True(orders.Distinct().Count() > 10, string.Join(" | ", orders));   // the rounds differ
        Assert.True(SequenceTimeline.StillMoves(sequence, 1_000_000));
        Assert.Equal(orders, Enumerable.Range(0, 20).Select(round => string.Join(",", SequenceTimeline.OrderOf(sequence, 42, round))));   // the same every time it is asked
        Assert.NotEqual(orders[0], string.Join(",", SequenceTimeline.OrderOf(sequence, seed: 7, 0)));   // another start, other orders
    }

    [Fact]
    public void AtRandomTwoRoundsNeverMeetWithTheSameStep()
    {
        Step[] steps = Enumerable.Range(1, 3).Select(number => new Step("channel", number, 255, 0, 1, 0)).ToArray();
        var sequence = new Sequence(Loop: true, steps) { Random = true };

        for (int seed = 0; seed < 200; seed++)
        {
            for (int round = 1; round < 10; round++)
                Assert.NotEqual(SequenceTimeline.OrderOf(sequence, seed, round - 1)[^1], SequenceTimeline.OrderOf(sequence, seed, round)[0]);
        }
    }

    [Fact]
    public void AtRandomTheLastStepOfARoundGoesDownWhileTheNextRoundBegins()
    {
        var step = new Step("channel", 1, 255, Up: 1, Wait: 1, Down: 1);
        var sequence = new Sequence(Loop: true, [step, step with { Number = 2 }, step with { Number = 3 }]) { Random = true };
        int last = SequenceTimeline.OrderOf(sequence, seed: 5, round: 0)[^1];
        int first = SequenceTimeline.OrderOf(sequence, seed: 5, round: 1)[0];

        // One round takes 6 s. Half a second into the second round:
        double[] fractions = SequenceTimeline.Fractions(sequence, 6.5, seed: 5);
        Assert.Equal(0.5, fractions[last], precision: 6);    // half way down
        Assert.Equal(0.5, fractions[first], precision: 6);   // half way up
        Assert.Equal(0, fractions.Where((_, index) => index != last && index != first).Single(), precision: 6);
    }

    [Fact]
    public void SequenceWithoutStepsGivesNothing()
    {
        var empty = new Sequence(Loop: true, []);

        Assert.Empty(SequenceTimeline.Fractions(empty, 5));
        Assert.Equal(-1, SequenceTimeline.CurrentStep(empty, 5));
        Assert.False(SequenceTimeline.StillMoves(empty, 5));
    }

    private static void AssertFractions(Sequence sequence, double elapsed, params double[] expected)
    {
        double[] fractions = SequenceTimeline.Fractions(sequence, elapsed);
        Assert.Equal(expected.Length, fractions.Length);
        for (int index = 0; index < expected.Length; index++)
            Assert.Equal(expected[index], fractions[index], precision: 6);
    }
}
