namespace DmxBox.Server;

/// <summary>
/// The messages the server sends to the UIs on its own. Calling one of these methods sends a
/// message with the method's name and arguments.
/// </summary>
/// <remarks>
/// <para>
/// The messages about one fader, one note or one independent key go to every UI except the
/// one that made the change: it shows the value already, and an echo would fight with a fader
/// that is still being dragged, or with a text that is still being typed.
/// </para>
/// <para>
/// Public, like the three records it mentions, although nothing outside this program uses
/// them: SignalR writes the class behind this interface while the program runs, in an
/// assembly of its own, and from there it can only see public types.
/// </para>
/// </remarks>
public interface IDeskClient
{
    /// <summary>A channel fader was moved.</summary>
    /// <param name="channel">Channel number, 1-512.</param>
    /// <param name="value">The fader's new level, 0-255.</param>
    Task ChannelChanged(int channel, int value);

    /// <summary>The fader of a memory was moved.</summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="value">The fader's new level, 0-255.</param>
    Task MemoryLevelChanged(int memory, int value);

    /// <summary>The master fader was moved.</summary>
    /// <param name="value">The fader's new level, 0-255.</param>
    Task MasterChanged(int value);

    /// <summary>The note under a channel fader was written or taken away.</summary>
    /// <param name="channel">Channel number, 1-512.</param>
    /// <param name="text">The note as it is stored now; empty if it was taken away.</param>
    Task ChannelNoteChanged(int channel, string text);

    /// <summary>The note under the fader of a memory was written or taken away.</summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="text">The note as it is stored now; empty if it was taken away.</param>
    Task MemoryNoteChanged(int memory, string text);

    /// <summary>A channel key of the independents was bound to another channel.</summary>
    /// <param name="index">Number of the key, 1-10.</param>
    /// <param name="channel">The channel the key switches now, 1-512.</param>
    Task IndependentChanged(int index, int channel);

    /// <summary>A memory key of the independents was bound to another memory.</summary>
    /// <param name="index">Number of the key, 1-10.</param>
    /// <param name="memory">The memory whose fader the key switches now, 1-128.</param>
    Task MemoryIndependentChanged(int index, int memory);

    /// <summary>Blackout was switched, on a UI or with the button on the box.</summary>
    /// <param name="on">True for dark.</param>
    Task BlackoutChanged(bool on);

    /// <summary>Something changed more than one fader, or a memory: here is the whole desk again.</summary>
    /// <param name="desk">The desk after the change.</param>
    Task DeskChanged(DeskState desk);

    /// <summary>The box was found or lost.</summary>
    /// <param name="box">The new state of the connection.</param>
    Task BoxChanged(BoxStatus box);

    /// <summary>The project's name, file or "unsaved" mark changed.</summary>
    /// <param name="project">The project after the change.</param>
    Task ProjectChanged(ProjectInfo project);

    /// <summary>The speed of the sequence of a memory was changed; the sequence goes on from where it is.</summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="speed">The new speed, 0.1 to 10.</param>
    Task SequenceSpeedChanged(int memory, double speed);

    /// <summary>A step of the stack was added, changed, moved, renumbered or taken out.</summary>
    /// <param name="stack">The stack after the change.</param>
    Task StackChanged(StackState stack);

    /// <summary>
    /// What changes with time: what the running sequences give, and where the stack stands.
    /// It comes about 10 times a second while something moves, once more when it has stopped,
    /// and after Play, Pause, Next, Previous and the switch that takes the stack live.
    /// </summary>
    /// <param name="playback">The state of this moment.</param>
    Task PlaybackChanged(Playback playback);

    /// <summary>
    /// LAN was switched on or off, or this PC's address in the network changed. Only the UIs on
    /// this PC get it: it carries the password.
    /// </summary>
    /// <param name="lan">LAN as it is now.</param>
    Task LanAccessChanged(LanState lan);

    /// <summary>
    /// The notes of the project were changed in a UI. (After undo, redo, a load, a new project or
    /// an import the UIs get the whole desk instead, and ask for the notes with it.)
    /// </summary>
    /// <param name="notes">The notes as the desk keeps them now; an empty text for none.</param>
    Task NotesChanged(string notes);

    /// <summary>What undo and redo would do changed: after a change of the project, an undo, a redo or a load.</summary>
    /// <param name="history">What undo and redo would do now.</param>
    Task HistoryChanged(HistoryState history);

    /// <summary>The rate, the speed master for all sequences, was switched or turned.</summary>
    /// <param name="on">True while the rate counts.</param>
    /// <param name="rate">The rate, 0.1 to 10.</param>
    Task RateChanged(bool on, double rate);

    /// <summary>
    /// The autosave was switched or got another interval, on the PC. Only the UIs on this PC get
    /// it: they show it in Settings.
    /// </summary>
    /// <param name="options">The autosave as it is now.</param>
    Task AutosaveChanged(AutosaveOptions options);

    /// <summary>
    /// The stage plan was changed in a UI. (After undo, redo, a load, a new project or an import
    /// the UIs get the whole desk instead, and ask for the plan with it.)
    /// </summary>
    /// <param name="plan">The plan as the desk keeps it now.</param>
    Task StageChanged(StagePlan plan);

    /// <summary>A key was held for a solo, or let go, on some UI, see <see cref="SoloState"/>.</summary>
    /// <param name="solo">The keys held for a solo now.</param>
    Task SoloChanged(SoloState solo);
}
