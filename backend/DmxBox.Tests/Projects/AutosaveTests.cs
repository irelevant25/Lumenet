using Microsoft.Extensions.Logging.Abstractions;

namespace DmxBox.Tests.Projects;

/// <summary>
/// The autosave: unsaved work is kept in a file of its own, deleted once the project is saved,
/// and brought back when the desk program starts again.
/// </summary>
public sealed class AutosaveTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("lumenet-autosave-");

    private string AutosaveFile => Path.Combine(_folder.FullName, "autosave.json");

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    [Fact]
    public void UnsavedWorkIsKeptAndTheFileGoesOnceTheProjectIsSaved()
    {
        (Desk desk, ProjectStore projects, Autosave autosave) = StartDesk();

        autosave.Keep();
        Assert.False(File.Exists(AutosaveFile));   // nothing unsaved yet

        desk.SetChannelNote(1, "Front");
        autosave.Keep();
        Assert.True(File.Exists(AutosaveFile));
        DateTime written = File.GetLastWriteTimeUtc(AutosaveFile);

        autosave.Keep();                             // nothing new: the file is not written again
        Assert.Equal(written, File.GetLastWriteTimeUtc(AutosaveFile));

        projects.Save("Show");
        autosave.Keep();
        Assert.False(File.Exists(AutosaveFile));
    }

    [Fact]
    public void AtTheNextStartTheWorkComesBackUnderItsNameUnsaved()
    {
        (Desk first, ProjectStore firstProjects, Autosave firstAutosave) = StartDesk();
        firstProjects.Save("Evening");
        first.SetChannel(4, 255);
        first.SaveMemory(2);
        first.SetMemoryNote(2, "Warm");
        first.SetNotes("<b>Doors at 7</b>");
        firstAutosave.Keep();

        (Desk desk, ProjectStore projects, Autosave autosave) = StartDesk();
        Assert.True(autosave.Restore());

        ProjectInfo project = projects.Current;
        Assert.Equal("Evening", project.Name);
        Assert.Equal(Path.Combine(_folder.FullName, "Evening.json"), project.Path);
        Assert.True(project.Unsaved);
        Assert.NotNull(projects.Recovered);
        Assert.Equal(255, desk.GetMemoryLooks()[2][3]);
        Assert.Equal("Warm", desk.GetState().MemoryNotes[1]);
        Assert.Equal("<b>Doors at 7</b>", desk.Notes);
        Assert.Equal(new int[512], desk.GetState().Channels);   // no fader is brought back: no light comes on by itself

        // Saved, it is an ordinary project again, and the autosave file goes.
        projects.Save(null);
        Assert.Null(projects.Recovered);
        autosave.Keep();
        Assert.False(File.Exists(AutosaveFile));
    }

    [Fact]
    public void TheAutosaveCanBeSwitchedOffAndTheNextStartKnowsIt()
    {
        (Desk desk, _, Autosave autosave) = StartDesk();
        Assert.Equal(new AutosaveOptions(true, 5), autosave.Options);
        desk.SetChannelNote(1, "Front");
        autosave.Keep();
        Assert.True(File.Exists(AutosaveFile));

        Assert.Equal(new AutosaveOptions(false, 30), autosave.SetOptions(false, 30));
        Assert.False(File.Exists(AutosaveFile));   // off: it keeps nothing, and its file goes
        desk.SetChannelNote(2, "Back");
        autosave.Keep();
        Assert.False(File.Exists(AutosaveFile));

        (_, _, Autosave next) = StartDesk();
        next.Restore();
        Assert.Equal(new AutosaveOptions(false, 30), next.Options);

        var odd = Assert.Throws<DeskException>(() => autosave.SetOptions(true, 7));
        Assert.Contains("every 5, 30, 60, 300 seconds", odd.Message);
    }

    [Fact]
    public void WithoutAnAutosaveFileTheDeskStartsAsUsual()
    {
        (_, ProjectStore projects, Autosave autosave) = StartDesk();
        Assert.False(autosave.Restore());
        Assert.Equal(new ProjectInfo("Untitled", null, false), projects.Current);
        Assert.Null(projects.Recovered);
    }

    [Fact]
    public void ADamagedAutosaveFileIsPutAsideAndTheDeskStartsAsUsual()
    {
        File.WriteAllText(AutosaveFile, "{ not json");
        (_, ProjectStore projects, Autosave autosave) = StartDesk();

        Assert.False(autosave.Restore());
        Assert.False(File.Exists(AutosaveFile));
        Assert.True(File.Exists(Path.Combine(_folder.FullName, "autosave.damaged.json")));
        Assert.False(projects.Current.Unsaved);
    }

    [Fact]
    public void AFileOutsideTheProjectsFolderIsNotTakenOver()
    {
        (Desk first, _, Autosave firstAutosave) = StartDesk();
        first.SetChannelNote(1, "Front");
        firstAutosave.Keep();
        string text = File.ReadAllText(AutosaveFile).Replace("\"path\": null", "\"path\": \"C:\\\\Windows\\\\evil.json\"");
        File.WriteAllText(AutosaveFile, text);

        (_, ProjectStore projects, Autosave autosave) = StartDesk();
        Assert.True(autosave.Restore());
        Assert.Null(projects.Current.Path);   // saving asks for a name in the projects folder
    }

    private (Desk Desk, ProjectStore Projects, Autosave Autosave) StartDesk()
    {
        var desk = new Desk();
        var projects = new ProjectStore(desk, _folder.FullName);
        var autosave = new Autosave(desk, projects, AutosaveFile, NullLogger<Autosave>.Instance);
        return (desk, projects, autosave);
    }
}
