using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// The stack: steps that are gone through with Play, Pause, Next and Previous, and that move
/// the real faders of their channels and memories.
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
    public void PlayMovesTheFaderOfTheStepUpOverItsUpTime()
    {
        int step = _desk.AddStackStep(new Step("channel", 1, 200, Up: 2, Wait: 0, Down: 1));
        AssertStack("idle", current: null, next: step);

        _desk.StackPlay();
        AssertStack("fading", current: step, next: null, remaining: 2);
        Assert.True(_desk.Animating);

        Pass(1);
        Assert.Equal(100, _desk.GetState().Channels[0]);   // the real fader, not a layer
        AssertStack("fading", current: step, next: null, remaining: 1);

        Pass(1);
        Assert.Equal(200, _desk.GetState().Channels[0]);
        Assert.Equal(200, _desk.GetOutput()[0]);
        AssertStack("holding", current: step, next: null);
        Assert.False(_desk.Animating);
    }

    [Fact]
    public void GoingOnMovesTheOldFaderDownAndTheNewOneUpAtTheSameTime()
    {
        _desk.AddStackStep(new Step("channel", 1, 200, Up: 0, Wait: 0, Down: 4));
        int second = _desk.AddStackStep(new Step("channel", 2, 100, Up: 2, Wait: 0, Down: 0));
        _desk.StackPlay();
        Pass(0.1);
        Assert.Equal(200, _desk.GetState().Channels[0]);   // an up time of 0: there at once

        _desk.StackPlay();                           // the first step is up and waits for Play
        Pass(1);
        Assert.Equal(150, _desk.GetState().Channels[0]);   // a quarter of its four seconds down
        Assert.Equal(50, _desk.GetState().Channels[1]);    // half of its two seconds up
        AssertStack("fading", current: second, next: null, remaining: 1);

        Pass(1);
        AssertStack("holding", current: second, next: null);
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

        _desk.StackPlay();
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

        _desk.StackPlay();
        Pass(100);                                   // the last step has a wait, but nothing to go on to
        AssertStack("holding", current: third, next: null);
    }

    [Fact]
    public void PauseHoldsAFadeAndPlayGoesOnFromThere()
    {
        int step = _desk.AddStackStep(new Step("channel", 1, 200, Up: 2, Wait: 0, Down: 0));
        _desk.StackPlay();
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
        _desk.StackPlay();
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
        _desk.StackPlay();

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

        _desk.StackNext();                           // from "no step is on" to the first
        Pass(1);
        _desk.StackNext();                           // in the middle of its fade
        Pass(0.1);
        AssertStack("holding", current: second, next: third);
        Assert.Equal(0, _desk.GetState().Channels[0]);     // left: its fader went down, in no time
        Assert.Equal(255, _desk.GetState().Channels[1]);

        _desk.StackNext();
        _desk.StackNext();                           // at the last step nothing happens
        Pass(0.1);
        AssertStack("holding", current: third, next: null);

        _desk.StackPause();                          // nothing is running: nothing to pause
        Assert.False(_desk.GetPlayback().Stack.Paused);

        _desk.StackPrevious();
        Pass(0.1);
        AssertStack("holding", current: second, next: third);
        Assert.Equal(0, _desk.GetState().Channels[2]);

        _desk.StackPrevious();
        _desk.StackPrevious();                       // from the first step: no step is on
        Pass(5);
        AssertStack("idle", current: null, next: first);
        Assert.Equal(0, _desk.GetState().Channels[0]);
        _desk.StackPrevious();                       // and there it stays
        AssertStack("idle", current: null, next: first);
    }

    [Fact]
    public void TwoStepsOnTheSameFaderMoveItFromTheOneValueToTheOther()
    {
        _desk.AddStackStep(new Step("channel", 1, 200, Up: 0, Wait: 0, Down: 9));
        _desk.AddStackStep(new Step("channel", 1, 100, Up: 2, Wait: 0, Down: 0));
        _desk.StackPlay();
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
        _desk.StackPlay();
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

        _desk.StackPlay();
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

        _desk.StackPlay();
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
    public void ClearStopsTheStackAndANewProjectEmptiesIt()
    {
        int first = _desk.AddStackStep(new Step("channel", 1, 200, Up: 5, Wait: 0, Down: 0));
        _desk.StackPlay();
        Pass(1);

        _desk.Clear();
        Pass(10);
        Assert.Equal(0, _desk.GetState().Channels[0]);     // and the fade does not go on
        AssertStack("idle", current: null, next: first);
        Assert.False(_desk.Animating);

        _desk.StartNewProject();
        Assert.Empty(_desk.GetStack().Steps);
    }

    [Fact]
    public void StepsCanBeChangedMovedRenumberedAndTakenOutWhileTheStackRuns()
    {
        int first = _desk.AddStackStep(new Step("channel", 1, 255, 0, 0, 0));
        int second = _desk.AddStackStep(new Step("channel", 2, 255, 0, 0, 0));
        int third = _desk.AddStackStep(new Step("channel", 3, 255, 0, 0, 0));
        int fourth = _desk.AddStackStep(new Step("channel", 4, 255, 0, 0, 0));
        _desk.StackPlay();
        _desk.StackPlay();                           // the second step is on
        Pass(0.1);

        _desk.MoveStackStep(fourth, second);         // the owner's example: 4.0 between 2.0 and 3.0 becomes 2.1
        Assert.Equal(["1.0", "2.0", "2.1", "3.0"], Cues());
        AssertStack("holding", current: second, next: fourth);

        _desk.RenumberStackStep(fourth, "3");        // and by hand to 3.0: the old 3.0 becomes 4.0
        Assert.Equal(["1.0", "2.0", "3.0", "4.0"], Cues());
        Assert.Equal([first, second, fourth, third], _desk.GetStack().Steps.Select(step => step.Id));

        _desk.RenumberStackStep(second, "9");        // the current step itself moves: "next" follows it
        Assert.Equal(["1.0", "3.0", "4.0", "9.0"], Cues());
        AssertStack("holding", current: second, next: null);
        _desk.RenumberStackStep(second, "2.0");

        _desk.UpdateStackStep(third, new Step("memory", 8, 10, 1, 2, 3));
        Assert.Equal(new StackStep(third, "4.0", "memory", 8, 10, 1, 2, 3), _desk.GetStack().Steps[3]);

        _desk.DeleteStackStep(second);               // the current step is taken out
        Assert.Equal(["1.0", "3.0", "4.0"], Cues());
        AssertStack("idle", current: null, next: fourth);   // the next one is the one that followed it
        Assert.Equal(255, _desk.GetState().Channels[1]);    // and its fader stays where it is

        Assert.Throws<DeskException>(() => _desk.DeleteStackStep(second));
        Assert.Throws<DeskException>(() => _desk.UpdateStackStep(999, new Step("channel", 1, 1, 0, 0, 0)));
        Assert.Throws<DeskException>(() => _desk.RenumberStackStep(first, "one"));
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
        _desk.StackPlay();                           // and going through it changes nothing in the project
        Assert.Equal(before + 6, _desk.ProjectChanges);

        ProjectContent content = _desk.GetProject().Content;
        ProjectStep saved = Assert.Single(content.Stack);
        Assert.Equal(new ProjectStep(new CueNumber(7, 5), new Step("channel", 1, 100, 0, 0, 0)), saved);

        var fresh = new Desk(_time);
        fresh.ReplaceProject(content);
        Assert.Equal(["7.5"], fresh.GetStack().Steps.Select(s => s.Cue));
        Assert.Null(fresh.GetPlayback().Stack.CurrentId);
    }

    // Lets time pass and the desk notice it, as the desk clock does 30 times a second.
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
