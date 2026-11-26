using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DmxBox.Server.Sounds;

/// <summary>
/// The file of Export, and of a backup: one ZIP that holds each part on its own, as JSON, and the sound
/// files of the project:
/// <code>
/// lumenet-export.json   which parts it holds, and the name of the project
/// project.json          the project file
/// stage.json            the stage alone
/// settings.json         the settings of the device that made it
/// keyboard.json         its keyboard shortcuts
/// midi.json             the MIDI controllers of the PC
/// sounds/thunder.mp3    the sound files of the project
/// </code>
/// The page hands the parts over as one text of Export (version 1 of "lumenet-export", every part in
/// it) and gets that text back from a ZIP; Import still reads a ZIP of before 2026-11-26, which held
/// that text as "Name.json" and the sounds in "Name sounds/". The sound files go into the store.
/// </summary>
internal static class SoundPack
{
    /// <summary>The longest file of Export a pack holds.</summary>
    public const int MaxJsonBytes = 64 * 1024 * 1024;

    /// <summary>The most a pack can bring, its sounds and all.</summary>
    public const long MaxPackBytes = 8L * 1024 * 1024 * 1024;

    /// <summary>The file at the top of a pack that says what it holds.</summary>
    public const string ManifestName = "lumenet-export.json";

    /// <summary>The folder of the sound files in a pack.</summary>
    public const string SoundsFolder = "sounds";

    // The text of Export the page reads, and the layout of the pack.
    private const string ExportFormat = "lumenet-export";
    private const int TextVersion = 1;
    private const int PackVersion = 2;

    /// <summary>The parts a pack can hold, each a JSON file of that name at its top, in this order.</summary>
    public static readonly string[] Parts = ["project", "stage", "settings", "keyboard", "midi"];

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>
    /// Writes a pack: each part of the text of Export as a file of its own, and the file of every sound
    /// of its project that the store has, each once.
    /// </summary>
    /// <param name="output">Where the ZIP goes; it stays open.</param>
    /// <param name="json">The text of Export, as the page makes it.</param>
    /// <param name="store">The sound files of the desk program.</param>
    /// <returns>The names of the files of sounds the store does not have: those are not in the pack.</returns>
    /// <exception cref="DeskException">The text is no JSON of Export.</exception>
    public static string[] Write(Stream output, string json, SoundStore store)
    {
        JsonObject export = ParseExport(json);
        string[] parts = [.. Parts.Where(part => export[part] is not null)];
        var manifest = new JsonObject
        {
            ["format"] = ExportFormat,
            ["version"] = PackVersion,
            ["name"] = (string?)export["name"] ?? "",
            ["parts"] = new JsonArray([.. parts.Select(part => (JsonNode)part)]),
        };

        var missing = new List<string>();
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        WriteText(zip, ManifestName, manifest.ToJsonString(Indented));
        foreach (string part in parts)
            WriteText(zip, part + ".json", export[part]!.ToJsonString(Indented));

        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string file, string hash) in SoundsOf(export))
        {
            if (!written.Add(file))
                continue;
            if (store.PathOf(hash) is not { } path)
            {
                missing.Add(file);
                continue;
            }

            // sound files are packed already: stored as they are
            ZipArchiveEntry entry = zip.CreateEntry($"{SoundsFolder}/{file}", CompressionLevel.NoCompression);
            using Stream to = entry.Open();
            using FileStream from = File.OpenRead(path);
            from.CopyTo(to);
        }

        return [.. missing];
    }

    /// <summary>
    /// Reads a pack: takes every sound file in a folder of it into the store, and gives back its parts
    /// as one text of Export, as the page reads it. A pack of before 2026-11-26 gives its own text.
    /// </summary>
    /// <param name="pack">The ZIP; it must be seekable, and it stays open.</param>
    /// <param name="store">The sound files of the desk program.</param>
    /// <param name="cancel">Stops it.</param>
    /// <returns>The text of Export, and the names of the files that could not be taken (no sound, too big).</returns>
    /// <exception cref="DeskException">It is no ZIP, holds no file of Export, or a newer Lumenet made it.</exception>
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
            string json = zip.GetEntry(ManifestName) is { } manifest
                ? await TextOfPartsAsync(zip, manifest, cancel)
                : await TextOfBeforeAsync(zip, cancel);

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

            return (json, [.. leftOut]);
        }
    }

    /// <summary>A name for a file: without what a file name cannot have.</summary>
    public static string SafeName(string? name)
    {
        string kept = new([.. (name ?? "").Where(letter => !Path.GetInvalidFileNameChars().Contains(letter) && letter is not ('/' or '\\'))]);
        kept = kept.Trim().TrimEnd('.');
        return kept.Length == 0 ? "Lumenet" : kept;
    }

    // The parts of a pack of now as one text of Export: those its manifest names that it holds.
    private static async Task<string> TextOfPartsAsync(ZipArchive zip, ZipArchiveEntry manifest, CancellationToken cancel)
    {
        JsonObject said = await JsonOfAsync(manifest, cancel) as JsonObject
            ?? throw new DeskException($"The {ManifestName} of the ZIP says nothing.");
        if ((string?)said["format"] != ExportFormat)
            throw new DeskException($"The {ManifestName} of the ZIP is no file of Lumenet.");
        if (said["version"]?.GetValueKind() != JsonValueKind.Number || (int)said["version"]! > PackVersion)
            throw new DeskException("A newer Lumenet made the ZIP: this one cannot read it.");

        var export = new JsonObject
        {
            ["format"] = ExportFormat,
            ["version"] = TextVersion,
            ["name"] = (string?)said["name"] ?? "",
        };
        foreach (string part in Parts)
        {
            if (zip.GetEntry(part + ".json") is { } entry)
                export[part] = await JsonOfAsync(entry, cancel);
        }

        return export.ToJsonString(Indented);
    }

    // A pack of before 2026-11-26: the text of Export at its top, as it was written.
    private static async Task<string> TextOfBeforeAsync(ZipArchive zip, CancellationToken cancel)
    {
        ZipArchiveEntry json = zip.Entries.FirstOrDefault(entry =>
                !entry.FullName.Contains('/', StringComparison.Ordinal) && entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            ?? throw new DeskException("The ZIP holds no file of Export (a .json at its top).");
        if (json.Length > MaxJsonBytes)
            throw new DeskException($"The file of Export in the ZIP is bigger than {MaxJsonBytes >> 20} MB.");

        using var reader = new StreamReader(json.Open(), Encoding.UTF8);
        return await reader.ReadToEndAsync(cancel);
    }

    private static async Task<JsonNode?> JsonOfAsync(ZipArchiveEntry entry, CancellationToken cancel)
    {
        if (entry.Length > MaxJsonBytes)
            throw new DeskException($"{entry.Name} in the ZIP is bigger than {MaxJsonBytes >> 20} MB.");
        try
        {
            await using Stream from = entry.Open();
            return await JsonNode.ParseAsync(from, cancellationToken: cancel);
        }
        catch (JsonException)
        {
            throw new DeskException($"{entry.Name} in the ZIP is no JSON.");
        }
    }

    private static void WriteText(ZipArchive zip, string name, string text)
    {
        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(text);
    }

    // The text of Export as the page makes it.
    private static JsonObject ParseExport(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? throw new DeskException("The file of Export is no JSON object.");
        }
        catch (JsonException)
        {
            throw new DeskException("The file of Export is no JSON.");
        }
    }

    // The sounds of the project in a text of Export ("project": a project file): their files and hashes.
    private static IEnumerable<(string File, string Hash)> SoundsOf(JsonObject export)
    {
        if (export["project"]?["sounds"] is not JsonArray sounds)
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
