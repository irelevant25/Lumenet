using System.Globalization;
using System.Text.RegularExpressions;

namespace DmxBox.Server.Stack;

/// <summary>
/// The number of a step of the stack, written like "2.1": a whole number and one digit after
/// the dot. The steps stand in the order of their numbers.
/// </summary>
/// <param name="Major">The part before the dot, 0-999.</param>
/// <param name="Minor">The digit after the dot, 0-9.</param>
internal readonly partial record struct CueNumber(int Major, int Minor) : IComparable<CueNumber>
{
    /// <summary>The highest part before the dot.</summary>
    public const int MaxMajor = 999;

    /// <summary>The highest digit after the dot.</summary>
    public const int MaxMinor = 9;

    /// <summary>Reads a number as a person types it: "3", "3.0", "3.1" or "3,1".</summary>
    /// <param name="text">The text.</param>
    /// <exception cref="DeskException">The text is not such a number.</exception>
    public static CueNumber Parse(string? text)
    {
        Match match = Written().Match((text ?? "").Trim());
        if (!match.Success)
        {
            throw new DeskException(
                $"A step number looks like 3 or 2.1: a whole number up to {MaxMajor}, and one digit after the dot.");
        }

        int major = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        int minor = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        return new CueNumber(major, minor);
    }

    /// <inheritdoc/>
    public int CompareTo(CueNumber other)
    {
        return Major != other.Major ? Major.CompareTo(other.Major) : Minor.CompareTo(other.Minor);
    }

    /// <summary>The number as it is shown: "2.1".</summary>
    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}");
    }

    public static bool operator <(CueNumber left, CueNumber right) => left.CompareTo(right) < 0;

    public static bool operator >(CueNumber left, CueNumber right) => left.CompareTo(right) > 0;

    public static bool operator <=(CueNumber left, CueNumber right) => left.CompareTo(right) <= 0;

    public static bool operator >=(CueNumber left, CueNumber right) => left.CompareTo(right) >= 0;

    // One to three digits, then maybe a dot or a comma and one digit.
    [GeneratedRegex(@"^(\d{1,3})(?:[.,](\d))?$")]
    private static partial Regex Written();
}
