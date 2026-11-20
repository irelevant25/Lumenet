using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// Undo and redo: what the project holds goes back and forward; the faders, Clear and the
/// settings are no steps.
/// </summary>
public sealed class UndoHistoryTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;
    private readonly UndoHistory _history;

    public UndoHistoryTests()
    {
        _desk = new Desk(_time);
        _history = new UndoHistory(_desk, _time);
    }

    [Fact]
    public void UndoTakesBackAChangeAndRedoMakesItAgain()
    {
        _desk.SetChannel(5, 200);
        Change("storing memory 1", () => _desk.SaveMemory(1));
        Assert.Equal(new HistoryState("storing memory 1", null), _history.State);

        Assert.Equal("storing memory 1", _history.Undo());
        Assert.False(_desk.GetState().MemoryStored[0]);
        Assert.Equal(new HistoryState(null, "storing memory 1"), _history.State);

        Assert.Equal("storing memory 1", _history.Redo());
        Assert.True(_desk.GetState().MemoryStored[0]);
        Assert.Equal(200, _desk.GetMemoryLooks()[1][4]);
        Assert.Equal(new HistoryState("storing memory 1", null), _history.State);
    }

    [Fact]
    public void StepsGoBackOneAfterTheOther()
    {
        Change("the note of channel 1", () => _desk.SetChannelNote(1, "Front"));
        Change("the note of channel 1", () => _desk.SetChannelNote(1, "Back"));
        Change("a new step of the stack", () => _desk.AddStackStep(new Step("memory", 1, 255, 1, 0, 1)));

        _history.Undo();
        Assert.Empty(_desk.GetStack().Steps);
        Assert.Equal("Back", _desk.GetState().ChannelNotes[0]);
        _history.Undo();
        Assert.Equal("Front", _desk.GetState().ChannelNotes[0]);
        _history.Undo();
        Assert.Equal("", _desk.GetState().ChannelNotes[0]);

        var nothing = Assert.Throws<DeskException>(() => _history.Undo());
        Assert.Equal("There is nothing to undo.", nothing.Message);
    }

    [Fact]
    public void ANewChangeAfterUndoLeavesNothingToRedo()
    {
        Change("the note of memory 2", () => _desk.SetMemoryNote(2, "Blue"));
        _history.Undo();
        Change("the note of memory 3", () => _desk.SetMemoryNote(3, "Red"));

        Assert.Equal(new HistoryState("the note of memory 3", null), _history.State);
        var nothing = Assert.Throws<DeskException>(() => _history.Redo());
        Assert.Equal("There is nothing to redo.", nothing.Message);
    }

    [Fact]
    public void FadersClearAndSettingsAreNoStepsAndUndoLeavesThemAlone()
    {
        _desk.SetChannel(3, 100);
        Change("storing memory 1", () => _desk.SaveMemory(1));
        _desk.SetChannel(3, 255);
        _desk.SetMemoryLevel(1, 255);
        _desk.SetSettings("""{"channelSize":8}""");
        _desk.Clear();
        _desk.SetChannel(4, 50);

        Change("nothing at all", () => _desk.SetChannelNote(9, ""));   // the same text again: no change, no step
        Assert.Equal(new HistoryState("storing memory 1", null), _history.State);

        _history.Undo();
        Assert.False(_desk.GetState().MemoryStored[0]);
        Assert.Equal(50, _desk.GetState().Channels[3]);   // the faders stay where they are
        Assert.Equal("""{"channelSize":8}""", _desk.Settings);
    }

    [Fact]
    public void ChangesOfTheSameKindInQuickSuccessionMakeOneStep()
    {
        _desk.SetSequence(3, loop: true, [new Step("channel", 1, 255, 1, 0, 1)]);
        for (int move = 1; move <= 5; move++)
        {
            Change("the speed of the sequence in memory 3", () => _desk.SetSequenceSpeed(3, 1 + move * 0.1), "speed 3");
            _time.Pass(0.5);
        }

        _history.Undo();
        Assert.Equal(1, _desk.GetSequences()[3].Speed);   // all five moves at once
        Assert.Throws<DeskException>(() => _history.Undo());

        // After a pause it is a new step.
        _history.Redo();
        _time.Pass(3);
        Change("the speed of the sequence in memory 3", () => _desk.SetSequenceSpeed(3, 2), "speed 3");
        _history.Undo();
        Assert.Equal(1.5, _desk.GetSequences()[3].Speed);
    }

    [Fact]
    public void UndoIsRefusedWhileTheStackIsLiveAndChangesNothing()
    {
        Change("a new step of the stack", () => _desk.AddStackStep(new Step("memory", 1, 255, 1, 0, 1)));
        _desk.SetStackLive(true);

        var live = Assert.Throws<DeskException>(() => _history.Undo());
        Assert.Contains("The stack is live", live.Message);
        Assert.Single(_desk.GetStack().Steps);
        Assert.Equal("a new step of the stack", _history.State.Undo);
    }

    [Fact]
    public void ClearForgetsEveryStep()
    {
        Change("storing memory 1", () => _desk.SaveMemory(1));
        _history.Clear();
        Assert.Equal(new HistoryState(null, null), _history.State);
    }

    [Fact]
    public void AtMostAHundredStepsAreKept()
    {
        for (int note = 1; note <= UndoHistory.MostSteps + 5; note++)
            Change($"the note of channel {note}", () => _desk.SetChannelNote(note, "x"));

        for (int step = 0; step < UndoHistory.MostSteps; step++)
            _history.Undo();

        Assert.Throws<DeskException>(() => _history.Undo());
        Assert.Equal("x", _desk.GetState().ChannelNotes[4]);    // the five oldest are kept for good
        Assert.Equal("", _desk.GetState().ChannelNotes[5]);
    }

    [Fact]
    public void TellsWhatChangedOnlyOnce()
    {
        Assert.Null(_history.TakeChange());
        Change("storing memory 1", () => _desk.SaveMemory(1));
        Assert.Equal(new HistoryState("storing memory 1", null), _history.TakeChange());
        Assert.Null(_history.TakeChange());
    }

    private void Change(string what, Action change, string? kind = null)
    {
        using (_history.Change(what, kind))
            change();
    }
}
