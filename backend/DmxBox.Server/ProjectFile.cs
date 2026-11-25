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

    /// <summary>
    /// How many universes the project has, 1 to 8: its channels are 512 for each. Files before
    /// version 5 do not have the property: they have one universe.
    /// </summary>
    public int? Universes { get; set; }

    /// <summary>
    /// The boxes the project wants and the universe each of their outputs plays. Written only for
    /// a project that lists boxes; without them every box that answers plays universes by itself.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ProjectFileBox?>? Boxes { get; set; }

    /// <summary>
    /// The outputs besides the boxes: Art-Net nodes, sACN and DMX USB interfaces, see
    /// <see cref="OutputPlan"/>. Written only for a project that has some; older files do not
    /// have the property, and an older program passes over it.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ProjectFileOutput?>? Outputs { get; set; }

    /// <summary>
    /// The notes of the project, instructions for the show, see <see cref="ProjectNotes"/>.
    /// Written only for a project that has some; files from before there were such notes do not
    /// have the property, nor do imported shows.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notes { get; set; }

    /// <summary>The memories that hold a look. Empty memories are not listed.</summary>
    public List<ProjectFileMemory?>? Memories { get; set; }

    /// <summary>
    /// The notes under the channel faders: channel number (counted on through the universes:
    /// universe 2 starts at 513) and text. Only faders that have a note are listed. Files from
    /// before there were notes do not have the property.
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

    /// <summary>The effects, see <see cref="Effect"/>. Written only for a project that has some (version 7).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ProjectFileEffect?>? Effects { get; set; }

    /// <summary>The effect number (1-64) of each of the 32 effect keys of the independents, key 1 first. Written with the effects.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? EffectIndependents { get; set; }

    /// <summary>
    /// The stage plan: the size of the stage and the lights of the rig on it. Files from before
    /// there were plans do not have the property, nor do imported shows.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectFileStage? Stage { get; set; }
}

/// <summary>A box the project wants, as it stands in a project file, see <see cref="BoxPlan"/>.</summary>
internal sealed class ProjectFileBox
{
    /// <summary>Where the box answers: "10.1.3.1", with ":port" when the port is not 7000.</summary>
    public string? Address { get; set; }

    /// <summary>For each output, output 1 first: the universe it plays, 1 to 8, or 0 for none.</summary>
    public List<int>? Universes { get; set; }
}

/// <summary>An output besides the boxes, as it stands in a project file, see <see cref="OutputPlan"/>.</summary>
internal sealed class ProjectFileOutput
{
    /// <summary>"artnet", "sacn" or "usb".</summary>
    public string? Kind { get; set; }

    /// <summary>The node's or receiver's address, "broadcast", "multicast", or a serial port like "COM3".</summary>
    public string? Target { get; set; }

    /// <summary>For a broadcast or a multicast, the network it goes out on, "192.168.1.0/24"; none for automatic.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Network { get; set; }

    /// <summary>For sACN, the priority 0 to 200; none for 100.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Priority { get; set; }

    /// <summary>What each port plays, and where it goes.</summary>
    public List<ProjectFilePort?>? Ports { get; set; }
}

/// <summary>A port of an output, as it stands in a project file, see <see cref="OutputPort"/>.</summary>
internal sealed class ProjectFilePort
{
    /// <summary>The universe of the project it plays, 1 to 8, or 0 for none.</summary>
    public int? Universe { get; set; }

    /// <summary>The Port-Address of Art-Net, the universe of sACN; none for USB.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Address { get; set; }
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

    /// <summary>The note under its key on the Fixtures page; a file without one means none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; set; }

    /// <summary>Its lights, by their place in the file's list of lights, counted from 1.</summary>
    public List<int>? Lights { get; set; }
}

/// <summary>A light of the stage plan, as it stands in a project file.</summary>
internal sealed class ProjectFileLight
{
    /// <summary>What the owner calls it. A file that does not say means no name.</summary>
    public string? Name { get; set; }

    /// <summary>The note under its key on the Fixtures page; a file without one means none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; set; }

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

/// <summary>
/// An effect, as it stands in a project file, see <see cref="Effect"/>. What is as an effect has
/// it unless it says otherwise is left out, and a file that leaves it out means that.
/// </summary>
internal sealed class ProjectFileEffect
{
    /// <summary>Its number, 1-64.</summary>
    public int? Number { get; set; }

    public string? Name { get; set; }

    /// <summary>"movement", "intensity", "color" or "pixel".</summary>
    public string? Kind { get; set; }

    /// <summary>Its shape, like "circle".</summary>
    public string? Shape { get; set; }

    /// <summary>The group it works on, by its place in the file's list of groups, counted from 1; none when it names lights.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Group { get; set; }

    /// <summary>The lights it works on, by their place in the file's list of lights, counted from 1.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? Lights { get; set; }

    /// <summary>Cycles a minute. A file that does not say means 30.</summary>
    public double? Speed { get; set; }

    /// <summary>Per cent. A file that does not say means 100.</summary>
    public int? Size { get; set; }

    /// <summary>Per cent. A file that does not say means 100.</summary>
    public int? Spread { get; set; }

    /// <summary>"forward" or "back"; none for forward.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Direction { get; set; }

    /// <summary>"address", "left", "center" or "random"; none for address.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Order { get; set; }

    /// <summary>Lights in a block; none for 1.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Blocks { get; set; }

    /// <summary>Wings; none for 1.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Wings { get; set; }

    /// <summary>Seconds; none for 0.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? FadeIn { get; set; }

    /// <summary>Seconds; none for 0.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? FadeOut { get; set; }

    /// <summary>Per cent of a cycle; none for 50.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Duty { get; set; }

    /// <summary>Colors like "#ff2000"; none for the usual ones.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string?>? Colors { get; set; }

    /// <summary>A movement around a center of its own: pan and tilt in per cent; none to move around where the head points.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<double>? Center { get; set; }

    /// <summary>Degrees; none for 0.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Angle { get; set; }

    /// <summary>Where a ripple starts, squares across and from the back; none for the middle of its lights.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<double>? Point { get; set; }

    /// <summary>Chooses the random order and shapes; none for 0.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Seed { get; set; }
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

    /// <summary>"channel", "memory", "fixture" or "group"; in the stack also "release" and "effect".</summary>
    public string? Target { get; set; }

    /// <summary>
    /// The channel (1-512) or the memory (1-128); a fixture or a group by its place in the file's
    /// list of lights or groups, counted from 1 (0 for one that is gone). 0 for a release of the
    /// stack, which names its step with <see cref="Releases"/>; for a release of a sequence the
    /// number of the step it takes down, counted from 1 (0 for none). The effect (1-64) of a step of an effect.
    /// </summary>
    public int? Number { get; set; }

    /// <summary>Only for a step that comes up on top of what is on: true. Written only then.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Add { get; set; }

    /// <summary>
    /// Only for a release: the number of the step of the stack it takes down, like "2.1". Written
    /// only for a release of a step; a release whose step was taken out has none.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Releases { get; set; }

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
