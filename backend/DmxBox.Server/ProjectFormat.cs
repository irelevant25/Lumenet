using System.Text.Encodings.Web;
using System.Text.Json;

namespace DmxBox.Server;

/// <summary>
/// Reads and writes project files: JSON that lists the memories that hold a look or a
/// sequence, the stack, the notes under the faders, and the channels of the independent keys.
/// </summary>
/// <remarks>
/// <code>
/// {
///   "format": "lumenet-project",
///   "version": 3,
///   "memories": [
///     { "number": 1, "levels": { "5": 255, "6": 128 } }
///   ],
///   "channelNotes": { "5": "Front left" },
///   "memoryNotes": { "1": "Blue wash" },
///   "independents": [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 ],
///   "memoryIndependents": [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 ],
///   "sequences": [
///     { "number": 2, "loop": true, "random": false, "speed": 1, "steps": [
///       { "target": "channel", "number": 5, "value": 255, "up": 1, "wait": 0.5, "down": 1 } ] }
///   ],
///   "stack": [
///     { "cue": "1.0", "target": "memory", "number": 1, "value": 255, "up": 2, "wait": 0, "down": 2 }
///   ]
/// }
/// </code>
/// The notes and the independents may be missing: then there are no notes, and the keys are
/// on channels 1 to 10 and memories 1 to 10. That keeps the files from before these existed
/// readable. Version 2 added the sequences and the stack; a file of version 1 has neither, and
/// is still read. A sequence that does not say whether it loops is played in a loop, one that
/// does not say "random" in turn, and one without a speed at speed 1. Version 3
/// has 10 keys for channels and 10 for memories among the independents; versions 1 and 2 had
/// 16 keys for channels, of which the first 10 are used.
/// </remarks>
internal static class ProjectFormat
{
    private const string FormatName = "lumenet-project";

    // The version this program writes, and the newest it can read.
    private const int CurrentVersion = 3;

    // The channel keys of the independents in a file of version 1 or 2.
    private const int OldIndependentCount = 16;

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
            MemoryIndependents = project.MemoryIndependents.ToList(),
            Sequences = [],
            Stack = project.Stack.Select(step => FileStep(step.Step, step.Cue.ToString())).ToList<ProjectFileStep?>(),
        };

        for (int index = 0; index < project.Looks.Length; index++)
        {
            if (project.Looks[index] is { } look)
                file.Memories.Add(new ProjectFileMemory { Number = index + 1, Levels = LevelsAboveZero(look) });
        }

        for (int index = 0; index < project.Sequences.Length; index++)
        {
            if (project.Sequences[index] is { } sequence)
            {
                file.Sequences.Add(new ProjectFileSequence
                {
                    Number = index + 1,
                    Loop = sequence.Loop,
                    Random = sequence.Random,
                    Speed = sequence.Speed,
                    Steps = sequence.Steps.Select(step => FileStep(step, cue: null)).ToList<ProjectFileStep?>(),
                });
            }
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
        int version = RequireFormat(file);

        byte[]?[] looks = LooksOf(file);
        return new ProjectContent(
            looks,
            NotesOf(file.ChannelNotes, Desk.ChannelCount, "channel"),
            NotesOf(file.MemoryNotes, Desk.MemoryCount, "memory"),
            IndependentsOf(file.Independents, version))
        {
            MemoryIndependents = MemoryIndependentsOf(file.MemoryIndependents),
            Sequences = SequencesOf(file.Sequences, looks),
            Stack = StackOf(file.Stack),
        };
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

    // The version of the format the file has, once it is clear that this program can read it.
    private static int RequireFormat(ProjectFile file)
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

        return version;
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

    // The channel of each channel key of the independents. A file may leave the list out: then
    // the keys are where they are on a new desk. A list that is there has to be whole: 10 keys,
    // or 16 in a file of version 1 or 2, which had that many; of those the first 10 are kept.
    private static int[] IndependentsOf(List<int>? listed, int version)
    {
        if (listed is null)
            return Desk.DefaultIndependents();

        int count = version < 3 ? OldIndependentCount : Desk.ChannelIndependentCount;
        if (listed.Count != count)
            throw Bad($"The file lists {listed.Count} independents; there must be {count}.");

        CheckKeys(listed, "Independent", ("channel", "channels"), Desk.ChannelCount);
        return listed.Take(Desk.ChannelIndependentCount).ToArray();
    }

    // The memory of each memory key of the independents, like the channels above. Files from
    // before version 3 have none: then the keys are on memories 1 to 10.
    private static int[] MemoryIndependentsOf(List<int>? listed)
    {
        if (listed is null)
            return Desk.DefaultMemoryIndependents();
        if (listed.Count != Desk.MemoryIndependentCount)
            throw Bad($"The file lists {listed.Count} memory independents; there must be {Desk.MemoryIndependentCount}.");

        CheckKeys(listed, "Memory independent", ("memory", "memories"), Desk.MemoryCount);
        return listed.ToArray();
    }

    // "key" names the keys and "fader" what they are bound to, one and many, for the message.
    private static void CheckKeys(List<int> listed, string key, (string One, string Many) fader, int highest)
    {
        for (int index = 0; index < listed.Count; index++)
        {
            if (listed[index] < 1 || listed[index] > highest)
            {
                throw Bad(
                    $"{key} {index + 1} in the file is on {fader.One} {listed[index]}; " +
                    $"{fader.Many} go from 1 to {highest}.");
            }
        }
    }

    // One entry per memory: its sequence, or null for a memory the file lists none for. A file
    // from before there were sequences has no list at all.
    private static Sequence?[] SequencesOf(List<ProjectFileSequence?>? listed, byte[]?[] looks)
    {
        var sequences = new Sequence?[Desk.MemoryCount];
        foreach (ProjectFileSequence? entry in listed ?? [])
        {
            if (entry?.Number is not int number || number < 1 || number > Desk.MemoryCount)
                throw Bad($"The file has a sequence without the number of its memory, from 1 to {Desk.MemoryCount}.");
            if (sequences[number - 1] is not null || looks[number - 1] is not null)
                throw Bad($"The file lists memory {number} twice.");

            List<ProjectFileStep?> steps = entry.Steps ?? throw Bad($"The sequence of memory {number} in the file has no steps list.");
            if (steps.Count > Sequence.MaxSteps)
                throw Bad($"The sequence of memory {number} in the file has {steps.Count} steps; there can be {Sequence.MaxSteps}.");

            string where = $"of the sequence of memory {number}";
            sequences[number - 1] = new Sequence(
                entry.Loop ?? Sequence.DefaultLoop,
                steps.Select((step, index) => StepOf(step, $"Step {index + 1} {where}")).ToArray())
            {
                Random = entry.Random ?? false,
                Speed = SpeedOf(entry.Speed, number),
            };
        }

        return sequences;
    }

    // The same rules as for a speed set on the UI. A file that does not say means 1.
    private static double SpeedOf(double? speed, int memory)
    {
        try
        {
            return Sequence.CheckedSpeed(speed ?? 1);
        }
        catch (DeskException e)
        {
            throw Bad($"The sequence of memory {memory} in the file cannot be used. {e.Message}");
        }
    }

    // The steps of the stack in the order of their numbers.
    private static ProjectStep[] StackOf(List<ProjectFileStep?>? listed)
    {
        var steps = new List<ProjectStep>();
        foreach (ProjectFileStep? entry in listed ?? [])
        {
            string where = $"Step {entry?.Cue ?? "without a number"} of the stack";
            CueNumber cue;
            try
            {
                cue = CueNumber.Parse(entry?.Cue);
            }
            catch (DeskException e)
            {
                throw Bad($"{where} in the file cannot be used. {e.Message}");
            }

            if (steps.Any(step => step.Cue == cue))
                throw Bad($"The file lists step {cue} of the stack twice.");

            steps.Add(new ProjectStep(cue, StepOf(entry, where)));
        }

        if (steps.Count > Desk.MaxStackSteps)
            throw Bad($"The stack in the file has {steps.Count} steps; there can be {Desk.MaxStackSteps}.");

        return steps.OrderBy(step => step.Cue).ToArray();
    }

    // The same rules as for a step made on the UI, so that a file cannot bring in a step that
    // the desk itself would refuse. "where" names the step for the message.
    private static Step StepOf(ProjectFileStep? entry, string where)
    {
        if (entry is null || entry.Target is null || entry.Number is null || entry.Value is null)
            throw Bad($"{where} in the file is not complete: it needs a target, a number and a value.");

        try
        {
            return new Step(entry.Target, entry.Number.Value, entry.Value.Value, entry.Up ?? 0, entry.Wait ?? 0, entry.Down ?? 0).Checked();
        }
        catch (DeskException e)
        {
            throw Bad($"{where} in the file cannot be used. {e.Message}");
        }
    }

    private static ProjectFileStep FileStep(Step step, string? cue)
    {
        return new ProjectFileStep
        {
            Cue = cue,
            Target = step.Target,
            Number = step.Number,
            Value = step.Value,
            Up = step.Up,
            Wait = step.Wait,
            Down = step.Down,
        };
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
