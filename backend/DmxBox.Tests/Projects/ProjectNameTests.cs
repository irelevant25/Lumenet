namespace DmxBox.Tests.Projects;

/// <summary>Which project names are accepted. A name becomes a file name, so the rules are strict.</summary>
public sealed class ProjectNameTests
{
    [Fact]
    public void OrdinaryNamesAreAccepted()
    {
        ProjectName.Require("show");
        ProjectName.Require("Summer show 2026");
        ProjectName.Require("v1.2 (final) - copy");
        ProjectName.Require("Sv\u011Btla");            // a letter beyond ASCII
        ProjectName.Require(new string('a', 80));     // the longest name allowed
    }

    [Fact]
    public void NamesThatOnlyResembleDeviceNamesAreAccepted()
    {
        ProjectName.Require("console");
        ProjectName.Require("COM");
        ProjectName.Require("COM10");
        ProjectName.Require("LPT");
        ProjectName.Require("null");
        ProjectName.Require("my.CON");
    }

    [Fact]
    public void EmptyNameIsRejected()
    {
        AssertRejected("");
        AssertRejected(null);
    }

    [Fact]
    public void NameLongerThan80CharactersIsRejected()
    {
        AssertRejected(new string('a', 81));
    }

    [Fact]
    public void CharactersThatWindowsForbidsInFileNamesAreRejected()
    {
        foreach (char forbidden in "<>:\"|?*")
            AssertRejected($"my{forbidden}show");
    }

    [Fact]
    public void PathSeparatorsAreRejected()
    {
        AssertRejected("shows/tonight");
        AssertRejected("shows\\tonight");
        AssertRejected("/tonight");
        AssertRejected("\\\\server\\share\\tonight");
        AssertRejected("C:\\tonight");
        AssertRejected("../tonight");
        AssertRejected("..\\tonight");
    }

    [Fact]
    public void ControlCharactersAreRejected()
    {
        AssertRejected("my\tshow");
        AssertRejected("my\nshow");
        AssertRejected("my\0show");
    }

    [Fact]
    public void DotOrSpaceAtEitherEndIsRejected()
    {
        AssertRejected(".show");
        AssertRejected("show.");
        AssertRejected(" show");
        AssertRejected("show ");
        AssertRejected(".");
        AssertRejected("..");
        AssertRejected(" ");
    }

    [Fact]
    public void WindowsDeviceNamesAreRejected()
    {
        string[] deviceNames =
        [
            "CON", "PRN", "AUX", "NUL",
            "COM0", "COM1", "COM9", "LPT0", "LPT1", "LPT9",
            "con", "Nul", "com1", "lpt9",        // in any case
            "CON.backup", "nul.txt", "COM1.old", // whatever follows the first dot
            "COM\u00B9", "LPT\u00B3",            // the raised digits count too
        ];

        foreach (string name in deviceNames)
            AssertRejected(name);
    }

    [Fact]
    public void EveryRejectionSaysWhy()
    {
        AssertMessage("A project needs a name.", "");
        AssertMessage("A project name can have at most 80 characters.", new string('a', 81));
        AssertMessage("A project name cannot begin or end with a dot or a space.", "show.");
        AssertMessage("A project name cannot contain the character \"/\".", "a/b");
        AssertMessage("A project name cannot contain a tab, a line break or the like.", "a\tb");
        AssertMessage("\"NUL\" cannot be a project name: Windows keeps that name for itself.", "NUL");
    }

    private static void AssertRejected(string? name)
    {
        Assert.Throws<DeskException>(() => ProjectName.Require(name));
    }

    private static void AssertMessage(string expected, string name)
    {
        var error = Assert.Throws<DeskException>(() => ProjectName.Require(name));
        Assert.Equal(expected, error.Message);
    }
}
