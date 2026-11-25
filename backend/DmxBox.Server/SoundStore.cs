using System.Security.Cryptography;
using NAudio.Wave;

namespace DmxBox.Server;

/// <summary>
/// The sound files the desk program plays, kept in a folder of its own under a hash of their
/// content (<c>3f2a....mp3</c>): a file added twice is kept once, and a project that is not saved
/// yet, an older moment of undo or the autosave still find their files.
/// </summary>
/// <remarks>
/// <para>
/// A project's files also go beside its file when it is saved: "Show.json" gets a folder "Show
/// sounds" with each file under the name of its sound's file ("thunder.mp3"). So a project is
/// moved to another PC with that folder, and loading it takes in the files the desk program does
/// not have yet (<see cref="LoadBeside"/>).
/// </para>
/// <para>Thread-safe.</para>
/// </remarks>
internal sealed class SoundStore
{
    /// <summary>The kinds of files that can be added, by their endings.</summary>
    public static readonly string[] Endings = [".mp3", ".wav", ".aif", ".aiff", ".flac", ".m4a", ".aac", ".wma"];

    /// <summary>The biggest file that can be added: 1 GB, a few hours of WAV.</summary>
    public const long MaxBytes = 1L << 30;

    /// <summary>The longest name a file can have.</summary>
    public const int MaxFileNameLength = 120;

    /// <summary>What the folder of a project's sounds is called after the project: "Show sounds" beside "Show.json".</summary>
    public const string BesideSuffix = " sounds";

    private readonly Lock _lock = new();
    private readonly Dictionary<string, SoundFileInfo> _known = [];

    // The waves measured since the start, by hash: a file of a hash never changes.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SoundWave> _waves = new();

    /// <summary>Keeps the files in a folder, which is made when the first file comes.</summary>
    /// <param name="folder">The folder.</param>
    public SoundStore(string folder)
    {
        Folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
    }

    /// <summary>The folder, as a full path.</summary>
    public string Folder { get; }

    /// <summary>True for a name the store keeps a file under: 32 hex digits, small.</summary>
    public static bool IsHash(string? hash)
    {
        return hash is { Length: 32 } && hash.All(digit => digit is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    /// <summary>
    /// The name of a sound's file as the desk keeps it: a name alone, no folder, with one of the
    /// <see cref="Endings"/>.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="where">The sound, for the message.</param>
    /// <exception cref="DeskException">The name cannot be used.</exception>
    public static string CheckedFileName(string? name, string where)
    {
        string trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxFileNameLength)
            throw new DeskException($"{where}: the name of its file has 1 to {MaxFileNameLength} characters.");
        if (trimmed.IndexOfAny([.. Path.GetInvalidFileNameChars(), '/', '\\', ':']) >= 0 || trimmed is "." or "..")
            throw new DeskException($"{where}: \"{trimmed}\" is no name of a file.");
        if (!Endings.Contains(Path.GetExtension(trimmed).ToLowerInvariant()))
            throw new DeskException($"{where}: a file of sound ends with {string.Join(", ", Endings)}, not \"{trimmed}\".");

        return trimmed;
    }

    /// <summary>
    /// Takes in a file: keeps it under the hash of its content, unless a file with that content is
    /// there already, and tells what it is. A file that cannot be played is refused.
    /// </summary>
    /// <param name="content">The file's content; it is read to its end.</param>
    /// <param name="fileName">The name of the file, like "thunder.mp3".</param>
    /// <returns>The file as the store keeps it.</returns>
    /// <exception cref="DeskException">It is too big, its name or kind cannot be used, or it cannot be played.</exception>
    public SoundFileInfo Add(Stream content, string? fileName)
    {
        return AddAsync(content, fileName, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>As <see cref="Add"/>, reading the content as it comes: a file sent by a page.</summary>
    /// <param name="content">The file's content; it is read to its end.</param>
    /// <param name="fileName">The name of the file, like "thunder.mp3".</param>
    /// <param name="cancel">Stops the reading.</param>
    /// <returns>The file as the store keeps it.</returns>
    /// <exception cref="DeskException">It is too big, its name or kind cannot be used, or it cannot be played.</exception>
    public async Task<SoundFileInfo> AddAsync(Stream content, string? fileName, CancellationToken cancel)
    {
        string name = CheckedFileName(fileName, "The sound");
        string ending = Path.GetExtension(name).ToLowerInvariant();
        string temporary = Path.Combine(Folder, $"adding-{Guid.NewGuid():N}{ending}");
        try
        {
            Directory.CreateDirectory(Folder);
            string hash;
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                byte[] buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await content.ReadAsync(buffer, cancel)) > 0)
                {
                    total += read;
                    if (total > MaxBytes)
                        throw new DeskException($"\"{name}\" is bigger than {MaxBytes >> 20} MB.");

                    sha.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), cancel);
                }

                if (total == 0)
                    throw new DeskException($"\"{name}\" is empty.");

                hash = Convert.ToHexStringLower(sha.GetHashAndReset())[..32];
            }

            string path = Path.Combine(Folder, hash + ending);
            lock (_lock)
            {
                if (FindPath(hash) is { } kept)
                {
                    File.Delete(temporary);
                    return Info(hash, kept, name);
                }

                SoundFileInfo info = Probe(hash, temporary, name);
                File.Move(temporary, path);
                _known[hash] = info;
                return info;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new DeskException($"\"{name}\" could not be kept in {Folder}. {e.Message}");
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    /// <summary>Where the file with this hash is; null when the store does not have it.</summary>
    /// <param name="hash">The hash, see <see cref="Sound.Hash"/>.</param>
    public string? PathOf(string? hash)
    {
        if (!IsHash(hash))
            return null;

        lock (_lock)
        {
            return FindPath(hash!);
        }
    }

    /// <summary>What the file with this hash is; null when the store does not have it, or it cannot be played.</summary>
    /// <param name="hash">The hash.</param>
    /// <param name="name">The name to give it in the answer.</param>
    public SoundFileInfo? InfoOf(string? hash, string name = "")
    {
        if (!IsHash(hash))
            return null;

        lock (_lock)
        {
            if (FindPath(hash!) is not { } path)
                return null;

            try
            {
                return Info(hash!, path, name);
            }
            catch (DeskException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// How loud the file with this hash is along its length, for the wave the Sounds view draws.
    /// Measured once (it reads the whole file), then kept while the desk program runs.
    /// </summary>
    /// <param name="hash">The hash.</param>
    /// <returns>The wave; null when the store does not have the file.</returns>
    /// <exception cref="DeskException">The file cannot be read.</exception>
    public SoundWave? WaveOf(string? hash)
    {
        if (PathOf(hash) is not { } path)
            return null;
        if (_waves.TryGetValue(hash!, out SoundWave? known))
            return known;

        try
        {
            SoundWave wave = SoundWave.Measure(path);
            _waves[hash!] = wave;
            return wave;
        }
        catch (Exception e) when (e is not DeskException)
        {
            throw new DeskException($"The file of sound {hash} cannot be read: {e.Message}");
        }
    }

    /// <summary>
    /// Writes the files of a project's sounds beside the project's file: "Show.json" gets "Show
    /// sounds" with each file under its sound's <see cref="Sound.File"/>. A file that is there with
    /// the same content stays; files of the folder that no sound uses are left alone.
    /// </summary>
    /// <param name="projectFile">The project's file.</param>
    /// <param name="sounds">Its sounds.</param>
    /// <exception cref="DeskException">The folder cannot be written.</exception>
    public void SaveBeside(string projectFile, IEnumerable<Sound?> sounds)
    {
        Sound[] used = [.. sounds.OfType<Sound>().Where(sound => !sound.IsTrack)];   // a track of the mixer has no file here
        if (used.Length == 0)
            return;

        string folder = BesideFolder(projectFile);
        try
        {
            Directory.CreateDirectory(folder);
            foreach (Sound sound in used.DistinctBy(sound => sound.File, StringComparer.OrdinalIgnoreCase))
            {
                if (PathOf(sound.Hash) is not { } source)
                    continue;   // a file the store does not have stays as the folder has it

                string target = Path.Combine(folder, sound.File);
                if (File.Exists(target) && HashOf(target) == sound.Hash)
                    continue;

                File.Copy(source, target, overwrite: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new DeskException($"The sounds of the project could not be saved in {folder}. {e.Message}");
        }
    }

    /// <summary>
    /// Takes in the files beside a project's file that the store does not have, and gives each sound
    /// the file the folder has under its name: a file changed there is what the sound plays now.
    /// </summary>
    /// <param name="projectFile">The project's file.</param>
    /// <param name="sounds">Its sounds, by number; the array is changed in place.</param>
    /// <returns>The names of the files no one has: neither the folder nor the store.</returns>
    public string[] LoadBeside(string projectFile, Sound?[] sounds)
    {
        string folder = BesideFolder(projectFile);
        var missing = new List<string>();
        for (int index = 0; index < sounds.Length; index++)
        {
            if (sounds[index] is not { } sound || sound.IsTrack)
                continue;

            string beside = Path.Combine(folder, sound.File);
            if (File.Exists(beside))
            {
                try
                {
                    using FileStream stream = File.OpenRead(beside);
                    SoundFileInfo info = Add(stream, sound.File);
                    sounds[index] = sound with { Hash = info.Hash, Seconds = info.Seconds };
                    continue;
                }
                catch (Exception e) when (e is DeskException or IOException or UnauthorizedAccessException)
                {
                    // a file there that cannot be played: the one the store has, if any
                }
            }

            if (PathOf(sound.Hash) is null)
                missing.Add(sound.File);
        }

        return [.. missing.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The folder beside a project's file that holds its sounds: "Show sounds" beside "Show.json".</summary>
    public static string BesideFolder(string projectFile)
    {
        string full = Path.GetFullPath(projectFile);
        return Path.Combine(Path.GetDirectoryName(full) ?? "", Path.GetFileNameWithoutExtension(full) + BesideSuffix);
    }

    // Call with the lock held. The kept file of a hash, whatever its ending.
    private string? FindPath(string hash)
    {
        if (_known.TryGetValue(hash, out SoundFileInfo? info) && File.Exists(info.Path))
            return info.Path;
        if (!Directory.Exists(Folder))
            return null;

        return Directory.EnumerateFiles(Folder, hash + ".*").FirstOrDefault(path => Endings.Contains(Path.GetExtension(path).ToLowerInvariant()));
    }

    // Call with the lock held.
    private SoundFileInfo Info(string hash, string path, string name)
    {
        if (!_known.TryGetValue(hash, out SoundFileInfo? info) || info.Path != path)
            _known[hash] = info = Probe(hash, path, name);

        return info with { Name = name.Length > 0 ? name : info.Name };
    }

    // Opens the file as it will be played, to learn what it is; one that cannot be played is refused.
    private static SoundFileInfo Probe(string hash, string path, string name)
    {
        try
        {
            using WaveStream reader = SoundFiles.Open(path);
            return new SoundFileInfo(hash, name, Math.Round(reader.TotalTime.TotalSeconds, 3), reader.WaveFormat.Channels, reader.WaveFormat.SampleRate)
            {
                Path = path,
            };
        }
        catch (Exception e) when (e is not DeskException)
        {
            throw new DeskException($"\"{name}\" cannot be played: {e.Message}");
        }
    }

    private static string HashOf(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream))[..32];
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a file left behind does no harm: it has a name nothing looks for
        }
    }
}

/// <summary>A sound file the desk program keeps, as the UIs learn of it.</summary>
/// <param name="Hash">What it is kept under, see <see cref="Sound.Hash"/>.</param>
/// <param name="Name">Its name, like "thunder.mp3".</param>
/// <param name="Seconds">How long it is.</param>
/// <param name="Channels">1 for mono, 2 for stereo.</param>
/// <param name="Rate">Its samples per second.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record SoundFileInfo(string Hash, string Name, double Seconds, int Channels, int Rate)
{
    /// <summary>Where the desk program keeps it; the UIs do not get it.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    internal string Path { get; init; } = "";
}

/// <summary>Opens the sound files for playing: WAV and AIFF everywhere, the rest through Windows' Media Foundation.</summary>
internal static class SoundFiles
{
    /// <summary>A reader of the file, from its start.</summary>
    /// <param name="path">The file.</param>
    /// <exception cref="DeskException">It needs Windows to be played, and this is not Windows.</exception>
    public static WaveStream Open(string path)
    {
        string ending = Path.GetExtension(path).ToLowerInvariant();
        if (ending == ".wav")
            return new WaveFileReader(path);
        if (ending is ".aif" or ".aiff")
            return new AiffFileReader(path);
        if (!OperatingSystem.IsWindows())
            throw new DeskException($"A {ending} file plays on Windows only; WAV and AIFF play everywhere.");

        return new MediaFoundationReader(path);
    }
}
