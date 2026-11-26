namespace DmxBox.Tests.Sequences;

/// <summary>
/// A sequence that plays itself the way the stack is played: a step that adds comes up on top of
/// what is on, a step without Add takes everything down, a release takes one step down in its own
/// down time, and a loop starts every round afresh; at random in the order of each round. A file
/// keeps all of it, in version 8.
/// </summary>
public sealed class SequenceAddReleaseTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;

    public SequenceAddReleaseTests()
    {
        _desk = new Desk(_time);
    }

    private static Step Channel(int channel, double up = 0, double wait = 1, double down = 0, bool add = false) =>
        new(Step.Channel, channel, 255, up, wait, down) { Add = add };

    private static Step Release(int step, double wait = 1, double down = 0) => new(Step.Release, step, 0, 0, wait, down);

    private void AssertLevels(params int[] channelsAndLevels)
    {
        byte[] output = _desk.GetOutput();
        for (int at = 0; at < channelsAndLevels.Length; at += 2)
            Assert.True(output[channelsAndLevels[at] - 1] == channelsAndLevels[at + 1], $"channel {channelsAndLevels[at]}: {output[channelsAndLevels[at] - 1]}, not {channelsAndLevels[at + 1]}");
    }

    [Fact]
    public void AStepThatAddsComesUpOnTopAndTheNextStepWithoutAddTakesEverythingDown()
    {
        _desk.SetSequence(3, loop: false,
        [
            Channel(1, wait: 1, down: 2),
            Channel(2, up: 2, wait: 1, down: 1, add: true),
            Channel(3),
        ]);
        _desk.SetMemoryLevel(3, 255);

        _time.Pass(0.5);
        AssertLevels(1, 255, 2, 0, 3, 0);
        _time.Pass(1.5);                              // half way through the up time of the step that adds
        AssertLevels(1, 255, 2, 128, 3, 0);
        Assert.Equal([1, 2], _desk.GetPlayback().SequenceOn[3]);
        Assert.Equal(2, _desk.GetPlayback().SequenceSteps[3]);

        _time.Pass(3);                                // a second into step 3: each goes down in its own down time
        AssertLevels(1, 128, 2, 0, 3, 255);
        Assert.Equal([3], _desk.GetPlayback().SequenceOn[3]);
    }

    [Fact]
    public void AReleaseTakesItsStepDownInItsOwnDownTimeAndLeavesTheRest()
    {
        _desk.SetSequence(3, loop: false,
        [
            Channel(1),
            Channel(2, down: 5, add: true),
            Release(2, wait: 1, down: 2),             // from 2 s, over two seconds: not in the step's five
            Channel(3, add: true),
        ]);
        _desk.SetMemoryLevel(3, 255);

        _time.Pass(1.5);
        AssertLevels(1, 255, 2, 255);
        _time.Pass(1.5);                              // half way down
        AssertLevels(1, 255, 2, 128);
        PlaybackOf(out int current, out int[] on);
        Assert.Equal(3, current);                     // the release is the step played last
        Assert.Equal([1], on);                        // step 1 is on
        _time.Pass(1);
        AssertLevels(1, 255, 2, 0, 3, 0);
        _time.Pass(1.5);                              // the step after the release's wait, on top again
        AssertLevels(1, 255, 2, 0, 3, 255);
        Assert.False(_desk.Animating);                // played once: what is on after the last step stays
    }

    [Fact]
    public void ALoopStartsEveryRoundAfreshSoThatWhatIsOnGoesDownWithoutADipWhereItComesAgain()
    {
        _desk.SetSequence(3, loop: true, [Channel(1, down: 1), Channel(2, down: 1, add: true)]);
        _desk.SetMemoryLevel(3, 255);

        _time.Pass(1.5);
        AssertLevels(1, 255, 2, 255);
        _time.Pass(1);                                // round 2 starts afresh: channel 2 goes down, channel 1 stays up
        AssertLevels(1, 255, 2, 128);
        Assert.Equal([1], _desk.GetPlayback().SequenceOn[3]);
        _time.Pass(1);                                // and comes on top again
        AssertLevels(1, 255, 2, 255);
    }

    [Fact]
    public void AStepThatAddsTakesOverAFaderItSharesWithAStepThatIsOnAndGivesItBackWhenReleased()
    {
        _desk.SetSequence(3, loop: false,
        [
            new Step(Step.Channel, 7, 200, 0, 1, 0),
            new Step(Step.Channel, 7, 60, Up: 2, Wait: 1, Down: 0) { Add = true },   // the same channel, lower: the later counts, as on the stack
            Release(2, wait: 1, down: 2),
        ]);
        _desk.SetMemoryLevel(3, 255);

        _time.Pass(2);                                // half way through the up time of the step that adds
        AssertLevels(7, 130);
        _time.Pass(1);
        AssertLevels(7, 60);
        _time.Pass(1.5);                              // released half a second ago, of two: back up a quarter
        AssertLevels(7, 95);
        _time.Pass(1.5);
        AssertLevels(7, 200);
    }

    [Fact]
    public void InRandomOrderTheStepsAddAndReleaseInTheOrderOfEachRound()
    {
        // step 3 releases step 1; step 2 adds. Every step lasts one second.
        Step[] steps = [Channel(1), Channel(2, add: true), Release(1)];
        var sequence = new Sequence(Loop: true, steps) { Random = true };

        for (int seed = 0; seed < 30; seed++)
        {
            for (long round = 0; round < 4; round++)
            {
                int[] order = SequenceTimeline.OrderOf(sequence, seed, round);
                List<int> on = [];
                for (int place = 0; place < order.Length; place++)
                {
                    int step = order[place];
                    if (place == 0)
                        on = step == 2 ? [] : [step];                 // every round starts afresh
                    else if (step == 2)
                        on.Remove(0);                                 // the release, of step 1 if it is on
                    else if (step == 1)
                        on.Add(1);                                    // on top
                    else
                        on = [0];                                     // takes everything down

                    SequenceTimeline.Moment moment = SequenceTimeline.At(sequence, round * 3 + place + 0.5, seed);
                    Assert.Equal(step, moment.Current);
                    Assert.Equal(on, moment.On);
                }
            }
        }
    }

    [Fact]
    public void AnAttributeComesFromTheLatestStepThatIsOnAndBackFromTheOneUnderItWhenThatOneIsReleased()
    {
        _desk.SetStage(EffectRuleTests.Plan(EffectRuleTests.Head()));   // pan, tilt and dimmer at 31 to 33
        _desk.SetSequence(3, loop: false,
        [
            new Step(Step.Channel, 31, 100, 0, 1, 0),
            new Step(Step.Channel, 31, 200, 0, 1, 0) { Add = true },
            Release(2),
        ]);
        _desk.SetMemoryLevel(3, 255);

        Assert.Equal(100, _desk.GetOutput()[30]);
        _time.Pass(1.5);
        Assert.Equal(200, _desk.GetOutput()[30]);
        _time.Pass(1);
        Assert.Equal(100, _desk.GetOutput()[30]);     // step 1 is still on: its pan again, as on the stack
    }

    [Fact]
    public void AReleaseMustNameAnotherStepOfTheSequence()
    {
        DeskException error = Assert.Throws<DeskException>(() => _desk.SetSequence(3, loop: true, [Channel(1), Release(2)]));
        Assert.Equal("Step 2 cannot release itself. Choose another step of the sequence.", error.Message);
        error = Assert.Throws<DeskException>(() => _desk.SetSequence(3, loop: true, [Release(4)]));
        Assert.Equal("Step 1 releases step 4, but the sequence has 1 step.", error.Message);
        error = Assert.Throws<DeskException>(() => _desk.AddSequenceStep(4, Release(1)));
        Assert.Equal("Step 1 cannot release itself. Choose another step of the sequence.", error.Message);

        _desk.SetSequence(3, loop: true, [Channel(1, add: true), Release(0), Release(1)]);
        Sequence kept = _desk.GetSequences()[3];
        Assert.True(kept.Steps[0].Add);
        Assert.Equal([0, 1], kept.Steps.Skip(1).Select(step => step.Number));
        Assert.False((_desk.GetSequences()[3].Steps[1] with { Add = true }).Checked().Add);   // a release never adds
    }

    [Fact]
    public void AFileKeepsTheStepsThatAddTheReleasesAndTheEffectsOfASequenceInVersion8()
    {
        _desk.SetSequence(3, loop: true,
        [
            Channel(1),
            Channel(2, add: true),
            Release(1, wait: 0.5, down: 2),
            new Step(Step.EffectTarget, 4, 200, Up: 1, Wait: 1, Down: 1) { Add = true },
        ]);

        string text = ProjectFormat.WriteText(_desk.GetProject().Content);
        Assert.Contains("\"version\": 8", text);
        Assert.Contains("\"add\": true", text);
        Assert.DoesNotContain("\"releases\"", text);  // a release of a sequence names its step by its number

        var loaded = new Desk(new ManualTime());
        loaded.ReplaceProject(ProjectFormat.ReadText(text));
        Step[] steps = loaded.GetSequences()[3].Steps;
        Assert.Equal([false, true, false, true], steps.Select(step => step.Add));
        Assert.Equal((Step.Release, 1, 2.0), (steps[2].Target, steps[2].Number, steps[2].Down));
        Assert.Equal((Step.EffectTarget, 4, 200), (steps[3].Target, steps[3].Number, steps[3].Value));

        // A release a file names wrongly, of itself or of a step the sequence does not have, releases nothing.
        const string wrong = """
            { "format": "lumenet-project", "version": 8, "memories": [], "sequences": [ { "number": 2, "steps": [
              { "target": "release", "number": 1, "value": 0, "up": 0, "wait": 0, "down": 1 },
              { "target": "release", "number": 9, "value": 0, "up": 0, "wait": 0, "down": 1 } ] } ] }
            """;
        loaded.ReplaceProject(ProjectFormat.ReadText(wrong));
        Assert.Equal([0, 0], loaded.GetSequences()[2].Steps.Select(step => step.Number));
    }

    [Fact]
    public void ASequenceWithoutThemKeepsTheVersionItHad()
    {
        _desk.SetSequence(3, loop: true, [Channel(1), Channel(2)]);
        string text = ProjectFormat.WriteText(_desk.GetProject().Content);
        Assert.Contains("\"version\": 5", text);
        Assert.DoesNotContain("\"add\"", text);
    }

    private void PlaybackOf(out int current, out int[] on)
    {
        Playback playback = _desk.GetPlayback();
        current = playback.SequenceSteps[3];
        on = playback.SequenceOn[3];
    }
}
