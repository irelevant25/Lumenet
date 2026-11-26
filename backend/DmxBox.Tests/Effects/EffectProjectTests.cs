using System.Text.Json;

namespace DmxBox.Tests.Effects;

/// <summary>
/// Effects as part of the project: in the project file (version 7 only with effects, their lights by
/// their places in the plan, older files still read), undo and redo, a new project and loading; and
/// through the hub: storing, the playback for every UI, what the effects give for the UIs, the
/// preview, and the way in from outside ("effect 3 speed = 120 BPM").
/// </summary>
public sealed class EffectProjectTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;

    public EffectProjectTests()
    {
        _desk = new Desk(_time);
    }

    private static Effect Circle(int number = 3) => new(number, "Circle", Effect.MovementKind, "circle")
    {
        Lights = [7, 9],
        Speed = 15,
        Size = 20,
        Spread = 50,
        Order = Effect.LeftToRight,
        Wings = 2,
        FadeIn = 1.5,
        CenterPan = 50,
        CenterTilt = 25,
        Seed = 4,
    };

    // Two heads with numbers that are not their places in the plan, and a group of both.
    private static StagePlan TwoHeads() => new(16, 10,
    [
        new StageLight(7, "Left", 1, "PTD", null, 2, 2, 0),
        new StageLight(9, "Right", 4, "PTD", null, 8, 2, 0),
    ])
    { Groups = [new LightGroup(5, "Heads", [7, 9])] };

    [Fact]
    public void AProjectWithEffectsIsWrittenAsVersion7AndReadBackWhole()
    {
        _desk.SetStage(TwoHeads());
        _desk.SetEffect(Circle());
        _desk.SetEffect(new Effect(4, "Wash", Effect.ColorKind, "fade") { Group = 5, Colors = ["#ff0000", "#00ff00"], Duty = 30 });
        _desk.SetEffectIndependent(2, 4);
        _desk.AddStackStep(new Step(Step.EffectTarget, 3, 200, 1, 0, 2));

        string text = ProjectFormat.WriteText(_desk.GetProject().Content);
        using JsonDocument file = JsonDocument.Parse(text);
        Assert.Equal(7, file.RootElement.GetProperty("version").GetInt32());
        JsonElement circle = file.RootElement.GetProperty("effects")[0];
        Assert.Equal([1, 2], circle.GetProperty("lights").EnumerateArray().Select(light => light.GetInt32()));   // by their places in the plan
        Assert.Equal([50.0, 25.0], circle.GetProperty("center").EnumerateArray().Select(part => part.GetDouble()));
        Assert.False(circle.TryGetProperty("direction", out _));             // forward, as unless it says
        Assert.Equal(1, file.RootElement.GetProperty("effects")[1].GetProperty("group").GetInt32());

        ProjectContent read = ProjectFormat.ReadText(text);
        var loaded = new Desk(_time);
        loaded.ReplaceProject(read);
        Effect[] effects = loaded.GetEffects();
        Assert.Equal([3, 4], effects.Select(effect => effect.Number));
        Assert.True(effects[0].SameAs(Circle() with { Lights = [1, 2] }));   // the lights numbered from 1, as the plan is read
        Assert.Equal(["#ff0000", "#00ff00"], effects[1].Colors);
        Assert.Equal(1, effects[1].Group);
        Assert.Equal(4, loaded.GetState().EffectIndependents[1]);
        Assert.Equal(Step.EffectTarget, loaded.GetStack().Steps[0].Target);
    }

    [Fact]
    public void AProjectWithoutEffectsStaysVersion5AndOlderFilesHaveNone()
    {
        string text = ProjectFormat.WriteText(_desk.GetProject().Content);
        Assert.Contains("\"version\": 5", text, StringComparison.Ordinal);
        Assert.DoesNotContain("effect", text, StringComparison.OrdinalIgnoreCase);

        ProjectContent old = ProjectFormat.ReadText("""{ "format": "lumenet-project", "version": 6, "memories": [] }""");
        Assert.All(old.Effects, effect => Assert.Null(effect));
        Assert.Equal(Desk.DefaultEffectIndependents(), old.EffectIndependents);
    }

    [Fact]
    public void AnEffectKeyBoundElsewhereIsKeptAlsoWithoutEffects()
    {
        _desk.SetEffectIndependent(1, 5);   // effect 5 is made later, or was deleted

        string text = ProjectFormat.WriteText(_desk.GetProject().Content);
        Assert.Contains("\"version\": 7", text, StringComparison.Ordinal);
        Assert.Equal(5, ProjectFormat.ReadText(text).EffectIndependents[0]);
    }

    [Theory]
    [InlineData("""{ "number": 0, "kind": "color", "shape": "rainbow" }""", "without a number from 1 to 64")]
    [InlineData("""{ "number": 2, "kind": "laser", "shape": "rainbow" }""", "Effect 2 in the file cannot be used. Effect 2 is a movement, an intensity, a color or a pixel effect")]
    [InlineData("""{ "number": 2, "kind": "color", "shape": "circle" }""", "a color effect has no shape \"circle\"")]
    [InlineData("""{ "number": 2, "kind": "color", "shape": "chase", "colors": [ "red" ] }""", "\"red\" is no color like #ff2000")]
    [InlineData("""{ "number": 2, "kind": "movement", "shape": "circle", "center": [ 50 ] }""", "not a pair of numbers")]
    [InlineData("""{ "number": 2, "kind": "intensity", "shape": "sine", "speed": 1000 }""", "the speed must be from 0.5 to 600 BPM")]
    public void AFileWithAnEffectTheDeskCannotUseIsRefused(string effect, string expectedInMessage)
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            ProjectFormat.ReadText($$"""{ "format": "lumenet-project", "version": 7, "memories": [], "effects": [ {{effect}} ] }"""));
        Assert.Contains(expectedInMessage, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UndoBringsAnEffectBackAndARunningEffectTakesTheValuesItHadThen()
    {
        _desk.SetStage(TwoHeads());
        var history = new UndoHistory(_desk, _time);
        using (history.Change("effect 3"))
            _desk.SetEffect(Circle() with { Speed = 15 });
        _desk.SetEffectControl(3, "go", 1);

        using (history.Change("effect 3"))
            _desk.SetEffect(Circle() with { Speed = 45 });
        Assert.Equal(45, _desk.GetEffectPlayback(3).Speed);

        Assert.Equal("effect 3", history.Undo());
        Assert.Equal(15, _desk.GetEffects()[0].Speed);
        Assert.Equal(15, _desk.GetEffectPlayback(3).Speed);
        Assert.True(_desk.GetEffectPlayback(3).Running);

        history.Undo();
        Assert.Empty(_desk.GetEffects());
        Assert.False(_desk.GetEffectPlayback(3).Running);
    }

    [Fact]
    public void ANewProjectHasNoEffectsAndLoadingOneStopsEveryEffect()
    {
        _desk.SetStage(TwoHeads());
        _desk.SetEffect(Circle());
        _desk.SetEffectIndependent(1, 9);
        _desk.SetEffectControl(3, "go", 1);
        ProjectContent saved = _desk.GetProject().Content;

        _desk.ReplaceProject(saved);
        Assert.False(_desk.GetEffectPlayback(3).Running);
        Assert.Equal(0, _desk.GetEffectPlayback(3).Level);

        _desk.StartNewProject();
        Assert.Empty(_desk.GetEffects());
        Assert.Equal(Desk.DefaultEffectIndependents(), _desk.GetState().EffectIndependents);
    }

    [Fact]
    public async Task EffectsAreStoredAndPlayedThroughTheHubAndEveryUiHearsOfIt()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await using HubClient other = await server.ConnectAsync();
        server.Box.PlugIn();
        await ui.InvokeAsync("SetStage", new
        {
            width = 16,
            depth = 10,
            lights = new[] { new { id = 1, name = "PAR", address = 1, channels = "DRGB", color = (string?)null, x = 2.0, y = 2.0, angle = 0 } },
        });

        JsonElement kept = await ui.InvokeAsync("SetEffect", new { number = 2, name = " Pulse ", kind = "intensity", shape = "pulse", lights = new[] { 1 }, speed = 60, spread = 0 });
        Assert.Equal("Pulse", kept.GetProperty("name").GetString());
        HubMessage effects = await NextNamedAsync(other, "EffectsChanged");
        Assert.Equal("Pulse", effects.Arguments[0][0].GetProperty("name").GetString());
        Assert.Equal(60, (await NextNamedAsync(other, "EffectPlaybackChanged")).Arguments[0].GetProperty("speed").GetDouble());

        // From outside, as a MIDI controller will: effect 2 at 120 BPM, then started.
        JsonElement faster = await ui.InvokeAsync("SetEffectControl", 2, "speed", 120);
        Assert.Equal(120, faster.GetProperty("speed").GetDouble());
        Assert.Equal(120, (await NextNamedAsync(other, "EffectPlaybackChanged")).Arguments[0].GetProperty("speed").GetDouble());

        JsonElement started = await ui.InvokeAsync("SetEffectControl", 2, "go", 1);
        Assert.True(started.GetProperty("running").GetBoolean());
        await Wait.UntilAsync(() => server.Box.Levels[0] == 255, "The effect did not reach the box.");

        JsonElement playback = await ui.InvokeAsync("GetPlayback");
        Assert.Equal(2, playback.GetProperty("effects")[0].GetProperty("number").GetInt32());
        Assert.Contains(1, playback.GetProperty("effectLevels").GetProperty("channels").EnumerateArray().Select(channel => channel.GetInt32()));

        JsonElement preview = await ui.InvokeAsync("PreviewEffect", new { number = 2, name = "Pulse", kind = "intensity", shape = "pulse", lights = new[] { 1 } });
        Assert.Equal(48, preview.GetProperty("frames").GetInt32());
        Assert.Equal(48, preview.GetProperty("items")[0].GetProperty("levels").GetArrayLength());

        var refused = await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync("SetEffectControl", 2, "size", 150));
        Assert.Contains("0 to 100 per cent", refused.Message, StringComparison.Ordinal);

        await ui.InvokeAsync("DeleteEffect", 2);
        Assert.Equal("[]", (await ui.InvokeAsync("GetEffects")).GetRawText());
        await Wait.UntilAsync(() => server.Box.Levels[0] == 0, "The effect went on after it was taken out.");
    }

    // The next message with this name; the others on the way are passed over.
    private static async Task<HubMessage> NextNamedAsync(HubClient ui, string name)
    {
        HubMessage message = await ui.NextMessageAsync();
        while (message.Name != name)
            message = await ui.NextMessageAsync();

        return message;
    }
}
