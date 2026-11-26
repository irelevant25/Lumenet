using System.Globalization;
using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Server.Api;

// The hub's part for the project: its files, Export and Import, undo, the notes and the autosave.
internal partial class DeskHub
{
    // The name of an imported project whose file's name cannot be one.
    private const string ImportedProjectName = "Imported project";

    /// <summary>
    /// Undo: the project goes back to how it was before its last change. See <see cref="UndoHistory"/>
    /// for what counts. Every UI gets the whole desk again.
    /// </summary>
    /// <returns>What was undone, like "storing memory 5".</returns>
    public async Task<string> Undo()
    {
        string what = _history.Undo();
        await TellDeskAsync();
        return what;
    }

    /// <summary>Redo: a change that was undone is made again.</summary>
    /// <returns>What was redone, like "storing memory 5".</returns>
    public async Task<string> Redo()
    {
        string what = _history.Redo();
        await TellDeskAsync();
        return what;
    }

    /// <summary>What undo and redo would do now.</summary>
    public HistoryState GetHistory()
    {
        return _history.State;
    }

    /// <summary>The notes of the project: instructions for the show, as the Notes view wrote them; an empty text for none.</summary>
    public string GetNotes()
    {
        return _desk.Notes;
    }

    /// <summary>
    /// Takes the notes as the owner wrote them on the Notes view. Every UI hears of it, also the
    /// one that wrote them (which goes on writing in its own editor meanwhile). Changes in quick
    /// succession, as while typing, are one step of the history.
    /// </summary>
    /// <param name="notes">The notes as the UI wrote them; empty for none.</param>
    public async Task SetNotes(string notes)
    {
        bool changed;
        using (_history.Change("the notes", "notes"))
            changed = _desk.SetNotes(notes);

        if (changed)
            await Clients.All.NotesChanged(_desk.Notes);

        await TellProjectAsync();
    }

    /// <summary>Whether the autosave keeps the work that is not saved, and how often.</summary>
    public AutosaveOptions GetAutosave()
    {
        return _autosave.Options;
    }

    /// <summary>
    /// Switches the autosave and sets how often it keeps the work. Only a UI on this PC can do
    /// this; the UIs on this PC hear of it.
    /// </summary>
    /// <param name="on">True to keep the work.</param>
    /// <param name="seconds">How often: 5, 30, 60 or 300 seconds.</param>
    public async Task<AutosaveOptions> SetAutosave(bool on, int seconds)
    {
        RequireLocal("The autosave can only be set on the PC.");
        AutosaveOptions options = _autosave.SetOptions(on, seconds);
        await Clients.Group(LanAccess.PcGroup).AutosaveChanged(options);
        return options;
    }

    /// <summary>
    /// When the unsaved work that the desk program brought back at its start was kept, as
    /// "2026-11-20T18:42:05+01:00"; null when nothing was brought back, and once the project was
    /// saved, loaded or started anew. See <see cref="Autosave"/>.
    /// </summary>
    public string? GetRecovered()
    {
        return _projects.Recovered?.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    /// <summary>Lists the projects that can be loaded, the newest first, and the examples.</summary>
    public ProjectList GetProjects()
    {
        return _projects.List();
    }

    /// <summary>
    /// Starts a new project: a clean desk, called "Untitled", without a file. The master and
    /// blackout stay as they are.
    /// </summary>
    /// <returns>The new project.</returns>
    public async Task<ProjectInfo> NewProject()
    {
        ProjectInfo project = _projects.StartNew();
        _history.Clear();
        await TellDeskAsync();
        return project;
    }

    /// <summary>Writes the memories, the notes and the independent keys to a project file.</summary>
    /// <param name="name">
    /// The name to save under; a project of that name is replaced. Null saves to the file the
    /// project already has, which fails for a project that was never saved.
    /// </param>
    /// <returns>The project after the save.</returns>
    public async Task<ProjectInfo> SaveProject(string? name)
    {
        ProjectInfo project = _projects.Save(name);
        await TellProjectAsync();
        return project;
    }

    /// <summary>
    /// Replaces the memories, the notes and the independent keys by those of a project file.
    /// All memory faders go to 0.
    /// </summary>
    /// <param name="name">The name of the project.</param>
    /// <returns>The project after the load.</returns>
    public async Task<ProjectInfo> LoadProject(string name)
    {
        ProjectInfo project = _projects.Load(name);
        _history.Clear();
        await TellDeskAsync();
        return project;
    }

    /// <summary>
    /// Opens one of the example projects that come with the program, in place of the open one, as
    /// loading does. The example itself never changes: the project has its name but no file, and
    /// nothing to save until something is changed.
    /// </summary>
    /// <param name="name">The name of the example, as <see cref="GetProjects"/> lists it.</param>
    /// <returns>The project after the load.</returns>
    public async Task<ProjectInfo> LoadExample(string name)
    {
        ProjectInfo project = _projects.OpenExample(name);
        _history.Clear();
        await TellDeskAsync();
        return project;
    }

    /// <summary>
    /// The open project as a project file, as saving would write it, for Export: the UI hands it
    /// to the user as a file on their device. The project and its file stay as they are.
    /// </summary>
    public string ExportProject()
    {
        return ProjectFormat.WriteText(_desk.GetProject().Content);
    }

    /// <summary>
    /// Opens a project file that the user chose on their device (Import), in place of the open
    /// one, as loading does: all memory faders go to 0. The project has a name but no file until
    /// it is saved.
    /// </summary>
    /// <param name="name">What the project is to be called, like the name of the file it came in.</param>
    /// <param name="text">The project file, as saving or Export writes it.</param>
    /// <returns>The project, now open.</returns>
    public async Task<ProjectInfo> ImportProjectFile(string name, string text)
    {
        ProjectContent content;
        try
        {
            content = ProjectFormat.ReadText(text);
        }
        catch (InvalidDataException e)
        {
            throw new DeskException($"The project cannot be imported. {e.Message}");
        }

        ProjectInfo project = _projects.Import(ProjectName.Suggest(name, ImportedProjectName), content);
        _history.Clear();
        await TellDeskAsync();
        return project;
    }

    /// <summary>
    /// Makes a project out of a theatre console's show file in the USITT ASCII format (".asc"),
    /// see <see cref="ShowImport"/>, and opens it in place of the open one, as loading does: all
    /// memory faders go to 0. Nothing is saved.
    /// </summary>
    /// <param name="fileName">The name of the file; the project is called after it.</param>
    /// <param name="text">What the file holds.</param>
    /// <returns>The project, and what the import made of the file, for the user to read.</returns>
    public async Task<ImportAnswer> ImportProject(string fileName, string text)
    {
        ImportedShow show = ShowImport.Read(fileName, text);
        ProjectInfo project = _projects.Import(show.Name, show.Content);
        _history.Clear();
        await TellDeskAsync();
        return new ImportAnswer(project, show.Report);
    }

    // The project goes out only when its name, file or "unsaved" mark really changed, and what
    // undo and redo would do only when that changed.
    private async Task TellProjectAsync()
    {
        if (_projects.TakeChange() is { } project)
            await Clients.All.ProjectChanged(project);

        if (_history.TakeChange() is { } history)
            await Clients.All.HistoryChanged(history);
    }
}
