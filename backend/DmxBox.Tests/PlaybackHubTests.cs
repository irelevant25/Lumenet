using System.Text.Json;
using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// Sequences and the stack as a UI reaches them: through the hub, in the project file, and
/// through the desk clock that tells the UIs what moves by itself.
/// </summary>
public sealed class PlaybackHubTests
{
    [Fact]
    public async Task SequenceIsStoredThroughTheHubAndEveryUiGetsTheDesk()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        Assert.Empty((await ui.InvokeAsync("GetSequences")).EnumerateObject());

        var step = new { target = "channel", number = 5, value = 200, up = 1.5, wait = 0, down = 2 };
        await ui.InvokeAsync("SetSequence", 7, new { loop = true, random = true, speed = 1.5, steps = new[] { step } });

        HubMessage message = await ui.NextMessageAsync();
        Assert.Equal("DeskChanged", message.Name);
        Assert.True(message.Arguments[0].GetProperty("memoryStored")[6].GetBoolean());
        Assert.True(message.Arguments[0].GetProperty("memorySequence")[6].GetBoolean());

        JsonElement sequences = await ui.InvokeAsync("GetSequences");
        Assert.Equal(
            """{"7":{"loop":true,"steps":[{"target":"channel","number":5,"value":200,"up":1.5,"wait":0,"down":2}],"random":true,"speed":1.5}}""",
            sequences.GetRawText());
    }

    [Fact]
    public async Task SpeedOfASequenceIsChangedLikeAFaderAndTheOtherUisHearOfIt()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient mover = await server.ConnectAsync();
        await using HubClient watcher = await server.ConnectAsync();
        var step = new { target = "channel", number = 5, value = 200, up = 1, wait = 0, down = 1 };
        await mover.InvokeAsync("SetSequence", 7, new { loop = true, steps = new[] { step } });

        await mover.InvokeAsync("SetSequenceSpeed", 7, 2.33);

        HubMessage message = await watcher.NextMessageAsync();
        while (message.Name != "SequenceSpeedChanged")
            message = await watcher.NextMessageAsync();
        Assert.Equal("[7,2.35]", JsonSerializer.Serialize(message.Arguments));   // in steps of 0.05
        Assert.Equal(2.35, (await watcher.InvokeAsync("GetSequences")).GetProperty("7").GetProperty("speed").GetDouble());

        var tooFast = await Assert.ThrowsAsync<HubCallException>(() => mover.InvokeAsync("SetSequenceSpeed", 7, 11));
        Assert.Contains("The speed of a sequence must be from 0.1 to 10, not 11.", tooFast.Message);
        var noSequence = await Assert.ThrowsAsync<HubCallException>(() => mover.InvokeAsync("SetSequenceSpeed", 8, 2));
        Assert.Contains("Memory 8 holds no sequence.", noSequence.Message);
    }

    [Fact]
    public async Task StepIsAddedToASequenceThroughTheHubAndEveryUiGetsTheDesk()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await using HubClient other = await server.ConnectAsync();

        var step = new { target = "channel", number = 5, value = 255, up = 1, wait = 1, down = 1 };
        await ui.InvokeAsync("AddSequenceStep", 9, step);
        await ui.InvokeAsync("AddSequenceStep", 9, new { target = "memory", number = 2, value = 255, up = 1, wait = 1, down = 1 });

        foreach (HubClient client in new[] { ui, other })
        {
            HubMessage changed = await client.NextMessageAsync();
            Assert.Equal("DeskChanged", changed.Name);
            Assert.True(changed.Arguments[0].GetProperty("memorySequence")[8].GetBoolean());
        }

        JsonElement sequence = (await other.InvokeAsync("GetSequences")).GetProperty("9");
        Assert.True(sequence.GetProperty("loop").GetBoolean());
        Assert.Equal(
            ["channel", "memory"],
            sequence.GetProperty("steps").EnumerateArray().Select(item => item.GetProperty("target").GetString()));

        await ui.InvokeAsync("SaveMemory", 3);
        var error = await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync("AddSequenceStep", 3, step));
        Assert.Contains("Memory 3 holds a look. Empty it first.", error.Message);
    }

    [Fact]
    public async Task StackStepsAreChangedThroughTheHubAndEveryUiGetsTheStack()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await using HubClient other = await server.ConnectAsync();

        var step = new { target = "channel", number = 5, value = 200, up = 1, wait = 0, down = 2 };
        int first = (await ui.InvokeAsync("AddStackStep", step)).GetInt32();

        string stack = $$"""{"steps":[{"id":{{first}},"cue":"1.0","target":"channel","number":5,"value":200,"up":1,"wait":0,"down":2}]}""";
        foreach (HubClient client in new[] { ui, other })
        {
            HubMessage changed = await client.NextMessageAsync();
            Assert.Equal("StackChanged", changed.Name);
            Assert.Equal(stack, changed.Arguments[0].GetRawText());
            Assert.Equal("PlaybackChanged", (await client.NextMessageAsync()).Name);
            Assert.Equal("ProjectChanged", (await client.NextMessageAsync()).Name);   // the stack is part of the project
        }

        Assert.Equal(stack, (await ui.InvokeAsync("GetStack")).GetRawText());

        int second = (await ui.InvokeAsync("AddStackStep", step)).GetInt32();
        await ui.InvokeAsync("MoveStackStep", second, (object?)null);
        await ui.InvokeAsync("RenumberStackStep", first, "7,5");
        await ui.InvokeAsync("UpdateStackStep", first, new { target = "memory", number = 2, value = 9, up = 0, wait = 0, down = 0 });

        JsonElement steps = (await ui.InvokeAsync("GetStack")).GetProperty("steps");
        Assert.Equal(["0.1", "7.5"], steps.EnumerateArray().Select(item => item.GetProperty("cue").GetString()));
        Assert.Equal("memory", steps[1].GetProperty("target").GetString());

        await ui.InvokeAsync("DeleteStackStep", second);
        Assert.Equal(1, (await ui.InvokeAsync("GetStack")).GetProperty("steps").GetArrayLength());
    }

    [Fact]
    public async Task StepsThatAddAndReleasesGoThroughTheHubAndTheUndoHistory()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        int first = (await ui.InvokeAsync("AddStackStep", new { target = "channel", number = 1, value = 255, up = 0, wait = 0, down = 0 })).GetInt32();
        var light = new { target = "channel", number = 2, value = 255, up = 0, wait = 0, down = 0, add = true };
        int adds = (await ui.InvokeAsync("AddStackStep", light)).GetInt32();
        await ui.InvokeAsync("AddStackStep", new { target = "release", number = adds, value = 0, up = 0, wait = 0, down = 1 });

        JsonElement steps = (await ui.InvokeAsync("GetStack")).GetProperty("steps");
        Assert.False(steps[0].TryGetProperty("add", out _));               // sent only when it is true
        Assert.True(steps[1].GetProperty("add").GetBoolean());
        Assert.Equal(("release", adds), (steps[2].GetProperty("target").GetString(), steps[2].GetProperty("number").GetInt32()));

        // Undo puts the project back with new names: the release still names the step that adds.
        await ui.InvokeAsync("UpdateStackStep", adds, light with { add = false });
        Assert.False((await ui.InvokeAsync("GetStack")).GetProperty("steps")[1].TryGetProperty("add", out _));
        await ui.InvokeAsync("Undo");
        steps = (await ui.InvokeAsync("GetStack")).GetProperty("steps");
        Assert.True(steps[1].GetProperty("add").GetBoolean());
        Assert.Equal(steps[1].GetProperty("id").GetInt32(), steps[2].GetProperty("number").GetInt32());

        await ui.InvokeAsync("SetStackLive", true);
        await ui.InvokeAsync("StackNext");
        JsonElement stack = (await ui.InvokeAsync("GetPlayback")).GetProperty("stack");
        Assert.Equal(2, stack.GetProperty("on").GetArrayLength());           // the first step and the one that adds
        Assert.NotEqual(first, steps[0].GetProperty("id").GetInt32());       // new names after the undo
    }

    [Fact]
    public async Task AStepPutInAfterAnotherIsOneStepOfTheHistory()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        var step = new { target = "channel", number = 5, value = 200, up = 1, wait = 0, down = 2 };
        int first = (await ui.InvokeAsync("AddStackStep", step)).GetInt32();
        await ui.InvokeAsync("AddStackStep", step);

        int added = (await ui.InvokeAsync("AddStackStepAfter", first, step)).GetInt32();

        JsonElement steps = (await ui.InvokeAsync("GetStack")).GetProperty("steps");
        Assert.Equal(["1.0", "1.1", "2.0"], steps.EnumerateArray().Select(item => item.GetProperty("cue").GetString()));
        Assert.Equal(added, steps[1].GetProperty("id").GetInt32());
        Assert.Equal("""{"undo":"a new step after step 1.0 of the stack","redo":null}""", (await ui.InvokeAsync("GetHistory")).GetRawText());

        await ui.InvokeAsync("Undo");
        steps = (await ui.InvokeAsync("GetStack")).GetProperty("steps");
        Assert.Equal(["1.0", "2.0"], steps.EnumerateArray().Select(item => item.GetProperty("cue").GetString()));
    }

    [Fact]
    public async Task PlaybackTellsWhereTheStackStands()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        JsonElement idle = await ui.InvokeAsync("GetPlayback");
        Assert.Equal(["levels", "sequenceSteps", "stack", "effects"], idle.EnumerateObject().Select(property => property.Name));
        Assert.Equal(512, idle.GetProperty("levels").GetArrayLength());
        Assert.Equal(
            """{"currentId":null,"nextId":null,"state":"idle","paused":false,"remaining":0,"live":false,"on":[]}""",
            idle.GetProperty("stack").GetRawText());

        // A step that takes a quarter of an hour to come up, so that the test finds it on its way whenever it looks.
        var slow = new { target = "channel", number = 1, value = 255, up = 900, wait = 0, down = 0 };
        int id = (await ui.InvokeAsync("AddStackStep", slow)).GetInt32();
        int second = (await ui.InvokeAsync("AddStackStep", slow)).GetInt32();
        await ui.InvokeAsync("SetStackLive", true);

        JsonElement stack = (await ui.InvokeAsync("GetPlayback")).GetProperty("stack");
        Assert.True(stack.GetProperty("live").GetBoolean());
        Assert.Equal(id, stack.GetProperty("currentId").GetInt32());
        Assert.Equal(second, stack.GetProperty("nextId").GetInt32());
        Assert.Equal("fading", stack.GetProperty("state").GetString());
        Assert.True(stack.GetProperty("remaining").GetDouble() > 800);
        Assert.Equal($"[{id}]", stack.GetProperty("on").GetRawText());

        await ui.InvokeAsync("StackPause");
        Assert.True((await ui.InvokeAsync("GetPlayback")).GetProperty("stack").GetProperty("paused").GetBoolean());

        await ui.InvokeAsync("StackNext");
        await ui.InvokeAsync("StackPlay");           // a fade is running: Play leaves it alone
        await ui.InvokeAsync("StackPrevious");
        Assert.Equal(id, (await ui.InvokeAsync("GetPlayback")).GetProperty("stack").GetProperty("currentId").GetInt32());

        await ui.InvokeAsync("SetStackLive", false);
        Assert.Equal(
            $$"""{"currentId":null,"nextId":{{id}},"state":"idle","paused":false,"remaining":0,"live":false,"on":[]}""",
            (await ui.InvokeAsync("GetPlayback")).GetProperty("stack").GetRawText());
    }

    [Fact]
    public async Task GoingLiveSendsEveryUiTheDeskAndLocksWhatIsStored()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await using HubClient other = await server.ConnectAsync();

        var step = new { target = "channel", number = 7, value = 255, up = 900, wait = 0, down = 0 };
        var noSteps = await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync("SetStackLive", true));
        Assert.Contains("The stack has no steps yet. Add a step first.", noSteps.Message);
        var notLive = await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync("StackPlay"));
        Assert.Contains("The stack is not live. Press Stack first.", notLive.Message);

        await ui.InvokeAsync("AddStackStep", step);
        await ui.InvokeAsync("SetChannel", 3, 200);
        await ui.InvokeAsync("SetStackLive", true);

        foreach (HubClient client in new[] { ui, other })
        {
            HubMessage message = await client.NextMessageAsync();
            while (message.Name != "DeskChanged")
                message = await client.NextMessageAsync();

            Assert.Equal(0, message.Arguments[0].GetProperty("channels")[2].GetInt32());   // every fader went down

            HubMessage playback = await client.NextMessageAsync();
            Assert.Equal("PlaybackChanged", playback.Name);
            Assert.True(playback.Arguments[0].GetProperty("stack").GetProperty("live").GetBoolean());
        }

        const string locked = "The stack is live, so nothing that is stored can be changed. Switch Stack off first.";
        (string Method, object?[] Arguments)[] changes =
        [
            ("SaveMemory", [1]),
            ("SetChannelNote", [1, "Front"]),
            ("SetIndependent", [1, 40]),
            ("SetMemoryIndependent", [1, 40]),
            ("AddStackStep", [step]),
            ("AddSequenceStep", [2, step]),
            ("NewProject", []),
            ("LoadProject", ["show"]),
        ];
        await ui.InvokeAsync("SaveProject", "show");     // saving reads the desk and changes nothing on it
        foreach ((string method, object?[] arguments) in changes)
        {
            var error = await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync(method, arguments));
            Assert.Contains(locked, error.Message);
        }

        await ui.InvokeAsync("SetChannel", 3, 90);       // the faders are free
        await ui.InvokeAsync("Clear");
        await ui.InvokeAsync("SetStackLive", false);
        await ui.InvokeAsync("SaveMemory", 1);           // and with the stack off the lock is gone
    }

    [Fact]
    public async Task DeskClockTellsTheUisTheFadersTheStackMovesAndThePlayback()
    {
        var time = new ManualTime();
        var desk = new Desk(time);
        var uis = new Listener();
        var clock = new DeskClock(desk, uis, Microsoft.Extensions.Logging.Abstractions.NullLogger<DeskClock>.Instance);

        await clock.StepAsync();
        Assert.Empty(uis.Messages);                  // nothing moves: nothing to tell

        desk.AddStackStep(new Step("channel", 7, 200, Up: 1, Wait: 0, Down: 0));
        desk.AddStackStep(new Step("memory", 3, 100, Up: 0, Wait: 0, Down: 0));
        desk.SetStackLive(true);
        time.Pass(0.5);
        await clock.StepAsync();
        Assert.Contains("ChannelChanged 7 100", uis.Messages);

        await clock.StepAsync();
        await clock.StepAsync();                     // every third round carries the playback
        Assert.Contains("PlaybackChanged fading", uis.Messages);

        uis.Messages.Clear();
        time.Pass(1);
        await clock.StepAsync();                     // the fade is over: one last playback, then silence
        Assert.Equal(["ChannelChanged 7 200", "PlaybackChanged holding"], uis.Messages);
        uis.Messages.Clear();
        await clock.StepAsync();
        await clock.StepAsync();
        await clock.StepAsync();
        Assert.Empty(uis.Messages);

        desk.StackPlay();
        time.Pass(0.1);
        await clock.StepAsync();
        Assert.Contains("MemoryLevelChanged 3 100", uis.Messages);
        Assert.Contains("ChannelChanged 7 0", uis.Messages);
    }

    [Fact]
    public async Task WhileAnEffectRunsEveryRoundOfTheClockCarriesThePlayback()
    {
        var time = new ManualTime();
        var desk = new Desk(time);
        var uis = new Listener();
        var clock = new DeskClock(desk, uis, Microsoft.Extensions.Logging.Abstractions.NullLogger<DeskClock>.Instance);
        desk.SetEffect(new Effect(1, "Pulse", Effect.IntensityKind, "pulse"));
        desk.SetEffectControl(1, "go", 1);

        for (int round = 0; round < 3; round++)
        {
            time.Pass(0.033);
            await clock.StepAsync();
        }

        Assert.Equal(3, uis.Messages.Count(message => message.StartsWith("PlaybackChanged", StringComparison.Ordinal)));
    }

    [Fact]
    public void ProjectFileKeepsSequencesAndTheStack()
    {
        var desk = new Desk();
        desk.SetChannel(1, 50);
        desk.SaveMemory(1);
        desk.SetSequence(2, new Sequence(Loop: true, [new Step("memory", 1, 255, 0.5, 1, 2), new Step("channel", 9, 10, 0, 0, 0)])
        {
            Random = true,
            Speed = 0.35,
        });
        desk.AddStackStep(new Step("channel", 3, 255, 1, 2, 3));
        int second = desk.AddStackStep(new Step("memory", 2, 128, 0, 0, 0));
        desk.RenumberStackStep(second, "0.5");

        using var file = new MemoryStream();
        ProjectFormat.Write(file, desk.GetProject().Content);
        file.Position = 0;
        ProjectContent read = ProjectFormat.Read(file);

        var loaded = new Desk();
        loaded.ReplaceProject(read);
        Assert.Equal(desk.GetSequences()[2].Steps, loaded.GetSequences()[2].Steps);
        Assert.True(loaded.GetSequences()[2].Loop);
        Assert.True(loaded.GetSequences()[2].Random);
        Assert.Equal(0.35, loaded.GetSequences()[2].Speed);
        Assert.Equal(["0.5", "1.0"], loaded.GetStack().Steps.Select(step => step.Cue));
        Assert.Equal("memory", loaded.GetStack().Steps[0].Target);
        Assert.Equal(3, loaded.GetStack().Steps[1].Down);
    }

    [Fact]
    public void SequenceInAFileThatDoesNotSayIsPlayedInALoop()
    {
        ProjectContent read = Read(
            """
            { "format": "lumenet-project", "version": 2, "memories": [], "sequences": [
              { "number": 1, "steps": [] }, { "number": 2, "loop": false, "steps": [] }, { "number": 3, "loop": true, "steps": [] } ] }
            """);

        Assert.Equal([true, false, true], read.Sequences.Take(3).Select(sequence => sequence!.Loop));
        Assert.All(read.Sequences.Take(3), sequence => Assert.False(sequence!.Random));   // nor at random,
        Assert.All(read.Sequences.Take(3), sequence => Assert.Equal(1, sequence!.Speed));  // and at speed 1
    }

    [Fact]
    public void ProjectFileFromBeforeSequencesIsStillRead()
    {
        ProjectContent old = Read("""{ "format": "lumenet-project", "version": 1, "memories": [ { "number": 4, "levels": { "9": 90 } } ] }""");

        Assert.All(old.Sequences, sequence => Assert.Null(sequence));
        Assert.Empty(old.Stack);
        Assert.NotNull(old.Looks[3]);
    }

    [Theory]
    [InlineData("""  "sequences": [ { "number": 4, "loop": false, "steps": [] } ]  """, "lists memory 4 twice")]
    [InlineData("""  "sequences": [ { "number": 200, "steps": [] } ]  """, "from 1 to 128")]
    [InlineData("""  "sequences": [ { "number": 5 } ]  """, "has no steps list")]
    [InlineData("""  "sequences": [ { "number": 5, "steps": [ { "target": "channel", "number": 5000, "value": 1 } ] } ]  """, "Step 1 of the sequence of memory 5")]
    [InlineData("""  "sequences": [ { "number": 5, "steps": [ { "target": "channel", "number": 600, "value": 1 } ] } ]  """, "The project has 1 universe, but step 1 of the sequence of memory 5 is on channel 88 (U2).")]
    [InlineData("""  "sequences": [ { "number": 5, "speed": 0.05, "steps": [] } ]  """, "The speed of a sequence must be from 0.1 to 10")]
    [InlineData("""  "stack": [ { "cue": "1.0", "target": "channel", "number": 1, "value": 1 }, { "cue": "1", "target": "channel", "number": 2, "value": 1 } ]  """, "step 1.0 of the stack twice")]
    [InlineData("""  "stack": [ { "cue": "first", "target": "channel", "number": 1, "value": 1 } ]  """, "looks like 3 or 2.1")]
    [InlineData("""  "stack": [ { "cue": "1.0", "target": "channel", "number": 1 } ]  """, "is not complete")]
    [InlineData("""  "stack": [ { "cue": "1.0", "target": "channel", "number": 1, "value": 1, "up": -2 } ]  """, "The up time")]
    public void ProjectFileWithABadSequenceOrStackIsRefused(string part, string expectedMessage)
    {
        string json = $$"""{ "format": "lumenet-project", "version": 2, "memories": [ { "number": 4, "levels": {} } ], {{part}} }""";

        var error = Assert.Throws<InvalidDataException>(() => Read(json));
        Assert.Contains(expectedMessage, error.Message);
    }

    private static ProjectContent Read(string json)
    {
        using var file = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return ProjectFormat.Read(file);
    }

    // Stands in for "every UI" and writes down what the desk clock tells them.
    private sealed class Listener : IDeskClient
    {
        public List<string> Messages { get; } = [];

        public Task ChannelChanged(int channel, int value) => Note($"ChannelChanged {channel} {value}");

        public Task MemoryLevelChanged(int memory, int value) => Note($"MemoryLevelChanged {memory} {value}");

        public Task PlaybackChanged(Playback playback) => Note($"PlaybackChanged {playback.Stack.State}");

        public Task MasterChanged(int value) => throw new NotSupportedException();

        public Task ChannelNoteChanged(int channel, string text) => throw new NotSupportedException();

        public Task MemoryNoteChanged(int memory, string text) => throw new NotSupportedException();

        public Task IndependentChanged(int index, int channel) => throw new NotSupportedException();

        public Task MemoryIndependentChanged(int index, int memory) => throw new NotSupportedException();

        public Task BlackoutChanged(bool on) => throw new NotSupportedException();

        public Task DeskChanged(DeskState desk) => throw new NotSupportedException();

        public Task BoxesChanged(BoxView[] boxes) => throw new NotSupportedException();

        public Task OutputsChanged(OutputView[] outputs) => throw new NotSupportedException();

        public Task ProjectChanged(ProjectInfo project) => throw new NotSupportedException();

        public Task StackChanged(StackState stack) => throw new NotSupportedException();

        public Task SequenceSpeedChanged(int memory, double speed) => throw new NotSupportedException();

        public Task LanAccessChanged(LanState lan) => throw new NotSupportedException();

        public Task NotesChanged(string notes) => throw new NotSupportedException();

        public Task HistoryChanged(HistoryState history) => throw new NotSupportedException();

        public Task RateChanged(bool on, double rate) => throw new NotSupportedException();

        public Task AutosaveChanged(AutosaveOptions options) => throw new NotSupportedException();

        public Task StageChanged(StagePlan plan) => throw new NotSupportedException();

        public Task SoloChanged(SoloState solo) => throw new NotSupportedException();

        public Task EffectsChanged(Effect[] effects) => throw new NotSupportedException();

        public Task EffectPlaybackChanged(EffectPlayback playback) =>
            Note($"EffectPlaybackChanged {playback.Number} {playback.Level} {(playback.Running ? "running" : "out")}");

        public Task EffectIndependentChanged(int index, int effect) => throw new NotSupportedException();

        // The MIDI of the test desk: no controllers, nothing to note.
        public Task MidiChanged(MidiState state) => Task.CompletedTask;

        public Task MidiMessages(MidiMonitorLine[] lines) => Task.CompletedTask;

        public Task MidiKey(string key, bool down) => Task.CompletedTask;

        public Task MidiRole(MidiRole role) => Task.CompletedTask;

        private Task Note(string message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
