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

    private string _name = NewProjectName;
    private string? _path;

    // The desk's ProjectChanges at the last save or load. Once the desk's count has moved on,
    // the project holds something that is not in its file.
    private long _savedChanges;

    // What TakeChange gave out the last time.
    private ProjectInfo _told;

    /// <summary>Starts with a new, empty project. The folder is not touched yet.</summary>
    /// <param name="desk">The desk whose memories and notes make up the project.</param>
    /// <param name="folder">The projects folder. It is created when it is first needed.</param>
    public ProjectStore(Desk desk, string folder)
    {
        _desk = desk;
        Folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
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
    /// Lists the project files in the folder, the newest first. Creates the folder if it is not
    /// there yet: the UI shows its path, and a path that leads nowhere would be confusing.
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

            return new ProjectList(Folder, projects);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new DeskException($"The projects folder {Folder} cannot be read. {e.Message}");
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
