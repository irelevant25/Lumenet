using System.Globalization;
using System.Text.RegularExpressions;

namespace DmxBox.Server;

/// <summary>
/// Reads a show file in the USITT ASCII format: the plain text in which theatre consoles (ETC
/// Eos, Ion and Express, the Strand consoles and others) hand a show to another console. Such
/// files mostly end in ".asc".
/// </summary>
/// <remarks>
/// <para>
/// The rules are those of the standard, "ASCII Text Representation for Lighting Console Data",
/// version 3.0 (USITT), as Strand's "Guide to ASCII Cues" (1992) explains them:
/// </para>
/// <list type="bullet">
/// <item>One instruction per line: a keyword, then numbers or a text. Blank lines are fine.</item>
/// <item>"!" makes the rest of the line a comment.</item>
/// <item>Upper and lower case are the same.</item>
/// <item>Space, tab, comma and the signs / &lt; &gt; = @ separate the parts of a line, any of
/// them as good as any other, and as many of them as wanted.</item>
/// <item>CUE, GROUP and SUB begin a block; CHAN, UP, DOWN, TEXT and the like after them belong
/// to that block. A cue lists every channel that is up in it: a channel it does not list is at
/// zero.</item>
/// <item>Keywords that begin with "$" belong to one maker's consoles. They are skipped, except
/// $$UTEXT (ETC's labels with letters beyond ASCII) and $$DIMMERNAMES (Strand's names of
/// dimmers). One with a single "$", like "$MACRO 8", begins a block of its own, whose lines are
/// skipped with it.</item>
/// <item>Nothing after ENDDATA counts.</item>
/// </list>
/// <para>
/// A level is a percentage, "FL" for full, or a hexadecimal value from H00 to HFF, as ETC Eos
/// writes it when asked to. A time is in seconds, as minutes:seconds, or as
/// hours:minutes:seconds, with tenths if wanted.
/// </para>
/// <para>
/// The reader does not give up on a line it cannot read: it notes the line's number in
/// <see cref="AsciiShow.UnreadLines"/> and goes on, so that one odd line does not lose a show.
/// </para>
/// <para>
/// An ETC (AVAB) SmartFade, "CONSOLE SMARTFADE", writes a dialect of its own. The reader knows
/// it from the console's manual and from files that SmartFades wrote, checked against the
/// console:
/// </para>
/// <list type="bullet">
/// <item>Its memories are GROUP blocks, numbered 1 to 288: 24 on each of 12 fader pages.</item>
/// <item>"$SEQUENCE 48" begins its stack, whose steps are CUE blocks. "$SEQUENCE n mode" with n
/// from 0 to 47 begins a sequence, whose steps are "$CUE" blocks in the order they run. A step
/// plays the memory that "$$MEMORY page fader" names (both counted from 0), and repeats its
/// levels in CHAN lines; a step without one has levels of its own.</item>
/// <item>A label is up to three lines of six letters, "$$TEXT 1" to "$$TEXT 3". "$CHANNEL n",
/// "$INDEPENDENT n" and "$PAGE n" only hold such labels.</item>
/// <item>A PATCH line has the DMX address first, then the channel: "PATCH 1 97/49/100". Channel
/// 0 is no channel; channels 49 and 50 are the two independents.</item>
/// <item>FOLLOWON is the step's wait, 0 for none.</item>
/// </list>
/// </remarks>
internal static partial class UsittAscii
{
    // Everything that separates the parts of a line.
    private static readonly char[] Separators = [' ', '\t', ',', '/', '<', '>', '=', '@'];

    // The sequence fader number of a SmartFade's stack: "$SEQUENCE 48".
    private const int SmartFadeStack = 48;

    // Memories on each fader page of a SmartFade.
    private const int SmartFadePageMemories = 24;

    // A file that starts with a byte order mark has it as its first character.
    private const char ByteOrderMark = (char)0xFEFF;

    /// <summary>Reads a show file.</summary>
    /// <param name="text">What the file holds.</param>
    /// <returns>What the file says about cues, submasters, groups and the patch.</returns>
    public static AsciiShow Read(string text)
    {
        var reader = new Reader();
        using var lines = new StringReader(text.TrimStart(ByteOrderMark));
        int number = 0;
        while (lines.ReadLine() is { } line)
        {
            number++;
            if (!reader.Take(line, number))
                break;
        }

        return reader.Show;
    }

    // A percentage, FL for full, or a hexadecimal value: H80, or 80H.
    private static bool TryLevel(string field, out double level)
    {
        level = 0;
        string upper = field.ToUpperInvariant();
        if (upper is "FL" or "FF" or "FULL")
        {
            level = 1;
            return true;
        }

        if (upper.Length > 1 && (upper[0] == 'H' || upper[^1] == 'H'))
        {
            string digits = upper[0] == 'H' ? upper[1..] : upper[..^1];
            if (!int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int hex))
                return false;

            level = Math.Min(hex, Desk.MaxLevel) / (double)Desk.MaxLevel;
            return true;
        }

        if (!double.TryParse(field, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double percent))
            return false;

        level = Math.Min(percent, 100) / 100;   // above full is full
        return true;
    }

    // Seconds, "1:15" for minutes and seconds, or "1:02:03" with hours in front. Only the last
    // part may have a fraction: "2.5", ".2", "1:07.5".
    private static bool TryTime(string field, out double seconds)
    {
        seconds = 0;
        string[] parts = field.Split(':');
        if (parts.Length > 3)
            return false;

        for (int index = 0; index < parts.Length; index++)
        {
            NumberStyles style = index == parts.Length - 1 ? NumberStyles.AllowDecimalPoint : NumberStyles.None;
            if (!double.TryParse(parts[index], style, CultureInfo.InvariantCulture, out double part))
                return false;

            seconds = seconds * 60 + part;
        }

        return true;
    }

    // A whole number from 1 up, as channels, dimmers and pages are.
    private static bool TryWhole(string field, out int number)
    {
        return TryCount(field, out number) && number >= 1;
    }

    // A whole number from 0 up: in a patch, 0 is no channel or no dimmer; a SmartFade counts its
    // fader pages and faders from 0.
    private static bool TryCount(string field, out int number)
    {
        return int.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    // A cue or group number: "12", "12.5", and the more digits after the dot that some consoles allow.
    private static bool TryNumber(string field, out decimal number)
    {
        return decimal.TryParse(field, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);
    }

    // ETC writes the letters of a label that are not ASCII as "&#8647;", with the decimal number
    // of the character.
    private static string Unicode(string text)
    {
        return CharacterReference().Replace(text, match =>
        {
            string digits = match.Groups[1].Value;
            bool hex = digits.StartsWith('x') || digits.StartsWith('X');
            bool valid = int.TryParse(
                hex ? digits[1..] : digits,
                hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int code);
            return valid && code is > 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF)
                ? char.ConvertFromUtf32(code)
                : "";
        });
    }

    [GeneratedRegex("&#([xX][0-9a-fA-F]{1,6}|[0-9]{1,7});")]
    private static partial Regex CharacterReference();

    // Goes through the lines one by one and remembers which block they belong to.
    private sealed class Reader
    {
        // The cue, submaster or group that the lines belong to; null outside of one.
        private AsciiLook? _block;

        // The lines belong to a block that is not taken over: one of a maker's own (its lines
        // count as such), or a cue whose number could not be read or came before (its lines are
        // dropped without a word, the cue line itself has been counted).
        private Skip _skip;

        // The SmartFade sequence whose steps the "$CUE" lines are; null outside of one.
        private AsciiSequence? _sequence;

        private enum Skip
        {
            Nothing,
            MakerBlock,
            DroppedBlock,
        }

        public AsciiShow Show { get; } = new();

        /// <summary>Takes one line.</summary>
        /// <returns>False after ENDDATA: the rest of the file does not count.</returns>
        public bool Take(string line, int number)
        {
            int comment = line.IndexOf('!');
            if (comment >= 0)
                line = line[..comment];

            string rest = line.TrimStart(Separators);
            if (rest.Length == 0)
                return true;

            int end = rest.IndexOfAny(Separators);
            string keyword = (end < 0 ? rest : rest[..end]).ToUpperInvariant();
            string tail = end < 0 ? "" : rest[end..];
            string[] fields = tail.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            string text = tail.TrimStart(Separators).TrimEnd();

            switch (keyword)
            {
                case "ENDDATA":
                    return false;
                case "IDENT":
                    EndBlock();
                    break;
                case "MANUFACTURER":
                    EndBlock();
                    Show.Manufacturer = NullIfEmpty(text);
                    break;
                case "CONSOLE":
                    EndBlock();
                    Show.Console = NullIfEmpty(text);
                    break;
                case "SET":
                    EndBlock();
                    Set(fields, text);
                    break;
                case "CLEAR":
                    EndBlock();
                    Clear(fields);
                    break;
                case "PATCH":
                    EndBlock();
                    Patch(fields, number);
                    break;
                case "CUE":
                    StartCue(fields, number);
                    break;
                case "GROUP":
                    StartGroup(fields, number);
                    break;
                case "SUB":
                    StartSubmaster(fields, number);
                    break;
                default:
                    InBlock(keyword, fields, text, number);
                    break;
            }

            return true;
        }

        private void EndBlock()
        {
            _block = null;
            _skip = Skip.Nothing;
            _sequence = null;
        }

        // "SET MANUFACTURER Strand" and "SET CONSOLE LightPalette90" name the console, as the
        // keywords of their own do. The other settings (number of channels, the default patch,
        // a title) change nothing here.
        private void Set(string[] fields, string text)
        {
            if (fields.Length < 2)
                return;

            string value = text[(text.IndexOf(fields[0], StringComparison.Ordinal) + fields[0].Length)..].TrimStart(Separators);
            switch (fields[0].ToUpperInvariant())
            {
                case "MANUFACTURER":
                    Show.Manufacturer = NullIfEmpty(value);
                    break;
                case "CONSOLE":
                    Show.Console = NullIfEmpty(value);
                    break;
            }
        }

        // CLEAR empties the console before the file is loaded. Here that only matters for what
        // the file itself has said before the CLEAR.
        private void Clear(string[] fields)
        {
            foreach (string field in fields)
            {
                string what = field.ToUpperInvariant();
                if (what is "CUES" or "ALL")
                    Show.Cues.Clear();
                if (what is "SUBS" or "ALL")
                    Show.Submasters.Clear();
                if (what is "GROUPS" or "ALL")
                    Show.Groups.Clear();
                if (what is "PATCH" or "ALL")
                    Show.Patch.Clear();
                if (what is "ALL")
                {
                    Show.Sequences.Clear();
                    Show.ChannelLabels.Clear();
                    Show.IndependentLabels.Clear();
                }
            }
        }

        // "PATCH 1 5<8@100 5<9@80": on patch page 1, dimmer 8 belongs to channel 5 at 100 %,
        // dimmer 9 to channel 5 at 80 %. The standard has the page and the share in every line.
        // A SmartFade has the dimmer first, "PATCH 1 8/5/100", and lists all 512 of them, those
        // that no channel drives with channel 0. A 0 on either side patches nothing.
        private void Patch(string[] fields, int number)
        {
            if (fields.Length < 4 || (fields.Length - 1) % 3 != 0)
            {
                Show.UnreadLines.Add(number);
                return;
            }

            bool dimmerFirst = Show.IsSmartFade;
            bool whole = true;
            for (int index = 1; index + 2 < fields.Length; index += 3)
            {
                if (TryCount(fields[index], out int first)
                    && TryCount(fields[index + 1], out int second)
                    && TryLevel(fields[index + 2], out double share))
                {
                    (int channel, int dimmer) = dimmerFirst ? (second, first) : (first, second);
                    if (channel > 0 && dimmer > 0)
                        Show.Patch[dimmer] = new AsciiPatch(channel, share);
                }
                else
                {
                    whole = false;
                }
            }

            if (!whole)
                Show.UnreadLines.Add(number);
        }

        // "CUE 2.3", or "CUE 2.3 1" with the cue list (page) after the number.
        private void StartCue(string[] fields, int number)
        {
            EndBlock();
            if (fields.Length == 0 || !TryNumber(fields[0], out decimal cueNumber))
            {
                Drop(number, unread: true);
                return;
            }

            int? list = PageOf(fields);
            if (Show.Cues.Any(cue => cue.Number == cueNumber && SamePage(cue.Page, list)))
            {
                Drop(number, unread: false);
                return;
            }

            var started = new AsciiCue(cueNumber, list);
            Show.Cues.Add(started);
            _block = started;
        }

        private void StartGroup(string[] fields, int number)
        {
            EndBlock();
            if (fields.Length == 0 || !TryNumber(fields[0], out decimal groupNumber))
            {
                Drop(number, unread: true);
                return;
            }

            StartLook(Show.Groups, new AsciiLook(groupNumber, PageOf(fields)), number);
        }

        // "SUB 1 5": submaster 1 on page 5.
        private void StartSubmaster(string[] fields, int number)
        {
            EndBlock();
            if (fields.Length == 0 || !TryWhole(fields[0], out int submaster))
            {
                Drop(number, unread: true);
                return;
            }

            StartLook(Show.Submasters, new AsciiLook(submaster, PageOf(fields)), number);
        }

        private void StartLook(List<AsciiLook> looks, AsciiLook look, int number)
        {
            if (looks.Any(other => other.Number == look.Number && SamePage(other.Page, look.Page)))
            {
                Drop(number, unread: false);
                return;
            }

            looks.Add(look);
            _block = look;
        }

        // A cue, group or submaster that is not taken over: its lines up to the next block go too.
        private void Drop(int number, bool unread)
        {
            if (unread)
                Show.UnreadLines.Add(number);
            else
                Show.Repeated++;

            _skip = Skip.DroppedBlock;
        }

        // A line inside a block, or one of a maker's keywords.
        private void InBlock(string keyword, string[] fields, string text, int number)
        {
            if (Show.IsSmartFade && SmartFadeLine(keyword, fields, text, number))
                return;

            if (keyword == "$$DIMMERNAMES")
            {
                DimmerNames(fields, number);
                return;
            }

            if (keyword == "$$UTEXT" && _block is not null)
            {
                _block.UnicodeLabel = Unicode(text);
                return;
            }

            if (keyword.StartsWith('$'))
            {
                Show.MakerLines++;
                if (!keyword.StartsWith("$$", StringComparison.Ordinal))
                {
                    // "$MACRO 8", "$EFFECT 2": a block of that maker's own begins
                    _block = null;
                    _skip = Skip.MakerBlock;
                }

                return;
            }

            switch (_skip)
            {
                case Skip.MakerBlock:
                    Show.MakerLines++;
                    return;
                case Skip.DroppedBlock:
                    return;
            }

            switch (_block)
            {
                case AsciiCue cue:
                    CueLine(cue, keyword, fields, text, number);
                    break;
                case { } look:
                    LookLine(look, keyword, fields, text, number);
                    break;
                default:
                    Show.UnreadLines.Add(number);   // CHAN and the like outside of a cue, group or submaster
                    break;
            }
        }

        // The keywords of a SmartFade's own (see the remarks of the class). False for any other line.
        private bool SmartFadeLine(string keyword, string[] fields, string text, int number)
        {
            switch (keyword)
            {
                case "$SOFTWAREVERSION":
                    EndBlock();
                    return true;
                case "$SEQUENCE":
                    StartSequence(fields, number);
                    return true;
                case "$CUE":
                    StartSequenceStep(fields, number);
                    return true;
                case "$CHANNEL":
                    StartLabel(Show.ChannelLabels, fields, number);
                    return true;
                case "$INDEPENDENT":
                    StartLabel(Show.IndependentLabels, fields, number);
                    return true;
                case "$PAGE":
                    StartLabel(labels: null, fields, number);   // the name of a fader page: Lumenet has none
                    return true;
                case "$$MEMORY":
                    MemoryOfStep(fields, number);
                    return true;
                case "$$CUEPARAMS":
                    return true;   // the kind of step, which the step's other lines tell as well
                case "$$TEXT":
                    LabelLine(fields, text, number);
                    return true;
                default:
                    return false;
            }
        }

        // "$SEQUENCE 0 1 0 256 256": the sequence on sequence fader 0, which runs once (One Shot).
        // The three numbers after the mode are not known. "$SEQUENCE 48 ..." begins the stack,
        // whose steps are CUE lines.
        private void StartSequence(string[] fields, int number)
        {
            EndBlock();
            if (fields.Length < 2 || !TryCount(fields[0], out int index) || !TryCount(fields[1], out int mode))
            {
                Drop(number, unread: true);
                return;
            }

            if (index == SmartFadeStack)
            {
                _block = new AsciiLook(index, null);   // the stack's label: Lumenet's stack has none
                return;
            }

            var sequence = new AsciiSequence(index, mode);
            Show.Sequences.Add(sequence);
            _sequence = sequence;
            _block = sequence;
        }

        // "$CUE 25.0": a step of the sequence above. A step is numbered after its memory, so two
        // steps can have the same number.
        private void StartSequenceStep(string[] fields, int number)
        {
            AsciiSequence? sequence = _sequence;
            EndBlock();
            if (sequence is null || fields.Length == 0 || !TryNumber(fields[0], out decimal stepNumber))
            {
                Drop(number, unread: true);
                return;
            }

            var step = new AsciiCue(stepNumber, null);
            sequence.Steps.Add(step);
            _sequence = sequence;
            _block = step;
        }

        // "$CHANNEL 7", "$INDEPENDENT 1 0 255 0 1", "$PAGE 3": the lines after it are the label of
        // that channel, independent or fader page.
        private void StartLabel(SortedDictionary<int, AsciiLook>? labels, string[] fields, int number)
        {
            EndBlock();
            if (fields.Length == 0 || !TryWhole(fields[0], out int which))
            {
                Drop(number, unread: true);
                return;
            }

            var label = new AsciiLook(which, null);
            if (labels is not null)
                labels[which] = label;
            _block = label;
        }

        // "$$MEMORY 1 4": the step plays the memory on fader page 2, fader 5, both counted from 0
        // here: memory 29.
        private void MemoryOfStep(string[] fields, int number)
        {
            if (_block is not AsciiCue step)
                return;   // in a block that is not taken over

            if (fields.Length < 2 || !TryCount(fields[0], out int page) || !TryCount(fields[1], out int fader))
            {
                Show.UnreadLines.Add(number);
                return;
            }

            step.Memory = (page * SmartFadePageMemories) + fader + 1;
        }

        // "$$TEXT 2 DOMA": line 2 of the label of the block above.
        private void LabelLine(string[] fields, string text, int number)
        {
            if (_block is null)
                return;   // in a block that is not taken over

            if (fields.Length == 0 || !TryWhole(fields[0], out int line))
            {
                Show.UnreadLines.Add(number);
                return;
            }

            // The label is all of the line after its number, separators and all, but for the one
            // that parts it from the number.
            string label = text[fields[0].Length..];
            if (label.Length > 0 && Separators.Contains(label[0]))
                label = label[1..];
            _block.LabelLines[line] = Unicode(label);
        }

        private void CueLine(AsciiCue cue, string keyword, string[] fields, string text, int number)
        {
            switch (keyword)
            {
                case "CHAN":
                    Levels(cue, fields, number);
                    break;
                case "TEXT" or "LABEL":
                    cue.Label = text;
                    break;
                case "UP":
                    if (TryFade(fields, out double up, out double? upDelay))
                        (cue.Fade.Up, cue.Fade.UpDelay) = (up, upDelay);
                    else
                        Show.UnreadLines.Add(number);
                    break;
                case "DOWN":
                    if (TryFade(fields, out double down, out double? downDelay))
                        (cue.Fade.Down, cue.Fade.DownDelay) = (down, downDelay);
                    else
                        Show.UnreadLines.Add(number);
                    break;
                case "FOLLOWON" or "FOLLOW":
                    if (fields.Length > 0 && TryTime(fields[0], out double follow))
                        cue.Follow = follow;
                    else
                        Show.UnreadLines.Add(number);
                    break;
                case "LINK":
                    if (fields.Length > 0 && TryNumber(fields[0], out decimal link))
                        cue.Link = link;
                    else
                        Show.UnreadLines.Add(number);
                    break;
                case "PART":
                    cue.Fades.Add(new AsciiFade());
                    break;
                default:
                    Show.UnreadLines.Add(number);
                    break;
            }
        }

        // A line in a submaster or a group.
        private void LookLine(AsciiLook look, string keyword, string[] fields, string text, int number)
        {
            switch (keyword)
            {
                case "CHAN":
                    Levels(look, fields, number);
                    break;
                case "TEXT" or "LABEL":
                    look.Label = text;
                    break;
                case "UP" or "DOWN":
                    break;   // the times of a submaster's bump button: a memory fader has none
                default:
                    Show.UnreadLines.Add(number);
                    break;
            }
        }

        // "CHAN 1 50 2 FL 3 H80": channel and level, again and again.
        private void Levels(AsciiLook look, string[] fields, int number)
        {
            bool whole = fields.Length > 0 && fields.Length % 2 == 0;
            for (int index = 0; index + 1 < fields.Length; index += 2)
            {
                if (TryWhole(fields[index], out int channel) && TryLevel(fields[index + 1], out double level))
                    look.Levels[channel] = level;
                else
                    whole = false;
            }

            if (!whole)
                Show.UnreadLines.Add(number);
        }

        // "$$DIMMERNAMES 1 FRT 2 BACK": dimmer and name, again and again.
        private void DimmerNames(string[] fields, int number)
        {
            bool whole = fields.Length > 0 && fields.Length % 2 == 0;
            for (int index = 0; index + 1 < fields.Length; index += 2)
            {
                if (TryWhole(fields[index], out int dimmer))
                    Show.DimmerNames[dimmer] = fields[index + 1];
                else
                    whole = false;
            }

            if (!whole)
                Show.UnreadLines.Add(number);
        }

        // "UP 5", or "UP 120 20" for a fade of 2 minutes that waits 20 seconds before it starts.
        private static bool TryFade(string[] fields, out double time, out double? delay)
        {
            time = 0;
            delay = null;
            if (fields.Length == 0 || !TryTime(fields[0], out time))
                return false;
            if (fields.Length == 1)
                return true;
            if (!TryTime(fields[1], out double wait))
                return false;

            delay = wait;
            return true;
        }

        // The page after the number of a cue, group or submaster; for a cue, its list.
        private static int? PageOf(string[] fields)
        {
            return fields.Length > 1 && TryWhole(fields[1], out int page) ? page : null;
        }

        // A file that gives no page means the first one.
        private static bool SamePage(int? one, int? other)
        {
            return (one ?? 1) == (other ?? 1);
        }

        private static string? NullIfEmpty(string text)
        {
            return text.Length == 0 ? null : text;
        }
    }
}
