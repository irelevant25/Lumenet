using System.Globalization;

namespace DmxBox.Server;

/// <summary>
/// The stage plan of a project: the stage seen from above, a grid of squares, and the lights of
/// the rig on it, each where it hangs. The UI draws every light in the colour its channels give
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

    /// <summary>The most lights a plan can have: one for every channel.</summary>
    public const int MaxLights = Desk.ChannelCount;

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
    /// The plan as the desk keeps it: every light checked, its letters in capitals, its colour in
    /// small letters, its place to two decimals and its angle from 0 to 359.
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

        StageLight?[] lights = Lights ?? throw new DeskException("The plan has no list of lights.");
        if (lights.Length > MaxLights)
            throw new DeskException($"The plan has {lights.Length} lights; there can be {MaxLights}.");

        var ids = new HashSet<int>();
        var kept = new StageLight[lights.Length];
        for (int index = 0; index < lights.Length; index++)
        {
            StageLight light = lights[index] ?? throw new DeskException($"Light {index + 1} of the plan is missing.");
            kept[index] = light.Checked(this, $"Light {index + 1} of the plan");
            if (!ids.Add(light.Id))
                throw new DeskException($"Two lights of the plan have the same number, {light.Id}.");
        }

        return this with { Lights = kept, PictureOpacity = Math.Round(PictureOpacity, 2), PictureOver = over };
    }

    /// <summary>True when the other plan has the same stage, picture and lights, in the same order.</summary>
    /// <param name="other">The other plan.</param>
    public bool SameAs(StagePlan other)
    {
        return Width == other.Width && Depth == other.Depth
            && PictureId == other.PictureId && PictureOpacity == other.PictureOpacity && PictureOver == other.PictureOver
            && Lights.SequenceEqual(other.Lights);
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
/// such as a strobe or a mode. A light of several cells repeats its colour letters: RGBRGB is two
/// cells. A light without colour letters shines in <paramref name="Colour"/>, and one without
/// colour letters and without a dimmer, such as a hazer, gives no light at all.
/// </param>
/// <param name="Colour">The colour of a light without colour channels (a gel), like "#ff2000"; null: warm white.</param>
/// <param name="X">Where it hangs, across: squares from the left edge of the stage.</param>
/// <param name="Y">Where it hangs, from the back: squares from the back edge of the stage.</param>
/// <param name="Angle">How it is turned on the plan, in degrees clockwise, 0 to 359.</param>
public sealed record StageLight(int Id, string Name, int Address, string Channels, string? Colour, double X, double Y, int Angle)
{
    /// <summary>The longest name a light can have: about what fits under it on the plan.</summary>
    public const int MaxNameLength = 24;

    /// <summary>The most channels a light can have.</summary>
    public const int MaxChannels = 64;

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

    /// <summary>The light as the desk keeps it, see <see cref="StagePlan.Checked"/>.</summary>
    /// <param name="plan">The plan it is on: its stage says how far the light can be from it.</param>
    /// <param name="where">Names the light in the messages, like "Light 3 of the plan".</param>
    internal StageLight Checked(StagePlan plan, string where)
    {
        if (Id < 1)
            throw new DeskException($"{where} needs a number of 1 or more.");

        string name = (Name ?? "").Trim();
        if (name.Length > MaxNameLength)
            throw new DeskException($"{where}: a name can have at most {MaxNameLength} characters.");
        if (name.Any(character => char.IsControl(character) || char.GetUnicodeCategory(character) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator))
            throw new DeskException($"{where}: a name cannot contain a tab, a line break or the like.");

        string channels = (Channels ?? "").Trim().ToUpperInvariant();
        if (channels.Length < 1 || channels.Length > MaxChannels || !channels.All(letter => letter is >= 'A' and <= 'Z'))
            throw new DeskException($"{where} needs 1 to {MaxChannels} letters for its channels, like RGB.");
        if (Address < 1 || Address + channels.Length - 1 > Desk.ChannelCount)
        {
            string count = channels.Length == 1 ? "1 channel" : $"{channels.Length} channels";
            throw new DeskException($"{where} at address {Address} with {count} does not fit into the channels 1 to {Desk.ChannelCount}.");
        }

        string? colour = string.IsNullOrEmpty(Colour) ? null : Colour.ToLowerInvariant();
        if (colour is not null && !IsColour(colour))
            throw new DeskException($"{where}: \"{Colour}\" is no colour like #ff2000.");

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
            Channels = channels,
            Colour = colour,
            Shape = shape,
            X = Math.Round(X, 2),
            Y = Math.Round(Y, 2),
            Angle = ((Angle % 360) + 360) % 360,
        };
    }

    // "#" and six digits of base 16, in small letters.
    private static bool IsColour(string text)
    {
        return text.Length == 7 && text[0] == '#' && text.Skip(1).All(digit => digit is >= '0' and <= '9' or >= 'a' and <= 'f');
    }
}
