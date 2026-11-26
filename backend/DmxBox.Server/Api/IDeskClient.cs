namespace DmxBox.Server.Api;

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

    /// <summary>
    /// A box was found or lost, or what the boxes play changed: another number of universes, or
    /// another list of boxes in the project.
    /// </summary>
    /// <param name="boxes">All boxes as the Boxes window shows them, see <see cref="BoxNetwork.Boxes"/>.</param>
    Task BoxesChanged(BoxView[] boxes);

    /// <summary>
    /// An output besides the boxes came or went, or the project's list of them changed: an Art-Net
    /// node that answers or stops answering, a USB interface plugged in or pulled out, an output
    /// added in the Outputs window.
    /// </summary>
    /// <param name="outputs">All of them as the Outputs window shows them, see <see cref="OutputNetwork.Outputs"/>.</param>
    Task OutputsChanged(OutputView[] outputs);

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

    /// <summary>The rate, a tempo for all sequences, was switched or set (its fader, or the taps of TEMPO).</summary>
    /// <param name="on">True while the rate counts (RATE is lit).</param>
    /// <param name="bpm">The tempo, 30 to 300 beats per minute.</param>
    Task RateChanged(bool on, double bpm);

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

    /// <summary>
    /// An effect was stored, copied, kept or taken out. (After undo, redo, a load, a new project or
    /// an import the UIs get the whole desk instead, and ask for the effects with it.)
    /// </summary>
    /// <param name="effects">The effects of the project now, in the order of their numbers.</param>
    Task EffectsChanged(Effect[] effects);

    /// <summary>
    /// The playback of an effect changed: its key, its fader, its live speed, size or spread, by a
    /// UI, the stack or from outside. The playbacks also come with every <see cref="Playback"/>.
    /// </summary>
    /// <param name="playback">The playback now.</param>
    Task EffectPlaybackChanged(EffectPlayback playback);

    /// <summary>An effect key of the independents was bound to another effect.</summary>
    /// <param name="index">Number of the key, 1-32.</param>
    /// <param name="effect">The effect the key starts and stops now, 1-64.</param>
    Task EffectIndependentChanged(int index, int effect);

    /// <summary>A sound key of the independents was bound to another sound.</summary>
    /// <param name="index">Number of the key, 1-32.</param>
    /// <param name="sound">The sound it starts and stops now, 1-64.</param>
    Task SoundIndependentChanged(int index, int sound);

    /// <summary>
    /// A sound was stored, copied or taken out. (After undo, redo, a load, a new project or an
    /// import the UIs get the whole desk instead, and ask for the sounds with it.)
    /// </summary>
    /// <param name="sounds">The sounds of the project now, in the order of their numbers.</param>
    Task SoundsChanged(Sound[] sounds);

    /// <summary>
    /// The playback of a sound changed: its key or its fader, by a UI or from outside. The playbacks
    /// also come with every <see cref="Playback"/>.
    /// </summary>
    /// <param name="playback">The playback now.</param>
    Task SoundPlaybackChanged(SoundPlayback playback);

    /// <summary>The master volume of the sound or MUTE changed.</summary>
    /// <param name="decibels">The master volume, -100 dB to 0 dB.</param>
    /// <param name="mute">True while MUTE is on.</param>
    Task SoundMasterChanged(double decibels, bool mute);

    /// <summary>The device of the sound changed, or its outputs, or it went or came back. Only the pages on the PC get it.</summary>
    /// <param name="output">The device now.</param>
    Task SoundOutputChanged(SoundOutputView output);

    /// <summary>LISTEN ON went on or off, or its device went or came back. Every page gets it.</summary>
    /// <param name="listen">The listening now.</param>
    Task SoundListenChanged(ListenView listen);

    /// <summary>Player mode went on or off, or the mixer was found or lost. Every page gets it.</summary>
    /// <param name="mixer">The mixer now.</param>
    Task MixerChanged(MixerView mixer);

    /// <summary>
    /// The MIDI changed: a device came or went, the setup changed, learning started or ended. Only
    /// the pages on the PC get it.
    /// </summary>
    /// <param name="state">The MIDI now.</param>
    Task MidiChanged(MidiState state);

    /// <summary>MIDI messages came in, and what became of them: for the pages that show the list.</summary>
    /// <param name="lines">The messages, the oldest first.</param>
    Task MidiMessages(MidiMonitorLine[] lines);

    /// <summary>
    /// A key of a controller was pressed or let go: the page presses the desk's key, as its keyboard
    /// shortcut would. Only the page on the PC that presses the keys gets it (see <see cref="MidiRole"/>).
    /// </summary>
    /// <param name="key">The desk's key, by its name ("memory.5").</param>
    /// <param name="down">True when pressed, false when let go.</param>
    Task MidiKey(string key, bool down);

    /// <summary>The page is now the one that presses the keys of the controllers, or no more.</summary>
    /// <param name="role">Whether it is, and the keys whose lamps the desk needs from it.</param>
    Task MidiRole(MidiRole role);
}
