namespace DmxBox.Server;

/// <summary>
/// The rules for project names. A project's name is the name of its file without ".json", so
/// it has to be fit for a file name. The rules are those of Windows on every system: they are
/// the strictest, and a projects folder can then be carried from one computer to another.
/// </summary>
internal static class ProjectName
{
    /// <summary>The longest name allowed. It leaves room for the folder in a Windows path.</summary>
    public const int MaxLength = 80;

    // Windows allows none of these in a file name. The first two separate folders.
    private const string ForbiddenCharacters = "/\\<>:\"|?*";

    // After COM or LPT, any of these makes a device name. The last three are the raised
    // digits 1, 2 and 3, which Windows counts as digits here.
    private const string PortDigits = "0123456789\u00B9\u00B2\u00B3";

    private static readonly string[] DeviceNames = ["CON", "PRN", "AUX", "NUL"];
    private static readonly string[] PortNames = ["COM", "LPT"];

    /// <summary>Checks a name that came from the user.</summary>
    /// <param name="name">The name to check. A UI can send none at all, hence the "?".</param>
    /// <exception cref="DeskException">The name cannot be used. The message says why.</exception>
    public static void Require(string? name)
    {
        if (string.IsNullOrEmpty(name))
            throw new DeskException("A project needs a name.");
        if (name.Length > MaxLength)
            throw new DeskException($"A project name can have at most {MaxLength} characters.");
        if (IsDotOrSpace(name[0]) || IsDotOrSpace(name[^1]))
            throw new DeskException("A project name cannot begin or end with a dot or a space.");

        foreach (char character in name)
        {
            if (ForbiddenCharacters.Contains(character))
                throw new DeskException($"A project name cannot contain the character \"{character}\".");
            if (char.IsControl(character))
                throw new DeskException("A project name cannot contain a tab, a line break or the like.");
        }

        if (IsDeviceName(name))
            throw new DeskException($"\"{name}\" cannot be a project name: Windows keeps that name for itself.");
    }

    private static bool IsDotOrSpace(char character)
    {
        return character is '.' or ' ';
    }

    // Windows keeps a handful of names for devices, in upper or lower case and whatever
    // follows the first dot: "nul.json" is the null device, not a file.
    private static bool IsDeviceName(string name)
    {
        string stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        if (DeviceNames.Contains(stem))
            return true;

        return stem.Length == 4
            && PortNames.Contains(stem[..3])
            && PortDigits.Contains(stem[3]);
    }
}
