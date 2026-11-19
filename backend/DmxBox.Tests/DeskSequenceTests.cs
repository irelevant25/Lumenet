using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// Sequences on the desk: a memory holds one in place of a look, and it runs while the
/// memory's fader is up, as a layer under that fader.
/// </summary>
public sealed class DeskSequenceTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;

    public DeskSequenceTests()
    {
        _desk = new Desk(_time);
    }

    [Fact]
    public void SequenceStartsWhenItsFaderLeavesZeroAndIsScaledByTheFader()
    {
        _desk.SetSequence(2, loop: false, [new Step("channel", 5, 200, Up: 2, Wait: 0, Down: 0)]);
        _time.Pass(10);                              // time that passes before the fader is up does not count
        Assert.Equal(0, _desk.GetOutput()[4]);

        _desk.SetMemoryLevel(2, 255);
        Assert.Equal(0, _desk.GetOutput()[4]);
        _time.Pass(1);
        Assert.Equal(100, _desk.GetOutput()[4]);     // half way up
        _time.Pass(1);
        Assert.Equal(200, _desk.GetOutput()[4]);
        _time.Pass(100);
        Assert.Equal(200, _desk.GetOutput()[4]);     // played once: the last step stays

        _desk.SetMemoryLevel(2, 128);                // the fader scales it, like a look
        Assert.Equal(100, _desk.GetOutput()[4]);
        Assert.Equal(0, _desk.GetState().Channels[4]);   // and no fader has moved
    }

    [Fact]
    public void SequenceStartsAgainFromItsFirstStepWhenTheFaderComesBackUp()
    {
        _desk.SetSequence(2, loop: false, [new Step("channel", 5, 200, Up: 2, Wait: 0, Down: 0)]);
        _desk.SetMemoryLevel(2, 255);
        _time.Pass(2);
        Assert.Equal(200, _desk.GetOutput()[4]);

        _desk.SetMemoryLevel(2, 0);
        Assert.Equal(0, _desk.GetOutput()[4]);
        _time.Pass(5);
        _desk.SetMemoryLevel(2, 255);
        Assert.Equal(0, _desk.GetOutput()[4]);
        _time.Pass(1);
        Assert.Equal(100, _desk.GetOutput()[4]);
    }

    [Fact]
    public void MemoryStepPlaysTheLookOfThatMemoryAndHighestWins()
    {
        _desk.SetChannel(1, 100);
        _desk.SetChannel(2, 255);
        _desk.SaveMemory(1);
        _desk.Clear();
        _desk.SetSequence(3, loop: false, [new Step("memory", 1, 128, Up: 0, Wait: 0, Down: 0)]);

        _desk.SetChannel(1, 60);                     // the channel's own fader is higher than what the step gives
        _desk.SetMemoryLevel(3, 255);

        byte[] output = _desk.GetOutput();
        Assert.Equal(60, output[0]);                 // 100 at 128 would be 50
        Assert.Equal(128, output[1]);
        Assert.Equal(0, output[2]);
    }

    [Fact]
    public void MemoryStepThatPointsAtNothingOrAtASequenceGivesNothing()
    {
        _desk.SetSequence(1, loop: true, [new Step("channel", 9, 255, 0, 1, 0)]);
        _desk.SetSequence(3, loop: false, [new Step("memory", 1, 255, 0, 0, 0), new Step("memory", 77, 255, 0, 0, 0)]);

        _desk.SetMemoryLevel(3, 255);
        _time.Pass(1);

        Assert.All(_desk.GetOutput(), level => Assert.Equal(0, level));
    }

    [Fact]
    public void SequenceIsOnlyStoredInAMemoryThatIsEmptyOrHoldsOne()
    {
        _desk.SaveMemory(4);
        var error = Assert.Throws<DeskException>(() => _desk.SetSequence(4, false, []));
        Assert.Equal("Memory 4 holds a look. Empty it first.", error.Message);

        _desk.SetSequence(5, loop: true, []);                                 // an empty one is allowed
        _desk.SetSequence(5, loop: false, [new Step("channel", 1, 1, 0, 0, 0)]);   // and it can be replaced
        Assert.False(_desk.GetSequences()[5].Loop);
        Assert.Single(_desk.GetSequences()[5].Steps);
        Assert.Equal([5], _desk.GetSequences().Keys);
    }

    [Fact]
    public void AddingAStepStartsASequencePlayedInALoopOrGoesOnWithTheOneThatIsThere()
    {
        long before = _desk.ProjectChanges;
        _desk.AddSequenceStep(4, new Step("channel", 1, 255, 1, 1.04, 1));

        Sequence started = _desk.GetSequences()[4];
        Assert.True(started.Loop);                   // a new sequence loops unless its owner says otherwise
        Assert.Equal([new Step("channel", 1, 255, 1, 1, 1)], started.Steps);
        Assert.True(_desk.GetState().MemorySequence[3]);
        Assert.Equal(before + 1, _desk.ProjectChanges);

        _desk.SetSequence(4, loop: false, started.Steps);
        _desk.AddSequenceStep(4, new Step("memory", 2, 100, 0, 0, 0));

        Sequence longer = _desk.GetSequences()[4];
        Assert.False(longer.Loop);                   // one that is there stays what it was
        Assert.Equal(["channel", "memory"], longer.Steps.Select(step => step.Target));

        _desk.SaveMemory(6);
        var error = Assert.Throws<DeskException>(() => _desk.AddSequenceStep(6, new Step("channel", 1, 1, 0, 0, 0)));
        Assert.Equal("Memory 6 holds a look. Empty it first.", error.Message);
        Assert.Throws<DeskException>(() => _desk.AddSequenceStep(4, new Step("channel", 513, 1, 0, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => _desk.AddSequenceStep(129, new Step("channel", 1, 1, 0, 0, 0)));
    }

    [Fact]
    public void SequenceThatIsFullTakesNoMoreSteps()
    {
        var step = new Step("channel", 1, 1, 0, 0, 0);
        _desk.SetSequence(1, loop: true, Enumerable.Repeat(step, Sequence.MaxSteps).ToArray());

        var error = Assert.Throws<DeskException>(() => _desk.AddSequenceStep(1, step));

        Assert.Equal("A sequence can have at most 99 steps.", error.Message);
        Assert.Equal(99, _desk.GetSequences()[1].Steps.Length);
    }

    [Fact]
    public void StepAddedWhileTheFaderIsUpStartsTheSequenceFromItsFirstStep()
    {
        _desk.AddSequenceStep(2, new Step("channel", 5, 200, Up: 2, Wait: 0, Down: 0));
        _desk.SetMemoryLevel(2, 255);
        _time.Pass(1);
        Assert.Equal(100, _desk.GetOutput()[4]);     // half way up

        _desk.AddSequenceStep(2, new Step("channel", 6, 200, Up: 2, Wait: 0, Down: 0));

        Assert.Equal(0, _desk.GetOutput()[4]);       // from the top again
        _time.Pass(0.5);
        Assert.Equal(50, _desk.GetOutput()[4]);
        Assert.Equal(0, _desk.GetOutput()[5]);
    }

    [Fact]
    public void SpeedMakesEveryTimeOfTheSequenceShorterOrLonger()
    {
        var step = new Step("channel", 5, 200, Up: 2, Wait: 0, Down: 0);
        _desk.SetSequence(2, new Sequence(Loop: false, [step]) { Speed = 2 });
        _desk.SetMemoryLevel(2, 255);

        _time.Pass(0.5);
        Assert.Equal(100, _desk.GetOutput()[4]);     // at speed 2 the two seconds up take one
        _time.Pass(0.5);
        Assert.Equal(200, _desk.GetOutput()[4]);

        _desk.SetSequence(3, new Sequence(Loop: false, [step with { Number = 6 }]) { Speed = 0.5 });
        _desk.SetMemoryLevel(3, 255);
        _time.Pass(2);
        Assert.Equal(100, _desk.GetOutput()[5]);     // at half speed they take four
    }

    [Fact]
    public void ChangingTheSpeedGoesOnFromWhereTheSequenceIs()
    {
        _desk.SetSequence(2, loop: false, [new Step("channel", 5, 200, Up: 4, Wait: 0, Down: 0)]);
        _desk.SetMemoryLevel(2, 255);
        _time.Pass(1);
        Assert.Equal(50, _desk.GetOutput()[4]);      // a quarter of the way up

        Assert.Equal(3, _desk.SetSequenceSpeed(2, 3));

        Assert.Equal(50, _desk.GetOutput()[4]);      // no jump: it does not start again either
        _time.Pass(0.5);
        Assert.Equal(125, _desk.GetOutput()[4]);     // the next 1.5 s of the step in half a second
        Assert.Equal(3, _desk.GetSequences()[2].Speed);
    }

    [Fact]
    public void SpeedGoesInStepsOfFiveHundredthsFromATenthToTen()
    {
        _desk.SetSequence(2, loop: true, [new Step("channel", 5, 200, 1, 1, 1)]);

        Assert.Equal(1.25, _desk.SetSequenceSpeed(2, 1.234));
        Assert.Equal(0.1, _desk.SetSequenceSpeed(2, 0.1));
        Assert.Equal(10, _desk.SetSequenceSpeed(2, 10));
        AssertSpeedRefused(0.09);
        AssertSpeedRefused(10.01);
        AssertSpeedRefused(double.NaN);
        Assert.Throws<DeskException>(() => _desk.SetSequence(3, new Sequence(true, []) { Speed = 0 }));

        var noSequence = Assert.Throws<DeskException>(() => _desk.SetSequenceSpeed(4, 1));
        Assert.Equal("Memory 4 holds no sequence.", noSequence.Message);

        long before = _desk.ProjectChanges;
        _desk.SetSequenceSpeed(2, 2);
        _desk.SetSequenceSpeed(2, 2);                // the speed it has already
        Assert.Equal(before + 1, _desk.ProjectChanges);
    }

    [Fact]
    public void RandomAndSpeedGoAlongWhenTheSequenceIsCopiedOrGetsAStep()
    {
        _desk.SetSequence(1, new Sequence(Loop: true, [new Step("channel", 1, 255, 1, 1, 1)]) { Random = true, Speed = 0.5 });

        _desk.CopyMemory(1, 2);
        _desk.AddSequenceStep(1, new Step("channel", 2, 255, 1, 1, 1));

        Assert.True(_desk.GetSequences()[2].Random);
        Assert.Equal(0.5, _desk.GetSequences()[2].Speed);
        Assert.True(_desk.GetSequences()[1].Random);
        Assert.Equal(0.5, _desk.GetSequences()[1].Speed);
    }

    [Fact]
    public void StepsOutOfRangeAreRefused()
    {
        AssertRefused("stands for a channel or a memory", new Step("light", 1, 1, 0, 0, 0));
        AssertRefused("The channel of a step must be from 1 to 512, not 513.", new Step("channel", 513, 1, 0, 0, 0));
        AssertRefused("The memory of a step must be from 1 to 128, not 0.", new Step("memory", 0, 1, 0, 0, 0));
        AssertRefused("The value of a step must be from 0 to 255, not 256.", new Step("channel", 1, 256, 0, 0, 0));
        AssertRefused("The up time", new Step("channel", 1, 1, -1, 0, 0));
        AssertRefused("The wait time", new Step("channel", 1, 1, 0, double.NaN, 0));
        AssertRefused("The down time", new Step("channel", 1, 1, 0, 0, 1000));

        Step[] tooMany = Enumerable.Repeat(new Step("channel", 1, 1, 0, 0, 0), 100).ToArray();
        Assert.Throws<DeskException>(() => _desk.SetSequence(1, false, tooMany));
        Assert.Empty(_desk.GetSequences());
    }

    [Fact]
    public void TimesAreKeptInTenthsOfASecond()
    {
        _desk.SetSequence(1, false, [new Step("channel", 1, 1, 1.26, 0.04, 999.9)]);

        Step step = Assert.Single(_desk.GetSequences()[1].Steps);
        Assert.Equal(1.3, step.Up);
        Assert.Equal(0, step.Wait);
        Assert.Equal(999.9, step.Down);
    }

    [Fact]
    public void TheMemoryFunctionsKnowSequences()
    {
        _desk.SetSequence(1, loop: true, [new Step("channel", 1, 255, 1, 1, 1)]);
        DeskState state = _desk.GetState();
        Assert.True(state.MemoryStored[0]);
        Assert.True(state.MemorySequence[0]);
        Assert.Empty(_desk.GetMemoryLooks());        // a sequence is no look

        var error = Assert.Throws<DeskException>(() => _desk.LoadMemory(1));
        Assert.Equal("Memory 1 holds a sequence. Change it on the Sequences page.", error.Message);

        _desk.CopyMemory(1, 2);                      // copy: a sequence of its own
        Assert.True(_desk.GetState().MemorySequence[1]);
        Assert.True(_desk.GetSequences()[2].Loop);

        _desk.SaveMemory(1);                         // save: a look takes its place
        Assert.False(_desk.GetState().MemorySequence[0]);
        Assert.True(_desk.GetState().MemoryStored[0]);
        Assert.Equal([1], _desk.GetMemoryLooks().Keys);

        _desk.CopyMemory(1, 2);                      // a look copied onto a sequence replaces it
        Assert.False(_desk.GetState().MemorySequence[1]);

        _desk.SetSequence(3, false, []);
        _desk.DeleteMemory(3);
        Assert.False(_desk.GetState().MemoryStored[2]);
        Assert.Empty(_desk.GetSequences());
    }

    [Fact]
    public void PlaybackSaysWhatTheSequencesGiveAndWhichStepTheyAreOn()
    {
        var step = new Step("channel", 5, 200, Up: 1, Wait: 1, Down: 0);
        _desk.SetSequence(2, loop: true, [step, step with { Number = 6 }]);
        Assert.False(_desk.Animating);

        _desk.SetChannel(5, 255);                    // the channel's own fader is not part of what the sequence gives
        _desk.SetMemoryLevel(2, 255);
        _time.Pass(0.5);

        Playback playback = _desk.GetPlayback();
        Assert.Equal(100, playback.Levels[4]);
        Assert.Equal(0, playback.Levels[5]);
        Assert.Equal(1, playback.SequenceSteps[2]);
        Assert.True(_desk.Animating);

        _time.Pass(2);                               // 2.5 s: the second step is half way up
        playback = _desk.GetPlayback();
        Assert.Equal(0, playback.Levels[4]);
        Assert.Equal(100, playback.Levels[5]);
        Assert.Equal(2, playback.SequenceSteps[2]);

        _desk.SetMemoryLevel(2, 0);
        Assert.Empty(_desk.GetPlayback().SequenceSteps);
        Assert.False(_desk.Animating);
    }

    [Fact]
    public void TheOutputCountsAsChangedWhileASequenceMovesAndNotAfterwards()
    {
        _desk.SetSequence(2, loop: false, [new Step("channel", 5, 200, Up: 1, Wait: 0, Down: 0)]);
        _desk.SetMemoryLevel(2, 255);

        long before = _desk.OutputChanges;
        _time.Pass(0.5);
        _desk.Tick();
        Assert.True(_desk.OutputChanges > before);   // the box link sends again

        _time.Pass(5);                               // played once, and done
        _desk.Tick();
        long rested = _desk.OutputChanges;
        _time.Pass(5);
        _desk.Tick();
        Assert.Equal(rested, _desk.OutputChanges);
        Assert.False(_desk.Animating);
    }

    private void AssertSpeedRefused(double speed)
    {
        var error = Assert.Throws<DeskException>(() => _desk.SetSequenceSpeed(2, speed));
        Assert.StartsWith("The speed of a sequence must be from 0.1 to 10", error.Message);
    }

    private void AssertRefused(string expectedMessage, Step step)
    {
        var error = Assert.Throws<DeskException>(() => _desk.SetSequence(1, false, [step]));
        Assert.Contains(expectedMessage, error.Message);
    }
}
