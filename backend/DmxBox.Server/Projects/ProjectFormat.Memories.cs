using System.Text.Encodings.Web;
using System.Text.Json;

namespace DmxBox.Server.Projects;

// The looks of the memories in a project file, the notes under the faders and the keys of the independents.
internal static partial class ProjectFormat
{
    // One entry per memory: its look, or null for a memory the file does not list.
    private static byte[]?[] LooksOf(ProjectFile file, int channels)
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

            looks[number - 1] = LookOf(memory, number, channels);
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

    private static byte[] LookOf(ProjectFileMemory memory, int number, int channels)
    {
        SortedDictionary<int, int> levels = memory.Levels ?? throw Bad($"Memory {number} in the file has no levels.");

        byte[] look = new byte[channels];
        foreach ((int channel, int level) in levels)
        {
            if (channel < 1 || channel > channels)
            {
                throw Bad(
                    $"Memory {number} in the file has a channel {channel}; " +
                    $"the project's channels go from 1 to {channels}.");
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
    // the keys are where they are on a new desk. A list that is there has to be whole for its
    // version: 32 keys, 10 in a file of version 3, 16 in one of version 1 or 2. The keys an older
    // file does not have are where they are on a new desk.
    private static int[] IndependentsOf(List<int>? listed, int version, int channels)
    {
        if (listed is null)
            return Desk.DefaultIndependents();

        int count = version switch
        {
            < 3 => FirstIndependentCount,
            3 => ThirdIndependentCount,
            _ => Desk.ChannelIndependentCount,
        };
        if (listed.Count != count)
            throw Bad($"The file lists {listed.Count} independents; there must be {count}.");

        CheckKeys(listed, "Independent", ("channel", "channels"), channels);
        return [.. listed, .. Desk.DefaultIndependents().Skip(count)];
    }

    // The memory of each memory key of the independents, like the channels above. Files from
    // before version 3 have none: then the keys are where they are on a new desk.
    private static int[] MemoryIndependentsOf(List<int>? listed, int version)
    {
        if (listed is null)
            return Desk.DefaultMemoryIndependents();

        int count = version == 3 ? ThirdIndependentCount : Desk.MemoryIndependentCount;
        if (listed.Count != count)
            throw Bad($"The file lists {listed.Count} memory independents; there must be {count}.");

        CheckKeys(listed, "Memory independent", ("memory", "memories"), Desk.MemoryCount);
        return [.. listed, .. Desk.DefaultMemoryIndependents().Skip(count)];
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
}
