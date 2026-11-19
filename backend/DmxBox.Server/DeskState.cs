namespace DmxBox.Server;

/// <summary>
/// The whole desk at one moment, in the form the UI receives it. The levels are numbers and
/// not bytes: a byte array would arrive in the browser as one base64 text.
/// </summary>
/// <param name="Channels">The 512 channel levels, 0-255; channel 1 is at index 0.</param>
/// <param name="MemoryLevels">The 128 memory levels, 0-255; memory 1 is at index 0.</param>
/// <param name="MemoryStored">For each of the 128 memories, whether it holds a look.</param>
/// <param name="Master">Master level, 0-255.</param>
/// <param name="Blackout">True while blackout is on.</param>
/// <param name="ChannelNotes">The 512 notes under the channel faders; an empty text where there is none.</param>
/// <param name="MemoryNotes">The 128 notes under the memory faders; an empty text where there is none.</param>
/// <param name="Independents">The channel number, 1-512, each of the 16 independent keys is bound to; key 1 is at index 0.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record DeskState(
    int[] Channels,
    int[] MemoryLevels,
    bool[] MemoryStored,
    int Master,
    bool Blackout,
    string[] ChannelNotes,
    string[] MemoryNotes,
    int[] Independents);
