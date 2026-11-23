namespace DmxBox.Server;

/// <summary>
/// The whole desk at one moment, in the form the UI receives it. The levels are numbers and
/// not bytes: a byte array would arrive in the browser as one base64 text.
/// </summary>
/// <param name="Channels">The level of every channel of the project's universes (512 each), 0-255; channel 1 is at index 0.</param>
/// <param name="MemoryLevels">The 128 memory levels, 0-255; memory 1 is at index 0.</param>
/// <param name="MemoryStored">For each of the 128 memories, whether it holds something: a look or a sequence.</param>
/// <param name="Master">Master level, 0-255.</param>
/// <param name="Blackout">True while blackout is on.</param>
/// <param name="ChannelNotes">The notes under the channel faders, as many as there are channels; an empty text where there is none.</param>
/// <param name="MemoryNotes">The 128 notes under the memory faders; an empty text where there is none.</param>
/// <param name="Independents">The channel number each channel key of the independents is bound to; key 1 is at index 0.</param>
/// <param name="MemorySequence">For each of the 128 memories, whether what it holds is a sequence.</param>
/// <param name="MemoryIndependents">The memory number, 1-128, each of the 10 memory keys of the independents is bound to; key 1 is at index 0.</param>
/// <param name="Rate">The rate, a speed master for all sequences: 0.1 to 10.</param>
/// <param name="RateOn">True while the rate counts.</param>
/// <param name="Solo">The keys held for a solo now, see <see cref="SoloState"/>.</param>
/// <param name="ChannelClaims">
/// For each channel fader, when it last took its channel (a number that grows with
/// every such move; the higher, the later; 0 for never): an attribute of a light, such as a pan,
/// comes from the fader that took it last. See Desk.Attributes.cs.
/// </param>
/// <param name="MemoryClaims">For each of the 128 memory faders, when it last left zero and took the lights its memory holds, on the same count.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record DeskState(
    int[] Channels,
    int[] MemoryLevels,
    bool[] MemoryStored,
    int Master,
    bool Blackout,
    string[] ChannelNotes,
    string[] MemoryNotes,
    int[] Independents,
    bool[] MemorySequence,
    int[] MemoryIndependents,
    double Rate,
    bool RateOn,
    SoloState Solo,
    long[] ChannelClaims,
    long[] MemoryClaims)
{
    /// <summary>How many universes the project has, 1 to 8: the channels are 512 for each.</summary>
    public int Universes { get; init; } = 1;
}

/// <summary>
/// The keys of channels and memories held for a solo now, on any UI: with SOLO UP lit only their
/// faders shine, at full, and every other light is dark; with SOLO DOWN lit their faders give
/// nothing and the rest stays.
/// </summary>
/// <param name="Up">The keys held with SOLO UP.</param>
/// <param name="Down">The keys held with SOLO DOWN.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record SoloState(SoloKey[] Up, SoloKey[] Down);

/// <summary>A key held for a solo: of a channel or of a memory.</summary>
/// <param name="Target">"channel" or "memory".</param>
/// <param name="Number">The channel (1-512) or the memory (1-128).</param>
public sealed record SoloKey(string Target, int Number);
