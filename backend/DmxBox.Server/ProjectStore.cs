namespace DmxBox.Server;

/// <summary>
/// The projects folder, and the project that is open. A project is the set of stored
/// memories, the notes under the faders and the bindings of the independent keys: saving
/// writes them to a file, loading replaces those of the desk by those of a file.
/// </summary>
/// <remarks>
/// <para>
/// Whatever goes wrong ends in a <see cref="DeskException"/> with a message for the user:
/// a name that cannot be used, a damaged file, a full disk.
/// </para>
/// <para>Thread-safe: one save or load at a time.</para>
/// </remarks>
internal sealed class ProjectStore
{
    private const string Extension = ".json";
    private const string NewProjectName = "Untitled";

    private readonly Lock _lock = new();
    private readonly Desk _desk;

    // The folder of the example projects that come with the program; null for none.
    private readonly string? _examples;

    private string _name = NewProjectName;
    private string? _path;

    // The desk's ProjectChanges at the last save or load. Once the desk's count has moved on,
    // the project holds something that is not in its file.
    private long _savedChanges;

    // What TakeChange gave out the last time.
    private ProjectInfo _told;

    // When the work that Recover brought back was autosaved; null once the project was saved,
    // loaded or started anew, and when nothing was brought back.
    private DateTimeOffset? _recovered;

    /// <summary>Starts with a new, empty project. The folder is not touched yet.</summary>
    /// <param name="desk">The desk whose memories and notes make up the project.</param>
    /// <param name="folder">The projects folder. It is created when it is first needed.</param>
    /// <param name="examples">
    /// The folder of the example projects that come with the program, which are read and never
    /// written; null for none. A folder that is not there has no examples.
    /// </param>
    public ProjectStore(Desk desk, string folder, string? examples = null)
    {
        _desk = desk;
        Folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        _examples = examples is null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(examples));
        _savedChanges = desk.ProjectChanges;
        _told = CurrentInfo();
    }

    /// <summary>The projects folder, as a full path.</summary>
    public string Folder { get; }

    /// <summary>The project that is open.</summary>
    public ProjectInfo Current
    {
        get
        {
            lock (_lock)
            {
                return CurrentInfo();
            }
        }
    }

    /// <summary>
    /// When the unsaved work that the desk program brought back at its start (see
    /// <see cref="Autosave"/>) was kept; null when nothing was brought back, and once the project
    /// was saved, loaded or started anew.
    /// </summary>
    public DateTimeOffset? Recovered
    {
        get
        {
            lock (_lock)
            {
                return _recovered;
            }
        }
    }

    /// <summary>
    /// For telling the UIs when the project changes. Gives the project if its name, file or
    /// "unsaved" mark differs from what this method gave out the last time, otherwise null.
    /// </summary>
    public ProjectInfo? TakeChange()
    {
        lock (_lock)
        {
            ProjectInfo current = CurrentInfo();
            if (current == _told)
                return null;

            _told = current;
            return current;
        }
    }

    /// <summary>
    /// Lists the project files in the folder, the newest first, and the examples. Creates the
    /// folder if it is not there yet: the UI shows its path, and a path that leads nowhere would
    /// be confusing.
    /// </summary>
    public ProjectList List()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            ProjectEntry[] projects = new DirectoryInfo(Folder)
                .EnumerateFiles("*" + Extension)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Select(file => new ProjectEntry(
                    Path.GetFileNameWithoutExtension(file.Name), file.FullName, file.LastWriteTimeUtc))
                .ToArray();

            return new ProjectList(Folder, projects, Examples());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new DeskException($"The projects folder {Folder} cannot be read. {e.Message}");
        }
    }

    /// <summary>
    /// Opens one of the example projects, as loading does. The project gets the example's name
    /// but no file, so saving asks for a name and the example stays as it is; until something is
    /// changed there is nothing to save.
    /// </summary>
    /// <param name="name">The name of the example, as <see cref="List"/> gives it.</param>
    /// <returns>The project after it was opened.</returns>
    public ProjectInfo OpenExample(string name)
    {
        // Only a name of the list: nothing else can be read this way.
        if (_examples is null || !Examples().Contains(name, StringComparer.Ordinal))
            throw new DeskException($"There is no example \"{name}\".");

        lock (_lock)
        {
            ProjectContent content = ReadFile(name, Path.Combine(_examples, name + Extension));
            _savedChanges = _desk.ReplaceProject(content);
            _name = name;
            _path = null;
            _recovered = null;
            return CurrentInfo();
        }
    }

    // The names of the examples, in the order of their names (they start with a number).
    private string[] Examples()
    {
        if (_examples is null || !Directory.Exists(_examples))
            return [];

        try
        {
            return new DirectoryInfo(_examples)
                .EnumerateFiles("*" + Extension)
                .Select(file => Path.GetFileNameWithoutExtension(file.Name))
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new DeskException($"The examples in {_examples} cannot be read. {e.Message}");
        }
    }

    /// <summary>
    /// Starts a new project: the desk is made clean (see <see cref="Desk.StartNewProject"/>),
    /// and the project is "Untitled" again, without a file and with nothing to save.
    /// </summary>
    /// <returns>The new project.</returns>
    public ProjectInfo StartNew()
    {
        lock (_lock)
        {
            _savedChanges = _desk.StartNewProject();
            _name = NewProjectName;
            _path = null;
            _recovered = null;
            return CurrentInfo();
        }
    }

    /// <summary>Writes the desk's memories, notes and independent keys to a project file.</summary>
    /// <param name="name">
    /// The name to save under; a project of that name is replaced. Null saves to the file the
    /// project already has.
    /// </param>
    /// <returns>The project after the save.</returns>
    public ProjectInfo Save(string? name)
    {
        lock (_lock)
        {
            string path = name is null ? CurrentPath() : PathOf(name);
            (ProjectContent content, long changes) = _desk.GetProject();
            WriteFile(path, content);

            _name = name ?? _name;
            _path = path;
            _savedChanges = changes;
            _recovered = null;
            return CurrentInfo();
        }
    }

    /// <summary>
    /// Replaces the desk's memories, notes and independent keys by those of a project file.
    /// A file that cannot be used leaves the desk and the open project as they were.
    /// </summary>
    /// <param name="name">The name of the project.</param>
    /// <returns>The project after the load.</returns>
    public ProjectInfo Load(string name)
    {
        lock (_lock)
        {
            string path = PathOf(name);
            ProjectContent content = ReadFile(name, path);

            _savedChanges = _desk.ReplaceProject(content);
            _name = name;
            _path = path;
            _recovered = null;
            return CurrentInfo();
        }
    }

    /// <summary>
    /// Replaces the desk's project by one made from another console's show file, see
    /// <see cref="ShowImport"/>. The project gets its name but no file: until it is saved it
    /// counts as unsaved, because nothing of it is in a file yet.
    /// </summary>
    /// <param name="name">The name of the project, which <see cref="ProjectName"/> allows.</param>
    /// <param name="content">The project.</param>
    /// <returns>The project after the import.</returns>
    public ProjectInfo Import(string name, ProjectContent content)
    {
        ProjectName.Require(name);
        lock (_lock)
        {
            long changes = _desk.ReplaceProject(content);
            _name = name;
            _path = null;
            _savedChanges = changes - 1;   // one change, the import itself, is not in a file
            _recovered = null;
            return CurrentInfo();
        }
    }

    /// <summary>
    /// Brings back the work the desk program had not saved when it stopped, as <see cref="Autosave"/>
    /// kept it: the project as it was, under its name and with its file, and unsaved, because
    /// it is not in its file.
    /// </summary>
    /// <param name="name">The project's name then.</param>
    /// <param name="path">Its file then; null for a project that had none yet.</param>
    /// <param name="content">The project as it was.</param>
    /// <param name="kept">When it was kept.</param>
    /// <returns>The project after it was brought back.</returns>
    public ProjectInfo Recover(string name, string? path, ProjectContent content, DateTimeOffset kept)
    {
        ProjectName.Require(name);

        // A file outside the projects folder is not taken over: saving would write there.
        string? file = path is null ? null : Path.GetFullPath(path);
        if (file is not null && !string.Equals(Path.GetDirectoryName(file), Folder, StringComparison.OrdinalIgnoreCase))
            file = null;

        lock (_lock)
        {
            long changes = _desk.ReplaceProject(content);
            _name = name;
            _path = file;
            _savedChanges = changes - 1;   // what was brought back is not in the file
            _recovered = kept;
            return CurrentInfo();
        }
    }

    // Call with the lock held.
    private ProjectInfo CurrentInfo()
    {
        return new ProjectInfo(_name, _path, Unsaved: _desk.ProjectChanges != _savedChanges);
    }

    private string CurrentPath()
    {
        return _path ?? throw new DeskException("This project has no file yet. Save it under a name.");
    }

    // The file of the project with this name. The rules for names keep it inside the projects
    // folder; the check at the end holds whatever those rules may have missed.
    private string PathOf(string? name)
    {
        ProjectName.Require(name);

        string path = Path.GetFullPath(Path.Combine(Folder, name + Extension));
        if (!string.Equals(Path.GetDirectoryName(path), Folder, StringComparison.OrdinalIgnoreCase))
            throw new DeskException($"The project name \"{name}\" leads out of the projects folder.");

        return path;
    }

    // Writes a temporary file first and then moves it over the real one. So the real file is
    // always whole, the old one or the new one, also when the program or the PC stops half way.
    private void WriteFile(string path, ProjectContent content)
    {
        string temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Folder);
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                ProjectFormat.Write(stream, content);
                stream.Flush(flushToDisk: true);   // on the disk for sure, before the old file goes
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new DeskException($"The project could not be saved as {path}. {e.Message}");
        }
    }

    private static ProjectContent ReadFile(string name, string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            return ProjectFormat.Read(stream);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new DeskException($"There is no project \"{name}\".");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            throw new DeskException($"The project \"{name}\" could not be loaded. {e.Message}");
        }
    }
}
