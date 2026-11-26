namespace DmxBox.Tests.Stack;

/// <summary>
/// Steps of the stack that add (they come up on top of what is on and take nothing down) and
/// releases (they take one step that is on down and leave the rest). The owner's example: a scene,
/// a special light added on top, and then the scene again, or a release of the special light.
/// </summary>
public sealed class StackAddReleaseTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;

    public StackAddReleaseTests()
    {
        _desk = new Desk(_time);
        Look(1, (10, 200), (11, 150), (45, 100));    // the scene
        Look(2, (45, 255), (46, 255));               // the special light
    }

    [Fact]
    public void AnAddStepComesUpOnTopAndTakesNothingDown()
    {
        int scene = Add(Memory(1, up: 0, down: 2));
        int special = Add(Memory(2, up: 2, down: 4) with { Add = true });
        int again = Add(Memory(1, up: 0, down: 2));
        Live();
        AssertLevels(10, 200, 11, 150, 45, 100, 46, 0);

        _desk.StackPlay();
        Pass(1);
        AssertLevels(10, 200, 11, 150, 45, 128, 46, 128);    // the special light half way up, the scene as it was
        Pass(1);
        AssertLevels(10, 200, 11, 150, 45, 255, 46, 255);
        Assert.Equal(255, _desk.GetState().MemoryLevels[0]);
        Assert.Equal([scene, special], _desk.GetPlayback().Stack.On);

        _desk.StackPlay();                                    // the scene again: the special light goes, in its own down time
        Pass(2);
        AssertLevels(10, 200, 11, 150, 45, 127, 46, 127);    // just under half way down: 127.5 not yet reached
        Pass(2);
        AssertLevels(10, 200, 11, 150, 45, 100, 46, 0);
        Assert.Equal(255, _desk.GetState().MemoryLevels[0]); // the scene never moved
        Assert.Equal([again], _desk.GetPlayback().Stack.On);
    }

    [Fact]
    public void AStepThatDoesNotAddTakesDownEveryStepThatIsOnEachInItsOwnDownTime()
    {
        Add(Channel(20, 255, down: 1));
        Add(Channel(21, 255, down: 3) with { Add = true });
        Add(Channel(22, 255, down: 0) with { Add = true });
        int last = Add(Channel(23, 200, down: 0));
        Live();
        _desk.StackPlay();
        _desk.StackPlay();
        Pass(0.1);
        AssertLevels(20, 255, 21, 255, 22, 255, 23, 0);

        _desk.StackPlay();
        Pass(0.6);
        AssertLevels(20, 102, 21, 204, 22, 0, 23, 200);      // 0.6 of 1 s, 0.6 of 3 s, at once
        Pass(3);
        AssertLevels(20, 0, 21, 0, 22, 0, 23, 200);
        Assert.Equal([last], _desk.GetPlayback().Stack.On);
    }

    [Fact]
    public void AReleaseTakesOnlyItsStepDownInItsOwnDownTimeAndThenWaits()
    {
        int scene = Add(Memory(1, up: 0, down: 2));
        int special = Add(Memory(2, up: 0, down: 9) with { Add = true });
        int release = Add(new Step(Step.Release, special, 0, Up: 0, Wait: 3, Down: 2));
        int after = Add(Channel(30, 255, down: 0));
        Live();
        _desk.StackPlay();
        Pass(0.1);
        AssertLevels(45, 255, 46, 255);

        _desk.StackPlay();
        Pass(1);
        AssertLevels(10, 200, 11, 150, 45, 127, 46, 127);    // one of the release's two seconds, not of the light's nine
        Assert.Equal("fading", _desk.GetPlayback().Stack.State);
        Pass(1);
        AssertLevels(10, 200, 11, 150, 45, 100, 46, 0);
        Playback playback = _desk.GetPlayback();
        Assert.Equal(release, playback.Stack.CurrentId);
        Assert.Equal([scene], playback.Stack.On);
        Assert.Equal("waiting", playback.Stack.State);

        Pass(3);                                              // the release's wait is over: the next step comes
        Assert.Equal(after, _desk.GetPlayback().Stack.CurrentId);
        AssertLevels(30, 255);
    }

    [Fact]
    public void PreviousFromAnAddStepTakesThatOneDownAndLeavesTheRest()
    {
        int scene = Add(Memory(1, up: 0, down: 2));
        int first = Add(Channel(60, 255, down: 1) with { Add = true });
        int second = Add(Channel(61, 255, down: 1) with { Add = true });
        Live();
        _desk.StackPlay();
        _desk.StackPlay();
        Pass(0.1);
        Assert.Equal([scene, first, second], _desk.GetPlayback().Stack.On);

        _desk.StackPrevious();
        Pass(1);
        AssertLevels(10, 200, 60, 255, 61, 0);
        Assert.Equal([scene, first], _desk.GetPlayback().Stack.On);

        _desk.StackPrevious();
        Pass(1);
        AssertLevels(10, 200, 60, 0, 61, 0);
        Assert.Equal([scene], _desk.GetPlayback().Stack.On);

        _desk.StackPrevious();                                // before the first step: the last one, with all it stands on
        Pass(0.1);
        AssertLevels(10, 200, 60, 255, 61, 255);
        Assert.Equal([scene, first, second], _desk.GetPlayback().Stack.On);
    }

    [Fact]
    public void OfTwoStepsWithTheSameFaderTheLaterCountsAndWhenItGoesTheEarlierAgain()
    {
        Add(Memory(1, up: 0, down: 2));
        int lower = Add(Memory(1, level: 100, up: 2, down: 0) with { Add = true });
        Add(new Step(Step.Release, lower, 0, Up: 0, Wait: 0, Down: 4));
        Live();

        _desk.StackPlay();
        Pass(2);
        Assert.Equal(100, _desk.GetState().MemoryLevels[0]);

        _desk.StackPlay();                                    // the release: back to the scene's level in its four seconds
        Pass(1);
        Assert.Equal(139, _desk.GetState().MemoryLevels[0]);  // 100 + 155 / 4
        Pass(3);
        Assert.Equal(255, _desk.GetState().MemoryLevels[0]);
    }

    [Fact]
    public void RoundFromTheLastStepToTheFirstTheFirstStartsAfresh()
    {
        int first = Add(Channel(70, 255, down: 1) with { Add = true });
        Add(Channel(71, 255, down: 1) with { Add = true });
        Live();
        _desk.StackPlay();
        Pass(0.1);
        AssertLevels(70, 255, 71, 255);

        _desk.StackPlay();                                    // after the last step: the first, alone
        Pass(1);
        AssertLevels(70, 255, 71, 0);
        Assert.Equal([first], _desk.GetPlayback().Stack.On);
    }

    [Fact]
    public void AReleaseOfAStepThatIsNotOnOrOfNoneChangesNothing()
    {
        int first = Add(Channel(80, 255, down: 1));
        int later = Add(Channel(81, 255, down: 1) with { Add = true });
        Add(new Step(Step.Release, later, 0, Up: 0, Wait: 0, Down: 1));
        Add(new Step(Step.Release, 0, 0, Up: 0, Wait: 0, Down: 1));
        _desk.MoveStackStep(later, afterId: _desk.GetStack().Steps[^1].Id);   // the light to release comes last
        Live();

        _desk.StackNext();                                    // the release of a step that is not on yet
        _desk.StackNext();                                    // the release of none
        Pass(1);
        AssertLevels(80, 255, 81, 0);
        Assert.Equal([first], _desk.GetPlayback().Stack.On);

        _desk.StackNext();
        Pass(0.1);
        Assert.Equal([first, later], _desk.GetPlayback().Stack.On);
    }

    [Fact]
    public void AReleaseMustNameAnotherStepOfTheStack()
    {
        int step = Add(Channel(1, 255, down: 1));

        DeskException error = Assert.Throws<DeskException>(() => _desk.AddStackStep(new Step(Step.Release, 99, 0, 0, 0, 1)));
        Assert.Equal("The cue to release is not in the stack any more.", error.Message);
        int release = Add(new Step(Step.Release, step, 0, 0, 0, 1));
        error = Assert.Throws<DeskException>(() => _desk.UpdateStackStep(release, new Step(Step.Release, release, 0, 0, 0, 1)));
        Assert.Equal("A cue cannot release itself. Choose another cue of the stack.", error.Message);

        _desk.DeleteStackStep(step);                          // the release releases nothing from then on
        StackStep left = Assert.Single(_desk.GetStack().Steps);
        Assert.Equal((Step.Release, 0), (left.Target, left.Number));
    }

    [Fact]
    public void AProjectKeepsTheStepsThatAddAndWhatTheReleasesReleaseThroughRenumberMoveAndSaving()
    {
        int scene = Add(Memory(1, up: 0, down: 2));
        int special = Add(Memory(2, up: 2, down: 4) with { Add = true });
        int release = Add(new Step(Step.Release, special, 0, Up: 0, Wait: 1, Down: 3));
        _desk.RenumberStackStep(special, "5");               // the light to release: number 5.0, at the end
        _desk.MoveStackStep(special, afterId: scene);         // and back right after the scene: 1.1
        Assert.Equal(["1.0", "1.1", "3.0"], _desk.GetStack().Steps.Select(step => step.Cue));

        string text = ProjectFormat.WriteText(_desk.GetProject().Content);
        Assert.Contains("\"version\": 6", text);
        Assert.Contains("\"add\": true", text);
        Assert.Contains("\"releases\": \"1.1\"", text);
        Assert.DoesNotContain("\"add\": false", text);

        var loaded = new Desk(new ManualTime());
        loaded.ReplaceProject(ProjectFormat.ReadText(text));
        StackStep[] steps = loaded.GetStack().Steps;
        Assert.Equal(["1.0", "1.1", "3.0"], steps.Select(step => step.Cue));
        Assert.Equal((Step.Release, steps[1].Id), (steps[2].Target, steps[2].Number));
        Assert.True(steps[1].Add);
        Assert.False(steps[0].Add);

        // undo and redo put the project back the same way, with new names
        _desk.RestoreProject(_desk.GetProject().Content);
        StackStep[] restored = _desk.GetStack().Steps;
        Assert.Equal(restored[1].Id, restored[2].Number);
        Assert.NotEqual(release, restored[2].Id);
    }

    [Fact]
    public void AFileWithoutStepsThatAddOrReleaseStaysOfVersion5AndOldFilesLoadAsTheyWere()
    {
        Add(Memory(1, up: 0, down: 2));
        string text = ProjectFormat.WriteText(_desk.GetProject().Content);
        Assert.Contains("\"version\": 5", text);
        Assert.DoesNotContain("\"add\"", text);
        Assert.DoesNotContain("\"releases\"", text);

        const string old = """
            { "format": "lumenet-project", "version": 5, "memories": [ { "number": 1, "levels": { "10": 200 } } ],
              "stack": [ { "cue": "1.0", "target": "memory", "number": 1, "value": 255, "up": 1, "wait": 0, "down": 2 } ] }
            """;
        var loaded = new Desk(new ManualTime());
        loaded.ReplaceProject(ProjectFormat.ReadText(old));
        StackStep step = Assert.Single(loaded.GetStack().Steps);
        Assert.Equal(("memory", 1, false), (step.Target, step.Number, step.Add));
    }

    [Fact]
    public void AFileWithAReleaseWithoutAProperNumberIsRefusedAndOneOfAStepThatIsGoneReleasesNothing()
    {
        const string stack = """
            { "format": "lumenet-project", "version": 6, "memories": [], "stack": [
              { "cue": "1.0", "target": "release", "number": 0, "value": 0, "up": 0, "wait": 0, "down": 1, "releases": "x" } ] }
            """;
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => ProjectFormat.ReadText(stack));
        Assert.Contains("no proper number", error.Message);

        const string gone = """
            { "format": "lumenet-project", "version": 6, "memories": [], "stack": [
              { "cue": "1.0", "target": "release", "number": 0, "value": 0, "up": 0, "wait": 0, "down": 1, "releases": "7.0" } ] }
            """;
        var loaded = new Desk(new ManualTime());
        loaded.ReplaceProject(ProjectFormat.ReadText(gone));
        Assert.Equal(0, Assert.Single(loaded.GetStack().Steps).Number);   // a step the stack does not have: none
    }

    // ---------------------------------------------------------------- helpers

    private void Look(int memory, params (int Channel, int Level)[] levels)
    {
        _desk.Clear();
        foreach ((int channel, int level) in levels)
            _desk.SetChannel(channel, level);

        _desk.SaveMemory(memory);
        _desk.Clear();
    }

    private static Step Memory(int memory, double up, double down, int level = 255) => new("memory", memory, level, up, Wait: 0, down);

    private static Step Channel(int channel, int level, double down) => new("channel", channel, level, Up: 0, Wait: 0, down);

    private int Add(Step step) => _desk.AddStackStep(step);

    private void Live()
    {
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
