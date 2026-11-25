using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DmxBox.Server;

/// <summary>
/// A file of Export that takes the sounds of its project along: a ZIP that holds the file of Export as
/// "Name.json" and the project's sound files in "Name sounds/", as Save lays them out beside a
/// project's file. Import takes such a file: the sound files go into the store, and the JSON is read
/// as any file of Export.
/// </summary>
internal static class SoundPack
{
    /// <summary>The longest file of Export a pack holds.</summary>
    public const int MaxJsonBytes = 64 * 1024 * 1024;

    /// <summary>The most a pack can bring, its sounds and all.</summary>
    public const long MaxPackBytes = 8L * 1024 * 1024 * 1024;

    /// <summary>
    /// Writes a pack: the text of Export, and the file of every sound of its project that the store
    /// has, each once.
    /// </summary>
    /// <param name="output">Where the ZIP goes; it stays open.</param>
    /// <param name="name">The name of the file of Export without ".json": the names inside the ZIP.</param>
    /// <param name="json">The text of Export.</param>
    /// <param name="store">The sound files of the desk program.</param>
    /// <returns>The names of the files of sounds the store does not have: those are not in the pack.</returns>
    /// <exception cref="DeskException">The text is no JSON.</exception>
    public static string[] Write(Stream output, string name, string json, SoundStore store)
    {
        string baseName = SafeName(name);
        var missing = new List<string>();
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        ZipArchiveEntry text = zip.CreateEntry(baseName + ".json", CompressionLevel.Optimal);
        using (var writer = new StreamWriter(text.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            writer.Write(json);

        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string file, string hash) in SoundsOf(json))
        {
            if (!written.Add(file))
                continue;
            if (store.PathOf(hash) is not { } path)
            {
                missing.Add(file);
                continue;
            }

            // sound files are packed already: stored as they are
            ZipArchiveEntry entry = zip.CreateEntry($"{baseName}{SoundStore.BesideSuffix}/{file}", CompressionLevel.NoCompression);
            using Stream to = entry.Open();
            using FileStream from = File.OpenRead(path);
            from.CopyTo(to);
        }

        return [.. missing];
    }

    /// <summary>
    /// Reads a pack: takes every sound file in a folder of it into the store, and gives back the file
    /// of Export at its top.
    /// </summary>
    /// <param name="pack">The ZIP; it must be seekable, and it stays open.</param>
    /// <param name="store">The sound files of the desk program.</param>
    /// <param name="cancel">Stops it.</param>
    /// <returns>The text of Export, and the names of the files that could not be taken (no sound, too big).</returns>
    /// <exception cref="DeskException">It is no ZIP, or holds no file of Export.</exception>
    public static async Task<(string Json, string[] LeftOut)> ReadAsync(Stream pack, SoundStore store, CancellationToken cancel)
    {
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(pack, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            throw new DeskException("The file is no ZIP of Export.");
        }

        using (zip)
        {
            ZipArchiveEntry json = zip.Entries.FirstOrDefault(entry =>
                    !entry.FullName.Contains('/', StringComparison.Ordinal) && entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                ?? throw new DeskException("The ZIP holds no file of Export (a .json at its top).");
            if (json.Length > MaxJsonBytes)
                throw new DeskException($"The file of Export in the ZIP is bigger than {MaxJsonBytes >> 20} MB.");

            var leftOut = new List<string>();
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                bool inFolder = entry.FullName.Contains('/', StringComparison.Ordinal) && entry.Name.Length > 0;
                if (!inFolder || !SoundStore.Endings.Contains(Path.GetExtension(entry.Name).ToLowerInvariant()))
                    continue;

                try
                {
                    await using Stream from = entry.Open();
                    await store.AddAsync(from, entry.Name, cancel);
                }
                catch (DeskException)
                {
                    leftOut.Add(entry.Name);
                }
            }

            using var reader = new StreamReader(json.Open(), Encoding.UTF8);
            return (await reader.ReadToEndAsync(cancel), [.. leftOut]);
        }
    }

    /// <summary>A name for the files inside a pack: without what a file name cannot have.</summary>
    public static string SafeName(string? name)
    {
        string kept = new([.. (name ?? "").Where(letter => !Path.GetInvalidFileNameChars().Contains(letter) && letter is not ('/' or '\\'))]);
        kept = kept.Trim().TrimEnd('.');
        return kept.Length == 0 ? "Lumenet" : kept;
    }

    // The sounds of the project in a file of Export ("project": a project file) or in a project file
    // itself: their files and hashes.
    private static IEnumerable<(string File, string Hash)> SoundsOf(string json)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            throw new DeskException("The file of Export is no JSON.");
        }

        JsonNode? project = root?["project"] ?? (root?["format"]?.GetValueKind() == JsonValueKind.String && (string?)root["format"] == "lumenet-project" ? root : null);
        if (project?["sounds"] is not JsonArray sounds)
            yield break;

        foreach (JsonNode? sound in sounds)
        {
            if (sound?["file"]?.GetValueKind() != JsonValueKind.String || sound["hash"]?.GetValueKind() != JsonValueKind.String)
                continue;

            string file = (string)sound["file"]!;
            string hash = ((string)sound["hash"]!).Trim().ToLowerInvariant();
            if (file.Length == 0 || file.IndexOfAny(['/', '\\']) >= 0 || !SoundStore.IsHash(hash))
                continue;

            yield return (file, hash);
        }
    }
}
