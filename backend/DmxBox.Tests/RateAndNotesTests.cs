using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// The rate (a speed master for all sequences) on the desk, and the notes of the project
/// (instructions for the show, written on the Notes view).
/// </summary>
public sealed class RateAndNotesTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;

    public RateAndNotesTests()
    {
        _desk = new Desk(_time);
    }

    // ---------------------------------------------------------------- the rate

    [Fact]
    public void TheRateIsASpeedMasterForEverySequenceWhileItIsOn()
    {
        // Steps of 2 s up: half way after 1 s.
        _desk.SetSequence(2, loop: true, [new Step("channel", 1, 200, 2, 1, 0), new Step("channel", 2, 200, 2, 1, 0)]);
        _desk.SetMemoryLevel(2, 255);
        long changes = _desk.ProjectChanges;

        _desk.SetRate(on: false, 2);                // off: it changes nothing yet
        _time.Pass(1);
        Assert.Equal(100, _desk.GetOutput()[0]);

        _desk.SetRate(on: true, 2);                 // on: twice as fast from here, where it is
        Assert.Equal(100, _desk.GetOutput()[0]);
        _time.Pass(0.5);
        Assert.Equal(200, _desk.GetOutput()[0]);
        Assert.Equal((true, 2.0), _desk.Rate);

        // No part of the project: nothing to save, and the sequence keeps its own speed.
        Assert.Equal(changes, _desk.ProjectChanges);
        Assert.Equal(1, _desk.GetSequences()[2].Speed);
        Assert.Equal(2, _desk.GetState().Rate);
        Assert.True(_desk.GetState().RateOn);
    }

    [Fact]
    public void TheRateWorksWhileTheStackIsLiveAndStaysThroughANewProject()
    {
        _desk.AddStackStep(new Step("memory", 2, 255, 0, 0, 0));
        _desk.SetStackLive(true);
        _desk.SetRate(on: true, 0.5);              // not locked: it is no part of the show
        _desk.SetStackLive(false);
        _desk.StartNewProject();
        Assert.Equal((true, 0.5), _desk.Rate);      // like the master, it belongs to the evening
    }

    [Fact]
    public void TheRateGoesFromATenthToTenTimes()
    {
        var slow = Assert.Throws<DeskException>(() => _desk.SetRate(true, 0.05));
        Assert.Equal("The rate must be from 0.1 to 10, not 0.05.", slow.Message);
        Assert.Throws<DeskException>(() => _desk.SetRate(true, 11));
        Assert.Throws<DeskException>(() => _desk.SetRate(true, double.NaN));
        Assert.Equal((false, 1.0), _desk.Rate);
    }

    [Fact]
    public void ASequenceSpeedChangedWhileTheRateIsOnGoesOnFromWhereItIs()
    {
        _desk.SetSequence(2, loop: true, [new Step("channel", 1, 200, 2, 1, 0), new Step("channel", 2, 200, 2, 1, 0)]);
        _desk.SetMemoryLevel(2, 255);
        _desk.SetRate(on: true, 2);
        _time.Pass(0.5);                            // 1 s of the sequence: half way up
        Assert.Equal(100, _desk.GetOutput()[0]);

        _desk.SetSequenceSpeed(2, 0.5);             // now at 0.5 x 2 = 1
        Assert.Equal(100, _desk.GetOutput()[0]);
        _time.Pass(1);
        Assert.Equal(200, _desk.GetOutput()[0]);
    }

    // ---------------------------------------------------------------- the notes of the project

    private const string Notes = "<div>Cue 1 <b>when the house lights are out</b></div><ul><li>Then <u>Play</u></li></ul>";

    [Fact]
    public void NotesBelongToTheProjectAndChangingThemMakesItUnsaved()
    {
        Assert.Equal("", _desk.Notes);
        long changes = _desk.ProjectChanges;

        Assert.True(_desk.SetNotes(Notes));
        Assert.NotEqual(changes, _desk.ProjectChanges);
        changes = _desk.ProjectChanges;
        Assert.False(_desk.SetNotes(Notes));                 // the same again: no change
        Assert.Equal(changes, _desk.ProjectChanges);
        Assert.Equal(Notes, _desk.GetProject().Content.Notes);

        Assert.True(_desk.SetNotes(null));                   // none
        Assert.Equal("", _desk.Notes);
    }

    [Fact]
    public void NotesAreLockedWhileTheStackIsLiveAndANewProjectHasNone()
    {
        _desk.SetNotes(Notes);
        _desk.AddStackStep(new Step("channel", 1, 255, 0, 0, 0));
        _desk.SetStackLive(true);
        var live = Assert.Throws<DeskException>(() => _desk.SetNotes("Something else"));
        Assert.StartsWith("The stack is live", live.Message, StringComparison.Ordinal);
        Assert.Equal(Notes, _desk.Notes);

        _desk.SetStackLive(false);
        _desk.StartNewProject();
        Assert.Equal("", _desk.Notes);

        // A project brings its own; one without notes has none.
        ProjectContent project = _desk.GetProject().Content;
        _desk.ReplaceProject(project with { Notes = Notes });
        Assert.Equal(Notes, _desk.Notes);
        _desk.ReplaceProject(project);
        Assert.Equal("", _desk.Notes);
    }

    [Fact]
    public void NotesHaveASensibleSize()
    {
        Assert.Equal("", ProjectNotes.Checked(null));
        string longest = new('x', ProjectNotes.MaxLength);
        Assert.Equal(longest, ProjectNotes.Checked(longest));
        var tooLong = Assert.Throws<DeskException>(() => _desk.SetNotes(longest + "x"));
        Assert.StartsWith($"The notes are too long: at most {ProjectNotes.MaxLength} characters", tooLong.Message, StringComparison.Ordinal);
        Assert.Equal("", _desk.Notes);
    }

    [Fact]
    public void ProjectFileKeepsTheNotesAndPassesOverTheSettingsOfEarlierVersions()
    {
        ProjectContent project = _desk.GetProject().Content with { Notes = Notes };
        string written = Write(project);
        Assert.Equal(Notes, Read(written).Notes);

        // A project without notes writes none; a file from before notes has none.
        string without = Write(project with { Notes = "" });
        Assert.DoesNotContain("\"notes\"", without, StringComparison.Ordinal);
        Assert.Equal("", Read(without).Notes);

        // An earlier version kept the layout and the shortcuts of the PC in the project. They
        // belong to each device now: the file is read without them, and written without them.
        string earlier = without.Replace("\"format\"", "\"settings\": { \"channelSize\": 8 },\n  \"format\"", StringComparison.Ordinal);
        ProjectContent back = Read(earlier);
        Assert.DoesNotContain("settings", Write(back), StringComparison.Ordinal);

        // Notes too long for the desk make the file unusable, as a note too long under a fader does.
        string huge = Write(project with { Notes = "" }).Replace(
            "\"format\"", $"\"notes\": \"{new string('x', ProjectNotes.MaxLength + 1)}\",\n  \"format\"", StringComparison.Ordinal);
        var bad = Assert.Throws<InvalidDataException>(() => Read(huge));
        Assert.StartsWith("The notes of the project in the file cannot be used.", bad.Message, StringComparison.Ordinal);
    }

    private static string Write(ProjectContent project)
    {
        return ProjectFormat.WriteText(project);
    }

    private static ProjectContent Read(string text)
    {
        return ProjectFormat.ReadText(text);
    }
}
