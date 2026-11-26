namespace DmxBox.Server.Projects;

/// <summary>One stored memory in a <see cref="ProjectFile"/>.</summary>
internal sealed class ProjectFileMemory
{
    /// <summary>Memory number, 1-128.</summary>
    public int? Number { get; set; }

    /// <summary>
    /// The memory's look: channel number (1-512) and its level (1-255). Channels at 0 are
    /// left out, so a dark look has no entries at all. Sorted, so the file lists the
    /// channels in their order.
    /// </summary>
    public SortedDictionary<int, int>? Levels { get; set; }
}
