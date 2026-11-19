using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// The example project, examples\Example show.json in the repository: it has to stay loadable,
/// in the current format, and show every kind of thing a project holds, without a step or a key
/// that points at an empty memory.
/// </summary>
public sealed class ExampleProjectTests : IDisposable
{
    private const string Name = "Example show";

    /// <summary>What a note has between its two lines.</summary>
    private const char SecondLine = (char)0x2028;

    /// <summary>The example as the repository has it; the test project copies it next to the tests.</summary>
    private static readonly string Example = Path.Combine(AppContext.BaseDirectory, "examples", Name + ".json");

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("lumenet-test-");
    private readonly Desk _desk = new();
    private readonly ProjectStore _store;

    /// <summary>Every test starts with a new desk and a projects folder that holds only the example.</summary>
    public ExampleProjectTests()
    {
        _store = new ProjectStore(_desk, _folder.FullName);
        File.Copy(Example, Path.Combine(_folder.FullName, Name + ".json"));
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
    public void ExampleLoads()
    {
        ProjectInfo loaded = _store.Load(Name);

        Assert.Equal(Name, loaded.Name);
        Assert.False(loaded.Unsaved);
    }

    [Fact]
    public void ExampleIsInTheCurrentFormat()
    {
        // Saved again, it gives the same file. If the format changes, load the example in the
        // desk, save it, and copy the file over examples\Example show.json.
        _store.Load(Name);
        _store.Save("Saved again");

        string again = File.ReadAllText(Path.Combine(_folder.FullName, "Saved again.json"));
        Assert.Equal(WithoutCarriageReturns(File.ReadAllText(Example)), WithoutCarriageReturns(again));
    }

    [Fact]
    public void ExampleShowsEveryKindOfThing()
    {
        _store.Load(Name);
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
    }

    [Fact]
    public void EveryMemoryTheExampleUsesHoldsSomethingAndHasANote()
    {
        _store.Load(Name);
        ProjectContent content = _desk.GetProject().Content;
        bool Stored(int memory) => content.Looks[memory - 1] is not null || content.Sequences[memory - 1] is not null;

        IEnumerable<int> used = content.Sequences.OfType<Sequence>()
            .SelectMany(sequence => sequence.Steps)
            .Concat(_desk.GetStack().Steps.Select(step => new Step(step.Target, step.Number, step.Value, step.Up, step.Wait, step.Down)))
            .Where(step => step.Target == Step.Memory)
            .Select(step => step.Number)
            .Concat(content.MemoryIndependents);
        Assert.All(used.Distinct(), memory => Assert.True(Stored(memory), $"memory {memory} is used but empty"));

        for (int memory = 1; memory <= Desk.MemoryCount; memory++)
        {
            if (Stored(memory))
                Assert.False(string.IsNullOrEmpty(content.MemoryNotes[memory - 1]), $"memory {memory} has no note");
        }
    }

    private static string WithoutCarriageReturns(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
