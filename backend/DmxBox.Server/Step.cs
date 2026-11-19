namespace DmxBox.Server;

/// <summary>
/// One step of a sequence or of the stack: a channel or a memory, the value it is brought to,
/// and three times in seconds.
/// </summary>
/// <param name="Target">What the step stands for: "channel" or "memory".</param>
/// <param name="Number">The channel (1-512) or the memory (1-128).</param>
/// <param name="Value">The level the step brings it to, 0-255.</param>
/// <param name="Up">Seconds for the way from zero to the value.</param>
/// <param name="Wait">
/// Seconds the step stays once it is up. In a sequence 0 means "go on at once"; in the stack
/// 0 means "stay until Play".
/// </param>
/// <param name="Down">Seconds for the way back to zero, when the next step takes over.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record Step(string Target, int Number, int Value, double Up, double Wait, double Down)
{
    /// <summary>The <see cref="Target"/> of a step that stands for a channel.</summary>
    public const string Channel = "channel";

    /// <summary>The <see cref="Target"/> of a step that stands for a memory.</summary>
    public const string Memory = "memory";

    /// <summary>The longest any of the three times can be.</summary>
    public const double MaxSeconds = 999.9;

    /// <summary>True for a step that stands for a memory, false for one that stands for a channel.</summary>
    internal bool IsMemory => Target == Memory;

    /// <summary>
    /// Checks a step that came from a UI or a file, and rounds its times to tenths of a second.
    /// </summary>
    /// <returns>The step as the desk keeps it.</returns>
    /// <exception cref="DeskException">Something about the step is out of range. The message says what.</exception>
    internal Step Checked()
    {
        if (Target is not (Channel or Memory))
            throw new DeskException($"A step stands for a {Channel} or a {Memory}, not for \"{Target}\".");

        int highest = IsMemory ? Desk.MemoryCount : Desk.ChannelCount;
        if (Number < 1 || Number > highest)
            throw new DeskException($"The {Target} of a step must be from 1 to {highest}, not {Number}.");
        if (Value < 0 || Value > Desk.MaxLevel)
            throw new DeskException($"The value of a step must be from 0 to {Desk.MaxLevel}, not {Value}.");

        return this with { Up = Seconds(Up, "up"), Wait = Seconds(Wait, "wait"), Down = Seconds(Down, "down") };
    }

    private static double Seconds(double time, string which)
    {
        // "Not a number" fails both comparisons, so it is refused as well.
        if (!(time >= 0 && time <= MaxSeconds))
            throw new DeskException($"The {which} time of a step must be from 0 to {MaxSeconds} seconds.");

        return Math.Round(time, 1);
    }
}
