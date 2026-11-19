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
    /// The channel number (1-512) of each of the 16 independent keys, key 1 first. Files from
    /// before there were independent keys do not have the property.
    /// </summary>
    public List<int>? Independents { get; set; }
}
