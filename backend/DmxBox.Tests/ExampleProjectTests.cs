using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// The example projects of the repository (examples\*.json), which the Load window lists: they
/// have to stay loadable, in the current format, with notes on how to use them, and without a step
/// or a key that points at nothing. The first ones show one thing each, the last ones all of it.
/// </summary>
public sealed class ExampleProjectTests : IDisposable
{
    /// <summary>What a note has between its two lines.</summary>
    private const char SecondLine = (char)0x2028;

    /// <summary>The memory keys of the independents a desk shows unless its settings show more.</summary>
    private const int ShownMemoryKeys = 10;

    /// <summary>The examples as the repository has them; the desk program brings them along (DmxBox.Server.csproj).</summary>
    private static readonly string Examples = Path.Combine(AppContext.BaseDirectory, "examples");

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("lumenet-test-");
    private readonly Desk _desk = new();
    private readonly ProjectStore _store;

    /// <summary>Every test starts with a new desk, an empty projects folder, and the examples.</summary>
    public ExampleProjectTests()
    {
        _store = new ProjectStore(_desk, _folder.FullName, Examples);
    }

    /// <summary>The names of the examples, for the tests that go through each of them.</summary>
    public static TheoryData<string> Names()
    {
        var names = new TheoryData<string>();
        foreach (string file in Directory.EnumerateFiles(Examples, "*.json").Order(StringComparer.Ordinal))
            names.Add(Path.GetFileNameWithoutExtension(file));

        return names;
    }

    /// <summary>Deletes the test's projects folder.</summary>
    public void Dispose()
    {
        try
        {
            _folder.Delete(recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A leftover folder in the temp directory is harmless. It must not fail a test.
        }
    }

    [Fact]
    public void TheExamplesGoFromOneThingToAllOfIt()
    {
        Assert.Equal(
            ["1 Memories and the stack", "2 Sequences", "3 Fixtures and the stage", "4 Moving heads", "5 Theatre show", "6 Concert", "7 Effects"],
            _store.List().Examples);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void ExampleOpensWithNotesAndIsInTheCurrentFormat(string name)
    {
        ProjectInfo opened = _store.OpenExample(name);
        Assert.Equal(new ProjectInfo(name, Path: null, Unsaved: false), opened);
        Assert.NotEqual("", _desk.Notes);

        // Saved, it gives the same file. If the format changes, open the example in the desk, save
        // it, and copy the file over the one in examples.
        _store.Save("Saved again");
        string again = File.ReadAllText(Path.Combine(_folder.FullName, "Saved again.json"));
        string example = File.ReadAllText(Path.Combine(Examples, name + ".json"));
        Assert.Equal(WithoutCarriageReturns(example), WithoutCarriageReturns(again));

        // An example brings no settings or shortcuts: they belong to each device.
        Assert.DoesNotContain("\"settings\"", example, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void EveryMemoryLightAndGroupAnExampleUsesIsThere(string name)
    {
        _store.OpenExample(name);
        ProjectContent content = _desk.GetProject().Content;
        StagePlan stage = _desk.Stage;
        bool Stored(int memory) => content.Looks[memory - 1] is not null || content.Sequences[memory - 1] is not null;

        Step[] steps = [
            .. content.Sequences.OfType<Sequence>().SelectMany(sequence => sequence.Steps),
            .. _desk.GetStack().Steps.Select(step => new Step(step.Target, step.Number, step.Value, step.Up, step.Wait, step.Down)),
        ];
        foreach (Step step in steps)
        {
            switch (step.Target)
            {
                case Step.Memory:
                    Assert.True(Stored(step.Number), $"memory {step.Number} is used but empty");
                    break;
                case Step.Fixture:
                    Assert.Contains(stage.Lights, light => light.Id == step.Number);
                    break;
                case Step.Group:
                    Assert.Contains(stage.Groups, group => group.Id == step.Number && group.Lights.Length > 0);
                    break;
                case Step.EffectTarget:
                    Assert.True(content.Effects[step.Number - 1] is not null, $"effect {step.Number} is used but not there");
                    break;
            }
        }

        // Every effect works on lights that are there: a group with lights, or lights of the plan.
        foreach (Effect effect in content.Effects.OfType<Effect>())
        {
            if (effect.Group is int group)
                Assert.Contains(stage.Groups, other => other.Id == group && other.Lights.Length > 0);
            else
                Assert.All(effect.Lights, light => Assert.Contains(stage.Lights, other => other.Id == light));
        }

        // The memory keys of the independents the example chose (not those where a new desk has them).
        IEnumerable<int> chosen = content.MemoryIndependents.Take(ShownMemoryKeys).Where((memory, key) => memory != key + 1);
        Assert.All(chosen, memory => Assert.True(Stored(memory), $"memory key on memory {memory}, which is empty"));

        for (int memory = 1; memory <= Desk.MemoryCount; memory++)
        {
            if (Stored(memory))
                Assert.False(string.IsNullOrEmpty(content.MemoryNotes[memory - 1]), $"memory {memory} has no note");
        }
    }

    [Fact]
    public void TheTheatreShowShowsEveryKindOfThingOfMemories()
    {
        _store.OpenExample("5 Theatre show");
        ProjectContent content = _desk.GetProject().Content;
        Sequence[] sequences = content.Sequences.OfType<Sequence>().ToArray();
        StackStep[] stack = _desk.GetStack().Steps;

        Assert.True(content.Looks.Count(look => look is not null) >= 5, "a few looks");
        Assert.True(sequences.Length >= 5, "a few sequences");
        Assert.Contains(sequences, sequence => sequence.Random);
        Assert.Contains(sequences, sequence => !sequence.Loop);
        Assert.Contains(sequences, sequence => sequence.Speed > 1);
        Assert.Contains(sequences, sequence => sequence.Speed < 1);
        Assert.Contains(sequences, sequence => sequence.Steps.Any(step => step.Target == Step.Channel));
        Assert.Contains(sequences, sequence => sequence.Steps.Any(step => step.Target == Step.Memory));

        Assert.Contains(content.ChannelNotes, note => note.Contains(SecondLine));
        Assert.Contains(content.MemoryNotes, note => note.Contains(SecondLine));
        Assert.NotEqual(Desk.DefaultIndependents(), content.Independents);
        Assert.NotEqual(Desk.DefaultMemoryIndependents(), content.MemoryIndependents);

        Assert.Contains(stack, step => step.Target == Step.Channel);
        Assert.Contains(stack, step => step.Target == Step.Memory);
        Assert.Contains(stack, step => step.Wait > 0); // goes on by itself
        Assert.Contains(stack, step => !step.Cue.EndsWith(".0", StringComparison.Ordinal)); // put in between
        Assert.NotNull(content.StagePicture);
    }

    [Fact]
    public void TheStackOfFixturesAndTheStageIsMadeOfLightsAndGroupsOnly()
    {
        _store.OpenExample("3 Fixtures and the stage");
        StackStep[] stack = _desk.GetStack().Steps;

        Assert.All(stack, step => Assert.Contains(step.Target, new[] { Step.Fixture, Step.Group }));
        Assert.Contains(stack, step => step.Target == Step.Fixture);
        Assert.Contains(stack, step => step.Target == Step.Group);
        Assert.All(stack.Where(step => step.Target == Step.Group), step => Assert.NotNull(step.Color));
        Assert.DoesNotContain(_desk.GetProject().Content.Looks, look => look is not null);
        Assert.True(_desk.Stage.Groups.Length >= 2, "groups of lights");
    }

    [Fact]
    public void TheMovingHeadsComeFromTheFixtureLibrary()
    {
        _store.OpenExample("4 Moving heads");
        FixtureType head = Assert.Single(_desk.Stage.Types);

        Assert.Equal("american-dj/inno-pocket-spot", head.Source);
        Assert.Equal("PTXXSDXXX", string.Concat(head.Channels.Select(channel => channel.Code)));
        Assert.Equal(2, _desk.Stage.Lights.Count(light => light.Type == head.Id));
    }

    [Fact]
    public void TheConcertUsesAllOfItTogether()
    {
        _store.OpenExample("6 Concert");
        ProjectContent content = _desk.GetProject().Content;
        StackStep[] stack = _desk.GetStack().Steps;

        Assert.Equal([Step.Channel, Step.Fixture, Step.Group, Step.Memory], stack.Select(step => step.Target).Distinct().Order(StringComparer.Ordinal));
        Assert.Contains(content.Sequences.OfType<Sequence>(), sequence => sequence.Steps.Any(step => step.Target == Step.Fixture));
        Assert.Contains(content.Sequences.OfType<Sequence>(), sequence => sequence.Steps.Any(step => step.Target == Step.Memory));
        Assert.NotEmpty(_desk.Stage.Types);
        Assert.True(_desk.Stage.Groups.Length >= 4, "groups of lights");
    }

    [Fact]
    public void TheEffectsExampleHasAnEffectOfEveryKindAndAStackThatPlaysThem()
    {
        _store.OpenExample("7 Effects");
        Effect[] effects = _desk.GetEffects();
        StackStep[] stack = _desk.GetStack().Steps;

        Assert.Equal(
            [Effect.ColorKind, Effect.IntensityKind, Effect.MovementKind, Effect.PixelKind],
            effects.Select(effect => effect.Kind).Distinct().Order(StringComparer.Ordinal));
        Assert.Contains(stack, step => step.Target == Step.EffectTarget && step.Add);
        Assert.Contains(stack, step => step.Target == Step.Release && step.Number > 0);
        Assert.Contains(_desk.Stage.Lights, light => light.Channels.Length == 24);   // a bar of eight cells, for the patterns

        // Every effect starts from its fader, and once it is out the output is as it was.
        foreach (Effect effect in effects)
        {
            byte[] before = _desk.GetOutput();
            _desk.SetEffectControl(effect.Number, "level", 100);
            Assert.True(_desk.GetEffectPlayback(effect.Number).Running);
            _desk.SetEffectControl(effect.Number, "level", 0);
            Assert.Equal(before, _desk.GetOutput());
        }
    }

    private static string WithoutCarriageReturns(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
