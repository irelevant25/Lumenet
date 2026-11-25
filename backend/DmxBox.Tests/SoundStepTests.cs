using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// Steps that play a sound, with a pretend engine and a clock the tests set by hand: in the stack
/// they move the sound's fader, with Add, releases and Continue (a sound that plays on through the
/// steps after it until it ends by itself or a release names it; Stack off stops it); going back
/// starts no other sound; a step of a sound that is over plays it again, one that still plays does
/// not. In a sequence a step plays the sound as loud as it is up, under the memory's fader, and leaves
/// the sound's fader alone; Continue keeps it through the steps and the rounds. And the file.
/// </summary>
public sealed class SoundStepTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;
    private readonly PretendSoundPlayer _player = new();

    public SoundStepTests()
    {
        _desk = new Desk(_time);
        _desk.UseSoundPlayer(_player);
        for (int number = 1; number <= 3; number++)
            _desk.SetSound(new Sound(number, $"Sound {number}", $"s{number}.wav", new string((char)('a' + number), 32)) { Seconds = 10 });
        _player.Calls.Clear();
    }

    private static Step SoundStep(int number, int value = 255, double up = 0, double wait = 0, double down = 0) =>
        new(Step.SoundTarget, number, value, up, wait, down);

    private static Step ChannelStep(int channel) => new(Step.Channel, channel, 255, 0, 0, 0);

    private SoundPlayback Playing(int number) => _desk.GetSoundPlayback(number);

    private int[] OnSteps() => _desk.GetPlayback().Stack.On;

    private int Starts(int number) => _player.Calls.Count(call => call == $"start {number}");

    [Fact]
    public void AStepOfASoundBringsItsFaderUpInItsUpTimeAndTheNextStepTakesItDown()
    {
        _desk.AddStackStep(SoundStep(1, up: 2, down: 2));
        _desk.AddStackStep(ChannelStep(5));
        _desk.SetStackLive(true);

        _time.Pass(1);
        _desk.Tick();
        Assert.Equal(["start 1"], _player.Calls);
        Assert.Equal((true, 128), (Playing(1).Playing, Playing(1).Level));   // half way up: its fader moves
        Assert.Equal(128 / 255.0, _player.Levels[0], 2);

        _time.Pass(1);
        _desk.Tick();
        Assert.Equal(255, Playing(1).Level);

        _desk.StackPlay();   // the next step: down in its down time, and stopped at zero
        _time.Pass(1);
        _desk.Tick();
        Assert.InRange(Playing(1).Level, 127, 128);
        _time.Pass(1);
        _desk.Tick();
        Assert.Equal(["start 1", "stop 1"], _player.Calls);
        Assert.False(Playing(1).Playing);
    }

    [Fact]
    public void PrevAndNextGoAtOnceAndMusicPlayedByMistakeStopsAtOnce()
    {
        _desk.AddStackStep(ChannelStep(5));
        _desk.AddStackStep(SoundStep(1, up: 3, down: 4));   // music, slow in and out
        _desk.SetStackLive(true);
        _desk.Tick();
        _desk.StackPlay();                                   // pressed by mistake: the music comes up
        _time.Pass(1);
        _desk.Tick();
        Assert.True(Playing(1).Playing);

        _desk.StackPrevious();                               // back at once: the music stops at once
        Assert.Equal(["start 1", "stop 1"], _player.Calls);
        Assert.Equal((false, 0), (Playing(1).Playing, Playing(1).Level));
        Assert.Equal(255, _desk.GetOutput()[4]);

        _desk.StackNext();                                   // on again at once: no up time
        Assert.Equal((true, 255), (Playing(1).Playing, Playing(1).Level));
        Assert.Equal(0, _desk.GetOutput()[4]);
        Assert.False(_desk.GetPlayback().Stack.State == "fading");
    }

    [Fact]
    public void PrevStaysOnAStepThatWouldGoOnByItselfSoTheMusicDoesNotComeBack()
    {
        _desk.AddStackStep(ChannelStep(5) with { Wait = 0.5 });   // goes on by itself into the music
        _desk.AddStackStep(SoundStep(1, up: 3, down: 4));
        _desk.SetStackLive(true);
        _time.Pass(1);
        _desk.Tick();
        Assert.True(Playing(1).Playing);

        _desk.StackPrevious();
        _time.Pass(2);
        _desk.Tick();
        _time.Pass(2);
        _desk.Tick();
        Assert.False(Playing(1).Playing);                       // it stays on the step: no music again
        Assert.Equal("holding", _desk.GetPlayback().Stack.State);

        _desk.StackPlay();                                      // Play goes on, as the show does
        _time.Pass(1);
        _desk.Tick();
        Assert.True(Playing(1).Playing);
    }

    [Fact]
    public void ASoundThatPlaysOnStaysThroughTheStepsAfterItUntilAReleaseNamesIt()
    {
        int rain = _desk.AddStackStep(SoundStep(1) with { Continue = true });
        _desk.AddStackStep(ChannelStep(5));
        _desk.AddStackStep(ChannelStep(6));
        _desk.AddStackStep(new Step(Step.Release, rain, 0, 0, 0, Down: 2));
        _desk.SetStackLive(true);
        _desk.Tick();
        Assert.True(Playing(1).Playing);

        _desk.StackNext();
        _desk.StackNext();   // two steps that take everything down: it plays on
        _desk.Tick();
        Assert.Equal((true, 255), (Playing(1).Playing, Playing(1).Level));
        Assert.Contains(rain, OnSteps());
        Assert.Equal(255, _desk.GetOutput()[5]);
        Assert.Equal(0, _desk.GetOutput()[4]);

        _desk.StackPlay();   // the release: down in its two seconds
        _time.Pass(1);
        _desk.Tick();
        Assert.InRange(Playing(1).Level, 127, 128);
        _time.Pass(1);
        _desk.Tick();
        Assert.False(Playing(1).Playing);
        Assert.Equal(1, Starts(1));
    }

    [Fact]
    public void ASoundThatPlaysOnIsOverWhenItEndsAndAStepWithAddGoesWithTheNextStepWithout()
    {
        int thunder = _desk.AddStackStep(SoundStep(1) with { Continue = true });
        int bell = _desk.AddStackStep(SoundStep(2) with { Add = true });
        _desk.AddStackStep(ChannelStep(5));
        _desk.SetStackLive(true);
        _desk.StackNext();
        _desk.Tick();
        Assert.Equal([thunder, bell], OnSteps());

        _player.End(1);      // the thunder has played to its end: its step is over, its fader at zero
        _desk.Tick();
        _desk.Tick();
        Assert.Equal([bell], OnSteps());
        Assert.Equal((false, 0), (Playing(1).Playing, Playing(1).Level));

        _desk.StackNext();   // a step without Add takes the bell down, as any step that adds
        _desk.Tick();
        Assert.False(Playing(2).Playing);
        Assert.Equal(1, Starts(1));
    }

    [Fact]
    public void StackOffStopsTheSoundsThatPlayOnAndLeavesTheOtherFaders()
    {
        _desk.AddStackStep(SoundStep(1) with { Continue = true });
        _desk.AddStackStep(SoundStep(2) with { Add = true });
        _desk.SetStackLive(true);
        _desk.StackNext();
        _desk.Tick();

        _desk.SetStackLive(false);
        _desk.Tick();
        Assert.Equal((false, 0), (Playing(1).Playing, Playing(1).Level));
        Assert.Equal((true, 255), (Playing(2).Playing, Playing(2).Level));   // faders stay, as the lights' do
    }

    [Fact]
    public void AStepOfASoundThatPlaysDoesNotStartItAgainButOneThatIsOverDoes()
    {
        _desk.AddStackStep(SoundStep(1) with { Continue = true });
        _desk.AddStackStep(SoundStep(1, value: 200) with { Add = true });
        _desk.AddStackStep(SoundStep(1, value: 200) with { Add = true });
        _desk.SetStackLive(true);
        _desk.Tick();
        _desk.StackNext();   // it plays: the later step only sets its level
        _desk.Tick();
        Assert.Equal((1, 200), (Starts(1), Playing(1).Level));

        _player.End(1);
        _desk.Tick();
        _desk.Tick();
        Assert.Equal(200, Playing(1).Level);   // over, while a step still holds its fader up

        _desk.StackNext();   // the same level: a step of it plays the sound again
        _desk.Tick();
        Assert.Equal(2, Starts(1));
        Assert.True(Playing(1).Playing);
    }

    [Fact]
    public void GoingBackStartsNoSoundButThatOfTheStepGoneTo()
    {
        _desk.AddStackStep(SoundStep(1) with { Continue = true });
        _desk.AddStackStep(ChannelStep(5));
        int third = _desk.AddStackStep(SoundStep(2) with { Add = true });
        _desk.SetStackLive(true);
        _desk.StackNext();
        _desk.StackNext();
        _desk.Tick();
        _player.End(1);
        _player.End(2);
        _desk.Tick();

        _desk.StackPrevious();   // step 2: the thunder of step 1 is over and does not start again
        _desk.Tick();
        Assert.Equal((1, 1), (Starts(1), Starts(2)));

        _desk.StackNext();       // step 3 itself plays its sound again
        _desk.Tick();
        Assert.Equal(2, Starts(2));
        Assert.Contains(third, OnSteps());
    }

    [Fact]
    public void ASequencePlaysASoundAsLoudAsItsStepAndLeavesItsFaderAlone()
    {
        _desk.SetSequence(3, loop: false, [SoundStep(1, up: 2)]);
        _desk.SetMemoryLevel(3, 255);
        _desk.Tick();
        Assert.Empty(_player.Calls);   // its step is only beginning to come up

        _time.Pass(1);
        _desk.Tick();
        Assert.Equal(["start 1"], _player.Calls);
        Assert.Equal((0, 128), (Playing(1).Level, Playing(1).Played));   // no fader moves for it
        Assert.Equal(128 / 255.0, _player.Levels[0], 2);

        _time.Pass(1);
        _desk.SetMemoryLevel(3, 128);   // the fader of the memory scales it
        _desk.Tick();
        Assert.Equal(128 / 255.0, _player.Levels[0], 2);

        _desk.SetMemoryLevel(3, 0);     // and stops it with the sequence
        _desk.Tick();
        Assert.Equal(["start 1", "stop 1"], _player.Calls);
    }

    [Fact]
    public void InASequenceASoundThatPlaysOnStaysThroughTheStepsAndTheRoundsUntilAReleaseNamesIt()
    {
        _desk.SetSequence(3, loop: true, [
            SoundStep(1, wait: 1) with { Continue = true },
            ChannelStep(5) with { Wait = 1 },
            ChannelStep(6) with { Wait = 1 },
        ]);
        _desk.SetMemoryLevel(3, 255);
        for (int beat = 0; beat < 7; beat++)   // two rounds and more: it never goes down
        {
            _desk.Tick();
            Assert.Equal(255, Playing(1).Played);
            _time.Pass(1);
        }

        Assert.Equal(1, Starts(1));   // and it was never started again: it plays on

        _desk.SetSequence(4, loop: false, [
            SoundStep(2) with { Continue = true },
            ChannelStep(5) with { Wait = 1 },
            new Step(Step.Release, 1, 0, 0, Wait: 1, Down: 2),
        ]);
        _desk.SetMemoryLevel(4, 255);
        _desk.Tick();
        _time.Pass(1);
        _desk.Tick();
        Assert.Equal(255, Playing(2).Played);   // through the step without Add
        _time.Pass(1.05);                     // the release (from 1.05 s), half way in its two seconds
        _desk.Tick();
        Assert.InRange(Playing(2).Played, 126, 130);
        _time.Pass(1);
        _desk.Tick();
        Assert.False(Playing(2).Playing);
    }

    [Fact]
    public void ANewRoundPlaysASoundThatIsOverAgain()
    {
        _desk.SetSequence(3, loop: true, [SoundStep(1, wait: 2) with { Continue = true }, ChannelStep(5) with { Wait = 2 }]);
        _desk.SetMemoryLevel(3, 255);
        _desk.Tick();
        _time.Pass(1);
        _player.End(1);
        _desk.Tick();
        _desk.Tick();
        Assert.False(Playing(1).Playing);   // over in its first round, its step still on

        _time.Pass(3.5);                  // the next round: its step plays it again
        _desk.Tick();
        _desk.Tick();
        Assert.Equal(2, Starts(1));
        Assert.True(Playing(1).Playing);
    }

    [Fact]
    public void AStepOfASoundIsCheckedAndOnlyASoundPlaysOn()
    {
        Assert.Throws<DeskException>(() => _desk.AddStackStep(SoundStep(65)));
        Assert.Throws<DeskException>(() => _desk.SetSequence(3, true, [SoundStep(0)]));

        int id = _desk.AddStackStep(ChannelStep(5) with { Continue = true });
        Assert.False(_desk.GetStack().Steps.Single(step => step.Id == id).Continue);
        int sound = _desk.AddStackStep(SoundStep(3) with { Continue = true, Add = true });
        Assert.True(_desk.GetStack().Steps.Single(step => step.Id == sound).Continue);
    }

    [Fact]
    public void AProjectWithStepsOfSoundsIsVersion9AndKeepsContinue()
    {
        _desk.AddStackStep(SoundStep(2, up: 1.5) with { Continue = true });
        _desk.SetSequence(3, loop: true, [SoundStep(1) with { Add = true }]);
        foreach (int number in new[] { 1, 2, 3 })
            _desk.DeleteSound(number);   // steps alone make it version 9 too

        using var file = new MemoryStream();
        ProjectFormat.Write(file, _desk.GetProject().Content);
        string text = System.Text.Encoding.UTF8.GetString(file.ToArray());
        Assert.Contains("\"version\": 9", text, StringComparison.Ordinal);
        Assert.Contains("\"continue\": true", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"sounds\"", text, StringComparison.Ordinal);

        file.Position = 0;
        ProjectContent read = ProjectFormat.Read(file);
        Step step = Assert.Single(read.Stack).Step;
        Assert.Equal((Step.SoundTarget, 2, true, 1.5), (step.Target, step.Number, step.Continue, step.Up));
        Assert.Equal(Step.SoundTarget, read.Sequences[2]!.Steps[0].Target);
        Assert.False(read.Sequences[2]!.Steps[0].Continue);
    }
}
