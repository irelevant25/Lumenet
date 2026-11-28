namespace DmxBox;

/// <summary>What came in at a box's DMX input (<see cref="DmxBoxClient.GetInput"/>): a console's levels.</summary>
/// <param name="Live">True while a console sends: the last frame came less than a second ago.</param>
/// <param name="Frame">The number of the last complete frame, counted from 1; 0 before the first.</param>
/// <param name="Levels">
/// Its levels, channel 1 at index 0: up to 512, as many as the console sends. Null when the caller
/// has this frame already, or no frame came yet.
/// </param>
public sealed record DmxInput(bool Live, uint Frame, byte[]? Levels);
