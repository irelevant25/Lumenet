namespace DmxBox.Server;

/// <summary>
/// What a show file in the USITT ASCII format holds, as <see cref="UsittAscii"/> read it:
/// cues, submasters, groups and the patch, with numbers and levels as the console wrote them.
/// </summary>
internal sealed class AsciiShow
{
    /// <summary>The maker of the console that wrote the file, if the file names one, like "ETC".</summary>
    public string? Manufacturer { get; set; }

    /// <summary>The console that wrote the file, if the file names it, like "Eos".</summary>
    public string? Console { get; set; }

    /// <summary>
    /// True for a file of an ETC (AVAB) SmartFade, which has a dialect of its own: see
    /// <see cref="UsittAscii"/>.
    /// </summary>
    public bool IsSmartFade => Console?.StartsWith("SMARTFADE", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>The cues, in the order of the file. On a SmartFade, the steps of its stack.</summary>
    public List<AsciiCue> Cues { get; } = [];

    /// <summary>The sequences of a SmartFade, the runs of steps on its sequence faders, in the order of the file.</summary>
    public List<AsciiSequence> Sequences { get; } = [];

    /// <summary>The labels of a SmartFade's channels: channel and label.</summary>
    public SortedDictionary<int, AsciiLook> ChannelLabels { get; } = [];

    /// <summary>The labels of a SmartFade's two independents: 1 or 2, and label.</summary>
    public SortedDictionary<int, AsciiLook> IndependentLabels { get; } = [];

    /// <summary>The submasters, in the order of the file.</summary>
    public List<AsciiLook> Submasters { get; } = [];

    /// <summary>The groups, in the order of the file.</summary>
    public List<AsciiLook> Groups { get; } = [];

    /// <summary>
    /// The soft patch: for every dimmer from 1 to 512 the file patches, the channel it belongs
    /// to. A dimmer is a DMX address. Empty when the file has no patch: then channel 1 is dimmer
    /// 1, and so on.
    /// </summary>
    public SortedDictionary<int, AsciiPatch> Patch { get; } = [];

    /// <summary>How often the patch gives a dimmer above 512, an address that one universe of DMX does not have.</summary>
    public int DimmersAbove { get; set; }

    /// <summary>The names of dimmers from 1 to 512, from Strand's $$DIMMERNAMES: dimmer and name.</summary>
    public SortedDictionary<int, string> DimmerNames { get; } = [];

    /// <summary>
    /// How many cues, submasters, groups, sequences, sequence steps and labels were not kept
    /// because the file has more than <see cref="UsittAscii.MaxKept"/> of that kind.
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// How many lines belong to one maker's consoles (those that begin with "$", and the lines
    /// of blocks such as "$MACRO 8"). They are skipped.
    /// </summary>
    public int MakerLines { get; set; }

    /// <summary>
    /// How many cues, submasters and groups had a number that came before in the same list or
    /// page. Only the first of each counts.
    /// </summary>
    public int Repeated { get; set; }

    /// <summary>The numbers of the lines that could not be read, the first one first.</summary>
    public List<int> UnreadLines { get; } = [];
}

/// <summary>A cue, a submaster or a group of a show file: the levels of its channels, and its label.</summary>
/// <param name="number">Its number, like 12.5.</param>
/// <param name="page">The page written after the number; for a cue, its cue list. Null if there is none.</param>
internal class AsciiLook(decimal number, int? page)
{
    /// <summary>Its number, like 12.5.</summary>
    public decimal Number { get; } = number;

    /// <summary>The page written after the number; for a cue, its cue list. Null if there is none.</summary>
    public int? Page { get; } = page;

    /// <summary>The console channel and its level, from 0 to 1, of every channel the file lists for it.</summary>
    public Dictionary<int, double> Levels { get; } = [];

    /// <summary>Its label (TEXT), or "" if the file gives none.</summary>
    public string Label { get; set; } = "";

    /// <summary>The label with letters beyond ASCII, from ETC's $$UTEXT, if the file has one.</summary>
    public string? UnicodeLabel { get; set; }

    /// <summary>
    /// The label as a SmartFade shows it, in lines of up to six letters ($$TEXT 1 to 3): line
    /// number and text.
    /// </summary>
    public SortedDictionary<int, string> LabelLines { get; } = [];

    /// <summary>
    /// The best label the file gives: the SmartFade's lines with a space between them, else
    /// $$UTEXT, else TEXT; "" if there is none.
    /// </summary>
    public string BestLabel()
    {
        string lines = string.Join(' ', LabelLines.Values.Select(line => line.Trim()).Where(line => line.Length > 0));
        if (lines.Length > 0)
            return lines;

        return string.IsNullOrWhiteSpace(UnicodeLabel) ? Label : UnicodeLabel;
    }
}

/// <summary>
/// A sequence of a SmartFade: steps on one of its sequence faders, run in a loop, once, or by
/// hand.
/// </summary>
/// <param name="index">
/// Which sequence fader it is on, from 0: four on each fader page, so 0 to 3 on page 1, 4 to 7
/// on page 2, and on.
/// </param>
/// <param name="mode">How it runs: 0 in a loop, 1 once (One Shot), 2 a step each time its bump button is pressed.</param>
internal sealed class AsciiSequence(int index, int mode) : AsciiLook(index, null)
{
    /// <summary>The run mode that loops.</summary>
    public const int Loop = 0;

    /// <summary>The run mode that goes through the steps once and stays on the last.</summary>
    public const int OneShot = 1;

    /// <summary>The run mode that waits for the bump button before each step.</summary>
    public const int Manual = 2;

    /// <summary>Which sequence fader it is on, from 0.</summary>
    public int Index { get; } = index;

    /// <summary>How it runs: <see cref="Loop"/>, <see cref="OneShot"/> or <see cref="Manual"/>.</summary>
    public int Mode { get; } = mode;

    /// <summary>The steps, in the order of the file, which is the order in which they run.</summary>
    public List<AsciiCue> Steps { get; } = [];
}

/// <summary>A cue of a show file: a look, with its fades and what comes after it.</summary>
/// <param name="number">The cue number, like 12.5.</param>
/// <param name="list">The cue list written after the number, or null.</param>
internal sealed class AsciiCue(decimal number, int? list) : AsciiLook(number, list)
{
    /// <summary>The times of the fade: the cue's own, then one for every PART of a cue in parts.</summary>
    public List<AsciiFade> Fades { get; } = [new()];

    /// <summary>The fade that UP and DOWN lines set: that of the last PART, or the cue's own.</summary>
    public AsciiFade Fade => Fades[^1];

    /// <summary>
    /// FOLLOWON: seconds from the start of this cue to the start of the next one, which then
    /// starts by itself. Null for a cue that waits for Go. A SmartFade writes its wait time
    /// here, which counts from the moment the step is up, and 0 for none.
    /// </summary>
    public double? Follow { get; set; }

    /// <summary>LINK: the cue that comes after this one, if not the next by number. Null if there is none.</summary>
    public decimal? Link { get; set; }

    /// <summary>
    /// On a SmartFade, the memory the step plays ($$MEMORY), from 1; null for a step with levels
    /// of its own. Its CHAN lines repeat the memory's levels.
    /// </summary>
    public int? Memory { get; set; }
}

/// <summary>The times of one fade, in seconds; null where the file gives none.</summary>
internal sealed class AsciiFade
{
    /// <summary>The time for the channels that go up.</summary>
    public double? Up { get; set; }

    /// <summary>How long the channels that go up wait before they start.</summary>
    public double? UpDelay { get; set; }

    /// <summary>The time for the channels that go down.</summary>
    public double? Down { get; set; }

    /// <summary>How long the channels that go down wait before they start.</summary>
    public double? DownDelay { get; set; }
}

/// <summary>Where a dimmer belongs in the patch.</summary>
/// <param name="Channel">The console channel that drives the dimmer.</param>
/// <param name="Share">The dimmer's share of the channel's level, from 0 to 1 (the proportional patch).</param>
internal readonly record struct AsciiPatch(int Channel, double Share);
