using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DmxBox.Server.Projects;

/// <summary>Where the backups go, whether a save makes one, and how many are kept. It belongs to the PC (backup.json).</summary>
/// <param name="Folder">The folder, as a full path; empty while none is chosen.</param>
/// <param name="OnSave">True: every save of the project makes a backup.</param>
/// <param name="Keep">How many backups the folder keeps: the oldest go.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record BackupSetup(string Folder, bool OnSave, int Keep)
{
    /// <summary>How many backups a folder keeps at first.</summary>
    public const int DefaultKeep = 30;

    /// <summary>The most backups a folder keeps.</summary>
    public const int MaxKeep = 1000;

    /// <summary>No folder yet: a save makes a backup once there is one.</summary>
    public static readonly BackupSetup None = new("", OnSave: true, DefaultKeep);

    /// <summary>The setup, if it is possible.</summary>
    /// <exception cref="DeskException">The folder is no full path, or the number is out of range.</exception>
    internal BackupSetup Checked()
    {
        string folder = (Folder ?? "").Trim();
        if (folder.Length > 0 && (!Path.IsPathFullyQualified(folder) || folder.IndexOfAny(Path.GetInvalidPathChars()) >= 0))
            throw new DeskException("The folder of the backups has to be a whole path, like G:\\My Drive\\Lumenet backups.");
        if (Keep is < 1 or > MaxKeep)
            throw new DeskException($"A folder keeps 1 to {MaxKeep} backups.");
        return this with { Folder = folder };
    }
}

/// <summary>The backups as the Backup tab of Settings shows them.</summary>
/// <param name="Folder">The folder; empty while none is chosen.</param>
/// <param name="OnSave">True: every save makes a backup.</param>
/// <param name="Keep">How many the folder keeps.</param>
/// <param name="Last">The newest backup in the folder, its file name; null for none.</param>
/// <param name="LastAt">When it was made, in milliseconds since 1970 (UTC); null for none.</param>
/// <param name="Count">How many backups the folder has.</param>
/// <param name="Problem">Why the folder cannot be used now (not there, cannot be written); null while it can.</param>
/// <param name="GoogleDrive">A folder in "My Drive" of Google Drive for desktop, to choose; null where it is not installed.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record BackupView(
    string Folder, bool OnSave, int Keep, string? Last, long? LastAt, int Count, string? Problem, string? GoogleDrive);

/// <summary>A backup that was made: its file, and the sound files of the project it could not hold.</summary>
/// <param name="File">The file's name.</param>
/// <param name="Missing">The sound files the desk program does not have.</param>
/// <param name="Backups">The backups now.</param>
/// <remarks>Public only because the hub's answers are.</remarks>
public sealed record BackupMade(string File, string[] Missing, BackupView Backups);

/// <summary>
/// Backups: everything Export can take (the project with its sound files, the stage, the settings and the
/// keyboard of the PC, the MIDI), one ZIP each time (<see cref="SoundPack"/>), into a folder of the PC.
/// A folder that Google Drive for desktop syncs, in "My Drive", goes up to Google Drive by itself; so
/// does one of OneDrive or Dropbox. The page on the PC asks for a backup (Back up now, and after a save);
/// the desk program writes it, keeps the newest <see cref="BackupSetup.Keep"/> and deletes older ones of
/// its own (their names begin with <see cref="Prefix"/>), never another file. Import brings one back.
/// </summary>
/// <remarks>Thread-safe.</remarks>
internal sealed class Backups
{
    /// <summary>How the name of every backup begins; only such files are ever deleted.</summary>
    public const string Prefix = "Lumenet backup - ";

    private readonly Lock _lock = new();
    private readonly BackupSetupFile _file;
    private readonly SoundStore _store;
    private readonly Func<DateTime> _now;
    private BackupSetup _setup;

    /// <summary>The backups of the PC.</summary>
    /// <param name="file">backup.json.</param>
    /// <param name="store">The sound files of the desk program.</param>
    /// <param name="now">The time of now, as the PC has it; null for the clock (the tests pass theirs).</param>
    public Backups(BackupSetupFile file, SoundStore store, Func<DateTime>? now = null)
    {
        _file = file;
        _store = store;
        _now = now ?? (() => DateTime.Now);
        _setup = file.Read();
    }

    /// <summary>The backups as the Backup tab shows them.</summary>
    public BackupView View
    {
        get
        {
            lock (_lock)
                return ViewLocked();
        }
    }

    /// <summary>Another folder, another number to keep, a save that makes a backup or not; kept in backup.json.</summary>
    /// <param name="setup">The setup.</param>
    /// <exception cref="DeskException">It is not possible, or backup.json cannot be written.</exception>
    public BackupView Set(BackupSetup setup)
    {
        BackupSetup checkedSetup = (setup ?? throw new DeskException("Which backups?")).Checked();
        lock (_lock)
        {
            try
            {
                _file.Write(checkedSetup);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new DeskException($"The setup of the backups cannot be kept in {_file.Path}: {e.Message}");
            }

            _setup = checkedSetup;
            return ViewLocked();
        }
    }

    /// <summary>
    /// Makes a backup of a text of Export: a ZIP in the folder, named after the project and the time,
    /// "Lumenet backup - Concert - 2026-11-26 14.05.33.zip". It is written beside under another name and
    /// takes its own one when it is whole, so that a sync never takes half a file. Older backups beyond
    /// the number to keep go.
    /// </summary>
    /// <param name="json">The text of Export with every part.</param>
    /// <param name="project">The name of the project.</param>
    /// <exception cref="DeskException">No folder is chosen, it cannot be reached, or the text is no Export.</exception>
    public BackupMade Write(string json, string project)
    {
        lock (_lock)
        {
            string folder = _setup.Folder;
            if (folder.Length == 0)
                throw new DeskException("Choose the folder of the backups first.");

            string name = $"{Prefix}{SoundPack.SafeName(project)} - {_now().ToString("yyyy-MM-dd HH.mm.ss", CultureInfo.InvariantCulture)}.zip";
            string path = Path.Combine(folder, name);
            string partial = path + ".partial";
            string[] missing;
            try
            {
                Directory.CreateDirectory(folder);
                using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
                    missing = SoundPack.Write(output, json, _store);
                File.Move(partial, path, overwrite: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                TryDelete(partial);
                throw new DeskException($"The backup cannot be written to {folder}: {e.Message}");
            }
            catch (DeskException)
            {
                TryDelete(partial);
                throw;
            }

            Prune(folder, _setup.Keep);
            return new BackupMade(name, missing, ViewLocked());
        }
    }

    /// <summary>
    /// The folder "Lumenet backups" in "My Drive" of Google Drive for desktop, where it is installed: on a
    /// drive of its own (G:\My Drive), or in the user's folder (the way of the older Backup and Sync).
    /// Null where none is found.
    /// </summary>
    public static string? GoogleDriveFolder()
    {
        var candidates = new List<string>();
        foreach (DriveInfo drive in SafeDrives())
            candidates.Add(Path.Combine(drive.RootDirectory.FullName, "My Drive"));
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.Length > 0)
        {
            candidates.Add(Path.Combine(home, "My Drive"));
            candidates.Add(Path.Combine(home, "Google Drive"));
        }

        string? found = candidates.FirstOrDefault(Directory.Exists);
        return found is null ? null : Path.Combine(found, "Lumenet backups");
    }

    // Call with the lock held.
    private BackupView ViewLocked()
    {
        string? problem = null;
        FileInfo[] backups = [];
        if (_setup.Folder.Length > 0)
        {
            try
            {
                var folder = new DirectoryInfo(_setup.Folder);
                if (folder.Exists)
                    backups = BackupsIn(folder);
                else if (!Directory.Exists(Path.GetPathRoot(_setup.Folder)))
                    problem = $"{Path.GetPathRoot(_setup.Folder)} is not there now: is the drive connected, and Google Drive running?";
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                problem = $"The folder cannot be read: {e.Message}";
            }
        }

        FileInfo? last = backups.FirstOrDefault();
        return new BackupView(
            _setup.Folder,
            _setup.OnSave,
            _setup.Keep,
            last?.Name,
            last is null ? null : new DateTimeOffset(last.LastWriteTimeUtc).ToUnixTimeMilliseconds(),
            backups.Length,
            problem,
            GoogleDriveFolder());
    }

    // The backups in a folder, the newest first.
    private static FileInfo[] BackupsIn(DirectoryInfo folder)
    {
        return [.. folder.EnumerateFiles(Prefix + "*.zip").OrderByDescending(file => file.LastWriteTimeUtc).ThenByDescending(file => file.Name, StringComparer.Ordinal)];
    }

    // The backups beyond the newest "keep" go; one that cannot be deleted now stays until the next time.
    private static void Prune(string folder, int keep)
    {
        try
        {
            foreach (FileInfo old in BackupsIn(new DirectoryInfo(folder)).Skip(keep))
                TryDelete(old.FullName);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // the next backup tries again
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // it stays
        }
    }

    // The drives that answer: a drive that is gone or not ready (an empty card reader) is passed over.
    private static IEnumerable<DriveInfo> SafeDrives()
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (DriveInfo drive in drives)
        {
            bool ready;
            try
            {
                ready = drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Network or DriveType.Removable;
            }
            catch (IOException)
            {
                ready = false;
            }

            if (ready)
                yield return drive;
        }
    }
}

/// <summary>backup.json: the setup of the backups, beside midi.json and the LAN password.</summary>
internal sealed class BackupSetupFile
{
    /// <summary>The name of the file, beside the LAN password.</summary>
    public const string FileName = "backup.json";

    private const string Format = "lumenet-backup";
    private const int Version = 1;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _path;
    private readonly ILogger _log;

    /// <summary>The file at this path.</summary>
    /// <param name="path">Usually backup.json beside the LAN password.</param>
    /// <param name="log">Where a file that cannot be read is told about.</param>
    public BackupSetupFile(string path, ILogger log)
    {
        _path = path;
        _log = log;
    }

    /// <summary>The path of the file.</summary>
    public string Path => _path;

    /// <summary>The setup in the file; none when there is no file or it cannot be read.</summary>
    public BackupSetup Read()
    {
        if (!File.Exists(_path))
            return BackupSetup.None;

        try
        {
            FileShape? shape = JsonSerializer.Deserialize<FileShape>(File.ReadAllText(_path), Options);
            if (shape is null || shape.Format != Format)
                throw new DeskException("It is no setup of the backups of Lumenet.");

            return new BackupSetup(shape.Folder ?? "", shape.OnSave ?? true, shape.Keep ?? BackupSetup.DefaultKeep).Checked();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or DeskException)
        {
            _log.LogWarning("The setup of the backups {File} cannot be read ({Reason}): there are none until a folder is chosen.", _path, e.Message);
            return BackupSetup.None;
        }
    }

    /// <summary>Writes the setup, beside the file first and then in its place.</summary>
    /// <param name="setup">The setup, checked.</param>
    public void Write(BackupSetup setup)
    {
        string? folder = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        string written = _path + ".new";
        var shape = new FileShape { Format = Format, Version = Version, Folder = setup.Folder, OnSave = setup.OnSave, Keep = setup.Keep };
        File.WriteAllText(written, JsonSerializer.Serialize(shape, Options));
        File.Move(written, _path, overwrite: true);
    }

    // The file as JSON.
    private sealed class FileShape
    {
        [JsonPropertyOrder(-2)]
        public string? Format { get; set; }

        [JsonPropertyOrder(-1)]
        public int Version { get; set; }

        public string? Folder { get; set; }

        public bool? OnSave { get; set; }

        public int? Keep { get; set; }
    }
}
