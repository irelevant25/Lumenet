namespace DmxBox.Tests.Sounds;

/// <summary>
/// Pause of the stack and its sounds, muted cues of sounds, and how a sound sounds changed while the
/// stack is live, with a pretend engine and a clock the tests set by hand: Pause holds the sounds of the
/// cues that are on (also while the stack only waits for GO) and GO lets them go on, while sounds of the
/// hand play on; a muted cue plays its sound unheard, on its way down too, a later cue of the same sound
/// aloud, the mute switches while the stack is live, and Stack off stops the sound of a muted cue; a step
/// of a sequence cannot be muted; the file.
/// </summary>
public sealed class SoundPauseMuteTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;
    private readonly PretendSoundPlayer _player = new();

    public SoundPauseMuteTests()
    {
        _desk = new Desk(_time);
        _desk.UseSoundPlayer(_player);
        for (int number = 1; number <= 2; number++)
            _desk.SetSound(new Sound(number, $"Sound {number}", $"s{number}.wav", new string((char)('a' + number), 32)) { Seconds = 10 });
        _player.Calls.Clear();
    }

    private static Step SoundStep(int number, int value = 255, double up = 0, double wait = 0, double down = 0) =>
        new(Step.SoundTarget, number, value, up, wait, down);

    private static Step ChannelStep(int channel, double down = 0) => new(Step.Channel, channel, 255, 0, 0, down);

    private SoundPlayback Playing(int number) => _desk.GetSoundPlayback(number);

    private StackPlayback Stack => _desk.GetPlayback().Stack;

    [Fact]
    public void PauseHoldsTheSoundsOfTheStackWhileItWaitsForGoAndGoLetsThemGoOn()
    {
        int first = _desk.AddStackStep(SoundStep(1));
        _desk.AddStackStep(ChannelStep(5));
        _desk.SetStackLive(true);
        _desk.Tick();
        _desk.SetSoundControl(2, "go", 1);   // a sound of the hand
        _desk.Tick();
        Assert.Equal("holding", Stack.State);
        Assert.Equal(["start 1", "start 2"], _player.Calls);

        _desk.StackPause();
        Assert.Equal(["start 1", "start 2", "pause 1"], _player.Calls);   // the hand's sound plays on
        Assert.True(Stack.Paused);
        Assert.Equal((true, true), (Playing(1).Playing, Playing(1).Paused));
        Assert.False(Playing(2).Paused);

        _desk.StackPause();   // once is enough
        Assert.Single(_player.Calls, call => call == "pause 1");

        _desk.StackPlay();    // GO goes on where it was, on the same cue
        Assert.Equal("resume 1", _player.Calls[^1]);
        Assert.Equal((false, first, false), (Stack.Paused, Stack.CurrentId, Playing(1).Paused));

        _desk.StackPlay();    // and then on to the next cue
        Assert.NotEqual(first, Stack.CurrentId);
    }

    [Fact]
    public void PauseHoldsAFadeAndTheSoundThatComesUpWithItTogether()
    {
        _desk.AddStackStep(SoundStep(1, up: 2));
        _desk.SetStackLive(true);
        _time.Pass(1);
        _desk.Tick();
        Assert.Equal(128, Playing(1).Level);

        _desk.StackPause();
        _time.Pass(5);
        _desk.Tick();
        Assert.Equal((128, true, "pause 1"), (Playing(1).Level, Playing(1).Paused, _player.Calls[^1]));

        _desk.StackPlay();
        _time.Pass(1);
        _desk.Tick();
        Assert.Equal((255, "resume 1"), (Playing(1).Level, _player.Calls[^1]));
    }

    [Fact]
    public void NextAndStackOffLetAPausedSoundGoOn()
    {
        _desk.AddStackStep(SoundStep(1) with { Continue = true });
        _desk.AddStackStep(ChannelStep(5) with { Add = true });
        _desk.SetStackLive(true);
        _desk.Tick();
        _desk.StackPause();
        _desk.StackNext();   // on at once: the sound that plays on goes on
        Assert.Equal(["start 1", "pause 1", "resume 1"], _player.Calls);

        _desk.StackPause();
        _desk.SetStackLive(false);   // a sound that plays on stops with the stack; nothing stays paused
        _desk.Tick();
        Assert.Equal(["start 1", "pause 1", "resume 1", "pause 1", "resume 1", "stop 1"], _player.Calls);
        Assert.False(Playing(1).Paused);
    }

    [Fact]
    public void AMutedCuePlaysItsSoundUnheardAndALaterCueOfItAloud()
    {
        _desk.AddStackStep(SoundStep(1) with { Muted = true });
        _desk.AddStackStep(SoundStep(1, value: 204));
        _desk.SetStackLive(true);
        _desk.Tick();
        Assert.Equal(["start 1"], _player.Calls);
        Assert.Equal((true, 255, true), (Playing(1).Playing, Playing(1).Level, Playing(1).Muted));   // it runs, its fader up
        Assert.Equal(0, _player.Levels[0]);                                                           // but nothing goes out

        _desk.StackPlay();
        _desk.Tick();
        Assert.Equal((204, false), (Playing(1).Level, Playing(1).Muted));
        Assert.Equal(0.8, _player.Levels[0], 3);
        Assert.Equal(["start 1"], _player.Calls);   // the same run of the sound, heard now
    }

    [Fact]
    public void AMutedCueGoesDownUnheardAndTheMuteSwitchesWhileTheStackIsLive()
    {
        int sound = _desk.AddStackStep(SoundStep(1, down: 2) with { Muted = true });
        int light = _desk.AddStackStep(ChannelStep(5));
        _desk.SetStackLive(true);
        _desk.Tick();
        _desk.StackPlay();   // the cue of the sound goes, its sound down in its down time
        _time.Pass(1);
        _desk.Tick();
        Assert.Equal((true, 0.0), (Playing(1).Playing, _player.Levels[0]));   // still unheard on its way down
        _time.Pass(1.5);
        _desk.Tick();
        Assert.False(Playing(1).Playing);

        // back to the cue of the sound, then heard again with a press: also while the stack is live
        _desk.StackPrevious();
        _desk.Tick();
        Assert.Equal(0, _player.Levels[0]);
        Assert.True(_desk.SetStackStepMuted(sound, false));
        _desk.Tick();
        Assert.Equal((1.0, false), (_player.Levels[0], Playing(1).Muted));
        Assert.True(_desk.SetStackStepMuted(sound, true));
        Assert.False(_desk.SetStackStepMuted(sound, true));   // no change
        _desk.Tick();
        Assert.Equal(0, _player.Levels[0]);
        Assert.True(_desk.GetStack().Steps.Single(step => step.Id == sound).Muted);

        DeskException notSound = Assert.Throws<DeskException>(() => _desk.SetStackStepMuted(light, true));
        Assert.Contains("only a cue of a sound can be muted", notSound.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StackOffStopsTheSoundOfAMutedCueAndTheOthersPlayOn()
    {
        _desk.AddStackStep(SoundStep(1) with { Muted = true });
        _desk.AddStackStep(SoundStep(2) with { Add = true });
        _desk.SetStackLive(true);
        _desk.Tick();
        _desk.StackPlay();
        _desk.Tick();
        Assert.Equal(["start 1", "start 2"], _player.Calls);

        _desk.SetStackLive(false);   // nobody hears the muted one: it stops; the other one's fader stays up
        _desk.Tick();
        Assert.Equal(["start 1", "start 2", "stop 1"], _player.Calls);
        Assert.Equal((false, false), (Playing(1).Playing, Playing(1).Muted));
        Assert.Equal((true, 255), (Playing(2).Playing, Playing(2).Level));
    }

    [Fact]
    public void StackOffStopsTheSoundOfAMutedCueOnItsWayDownToo()
    {
        _desk.AddStackStep(SoundStep(1, down: 4) with { Muted = true });
        _desk.AddStackStep(ChannelStep(5));
        _desk.SetStackLive(true);
        _desk.Tick();
        _desk.StackPlay();   // its cue goes: the sound goes down in 4 seconds, unheard
        _time.Pass(1);
        _desk.Tick();
        Assert.True(Playing(1).Playing);

        _desk.SetStackLive(false);
        _desk.Tick();
        Assert.Equal((false, "stop 1"), (Playing(1).Playing, _player.Calls[^1]));
    }

    [Fact]
    public void AStepOfASequenceCannotBeMutedAndTheFileKeepsMutedCues()
    {
        DeskException error = Assert.Throws<DeskException>(
            () => _desk.SetSequence(3, new Sequence(false, [SoundStep(1) with { Muted = true }])));
        Assert.Contains("only a cue of the stack", error.Message, StringComparison.Ordinal);
        Assert.False((ChannelStep(1) with { Muted = true }).Checked().Muted);   // only a sound is muted

        // a muted cue makes the project a file of version 11; without one it stays as it was
        int cue = _desk.AddStackStep(SoundStep(1) with { Muted = true });
        string text = ProjectFormat.WriteText(_desk.GetProject().Content);
        Assert.Contains("\"version\": 11", text, StringComparison.Ordinal);
        Assert.Contains("\"muted\": true", text, StringComparison.Ordinal);
        Assert.True(ProjectFormat.ReadText(text).Stack.Single().Step.Muted);

        _desk.SetStackStepMuted(cue, false);
        Assert.Contains("\"version\": 9", ProjectFormat.WriteText(_desk.GetProject().Content), StringComparison.Ordinal);
    }

    [Fact]
    public void HowASoundSoundsChangesWhileTheStackIsLiveAndNothingElseOfIt()
    {
        _desk.AddStackStep(SoundStep(1));
        _desk.SetStackLive(true);
        _desk.Tick();
        Sound sound = _desk.GetSounds()[0];

        Sound changed = _desk.SetSound(sound with { Decibels = -3, Pan = 20, Speed = 1.25, Pitch = 2, Echo = 30, Reverb = 40 });
        Assert.Equal((-3.0, 20, 1.25, 2, 30, 40), (changed.Decibels, changed.Pan, changed.Speed, changed.Pitch, changed.Echo, changed.Reverb));
        Assert.Equal("change 1", _player.Calls[^1]);   // the sound that plays goes on with it

        // the repeats of the echo and the room of the reverb too
        changed = _desk.SetSound(changed with { EchoTime = 0.5, EchoRepeats = 60, Room = 80 });
        Assert.Equal((0.5, 60, 80), (changed.EchoTime, changed.EchoRepeats, changed.Room));

        foreach (Sound other in new[] { changed with { Name = "Rain" }, changed with { FadeIn = 2 }, changed with { Loop = true }, changed with { Start = 1 } })
        {
            DeskException refused = Assert.Throws<DeskException>(() => _desk.SetSound(other));
            Assert.Contains("The stack is live", refused.Message, StringComparison.Ordinal);
        }

        Assert.Throws<DeskException>(() => _desk.SetSound(new Sound(9, "New", "n.wav", new string('f', 32))));   // nor a new sound
        Assert.Equal(changed, _desk.GetSounds()[0]);
    }
}
