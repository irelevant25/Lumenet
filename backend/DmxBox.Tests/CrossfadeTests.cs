using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// Going from one step to the next, in a sequence and in the stack: every channel moves from its
/// level in the one step to its level in the other. A channel both steps hold at the same level
/// stays where it is (before, the higher of the two fading steps won, and it dipped half way); one
/// that rises follows the up time of the step that comes, one that falls the down time of the step
/// that goes.
/// </summary>
public sealed class CrossfadeTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;

    public CrossfadeTests()
    {
        _desk = new Desk(_time);
        Look(1, (37, 210), (38, 100), (45, 255), (50, 88));     // 50 only here
        Look(2, (37, 150), (38, 200), (45, 255), (60, 80));     // 60 only here
        Look(4, (37, 30), (45, 255));
    }

    // ---------------------------------------------------------------- sequences

    [Fact]
    public void ASequenceGoesFromStepToStepWithoutADip()
    {
        _desk.SetSequence(3, loop: true,
        [
            new Step("memory", 1, 255, Up: 0, Wait: 2, Down: 4),
            new Step("memory", 2, 255, Up: 2, Wait: 2, Down: 2),
        ]);
        _desk.SetMemoryLevel(3, 255);

        _time.Pass(1);
        AssertLevels(37, 210, 38, 100, 45, 255, 50, 88, 60, 0);

        _time.Pass(2);                               // one second into the step of memory 2
        AssertLevels(
            37, 195,                                 // 210 to 150 over the four seconds down of the step before: a quarter
            38, 150,                                 // 100 to 200 over the two seconds up of this step: half
            45, 255,                                 // the same in both: it stays
            50, 66,                                  // only in the step before: down to 0 with it
            60, 40);                                 // only in this one: up with it
        _time.Pass(1);
        AssertLevels(37, 180, 38, 200, 45, 255, 50, 44, 60, 80);
        _time.Pass(1);
        AssertLevels(37, 165, 50, 22);               // three of the four seconds down

        _time.Pass(1.5);                             // the loop: the first step again, up at once, the second goes down in 2 s
        AssertLevels(37, 210, 38, 175, 45, 255, 50, 88, 60, 60);
    }

    [Fact]
    public void AChannelTheStepsShareNeverDipsAlsoInALoopAndAtRandom()
    {
        Step[] steps =
        [
            new Step("memory", 1, 255, Up: 1, Wait: 1, Down: 1),
            new Step("memory", 2, 255, Up: 1, Wait: 1, Down: 1),
            new Step("memory", 4, 255, Up: 1, Wait: 1, Down: 1),
        ];
        _desk.SetSequence(3, new Sequence(Loop: true, steps));
        _desk.SetSequence(5, new Sequence(Loop: true, steps) { Random = true });
        _desk.SetMemoryLevel(3, 255);
        _time.Pass(1);                               // the first step is up

        for (int tenth = 0; tenth < 200; tenth++)    // 20 seconds: more than three rounds
        {
            Assert.Equal(255, _desk.GetOutput()[44]);
            _time.Pass(0.1);
        }

        _desk.SetMemoryLevel(3, 0);
        _desk.SetMemoryLevel(5, 255);                // the same steps, in another order on every round
        _time.Pass(1);
        for (int tenth = 0; tenth < 200; tenth++)
        {
            Assert.Equal(255, _desk.GetOutput()[44]);
            _time.Pass(0.1);
        }
    }

    [Fact]
    public void TheStepsPlayedBeforeAreThoseOfTheRoundBeforeAtTheStartOfARound()
    {
        Step[] steps =
        [
            new Step("channel", 1, 255, Up: 1, Wait: 1, Down: 3),
            new Step("channel", 2, 255, Up: 1, Wait: 1, Down: 3),
            new Step("channel", 3, 255, Up: 1, Wait: 1, Down: 3),
        ];
        var sequence = new Sequence(Loop: true, steps) { Random = true };
        int[] before = SequenceTimeline.OrderOf(sequence, seed: 5, round: 0);
        int[] now = SequenceTimeline.OrderOf(sequence, seed: 5, round: 1);

        SequenceTimeline.Moment moment = SequenceTimeline.At(sequence, 6.5, seed: 5);

        Assert.Equal(now[0], moment.Current);
        Assert.Equal(0.5, moment.In, precision: 6);                    // half way up
        Assert.Equal([before[2]], moment.Before);                      // the light rises from there
        Assert.Equal([before[2], before[1]], moment.Going.Select(going => going.Step));
        Assert.Equal(1 - 0.5 / 3, moment.Going[0].Fraction, precision: 6);    // half a second of its three down
        Assert.Equal(1 - 2.5 / 3, moment.Going[1].Fraction, precision: 6);    // still on its way down: kept

        SequenceTimeline.Moment first = SequenceTimeline.At(sequence, 0.5, seed: 5);   // the first step of all: nothing before it
        Assert.Empty(first.Before);
        Assert.Empty(first.Going);
    }

    [Fact]
    public void AStepPlayedEarlierWithALongDownTimeFadesOnFromWhereTheNewerStepsHaveIt()
    {
        _desk.SetSequence(3, loop: false,
        [
            new Step("channel", 9, 200, Up: 0, Wait: 1, Down: 8),
            new Step("channel", 9, 100, Up: 0, Wait: 1, Down: 0),
            new Step("channel", 9, 100, Up: 0, Wait: 1, Down: 0),
        ]);
        _desk.SetMemoryLevel(3, 255);

        _time.Pass(1.5);                             // the second step: 200 to 100 over the first step's eight seconds down
        Assert.Equal(194, _desk.GetOutput()[8]);     // 100 + 100 x 15/16 (193.75)
        _time.Pass(1);                               // the third step holds 100 too: the first one goes on down from there
        Assert.Equal(181, _desk.GetOutput()[8]);     // 100 + 100 x 13/16 (181.25)
        _time.Pass(10);
        Assert.Equal(100, _desk.GetOutput()[8]);
    }

    // ---------------------------------------------------------------- the stack

    [Fact]
    public void TheStackGoesFromMemoryToMemoryWithoutADip()
    {
        StackOf(new Step("memory", 1, 255, Up: 0, Wait: 0, Down: 4), new Step("memory", 2, 255, Up: 2, Wait: 0, Down: 2));
        AssertLevels(37, 210, 38, 100, 45, 255, 50, 88, 60, 0);

        _desk.StackPlay();
        Pass(1);
        AssertLevels(37, 195, 38, 150, 45, 255, 50, 66, 60, 40);
        Assert.Equal(191, _desk.GetState().MemoryLevels[0]);   // the faders move as they always did
        Assert.Equal(128, _desk.GetState().MemoryLevels[1]);

        Pass(1);
        AssertLevels(37, 180, 38, 200, 45, 255, 50, 44, 60, 80);
        Pass(2);
        AssertLevels(37, 150, 38, 200, 45, 255, 50, 0, 60, 80);
        Assert.Equal(0, _desk.GetState().MemoryLevels[0]);
        Assert.False(_desk.Animating);
    }

    [Fact]
    public void AChannelBothStepsLightStaysUpThroughTheWholeFade()
    {
        StackOf(new Step("memory", 1, 255, Up: 0, Wait: 0, Down: 3), new Step("memory", 2, 255, Up: 3, Wait: 0, Down: 0));
        _desk.StackPlay();
        for (int quarter = 0; quarter < 16; quarter++)
        {
            Pass(0.25);
            Assert.Equal(255, _desk.GetOutput()[44]);
        }
    }

    [Fact]
    public void AChannelStepAndAMemoryStepThatShareAChannelCrossfadeToo()
    {
        StackOf(new Step("channel", 45, 255, Up: 0, Wait: 0, Down: 4), new Step("memory", 2, 255, Up: 2, Wait: 0, Down: 0));
        _desk.StackPlay();
        Pass(1);

        Assert.Equal(255, _desk.GetOutput()[44]);
        Assert.Equal(191, _desk.GetState().Channels[44]);      // the channel fader a quarter of its way down
        Assert.Equal(75, _desk.GetOutput()[36]);               // memory 2 alone in 37: half way up
    }

    [Fact]
    public void AHandOnAFaderTakesItOutOfTheCrossfade()
    {
        StackOf(new Step("memory", 1, 255, Up: 0, Wait: 0, Down: 4), new Step("memory", 2, 255, Up: 2, Wait: 0, Down: 2));
        _desk.StackPlay();
        Pass(1);

        _desk.SetMemoryLevel(1, 0);                  // memory 1 pulled down by hand

        Assert.Equal(128, _desk.GetOutput()[44]);    // only memory 2 is left, half way up
        Assert.Equal(0, _desk.GetOutput()[49]);      // memory 1's own channel is gone with its fader
        Pass(1);
        Assert.Equal(255, _desk.GetOutput()[44]);
    }

    [Fact]
    public void PauseHoldsTheCrossfadeWhereItIs()
    {
        StackOf(new Step("memory", 1, 255, Up: 0, Wait: 0, Down: 4), new Step("memory", 2, 255, Up: 2, Wait: 0, Down: 2));
        _desk.StackPlay();
        Pass(1);
        _desk.StackPause();

        Pass(5);
        AssertLevels(37, 195, 38, 150, 45, 255);

        _desk.StackPlay();
        Pass(1);
        AssertLevels(37, 180, 38, 200, 45, 255);
    }

    [Fact]
    public void NextDuringAFadeFadesOnFromWhereTheLightIs()
    {
        StackOf(
            new Step("memory", 1, 255, Up: 0, Wait: 0, Down: 4),
            new Step("memory", 2, 255, Up: 2, Wait: 0, Down: 2),
            new Step("memory", 4, 255, Up: 2, Wait: 0, Down: 0));
        _desk.StackPlay();
        Pass(1);
        Assert.Equal(195, _desk.GetOutput()[36]);

        _desk.StackNext();                           // memory 4 comes before the fade from 1 to 2 is over
        Assert.Equal(195, _desk.GetOutput()[36]);    // no jump
        Assert.Equal(255, _desk.GetOutput()[44]);
        for (int quarter = 0; quarter < 16; quarter++)
        {
            Pass(0.25);
            Assert.Equal(255, _desk.GetOutput()[44]);
        }

        Assert.Equal(30, _desk.GetOutput()[36]);     // and in the end only memory 4
        Assert.Equal(0, _desk.GetOutput()[59]);
    }

    // ---------------------------------------------------------------- the owner's numbers: one channel at 120 and at 150

    [Fact]
    public void InTheStackFrom120To150TheChannelNeverGoesBelow120AndArrivesWithTheUpTime()
    {
        Look(6, (37, 120));
        Look(7, (37, 150));
        StackOf(new Step("memory", 6, 255, Up: 0, Wait: 0, Down: 2), new Step("memory", 7, 255, Up: 3, Wait: 0, Down: 2));

        _desk.StackPlay();
        int last = 120;
        for (int tenth = 1; tenth <= 30; tenth++)
        {
            Pass(0.1);
            int level = _desk.GetOutput()[36];
            Assert.InRange(level, last, 150);        // never lower than a moment before, never past 150
            last = level;
            if (tenth == 15)
                Assert.Equal(135, level);            // half way through the three seconds up of 150
        }

        Assert.Equal(150, last);
    }

    [Fact]
    public void InTheStackFrom150To120TheChannelGoesDownWithTheDownTimeAndStays()
    {
        Look(6, (37, 120));
        Look(7, (37, 150));
        StackOf(new Step("memory", 7, 255, Up: 0, Wait: 0, Down: 2), new Step("memory", 6, 255, Up: 3, Wait: 0, Down: 0));

        _desk.StackPlay();
        Pass(1);
        Assert.Equal(135, _desk.GetOutput()[36]);    // half of the two seconds down of the step that goes
        Pass(1);
        Assert.Equal(120, _desk.GetOutput()[36]);
        for (int tenth = 0; tenth < 30; tenth++)
        {
            Pass(0.1);
            Assert.Equal(120, _desk.GetOutput()[36]);
        }
    }

    [Fact]
    public void InASequenceTheChannelGoesStraightFrom120To150AndBackTo120()
    {
        Look(6, (37, 120));
        Look(7, (37, 150));
        _desk.SetSequence(3, loop: true,
        [
            new Step("memory", 6, 255, Up: 0, Wait: 2, Down: 2),
            new Step("memory", 7, 255, Up: 3, Wait: 2, Down: 2),
        ]);
        _desk.SetMemoryLevel(3, 255);

        _time.Pass(1);
        Assert.Equal(120, _desk.GetOutput()[36]);
        int last = 120;
        for (int tenth = 1; tenth <= 40; tenth++)    // the step of 150 starts at 2 s and is up at 5 s
        {
            _time.Pass(0.1);
            int level = _desk.GetOutput()[36];
            Assert.InRange(level, last, 150);
            last = level;
        }

        Assert.Equal(150, last);
        _time.Pass(2);                               // 7 s: round again, 120 comes, 150 goes in its two seconds down
        Assert.Equal(150, _desk.GetOutput()[36]);
        _time.Pass(1);
        Assert.Equal(135, _desk.GetOutput()[36]);
        _time.Pass(1);
        Assert.Equal(120, _desk.GetOutput()[36]);
        _time.Pass(0.5);                             // the step of 120 waited its 2 s: 150 comes again, from 120
        Assert.Equal(125, _desk.GetOutput()[36]);
    }

    [Fact]
    public void ThePlaybackGivesTheUisWhatTheCrossfadeGivesSoThatThePageShowsNoDipEither()
    {
        Look(6, (37, 120));
        Look(7, (37, 150));
        StackOf(new Step("memory", 6, 255, Up: 0, Wait: 0, Down: 2), new Step("memory", 7, 255, Up: 2, Wait: 0, Down: 2));
        Assert.Null(_desk.GetPlayback().Crossfade);

        _desk.StackPlay();
        Pass(1);
        StackCrossfade cross = _desk.GetPlayback().Crossfade!;
        Assert.Equal([6, 7], cross.Memories);
        Assert.Empty(cross.Channels);
        Assert.Equal(135, cross.Levels[36]);
        Assert.Equal(_desk.GetOutput()[36], cross.Levels[36]);
        Pass(1.5);
        Assert.Null(_desk.GetPlayback().Crossfade);   // over: the faders give the light on their own again
    }

    [Fact]
    public void StepsThatGoTogetherWithDownTimesOfTheirOwnEachFallInTheirOwnTime()
    {
        Look(6, (37, 120));
        Look(7, (38, 200));
        Look(8, (37, 60), (38, 50));
        StackOf(
            new Step("memory", 6, 255, Up: 0, Wait: 0, Down: 1),
            new Step("memory", 7, 255, Up: 0, Wait: 0, Down: 4) { Add = true },
            new Step("memory", 8, 255, Up: 2, Wait: 0, Down: 0));
        _desk.StackPlay();
        Pass(0.1);
        AssertLevels(37, 120, 38, 200);

        _desk.StackPlay();                           // both go, each in its own down time, down to the new step's levels
        Pass(0.5);
        AssertLevels(37, 90, 38, 181);               // 120 to 60 over 1 s; 200 to 50 over 4 s
        Pass(0.6);
        AssertLevels(37, 60, 38, 159);
        Pass(3);
        AssertLevels(37, 60, 38, 50);
    }

    // ---------------------------------------------------------------- helpers

    // Stores a look in a memory, with all faders down before and after.
    private void Look(int memory, params (int Channel, int Level)[] levels)
    {
        _desk.Clear();
        foreach ((int channel, int level) in levels)
            _desk.SetChannel(channel, level);

        _desk.SaveMemory(memory);
        _desk.Clear();
    }

    // A stack of these steps, live, with its first step up.
    private void StackOf(params Step[] steps)
    {
        foreach (Step step in steps)
            _desk.AddStackStep(step);

        _desk.SetStackLive(true);
        Pass(0.1);
    }

    private void Pass(double seconds)
    {
        _time.Pass(seconds);
        _desk.Tick();
    }

    // Pairs of channel number and the level the desk sends on it.
    private void AssertLevels(params int[] pairs)
    {
        byte[] output = _desk.GetOutput();
        for (int at = 0; at < pairs.Length; at += 2)
            Assert.True(pairs[at + 1] == output[pairs[at] - 1], $"channel {pairs[at]}: expected {pairs[at + 1]}, sent {output[pairs[at] - 1]}");
    }
}
