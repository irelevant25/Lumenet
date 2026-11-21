using System.Text.Json;
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

    /// <summary>
    /// The settings of the UI on the PC that belong to the project, see
    /// <see cref="ProjectSettings"/>. Written only for a project that has some; files from before
    /// there were such settings do not have the property, nor do imported shows.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Settings { get; set; }

    /// <summary>
    /// The stage plan: the size of the stage and the lights of the rig on it. Files from before
    /// there were plans do not have the property, nor do imported shows.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectFileStage? Stage { get; set; }
}

/// <summary>The stage plan, as it stands in a project file.</summary>
internal sealed class ProjectFileStage
{
    /// <summary>How wide the stage is, in squares. A file that does not say means 16.</summary>
    public int? Width { get; set; }

    /// <summary>How deep the stage is, in squares. A file that does not say means 10.</summary>
    public int? Depth { get; set; }

    /// <summary>The lights on the plan.</summary>
    public List<ProjectFileLight?>? Lights { get; set; }

    /// <summary>The picture under the plan, as a data URL of an image; none without one.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Picture { get; set; }

    /// <summary>How brightly the picture shows, 0.1 to 1. A file that does not say means 0.6.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? PictureOpacity { get; set; }

    /// <summary>"stage" or "plan": what the picture lies under. A file that does not say means the stage.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PictureOver { get; set; }

    /// <summary>The kinds of lights from the fixture library, numbered from 1 in this order. Written only when there are some.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ProjectFileType?>? Types { get; set; }

    /// <summary>The groups of lights. Written only when there are some.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ProjectFileGroup?>? Groups { get; set; }
}

/// <summary>A kind of light from the fixture library, as it stands in a project file, see <see cref="FixtureType"/>.</summary>
internal sealed class ProjectFileType
{
    public string? Maker { get; set; }

    public string? Model { get; set; }

    public string? Mode { get; set; }

    /// <summary>Where it is in the library, like "american-dj/inno-pocket-spot".</summary>
    public string? Source { get; set; }

    public List<ProjectFileTypeChannel?>? Channels { get; set; }
}

/// <summary>A channel of a kind of light, as it stands in a project file, see <see cref="FixtureChannel"/>.</summary>
internal sealed class ProjectFileTypeChannel
{
    public string? Name { get; set; }

    /// <summary>Its letter, like "P".</summary>
    public string? Code { get; set; }

    /// <summary>For the fine part of another channel: the index of that one, 0 for the first.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Fine { get; set; }

    /// <summary>What it does at its values. Written only for a channel that has ranges.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ProjectFileRange?>? Ranges { get; set; }
}

/// <summary>A range of a channel of a kind of light, as it stands in a project file.</summary>
internal sealed class ProjectFileRange
{
    public int? From { get; set; }

    public int? To { get; set; }

    public string? Label { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Color { get; set; }
}

/// <summary>A group of lights, as it stands in a project file.</summary>
internal sealed class ProjectFileGroup
{
    public string? Name { get; set; }

    /// <summary>Its lights, by their place in the file's list of lights, counted from 1.</summary>
    public List<int>? Lights { get; set; }
}

/// <summary>A light of the stage plan, as it stands in a project file.</summary>
internal sealed class ProjectFileLight
{
    /// <summary>What the owner calls it. A file that does not say means no name.</summary>
    public string? Name { get; set; }

    /// <summary>Its first channel, 1-512.</summary>
    public int? Address { get; set; }

    /// <summary>One letter per channel, like "DRGB", see <see cref="StageLight.Channels"/>.</summary>
    public string? Channels { get; set; }

    /// <summary>The color of a light without color channels, like "#ff2000"; none for warm white.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Color { get; set; }

    /// <summary>
    /// The color as files written before 2026-11-21 name it, in the British spelling. It is read,
    /// so that those files keep their colors, and never written.
    /// </summary>
    [JsonPropertyName("colour")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EarlierColor { get; set; }

    /// <summary>Where it hangs: squares from the left edge of the stage.</summary>
    public double? X { get; set; }

    /// <summary>Where it hangs: squares from the back edge of the stage.</summary>
    public double? Y { get; set; }

    /// <summary>How it is turned, in degrees clockwise. A file that does not say means 0.</summary>
    public int? Angle { get; set; }

    /// <summary>"round" or "square". A file that does not say means round.</summary>
    public string? Shape { get; set; }

    /// <summary>How wide it is, in squares. A file that does not say means 1.</summary>
    public int? Width { get; set; }

    /// <summary>How deep it is, in squares. A file that does not say means 1.</summary>
    public int? Depth { get; set; }

    /// <summary>The kind of light from the fixture library it is, by its place in the file's list of kinds, counted from 1; none for a light of letters.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Type { get; set; }

    /// <summary>The most it gives, in per cent. Written only for a light with a limit below 100.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Limit { get; set; }
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

    /// <summary>"channel", "memory", "fixture" or "group".</summary>
    public string? Target { get; set; }

    /// <summary>
    /// The channel (1-512) or the memory (1-128); a fixture or a group by its place in the file's
    /// list of lights or groups, counted from 1 (0 for one that is gone).
    /// </summary>
    public int? Number { get; set; }

    /// <summary>For a step of a fixture or a group: the color of its lights, like "#ff2000". Written only when it has one.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Color { get; set; }

    /// <summary>The level the step brings it to, 0-255.</summary>
    public int? Value { get; set; }

    /// <summary>Seconds for the way up.</summary>
    public double? Up { get; set; }

    /// <summary>Seconds the step stays.</summary>
    public double? Wait { get; set; }

    /// <summary>Seconds for the way down.</summary>
    public double? Down { get; set; }
}
