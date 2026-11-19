namespace DmxBox.Server;

/// <summary>
/// The rules for the notes under the faders: a short text that says which light a channel is,
/// or what a memory is for. Like a strip of tape on a real desk.
/// </summary>
internal static class Note
{
    /// <summary>The longest note allowed: about what fits under a fader.</summary>
    public const int MaxLength = 24;

    // The last of the control characters; the others are all those below the space.
    private const char Delete = (char)0x7F;

    // The UI shows a note as two lines and writes this character between them (the Unicode
    // "line separator"). A note whose first line is empty starts with it.
    private const char LineSeparator = '\u2028';

    /// <summary>
    /// Makes a note out of a text: without the spaces at its ends, and checked. A line
    /// separator at the start stays, because it says that the first line is empty.
    /// </summary>
    /// <param name="text">The text as typed. No text at all counts as an empty one.</param>
    /// <returns>The note. An empty one means "no note".</returns>
    /// <exception cref="DeskException">The text cannot be a note. The message says why.</exception>
    public static string Clean(string? text)
    {
        string note = (text ?? "").TrimEnd();
        int start = 0;
        while (start < note.Length && char.IsWhiteSpace(note[start]) && note[start] != LineSeparator)
            start++;
        note = note[start..];
        if (note.Length > MaxLength)
            throw new DeskException($"A note can have at most {MaxLength} characters.");
        if (note.Any(IsControlCharacter))
            throw new DeskException("A note cannot contain a tab, a line break or the like.");

        return note;
    }

    // Control characters are no text, and a line break would tear the line under the fader apart.
    private static bool IsControlCharacter(char character)
    {
        return character < ' ' || character == Delete;
    }
}
