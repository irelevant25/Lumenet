namespace DmxBox.Server;

/// <summary>
/// One step of a sequence or of the stack: a channel, a memory, a fixture or a group of fixtures
/// of the stage plan, or an effect, the value it is brought to, and three times in seconds. A step
/// can also release another step of the same stack or sequence, see <see cref="Release"/>.
/// </summary>
/// <param name="Target">What the step stands for: "channel", "memory", "fixture", "group", "effect" or "release".</param>
/// <param name="Number">
/// The channel (1-512) or the memory (1-128); for a fixture or a group its number in the stage
/// plan (<see cref="StageLight.Id"/>, <see cref="LightGroup.Id"/>), 0 for one that is gone; for an
/// effect its number (1-64); for a release the step it takes down, 0 for none: in the stack that
/// step's name (<see cref="StackStep.Id"/>), in a sequence its number in the list, from 1.
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
    /// The <see cref="Target"/> of a step that takes the light of another step of the same stack or
    /// sequence down, in its own down time, and leaves everything else as it is. <see cref="Number"/>
    /// names that step; the value and the up time are not used.
    /// </summary>
    public const string Release = "release";

    /// <summary>
    /// The <see cref="Target"/> of a step that plays an effect. In the stack it brings the effect's
    /// fader to its value in its up time, which starts the effect, and back to zero in its down time
    /// when the stack leaves it, which stops it. In a sequence it plays the effect as strongly as the
    /// step is up, and leaves the fader alone (Desk.Effects.cs). <see cref="Number"/> names the effect.
    /// </summary>
    public const string EffectTarget = "effect";

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
    /// True for a step that comes up on top of what is on and takes nothing down. A step without
    /// it takes down every step that is on (in the stack: unless it uses the same fader itself).
    /// False for a release; the UIs get the field only when it is true.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Add { get; init; }

    /// <summary>True for a step that stands for a memory.</summary>
    internal bool IsMemory => Target == Memory;

    /// <summary>True for a step that stands for a fixture or a group of fixtures.</summary>
    internal bool IsLights => Target is Fixture or Group;

    /// <summary>True for a step that releases another one.</summary>
    internal bool IsRelease => Target == Release;

    /// <summary>True for a step that plays an effect.</summary>
    internal bool IsEffect => Target == EffectTarget;

    /// <summary>True for a step that gives light: a channel, a memory, a fixture or a group.</summary>
    internal bool GivesLight => !IsRelease && !IsEffect;

    /// <summary>
    /// Checks a step that came from a UI or a file, and rounds its times to tenths of a second.
    /// </summary>
    /// <returns>The step as the desk keeps it.</returns>
    /// <exception cref="DeskException">Something about the step is out of range. The message says what.</exception>
    internal Step Checked()
    {
        if (Target is not (Channel or Memory or Fixture or Group or Release or EffectTarget))
            throw new DeskException($"A step stands for a channel, a memory, a fixture, a group or an effect, not for \"{Target}\".");

        if (IsEffect)
        {
            if (Number < 1 || Number > Effect.MaxEffects)
                throw new DeskException($"The effect of a step must be from 1 to {Effect.MaxEffects}, not {Number}.");
        }
        else if (IsLights)
        {
            if (Number < 0)
                throw new DeskException($"The {Target} of a step must be one of the stage plan, not {Number}.");
        }
        else if (IsRelease)
        {
            // which steps there are, the stack or the sequence checks
            if (Number < 0)
                throw new DeskException($"A release names another step, not {Number}.");
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

    private static double Seconds(double time, string which)
    {
        // "Not a number" fails both comparisons, so it is refused as well.
        if (!(time >= 0 && time <= MaxSeconds))
            throw new DeskException(FormattableString.Invariant($"The {which} time of a step must be from 0 to {MaxSeconds} seconds."));

        return Math.Round(time, 1);
    }
}
