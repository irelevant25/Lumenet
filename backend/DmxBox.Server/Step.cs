namespace DmxBox.Server;

/// <summary>
/// One step of a sequence or of the stack: a channel, a memory, a fixture or a group of fixtures
/// of the stage plan, the value it is brought to, and three times in seconds. In the stack a step
/// can also release another step of the stack, see <see cref="Release"/>.
/// </summary>
/// <param name="Target">What the step stands for: "channel", "memory", "fixture", "group", or in the stack "release".</param>
/// <param name="Number">
/// The channel (1-512) or the memory (1-128); for a fixture or a group its number in the stage
/// plan (<see cref="StageLight.Id"/>, <see cref="LightGroup.Id"/>), 0 for one that is gone; for a
/// release the name of the step of the stack it takes down (<see cref="StackStep.Id"/>), 0 for none.
/// </param>
/// <param name="Value">The level the step brings it to, 0-255; for a fixture or a group the intensity of its lights.</param>
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

    /// <summary>The <see cref="Target"/> of a step that stands for a fixture: a light of the stage plan.</summary>
    public const string Fixture = "fixture";

    /// <summary>The <see cref="Target"/> of a step that stands for a group of fixtures of the stage plan.</summary>
    public const string Group = "group";

    /// <summary>
    /// The <see cref="Target"/> of a step of the stack that takes the light of another step of the
    /// stack down, in its own down time, and leaves everything else as it is. <see cref="Number"/>
    /// names that step; the value and the up time are not used.
    /// </summary>
    public const string Release = "release";

    /// <summary>The longest any of the three times can be.</summary>
    public const double MaxSeconds = 999.9;

    /// <summary>
    /// For a step of a fixture or a group: the color its lights shine in, like "#ff2000"; null
    /// for white. See <see cref="FixtureLooks"/>. A step of a channel or a memory has none, and
    /// the UIs get no such field for it.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Color { get; init; }

    /// <summary>
    /// Only in the stack: true for a step that comes up on top of what is on and takes nothing
    /// down. A step without it takes down every step that is on and that it does not use itself.
    /// False for a release and for every step of a sequence; the UIs get the field only when it is true.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Add { get; init; }

    /// <summary>True for a step that stands for a memory.</summary>
    internal bool IsMemory => Target == Memory;

    /// <summary>True for a step that stands for a fixture or a group of fixtures.</summary>
    internal bool IsLights => Target is Fixture or Group;

    /// <summary>True for a step of the stack that releases another one.</summary>
    internal bool IsRelease => Target == Release;

    /// <summary>
    /// Checks a step that came from a UI or a file, and rounds its times to tenths of a second.
    /// </summary>
    /// <returns>The step as the desk keeps it.</returns>
    /// <exception cref="DeskException">Something about the step is out of range. The message says what.</exception>
    internal Step Checked()
    {
        if (Target is not (Channel or Memory or Fixture or Group or Release))
            throw new DeskException($"A step stands for a channel, a memory, a fixture or a group, not for \"{Target}\".");

        if (IsLights)
        {
            if (Number < 0)
                throw new DeskException($"The {Target} of a step must be one of the stage plan, not {Number}.");
        }
        else if (IsRelease)
        {
            // which steps there are, the stack checks
            if (Number < 0)
                throw new DeskException($"A release names a step of the stack, not {Number}.");
        }
        else
        {
            // A channel against all there can be; the desk checks it against the project's universes.
            int highest = IsMemory ? Desk.MemoryCount : Desk.MaxChannels;
            if (Number < 1 || Number > highest)
                throw new DeskException($"The {Target} of a step must be from 1 to {highest}, not {Number}.");
        }

        if (Value < 0 || Value > Desk.MaxLevel)
            throw new DeskException($"The value of a step must be from 0 to {Desk.MaxLevel}, not {Value}.");

        string? color = IsLights && !string.IsNullOrEmpty(Color) ? Color.ToLowerInvariant() : null;
        if (color is not null && !PlanText.IsColor(color))
            throw new DeskException($"\"{Color}\" is no color like #ff2000.");

        // A release takes down and adds nothing: it never counts as a step that adds.
        return this with
        {
            Up = Seconds(Up, "up"), Wait = Seconds(Wait, "wait"), Down = Seconds(Down, "down"), Color = color, Add = Add && !IsRelease,
        };
    }

    /// <summary>
    /// Checks a step of a sequence: as <see cref="Checked"/>, but a release belongs to the stack
    /// only, and a step of a sequence never adds (the steps of a sequence follow each other).
    /// </summary>
    /// <exception cref="DeskException">Something about the step is out of range, or it is a release.</exception>
    internal Step CheckedForSequence()
    {
        if (IsRelease)
            throw new DeskException("A step of a sequence cannot release anything; only a step of the stack can.");

        return Checked() with { Add = false };
    }

    private static double Seconds(double time, string which)
    {
        // "Not a number" fails both comparisons, so it is refused as well.
        if (!(time >= 0 && time <= MaxSeconds))
            throw new DeskException(FormattableString.Invariant($"The {which} time of a step must be from 0 to {MaxSeconds} seconds."));

        return Math.Round(time, 1);
    }
}
