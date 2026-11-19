using System.Text.Encodings.Web;
using System.Text.Json;

namespace DmxBox.Server;

/// <summary>
/// Reads and writes project files: JSON that lists the memories that hold a look, the notes
/// under the faders, and the channels of the independent keys.
/// </summary>
/// <remarks>
/// <code>
/// {
///   "format": "lumenet-project",
///   "version": 1,
///   "memories": [
///     { "number": 1, "levels": { "5": 255, "6": 128 } }
///   ],
///   "channelNotes": { "5": "Front left" },
///   "memoryNotes": { "1": "Blue wash" },
///   "independents": [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 ]
/// }
/// </code>
/// The notes and the independents may be missing: then there are no notes, and the keys are
/// on channels 1 to 16. That keeps the files from before these existed readable, and is why
/// the version is still 1.
/// </remarks>
internal static class ProjectFormat
{
    private const string FormatName = "lumenet-project";

    // The version this program writes, and the newest it can read.
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,

        // Notes go into the file as they were typed, so that it stays readable. Without this
        // every letter beyond ASCII would be written as a six-character code instead.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Writes a project file: UTF-8 without a byte order mark, indented.</summary>
    /// <param name="stream">Receives the file.</param>
    /// <param name="project">The looks, the notes and the independents to write.</param>
    public static void Write(Stream stream, ProjectContent project)
    {
        var file = new ProjectFile
        {
            Format = FormatName,
            Version = CurrentVersion,
            Memories = [],
            ChannelNotes = NotesThatAreNotEmpty(project.ChannelNotes),
            MemoryNotes = NotesThatAreNotEmpty(project.MemoryNotes),
            Independents = project.Independents.ToList(),
        };

        for (int index = 0; index < project.Looks.Length; index++)
        {
            if (project.Looks[index] is { } look)
                file.Memories.Add(new ProjectFileMemory { Number = index + 1, Levels = LevelsAboveZero(look) });
        }

        JsonSerializer.Serialize(stream, file, Options);
    }

    /// <summary>Reads a project file.</summary>
    /// <param name="stream">The file.</param>
    /// <returns>The looks, the notes and the independents, with an entry for every memory, fader and key.</returns>
    /// <exception cref="InvalidDataException">
    /// This is not a project file the program can use. The message says why.
    /// </exception>
    public static ProjectContent Read(Stream stream)
    {
        ProjectFile file = Parse(stream);
        RequireFormat(file);

        return new ProjectContent(
            LooksOf(file),
            NotesOf(file.ChannelNotes, Desk.ChannelCount, "channel"),
            NotesOf(file.MemoryNotes, Desk.MemoryCount, "memory"),
            IndependentsOf(file.Independents));
    }

    private static ProjectFile Parse(Stream stream)
    {
        try
        {
            return JsonSerializer.Deserialize<ProjectFile>(stream, Options) ?? throw Bad("The file holds no project.");
        }
        catch (JsonException e)
        {
            // Not JSON at all, or for example a text where a number belongs. The JSON reader's
            // message names the place in the file.
            throw Bad($"The file is damaged: {e.Message}");
        }
    }

    private static void RequireFormat(ProjectFile file)
    {
        if (file.Format != FormatName)
            throw Bad("The file is not a Lumenet project.");
        if (file.Version is not int version || version < 1)
            throw Bad("The file does not say which version of the project format it has.");
        if (version > CurrentVersion)
        {
            throw Bad(
                $"The file has version {version} of the project format, and this program reads up to " +
                $"version {CurrentVersion}. A newer Lumenet wrote the file.");
        }
    }

    // One entry per memory: its look, or null for a memory the file does not list.
    private static byte[]?[] LooksOf(ProjectFile file)
    {
        List<ProjectFileMemory?> memories = file.Memories ?? throw Bad("The file has no list of memories.");

        byte[]?[] looks = new byte[]?[Desk.MemoryCount];
        foreach (ProjectFileMemory? memory in memories)
        {
            if (memory is null)
                throw Bad("The list of memories in the file has an empty entry.");

            int number = NumberOf(memory);
            if (looks[number - 1] is not null)
                throw Bad($"The file lists memory {number} twice.");

            looks[number - 1] = LookOf(memory, number);
        }

        return looks;
    }

    private static int NumberOf(ProjectFileMemory memory)
    {
        if (memory.Number is not int number)
            throw Bad("The file has a memory without a number.");
        if (number < 1 || number > Desk.MemoryCount)
            throw Bad($"The file has a memory {number}; memories go from 1 to {Desk.MemoryCount}.");

        return number;
    }

    private static byte[] LookOf(ProjectFileMemory memory, int number)
    {
        SortedDictionary<int, int> levels = memory.Levels ?? throw Bad($"Memory {number} in the file has no levels.");

        byte[] look = new byte[Desk.ChannelCount];
        foreach ((int channel, int level) in levels)
        {
            if (channel < 1 || channel > Desk.ChannelCount)
            {
                throw Bad(
                    $"Memory {number} in the file has a channel {channel}; " +
                    $"channels go from 1 to {Desk.ChannelCount}.");
            }

            if (level < 0 || level > Desk.MaxLevel)
            {
                throw Bad(
                    $"Memory {number} in the file has level {level} for channel {channel}; " +
                    $"levels go from 0 to {Desk.MaxLevel}.");
            }

            look[channel - 1] = (byte)level;
        }

        return look;
    }

    // One note per fader, empty where the file lists none. "fader" says which faders these
    // are, "channel" or "memory", for the messages.
    private static string[] NotesOf(SortedDictionary<int, string?>? listed, int faderCount, string fader)
    {
        string[] notes = new string[faderCount];
        Array.Fill(notes, "");
        if (listed is null)
            return notes;

        foreach ((int number, string? text) in listed)
        {
            if (number < 1 || number > faderCount)
                throw Bad($"The file has a note for {fader} {number}; these numbers go from 1 to {faderCount}.");

            notes[number - 1] = NoteOf(text, fader, number);
        }

        return notes;
    }

    // The same rules as for a note typed on the UI, so that a file cannot bring in a note
    // that the desk itself would refuse.
    private static string NoteOf(string? text, string fader, int number)
    {
        if (text is null)
            throw Bad($"The note for {fader} {number} in the file is not a text.");

        try
        {
            return Note.Clean(text);
        }
        catch (DeskException e)
        {
            throw Bad($"The note for {fader} {number} in the file cannot be used. {e.Message}");
        }
    }

    // The channel of each independent key. A file may leave the list out: then the keys are
    // where they are on a new desk. A list that is there has to be whole.
    private static int[] IndependentsOf(List<int>? listed)
    {
        if (listed is null)
            return Desk.DefaultIndependents();
        if (listed.Count != Desk.IndependentCount)
            throw Bad($"The file lists {listed.Count} independents; there must be {Desk.IndependentCount}.");

        for (int index = 0; index < listed.Count; index++)
        {
            if (listed[index] < 1 || listed[index] > Desk.ChannelCount)
            {
                throw Bad(
                    $"Independent {index + 1} in the file is on channel {listed[index]}; " +
                    $"channels go from 1 to {Desk.ChannelCount}.");
            }
        }

        return listed.ToArray();
    }

    // Channel number and level of every channel that is not at 0.
    private static SortedDictionary<int, int> LevelsAboveZero(byte[] look)
    {
        var levels = new SortedDictionary<int, int>();
        for (int index = 0; index < look.Length; index++)
        {
            if (look[index] > 0)
                levels.Add(index + 1, look[index]);
        }

        return levels;
    }

    // Fader number and text of every fader that has a note.
    private static SortedDictionary<int, string?> NotesThatAreNotEmpty(string[] notes)
    {
        var listed = new SortedDictionary<int, string?>();
        for (int index = 0; index < notes.Length; index++)
        {
            if (notes[index] != "")
                listed.Add(index + 1, notes[index]);
        }

        return listed;
    }

    private static InvalidDataException Bad(string message)
    {
        return new InvalidDataException(message);
    }
}
