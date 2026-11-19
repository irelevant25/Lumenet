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
        await ui.InvokeAsync("SetSequence", 7, true, new[] { step });

        HubMessage message = await ui.NextMessageAsync();
        Assert.Equal("DeskChanged", message.Name);
        Assert.True(message.Arguments[0].GetProperty("memoryStored")[6].GetBoolean());
        Assert.True(message.Arguments[0].GetProperty("memorySequence")[6].GetBoolean());

        JsonElement sequences = await ui.InvokeAsync("GetSequences");
        Assert.Equal(
            """{"7":{"loop":true,"steps":[{"target":"channel","number":5,"value":200,"up":1.5,"wait":0,"down":2}]}}""",
            sequences.GetRawText());
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
    public async Task PlaybackTellsWhereTheStackStands()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        JsonElement idle = await ui.InvokeAsync("GetPlayback");
        Assert.Equal(["levels", "sequenceSteps", "stack"], idle.EnumerateObject().Select(property => property.Name));
        Assert.Equal(512, idle.GetProperty("levels").GetArrayLength());
        Assert.Equal(
            """{"currentId":null,"nextId":null,"state":"idle","paused":false,"remaining":0}""",
            idle.GetProperty("stack").GetRawText());

        // A step that takes an hour to come up, so that the test finds it on its way whenever it looks.
        var slow = new { target = "channel", number = 1, value = 255, up = 900, wait = 0, down = 0 };
        int id = (await ui.InvokeAsync("AddStackStep", slow)).GetInt32();
        await ui.InvokeAsync("StackPlay");

        JsonElement stack = (await ui.InvokeAsync("GetPlayback")).GetProperty("stack");
        Assert.Equal(id, stack.GetProperty("currentId").GetInt32());
        Assert.Equal("fading", stack.GetProperty("state").GetString());
        Assert.True(stack.GetProperty("remaining").GetDouble() > 800);

        await ui.InvokeAsync("StackPause");
        Assert.True((await ui.InvokeAsync("GetPlayback")).GetProperty("stack").GetProperty("paused").GetBoolean());

        await ui.InvokeAsync("StackPrevious");       // back to "no step is on"
        Assert.Equal("idle", (await ui.InvokeAsync("GetPlayback")).GetProperty("stack").GetProperty("state").GetString());
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
        desk.StackPlay();
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
    public void ProjectFileKeepsSequencesAndTheStack()
    {
        var desk = new Desk();
        desk.SetChannel(1, 50);
        desk.SaveMemory(1);
        desk.SetSequence(2, loop: true, [new Step("memory", 1, 255, 0.5, 1, 2), new Step("channel", 9, 10, 0, 0, 0)]);
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
        Assert.Equal(["0.5", "1.0"], loaded.GetStack().Steps.Select(step => step.Cue));
        Assert.Equal("memory", loaded.GetStack().Steps[0].Target);
        Assert.Equal(3, loaded.GetStack().Steps[1].Down);
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
    [InlineData("""  "sequences": [ { "number": 5, "steps": [ { "target": "channel", "number": 600, "value": 1 } ] } ]  """, "Step 1 of the sequence of memory 5")]
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

        public Task BlackoutChanged(bool on) => throw new NotSupportedException();

        public Task DeskChanged(DeskState desk) => throw new NotSupportedException();

        public Task BoxChanged(BoxStatus box) => throw new NotSupportedException();

        public Task ProjectChanged(ProjectInfo project) => throw new NotSupportedException();

        public Task StackChanged(StackState stack) => throw new NotSupportedException();

        private Task Note(string message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
