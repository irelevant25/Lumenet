namespace DmxBox.Server;

/// <summary>
/// What a project file holds: the looks and the sequences of the memories, the stack, the
/// notes under the faders, and the channels and memories the independent keys are bound to.
/// </summary>
/// <param name="Looks">
/// One entry per memory, memory 1 first: the memory's 512 levels, or null while it holds no look.
/// </param>
/// <param name="ChannelNotes">The 512 notes under the channel faders; an empty text where there is none.</param>
/// <param name="MemoryNotes">The 128 notes under the memory faders; an empty text where there is none.</param>
/// <param name="Independents">The channel number, 1-512, of each of the 10 channel keys of the independents; key 1 first.</param>
internal sealed record ProjectContent(byte[]?[] Looks, string[] ChannelNotes, string[] MemoryNotes, int[] Independents)
{
    /// <summary>The memory number, 1-128, of each of the 10 memory keys of the independents; key 1 first.</summary>
    public int[] MemoryIndependents { get; init; } = Desk.DefaultMemoryIndependents();

    /// <summary>
    /// One entry per memory, memory 1 first: the sequence the memory holds, or null. A memory
    /// holds a look or a sequence, never both.
    /// </summary>
    public Sequence?[] Sequences { get; init; } = new Sequence?[Desk.MemoryCount];

    /// <summary>The steps of the stack, in the order of their numbers.</summary>
    public ProjectStep[] Stack { get; init; } = [];
}
