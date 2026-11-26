namespace DmxBox.Server.Stage;

/// <summary>
/// What the stage plan says about the channels of its lights, for the output.
/// </summary>
/// <remarks>
/// <para>
/// A channel gives light when it is a dimmer (D) or a color (R G B W A U) of a light, a channel of
/// a device that gives no light (a hazer: no dimmer, color, pan or tilt), or no light's channel at
/// all. For these the highest level wins, and the master, blackout, a solo and a light's limit act
/// on them, as always.
/// </para>
/// <para>
/// Every other channel of a light that gives light is an attribute of it: a pan, a tilt, a wheel,
/// a shutter, a mode, a fine channel. An attribute keeps its value through the master, blackout and
/// a solo (the light goes dark, the moving head does not move), and the fader or memory that took
/// the light last gives it: latest takes precedence, see Desk.Attributes.cs.
/// </para>
/// </remarks>
internal sealed class PlanRoles
{
    /// <summary>The roles on a plan without lights: every channel gives light.</summary>
    public static PlanRoles None { get; } = new(StagePlan.Empty);

    // The letters of channels that give light.
    private const string LightLetters = "DRGBWAU";

    /// <summary>Works out the roles of the channels of a plan's lights.</summary>
    /// <param name="plan">A plan as <see cref="StagePlan.Checked"/> keeps it.</param>
    public PlanRoles(StagePlan plan)
    {
        var attributeLights = new List<AttributeLight>();
        var limits = new List<LimitRule>();
        foreach (StageLight light in plan.Lights)
        {
            int first = light.Address - 1;
            string letters = light.Channels;
            int[] channels = [.. Enumerable.Range(first, letters.Length)];

            bool device = IsDevice(letters);
            int[] attributes = device ? [] : [.. channels.Where(channel => !LightLetters.Contains(letters[channel - first]))];
            if (attributes.Length > 0)
            {
                foreach (int channel in attributes)
                    Attribute[channel] = true;
                attributeLights.Add(new AttributeLight(channels, attributes));
            }

            if (light.Limit < StageLight.NoLimit)
                limits.Add(LimitRule.For(light, device));
        }

        AttributeLights = [.. attributeLights];
        Limits = [.. limits];
    }

    /// <summary>For each channel (channel 1 at index 0): true for an attribute of a light, false for a channel that gives light.</summary>
    public bool[] Attribute { get; } = new bool[Desk.MaxChannels];

    /// <summary>The lights that have attributes.</summary>
    public AttributeLight[] AttributeLights { get; }

    /// <summary>What the limits of the lights with one below 100 % do.</summary>
    public LimitRule[] Limits { get; }

    /// <summary>True for the letters of a light that gives no light: no dimmer, no color, no pan or tilt (a hazer).</summary>
    public static bool IsDevice(string letters)
    {
        return !letters.Any(letter => LightLetters.Contains(letter) || letter is 'P' or 'T');
    }

    /// <summary>
    /// Finds the cells and the dimmers in the letters of a light's channels, as the UI does
    /// (stage-lights.ts): a cell is a run of color letters, a color that comes again starts the
    /// next cell; a D dims the cell it stands in, before or after its colors; one D in a light of
    /// several cells dims all of them.
    /// </summary>
    /// <returns>The color channels of each cell and its own dimmers, and the dimmers of the whole light, as places from the address.</returns>
    public static (List<(List<int> Colors, List<int> Dimmers)> Cells, List<int> Masters) PartsOf(string letters)
    {
        var cells = new List<(List<int> Colors, List<int> Dimmers)>();
        List<int>? colors = null;
        List<int>? dimmers = null;
        var waiting = new List<int>();   // dimmers before the colors of the next cell
        int dimmerCount = 0;

        for (int offset = 0; offset < letters.Length; offset++)
        {
            char letter = letters[offset];
            if (letter is 'R' or 'G' or 'B' or 'W' or 'A' or 'U')
            {
                if (colors is null || colors.Any(place => letters[place] == letter))
                {
                    colors = [];
                    dimmers = waiting;
                    waiting = [];
                    cells.Add((colors, dimmers));
                }

                colors.Add(offset);
            }
            else if (letter == 'D')
            {
                dimmerCount++;
                // After the colors of a cell that has no dimmer yet: that cell's. Otherwise the next one's.
                if (dimmers is { Count: 0 } && waiting.Count == 0)
                    dimmers.Add(offset);
                else
                    waiting.Add(offset);
            }
        }

        if (cells.Count == 0)
            return (cells, waiting);

        cells[^1].Dimmers.AddRange(waiting);   // dimmers after the last colors

        // One dimmer for a light of several cells: it dims them all.
        if (dimmerCount == 1 && cells.Count > 1)
        {
            List<int> master = [.. cells.SelectMany(cell => cell.Dimmers)];
            return ([.. cells.Select(cell => (cell.Colors, new List<int>()))], master);
        }

        return (cells, []);
    }
}

/// <summary>A light with attributes: all its channels, and those of them that are attributes (channel 1 at 0).</summary>
internal sealed record AttributeLight(int[] Channels, int[] Attributes);

/// <summary>
/// What the limit of a light does: its dimmers, and the channels of a device, stop at the limit;
/// the colors of a cell without a dimmer are dimmed together, so that the color stays.
/// </summary>
/// <param name="Cap">The limit as a level, 0-255.</param>
/// <param name="Stopped">The channels that stop at the limit (channel 1 at 0).</param>
/// <param name="Cells">For each cell without a dimmer: its color channels.</param>
internal sealed record LimitRule(byte Cap, int[] Stopped, int[][] Cells)
{
    /// <summary>The rule for a light with a limit.</summary>
    /// <param name="light">The light.</param>
    /// <param name="device">True for a light that gives no light: all its channels stop at the limit.</param>
    public static LimitRule For(StageLight light, bool device)
    {
        int first = light.Address - 1;

        // Halves round up, as the UI rounds them, so that both show the same.
        var cap = (byte)Math.Round(light.Limit * Desk.MaxLevel / 100.0, MidpointRounding.AwayFromZero);
        if (device)
            return new LimitRule(cap, [.. Enumerable.Range(first, light.Channels.Length)], []);

        (List<(List<int> Colors, List<int> Dimmers)> cells, List<int> masters) = PlanRoles.PartsOf(light.Channels);
        int[] stopped = [.. masters.Concat(cells.SelectMany(cell => cell.Dimmers)).Select(offset => first + offset)];
        int[][] free = masters.Count > 0
            ? []
            : [.. cells.Where(cell => cell.Dimmers.Count == 0).Select(cell => cell.Colors.Select(offset => first + offset).ToArray())];
        return new LimitRule(cap, stopped, free);
    }

    /// <summary>Holds the light's channels in "levels" to its limit.</summary>
    public void Apply(byte[] levels)
    {
        foreach (int channel in Stopped)
            levels[channel] = Math.Min(levels[channel], Cap);

        foreach (int[] cell in Cells)
        {
            byte strongest = cell.Max(channel => levels[channel]);
            if (strongest <= Cap)
                continue;

            foreach (int channel in cell)
                levels[channel] = (byte)Math.Round(levels[channel] * (double)Cap / strongest, MidpointRounding.AwayFromZero);
        }
    }
}
