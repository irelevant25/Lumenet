namespace DmxBox.Server;

/// <summary>
/// The notes of a project: instructions for the show, written on the Notes view of the UI. The UI
/// writes them as a little HTML, with the few marks its editor has (bold, underline, sizes, colors
/// and lists), and shows only those marks of whatever it reads, also of a file someone edited by
/// hand. The desk keeps the text as it is, and only sees to it that it has a sensible size.
/// </summary>
internal static class ProjectNotes
{
    /// <summary>The most characters the notes may have: many pages of instructions, with their marks.</summary>
    public const int MaxLength = 200_000;

    /// <summary>Notes from a UI or a file, checked.</summary>
    /// <param name="notes">The notes; null for none.</param>
    /// <returns>The notes as the desk keeps them: an empty text for none.</returns>
    /// <exception cref="DeskException">They are too long. The message says so.</exception>
    public static string Checked(string? notes)
    {
        if (notes is null)
            return "";
        if (notes.Length > MaxLength)
            throw new DeskException($"The notes are too long: at most {MaxLength} characters, with their marks for bold, colors and the like.");

        return notes;
    }
}
