using System.Globalization;

namespace DmxBox.Server.Effects;

/// <summary>
/// An effect: a shape the desk works out anew for every frame, on lights of the stage plan,
/// instead of steps that are stored. It moves heads (a circle, a figure 8), lets lights rise and
/// fall (a sine, a pulse), changes their color (a rainbow, a chase) or paints a pattern over the
/// cells of lights by where they are on the plan (bars, a ripple). How it mixes with the rest of
/// the desk is in Desk.Effects.cs; what each shape does is in <see cref="EffectEngine"/>.
/// </summary>
/// <remarks>
/// An effect belongs to the project and is stored there; what it does while it plays (whether it
/// runs, its level, its live speed) belongs to its playback, see <see cref="EffectPlayback"/>.
/// Public only because <see cref="IDeskClient"/> has to be.
/// </remarks>
/// <param name="Number">Its number, 1 to <see cref="MaxEffects"/>, by which keys, steps and the outside name it.</param>
/// <param name="Name">What its owner calls it, up to <see cref="MaxNameLength"/> characters.</param>
/// <param name="Kind"><see cref="MovementKind"/>, <see cref="IntensityKind"/>, <see cref="ColorKind"/> or <see cref="PixelKind"/>.</param>
/// <param name="Shape">One of the <see cref="Shapes"/> of its kind, like "circle".</param>
public sealed record Effect(int Number, string Name, string Kind, string Shape)
{
    /// <summary>The most effects a project can have.</summary>
    public const int MaxEffects = 64;

    /// <summary>The longest name an effect can have.</summary>
    public const int MaxNameLength = 24;

    /// <summary>Moves heads: their pan and tilt.</summary>
    public const string MovementKind = "movement";

    /// <summary>Lets lights rise and fall.</summary>
    public const string IntensityKind = "intensity";

    /// <summary>Changes the color of lights.</summary>
    public const string ColorKind = "color";

    /// <summary>Paints a pattern over the cells of lights, by where they are on the plan.</summary>
    public const string PixelKind = "pixel";

    /// <summary>The way through the lights, or the turn of a shape, as it is.</summary>
    public const string Forward = "forward";

    /// <summary>The way through the lights, or the turn of a shape, the other way round.</summary>
    public const string Back = "back";

    /// <summary>The lights in the order of their addresses.</summary>
    public const string ByAddress = "address";

    /// <summary>The lights from the left of the stage to its right.</summary>
    public const string LeftToRight = "left";

    /// <summary>The lights from the middle of the selection outwards.</summary>
    public const string FromCenter = "center";

    /// <summary>The lights in an order chosen at random, the same every time (see <see cref="Seed"/>).</summary>
    public const string RandomOrder = "random";

    /// <summary>The slowest an effect can run: half a cycle a minute.</summary>
    public const double MinSpeed = 0.5;

    /// <summary>The fastest an effect can run: ten cycles a second.</summary>
    public const double MaxSpeed = 600;

    /// <summary>The longest fade in or out, in seconds, as for the times of a step.</summary>
    public const double MaxFade = Step.MaxSeconds;

    /// <summary>The most colors a chase, a fade or a pattern can have.</summary>
    public const int MaxColors = 8;

    /// <summary>The most lights a block can have.</summary>
    public const int MaxBlocks = 64;

    /// <summary>The most wings an effect can have.</summary>
    public const int MaxWings = 8;

    /// <summary>The shapes of each kind.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Shapes = new Dictionary<string, string[]>
    {
        [MovementKind] = ["circle", "eight", "pan", "tilt", "line", "random"],
        [IntensityKind] = ["sine", "rampup", "rampdown", "pulse", "random"],
        [ColorKind] = ["rainbow", "chase", "fade"],
        [PixelKind] = ["gradient", "ripple", "plasma", "rainbow", "bars", "sparkle"],
    };

    /// <summary>
    /// The group of the stage plan it works on, by its <see cref="LightGroup.Id"/>; null when it
    /// works on <see cref="Lights"/>. A group that is gone gives the effect no lights.
    /// </summary>
    public int? Group { get; init; }

    /// <summary>
    /// The lights of the stage plan it works on, by their <see cref="StageLight.Id"/>, when it has
    /// no <see cref="Group"/>. A light that is gone is passed over.
    /// </summary>
    public int[] Lights { get; init; } = [];

    /// <summary>How fast it runs: cycles a minute (BPM), <see cref="MinSpeed"/> to <see cref="MaxSpeed"/>.</summary>
    public double Speed { get; init; } = 30;

    /// <summary>
    /// 0 to 100 per cent. A movement: how big the shape is, as a share of each head's range. An
    /// intensity: how deep the wave goes (100 from dark to full, 20 from 80 % to full). A color: how
    /// deep the colors are (100 full colors, 0 white). A pixel pattern: how big it is, as a share of
    /// the lights it covers.
    /// </summary>
    public int Size { get; init; } = 100;

    /// <summary>0 to 100 per cent: 0 all lights together, 100 spread evenly over the lights. Not used by pixel patterns.</summary>
    public int Spread { get; init; } = 100;

    /// <summary><see cref="Forward"/> or <see cref="Back"/>.</summary>
    public string Direction { get; init; } = Forward;

    /// <summary><see cref="ByAddress"/>, <see cref="LeftToRight"/>, <see cref="FromCenter"/> or <see cref="RandomOrder"/>.</summary>
    public string Order { get; init; } = ByAddress;

    /// <summary>How many lights next to each other in the order move as one, 1 to <see cref="MaxBlocks"/>.</summary>
    public int Blocks { get; init; } = 1;

    /// <summary>Into how many parts the lights are split, every second one mirrored, 1 to <see cref="MaxWings"/>.</summary>
    public int Wings { get; init; } = 1;

    /// <summary>Seconds its key takes to bring it in.</summary>
    public double FadeIn { get; init; }

    /// <summary>Seconds its key takes to take it out.</summary>
    public double FadeOut { get; init; }

    /// <summary>A pulse: how long it is on, 1 to 99 per cent of a cycle. Bars: how wide they are.</summary>
    public int Duty { get; init; } = 50;

    /// <summary>
    /// The colors of a chase or a fade, and of a pattern (the first for the light, the second for
    /// what lies under it), like "#ff2000"; at most <see cref="MaxColors"/>. Empty for the usual ones.
    /// </summary>
    public string[] Colors { get; init; } = [];

    /// <summary>A movement around a center of its own: its pan, 0 to 100 per cent of the range; null to move around where the head points.</summary>
    public double? CenterPan { get; init; }

    /// <summary>The tilt of the center, 0 to 100 per cent; null with <see cref="CenterPan"/>.</summary>
    public double? CenterTilt { get; init; }

    /// <summary>The angle of a line, and the way a gradient, bars or a rainbow run over the plan, in degrees: 0 to the right, 90 to the front.</summary>
    public int Angle { get; init; }

    /// <summary>Where a ripple starts, in squares from the left of the stage; null for the middle of its lights.</summary>
    public double? PointX { get; init; }

    /// <summary>Where a ripple starts, in squares from the back of the stage; null with <see cref="PointX"/>.</summary>
    public double? PointY { get; init; }

    /// <summary>Chooses a random order and the random shapes; the same seed gives the same every time.</summary>
    public int Seed { get; init; }

    /// <summary>True for an effect that moves heads.</summary>
    internal bool IsMovement => Kind == MovementKind;

    /// <summary>True for an effect that paints a pattern over the cells of lights.</summary>
    internal bool IsPixel => Kind == PixelKind;

    /// <summary>True when it runs through the lights the other way round.</summary>
    internal bool Backwards => Direction == Back;

    /// <summary>The effect as the desk keeps it: every part checked, texts trimmed, numbers rounded.</summary>
    /// <returns>An effect the desk can keep.</returns>
    /// <exception cref="DeskException">Something about it cannot be used. The message says what.</exception>
    internal Effect Checked()
    {
        if (Number < 1 || Number > MaxEffects)
            throw new DeskException($"An effect has a number from 1 to {MaxEffects}, not {Number}.");

        string where = $"Effect {Number}";
        string kind = (Kind ?? "").Trim().ToLowerInvariant();
        if (!Shapes.TryGetValue(kind, out string[]? shapes))
            throw new DeskException($"{where} is a movement, an intensity, a color or a pixel effect, not \"{Kind}\".");

        string shape = (Shape ?? "").Trim().ToLowerInvariant();
        if (!shapes.Contains(shape))
            throw new DeskException($"{where}: a {kind} effect has no shape \"{Shape}\". It can be {string.Join(", ", shapes)}.");

        if (Group is < 1)
            throw new DeskException($"{where} needs a group of the stage plan, not {Group}.");

        int[] lights = Lights ?? [];
        if (lights.Length > StagePlan.MaxLights)
            throw new DeskException($"{where} has {lights.Length} lights; there can be {StagePlan.MaxLights}.");
        if (lights.Any(light => light < 1))
            throw new DeskException($"{where} names a light that is not on the stage plan.");

        if (!(Speed >= MinSpeed && Speed <= MaxSpeed))
            throw new DeskException(FormattableString.Invariant($"{where}: the speed must be from {MinSpeed} to {MaxSpeed} BPM, not {Speed}."));

        RequirePercent(Size, where, "size");
        RequirePercent(Spread, where, "spread");
        if (Duty < 1 || Duty > 99)
            throw new DeskException($"{where}: the duty must be from 1 to 99 per cent, not {Duty}.");

        string direction = (Direction ?? Forward).Trim().ToLowerInvariant();
        if (direction is not (Forward or Back))
            throw new DeskException($"{where} goes forward or back, not \"{Direction}\".");

        string order = (Order ?? ByAddress).Trim().ToLowerInvariant();
        if (order is not (ByAddress or LeftToRight or FromCenter or RandomOrder))
            throw new DeskException($"{where} takes its lights by address, from the left, from the center or at random, not \"{Order}\".");

        if (Blocks < 1 || Blocks > MaxBlocks)
            throw new DeskException($"{where}: a block has 1 to {MaxBlocks} lights, not {Blocks}.");
        if (Wings < 1 || Wings > MaxWings)
            throw new DeskException($"{where} has 1 to {MaxWings} wings, not {Wings}.");

        string[] colors = [.. (Colors ?? []).Select(color => (color ?? "").Trim().ToLowerInvariant())];
        if (colors.Length > MaxColors)
            throw new DeskException($"{where} has {colors.Length} colors; there can be {MaxColors}.");
        if (colors.FirstOrDefault(color => !PlanText.IsColor(color)) is { } bad)
            throw new DeskException($"{where}: \"{bad}\" is no color like #ff2000.");

        if (CenterPan.HasValue != CenterTilt.HasValue)
            throw new DeskException($"{where}: a center has a pan and a tilt.");
        if (CenterPan is double pan && !(pan >= 0 && pan <= 100) || CenterTilt is double tilt && !(tilt >= 0 && tilt <= 100))
            throw new DeskException($"{where}: the center's pan and tilt go from 0 to 100 per cent.");

        if (PointX.HasValue != PointY.HasValue)
            throw new DeskException($"{where}: the point a ripple starts from has a place across and from the back.");
        if (PointX is double x && !(Math.Abs(x) <= 1000) || PointY is double y && !(Math.Abs(y) <= 1000))
            throw new DeskException($"{where}: the point a ripple starts from is not on the plan.");

        return this with
        {
            Name = PlanText.Checked(Name, MaxNameLength, where, "a name"),
            Kind = kind,
            Shape = shape,
            Lights = [.. lights.Distinct()],
            Speed = Math.Round(Speed, 1),
            Direction = direction,
            Order = order,
            FadeIn = Seconds(FadeIn, where, "fade in"),
            FadeOut = Seconds(FadeOut, where, "fade out"),
            Colors = colors,
            CenterPan = CenterPan is double centerPan ? Math.Round(centerPan, 1) : null,
            CenterTilt = CenterTilt is double centerTilt ? Math.Round(centerTilt, 1) : null,
            Angle = ((Angle % 360) + 360) % 360,
            PointX = PointX is double pointX ? Math.Round(pointX, 2) : null,
            PointY = PointY is double pointY ? Math.Round(pointY, 2) : null,
        };
    }

    /// <summary>True when the other effect is the same in every part.</summary>
    internal bool SameAs(Effect other)
    {
        // The arrays are compared by their content; the rest as a record compares itself.
        int[] noLights = Array.Empty<int>();
        string[] noColors = Array.Empty<string>();
        return this with { Lights = noLights, Colors = noColors } == other with { Lights = noLights, Colors = noColors }
            && Lights.SequenceEqual(other.Lights) && Colors.SequenceEqual(other.Colors);
    }

    private static void RequirePercent(int value, string where, string what)
    {
        if (value < 0 || value > 100)
            throw new DeskException($"{where}: the {what} must be from 0 to 100 per cent, not {value}.");
    }

    private static double Seconds(double time, string where, string which)
    {
        // "Not a number" fails both comparisons, so it is refused as well.
        if (!(time >= 0 && time <= MaxFade))
            throw new DeskException(string.Create(CultureInfo.InvariantCulture, $"{where}: the {which} time must be from 0 to {MaxFade} seconds."));

        return Math.Round(time, 1);
    }
}
