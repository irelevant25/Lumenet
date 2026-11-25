namespace DmxBox.Server;

/// <summary>
/// The lights an effect works on, as the desk needs them for it: their channels by what they do,
/// where they and their cells are on the plan, and where each stands in the effect's run (its
/// order, wings and blocks). Made from the plan and the effect; made anew when either changes.
/// </summary>
internal sealed class EffectRig
{
    private EffectRig(EffectTarget[] targets, EffectPixel[] pixels, double centerX, double centerY, double span)
    {
        Targets = targets;
        Pixels = pixels;
        CenterX = centerX;
        CenterY = centerY;
        Span = span;
    }

    /// <summary>The lights, in the effect's order, each with its place in the run.</summary>
    public EffectTarget[] Targets { get; }

    /// <summary>For a pixel effect: every cell of its lights (a light without cells is one), with its place on the plan.</summary>
    public EffectPixel[] Pixels { get; }

    /// <summary>The middle of the lights on the plan, in squares.</summary>
    public double CenterX { get; }

    /// <summary>The middle of the lights from the back, in squares.</summary>
    public double CenterY { get; }

    /// <summary>How far the lights reach on the plan, in squares (at least 1): a pixel pattern of size 100 % is this big.</summary>
    public double Span { get; }

    /// <summary>The lights of an effect on a plan, those that can do what the effect does.</summary>
    /// <param name="plan">The stage plan.</param>
    /// <param name="effect">The effect.</param>
    public static EffectRig Of(StagePlan plan, Effect effect)
    {
        IEnumerable<int> ids = effect.Group is int group
            ? plan.Groups.FirstOrDefault(candidate => candidate.Id == group)?.Lights ?? []
            : effect.Lights;

        var targets = new List<EffectTarget>();
        foreach (int id in ids.Distinct())
        {
            if (plan.Lights.FirstOrDefault(light => light.Id == id) is not { } light || PlanRoles.IsDevice(light.Channels))
                continue;

            EffectTarget target = TargetOf(plan, light);
            if (Fits(target, effect))
                targets.Add(target);
        }

        EffectTarget[] ordered = Ordered(targets, effect);
        PlaceInRun(ordered, effect);

        EffectPixel[] pixels = effect.IsPixel ? PixelsOf(ordered) : [];
        (double centerX, double centerY, double span) = Extent(pixels.Length > 0
            ? pixels.Select(pixel => (pixel.X, pixel.Y))
            : ordered.Select(target => (target.Light.X, target.Light.Y)));
        return new EffectRig(ordered, pixels, centerX, centerY, span);
    }

    // What a light's channels do, by the letters of its channels and its kind from the library.
    private static EffectTarget TargetOf(StagePlan plan, StageLight light)
    {
        int first = light.Address - 1;
        string letters = light.Channels;
        (List<(List<int> Colors, List<int> Dimmers)> cells, List<int> masters) = PlanRoles.PartsOf(letters);
        FixtureType? type = light.Type is int kind ? plan.Types.FirstOrDefault(candidate => candidate.Id == kind) : null;

        // Cells lie along the light's longer side, as the Stage view draws them, and turn with it.
        var targetCells = new EffectCell[cells.Count];
        bool deep = light.Depth > light.Width;
        double turn = light.Angle * Math.PI / 180;
        for (int index = 0; index < cells.Count; index++)
        {
            double along = ((index + 0.5) / cells.Count) - 0.5;
            double localX = deep ? 0 : along * light.Width;
            double localY = deep ? along * light.Depth : 0;

            // Clockwise on the plan, where the y axis points to the front.
            double x = light.X + (localX * Math.Cos(turn)) - (localY * Math.Sin(turn));
            double y = light.Y + (localX * Math.Sin(turn)) + (localY * Math.Cos(turn));
            targetCells[index] = new EffectCell(
                [.. cells[index].Colors],
                [.. cells[index].Colors.Select(offset => first + offset)],
                [.. cells[index].Dimmers.Select(offset => first + offset)],
                x,
                y);
        }

        var target = new EffectTarget(light, first, targetCells, [.. masters.Select(offset => first + offset)]);

        int pan = letters.IndexOf('P');
        int tilt = letters.IndexOf('T');
        target.Pan = pan >= 0 ? first + pan : -1;
        target.Tilt = tilt >= 0 ? first + tilt : -1;
        if (type is null)
            return target;

        int FineOf(int coarse) => coarse < 0 ? -1 : Array.FindIndex(type.Channels, channel => channel.Code == "F" && channel.Fine == coarse);
        int panFine = FineOf(pan), tiltFine = FineOf(tilt);
        target.PanFine = panFine >= 0 ? first + panFine : -1;
        target.TiltFine = tiltFine >= 0 ? first + tiltFine : -1;

        for (int offset = 0; offset < type.Channels.Length && offset < letters.Length; offset++)
        {
            FixtureChannel channel = type.Channels[offset];
            if (channel.Code == "S" && target.Shutter < 0
                && channel.Ranges.FirstOrDefault(range => range.Label.StartsWith("Open", StringComparison.OrdinalIgnoreCase)) is { } open)
            {
                target.Shutter = first + offset;
                target.ShutterRanges = channel.Ranges;
                target.OpenValue = open.From;
            }
            else if (cells.Count == 0 && target.Wheel < 0 && "DRGBWAU".IndexOf(letters[offset]) < 0 && channel.Ranges.Any(range => range.Color is not null))
            {
                target.Wheel = first + offset;
                target.WheelRanges = channel.Ranges;
            }
        }

        return target;
    }

    // Only the lights that can do what the effect does: heads for a movement, lights that give
    // light for an intensity or a pattern, lights with colors or a color wheel for a color.
    private static bool Fits(EffectTarget target, Effect effect)
    {
        return effect.Kind switch
        {
            Effect.MovementKind => target.Pan >= 0 || target.Tilt >= 0,
            Effect.ColorKind => target.Cells.Length > 0 || target.Wheel >= 0,
            _ => target.Cells.Length > 0 || target.Masters.Length > 0,
        };
    }

    // The lights in the effect's order: by address, from the left, from the middle outwards, or at
    // random (the same random order for the same seed).
    private static EffectTarget[] Ordered(List<EffectTarget> targets, Effect effect)
    {
        EffectTarget[] byAddress = [.. targets.OrderBy(target => target.Light.Address).ThenBy(target => target.Light.Id)];
        switch (effect.Order)
        {
            case Effect.LeftToRight:
                return [.. byAddress.OrderBy(target => target.Light.X).ThenBy(target => target.Light.Y)];
            case Effect.FromCenter:
                {
                    double x = byAddress.Length == 0 ? 0 : byAddress.Average(target => target.Light.X);
                    double y = byAddress.Length == 0 ? 0 : byAddress.Average(target => target.Light.Y);
                    foreach (EffectTarget target in byAddress)
                        target.Distance = Math.Round(Math.Sqrt(Math.Pow(target.Light.X - x, 2) + Math.Pow(target.Light.Y - y, 2)), 2);
                    return [.. byAddress.OrderBy(target => target.Distance)];
                }

            case Effect.RandomOrder:
                {
                    var random = new Random(effect.Seed);
                    EffectTarget[] shuffled = [.. byAddress];
                    random.Shuffle(shuffled);
                    return shuffled;
                }

            default:
                return byAddress;
        }
    }

    // Where each light stands in the run, from 0 (first) to below 1. Lights the same distance from
    // the middle stand together. The run is split into wings, every second one the other way round,
    // and in each wing the lights go in blocks that move as one.
    private static void PlaceInRun(EffectTarget[] ordered, Effect effect)
    {
        // The lights in turns: with the order from the middle, those as far out go together.
        var turns = new List<List<EffectTarget>>();
        foreach (EffectTarget target in ordered)
        {
            if (effect.Order == Effect.FromCenter && turns.Count > 0 && turns[^1][0].Distance == target.Distance)
                turns[^1].Add(target);
            else
                turns.Add([target]);
        }

        int wings = Math.Min(effect.Wings, Math.Max(1, turns.Count));
        for (int wing = 0; wing < wings; wing++)
        {
            int from = turns.Count * wing / wings;
            int to = turns.Count * (wing + 1) / wings;
            int length = to - from;
            int blocks = (length + effect.Blocks - 1) / effect.Blocks;
            for (int at = from; at < to; at++)
            {
                int inWing = wing % 2 == 1 ? to - 1 - at : at - from;
                double place = blocks <= 1 ? 0 : (double)(inWing / effect.Blocks) / blocks;
                foreach (EffectTarget target in turns[at])
                    target.Place = place;
            }
        }
    }

    // Every cell of the lights, each a pixel; a light without cells (a dimmer and a gel) is one.
    private static EffectPixel[] PixelsOf(EffectTarget[] targets)
    {
        var pixels = new List<EffectPixel>();
        for (int index = 0; index < targets.Length; index++)
        {
            EffectTarget target = targets[index];
            if (target.Cells.Length == 0)
            {
                pixels.Add(new EffectPixel(target, -1, target.Light.X, target.Light.Y, pixels.Count));
                continue;
            }

            for (int cell = 0; cell < target.Cells.Length; cell++)
                pixels.Add(new EffectPixel(target, cell, target.Cells[cell].X, target.Cells[cell].Y, pixels.Count));
        }

        return [.. pixels];
    }

    private static (double CenterX, double CenterY, double Span) Extent(IEnumerable<(double X, double Y)> places)
    {
        (double X, double Y)[] all = [.. places];
        if (all.Length == 0)
            return (0, 0, 1);

        double minX = all.Min(place => place.X), maxX = all.Max(place => place.X);
        double minY = all.Min(place => place.Y), maxY = all.Max(place => place.Y);
        return ((minX + maxX) / 2, (minY + maxY) / 2, Math.Max(1, Math.Max(maxX - minX, maxY - minY)));
    }
}

/// <summary>A light an effect works on: its channels by what they do (channel 1 at 0; -1 for none) and its place in the run.</summary>
/// <param name="Light">The light of the plan.</param>
/// <param name="First">Its first channel.</param>
/// <param name="Cells">Its cells of color channels, each with its own dimmers.</param>
/// <param name="Masters">Its dimmers that dim all its cells, or the dimmers of a light without colors.</param>
internal sealed record EffectTarget(StageLight Light, int First, EffectCell[] Cells, int[] Masters)
{
    public int Pan { get; set; } = -1;

    public int PanFine { get; set; } = -1;

    public int Tilt { get; set; } = -1;

    public int TiltFine { get; set; } = -1;

    /// <summary>A shutter of a light from the library that has a range "Open".</summary>
    public int Shutter { get; set; } = -1;

    public FixtureRange[] ShutterRanges { get; set; } = [];

    /// <summary>The value that opens the shutter: the start of its first range "Open".</summary>
    public int OpenValue { get; set; } = -1;

    /// <summary>A color wheel of a light from the library without color channels.</summary>
    public int Wheel { get; set; } = -1;

    public FixtureRange[] WheelRanges { get; set; } = [];

    /// <summary>Where it stands in the effect's run: 0 first, up to below 1.</summary>
    public double Place { get; set; }

    /// <summary>How far it is from the middle of the lights, for the order from the middle outwards.</summary>
    public double Distance { get; set; }
}

/// <summary>A cell of a light: its color channels (their places among the light's letters, and the channels) and its own dimmers.</summary>
internal sealed record EffectCell(int[] Offsets, int[] Colors, int[] Dimmers, double X, double Y);

/// <summary>A pixel of a pixel effect: a cell of a light (-1 for a light without cells), where it is, and its number among the pixels.</summary>
internal sealed record EffectPixel(EffectTarget Target, int Cell, double X, double Y, int Index);
