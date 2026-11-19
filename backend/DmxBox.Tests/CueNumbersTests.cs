using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// The numbers of the steps of the stack: how a step gets one when it is added, moved, or
/// given a number by hand. The rule behind them all: a step keeps its number unless it has to
/// give way.
/// </summary>
public sealed class CueNumbersTests
{
    [Fact]
    public void NumbersAreReadAsAPersonTypesThem()
    {
        Assert.Equal(new CueNumber(3, 0), CueNumber.Parse("3"));
        Assert.Equal(new CueNumber(3, 0), CueNumber.Parse(" 3.0 "));
        Assert.Equal(new CueNumber(2, 1), CueNumber.Parse("2.1"));
        Assert.Equal(new CueNumber(2, 1), CueNumber.Parse("2,1"));
        Assert.Equal(new CueNumber(999, 9), CueNumber.Parse("999.9"));
        Assert.Equal("2.1", new CueNumber(2, 1).ToString());
        Assert.Equal("10.0", new CueNumber(10, 0).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("two")]
    [InlineData("2.")]
    [InlineData("2.15")]
    [InlineData("1000")]
    [InlineData("-1")]
    [InlineData("2.1.0")]
    public void TextThatIsNoStepNumberIsRefused(string text)
    {
        var error = Assert.Throws<DeskException>(() => CueNumber.Parse(text));
        Assert.Contains("looks like 3 or 2.1", error.Message);
    }

    [Fact]
    public void NewStepGetsTheNextWholeNumber()
    {
        Assert.Equal("1.0", CueNumbers.ForNew(Steps()).ToString());
        Assert.Equal("2.0", CueNumbers.ForNew(Steps("1.0")).ToString());
        Assert.Equal("4.0", CueNumbers.ForNew(Steps("1.0", "2.0", "3.1")).ToString());
        Assert.Throws<DeskException>(() => CueNumbers.ForNew(Steps("999.0")));
    }

    [Fact]
    public void MovedBetweenTwoStepsItTakesTheNumberAfterTheFirstAndTheOthersKeepTheirs()
    {
        // The owner's example: 4.0 moved between 2.0 and 3.0 becomes 2.1.
        Assert.Equal("1.0 2.0 2.1 3.0", After(Steps("1.0", "2.0", "3.0", "4.0"), move: "4.0", behind: "2.0"));

        // Also downwards, and behind a step that has a digit after the dot already.
        Assert.Equal("2.0 3.0 3.1 4.0", After(Steps("1.0", "2.0", "3.0", "4.0"), move: "1.0", behind: "3.0"));
        Assert.Equal("1.0 2.0 2.1 2.2 3.0", After(Steps("1.0", "2.0", "2.1", "3.0", "4.0"), move: "4.0", behind: "2.1"));
    }

    [Fact]
    public void WhenTheNumberIsTakenTheStepsDirectlyBehindGiveWay()
    {
        // 2.1 is taken: it becomes 2.2, and 2.2 becomes 2.3. The 2.5 further on stays.
        string after = After(Steps("1.0", "2.0", "2.1", "2.2", "2.5", "3.0", "4.0"), move: "4.0", behind: "2.0");
        Assert.Equal("1.0 2.0 2.1 2.2 2.3 2.5 3.0", after);

        var steps = Steps("2.0", "2.1", "2.2", "4.0");
        Dictionary<int, CueNumber> changes = CueNumbers.Move(steps, Id(steps, "4.0"), Id(steps, "2.0"));
        Assert.Equal(3, changes.Count);
        Assert.Equal("2.1", changes[Id(steps, "4.0")].ToString());
        Assert.Equal("2.2", changes[Id(steps, "2.1")].ToString());
        Assert.Equal("2.3", changes[Id(steps, "2.2")].ToString());
    }

    [Fact]
    public void MovedToTheVeryTopItBecomesZeroPointOne()
    {
        Assert.Equal("0.1 1.0 2.0", After(Steps("1.0", "2.0", "3.0"), move: "3.0", behind: null));
        Assert.Equal("0.1 0.2 1.0", After(Steps("0.1", "1.0", "3.0"), move: "3.0", behind: null));
    }

    [Fact]
    public void DroppedWhereItStandsNothingChanges()
    {
        var steps = Steps("1.0", "2.0", "3.0");
        Assert.Empty(CueNumbers.Move(steps, Id(steps, "2.0"), Id(steps, "1.0")));   // it is behind 1.0 already
        Assert.Empty(CueNumbers.Move(steps, Id(steps, "1.0"), null));               // it is at the top already
        Assert.Empty(CueNumbers.Move(steps, Id(steps, "2.0"), Id(steps, "2.0")));   // behind itself
    }

    [Fact]
    public void WhenNoNumberIsFreeTheMoveIsRefused()
    {
        var full = Steps("2.9", "3.0", "4.0");
        var error = Assert.Throws<DeskException>(() => CueNumbers.Move(full, Id(full, "4.0"), Id(full, "2.9")));
        Assert.Equal("There is no free number after 2.9. Give a step another number first.", error.Message);

        var zero = Steps("0.0", "1.0");
        Assert.Throws<DeskException>(() => CueNumbers.Move(zero, Id(zero, "1.0"), null));

        var steps = Steps("1.0", "2.0");
        Assert.Throws<DeskException>(() => CueNumbers.Move(steps, 99, null));          // no such step
        Assert.Throws<DeskException>(() => CueNumbers.Move(steps, Id(steps, "1.0"), 99));
    }

    [Fact]
    public void AFreeNumberTypedByHandIsSimplyTaken()
    {
        Assert.Equal("1.0 3.0 7.5", Renumbered(Steps("1.0", "2.0", "3.0"), "2.0", "7.5"));
        Assert.Equal("0.0 1.0 3.0", Renumbered(Steps("1.0", "2.0", "3.0"), "2.0", "0"));

        var steps = Steps("1.0", "2.0");
        Assert.Empty(CueNumbers.Renumber(steps, Id(steps, "2.0"), new CueNumber(2, 0)));   // the number it has
    }

    [Fact]
    public void AWholeNumberTypedByHandMovesTheStepsThatHaveItOn()
    {
        // The owner's example: 2.1 becomes 3.0; the old 3.0 becomes 4.0, the old 3.1 becomes 4.1,
        // and the old 4.0 has to move on to 5.0.
        string after = Renumbered(Steps("1.0", "2.0", "2.1", "3.0", "3.1", "4.0"), "2.1", "3.0");
        Assert.Equal("1.0 2.0 3.0 4.0 4.1 5.0", after);

        // Only as far as needed: 7.0 is not in the way.
        Assert.Equal("1.0 2.0 3.0 7.0", Renumbered(Steps("1.0", "2.0", "7.0", "9.0"), "9.0", "2.0"));
    }

    [Fact]
    public void ANumberWithADigitTypedByHandMovesTheStepsDirectlyBehindOn()
    {
        string after = Renumbered(Steps("1.0", "3.0", "3.1", "3.2", "3.6"), "1.0", "3.1");
        Assert.Equal("3.0 3.1 3.2 3.3 3.6", after);

        var full = Steps("1.0", "3.8", "3.9");
        Assert.Throws<DeskException>(() => CueNumbers.Renumber(full, Id(full, "1.0"), new CueNumber(3, 8)));
    }

    // Steps with these numbers; the name of each is its place in the list, from 1.
    private static List<(int Id, CueNumber Cue)> Steps(params string[] cues)
    {
        return cues.Select((cue, index) => (index + 1, CueNumber.Parse(cue))).ToList();
    }

    private static int Id(List<(int Id, CueNumber Cue)> steps, string cue)
    {
        return steps.Single(step => step.Cue == CueNumber.Parse(cue)).Id;
    }

    // The numbers in order after the step "move" was put behind the step "behind".
    private static string After(List<(int Id, CueNumber Cue)> steps, string move, string? behind)
    {
        return Applied(steps, CueNumbers.Move(steps, Id(steps, move), behind is null ? null : Id(steps, behind)));
    }

    // The numbers in order after the step "cue" was given the number "wanted".
    private static string Renumbered(List<(int Id, CueNumber Cue)> steps, string cue, string wanted)
    {
        return Applied(steps, CueNumbers.Renumber(steps, Id(steps, cue), CueNumber.Parse(wanted)));
    }

    private static string Applied(List<(int Id, CueNumber Cue)> steps, Dictionary<int, CueNumber> changes)
    {
        List<CueNumber> numbers = steps.Select(step => changes.GetValueOrDefault(step.Id, step.Cue)).ToList();
        Assert.Equal(numbers.Count, numbers.Distinct().Count());   // never two steps with one number
        return string.Join(" ", numbers.Order());
    }
}
