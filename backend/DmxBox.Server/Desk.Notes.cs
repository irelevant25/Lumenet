namespace DmxBox.Server;

/// <summary>
/// The notes of the project: instructions for the show, written on the Notes view of the UI, see
/// <see cref="ProjectNotes"/>.
/// </summary>
internal sealed partial class Desk
{
    // The notes of the project; an empty text for none.
    private string _notes = "";

    /// <summary>The notes of the project, as the UI wrote them; an empty text for none.</summary>
    public string Notes
    {
        get
        {
            lock (_lock)
            {
                return _notes;
            }
        }
    }

    /// <summary>
    /// Takes the notes as the owner wrote them. They belong to the project, which then has
    /// something to save; like everything that is stored, they cannot change while the stack is live.
    /// </summary>
    /// <param name="notes">The notes as the UI wrote them; null or empty for none.</param>
    /// <returns>True if they differ from the notes the desk had.</returns>
    /// <exception cref="DeskException">They are too long, or the stack is live.</exception>
    public bool SetNotes(string? notes)
    {
        string kept = ProjectNotes.Checked(notes);

        lock (_lock)
        {
            RequireUnlocked();
            if (kept == _notes)
                return false;

            _notes = kept;
            _projectChanges++;
            return true;
        }
    }
}
