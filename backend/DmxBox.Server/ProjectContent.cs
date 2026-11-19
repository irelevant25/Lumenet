namespace DmxBox.Server;

/// <summary>
/// What a project file holds: the looks of the memories, the notes under the faders, and the
/// channels the independent keys are bound to.
/// </summary>
/// <param name="Looks">
/// One entry per memory, memory 1 first: the memory's 512 levels, or null while it is empty.
/// </param>
/// <param name="ChannelNotes">The 512 notes under the channel faders; an empty text where there is none.</param>
/// <param name="MemoryNotes">The 128 notes under the memory faders; an empty text where there is none.</param>
/// <param name="Independents">The channel number, 1-512, of each of the 16 independent keys; key 1 first.</param>
internal sealed record ProjectContent(byte[]?[] Looks, string[] ChannelNotes, string[] MemoryNotes, int[] Independents);
