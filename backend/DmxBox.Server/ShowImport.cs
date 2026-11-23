using System.Globalization;
using System.Text;

namespace DmxBox.Server;

/// <summary>
/// Makes a Lumenet project out of the show of a theatre console, from a file in the USITT
/// ASCII format (see <see cref="UsittAscii"/>), and says in a few sentences what became of it.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Every cue of the first cue list becomes a memory that holds its look, and a step of
/// the stack that brings that memory up. The step has the cue's number and the cue's up time.
/// Its down is the down time of the cue after it: the stack takes the old step down while the
/// new one comes up, so the cue that comes decides how fast the old one goes. A follow time
/// counts from the start of a cue, a wait from the moment the step is up: the wait is the follow
/// time less the up time. After the last step the stack goes round to the first.</item>
/// <item>Then the submasters, and then the groups, become memories, as long as memories are left.</item>
/// <item>The levels go through the file's patch onto the DMX addresses 1 to 512, which are the
/// channels of Lumenet. A file without a patch has channel 1 on address 1, and so on.</item>
/// <item>The note under a memory names the cue, submaster or group and has the start of its
/// label. The note under a channel names the console channel on that address, where that has
/// another number, or the dimmer's name if the file gives one.</item>
/// <item>The show of a SmartFade (see <see cref="UsittAscii"/>) keeps its shape: its memories
/// become memories, in the order of their numbers, and its stack the stack, each step bringing
/// up the memory it plays on the console; only a step with levels of its own gets a memory of
/// its own. Each of its sequences becomes a memory that holds a sequence. Its waits count from
/// the moment a step is up, as Lumenet's do, and 0 is none. Its two independents get the
/// independent keys 1 and 2, and a percentage becomes a DMX value as on the console, rounded
/// up.</item>
/// </list>
/// </remarks>
internal static class ShowImport
{
    /// <summary>The fade time of a cue for which the file names none.</summary>
    public const double DefaultFadeSeconds = 5;

    /// <summary>The name of the project when the file's name cannot be one.</summary>
    public const string FallbackName = "Imported show";

    // The stack stays on a step whose wait is 0; this is the shortest wait that goes on by itself.
    private const double ShortestWait = 0.1;

    // A note under a fader has two lines of up to 11 characters, with this character between them.
    private const int NoteLineLength = 11;
    private const char NoteLineSeparator = (char)0x2028;

    // How many numbers a sentence of the report names before it says "and 3 more".
    private const int NamedNumbers = 5;

    // A SmartFade has 48 channels. Channels 49 and 50 of its patch are its independents IND 1 and IND 2.
    private const int SmartFadeChannels = 48;

    /// <summary>Reads a show file and makes a project of it.</summary>
    /// <param name="fileName">The name of the file. It names the project, and the report names it.</param>
    /// <param name="text">What the file holds.</param>
    /// <returns>The project, its name, and the report for the user.</returns>
    /// <exception cref="DeskException">The file has nothing in it that could be imported.</exception>
    public static ImportedShow Read(string fileName, string text)
    {
        AsciiShow show = UsittAscii.Read(text);
        if (show.Cues.Count == 0 && show.Submasters.Count == 0 && show.Groups.Count == 0 && show.Sequences.Count == 0)
        {
            throw new DeskException(show.IsSmartFade
                ? $"\"{fileName}\" is the show of a SmartFade, but it has no stack, memories or sequences: there is nothing to import."
                : $"\"{fileName}\" has no cues, submasters or groups. Lumenet imports show files in the USITT ASCII " +
                  "format, the text that theatre consoles write.");
        }

        return new Builder(show, fileName).Build();
    }

    // The cue number as the console has it: "12.5", "7".
    private static string Number(decimal number)
    {
        return number.ToString("0.##########", CultureInfo.InvariantCulture);
    }

    // The note under a fader: the first line, and the second one if it has something.
    private static string NoteOf(string first, string second)
    {
        first = Fit(first);
        second = Fit(second);
        return Note.Clean(second.Length == 0 ? first : first + NoteLineSeparator + second);
    }

    // A name over both lines of a note, broken between two words where it can be.
    private static string TwoLines(string text)
    {
        string whole = Tidy(text);
        string first = Fit(whole);
        return NoteOf(first, whole[first.Length..]);
    }

    // A text made fit for one line of a note: single spaces, no control characters, and at most
    // 11 characters, cut between two words where there is a space far enough on.
    private static string Fit(string text)
    {
        string line = Tidy(text);
        if (line.Length <= NoteLineLength)
            return line;

        int space = line.LastIndexOf(' ', NoteLineLength);
        int cut = space >= NoteLineLength / 2 ? space : NoteLineLength;
        if (char.IsHighSurrogate(line[cut - 1]))
            cut--;   // a character beyond the first 65536 is two in a text: they stay together

        return line[..cut].TrimEnd();
    }

    // Single spaces between words, none at the ends; control characters and line breaks are spaces.
    private static string Tidy(string text)
    {
        var tidy = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (!char.IsWhiteSpace(character) && !char.IsControl(character))
                tidy.Append(character);
            else if (tidy.Length > 0 && tidy[^1] != ' ')
                tidy.Append(' ');
        }

        return tidy.ToString().TrimEnd();
    }

    // The step number a cue number is, if the stack can have it: up to 999.9, one digit after the dot.
    private static CueNumber? Fitting(decimal number)
    {
        decimal tenths = number * 10;
        if (number < 0 || tenths != decimal.Truncate(tenths) || tenths > (CueNumber.MaxMajor * 10) + CueNumber.MaxMinor)
            return null;

        int whole = (int)tenths;
        return new CueNumber(whole / 10, whole % 10);
    }

    private static CueNumber After(CueNumber number)
    {
        return number.Minor < CueNumber.MaxMinor
            ? new CueNumber(number.Major, number.Minor + 1)
            : new CueNumber(number.Major + 1, 0);
    }

    // "1 cue", "3 cues".
    private static string Counted(int count, string thing)
    {
        return Counted(count, thing, thing + "s");
    }

    // "1 memory", "3 memories".
    private static string Counted(int count, string thing, string things)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? thing : things)}");
    }

    // "a", "a and b", "a, b and c".
    private static string Join(IReadOnlyList<string> parts)
    {
        return parts.Count < 2 ? string.Concat(parts) : string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1];
    }

    // "line 12", "lines 12, 40 and 41", "lines 12, 40, 41, 50, 60 and 3 more".
    private static string Named(IReadOnlyCollection<int> numbers, string thing)
    {
        var shown = numbers.Take(NamedNumbers).Select(number => number.ToString(CultureInfo.InvariantCulture)).ToList();
        if (numbers.Count > shown.Count)
            shown.Add(string.Create(CultureInfo.InvariantCulture, $"{numbers.Count - shown.Count} more"));

        return (numbers.Count == 1 ? thing : thing + "s") + " " + Join(shown);
    }

    // Puts a show together, and notes on the way what the report has to tell.
    private sealed class Builder(AsciiShow show, string fileName)
    {
        private readonly byte[]?[] _looks = new byte[]?[Desk.MemoryCount];
        private readonly string[] _memoryNotes = Enumerable.Repeat("", Desk.MemoryCount).ToArray();
        private readonly string[] _channelNotes = Enumerable.Repeat("", Desk.MaxChannels).ToArray();

        // The universes the show needs, from the highest address it uses, and their channels.
        private int _universes = 1;
        private int _channelCount = Desk.UniverseSize;
        private int _highestAddress = 1;

        // For every DMX address: the console channel that drives it, and its share; null if none does.
        private readonly AsciiPatch?[] _addresses = new AsciiPatch?[Desk.MaxChannels];
        private readonly HashSet<int> _patchedChannels = [];

        // For the report.
        private readonly SortedSet<int> _lostChannels = [];
        private readonly HashSet<AsciiCue> _delayed = [];
        private readonly HashSet<AsciiCue> _untimed = [];
        private int _early;
        private int _links;
        private int _shortened;
        private int _renumbered;
        private bool _numberedInTurn;

        // The next free memory, counted from 0.
        private int _memory;

        // The sequences the memories hold, and the channels the keys of the independents are bound to.
        private readonly Sequence?[] _sequences = new Sequence?[Desk.MemoryCount];
        private readonly int[] _independents = Desk.DefaultIndependents();

        // A SmartFade turns a percentage into a DMX value by rounding up.
        private bool _ceiling;

        // On a SmartFade: its memories as the file has them, the Lumenet memory that each of them
        // got, and the memory of each step that has levels of its own.
        private readonly Dictionary<int, AsciiLook> _consoleMemories = [];
        private readonly Dictionary<int, int> _memoryOf = [];
        private readonly Dictionary<AsciiCue, int> _ownMemoryOf = [];

        // For the report of a SmartFade.
        private readonly List<(int Independent, int[] Addresses)> _boundIndependents = [];
        private int _lostSteps;
        private int _manual;
        private int _cutStackSteps;
        private int _cutSequenceSteps;

        public ImportedShow Build()
        {
            _ceiling = show.IsSmartFade;
            Patch();
            ProjectStep[] stack;
            List<string> report = show.IsSmartFade ? SmartFade(out stack) : Usitt(out stack);
            report.Add("Nothing is saved yet: Save keeps the project.");

            var content = new ProjectContent(_looks, _channelNotes[.._channelCount], _memoryNotes, _independents)
            {
                Universes = _universes,
                Sequences = _sequences,
                Stack = stack,
            };
            return new ImportedShow(ProjectName.Suggest(Path.GetFileNameWithoutExtension(fileName), FallbackName), content, report.ToArray());
        }

        // The show of any console but a SmartFade: its cues, then its submasters, then its groups
        // become memories, and the cues the steps of the stack.
        private List<string> Usitt(out ProjectStep[] stack)
        {
            // The first cue list of the file. A file that gives no list has all cues in list 1.
            int list = show.Cues.Count > 0 ? ListOf(show.Cues[0]) : 1;
            List<AsciiCue> listed = show.Cues.Where(cue => ListOf(cue) == list).OrderBy(cue => cue.Number).ToList();
            bool pages = show.Submasters.Any(submaster => submaster.Page is > 1);

            var cues = Store(listed, cue => "Cue " + Number(cue.Number));
            var submasters = Store(
                show.Submasters.OrderBy(ListOf).ThenBy(submaster => submaster.Number).ToList(),
                submaster => "Sub " + Number(submaster.Number) + (pages ? " p" + ListOf(submaster) : ""));
            var groups = Store(
                show.Groups.OrderBy(ListOf).ThenBy(group => group.Number).ToList(),
                group => "Group " + Number(group.Number));
            stack = Stack(cues);

            var report = new List<string>();
            Tell(report, cues.Select(entry => entry.Memory).ToList(), submasters.Count, groups.Count);
            TellWhatIsLeftOut(
                report,
                list,
                otherCues: show.Cues.Count - listed.Count,
                left: (listed.Count - cues.Count, show.Submasters.Count - submasters.Count, show.Groups.Count - groups.Count));
            TellTimes(report, cues.Count(entry => entry.Look.Fades.Count > 1));
            TellNumbers(report, listed.Take(cues.Count).Count(cue => Fitting(cue.Number) is null));
            TellChannels(report);
            return report;
        }

        // The show of a SmartFade keeps its shape: its memories become memories, in the order of
        // their numbers; the steps of its stack and of its sequences bring them up. A memory that
        // a step plays but the file does not hold is empty on the console, and so it is here. A step
        // with levels of its own (a "state" of the stack, a single channel) gets a memory of its
        // own, and each sequence a memory that holds it.
        private List<string> SmartFade(out ProjectStep[] stack)
        {
            // As many steps as the stack and a sequence can have; a SmartFade has fewer anyway.
            List<AsciiCue> cues = show.Cues.OrderBy(cue => cue.Number).Take(Desk.MaxStackSteps).ToList();
            _cutStackSteps = show.Cues.Count - cues.Count;
            List<AsciiSequence> sequences = show.Sequences.Where(sequence => sequence.Steps.Count > 0).OrderBy(sequence => sequence.Index).ToList();
            var stepsOf = sequences.ToDictionary(sequence => sequence, sequence => sequence.Steps.Take(Sequence.MaxSteps).ToList());
            _cutSequenceSteps = sequences.Sum(sequence => sequence.Steps.Count - stepsOf[sequence].Count);
            List<AsciiCue> steps = [.. cues, .. sequences.SelectMany(sequence => stepsOf[sequence])];

            foreach (AsciiLook memory in show.Groups)
                _consoleMemories[(int)memory.Number] = memory;
            var memories = Store(show.Groups.OrderBy(memory => memory.Number).ToList(), memory => "Mem " + Number(memory.Number));
            foreach ((AsciiLook memory, int found) in memories)
                _memoryOf[(int)memory.Number] = found;

            var empty = steps
                .Where(step => step.Levels.Count == 0 && step.Memory is { } memory && !_consoleMemories.ContainsKey(memory))
                .Select(step => step.Memory!.Value)
                .Distinct()
                .Order()
                .ToList();
            int emptyFound = 0;
            foreach (int memory in empty)
            {
                if (Reserve(NoteOf("Mem " + memory, "")) is not { } found)
                    break;

                _memoryOf[memory] = found;
                emptyFound++;
            }

            var stackCues = cues.ToHashSet();
            var own = Store(
                steps.Where(step => !Plays(step)).ToList(),
                step => stackCues.Contains(step) ? "Cue " + Number(step.Number) : StepName(step));
            foreach ((AsciiCue step, int found) in own)
                _ownMemoryOf[step] = found;

            int sequencesFound = 0;
            foreach (AsciiSequence sequence in sequences)
            {
                List<(AsciiCue Cue, int Memory)> targets = Targets(stepsOf[sequence]);
                if (targets.Count == 0 || Reserve(NoteOf("Seq " + (sequence.Index + 1), sequence.BestLabel())) is not { } found)
                    continue;

                if (sequence.Mode == AsciiSequence.Manual)
                    _manual++;
                _sequences[found - 1] = new Sequence(Loop: sequence.Mode != AsciiSequence.OneShot, Timed(targets));
                sequencesFound++;
            }

            List<(AsciiCue Cue, int Memory)> played = Targets(cues);
            CueNumber[] numbers = StepNumbers(played.Select(entry => entry.Cue.Number).ToList());
            stack = Timed(played).Select((step, index) => new ProjectStep(numbers[index], step)).ToArray();

            var report = new List<string>();
            TellSmartFade(report, memories.Count, emptyFound, own.Count, sequencesFound);
            TellNumbers(report, played.Count(entry => Fitting(entry.Cue.Number) is null));
            TellChannels(report);
            return report;
        }

        // Whether a SmartFade step brings up a memory of the console that has a memory here: one
        // with the same levels, or an empty one for a step without levels.
        private bool Plays(AsciiCue step)
        {
            if (step.Memory is not { } memory || !_memoryOf.ContainsKey(memory))
                return false;

            return _consoleMemories.TryGetValue(memory, out AsciiLook? look) ? SameLevels(step, look) : step.Levels.Count == 0;
        }

        private static bool SameLevels(AsciiLook one, AsciiLook other)
        {
            return one.Levels.Count == other.Levels.Count
                && one.Levels.All(level => other.Levels.TryGetValue(level.Key, out double same) && same == level.Value);
        }

        // The first line of the note under the memory of a sequence step with levels of its own:
        // the channel, for a step of a single channel, as a SmartFade has them.
        private static string StepName(AsciiCue step)
        {
            return step.Levels.Count == 1 ? "Ch " + step.Levels.Keys.Single() : "Step " + Number(step.Number);
        }

        // The memory that each step brings up. A step that found none is left out and counted.
        private List<(AsciiCue Cue, int Memory)> Targets(IEnumerable<AsciiCue> steps)
        {
            var targets = new List<(AsciiCue, int)>();
            foreach (AsciiCue step in steps)
            {
                if (Plays(step))
                    targets.Add((step, _memoryOf[step.Memory!.Value]));
                else if (_ownMemoryOf.TryGetValue(step, out int memory))
                    targets.Add((step, memory));
                else
                    _lostSteps++;
            }

            return targets;
        }

        // The steps of a SmartFade's stack or sequence. Its wait counts from the moment the step
        // is up, as in Lumenet; 0 is none, which in the stack waits for Play and in a sequence goes
        // on at once. The down time is that of the step after, as in the stack of any console.
        private Step[] Timed(List<(AsciiCue Cue, int Memory)> targets)
        {
            var steps = new Step[targets.Count];
            for (int index = 0; index < targets.Count; index++)
            {
                (AsciiCue cue, int memory) = targets[index];
                AsciiCue next = targets[(index + 1) % targets.Count].Cue;
                double wait = cue.Follow is { } follow && follow > 0 ? follow : 0;
                var step = new Step(
                    Step.Memory,
                    memory,
                    Desk.MaxLevel,
                    Shortened(Seconds(cue, up: true)),
                    Shortened(wait),
                    Shortened(Seconds(next, up: false)));
                steps[index] = step.Checked();
            }

            return steps;
        }

        // The next free memory, with a note but without a look; null when none is left.
        private int? Reserve(string note)
        {
            if (_memory == Desk.MemoryCount)
                return null;

            _memoryNotes[_memory] = note;
            return ++_memory;
        }

        // A file that gives no page or list means the first.
        private static int ListOf(AsciiLook look)
        {
            return look.Page ?? 1;
        }

        // Which console channel drives each address, and the notes under the channel faders.
        private void Patch()
        {
            if (show.Patch.Count == 0)
            {
                for (int index = 0; index < Desk.MaxChannels; index++)
                    _addresses[index] = new AsciiPatch(index + 1, 1);
            }

            foreach ((int dimmer, AsciiPatch patch) in show.Patch)
                _addresses[dimmer - 1] = patch;   // the reader keeps dimmers 1 to 4096 only

            // As many universes as the highest address needs: of the patch, or without one, of the
            // highest channel any look of the show gives a level (channel and address are one then).
            int highest = show.Patch.Count > 0 ? show.Patch.Keys.Max() : HighestChannel();
            _highestAddress = Math.Min(highest, Desk.MaxChannels);
            _universes = Math.Clamp((highest + Desk.UniverseSize - 1) / Desk.UniverseSize, 1, Desk.MaxUniverses);
            _channelCount = _universes * Desk.UniverseSize;

            for (int index = 0; index < _channelCount; index++)
            {
                AsciiPatch? patch = _addresses[index];
                if (patch is { } driven)
                    _patchedChannels.Add(driven.Channel);

                if (show.DimmerNames.TryGetValue(index + 1, out string? name))
                {
                    _channelNotes[index] = TwoLines(name);
                }
                else if (show.IsSmartFade && patch is { } smartFade)
                {
                    _channelNotes[index] = SmartFadeChannelNote(index + 1, smartFade.Channel);
                }
                else if (patch is { } other && (other.Channel != index + 1 || other.Share < 1))
                {
                    string share = other.Share < 1 ? string.Create(CultureInfo.InvariantCulture, $"{Math.Round(other.Share * 100)}%") : "";
                    _channelNotes[index] = NoteOf(string.Create(CultureInfo.InvariantCulture, $"Ch {other.Channel}"), share);
                }
            }

            if (show.IsSmartFade)
                BindIndependents();
        }

        // The highest channel that a cue, a submaster or a group of the show gives a level; 1 for none.
        private int HighestChannel()
        {
            IEnumerable<AsciiLook> looks = show.Cues.Cast<AsciiLook>()
                .Concat(show.Sequences.SelectMany(sequence => sequence.Steps))
                .Concat(show.Submasters)
                .Concat(show.Groups);
            return looks.SelectMany(look => look.Levels.Keys).DefaultIfEmpty(1).Max();
        }

        // The note under an address that a SmartFade's channel drives: the channel, where it has
        // another number, or which independent it is, and the label it has on the console.
        private string SmartFadeChannelNote(int address, int channel)
        {
            int independent = channel - SmartFadeChannels;
            AsciiLook? labelled;
            string name;
            if (independent is 1 or 2)
            {
                show.IndependentLabels.TryGetValue(independent, out labelled);
                name = string.Create(CultureInfo.InvariantCulture, $"IND {independent}");
            }
            else
            {
                show.ChannelLabels.TryGetValue(channel, out labelled);
                name = channel == address ? "" : string.Create(CultureInfo.InvariantCulture, $"Ch {channel}");
            }

            string label = labelled?.BestLabel() ?? "";
            return name.Length > 0 ? NoteOf(name, label) : TwoLines(label);
        }

        // A SmartFade's independents IND 1 and IND 2 switch the DMX addresses that the patch gives
        // to channel 49 and 50. Lumenet's independent keys 1 and 2 get the first address of each:
        // a key switches one channel.
        private void BindIndependents()
        {
            for (int independent = 1; independent <= 2; independent++)
            {
                int[] addresses = Enumerable.Range(1, _channelCount)
                    .Where(address => _addresses[address - 1]?.Channel == SmartFadeChannels + independent)
                    .ToArray();
                if (addresses.Length == 0)
                    continue;

                _independents[independent - 1] = addresses[0];
                _boundIndependents.Add((independent, addresses));
            }
        }

        // Puts looks into the memories that are left, in this order, and gives back those that
        // found one, each with its memory. "name" gives the first line of the note.
        private List<(T Look, int Memory)> Store<T>(IReadOnlyList<T> looks, Func<T, string> name)
            where T : AsciiLook
        {
            var stored = new List<(T, int)>();
            foreach (T look in looks)
            {
                if (_memory == Desk.MemoryCount)
                    break;

                _looks[_memory] = LookOf(look);
                _memoryNotes[_memory] = NoteOf(name(look), look.BestLabel());
                stored.Add((look, _memory + 1));
                _memory++;
            }

            return stored;
        }

        // The levels of a look on the show's channels, through the patch.
        private byte[] LookOf(AsciiLook look)
        {
            var levels = new byte[_channelCount];
            for (int index = 0; index < levels.Length; index++)
            {
                if (_addresses[index] is { } patch && look.Levels.TryGetValue(patch.Channel, out double level))
                    levels[index] = Dmx(level * patch.Share);
            }

            foreach ((int channel, double level) in look.Levels)
            {
                if (level > 0 && !_patchedChannels.Contains(channel))
                    _lostChannels.Add(channel);
            }

            return levels;
        }

        // A level from 0 to 1 as a DMX value. A SmartFade keeps its levels as DMX values and
        // writes them as percentages; rounding up gives the console's value back (17 % is its 44,
        // not 43). Rounded to a millionth first, so that 20 % stays 51 whatever binary fractions do.
        private byte Dmx(double fraction)
        {
            double value = Math.Clamp(fraction, 0, 1) * Desk.MaxLevel;
            return (byte)(_ceiling ? Math.Ceiling(Math.Round(value, 6)) : Math.Round(value, MidpointRounding.AwayFromZero));
        }

        // A step for every cue that found a memory.
        private ProjectStep[] Stack(List<(AsciiCue Look, int Memory)> cues)
        {
            if (cues.Count == 0)
                return [];

            CueNumber[] numbers = StepNumbers(cues.Select(entry => entry.Look.Number).ToList());
            var steps = new ProjectStep[cues.Count];
            for (int index = 0; index < cues.Count; index++)
            {
                (AsciiCue cue, int memory) = cues[index];
                AsciiCue next = cues[(index + 1) % cues.Count].Look;
                double up = Seconds(cue, up: true);
                double down = Seconds(next, up: false);
                double wait = Wait(cue, next, up, last: index == cues.Count - 1);
                var step = new Step(Step.Memory, memory, Desk.MaxLevel, Shortened(up), Shortened(wait), Shortened(down));
                steps[index] = new ProjectStep(numbers[index], step.Checked());
            }

            return steps;
        }

        // How long a cue takes in one direction, its delay included: of its parts, the one that
        // ends last. A part that names only the other direction has that time for this one too, as
        // the standard says. A cue without any time takes DefaultFadeSeconds.
        private double Seconds(AsciiCue cue, bool up)
        {
            double? longest = null;
            foreach (AsciiFade fade in cue.Fades)
            {
                (double? time, double? delay) = up ? (fade.Up, fade.UpDelay) : (fade.Down, fade.DownDelay);
                if (time is null)
                    (time, delay) = (up ? fade.Down : fade.Up, null);
                if (time is not { } seconds)
                    continue;

                if (delay > 0)
                    _delayed.Add(cue);
                longest = Math.Max(longest ?? 0, (delay ?? 0) + seconds);
            }

            if (longest is null)
                _untimed.Add(cue);
            return longest ?? DefaultFadeSeconds;
        }

        // The wait of a cue's step: none for a cue that waits for Go. A link to the cue that comes
        // next anyway is what the stack does by itself; any other link cannot be followed. The last
        // cue follows on to the first only if it links there: otherwise the show ends with it.
        private double Wait(AsciiCue cue, AsciiCue next, double up, bool last)
        {
            bool linksOn = cue.Link == next.Number;
            if (cue.Link is not null && !linksOn)
                _links++;
            if (cue.Follow is not { } follow || (last && !linksOn))
                return 0;

            if (follow < up)
                _early++;
            return Math.Max(follow - up, ShortestWait);
        }

        private double Shortened(double seconds)
        {
            if (seconds <= Step.MaxSeconds)
                return seconds;

            _shortened++;
            return Step.MaxSeconds;
        }

        // The numbers of the steps. A cue keeps its own number where the stack can have it. One
        // that does not fit gets the number after the step before, if that stays below the next
        // cue that keeps its own; if it does not, all steps are numbered 1.0, 2.0 and on. The note
        // under each memory keeps the cue's own number in any case.
        private CueNumber[] StepNumbers(List<decimal> cues)
        {
            CueNumber?[] fitting = cues.Select(Fitting).ToArray();

            // For each cue, the first number after it that the stack can have as it is.
            var nextFitting = new CueNumber?[cues.Count];
            CueNumber? following = null;
            for (int index = cues.Count - 1; index >= 0; index--)
            {
                nextFitting[index] = following;
                following = fitting[index] ?? following;
            }

            var numbers = new CueNumber[cues.Count];
            for (int index = 0; index < cues.Count; index++)
            {
                CueNumber? own = fitting[index];
                CueNumber? before = index > 0 ? numbers[index - 1] : null;
                if (own is { } fits && (before is null || fits > before.Value))
                {
                    numbers[index] = fits;
                    continue;
                }

                CueNumber candidate = before is { } previous ? After(previous) : new CueNumber(0, 1);
                if (candidate.Major > CueNumber.MaxMajor || (nextFitting[index] is { } next && candidate >= next))
                {
                    _numberedInTurn = true;
                    return Enumerable.Range(1, cues.Count).Select(number => new CueNumber(number, 0)).ToArray();
                }

                numbers[index] = candidate;
                _renumbered++;
            }

            return numbers;
        }

        // What the file held and where it went.
        private void Tell(List<string> report, List<int> cueMemories, int submasters, int groups)
        {
            var kinds = new List<string>();
            if (show.Cues.Count > 0)
                kinds.Add(Counted(show.Cues.Count, "cue"));
            if (show.Submasters.Count > 0)
                kinds.Add(Counted(show.Submasters.Count, "submaster"));
            if (show.Groups.Count > 0)
                kinds.Add(Counted(show.Groups.Count, "group"));
            report.Add($"{fileName}{WrittenBy()}: {Join(kinds)}.");

            if (Places([(cueMemories.Count, "the cue", "the cues"), (submasters, "the submaster", "the submasters"), (groups, "the group", "the groups")]) is { } places)
                report.Add(places);
            report.Add(cueMemories.Count > 0
                ? "The stack has a step for each cue, with the cue's number and times."
                : "The file has no cues, so the stack is empty.");
        }

        // What a SmartFade's file held and where it went.
        private void TellSmartFade(List<string> report, int memories, int empty, int own, int sequences)
        {
            int withSteps = show.Sequences.Count(sequence => sequence.Steps.Count > 0);
            var kinds = new List<string>();
            if (show.Cues.Count > 0)
                kinds.Add("a stack of " + Counted(show.Cues.Count, "step"));
            if (show.Groups.Count > 0)
                kinds.Add(Counted(show.Groups.Count, "memory", "memories"));
            if (withSteps > 0)
                kinds.Add(Counted(withSteps, "sequence"));
            report.Add($"{fileName}{WrittenBy()}: {Join(kinds)}.");

            if (Places(
                [
                    (memories, "the console's memory", "the console's memories"),
                    (empty, "an empty memory that a step plays", "empty memories that steps play"),
                    (own, "a step with levels of its own", "steps with levels of their own"),
                    (sequences, "the sequence", "the sequences"),
                ]) is { } places)
            {
                report.Add(places);
            }

            if (memories > 0)
                report.Add("The note under a memory gives its number on the console, as \"Mem 25\", and its label.");
            report.Add(show.Cues.Count > 0
                ? "The stack has the console's steps, with their numbers and times; each brings up the memory it plays on the console. " +
                  "A wait of 0 waits for Play, as on the console; a longer one starts the next step that long after this one is up."
                : "The file has no stack, so the stack is empty.");
            if (_manual > 0)
            {
                report.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Sequences in Manual mode: {_manual}. A sequence here cannot be stepped by hand, so they run in a loop."));
            }

            if (_boundIndependents.Count > 0)
            {
                bool one = _boundIndependents.Count == 1;
                string keys = Join(_boundIndependents.Select(bound => bound.Independent.ToString(CultureInfo.InvariantCulture)).ToList());
                string where = Join(_boundIndependents
                    .Select(bound => string.Create(CultureInfo.InvariantCulture, $"IND {bound.Independent} on DMX {bound.Addresses[0]}"))
                    .ToList());
                string sentence = $"Independent {(one ? "key" : "keys")} {keys} {(one ? "switches" : "switch")} the console's " +
                                  $"{(one ? "independent" : "independents")}: {where}.";
                foreach ((int independent, int[] addresses) in _boundIndependents.Where(bound => bound.Addresses.Length > 1))
                {
                    string more = Join(addresses.Skip(1).Select(address => address.ToString(CultureInfo.InvariantCulture)).ToList());
                    sentence += string.Create(
                        CultureInfo.InvariantCulture,
                        $" IND {independent} also drives DMX {more}, which the key does not switch: a key has one channel.");
                }

                report.Add(sentence);
            }

            var left = new List<string>();
            if (show.Groups.Count > memories)
                left.Add(Counted(show.Groups.Count - memories, "memory", "memories"));
            if (withSteps > sequences)
                left.Add(Counted(withSteps - sequences, "sequence"));
            if (_lostSteps > 0)
                left.Add(Counted(_lostSteps, "step"));
            if (left.Count > 0)
                report.Add(string.Create(CultureInfo.InvariantCulture, $"Left out, because Lumenet has {Desk.MemoryCount} memories: {Join(left)}."));
            if (_cutStackSteps > 0)
            {
                report.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Stack steps beyond {Desk.MaxStackSteps}: {_cutStackSteps}. They are left out: the stack has at most {Desk.MaxStackSteps}."));
            }

            if (_cutSequenceSteps > 0)
            {
                report.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Sequence steps beyond {Sequence.MaxSteps}: {_cutSequenceSteps}. They are left out: a sequence has at most {Sequence.MaxSteps}."));
            }

            TellTimes(report, inParts: 0);
        }

        // ", written by ETC Eos", or nothing if the file does not say.
        private string WrittenBy()
        {
            string console = string.Join(" ", new[] { show.Manufacturer, show.Console }.Where(name => name is not null));
            return console.Length > 0 ? ", written by " + console : "";
        }

        // "Memories 1 to 14 hold the cues, 15 to 16 the submasters and 17 the group.": where each
        // kind of thing went, in the order in which the memories were given out. Null if nothing went anywhere.
        private static string? Places(IEnumerable<(int Count, string One, string Many)> kinds)
        {
            int first = 1;
            var places = new List<string>();
            foreach ((int count, string one, string many) in kinds)
            {
                if (count == 0)
                    continue;

                string memories = count == 1
                    ? first.ToString(CultureInfo.InvariantCulture)
                    : string.Create(CultureInfo.InvariantCulture, $"{first} to {first + count - 1}");
                string what = count == 1 ? one : many;
                places.Add(places.Count == 0
                    ? $"{(count == 1 ? "Memory" : "Memories")} {memories} {(count == 1 ? "holds" : "hold")} {what}"
                    : $"{memories} {what}");
                first += count;
            }

            return places.Count == 0 ? null : Join(places) + ".";
        }

        private void TellWhatIsLeftOut(List<string> report, int list, int otherCues, (int Cues, int Submasters, int Groups) left)
        {
            if (otherCues > 0)
            {
                report.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cues in other cue lists: {otherCues}. Only cue list {list} is imported, the first in the file."));
            }

            var full = new List<string>();
            if (left.Cues > 0)
                full.Add(Counted(left.Cues, "cue"));
            if (left.Submasters > 0)
                full.Add(Counted(left.Submasters, "submaster"));
            if (left.Groups > 0)
                full.Add(Counted(left.Groups, "group"));
            if (full.Count > 0)
                report.Add(string.Create(CultureInfo.InvariantCulture, $"Left out, because Lumenet has {Desk.MemoryCount} memories: {Join(full)}."));

            if (show.Repeated > 0)
                report.Add(string.Create(CultureInfo.InvariantCulture, $"Numbers that come twice in the file: {show.Repeated}. The first of each is used."));
        }

        private void TellTimes(List<string> report, int inParts)
        {
            if (inParts > 0)
                report.Add(string.Create(CultureInfo.InvariantCulture, $"Cues in parts: {inParts}. Each became one fade, as long as its slowest part."));
            if (_delayed.Count > 0)
            {
                report.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cues with a delay: {_delayed.Count}. The delay is added to the fade time, so the fade ends when it would on the console."));
            }

            if (_untimed.Count > 0)
                report.Add(string.Create(CultureInfo.InvariantCulture, $"Cues without a fade time: {_untimed.Count}. They fade in {DefaultFadeSeconds} seconds."));
            if (_early > 0)
            {
                report.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Follows that start the next cue before this one is up: {_early}. Here the next cue starts once this one is up."));
            }

            if (_links > 0)
            {
                report.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Links to other cues: {_links}. They are left out: the stack plays the cues in the order of their numbers, and after the last comes the first."));
            }

            if (_shortened > 0)
            {
                report.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Times above {Step.MaxSeconds} seconds: {_shortened}. They are cut to {Step.MaxSeconds} seconds, the longest a step can have."));
            }
        }

        private void TellNumbers(List<string> report, int notFitting)
        {
            if (notFitting == 0 && !_numberedInTurn)
                return;

            string what = string.Create(
                CultureInfo.InvariantCulture,
                $"Cue numbers the stack cannot have: {notFitting}. Its numbers go up to {CueNumber.MaxMajor}.{CueNumber.MaxMinor}, with one digit after the dot.");
            report.Add(_numberedInTurn
                ? what + " So the steps are numbered 1.0, 2.0 and on; the note under each memory keeps the cue's own number."
                : what + " Those steps got the next free number; the note under the memory keeps the cue's own number.");
        }

        private void TellChannels(List<string> report)
        {
            if (_universes > 1)
            {
                report.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The show uses addresses up to {_highestAddress}, so the project has {_universes} universes: address 513 of the show is channel 1 (U2), and so on."));
            }

            if (_lostChannels.Count > 0)
            {
                report.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Channels without a DMX address from 1 to {_channelCount}: {_lostChannels.Count} ({Named(_lostChannels, "channel")}). Their levels are left out."));
            }

            if (show.DimmersAbove > 0)
            {
                report.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Dimmers above {Desk.MaxChannels} in the patch: {show.DimmersAbove}. They are left out: the desk has {Desk.MaxUniverses} universes at most."));
            }

            if (show.Skipped > 0)
            {
                report.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Cues, memories, steps and labels beyond {UsittAscii.MaxKept} of one kind: {show.Skipped}. They are skipped."));
            }

            if (show.MakerLines > 0)
            {
                report.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Lines for one maker's consoles: {show.MakerLines}. They hold such things as moving lights, effects and macros, and are skipped."));
            }

            if (show.UnreadLines.Count > 0)
            {
                report.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Lines that could not be read: {show.UnreadLines.Count} ({Named(show.UnreadLines, "line")}). They are skipped."));
            }
        }
    }
}

/// <summary>A project made from the show file of another console, see <see cref="ShowImport"/>.</summary>
/// <param name="Name">The name for the project: that of the file, without its extension.</param>
/// <param name="Content">The project.</param>
/// <param name="Report">Sentences for the user: what the file held, where it went, and what was left out.</param>
internal sealed record ImportedShow(string Name, ProjectContent Content, IReadOnlyList<string> Report);
