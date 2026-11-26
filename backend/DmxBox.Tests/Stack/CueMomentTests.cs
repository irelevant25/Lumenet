namespace DmxBox.Tests.Stack;

/// <summary>
/// Cues of the stack that come at moments of a sound (<see cref="Step.At"/>): the stack follows the
/// sound that plays, a jump in it takes the stack to the cue of that moment, GO still goes earlier,
/// and Learn stores the moments with GO in a rehearsal.
/// </summary>
public sealed class CueMomentTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;
    private readonly PretendSoundPlayer _player = new();

    public CueMomentTests()
    {
        _desk = new Desk(_time);
        _desk.UseSoundPlayer(_player);
        _desk.SetSound(new Sound(1, "Song", "song.wav", new string('a', 32)) { Seconds = 120 });
        _desk.SetSound(new Sound(2, "Rain", "rain.wav", new string('b', 32)) { Seconds = 60 });
        _player.Calls.Clear();
    }

    private static Step Channel(int channel) => new(Step.Channel, channel, 255, 0, 0, 0);

    private static Step Song(double wait = 0) => new(Step.SoundTarget, 1, 255, 0, wait, 0) { Continue = true };

    // The song starts with the first cue; the next three come at 10, 20 and 30 seconds of it.
    private int[] Show()
    {
        int song = _desk.AddStackStep(Song());
        int a = _desk.AddStackStep(Channel(1) with { At = new SoundTime(1, 10) });
        int b = _desk.AddStackStep(Channel(2) with { At = new SoundTime(1, 20) });
        int c = _desk.AddStackStep(Channel(3) with { At = new SoundTime(1, 30) });
        return [song, a, b, c];
    }

    private int? Current => _desk.GetPlayback().Stack.CurrentId;

    private int Level(int channel) => _desk.GetState().Channels[channel - 1];

    private StackStep CueOf(int id) => _desk.GetStack().Steps.Single(step => step.Id == id);

    // The song is at "seconds", and a moment passes for the desk.
    private void SongAt(double seconds)
    {
        _player.Position[0] = seconds;
        _time.Pass(0.05);
        _desk.Tick();
    }

    [Fact]
    public void TheStackGoesToACueWhenItsSoundReachesItsMoment()
    {
        int[] cues = Show();
        _desk.SetStackLive(true);   // the first cue: the song starts
        SongAt(0);
        Assert.Equal(["start 1"], _player.Calls);
        Assert.Equal(cues[0], Current);

        SongAt(9.9);
        Assert.Equal(cues[0], Current);
        SongAt(10);
        Assert.Equal(cues[1], Current);
        Assert.Equal(255, Level(1));
        SongAt(20.05);
        Assert.Equal(cues[2], Current);
        Assert.Equal((0, 255), (Level(1), Level(2)));   // as GO would: the cue before goes down
    }

    [Fact]
    public void GoStillGoesEarlierAndThenTheNextMomentIsWaitedFor()
    {
        int[] cues = Show();
        _desk.SetStackLive(true);
        SongAt(2);
        _desk.StackPlay();   // before the moment of the second cue
        Assert.Equal(cues[1], Current);
        SongAt(2.5);         // the song plays on, before that moment: the cue stays (the song went nowhere back)
        SongAt(6);
        Assert.Equal(cues[1], Current);

        SongAt(12);          // past the moment of the cue that is on: the third waits for its own
        Assert.Equal(cues[1], Current);
        SongAt(20);
        Assert.Equal(cues[2], Current);

        _desk.StackNext();   // Next as well: two cues ahead of the song, it stays there
        SongAt(20.5);
        Assert.Equal(cues[3], Current);
    }

    [Fact]
    public void ASoundSentBackFromACueThatFollowsAMomentGoesBackToo()
    {
        int song = _desk.AddStackStep(Song());
        int verse = _desk.AddStackStep(Channel(1) with { At = new SoundTime(1, 10) });
        int chorus = _desk.AddStackStep(Channel(2) with { At = new SoundTime(1, 20), Wait = 0.1 });
        int chase = _desk.AddStackStep(Channel(3) with { Add = true });   // comes after the chorus by itself
        _desk.SetStackLive(true);
        SongAt(1);
        SongAt(20);
        _time.Pass(0.2);     // the wait of the chorus
        _desk.Tick();
        Assert.Equal(chase, Current);

        SongAt(22);          // a little back within the chorus: the stack stays where it is
        SongAt(21.5);
        Assert.Equal(chase, Current);

        SongAt(12);          // back to the verse
        Assert.Equal(verse, Current);
        Assert.Equal((255, 0, 0), (Level(1), Level(2), Level(3)));

        SongAt(2);           // back before the first moment: the cue the moments follow
        Assert.Equal(song, Current);
    }

    [Fact]
    public void AJumpInTheSoundTakesTheStackToTheCueOfThatMoment()
    {
        int[] cues = Show();
        _desk.SetStackLive(true);
        SongAt(1);

        SongAt(25);   // on over two moments: straight to the last of them, at once
        Assert.Equal(cues[2], Current);
        Assert.Equal((0, 255), (Level(1), Level(2)));

        SongAt(15);   // back: to the cue of that moment
        Assert.Equal(cues[1], Current);
        Assert.Equal((255, 0), (Level(1), Level(2)));

        SongAt(3);    // back before the first moment: to the cue the moments follow
        Assert.Equal(cues[0], Current);
        Assert.Equal(0, Level(1));

        SongAt(10);   // and on again from there
        Assert.Equal(cues[1], Current);
    }

    [Fact]
    public void AWaitDoesNotTakeTheStackOnToACueThatComesAtAMoment()
    {
        int song = _desk.AddStackStep(Song(wait: 5));
        int timed = _desk.AddStackStep(Channel(1) with { At = new SoundTime(1, 30) });
        _desk.SetStackLive(true);
        SongAt(0);

        _player.Position[0] = 6;
        _time.Pass(6);       // the wait is over, the moment is not
        _desk.Tick();
        Assert.Equal(song, Current);

        SongAt(30);
        Assert.Equal(timed, Current);
    }

    [Fact]
    public void WhileItsSoundIsSilentACueOfAMomentWaitsForGo()
    {
        int first = _desk.AddStackStep(Channel(5));
        int timed = _desk.AddStackStep(Channel(1) with { At = new SoundTime(1, 0) });
        _desk.SetStackLive(true);
        _time.Pass(1);
        _desk.Tick();
        Assert.Equal(first, Current);   // sound 1 does not play

        _desk.StackPlay();
        Assert.Equal(timed, Current);
    }

    [Fact]
    public void LearnStoresTheMomentOfTheSoundThatPlaysIntoTheCueGoGoesTo()
    {
        _desk.AddStackStep(Song());
        int a = _desk.AddStackStep(Channel(1));
        int b = _desk.AddStackStep(Channel(2));
        _desk.SetStackLive(true);
        Assert.True(_desk.SetStackLearning(true));
        Assert.True(_desk.GetPlayback().Stack.Learning);

        SongAt(12.5);
        _desk.StackPlay();
        Assert.Equal(new SoundTime(1, 12.5), CueOf(a).At);
        Assert.Equal(a, Current);

        // a sound started later is the one GO learns from, also in the same moment as the song
        _desk.SetSoundControl(2, "level", 100);   // its fader up by hand
        _player.Position[1] = 3.25;
        SongAt(14);
        Assert.True(_desk.GetSoundPlayback(1).Playing && _desk.GetSoundPlayback(2).Playing);
        _desk.StackPlay();
        Assert.Equal(new SoundTime(2, 3.25), CueOf(b).At);

        _desk.SetStackLive(false);   // the stack going off ends Learn
        Assert.False(_desk.GetPlayback().Stack.Learning);
    }

    [Fact]
    public void AMomentIsSetAlsoWhileTheStackIsLiveButNotOnAStepOfASequence()
    {
        int a = _desk.AddStackStep(Channel(1));
        _desk.SetStackLive(true);
        Assert.True(_desk.SetStackStepAt(a, new SoundTime(2, 3.456)));
        Assert.Equal(new SoundTime(2, 3.46), CueOf(a).At);   // in hundredths of a second
        Assert.False(_desk.SetStackStepAt(a, new SoundTime(2, 3.46)));
        Assert.Throws<DeskException>(() => _desk.SetStackStepAt(a, new SoundTime(65, 1)));
        Assert.Throws<DeskException>(() => _desk.SetStackStepAt(a, new SoundTime(1, -1)));
        Assert.True(_desk.SetStackStepAt(a, null));
        Assert.Null(CueOf(a).At);

        _desk.SetStackLive(false);
        var error = Assert.Throws<DeskException>(
            () => _desk.SetSequence(3, new Sequence(false, [Channel(1) with { At = new SoundTime(1, 1) }])));
        Assert.Contains("only a cue of the stack", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AProjectWithMomentsIsAFileOfVersion10AndBringsThemBack()
    {
        int[] cues = Show();
        string text = ProjectFormat.WriteText(_desk.GetProject().Content);
        Assert.Contains("\"version\": 10", text, StringComparison.Ordinal);
        Assert.Contains("\"at\": {", text, StringComparison.Ordinal);

        ProjectContent read = ProjectFormat.ReadText(text);
        Assert.Equal([null, new SoundTime(1, 10), new SoundTime(1, 20), new SoundTime(1, 30)], read.Stack.Select(step => step.Step.At));

        // without a moment the version stays as it was
        foreach (int cue in cues)
            _desk.SetStackStepAt(cue, null);
        Assert.Contains("\"version\": 9", ProjectFormat.WriteText(_desk.GetProject().Content), StringComparison.Ordinal);
    }
}
