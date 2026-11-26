using System.Globalization;

namespace DmxBox.Server.Stage;

/// <summary>
/// The stage plan of a project: the stage seen from above, a grid of squares, and the lights of
/// the rig on it, each where it hangs. The UI draws every light in the color its channels give
/// it at the moment, so that a show can be programmed without the rig.
/// </summary>
/// <remarks>
/// A light can also hang around the stage: <see cref="SideRoom"/> squares beside it and
/// <see cref="BackRoom"/> behind it, and <see cref="FrontRoom"/> in front of it, over the
/// audience. Public only because <see cref="IDeskClient"/> has to be.
/// </remarks>
/// <param name="Width">How wide the stage is, in squares of its grid.</param>
/// <param name="Depth">How deep the stage is, from its back to its front edge, in squares.</param>
/// <param name="Lights">The lights, in no particular order.</param>
public sealed record StagePlan(int Width, int Depth, StageLight[] Lights)
{
    /// <summary>The size of the stage of a new plan, in squares.</summary>
    public const int DefaultWidth = 16;

    /// <summary>The depth of the stage of a new plan, in squares.</summary>
    public const int DefaultDepth = 10;

    /// <summary>The smallest a stage can be, across and from back to front, in squares.</summary>
    public const int MinSize = 4;

    /// <summary>The biggest a stage can be, across and from back to front, in squares.</summary>
    public const int MaxSize = 64;

    /// <summary>The most lights a plan can have: one for every channel of the most universes.</summary>
    public const int MaxLights = Desk.MaxChannels;

    /// <summary>The most kinds of lights from the fixture library a plan can have.</summary>
    public const int MaxTypes = 64;

    /// <summary>The most groups of lights a plan can have.</summary>
    public const int MaxGroups = 64;

    /// <summary>Squares beside the stage, at the left and the right, where lights can hang too.</summary>
    public const int SideRoom = 2;

    /// <summary>Squares behind the stage where lights can hang too.</summary>
    public const int BackRoom = 2;

    /// <summary>Squares in front of the stage, over the audience, where lights can hang too.</summary>
    public const int FrontRoom = 4;

    /// <summary>How brightly a new picture shows through: 0.1 is faint, 1 as it is.</summary>
    public const double DefaultPictureOpacity = 0.6;

    /// <summary>A picture under the stage only; <see cref="PictureOverPlan"/> under the stage and the room around it.</summary>
    public const string PictureOverStage = "stage";

    /// <summary>A picture under the whole plan: the stage and the room around it.</summary>
    public const string PictureOverPlan = "plan";

    /// <summary>The plan of a desk that has none yet: a stage of the usual size without lights.</summary>
    public static StagePlan Empty { get; } = new(DefaultWidth, DefaultDepth, []);

    /// <summary>
    /// The picture under the plan, a photo or a drawing of the stage, by a short name of its
    /// content; null without one. The picture itself is not part of the plan the UIs get with
    /// every change: they ask for it (GetStagePicture) when this name changes. A plan from a UI
    /// cannot change it, see <see cref="Desk.SetStagePicture"/>.
    /// </summary>
    public string? PictureId { get; init; }

    /// <summary>How brightly the picture shows through, from 0.1 to 1.</summary>
    public double PictureOpacity { get; init; } = DefaultPictureOpacity;

    /// <summary>What the picture lies under: <see cref="PictureOverStage"/> or <see cref="PictureOverPlan"/>.</summary>
    public string PictureOver { get; init; } = PictureOverStage;

    /// <summary>
    /// The kinds of lights from the fixture library (the Open Fixture Library) that lights of the
    /// plan are, each with the names of its channels. A light names its kind by its
    /// <see cref="FixtureType.Id"/>; a kind no light names is dropped.
    /// </summary>
    public FixtureType[] Types { get; init; } = [];

    /// <summary>Groups of lights: each has one fader on the Fixtures page that moves all of them.</summary>
    public LightGroup[] Groups { get; init; } = [];

    /// <summary>
    /// The plan as the desk keeps it: every light checked, its letters in capitals, its color in
    /// small letters, its place to two decimals and its angle from 0 to 359; the kinds of lights
    /// that no light is, and the lights of groups that are not on the plan, left out.
    /// </summary>
    /// <returns>A plan the desk can keep.</returns>
    /// <exception cref="DeskException">Something in the plan cannot be used. The message says what.</exception>
    public StagePlan Checked()
    {
        if (Width < MinSize || Width > MaxSize || Depth < MinSize || Depth > MaxSize)
        {
            throw new DeskException(
                $"The stage can be {MinSize} to {MaxSize} squares wide and deep, not {Width} by {Depth}.");
        }

        if (!(PictureOpacity >= 0.1 && PictureOpacity <= 1))
            throw new DeskException($"The picture can show from 0.1 to 1, not {PictureOpacity.ToString(CultureInfo.InvariantCulture)}.");

        string over = (PictureOver ?? PictureOverStage).ToLowerInvariant();
        if (over is not (PictureOverStage or PictureOverPlan))
            throw new DeskException($"The picture can lie under the stage or under the whole plan, not \"{PictureOver}\".");

        FixtureType?[] types = Types ?? [];
        if (types.Length > MaxTypes)
            throw new DeskException($"The plan has {types.Length} kinds of lights from the library; there can be {MaxTypes}.");

        var typesById = new Dictionary<int, FixtureType>();
        for (int index = 0; index < types.Length; index++)
        {
            FixtureType type = (types[index] ?? throw new DeskException($"Kind {index + 1} of the plan's lights is missing."))
                .Checked($"Kind {index + 1} of the plan's lights");
            if (!typesById.TryAdd(type.Id, type))
                throw new DeskException($"Two kinds of the plan's lights have the same number, {type.Id}.");
        }

        StageLight?[] lights = Lights ?? throw new DeskException("The plan has no list of lights.");
        if (lights.Length > MaxLights)
            throw new DeskException($"The plan has {lights.Length} lights; there can be {MaxLights}.");

        var ids = new HashSet<int>();
        var kept = new StageLight[lights.Length];
        for (int index = 0; index < lights.Length; index++)
        {
            StageLight light = lights[index] ?? throw new DeskException($"Light {index + 1} of the plan is missing.");
            kept[index] = light.Checked(this, typesById, $"Light {index + 1} of the plan");
            if (!ids.Add(light.Id))
                throw new DeskException($"Two lights of the plan have the same number, {light.Id}.");
        }

        // Kinds of lights stay as long as a light is one: a kind no light is any more goes.
        var used = kept.Select(light => light.Type).OfType<int>().ToHashSet();
        FixtureType[] keptTypes = [.. typesById.Values.Where(type => used.Contains(type.Id))];

        return this with
        {
            Lights = kept,
            Types = keptTypes,
            Groups = CheckedGroups(ids),
            PictureOpacity = Math.Round(PictureOpacity, 2),
            PictureOver = over,
        };
    }

    /// <summary>True when the other plan has the same stage, picture, lights, kinds and groups, in the same order.</summary>
    /// <param name="other">The other plan.</param>
    public bool SameAs(StagePlan other)
    {
        return Width == other.Width && Depth == other.Depth
            && PictureId == other.PictureId && PictureOpacity == other.PictureOpacity && PictureOver == other.PictureOver
            && Lights.SequenceEqual(other.Lights)
            && Types.Length == other.Types.Length && Types.Zip(other.Types).All(pair => pair.First.SameAs(pair.Second))
            && Groups.Length == other.Groups.Length && Groups.Zip(other.Groups).All(pair => pair.First.SameAs(pair.Second));
    }

    // The groups, checked: a light that is not on the plan (any more) is left out of them.
    private LightGroup[] CheckedGroups(HashSet<int> lightIds)
    {
        LightGroup?[] groups = Groups ?? [];
        if (groups.Length > MaxGroups)
            throw new DeskException($"The plan has {groups.Length} groups of lights; there can be {MaxGroups}.");

        var ids = new HashSet<int>();
        var kept = new LightGroup[groups.Length];
        for (int index = 0; index < groups.Length; index++)
        {
            LightGroup group = groups[index] ?? throw new DeskException($"Group {index + 1} of the plan is missing.");
            string where = $"Group {index + 1} of the plan";
            if (group.Id < 1)
                throw new DeskException($"{where} needs a number of 1 or more.");
            if (!ids.Add(group.Id))
                throw new DeskException($"Two groups of the plan have the same number, {group.Id}.");

            int[] members = [.. (group.Lights ?? []).Where(lightIds.Contains).Distinct()];
            kept[index] = group with
            {
                Name = PlanText.Checked(group.Name, LightGroup.MaxNameLength, where, "a name"),
                Note = PlanText.CheckedNote(group.Note, where),
                Lights = members,
            };
        }

        return kept;
    }
}

/// <summary>A text of the plan: a name, the name of a channel, a label.</summary>
internal static class PlanText
{
    /// <summary>The text without spaces at its ends, checked: not too long, no tab, line break or the like.</summary>
    /// <param name="text">The text as it came; null for none.</param>
    /// <param name="maxLength">The most characters it can have.</param>
    /// <param name="where">Names what it belongs to in the message, like "Light 3 of the plan".</param>
    /// <param name="what">Says what it is in the message, like "a name".</param>
    /// <exception cref="DeskException">It is too long, or has a control character.</exception>
    public static string Checked(string? text, int maxLength, string where, string what)
    {
        string kept = (text ?? "").Trim();
        if (kept.Length > maxLength)
            throw new DeskException($"{where}: {what} can have at most {maxLength} characters.");
        if (kept.Any(character => char.IsControl(character) || char.GetUnicodeCategory(character) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator))
            throw new DeskException($"{where}: {what} cannot contain a tab, a line break or the like.");

        return kept;
    }

    /// <summary>A note under a key of the Fixtures page, with the rules of the notes under the faders.</summary>
    /// <param name="note">The note as it came; null for none.</param>
    /// <param name="where">Names what it belongs to in the message, like "Light 3 of the plan".</param>
    /// <exception cref="DeskException">It is too long, or has a tab, a line break or the like.</exception>
    public static string CheckedNote(string? note, string where)
    {
        try
        {
            return Note.Clean(note);
        }
        catch (DeskException e)
        {
            // "A note can ..." goes on after the colon as "a note can ..."
            throw new DeskException($"{where}: {char.ToLowerInvariant(e.Message[0])}{e.Message[1..]}");
        }
    }

    /// <summary>True for "#" and six digits of base 16, in small letters, like "#ff2000".</summary>
    public static bool IsColor(string text)
    {
        return text.Length == 7 && text[0] == '#' && text.Skip(1).All(digit => digit is >= '0' and <= '9' or >= 'a' and <= 'f');
    }
}

/// <summary>
/// A kind of light from the fixture library (the Open Fixture Library): a maker's model in one of
/// its modes, with what each of its channels does. The lights of the plan that are of this kind
/// have its <see cref="Codes"/> as their channels.
/// </summary>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
/// <param name="Id">Its number in the plan; a project file numbers the kinds from 1 in its order.</param>
/// <param name="Maker">The maker, like "American DJ".</param>
/// <param name="Model">The model, like "Inno Pocket Spot".</param>
/// <param name="Mode">The mode of the model, like "11-channel".</param>
/// <param name="Source">
/// Where it is in its library: in the Open Fixture Library maker and model as it names their files,
/// like "american-dj/inno-pocket-spot"; in the library of QLC+ its file after "qlcplus:", like
/// "qlcplus:American_DJ/American-DJ-Dotz-Bar-1.4.qxf".
/// </param>
/// <param name="Channels">Its channels, in their order from the address.</param>
public sealed record FixtureType(int Id, string Maker, string Model, string Mode, string Source, FixtureChannel[] Channels)
{
    /// <summary>The longest name of a maker, a model or a mode.</summary>
    public const int MaxTextLength = 80;

    /// <summary>The letters of its channels, one each, as the lights of this kind have them.</summary>
    public string Codes => string.Concat(Channels.Select(channel => channel.Code));

    /// <summary>The kind as the plan keeps it.</summary>
    /// <param name="where">Names it in the messages, like "Kind 2 of the plan's lights".</param>
    internal FixtureType Checked(string where)
    {
        if (Id < 1)
            throw new DeskException($"{where} needs a number of 1 or more.");

        // Letters, digits and the few signs the two libraries use in the names of their files.
        string source = (Source ?? "").Trim();
        if (source.Length > MaxTextLength * 2 || !source.All(character => char.IsAsciiLetterOrDigit(character) || "-/_.:+&(),".Contains(character)))
            throw new DeskException($"{where}: \"{Source}\" is not where a light is in the library.");

        FixtureChannel?[] channels = Channels ?? [];
        if (channels.Length < 1 || channels.Length > StageLight.MaxChannels)
            throw new DeskException($"{where} needs 1 to {StageLight.MaxChannels} channels.");

        var kept = new FixtureChannel[channels.Length];
        for (int index = 0; index < channels.Length; index++)
        {
            FixtureChannel channel = channels[index] ?? throw new DeskException($"{where}: channel {index + 1} is missing.");
            kept[index] = channel.Checked(index, channels.Length, $"{where}, channel {index + 1}");
        }

        return this with
        {
            Maker = PlanText.Checked(Maker, MaxTextLength, where, "the maker"),
            Model = PlanText.Checked(Model, MaxTextLength, where, "the model"),
            Mode = PlanText.Checked(Mode, MaxTextLength, where, "the mode"),
            Source = source,
            Channels = kept,
        };
    }

    /// <summary>True when the other kind is the same in every part.</summary>
    internal bool SameAs(FixtureType other)
    {
        return Id == other.Id && Maker == other.Maker && Model == other.Model && Mode == other.Mode && Source == other.Source
            && Channels.Length == other.Channels.Length && Channels.Zip(other.Channels).All(pair => pair.First.SameAs(pair.Second));
    }
}

/// <summary>A channel of a kind of light from the fixture library.</summary>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
/// <param name="Name">What the library calls it, like "Pan" or "Color Wheel".</param>
/// <param name="Code">
/// Its letter, as in <see cref="StageLight.Channels"/>: D dimmer, R G B W A U colors; P pan, T
/// tilt, F the fine part of another channel, S shutter or strobe, X anything else.
/// </param>
public sealed record FixtureChannel(string Name, string Code)
{
    /// <summary>The longest name of a channel and of a range.</summary>
    public const int MaxNameLength = 40;

    /// <summary>The most ranges a channel can have.</summary>
    public const int MaxRanges = 128;

    /// <summary>For the fine part of another channel (16 bits): the index of that channel, 0 for the first; null for any other.</summary>
    public int? Fine { get; init; }

    /// <summary>
    /// What the channel does at its values, for a channel that does several things: a slot of a
    /// wheel, a shutter that is open or closed, a mode. Empty for one that simply goes from 0 to full.
    /// </summary>
    public FixtureRange[] Ranges { get; init; } = [];

    internal FixtureChannel Checked(int index, int count, string where)
    {
        string code = (Code ?? "").Trim().ToUpperInvariant();
        if (code.Length != 1 || code[0] is < 'A' or > 'Z')
            throw new DeskException($"{where} needs one letter, like D or P.");
        if (Fine is int fine && (fine < 0 || fine >= count || fine == index))
            throw new DeskException($"{where} is the fine part of a channel the light does not have.");

        FixtureRange?[] ranges = Ranges ?? [];
        if (ranges.Length > MaxRanges)
            throw new DeskException($"{where} has {ranges.Length} ranges; there can be {MaxRanges}.");

        var kept = new FixtureRange[ranges.Length];
        for (int number = 0; number < ranges.Length; number++)
        {
            FixtureRange range = ranges[number] ?? throw new DeskException($"{where}: range {number + 1} is missing.");
            string what = $"{where}, range {number + 1}";
            if (range.From < 0 || range.To > Desk.MaxLevel || range.From > range.To)
                throw new DeskException($"{what} must lie within 0 to {Desk.MaxLevel}, not {range.From} to {range.To}.");

            string? color = string.IsNullOrEmpty(range.Color) ? null : range.Color.ToLowerInvariant();
            if (color is not null && !PlanText.IsColor(color))
                throw new DeskException($"{what}: \"{range.Color}\" is no color like #ff2000.");

            kept[number] = range with { Label = PlanText.Checked(range.Label, MaxNameLength, what, "a label"), Color = color };
        }

        return this with { Name = PlanText.Checked(Name, MaxNameLength, where, "a name"), Code = code, Ranges = kept };
    }

    internal bool SameAs(FixtureChannel other)
    {
        return Name == other.Name && Code == other.Code && Fine == other.Fine && Ranges.SequenceEqual(other.Ranges);
    }
}

/// <summary>What a channel of a kind of light does from one value to another, like "Red" on a color wheel.</summary>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
/// <param name="From">The first value, 0-255.</param>
/// <param name="To">The last value, 0-255.</param>
/// <param name="Label">What it does, like "Red", "Open" or "Strobe slow to fast".</param>
public sealed record FixtureRange(int From, int To, string Label)
{
    /// <summary>For a slot of a color wheel: its color, like "#ff0000"; null for anything else.</summary>
    public string? Color { get; init; }
}

/// <summary>A group of lights of the plan, with one fader on the Fixtures page that moves all of them.</summary>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
/// <param name="Id">Its number in the plan; a project file numbers the groups from 1 in its order.</param>
/// <param name="Name">What the owner calls it, up to <see cref="MaxNameLength"/> characters.</param>
/// <param name="Lights">The numbers (<see cref="StageLight.Id"/>) of its lights.</param>
public sealed record LightGroup(int Id, string Name, int[] Lights)
{
    /// <summary>The longest name a group can have.</summary>
    public const int MaxNameLength = 24;

    /// <summary>
    /// The note under its key on the Fixtures page, as the owner typed it there: two lines in one
    /// text, with the rules of the notes under the faders. Empty for none: then the page shows the
    /// name there, dimmed.
    /// </summary>
    public string Note { get; init; } = "";

    internal bool SameAs(LightGroup other)
    {
        return Id == other.Id && Name == other.Name && Note == other.Note && Lights.SequenceEqual(other.Lights);
    }
}

/// <summary>
/// The picture under a stage plan: a photo or a drawing, as a data URL of a JPEG, PNG or WebP
/// image ("data:image/jpeg;base64,..."). The UI makes it smaller before it sends it.
/// </summary>
internal static class StagePicture
{
    /// <summary>The most characters a picture can have: about 3 MB of image.</summary>
    public const int MaxLength = 4_000_000;

    private static readonly string[] Starts = ["data:image/jpeg;base64,", "data:image/png;base64,", "data:image/webp;base64,"];

    /// <summary>A picture from a UI or a file, checked; null or an empty text for none.</summary>
    /// <exception cref="DeskException">It is no JPEG, PNG or WebP picture as a data URL, or it is too big.</exception>
    public static string? Checked(string? picture)
    {
        if (string.IsNullOrEmpty(picture))
            return null;
        if (picture.Length > MaxLength)
            throw new DeskException($"The picture is too big: at most {MaxLength / 1_000_000} million characters.");

        string? start = Starts.FirstOrDefault(prefix => picture.StartsWith(prefix, StringComparison.Ordinal));
        if (start is null)
            throw new DeskException("The picture must be a JPEG, PNG or WebP image.");

        Span<byte> bytes = new byte[(picture.Length - start.Length) / 4 * 3 + 3];
        if (!Convert.TryFromBase64Chars(picture.AsSpan(start.Length), bytes, out _))
            throw new DeskException("The picture is damaged.");

        return picture;
    }

    /// <summary>A short name of a picture's content: the same picture always has the same name.</summary>
    public static string IdOf(string picture)
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(picture));
        return Convert.ToHexStringLower(hash, 0, 8);
    }
}

/// <summary>A light of the rig on the stage plan.</summary>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
/// <param name="Id">
/// Its number for the UI while the plan is edited: one no other light of the plan has. A project
/// file does not keep it; the lights are numbered from 1 when the file is read.
/// </param>
/// <param name="Name">What the owner calls it, up to <see cref="MaxNameLength"/> characters.</param>
/// <param name="Address">Its first channel, 1-512.</param>
/// <param name="Channels">
/// One letter for each of its channels, in their order from the address: D dimmer, R red, G green,
/// B blue, W white, A amber, U ultraviolet. Any other letter is a channel that gives no light,
/// such as a strobe or a mode; P is a pan and T a tilt. A light of several cells repeats its
/// color letters: RGBRGB is two cells. A light without color letters shines in
/// <paramref name="Color"/>, and one without color letters, dimmer, pan and tilt, such as a
/// hazer, gives no light at all. See <see cref="PlanRoles"/> for what the letters do to the output.
/// </param>
/// <param name="Color">The color of a light without color channels (a gel), like "#ff2000"; null: warm white.</param>
/// <param name="X">Where it hangs, across: squares from the left edge of the stage.</param>
/// <param name="Y">Where it hangs, from the back: squares from the back edge of the stage.</param>
/// <param name="Angle">How it is turned on the plan, in degrees clockwise, 0 to 359.</param>
public sealed record StageLight(int Id, string Name, int Address, string Channels, string? Color, double X, double Y, int Angle)
{
    /// <summary>The longest name a light can have: about what fits under it on the plan.</summary>
    public const int MaxNameLength = 24;

    /// <summary>The most channels a light can have: a pixel bar from the library can have hundreds, but all in one universe.</summary>
    public const int MaxChannels = Desk.UniverseSize;

    /// <summary>A light without a limit: it can go to full.</summary>
    public const int NoLimit = 100;

    /// <summary>The biggest a light can be, across and from back to front, in squares.</summary>
    public const int MaxSize = 32;

    /// <summary>A round light: a circle, or an ellipse when it is wider than deep.</summary>
    public const string Round = "round";

    /// <summary>A square light: a square, or an oblong over several squares of the grid.</summary>
    public const string Square = "square";

    /// <summary><see cref="Round"/> or <see cref="Square"/>.</summary>
    public string Shape { get; init; } = Round;

    /// <summary>
    /// How wide it is before it is turned, in whole squares (1 to 32). Its cells, if it has
    /// several, lie along its longer side; one cell fills it all.
    /// </summary>
    public int Width { get; init; } = 1;

    /// <summary>How deep it is before it is turned, in whole squares (1 to 32).</summary>
    public int Depth { get; init; } = 1;

    /// <summary>
    /// The kind of light from the fixture library it is, by its <see cref="FixtureType.Id"/> in the
    /// plan; null for a light whose channels were given as letters. Its channels are then the
    /// letters of that kind.
    /// </summary>
    public int? Type { get; init; }

    /// <summary>
    /// The most it gives, in per cent of full (1 to 100), like a hazer at most 60: its dimmers and
    /// the channels of a device stop there, the colors of a cell without a dimmer are dimmed
    /// together so that the color stays.
    /// </summary>
    public int Limit { get; init; } = NoLimit;

    /// <summary>
    /// The note under its key on the Fixtures page, as the owner typed it there: two lines in one
    /// text, with the rules of the notes under the faders. Empty for none: then the page shows the
    /// name there, dimmed.
    /// </summary>
    public string Note { get; init; } = "";

    /// <summary>The light as the desk keeps it, see <see cref="StagePlan.Checked"/>.</summary>
    /// <param name="plan">The plan it is on: its stage says how far the light can be from it.</param>
    /// <param name="types">The kinds of lights of the plan, by their number.</param>
    /// <param name="where">Names the light in the messages, like "Light 3 of the plan".</param>
    internal StageLight Checked(StagePlan plan, IReadOnlyDictionary<int, FixtureType> types, string where)
    {
        if (Id < 1)
            throw new DeskException($"{where} needs a number of 1 or more.");

        string name = PlanText.Checked(Name, MaxNameLength, where, "a name");

        string channels = (Channels ?? "").Trim().ToUpperInvariant();
        if (channels.Length < 1 || channels.Length > MaxChannels || !channels.All(letter => letter is >= 'A' and <= 'Z'))
            throw new DeskException($"{where} needs 1 to {MaxChannels} letters for its channels, like RGB.");
        if (Type is int type && (!types.TryGetValue(type, out FixtureType? kind) || kind.Codes != channels))
            throw new DeskException($"{where} is of a kind of light the plan does not have, or its channels do not match it.");
        if (Limit < 1 || Limit > NoLimit)
            throw new DeskException($"{where} can be limited to 1 to {NoLimit} per cent, not {Limit}.");
        if (Address < 1 || Address > Desk.MaxChannels)
        {
            throw new DeskException(
                $"{where} cannot have the address {Address}: the desk has {Desk.MaxUniverses} universes of {Desk.UniverseSize} channels at most.");
        }

        // A light is on one cable: all its channels in one universe.
        if ((Address - 1) / Desk.UniverseSize != (Address + channels.Length - 2) / Desk.UniverseSize)
        {
            int universeEnd = ((Address - 1) / Desk.UniverseSize + 1) * Desk.UniverseSize;
            throw new DeskException(
                $"{where} at address {Desk.ChannelLabel(Address)} with {channels.Length} channels reaches past channel " +
                $"{Desk.ChannelLabel(universeEnd)}, the end of its universe: all channels of a light must be in one universe.");
        }

        string? color = string.IsNullOrEmpty(Color) ? null : Color.ToLowerInvariant();
        if (color is not null && !PlanText.IsColor(color))
            throw new DeskException($"{where}: \"{Color}\" is no color like #ff2000.");

        string shape = (Shape ?? Round).ToLowerInvariant();
        if (shape is not (Round or Square))
            throw new DeskException($"{where} can be round or square, not \"{Shape}\".");
        if (Width < 1 || Width > MaxSize || Depth < 1 || Depth > MaxSize)
            throw new DeskException($"{where} can be 1 to {MaxSize} squares wide and deep, not {Width} by {Depth}.");

        if (!double.IsFinite(X) || !double.IsFinite(Y)
            || X < -StagePlan.SideRoom || X > plan.Width + StagePlan.SideRoom
            || Y < -StagePlan.BackRoom || Y > plan.Depth + StagePlan.FrontRoom)
        {
            throw new DeskException($"{where} is not on the plan.");
        }

        return this with
        {
            Name = name,
            Note = PlanText.CheckedNote(Note, where),
            Channels = channels,
            Color = color,
            Shape = shape,
            X = Math.Round(X, 2),
            Y = Math.Round(Y, 2),
            Angle = ((Angle % 360) + 360) % 360,
        };
    }
}
