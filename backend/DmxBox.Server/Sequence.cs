namespace DmxBox.Server;

/// <summary>
/// A sequence, stored in a memory in place of a look: steps that run one after the other
/// while the memory's fader is up. <see cref="SequenceTimeline"/> says what it gives when.
/// </summary>
/// <param name="Loop">
/// True: after the last step the first one starts again. False: it runs once, and the last
/// step stays on for as long as the fader is up.
/// </param>
/// <param name="Steps">The steps, the first one first.</param>
/// <remarks>Public only because <see cref="Step"/> is.</remarks>
public sealed record Sequence(bool Loop, Step[] Steps)
{
    /// <summary>The most steps a sequence can have.</summary>
    public const int MaxSteps = 99;
}
