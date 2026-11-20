using System.Text.Json;
using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// The rate (a speed master for all sequences) on the desk, and the settings of the UI that a project keeps.
/// </summary>
public sealed class RateAndSettingsTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;

    public RateAndSettingsTests()
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

    // ---------------------------------------------------------------- settings

    [Fact]
    public void SettingsBelongToTheProjectAndChangingThemMakesItUnsaved()
    {
        Assert.Null(_desk.Settings);
        Assert.True(_desk.AdoptSettings("""{"channelSize":"auto"}"""));   // brought along: no change
        long changes = _desk.ProjectChanges;
        Assert.False(_desk.AdoptSettings("""{"channelSize":8}"""));       // the desk has some already

        Assert.True(_desk.SetSettings("""{"channelSize":8}"""));
        Assert.NotEqual(changes, _desk.ProjectChanges);
        Assert.False(_desk.SetSettings("""{"channelSize":8}"""));         // the same again: no change
        Assert.Equal("""{"channelSize":8}""", _desk.GetProject().Content.Settings);
    }

    [Fact]
    public void AProjectWithoutSettingsKeepsThoseInUseAndANewProjectToo()
    {
        _desk.SetSettings("""{"channelSize":8}""");
        _desk.StartNewProject();
        Assert.Equal("""{"channelSize":8}""", _desk.Settings);

        ProjectContent old = _desk.GetProject().Content with { Settings = null };
        _desk.ReplaceProject(old);
        Assert.Equal("""{"channelSize":8}""", _desk.Settings);

        _desk.ReplaceProject(old with { Settings = """{"channelSize":64}""" });
        Assert.Equal("""{"channelSize":64}""", _desk.Settings);
    }

    [Fact]
    public void SettingsAreAJsonObjectOfASensibleSize()
    {
        Assert.Equal("""{"a":1}""", ProjectSettings.Checked(Json("""{ "a": 1 }""")));
        var list = Assert.Throws<DeskException>(() => ProjectSettings.Checked(Json("[1, 2]")));
        Assert.Equal("The settings must be a JSON object.", list.Message);
        string huge = $$"""{"a":"{{new string('x', ProjectSettings.MaxLength)}}"}""";
        Assert.Throws<DeskException>(() => ProjectSettings.Checked(Json(huge)));
    }

    [Fact]
    public void ProjectFileKeepsTheSettingsAndPassesOverDamagedOnes()
    {
        ProjectContent project = _desk.GetProject().Content with
        {
            Settings = """{"channelSize":8,"shortcuts":{"master.clear":"Ctrl+KeyC"}}""",
        };

        ProjectContent back = RoundTrip(project);
        Assert.Equal(project.Settings, back.Settings);

        // A file without settings, or with settings that are no object, is a project without them.
        string without = Write(project with { Settings = null });
        Assert.DoesNotContain("settings", without);
        Assert.Null(Read(without).Settings);
        Assert.Null(Read(without.Replace("\"format\"", "\"settings\": [1, 2],\n  \"format\"")).Settings);
    }

    private static JsonElement Json(string text)
    {
        using JsonDocument document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static ProjectContent RoundTrip(ProjectContent project)
    {
        return Read(Write(project));
    }

    private static string Write(ProjectContent project)
    {
        using var stream = new MemoryStream();
        ProjectFormat.Write(stream, project);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static ProjectContent Read(string text)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
        return ProjectFormat.Read(stream);
    }
}
