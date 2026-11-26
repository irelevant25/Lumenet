namespace DmxBox.Server.Effects;

/// <summary>
/// What the shapes of the effects give at a moment: plain functions of the effect, the place of a
/// light in its run and the time, counted in cycles of the effect. The desk mixes what they give
/// with the rest (Desk.Effects.cs); the preview of the Effects view samples them.
/// </summary>
/// <remarks>
/// Every random shape takes its numbers from <see cref="Random01"/>, a function of the effect's
/// seed, the light and the cycle: so every output that asks at the same moment gets the same
/// light, and a shape comes back the same when it is played again.
/// </remarks>
internal static class EffectEngine
{
    /// <summary>A color: red, green and blue, each 0 to 1.</summary>
    internal readonly record struct Rgb(double R, double G, double B)
    {
        public static Rgb White { get; } = new(1, 1, 1);

        public static Rgb Black { get; } = new(0, 0, 0);

        /// <summary>"#ff2000" as a color.</summary>
        public static Rgb Of(string hex)
        {
            (double red, double green, double blue) = FixtureLooks.RgbOf(hex);
            return new Rgb(red / 255, green / 255, blue / 255);
        }

        /// <summary>The color as red, green and blue from 0 to 255, as the mixing of the fixtures takes it.</summary>
        public (double R, double G, double B) Scaled => (R * 255, G * 255, B * 255);

        /// <summary>The color as "#ff2000".</summary>
        public string Hex => $"#{Byte(R):x2}{Byte(G):x2}{Byte(B):x2}";

        public Rgb Times(double factor) => new(R * factor, G * factor, B * factor);

        private static int Byte(double part) => (int)Math.Round(Math.Clamp(part, 0, 1) * 255, MidpointRounding.AwayFromZero);
    }

    /// <summary>The colors of a chase or a fade that names none: red and blue.</summary>
    private static readonly Rgb[] TwoColors = [new(1, 0, 0), new(0, 0, 1)];

    /// <summary>
    /// The time of one light in the effect, in cycles: the effect's own time, the other way round
    /// for an effect that goes back, behind by its place in the run times the spread.
    /// </summary>
    /// <param name="cycles">How many cycles the effect has run.</param>
    /// <param name="place">The light's place in the run, 0 to below 1.</param>
    /// <param name="spread">0 to 1: 0 all lights together, 1 spread evenly over one cycle.</param>
    /// <param name="backwards">True for an effect that goes back.</param>
    public static double PhaseOf(double cycles, double place, double spread, bool backwards)
    {
        return (backwards ? -cycles : cycles) - (spread * place);
    }

    /// <summary>What lies after the point of a number: 2.25 gives 0.25, -0.25 gives 0.75.</summary>
    public static double Fraction(double value) => value - Math.Floor(value);

    /// <summary>True for a movement that moves the pan.</summary>
    public static bool UsesPan(Effect effect) => effect.CenterPan is not null || effect.Shape != "tilt";

    /// <summary>True for a movement that moves the tilt.</summary>
    public static bool UsesTilt(Effect effect) => effect.CenterPan is not null || effect.Shape != "pan";

    /// <summary>
    /// Where a movement takes a head away from its center: pan and tilt from -0.5 to 0.5 of the
    /// shape's size. A circle starts at its top and turns clockwise.
    /// </summary>
    /// <param name="effect">A movement.</param>
    /// <param name="phase">The light's time in cycles, see <see cref="PhaseOf"/>.</param>
    /// <param name="light">The light, for the random shape.</param>
    public static (double Pan, double Tilt) Movement(Effect effect, double phase, int light)
    {
        double turn = 2 * Math.PI * Fraction(phase);
        double angle = effect.Angle * Math.PI / 180;
        return effect.Shape switch
        {
            "circle" => (0.5 * Math.Sin(turn), -0.5 * Math.Cos(turn)),
            "eight" => (0.5 * Math.Sin(turn), 0.25 * Math.Sin(2 * turn)),
            "pan" => (0.5 * Math.Sin(turn), 0),
            "tilt" => (0, 0.5 * Math.Sin(turn)),
            "line" => (0.5 * Math.Sin(turn) * Math.Cos(angle), 0.5 * Math.Sin(turn) * Math.Sin(angle)),
            "random" => (0.5 * Wander(effect.Seed, light, 0, phase), 0.5 * Wander(effect.Seed, light, 1, phase)),
            _ => (0, 0),
        };
    }

    /// <summary>
    /// The wave of an intensity, 0 to 1: a sine from dark to full and back, a ramp up, a ramp down,
    /// a pulse that is on for its duty, or a new random level every cycle.
    /// </summary>
    /// <param name="effect">An intensity.</param>
    /// <param name="phase">The light's time in cycles, see <see cref="PhaseOf"/>.</param>
    /// <param name="light">The light, for the random shape.</param>
    public static double Wave(Effect effect, double phase, int light)
    {
        double part = Fraction(phase);
        return effect.Shape switch
        {
            "sine" => 0.5 - (0.5 * Math.Cos(2 * Math.PI * part)),
            "rampup" => part,
            "rampdown" => 1 - part,
            "pulse" => part < effect.Duty / 100.0 ? 1 : 0,
            "random" => Random01(effect.Seed, light, (long)Math.Floor(phase), 2),
            _ => 0,
        };
    }

    /// <summary>
    /// What an intensity gives a light, 0 to 1, before its fader: the wave, so deep as the size says
    /// (100 from dark to full, 20 from 80 % to full).
    /// </summary>
    public static double Intensity(Effect effect, double phase, int light, int size)
    {
        double depth = size / 100.0;
        return 1 - depth + (depth * Wave(effect, phase, light));
    }

    /// <summary>
    /// The color of a color effect: round the color circle (a rainbow), from one color to the next
    /// at once (a chase) or fading (a fade), as deep as the size says (100 full color, 0 white).
    /// </summary>
    /// <param name="effect">A color effect.</param>
    /// <param name="phase">The light's time in cycles, see <see cref="PhaseOf"/>.</param>
    /// <param name="size">How deep the colors are, 0 to 100.</param>
    public static Rgb ColorOf(Effect effect, double phase, int size)
    {
        double part = Fraction(phase);
        Rgb[] colors = ColorsOf(effect, TwoColors);
        Rgb color = effect.Shape switch
        {
            "rainbow" => Hue(part),
            "chase" => colors[Math.Min(colors.Length - 1, (int)Math.Floor(part * colors.Length))],
            "fade" => Through(colors, part, round: true),
            _ => Rgb.White,
        };

        double depth = size / 100.0;
        return new Rgb(1 - (depth * (1 - color.R)), 1 - (depth * (1 - color.G)), 1 - (depth * (1 - color.B)));
    }

    /// <summary>
    /// What a pixel pattern gives one pixel: a color and how bright, 0 to 1. The patterns lie on the
    /// plan: they run over the pixels by where these are, whatever lights they belong to.
    /// </summary>
    /// <param name="effect">A pixel effect.</param>
    /// <param name="rig">Its lights, for their middle and how far they reach.</param>
    /// <param name="pixel">The pixel.</param>
    /// <param name="cycles">How many cycles the effect has run.</param>
    /// <param name="size">How big the pattern is, as a share of the lights it covers, 0 to 100; for sparkle how many flash.</param>
    public static (Rgb Color, double Brightness) Pattern(Effect effect, EffectRig rig, EffectPixel pixel, double cycles, int size)
    {
        double scale = Math.Max(size, 1) / 100.0;
        double time = effect.Backwards ? -cycles : cycles;
        double x = (pixel.X - rig.CenterX) / rig.Span;
        double y = (pixel.Y - rig.CenterY) / rig.Span;
        double angle = effect.Angle * Math.PI / 180;
        double along = (x * Math.Cos(angle)) + (y * Math.Sin(angle)) + 0.5;   // 0 to 1 over the lights, the effect's way
        Rgb[] colors = ColorsOf(effect, []);
        Rgb first = colors.Length > 0 ? colors[0] : Rgb.White;
        Rgb? second = colors.Length > 1 ? colors[1] : null;

        switch (effect.Shape)
        {
            case "gradient":
                {
                    // there and back over one length, so that no edge shows where it starts again
                    double there = 1 - Math.Abs((2 * Fraction((along / scale) - time)) - 1);
                    return second is not null ? (Through(colors, there, round: false), 1) : (first, 1 - there);
                }

            case "rainbow":
                return (Hue(Fraction((along / scale) - time)), 1);

            case "bars":
                {
                    bool lit = Fraction((along / scale) - time) < effect.Duty / 100.0;
                    return second is { } under ? (lit ? first : under, 1) : (first, lit ? 1 : 0);
                }

            case "ripple":
                {
                    double fromX = (effect.PointX ?? rig.CenterX) - pixel.X;
                    double fromY = (effect.PointY ?? rig.CenterY) - pixel.Y;
                    double away = Math.Sqrt((fromX * fromX) + (fromY * fromY)) / rig.Span;
                    double ring = 0.5 + (0.5 * Math.Cos(2 * Math.PI * ((away / scale) - time)));
                    return second is { } under ? (Mixed(under, first, ring), 1) : (first, ring);
                }

            case "plasma":
                {
                    double px = x / scale, py = y / scale;
                    double sum = Math.Sin(2 * Math.PI * (px + time))
                        + Math.Sin(2 * Math.PI * (py - (0.7 * time)))
                        + Math.Sin(2 * Math.PI * ((0.7 * (px + py)) + (0.5 * time)))
                        + Math.Sin(2 * Math.PI * ((1.3 * Math.Sqrt((px * px) + (py * py))) - time));
                    double level = (sum / 4 + 1) / 2;
                    return (second is not null ? Through(colors, level, round: false) : Hue(level), 1);
                }

            case "sparkle":
                {
                    // Every cycle each pixel may flash once, at a moment of its own, and fade within an
                    // eighth of a cycle; the size says how many of them do.
                    double whole = Math.Floor(cycles);
                    double into = cycles - whole;
                    bool flashes = Random01(effect.Seed, pixel.Index, (long)whole, 3) < scale;
                    double moment = Random01(effect.Seed, pixel.Index, (long)whole, 4) * 0.875;
                    double bright = flashes && into >= moment ? Math.Max(0, 1 - ((into - moment) * 8)) : 0;
                    return (first, bright);
                }

            default:
                return (Rgb.Black, 0);
        }
    }

    /// <summary>A color of the color circle: 0 red, a third green, two thirds blue, 1 red again.</summary>
    public static Rgb Hue(double hue)
    {
        double sixth = Fraction(hue) * 6;
        double rising = sixth - Math.Floor(sixth);
        return (int)Math.Floor(sixth) switch
        {
            0 => new Rgb(1, rising, 0),
            1 => new Rgb(1 - rising, 1, 0),
            2 => new Rgb(0, 1, rising),
            3 => new Rgb(0, 1 - rising, 1),
            4 => new Rgb(rising, 0, 1),
            _ => new Rgb(1, 0, 1 - rising),
        };
    }

    /// <summary>The way between two colors: 0 the first, 1 the second.</summary>
    public static Rgb Mixed(Rgb from, Rgb to, double way)
    {
        return new Rgb(from.R + ((to.R - from.R) * way), from.G + ((to.G - from.G) * way), from.B + ((to.B - from.B) * way));
    }

    /// <summary>
    /// A number from 0 to below 1 that looks random, the same every time for the same seed, light,
    /// cycle and use: a hash of the four (SplitMix64's way of mixing).
    /// </summary>
    public static double Random01(int seed, int light, long cycle, int use)
    {
        unchecked
        {
            ulong hash = ((ulong)(uint)seed * 0x9E3779B97F4A7C15UL)
                ^ ((ulong)(uint)light * 0xC2B2AE3D27D4EB4FUL)
                ^ ((ulong)cycle * 0x165667B19E3779F9UL)
                ^ ((ulong)(uint)use * 0x27D4EB2F165667C5UL);
            hash += 0x9E3779B97F4A7C15UL;
            hash = (hash ^ (hash >> 30)) * 0xBF58476D1CE4E5B9UL;
            hash = (hash ^ (hash >> 27)) * 0x94D049BB133111EBUL;
            hash ^= hash >> 31;
            return (hash >> 11) * (1.0 / (1UL << 53));
        }
    }

    // A random way that moves smoothly: a new point every cycle, the way between eased; -1 to 1.
    private static double Wander(int seed, int light, int axis, double phase)
    {
        double whole = Math.Floor(phase);
        double part = phase - whole;
        double from = (Random01(seed, light, (long)whole, 10 + axis) * 2) - 1;
        double to = (Random01(seed, light, (long)whole + 1, 10 + axis) * 2) - 1;
        double eased = part * part * (3 - (2 * part));
        return from + ((to - from) * eased);
    }

    // The way through a list of colors, 0 the first, 1 the last; "round": back to the first at 1.
    private static Rgb Through(Rgb[] colors, double way, bool round)
    {
        if (colors.Length == 1)
            return colors[0];

        int steps = round ? colors.Length : colors.Length - 1;
        double at = Math.Clamp(way, 0, 1) * steps;
        int from = Math.Min((int)Math.Floor(at), steps - 1);
        double part = at - from;
        double eased = part * part * (3 - (2 * part));
        return Mixed(colors[from], colors[(from + 1) % colors.Length], eased);
    }

    private static Rgb[] ColorsOf(Effect effect, Rgb[] usual)
    {
        return effect.Colors.Length > 0 ? [.. effect.Colors.Select(Rgb.Of)] : usual;
    }
}
