using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// The example show file of a theatre console, examples\Example theatre show.asc in the
/// repository: IMPORT has to take all of it, and say only what is true of it.
/// </summary>
public class ExampleShowFileTests
{
    private const string FileName = "Example theatre show.asc";

    /// <summary>The file as the repository has it; the test project copies it next to the tests.</summary>
    private static readonly string ShowFile = Path.Combine(AppContext.BaseDirectory, "examples", FileName);

    [Fact]
    public void ExampleIsImportedWhole()
    {
        ImportedShow show = ShowImport.Read(FileName, File.ReadAllText(ShowFile));

        Assert.Equal("Example theatre show", show.Name);
        Assert.Equal(19, show.Content.Looks.Count(look => look is not null));
        Assert.Equal(14, show.Content.Stack.Length);
        Assert.Equal(
            [
                "Example theatre show.asc: 14 cues, 2 submasters and 3 groups.",
                "Memories 1 to 14 hold the cues, 15 to 16 the submasters and 17 to 19 the groups.",
                "The stack has a step for each cue, with the cue's number and times.",
                "Cues in parts: 1. Each became one fade, as long as its slowest part.",
                "Cues with a delay: 1. The delay is added to the fade time, so the fade ends when it would on the console.",
                "Lines for one maker's consoles: 1. They hold such things as moving lights, effects and macros, and are skipped.",
                "Nothing is saved yet: Save keeps the project.",
            ],
            show.Report);
    }

    [Fact]
    public void ExampleStormFollowsOnByItselfAndTheLastCueGoesRound()
    {
        ImportedShow show = ShowImport.Read(FileName, File.ReadAllText(ShowFile));
        Dictionary<string, Step> steps = show.Content.Stack.ToDictionary(step => step.Cue.ToString(), step => step.Step);

        Assert.Equal(1, steps["6.0"].Wait);     // 2 seconds after the start of a fade of 1 second
        Assert.Equal(0.5, steps["6.1"].Wait);
        Assert.Equal(0, steps["6.2"].Wait);     // waits for Play
        Assert.Equal(90, steps["8.0"].Up);      // 1:30
        Assert.Equal(0, steps["10.0"].Wait);    // waits for Play, then goes round to 0.5
        Assert.Equal(new CueNumber(0, 5), show.Content.Stack[0].Cue);
    }
}
