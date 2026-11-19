using System.Text.Json.Serialization;

namespace DmxBox.Server;

/// <summary>
/// A project file as it stands on the disk; <see cref="ProjectFormat"/> reads and writes it.
/// Every property can be missing, because a file may have been edited by hand, or may not be
/// a project file at all.
/// </summary>
internal sealed class ProjectFile
{
    /// <summary>Says what kind of file this is: always "lumenet-project".</summary>
    public string? Format { get; set; }

    /// <summary>The version of the file format, so that a later format can be told apart.</summary>
    public int? Version { get; set; }

    /// <summary>The memories that hold a look. Empty memories are not listed.</summary>
    public List<ProjectFileMemory?>? Memories { get; set; }

    /// <summary>
    /// The notes under the channel faders: channel number (1-512) and text. Only faders that
    /// have a note are listed. Files from before there were notes do not have the property.
    /// </summary>
    public SortedDictionary<int, string?>? ChannelNotes { get; set; }

    /// <summary>The notes under the memory faders: memory number (1-128) and text.</summary>
    public SortedDictionary<int, string?>? MemoryNotes { get; set; }

    /// <summary>
    /// The channel number (1-512) of each channel key of the independents, key 1 first: 10 of
    /// them from version 3 on, 16 in older files (of those, the first 10 are used). Files from
    /// before there were independent keys do not have the property.
    /// </summary>
    public List<int>? Independents { get; set; }

    /// <summary>
    /// The memory number (1-128) of each of the 10 memory keys of the independents, key 1 first.
    /// Files from before version 3 do not have the property.
    /// </summary>
    public List<int>? MemoryIndependents { get; set; }

    /// <summary>
    /// The memories that hold a sequence. Files of version 1, from before there were
    /// sequences, do not have the property.
    /// </summary>
    public List<ProjectFileSequence?>? Sequences { get; set; }

    /// <summary>The steps of the stack. Files of version 1 do not have the property.</summary>
    public List<ProjectFileStep?>? Stack { get; set; }
}

/// <summary>A memory that holds a sequence, as it stands in a project file.</summary>
internal sealed class ProjectFileSequence
{
    /// <summary>The number of the memory, 1-128.</summary>
    public int? Number { get; set; }

    /// <summary>True to start again after the last step, false to play once. A file that does not say means a loop.</summary>
    public bool? Loop { get; set; }

    /// <summary>True for a new random order on every round. A file that does not say means in turn.</summary>
    public bool? Random { get; set; }

    /// <summary>How fast it is played, 0.1 to 10. A file that does not say means 1.</summary>
    public double? Speed { get; set; }

    /// <summary>The steps, the first one first. They have no <see cref="ProjectFileStep.Cue"/>.</summary>
    public List<ProjectFileStep?>? Steps { get; set; }
}

/// <summary>A step of a sequence or of the stack, as it stands in a project file.</summary>
internal sealed class ProjectFileStep
{
    /// <summary>Only for a step of the stack: its number, written like "2.1". A step of a sequence has none, and none is written.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cue { get; set; }

    /// <summary>"channel" or "memory".</summary>
    public string? Target { get; set; }

    /// <summary>The channel (1-512) or the memory (1-128).</summary>
    public int? Number { get; set; }

    /// <summary>The level the step brings it to, 0-255.</summary>
    public int? Value { get; set; }

    /// <summary>Seconds for the way up.</summary>
    public double? Up { get; set; }

    /// <summary>Seconds the step stays.</summary>
    public double? Wait { get; set; }

    /// <summary>Seconds for the way down.</summary>
    public double? Down { get; set; }
}
