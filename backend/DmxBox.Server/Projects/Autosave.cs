using System.Text.Json;

namespace DmxBox.Server.Projects;

/// <summary>
/// Keeps the work that is not saved yet, so that a crash, a restart of Windows or a closed window
/// loses none of it: every few seconds, while the project has changes that are not in its file,
/// the whole project goes into a file of its own, the autosave file. Once the project is saved
/// (or loaded, or started anew), that file is deleted. When the desk program starts and finds
/// the file, it brings the work back: the project as it was, under its name, unsaved.
/// </summary>
/// <remarks>
/// <para>
/// The autosave file is not a project file: it holds the project file's JSON together with the
/// project's name and file and the time it was kept. It lives on this PC only (the setting
/// "Autosave:File"), not with the projects, which may be copied elsewhere.
/// </para>
/// <para>
/// The faders are not kept: a desk program that starts does not switch lights on by itself,
/// whatever it brings back.
/// </para>
/// </remarks>
internal sealed class Autosave : BackgroundService
{
    /// <summary>How often the autosave file can be brought up to date, in seconds: the choices of Settings.</summary>
    public static readonly int[] Intervals = [5, 30, 60, 300];

    /// <summary>The autosave of a desk program that has never been told otherwise: on, every 5 seconds.</summary>
    public static readonly AutosaveOptions DefaultOptions = new(On: true, Seconds: 5);

    private const string FormatName = "lumenet-autosave";
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly Desk _desk;
    private readonly ProjectStore _projects;
    private readonly string _file;
    private readonly string _optionsFile;
    private readonly ILogger<Autosave> _log;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();

    // The desk's ProjectChanges when the autosave file was written last; null while there is no
    // file of this run.
    private long? _written;

    // Whether the autosave keeps anything, and how often; kept in a file next to the autosave file.
    private AutosaveOptions _options = DefaultOptions;

    // The timer of the loop, once it runs: a new interval takes effect at once.
    private PeriodicTimer? _timer;

    /// <summary>Makes the autosave. Nothing is read or written before <see cref="Restore"/> and the start.</summary>
    /// <param name="desk">The desk whose project is kept.</param>
    /// <param name="projects">The open project: its name, its file, whether it is saved.</param>
    /// <param name="file">The autosave file. Its folder is made when it is first needed.</param>
    /// <param name="log">For what went wrong, and what was brought back.</param>
    /// <param name="time">The clock for the time a project was kept; the tests pass one of their own.</param>
    public Autosave(Desk desk, ProjectStore projects, string file, ILogger<Autosave> log, TimeProvider? time = null)
    {
        _desk = desk;
        _projects = projects;
        _file = Path.GetFullPath(file);
        _optionsFile = Path.Combine(Path.GetDirectoryName(_file) ?? ".", "autosave-options.json");
        _log = log;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Whether the autosave keeps the work, and how often.</summary>
    public AutosaveOptions Options
    {
        get
        {
            lock (_lock)
            {
                return _options;
            }
        }
    }

    /// <summary>
    /// Switches the autosave on or off and sets how often it keeps the work. The choice is kept
    /// for the next start. Off, it keeps nothing any more, and the autosave file goes.
    /// </summary>
    /// <param name="on">True to keep the work.</param>
    /// <param name="seconds">How often: one of <see cref="Intervals"/>.</param>
    /// <exception cref="DeskException">Another interval, or the choice could not be kept.</exception>
    public AutosaveOptions SetOptions(bool on, int seconds)
    {
        if (!Intervals.Contains(seconds))
            throw new DeskException($"The autosave can keep the work every {string.Join(", ", Intervals)} seconds, not every {seconds}.");

        lock (_lock)
        {
            var options = new AutosaveOptions(on, seconds);
            try
            {
                string? folder = Path.GetDirectoryName(_optionsFile);
                if (folder is not null)
                    Directory.CreateDirectory(folder);

                File.WriteAllText(_optionsFile, JsonSerializer.Serialize(options, JsonOptions));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new DeskException($"The autosave could not be set: {e.Message}");
            }

            _options = options;
            if (_timer is not null)
                _timer.Period = TimeSpan.FromSeconds(seconds);

            if (!on)
            {
                TryDelete(_file);
                _written = null;
            }

            return options;
        }
    }

    /// <summary>
    /// At the start of the desk program: brings back the work an autosave file holds, if there is
    /// one. A file that cannot be used is put aside under another name, and the desk starts as
    /// usual.
    /// </summary>
    /// <returns>True if work was brought back.</returns>
    public bool Restore()
    {
        lock (_lock)
        {
            ReadOptions();
            if (!File.Exists(_file))
                return false;

            try
            {
                (string name, string? path, ProjectContent content, DateTimeOffset kept) = Read();
                ProjectInfo project = _projects.Recover(name, path, content, kept);
                _written = _desk.ProjectChanges;
                _log.LogInformation(
                    "Brought back the unsaved work on the project {Name}, kept at {Kept:yyyy-MM-dd HH:mm:ss}.",
                    project.Name,
                    kept.ToLocalTime());
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or DeskException)
            {
                string aside = Path.ChangeExtension(_file, ".damaged.json");
                _log.LogWarning(
                    "The autosave file {File} could not be used, so nothing was brought back ({Reason}). It is kept as {Aside}.",
                    _file,
                    e.Message,
                    aside);
                TryMove(_file, aside);
                return false;
            }
        }
    }

    /// <summary>
    /// Brings the autosave file up to date: writes the project into it if it has changes that are
    /// neither in its file nor in the autosave file yet, and deletes it once nothing is unsaved.
    /// </summary>
    public void Keep()
    {
        lock (_lock)
        {
            if (!_options.On)
                return;

            ProjectInfo project = _projects.Current;
            if (!project.Unsaved)
            {
                if (_written is not null || File.Exists(_file))
                    TryDelete(_file);

                _written = null;
                return;
            }

            if (_desk.ProjectChanges == _written)
                return;

            (ProjectContent content, long changes) = _desk.GetProject();
            try
            {
                Write(project, content);
                _written = changes;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Tried again at the next round; the log says it once a round, which is enough.
                _log.LogWarning("The unsaved work could not be kept in {File}: {Reason}", _file, e.Message);
            }
        }
    }

    /// <summary>Keeps the work every few seconds while the program runs.</summary>
    /// <param name="stoppingToken">Ends the loop when the program stops.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        PeriodicTimer timer;
        lock (_lock)
        {
            timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.Seconds), _time);
            _timer = timer;
        }

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                Keep();
        }
        catch (OperationCanceledException)
        {
            // The program stops; StopAsync keeps the last changes.
        }
        finally
        {
            lock (_lock)
            {
                _timer = null;
            }

            timer.Dispose();
        }
    }

    // The choice of on or off and of the interval, from the last run; the usual ones without it.
    private void ReadOptions()
    {
        try
        {
            if (!File.Exists(_optionsFile))
                return;

            AutosaveOptions? kept = JsonSerializer.Deserialize<AutosaveOptions>(File.ReadAllText(_optionsFile), JsonOptions);
            if (kept is not null && Intervals.Contains(kept.Seconds))
                _options = kept;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            _log.LogWarning("The autosave options in {File} could not be read; it keeps the work every 5 seconds.", _optionsFile);
        }
    }

    /// <summary>When the program stops: the changes of the last seconds are kept too.</summary>
    /// <param name="cancellationToken">Tells when stopping has to be quick.</param>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        Keep();
    }

    // Writes a temporary file first and then moves it over the real one, as the projects do:
    // the autosave file is always whole, the old one or the new one.
    private void Write(ProjectInfo project, ProjectContent content)
    {
        using var written = new MemoryStream();
        ProjectFormat.Write(written, content);
        written.Position = 0;
        using JsonDocument projectJson = JsonDocument.Parse(written);

        var file = new AutosaveFile
        {
            Format = FormatName,
            Version = CurrentVersion,
            Kept = _time.GetLocalNow(),
            Name = project.Name,
            Path = project.Path,
            Project = projectJson.RootElement.Clone(),
        };

        string? folder = Path.GetDirectoryName(_file);
        if (folder is not null)
            Directory.CreateDirectory(folder);

        string temporary = _file + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, file, JsonOptions);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temporary, _file, overwrite: true);
    }

    private (string Name, string? Path, ProjectContent Content, DateTimeOffset Kept) Read()
    {
        AutosaveFile file;
        try
        {
            using FileStream stream = File.OpenRead(_file);
            file = JsonSerializer.Deserialize<AutosaveFile>(stream, JsonOptions) ?? throw new InvalidDataException("The file is empty.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"The file is damaged: {e.Message}");
        }

        if (file.Format != FormatName || file.Version != CurrentVersion)
            throw new InvalidDataException("The file is not an autosave file of this program.");
        if (file.Project is not { ValueKind: JsonValueKind.Object } projectJson)
            throw new InvalidDataException("The file holds no project.");

        ProjectName.Require(file.Name);
        using var project = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(projectJson));
        ProjectContent content = ProjectFormat.Read(project);
        return (file.Name!, file.Path, content, file.Kept ?? _time.GetLocalNow());
    }

    private void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.LogWarning("The autosave file {File} could not be deleted: {Reason}", path, e.Message);
        }
    }

    private void TryMove(string from, string to)
    {
        try
        {
            File.Move(from, to, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _log.LogWarning("The autosave file {File} could not be put aside: {Reason}", from, e.Message);
        }
    }

    /// <summary>The autosave file as it stands on the disk.</summary>
    private sealed class AutosaveFile
    {
        public string? Format { get; set; }

        public int? Version { get; set; }

        public DateTimeOffset? Kept { get; set; }

        public string? Name { get; set; }

        public string? Path { get; set; }

        public JsonElement? Project { get; set; }
    }
}

/// <summary>Whether the autosave keeps the work that is not saved, and how often.</summary>
/// <param name="On">True: it keeps the work.</param>
/// <param name="Seconds">How often, in seconds: 5, 30, 60 or 300.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record AutosaveOptions(bool On, int Seconds);
