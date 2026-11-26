namespace DmxBox.Server.Sequences;

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

    /// <summary>
    /// How a sequence is played unless its owner says otherwise: in a loop. It is what a new
    /// sequence gets, and what a project file means that does not say.
    /// </summary>
    public const bool DefaultLoop = true;

    /// <summary>The slowest a sequence can be played: a tenth of its speed, every time ten times as long.</summary>
    public const double MinSpeed = 0.1;

    /// <summary>The fastest: ten times its speed.</summary>
    public const double MaxSpeed = 10;

    /// <summary>The speed goes in steps of this size: 0.95, 1.00, 1.05 ...</summary>
    public const double SpeedStep = 0.05;

    /// <summary>
    /// True: played in a loop, but on every round through the steps in another random order,
    /// so that a few steps never look like a loop. Such a sequence loops whatever
    /// <see cref="Loop"/> says.
    /// </summary>
    public bool Random { get; init; }

    /// <summary>
    /// How fast it is played: 1 as its times say, 0.5 half as fast (every time twice as long),
    /// 2 twice as fast. From <see cref="MinSpeed"/> to <see cref="MaxSpeed"/>.
    /// </summary>
    public double Speed { get; init; } = 1;

    /// <summary>True when the first step follows the last: played in a loop, in turn or at random.</summary>
    internal bool Loops => Loop || Random;

    /// <summary>A speed that came from a UI or a file, checked and put on a step of <see cref="SpeedStep"/>.</summary>
    /// <param name="speed">The speed.</param>
    /// <returns>The speed as the desk keeps it.</returns>
    /// <exception cref="DeskException">It is out of range. The message says so.</exception>
    internal static double CheckedSpeed(double speed)
    {
        // "Not a number" fails both comparisons, so it is refused as well. The numbers are written
        // with a point, as on the UI, whatever the language of the PC.
        if (!(speed >= MinSpeed && speed <= MaxSpeed))
            throw new DeskException(FormattableString.Invariant($"The speed of a sequence must be from {MinSpeed} to {MaxSpeed}, not {speed}."));

        // The second rounding takes off what binary fractions leave behind: 0.35, not 0.35000000000000003.
        return Math.Round(Math.Round(speed / SpeedStep) * SpeedStep, 2);
    }
}
