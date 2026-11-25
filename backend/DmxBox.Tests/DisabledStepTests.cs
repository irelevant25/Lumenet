using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// Steps that are skipped (Disabled): the stack passes over them with Play, Next, Prev, a wait that
/// goes on by itself and going live; a sequence leaves them out of its rounds. Both can be switched
/// while the stack is live, but not the step that is on just then. And the file keeps them.
/// </summary>
public sealed class DisabledStepTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;

    public DisabledStepTests()
    {
        _desk = new Desk(_time);
    }

    private static Step Channel(int channel, double wait = 0) => new(Step.Channel, channel, 255, 0, wait, 0);

    private int? Current() => _desk.GetPlayback().Stack.CurrentId;

    private int? Next() => _desk.GetPlayback().Stack.NextId;

    [Fact]
    public void TheStackPassesOverASkippedStepEveryWayItGoes()
    {
        int first = _desk.AddStackStep(Channel(1) with { Disabled = true });
        int second = _desk.AddStackStep(Channel(2));
        int third = _desk.AddStackStep(Channel(3) with { Disabled = true });
        int fourth = _desk.AddStackStep(Channel(4, wait: 1));
        int fifth = _desk.AddStackStep(Channel(5));

        _desk.SetStackLive(true);              // live: from the first step that is not skipped
        Assert.Equal(second, Current());
        Assert.Equal(fourth, Next());
        Assert.Equal(0, _desk.GetOutput()[0]);

        _desk.StackPlay();                     // Play over the skipped step
        Assert.Equal(fourth, Current());
        _time.Pass(1.5);                       // its wait goes on by itself, to the step after
        _desk.Tick();
        Assert.Equal(fifth, Current());

        _desk.StackNext();                     // round: the first step is skipped too
        Assert.Equal(second, Current());
        _desk.StackPrevious();                 // and backwards
        Assert.Equal(fifth, Current());
        _desk.StackPrevious();
        Assert.Equal(fourth, Current());
        _desk.StackPrevious();
        Assert.Equal(second, Current());
        Assert.Equal(0, _desk.GetOutput()[2]);
        Assert.True(_desk.GetStack().Steps.Single(step => step.Id == third).Disabled);   // the pages see it
        Assert.True(_desk.GetStack().Steps.Single(step => step.Id == first).Disabled);
    }

    [Fact]
    public void AStepIsSkippedWhileTheStackIsLiveButNotTheStepThatIsOn()
    {
        int first = _desk.AddStackStep(Channel(1));
        int second = _desk.AddStackStep(Channel(2));
        int third = _desk.AddStackStep(Channel(3));
        _desk.SetStackLive(true);

        DeskException error = Assert.Throws<DeskException>(() => _desk.SetStackStepDisabled(first, true));
        Assert.Equal("Step 1.0 is the step that is on: it cannot be skipped now.", error.Message);
        Assert.Throws<DeskException>(() => _desk.UpdateStackStep(second, Channel(9)));   // every other change stays locked

        Assert.True(_desk.SetStackStepDisabled(second, true));
        Assert.Equal(third, Next());
        _desk.StackNext();
        Assert.Equal(third, Current());

        Assert.True(_desk.SetStackStepDisabled(second, false));   // and back
        Assert.False(_desk.SetStackStepDisabled(second, false));
        Assert.Equal(first, Next());
    }

    [Fact]
    public void AStackWhoseStepsAreAllSkippedDoesNotGoLive()
    {
        _desk.AddStackStep(Channel(1) with { Disabled = true });
        DeskException error = Assert.Throws<DeskException>(() => _desk.SetStackLive(true));
        Assert.Contains("Every step of the stack is skipped", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASequenceLeavesASkippedStepOutOfItsRounds()
    {
        var sequence = new Sequence(Loop: true, [Channel(1, wait: 1), Channel(2, wait: 1) with { Disabled = true }, Channel(3, wait: 1)]);
        Assert.Equal(0, SequenceTimeline.CurrentStep(sequence, 0.5));
        Assert.Equal(2, SequenceTimeline.CurrentStep(sequence, 1.5));   // straight to the third step
        Assert.Equal(0, SequenceTimeline.CurrentStep(sequence, 2.5));   // a round is two steps long
        Assert.Equal(1, SequenceTimeline.AverageStep(sequence));

        var none = sequence with { Steps = [Channel(1) with { Disabled = true }] };
        Assert.Equal(-1, SequenceTimeline.CurrentStep(none, 3));
        Assert.False(SequenceTimeline.StillMoves(none, 0));

        _desk.SetSequence(5, sequence);
        _desk.SetMemoryLevel(5, 255);
        _time.Pass(1.5);
        _desk.Tick();
        Assert.Equal((0, 255), (_desk.GetOutput()[1], _desk.GetOutput()[2]));
    }

    [Fact]
    public void AStepOfASequenceIsSkippedWhileTheStackIsLiveButNotTheStepThatPlays()
    {
        _desk.SetSequence(5, new Sequence(Loop: true, [Channel(1, wait: 1), Channel(2, wait: 1), Channel(3, wait: 1)]));
        _desk.AddStackStep(new Step(Step.Memory, 5, 255, 0, 0, 0));
        _desk.SetStackLive(true);              // the stack brings memory 5 up: its sequence runs from step 1

        DeskException error = Assert.Throws<DeskException>(() => _desk.SetSequenceStepDisabled(5, 1, true));
        Assert.Equal("Step 1 of the sequence in memory 5 plays just now: it cannot be skipped now.", error.Message);
        Assert.True(_desk.SetSequenceStepDisabled(5, 2, true));
        Assert.True(_desk.GetSequences()[5].Steps[1].Disabled);

        _time.Pass(1.5);                       // on from step 1 to step 3
        _desk.Tick();
        Assert.Equal((0, 255), (_desk.GetOutput()[1], _desk.GetOutput()[2]));
        Assert.Throws<DeskException>(() => _desk.SetSequenceStepDisabled(5, 4, true));
        Assert.Throws<DeskException>(() => _desk.SetSequenceStepDisabled(6, 1, true));
    }

    [Fact]
    public void AProjectWithSkippedStepsIsVersion9AndKeepsThem()
    {
        _desk.AddStackStep(Channel(1) with { Disabled = true });
        _desk.SetSequence(3, new Sequence(Loop: true, [Channel(2), Channel(3) with { Disabled = true }]));
        using var file = new MemoryStream();
        ProjectFormat.Write(file, _desk.GetProject().Content);
        string text = System.Text.Encoding.UTF8.GetString(file.ToArray());
        Assert.Contains("\"version\": 9", text, StringComparison.Ordinal);
        Assert.Equal(2, text.Split("\"disabled\": true").Length - 1);

        file.Position = 0;
        ProjectContent read = ProjectFormat.Read(file);
        Assert.True(Assert.Single(read.Stack).Step.Disabled);
        Assert.Equal([false, true], read.Sequences[2]!.Steps.Select(step => step.Disabled));
    }
}
