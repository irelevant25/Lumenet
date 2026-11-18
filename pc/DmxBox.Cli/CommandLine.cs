using System.Globalization;

namespace DmxBox.Cli;

/// <summary>
/// The command line, split into plain words ("fade", "1", "255") and options ("--time 2").
/// A command takes the parts it understands; <see cref="ExpectEnd"/> then rejects what is left.
/// </summary>
/// <remarks>
/// Numbers are read with the invariant culture: "2.5" is two and a half everywhere,
/// also on a PC whose language writes "2,5".
/// </remarks>
internal sealed class CommandLine
{
    // Every option takes one value. A short name is stored under its long name.
    private static readonly Dictionary<string, string> LongNames = new()
    {
        ["-H"] = "--host",
        ["-t"] = "--time",
        ["-u"] = "--universe",
        ["--host"] = "--host",
        ["--port"] = "--port",
        ["--artnet-port"] = "--artnet-port",
        ["--time"] = "--time",
        ["--universe"] = "--universe",
        ["--target"] = "--target",
    };

    // The protocol counts durations in milliseconds, in 32 bits.
    private const double MaxSeconds = uint.MaxValue / 1000.0;

    private readonly Queue<string> _words = new();
    private readonly Dictionary<string, string> _options = [];

    /// <summary>Splits the arguments. Options may stand anywhere, as "--time 2" or "--time=2".</summary>
    public CommandLine(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith('-'))
            {
                _words.Enqueue(args[i]);
                continue;
            }

            string[] nameAndValue = args[i].Split('=', 2);
            string name = nameAndValue[0];
            if (!LongNames.TryGetValue(name, out string? longName))
                throw new UsageException($"unknown option {name}");

            // The value is attached ("--time=2") or it is the next argument ("--time 2").
            string? value = null;
            if (nameAndValue.Length == 2)
                value = nameAndValue[1];
            else if (i + 1 < args.Length)
                value = args[++i];

            if (string.IsNullOrEmpty(value))
                throw new UsageException($"option {name} needs a value");

            _options[longName] = value;
        }
    }

    /// <summary>True while there are plain words left.</summary>
    public bool HasWords => _words.Count > 0;

    /// <summary>Takes the next plain word.</summary>
    /// <param name="what">What the word is, for the error message when it is missing.</param>
    public string NextWord(string what)
    {
        if (!HasWords)
            throw new UsageException($"missing {what}");

        return _words.Dequeue();
    }

    /// <summary>Takes the next plain word, which has to be one of <paramref name="choices"/>.</summary>
    public string NextChoice(string what, params string[] choices)
    {
        string allowed = string.Join(" or ", choices);
        string word = NextWord($"{what}: {allowed}");
        if (!choices.Contains(word))
            throw new UsageException($"{what} must be {allowed}, not '{word}'");

        return word;
    }

    /// <summary>Takes the next plain word as a whole number from <paramref name="min"/> to <paramref name="max"/>.</summary>
    public int NextInt(string what, int min, int max)
    {
        return ParseInt(NextWord(what), what, min, max);
    }

    /// <summary>Takes all plain words that are left as levels from 0 to 255. There has to be at least one.</summary>
    public byte[] RemainingLevels()
    {
        var levels = new List<byte>();
        do
        {
            levels.Add((byte)NextInt("value", min: 0, max: 255));
        }
        while (HasWords);

        return levels.ToArray();
    }

    /// <summary>Takes the value of an option; null if the option was not given.</summary>
    /// <param name="longName">The option's long name, for example "--host".</param>
    public string? Option(string longName)
    {
        return _options.Remove(longName, out string? value) ? value : null;
    }

    /// <summary>Takes the value of an option as a whole number.</summary>
    public int IntOption(string longName, int defaultValue, int min, int max)
    {
        string? text = Option(longName);
        return text is null ? defaultValue : ParseInt(text, longName, min, max);
    }

    /// <summary>Takes the value of an option as a time in seconds, for example "2.5".</summary>
    public TimeSpan SecondsOption(string longName, double defaultSeconds)
    {
        string? text = Option(longName);
        if (text is null)
            return TimeSpan.FromSeconds(defaultSeconds);

        bool valid = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds)
            && seconds >= 0
            && seconds <= MaxSeconds;
        if (!valid)
            throw new UsageException($"{longName} must be a time in seconds like 2.5, not '{text}'");

        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// Call this when the command has taken everything it understands:
    /// whatever is left on the command line is a mistake.
    /// </summary>
    public void ExpectEnd()
    {
        if (HasWords)
            throw new UsageException($"unexpected argument '{_words.Peek()}'");
        if (_options.Count > 0)
            throw new UsageException($"option {_options.Keys.First()} does not belong to this command");
    }

    private static int ParseInt(string text, string what, int min, int max)
    {
        bool valid = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            && value >= min
            && value <= max;
        if (!valid)
            throw new UsageException($"{what} must be a whole number from {min} to {max}, not '{text}'");

        return value;
    }
}
