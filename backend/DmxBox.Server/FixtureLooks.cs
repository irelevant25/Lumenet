namespace DmxBox.Server;

/// <summary>
/// What a step of a fixture or of a group of fixtures gives the channels of its lights, at the
/// step's full value. The UI plays a fixture the same way (fixtures.ts there).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A light with a dimmer: the dimmer at the step's value, and its color channels mixing
/// the step's color at full (the dimmer dims them).</item>
/// <item>A light without a dimmer: its color channels mix the color, the strongest at the step's
/// value. A white channel takes the white part of the color, an amber one the warm part.</item>
/// <item>A device that gives no light (a hazer): its first channel at the value.</item>
/// <item>A light of the fixture library, while the step gives light: its shutter open (the first
/// range called "Open"), and its color wheel on the slot nearest the color. These are
/// attributes of the light, see <see cref="PlanRoles"/>.</item>
/// </list>
/// A step without a color shines white. Pan, tilt and the other attributes are left to the
/// memories, which hold whole looks.
/// </remarks>
internal static class FixtureLooks
{
    // The letters of channels that give light.
    private const string LightLetters = "DRGBWAU";

    /// <summary>A channel a step gives a level: channel 1 at 0; true for an attribute of a light.</summary>
    public readonly record struct Part(int Channel, byte Value, bool Attribute);

    /// <summary>What a step of a fixture or a group gives, at its full value; nothing for a light or group that is gone.</summary>
    /// <param name="plan">The stage plan, which says what the fixtures are.</param>
    /// <param name="step">A step whose target is a fixture or a group.</param>
    public static Part[] Of(StagePlan plan, Step step)
    {
        IEnumerable<StageLight> lights = step.Target == Step.Fixture
            ? plan.Lights.Where(light => light.Id == step.Number)
            : (plan.Groups.FirstOrDefault(group => group.Id == step.Number)?.Lights ?? [])
                .Select(id => plan.Lights.FirstOrDefault(light => light.Id == id))
                .OfType<StageLight>();

        (double R, double G, double B)? color = step.Color is { } hex ? RgbOf(hex) : null;
        var parts = new List<Part>();
        foreach (StageLight light in lights)
            AddLight(parts, plan, light, step.Value, color);

        return [.. parts.Where(part => part.Channel < Desk.MaxChannels)];
    }

    private static void AddLight(List<Part> parts, StagePlan plan, StageLight light, int level, (double R, double G, double B)? color)
    {
        int first = light.Address - 1;
        string letters = light.Channels;
        if (PlanRoles.IsDevice(letters))
        {
            parts.Add(new Part(first, (byte)level, Attribute: false));
            return;
        }

        (List<(List<int> Colors, List<int> Dimmers)> cells, List<int> masters) = PlanRoles.PartsOf(letters);
        int[] dimmers = [.. masters.Concat(cells.SelectMany(cell => cell.Dimmers))];
        foreach (int offset in dimmers)
            parts.Add(new Part(first + offset, (byte)level, Attribute: false));

        if (cells.Count > 0)
            AddColors(parts, first, letters, cells, color ?? (255, 255, 255), dimmers.Length > 0 ? Desk.MaxLevel : level);

        FixtureType? type = light.Type is int id ? plan.Types.FirstOrDefault(kind => kind.Id == id) : null;
        if (type is null || level == 0)
            return;

        for (int offset = 0; offset < type.Channels.Length && offset < letters.Length; offset++)
        {
            FixtureChannel channel = type.Channels[offset];
            bool attribute = !LightLetters.Contains(letters[offset]);
            if (!attribute)
                continue;

            if (channel.Code == "S" && channel.Ranges.FirstOrDefault(range => range.Label.StartsWith("Open", StringComparison.OrdinalIgnoreCase)) is { } open)
                parts.Add(new Part(first + offset, (byte)open.From, Attribute: true));
            else if (color is { } wanted && cells.Count == 0 && channel.Ranges.Any(range => range.Color is not null))
                parts.Add(new Part(first + offset, (byte)Nearest(channel.Ranges, wanted).From, Attribute: true));
        }
    }

    // The color channels of every cell mixing the color, the strongest at "intensity".
    private static void AddColors(
        List<Part> parts,
        int first,
        string letters,
        List<(List<int> Colors, List<int> Dimmers)> cells,
        (double R, double G, double B) color,
        int intensity)
    {
        foreach ((List<int> offsets, _) in cells)
        {
            double[] mix = MixCell(letters, offsets, color);
            for (int index = 0; index < offsets.Count; index++)
            {
                double value = mix[index] * intensity;
                parts.Add(new Part(first + offsets[index], (byte)Math.Round(value, MidpointRounding.AwayFromZero), Attribute: false));
            }
        }
    }

    /// <summary>
    /// How the color channels of one cell mix a color, as the Fixtures view mixes it: a white
    /// channel takes the white part of the color, an amber one the warm part that is left, an
    /// ultraviolet one nothing. The effects mix their colors the same way.
    /// </summary>
    /// <param name="letters">The letters of the light's channels.</param>
    /// <param name="offsets">The places of the cell's color channels among them.</param>
    /// <param name="color">The color: red, green and blue, 0 to 255 (or 0 to 1: only the proportions count).</param>
    /// <returns>For each channel of the cell, in the order of <paramref name="offsets"/>: its share, the strongest at 1; all 0 for black.</returns>
    internal static double[] MixCell(string letters, IReadOnlyList<int> offsets, (double R, double G, double B) color)
    {
        bool Has(char letter) => offsets.Any(offset => letters[offset] == letter);
        (double red, double green, double blue) = color;
        double white = 0;
        double amber = 0;
        if (Has('W'))
        {
            white = Math.Min(red, Math.Min(green, blue));
            red -= white;
            green -= white;
            blue -= white;
        }

        if (Has('A') && red > 0 && green > 0 && blue == 0)
        {
            // amber is red with about a third of green: the part of the color that is amber goes to it
            amber = Math.Min(red, green / 0.35);
            red -= amber;
            green -= amber * 0.35;
        }

        double Wanted(char letter) => letter switch
        {
            'R' => red,
            'G' => green,
            'B' => blue,
            'W' => white,
            'A' => amber,
            _ => 0,
        };

        double[] shares = new double[offsets.Count];
        double strongest = offsets.Count == 0 ? 0 : offsets.Max(offset => Wanted(letters[offset]));
        if (strongest <= 0)
            return shares;

        for (int index = 0; index < offsets.Count; index++)
            shares[index] = Math.Max(0, Wanted(letters[offsets[index]])) / strongest;

        return shares;
    }

    /// <summary>The slot of a color wheel whose color is nearest to the color wanted (red, green and blue, 0 to 255).</summary>
    internal static FixtureRange Nearest(FixtureRange[] ranges, (double R, double G, double B) wanted)
    {
        return ranges
            .Where(range => range.Color is not null)
            .MinBy(range =>
            {
                (double red, double green, double blue) = RgbOf(range.Color!);
                return Math.Pow(red - wanted.R, 2) + Math.Pow(green - wanted.G, 2) + Math.Pow(blue - wanted.B, 2);
            })!;
    }

    /// <summary>"#ff2000" as red, green and blue, 0 to 255.</summary>
    internal static (double R, double G, double B) RgbOf(string hex)
    {
        int Part(int start) => Convert.ToInt32(hex.Substring(start, 2), 16);
        return (Part(1), Part(3), Part(5));
    }
}
