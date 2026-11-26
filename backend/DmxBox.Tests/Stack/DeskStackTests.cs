namespace DmxBox.Tests.Stack;

/// <summary>
/// The stack: steps that are gone through with Play, Pause, Next and Previous while the stack
/// is live, and that move the real faders of their channels and memories.
/// </summary>
public sealed class DeskStackTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;

    public DeskStackTests()
    {
        _desk = new Desk(_time);
    }

    [Fact]
    public void NewStepsGetWholeNumbersInTheOrderTheyAreAdded()
    {
        int first = _desk.AddStackStep(new Step("channel", 1, 200, 2, 0, 1));
        int second = _desk.AddStackStep(new Step("memory", 5, 255, 1.26, 0, 1));

        StackStep[] steps = _desk.GetStack().Steps;
        Assert.Equal(["1.0", "2.0"], steps.Select(step => step.Cue));
        Assert.Equal([first, second], steps.Select(step => step.Id));
        Assert.NotEqual(first, second);
        Assert.Equal(new StackStep(second, "2.0", "memory", 5, 255, 1.3, 0, 1), steps[1]);
        Assert.Throws<DeskException>(() => _desk.AddStackStep(new Step("channel", 600, 1, 0, 0, 0)));
    }

    [Fact]
    public void GoingLiveBringsEveryFaderToZeroAndGoesToTheFirstStep()
    {
        int first = _desk.AddStackStep(new Step("channel", 1, 200, Up: 2, Wait: 0, Down: 1));
        int second = _desk.AddStackStep(new Step("channel", 2, 255, Up: 0, Wait: 0, Down: 0));
        _desk.SetChannel(9, 100);
        _desk.SetMemoryLevel(3, 50);
        _desk.SetMaster(180);
        AssertStack("idle", current: null, next: first);
        Assert.False(_desk.GetPlayback().Stack.Live);

        _desk.SetStackLive(true);

        Assert.True(_desk.GetPlayback().Stack.Live);
        DeskState desk = _desk.GetState();
        Assert.Equal(0, desk.Channels[8]);
        Assert.Equal(0, desk.MemoryLevels[2]);
        Assert.Equal(180, desk.Master);              // the master belongs to the evening, not to the show
        AssertStack("fading", current: first, next: second, remaining: 2);
        Assert.True(_desk.Animating);

        Pass(1);
        Assert.Equal(100, _desk.GetState().Channels[0]);   // the real fader, not a layer
        AssertStack("fading", current: first, next: second, remaining: 1);

        Pass(1);
        Assert.Equal(200, _desk.GetState().Channels[0]);
        Assert.Equal(141, _desk.GetOutput()[0]);     // 200 under a master at 180
        AssertStack("holding", current: first, next: second);
        Assert.False(_desk.Animating);
    }

    [Fact]
    public void StackWithoutStepsCannotGoLive()
    {
        var error = Assert.Throws<DeskException>(() => _desk.SetStackLive(true));

        Assert.Equal("The stack has no steps yet. Add a step first.", error.Message);
        Assert.False(_desk.GetPlayback().Stack.Live);
    }

    [Fact]
    public void TheKeysDoNothingUnlessTheStackIsLive()
    {
        int step = _desk.AddStackStep(new Step("channel", 1, 200, Up: 0, Wait: 0, Down: 0));

        foreach (Action key in new Action[] { _desk.StackPlay, _desk.StackPause, _desk.StackNext, _desk.StackPrevious })
        {
            var error = Assert.Throws<DeskException>(key);
            Assert.Equal("The stack is not live. Press Stack first.", error.Message);
        }

        Pass(1);
        AssertStack("idle", current: null, next: step);
        Assert.Equal(0, _desk.GetState().Channels[0]);
    }

    [Fact]
    public void SwitchingTheStackOffStopsItAndLeavesTheFadersWhereTheyAre()
    {
        int step = _desk.AddStackStep(new Step("channel", 1, 200, Up: 2, Wait: 0, Down: 1));
        _desk.SetStackLive(true);
        _time.Pass(1);                               // half way up, and no tick has noticed it yet

        _desk.SetStackLive(false);

        Assert.False(_desk.GetPlayback().Stack.Live);
        Assert.Equal(100, _desk.GetState().Channels[0]);
        Pass(10);
        Assert.Equal(100, _desk.GetState().Channels[0]);   // the fade does not go on
        AssertStack("idle", current: null, next: step);
        Assert.False(_desk.Animating);

        _desk.SetStackLive(false);                   // off already: nothing happens
        _desk.SetChannel(7, 70);
        _desk.SetStackLive(true);                    // and live again starts from the top, with everything down
        Assert.Equal(0, _desk.GetState().Channels[6]);
        AssertStack("fading", current: step, next: null, remaining: 2);
        Assert.Equal(0, _desk.GetState().Channels[0]);

        _desk.SetStackLive(true);                    // live already: the show is not started again
        Pass(1);
        Assert.Equal(100, _desk.GetState().Channels[0]);
    }

    [Fact]
    public void GoingOnMovesTheOldFaderDownAndTheNewOneUpAtTheSameTime()
    {
        int first = _desk.AddStackStep(new Step("channel", 1, 200, Up: 0, Wait: 0, Down: 4));
        int second = _desk.AddStackStep(new Step("channel", 2, 100, Up: 2, Wait: 0, Down: 0));
        _desk.SetStackLive(true);
        Pass(0.1);
        Assert.Equal(200, _desk.GetState().Channels[0]);   // an up time of 0: there at once

        _desk.StackPlay();                           // the first step is up and waits for Play
        Pass(1);
        Assert.Equal(150, _desk.GetState().Channels[0]);   // a quarter of its four seconds down
        Assert.Equal(50, _desk.GetState().Channels[1]);    // half of its two seconds up
        AssertStack("fading", current: second, next: first, remaining: 1);

        Pass(1);
        AssertStack("holding", current: second, next: first);
        Assert.True(_desk.Animating);                // the first fader is still on its way down
        Pass(2);
        Assert.Equal(0, _desk.GetState().Channels[0]);
        Assert.Equal(100, _desk.GetState().Channels[1]);
        Assert.False(_desk.Animating);
    }

    [Fact]
    public void WithAWaitTheStackGoesOnByItselfAndWithoutItWaitsForPlay()
    {
        int first = _desk.AddStackStep(new Step("channel", 1, 255, Up: 1, Wait: 3, Down: 0));
        int second = _desk.AddStackStep(new Step("channel", 2, 255, Up: 1, Wait: 0, Down: 0));
        int third = _desk.AddStackStep(new Step("channel", 3, 255, Up: 0, Wait: 5, Down: 0));

        _desk.SetStackLive(true);
        Pass(1);
        AssertStack("waiting", current: first, next: second, remaining: 3);
        Pass(2.5);
        AssertStack("waiting", current: first, next: second, remaining: 0.5);

        Pass(1);                                     // the wait ended half a second ago
        AssertStack("fading", current: second, next: third, remaining: 0.5);
        Assert.Equal(128, _desk.GetState().Channels[1]);
        Assert.Equal(0, _desk.GetState().Channels[0]);

        Pass(100);                                   // a wait of 0: it stays
        AssertStack("holding", current: second, next: third);
    }

    [Fact]
    public void AfterTheLastStepComesTheFirst()
    {
        int first = _desk.AddStackStep(new Step("channel", 1, 255, Up: 1, Wait: 0, Down: 0));
        int second = _desk.AddStackStep(new Step("channel", 2, 255, Up: 0, Wait: 0, Down: 0));
        int last = _desk.AddStackStep(new Step("channel", 3, 255, Up: 0, Wait: 5, Down: 0));
        _desk.SetStackLive(true);
        Pass(1);
        _desk.StackPlay();
        Pass(1);
        AssertStack("holding", current: second, next: last);

        _desk.StackPlay();                           // the last step has a wait: it goes on by itself, to the first
        Pass(1);
        AssertStack("waiting", current: last, next: first, remaining: 4);
        Pass(4.5);
        AssertStack("fading", current: first, next: second, remaining: 0.5);
        Assert.Equal(128, _desk.GetState().Channels[0]);
        Assert.Equal(0, _desk.GetState().Channels[2]);

        Pass(1);
        _desk.StackPrevious();                       // and back: before the first step stands the last
        Pass(0.1);
        Assert.Equal(last, _desk.GetPlayback().Stack.CurrentId);
        _desk.StackNext();
        Pass(0.1);
        Assert.Equal(first, _desk.GetPlayback().Stack.CurrentId);
    }

    [Fact]
    public void StackOfOneStepHasNowhereToGo()
    {
        int only = _desk.AddStackStep(new Step("channel", 1, 200, Up: 1, Wait: 2, Down: 0));
        _desk.SetStackLive(true);

        Pass(50);                                    // its wait leads nowhere
        AssertStack("holding", current: only, next: null);
        _desk.StackPlay();
        _desk.StackNext();
        _desk.StackPrevious();
        Pass(1);
        AssertStack("holding", current: only, next: null);
        Assert.Equal(200, _desk.GetState().Channels[0]);
        Assert.False(_desk.Animating);
    }

    [Fact]
    public void StackThatGoesRoundByItselfCatchesUpAfterALongTimeWithoutATick()
    {
        // Two steps that go on by themselves every tenth of a second, and a PC that slept for a
        // day: the stack must not try to replay 864 000 steps, and must still be running after.
        int first = _desk.AddStackStep(new Step("channel", 1, 255, Up: 0, Wait: 0.1, Down: 0));
        int second = _desk.AddStackStep(new Step("channel", 2, 255, Up: 0, Wait: 0.1, Down: 0));
        _desk.SetStackLive(true);

        Pass(86400);

        StackPlayback stack = _desk.GetPlayback().Stack;
        Assert.Equal("waiting", stack.State);
        Assert.Contains(stack.CurrentId, new int?[] { first, second });
        DeskState desk = _desk.GetState();
        Assert.Equal(255, desk.Channels[0] + desk.Channels[1]);   // one of the two is on, the other is down

        int? before = stack.CurrentId;
        Pass(0.11);                                  // and from here on it steps as always
        Assert.NotEqual(before, _desk.GetPlayback().Stack.CurrentId);
    }

    [Fact]
    public void PauseHoldsAFadeAndPlayGoesOnFromThere()
    {
        int step = _desk.AddStackStep(new Step("channel", 1, 200, Up: 2, Wait: 0, Down: 0));
        _desk.SetStackLive(true);
        Pass(0.5);
        Assert.Equal(50, _desk.GetState().Channels[0]);

        _desk.StackPause();
        Assert.True(_desk.GetPlayback().Stack.Paused);
        Assert.False(_desk.Animating);
        Pass(60);
        Assert.Equal(50, _desk.GetState().Channels[0]);
        AssertStack("fading", current: step, next: null, remaining: 1.5);

        _desk.StackPlay();                           // goes on; it does not go to another step
        Assert.False(_desk.GetPlayback().Stack.Paused);
        Pass(0.5);
        Assert.Equal(100, _desk.GetState().Channels[0]);
        Pass(1);
        Assert.Equal(200, _desk.GetState().Channels[0]);
    }

    [Fact]
    public void PauseAlsoHoldsAWait()
    {
        int first = _desk.AddStackStep(new Step("channel", 1, 255, Up: 0, Wait: 2, Down: 0));
        int second = _desk.AddStackStep(new Step("channel", 2, 255, Up: 0, Wait: 0, Down: 0));
        _desk.SetStackLive(true);
        Pass(0.5);

        _desk.StackPause();
        Pass(60);
        AssertStack("waiting", current: first, next: second, remaining: 1.5);

        _desk.StackPlay();
        Pass(1.4);
        AssertStack("waiting", current: first, next: second, remaining: 0.1);
        Pass(0.2);
        Assert.Equal(second, _desk.GetPlayback().Stack.CurrentId);
    }

    [Fact]
    public void PlayDoesNothingWhileAFadeOrAWaitIsRunning()
    {
        int first = _desk.AddStackStep(new Step("channel", 1, 255, Up: 2, Wait: 2, Down: 0));
        _desk.AddStackStep(new Step("channel", 2, 255, Up: 0, Wait: 0, Down: 0));
        _desk.SetStackLive(true);

        Pass(1);
        _desk.StackPlay();                           // while fading
        Assert.Equal(first, _desk.GetPlayback().Stack.CurrentId);
        Pass(2);
        _desk.StackPlay();                           // while waiting
        Assert.Equal(first, _desk.GetPlayback().Stack.CurrentId);
    }

    [Fact]
    public void NextAndPreviousGoThereAtOnceWhateverTheStackIsDoing()
    {
        int first = _desk.AddStackStep(new Step("channel", 1, 255, Up: 4, Wait: 9, Down: 0));
        int second = _desk.AddStackStep(new Step("channel", 2, 255, Up: 0, Wait: 0, Down: 0));
        int third = _desk.AddStackStep(new Step("channel", 3, 255, Up: 0, Wait: 0, Down: 0));

        _desk.SetStackLive(true);                    // the first step starts its four seconds up
        Pass(1);
        _desk.StackNext();                           // in the middle of its fade
        Pass(0.1);
        AssertStack("holding", current: second, next: third);
        Assert.Equal(0, _desk.GetState().Channels[0]);     // left: its fader went down, in no time
        Assert.Equal(255, _desk.GetState().Channels[1]);

        _desk.StackNext();
        Pass(0.1);
        AssertStack("holding", current: third, next: first);

        _desk.StackPause();                          // nothing is running: nothing to pause
        Assert.False(_desk.GetPlayback().Stack.Paused);

        _desk.StackPrevious();
        Pass(0.1);
        AssertStack("holding", current: second, next: third);
        Assert.Equal(0, _desk.GetState().Channels[2]);

        _desk.StackPrevious();
        _desk.StackPause();                          // the first step again, held a moment after its start
        Pass(5);
        _desk.StackNext();                           // Next and Previous end a pause
        Pass(0.1);
        AssertStack("holding", current: second, next: third);
    }

    [Fact]
    public void TwoStepsOnTheSameFaderMoveItFromTheOneValueToTheOther()
    {
        _desk.AddStackStep(new Step("channel", 1, 200, Up: 0, Wait: 0, Down: 9));
        _desk.AddStackStep(new Step("channel", 1, 100, Up: 2, Wait: 0, Down: 0));
        _desk.SetStackLive(true);
        Pass(0.1);

        _desk.StackPlay();
        Pass(1);
        Assert.Equal(150, _desk.GetState().Channels[0]);   // on its way from 200 to 100, not down to 0
        Pass(1);
        Assert.Equal(100, _desk.GetState().Channels[0]);
    }

    [Fact]
    public void AHandOnAFaderTakesItAwayFromTheStack()
    {
        int step = _desk.AddStackStep(new Step("channel", 1, 200, Up: 2, Wait: 0, Down: 0));
        _desk.SetStackLive(true);
        Pass(1);

        _desk.SetChannel(1, 30);
        Pass(5);

        Assert.Equal(30, _desk.GetState().Channels[0]);
        AssertStack("holding", current: step, next: null);   // the stack itself went on as if nothing had happened
    }

    [Fact]
    public void AMemoryStepMovesTheFaderOfTheMemoryAndThatStartsItsSequence()
    {
        _desk.SetSequence(5, loop: false, [new Step("channel", 9, 255, Up: 1, Wait: 0, Down: 0)]);
        _desk.AddStackStep(new Step("memory", 5, 200, Up: 0, Wait: 0, Down: 2));
        _desk.AddStackStep(new Step("channel", 1, 0, Up: 0, Wait: 0, Down: 0));

        _desk.SetStackLive(true);
        Pass(0.5);
        Assert.Equal(200, _desk.GetState().MemoryLevels[4]);
        Assert.Equal(100, _desk.GetOutput()[8]);     // the sequence is half a second old: 128, under a fader at 200

        _desk.StackPlay();                           // leaving the step: the memory's fader goes down in two seconds
        Pass(1);
        Assert.Equal(100, _desk.GetState().MemoryLevels[4]);
        Pass(1);
        Assert.Equal(0, _desk.GetState().MemoryLevels[4]);
        Assert.Equal(0, _desk.GetOutput()[8]);
    }

    [Fact]
    public void TickTellsWhichFadersTheStackMoved()
    {
        _desk.AddStackStep(new Step("channel", 7, 200, Up: 2, Wait: 0, Down: 0));
        _desk.AddStackStep(new Step("memory", 3, 100, Up: 0, Wait: 0, Down: 0));
        Assert.Empty(_desk.Tick());

        _desk.SetStackLive(true);
        long before = _desk.OutputChanges;
        _time.Pass(1);
        Assert.Equal([new FaderMoved(Memory: false, 7, 100)], _desk.Tick());
        Assert.True(_desk.OutputChanges > before);   // the box link sends again
        Assert.Empty(_desk.Tick());                  // no time has passed: nothing moved

        _time.Pass(1);
        _desk.Tick();
        _desk.StackPlay();
        _time.Pass(0.1);
        IReadOnlyList<FaderMoved> moved = _desk.Tick();
        Assert.Contains(new FaderMoved(Memory: true, 3, 100), moved);
        Assert.Contains(new FaderMoved(Memory: false, 7, 0), moved);
    }

    [Fact]
    public void ClearTakesTheStackOffItsStepButLeavesItLive()
    {
        int first = _desk.AddStackStep(new Step("channel", 1, 200, Up: 5, Wait: 0, Down: 0));
        _desk.AddStackStep(new Step("channel", 2, 200, Up: 0, Wait: 0, Down: 0));
        _desk.SetStackLive(true);
        Pass(1);
        _desk.StackNext();                           // the second fader jumps up, and no tick has told the UIs yet

        _desk.Clear();
        Assert.Empty(_desk.Tick());                  // the UIs get the whole desk: no fader move is left to tell
        Pass(10);
        Assert.Equal(0, _desk.GetState().Channels[0]);
        Assert.Equal(0, _desk.GetState().Channels[1]);
        AssertStack("idle", current: null, next: first);
        Assert.False(_desk.Animating);
        Assert.True(_desk.GetPlayback().Stack.Live);

        _desk.StackPrevious();                       // no step is on: there is none before it
        AssertStack("idle", current: null, next: first);
        _desk.StackPlay();                           // and Play starts with the first one again
        AssertStack("fading", current: first, next: _desk.GetStack().Steps[1].Id, remaining: 5);
    }

    [Fact]
    public void StepsCanBeChangedMovedRenumberedAndTakenOut()
    {
        int first = _desk.AddStackStep(new Step("channel", 1, 255, 0, 0, 0));
        int second = _desk.AddStackStep(new Step("channel", 2, 255, 0, 0, 0));
        int third = _desk.AddStackStep(new Step("channel", 3, 255, 0, 0, 0));
        int fourth = _desk.AddStackStep(new Step("channel", 4, 255, 0, 0, 0));

        _desk.MoveStackStep(fourth, second);         // the owner's example: 4.0 between 2.0 and 3.0 becomes 2.1
        Assert.Equal(["1.0", "2.0", "2.1", "3.0"], Cues());

        _desk.RenumberStackStep(fourth, "3");        // and by hand to 3.0: the old 3.0 becomes 4.0
        Assert.Equal(["1.0", "2.0", "3.0", "4.0"], Cues());
        Assert.Equal([first, second, fourth, third], _desk.GetStack().Steps.Select(step => step.Id));

        _desk.RenumberStackStep(second, "9");
        Assert.Equal(["1.0", "3.0", "4.0", "9.0"], Cues());
        _desk.RenumberStackStep(second, "2.0");

        _desk.UpdateStackStep(third, new Step("memory", 8, 10, 1, 2, 3));
        Assert.Equal(new StackStep(third, "4.0", "memory", 8, 10, 1, 2, 3), _desk.GetStack().Steps[3]);

        _desk.DeleteStackStep(second);
        Assert.Equal(["1.0", "3.0", "4.0"], Cues());
        AssertStack("idle", current: null, next: first);

        Assert.Throws<DeskException>(() => _desk.DeleteStackStep(second));
        Assert.Throws<DeskException>(() => _desk.UpdateStackStep(999, new Step("channel", 1, 1, 0, 0, 0)));
        Assert.Throws<DeskException>(() => _desk.RenumberStackStep(first, "one"));
    }

    [Fact]
    public void WhileTheStackIsLiveNothingThatIsStoredCanBeChanged()
    {
        int step = _desk.AddStackStep(new Step("channel", 1, 255, 0, 0, 0));
        int other = _desk.AddStackStep(new Step("channel", 2, 255, 0, 0, 0));
        _desk.SetChannel(1, 50);
        _desk.SaveMemory(1);
        _desk.SetSequence(2, loop: true, [new Step("channel", 3, 255, 1, 1, 1)]);
        ProjectContent project = _desk.GetProject().Content;

        _desk.SetStackLive(true);
        long before = _desk.ProjectChanges;

        var aStep = new Step("channel", 9, 9, 0, 0, 0);
        Action[] changes =
        [
            () => _desk.AddStackStep(aStep),
            () => _desk.UpdateStackStep(step, aStep),
            () => _desk.DeleteStackStep(step),
            () => _desk.MoveStackStep(other, null),
            () => _desk.RenumberStackStep(step, "7"),
            () => _desk.SaveMemory(3),
            () => _desk.LoadMemory(1),
            () => _desk.CopyMemory(1, 4),
            () => _desk.DeleteMemory(1),
            () => _desk.SetSequence(2, loop: false, []),
            () => _desk.SetSequenceSpeed(2, 2),
            () => _desk.AddSequenceStep(2, aStep),
            () => _desk.AddSequenceStep(5, aStep),
            () => _desk.SetChannelNote(1, "Front"),
            () => _desk.SetMemoryNote(1, "Wash"),
            () => _desk.SetIndependent(1, 40),
            () => _desk.SetMemoryIndependent(1, 40),
            () => _desk.StartNewProject(),
            () => _desk.ReplaceProject(project),
        ];
        foreach (Action change in changes)
        {
            var error = Assert.Throws<DeskException>(change);
            Assert.Equal("The stack is live, so nothing that is stored can be changed. Switch Stack off first.", error.Message);
        }

        Assert.Equal(before, _desk.ProjectChanges);
        Assert.Equal(["1.0", "2.0"], Cues());
        Assert.Equal([1], _desk.GetMemoryLooks().Keys);
        Assert.True(_desk.GetSequences()[2].Loop);

        // What a show needs goes on working: the faders, the master, blackout, Clear, and saving.
        _desk.SetChannel(4, 10);
        _desk.SetMemoryLevel(1, 255);
        _desk.SetMaster(100);
        _desk.SetBlackout(true);
        Assert.Equal(10, _desk.GetState().Channels[3]);
        Assert.Equal(2, _desk.GetProject().Content.Stack.Length);
        _desk.Clear();

        _desk.SetStackLive(false);                   // and with the stack off everything can be changed again
        _desk.SaveMemory(3);
        _desk.SetChannelNote(1, "Front");
        _desk.DeleteStackStep(other);
        _desk.StartNewProject();
        Assert.Empty(_desk.GetStack().Steps);
    }

    [Fact]
    public void TheStackIsPartOfTheProject()
    {
        long before = _desk.ProjectChanges;
        int step = _desk.AddStackStep(new Step("channel", 1, 255, 0, 0, 0));
        int other = _desk.AddStackStep(new Step("channel", 2, 255, 0, 0, 0));
        _desk.UpdateStackStep(step, new Step("channel", 1, 100, 0, 0, 0));
        _desk.MoveStackStep(other, null);
        _desk.RenumberStackStep(step, "7.5");
        _desk.DeleteStackStep(other);
        Assert.Equal(before + 6, _desk.ProjectChanges);

        _desk.MoveStackStep(step, null);             // it is at the top already: no change
        _desk.SetStackLive(true);                    // and going through it changes nothing in the project
        Pass(1);
        _desk.SetStackLive(false);
        Assert.Equal(before + 6, _desk.ProjectChanges);

        ProjectContent content = _desk.GetProject().Content;
        ProjectStep saved = Assert.Single(content.Stack);
        Assert.Equal(new ProjectStep(new CueNumber(7, 5), new Step("channel", 1, 100, 0, 0, 0)), saved);

        var fresh = new Desk(_time);
        fresh.ReplaceProject(content);
        Assert.Equal(["7.5"], fresh.GetStack().Steps.Select(s => s.Cue));
        Assert.Null(fresh.GetPlayback().Stack.CurrentId);
        Assert.False(fresh.GetPlayback().Stack.Live);   // a loaded project never starts by itself
    }

    // Lets time pass and the desk notice it, as the desk clock does 30 times a second.
    [Fact]
    public void ANewStepCanGoRightAfterAnotherOne()
    {
        int first = _desk.AddStackStep(new Step("channel", 1, 255, 0, 0, 0));
        int second = _desk.AddStackStep(new Step("channel", 2, 255, 0, 0, 0));
        int third = _desk.AddStackStep(new Step("channel", 3, 255, 0, 0, 0));
        long before = _desk.ProjectChanges;

        int added = _desk.AddStackStep(new Step("channel", 7, 255, 0, 0, 0), afterId: first);

        Assert.Equal(["1.0", "1.1", "2.0", "3.0"], Cues());
        Assert.Equal([first, added, second, third], _desk.GetStack().Steps.Select(step => step.Id));
        Assert.Equal(7, _desk.GetStack().Steps[1].Number);
        Assert.True(_desk.ProjectChanges > before);

        int again = _desk.AddStackStep(new Step("channel", 8, 255, 0, 0, 0), afterId: first);
        Assert.Equal(["1.0", "1.1", "1.2", "2.0", "3.0"], Cues());   // 1.1 was taken: it gave way
        Assert.Equal([first, again, added, second, third], _desk.GetStack().Steps.Select(step => step.Id));

        _desk.AddStackStep(new Step("channel", 9, 255, 0, 0, 0), afterId: third);
        Assert.Equal("4.0", Cues()[^1]);              // after the last one: the next whole number, as at the end

        var error = Assert.Throws<DeskException>(() => _desk.AddStackStep(new Step("channel", 9, 255, 0, 0, 0), afterId: 9999));
        Assert.Equal("That step is not in the stack any more.", error.Message);
        Assert.Equal(6, Cues().Length);              // nothing was put in
    }

    private void Pass(double seconds)
    {
        _time.Pass(seconds);
        _desk.Tick();
    }

    private string[] Cues()
    {
        return _desk.GetStack().Steps.Select(step => step.Cue).ToArray();
    }

    private void AssertStack(string state, int? current, int? next, double remaining = 0)
    {
        StackPlayback stack = _desk.GetPlayback().Stack;
        Assert.Equal(state, stack.State);
        Assert.Equal(current, stack.CurrentId);
        Assert.Equal(next, stack.NextId);
        Assert.Equal(remaining, stack.Remaining, precision: 2);
    }
}
