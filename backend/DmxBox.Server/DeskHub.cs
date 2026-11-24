using System.Globalization;
using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Server;

/// <summary>
/// What a UI can ask of the server, at /hub/desk. Every method does its work on the desk or
/// the project and then tells the UIs that have to know; <see cref="IDeskClient"/> lists
/// those messages.
/// </summary>
/// <remarks>
/// <para>
/// A request that cannot be carried out ends in an exception from the desk or the project
/// store. <see cref="UserErrorFilter"/> hands its message to the UI that asked, and writes
/// the line about it in the log.
/// </para>
/// <para>
/// Every change of what the project holds runs inside the undo history, which keeps the
/// project as it was before (see <see cref="UndoHistory"/>).
/// </para>
/// </remarks>
internal sealed class DeskHub : Hub<IDeskClient>
{
    // The name of an imported project whose file's name cannot be one.
    private const string ImportedProjectName = "Imported project";

    private readonly Desk _desk;
    private readonly ProjectStore _projects;
    private readonly BoxNetwork _boxes;
    private readonly LanAccess _lan;
    private readonly UndoHistory _history;
    private readonly Autosave _autosave;

    /// <summary>SignalR makes a hub for every call and fills in the parts of the server.</summary>
    /// <param name="desk">The desk.</param>
    /// <param name="projects">The projects folder and the open project.</param>
    /// <param name="boxes">The boxes, for their state and what they play.</param>
    /// <param name="lan">Whether other devices in the network may use the desk.</param>
    /// <param name="history">Undo and redo for what the project holds.</param>
    /// <param name="autosave">Keeps the work that is not saved yet.</param>
    public DeskHub(Desk desk, ProjectStore projects, BoxNetwork boxes, LanAccess lan, UndoHistory history, Autosave autosave)
    {
        _desk = desk;
        _projects = projects;
        _boxes = boxes;
        _lan = lan;
        _history = history;
        _autosave = autosave;
    }

    // True when the UI that calls runs on this PC, not on another device in the network.
    private bool IsLocal => _lan.IsLocal(Context.GetHttpContext()?.Connection.RemoteIpAddress);

    /// <summary>
    /// A UI connected. One on this PC joins the group that hears about LAN; the connection of
    /// another device is noted, so that LAN off can cut it.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        if (IsLocal)
            await Groups.AddToGroupAsync(Context.ConnectionId, LanAccess.PcGroup);
        else
            _lan.Track(Context);

        await base.OnConnectedAsync();
    }

    /// <summary>A UI went away.</summary>
    /// <param name="exception">Why, if it was not a goodbye.</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _lan.Untrack(Context.ConnectionId);

        // Keys it held for a solo are let go with it: no solo stays on by itself.
        if (_desk.DropSolo(Context.ConnectionId))
            await Clients.Others.SoloChanged(_desk.Solo);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// A key of a channel or a memory is held for a solo, or let go: with SOLO UP only the faders
    /// of the held keys shine, at full; with SOLO DOWN their faders give nothing. Every UI hears
    /// which keys are held. The UI moves the fader of the key as well, to full or to zero, and
    /// back when it is let go.
    /// </summary>
    /// <param name="up">True for SOLO UP, false for SOLO DOWN.</param>
    /// <param name="target">"channel" or "memory".</param>
    /// <param name="number">The channel (1-512) or the memory (1-128).</param>
    /// <param name="held">True while it is held, false when it is let go.</param>
    public Task HoldSolo(bool up, string target, int number, bool held)
    {
        return _desk.HoldSolo(Context.ConnectionId, up, target, number, held)
            ? Clients.All.SoloChanged(_desk.Solo)
            : Task.CompletedTask;
    }

    /// <summary>The first call of a UI: everything it needs to draw itself.</summary>
    public HelloAnswer Hello()
    {
        return new HelloAnswer(AppInfo.Version, _desk.GetState(), _boxes.Boxes, _projects.Current);
    }

    /// <summary>Moves one channel fader.</summary>
    /// <param name="channel">Channel number, 1-512.</param>
    /// <param name="value">The fader's new level, 0-255.</param>
    public Task SetChannel(int channel, int value)
    {
        _desk.SetChannel(channel, value);
        return Clients.Others.ChannelChanged(channel, value);
    }

    /// <summary>Moves the fader of one memory.</summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="value">The fader's new level, 0-255.</param>
    public Task SetMemoryLevel(int memory, int value)
    {
        _desk.SetMemoryLevel(memory, value);
        return Clients.Others.MemoryLevelChanged(memory, value);
    }

    /// <summary>Moves the master fader.</summary>
    /// <param name="value">The fader's new level, 0-255.</param>
    public Task SetMaster(int value)
    {
        _desk.SetMaster(value);
        return Clients.Others.MasterChanged(value);
    }

    /// <summary>Writes the note under a channel fader. An empty text takes the note away.</summary>
    /// <param name="channel">Channel number, 1-512.</param>
    /// <param name="text">The text as typed: at most 24 characters, not counting spaces at its ends.</param>
    public async Task SetChannelNote(int channel, string text)
    {
        string note;
        using (_history.Change($"the note of channel {Desk.ChannelLabel(channel)}"))
            note = _desk.SetChannelNote(channel, text);

        await Clients.Others.ChannelNoteChanged(channel, note);
        await TellProjectAsync();
    }

    /// <summary>Writes the note under the fader of a memory. An empty text takes the note away.</summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="text">The text as typed: at most 24 characters, not counting spaces at its ends.</param>
    public async Task SetMemoryNote(int memory, string text)
    {
        string note;
        using (_history.Change($"the note of memory {memory}"))
            note = _desk.SetMemoryNote(memory, text);

        await Clients.Others.MemoryNoteChanged(memory, note);
        await TellProjectAsync();
    }

    /// <summary>
    /// Binds a channel key of the independents to a channel. Switching that channel is the UI's
    /// business: it uses SetChannel.
    /// </summary>
    /// <param name="index">Number of the key, 1-10.</param>
    /// <param name="channel">The channel the key switches from now on, 1-512.</param>
    public async Task SetIndependent(int index, int channel)
    {
        using (_history.Change($"channel key {index} of the independents"))
            _desk.SetIndependent(index, channel);

        await Clients.Others.IndependentChanged(index, channel);
        await TellProjectAsync();
    }

    /// <summary>
    /// Binds a memory key of the independents to a memory. Switching that memory's fader is the
    /// UI's business: it uses SetMemoryLevel.
    /// </summary>
    /// <param name="index">Number of the key, 1-10.</param>
    /// <param name="memory">The memory whose fader the key switches from now on, 1-128.</param>
    public async Task SetMemoryIndependent(int index, int memory)
    {
        using (_history.Change($"memory key {index} of the independents"))
            _desk.SetMemoryIndependent(index, memory);

        await Clients.Others.MemoryIndependentChanged(index, memory);
        await TellProjectAsync();
    }

    /// <summary>Switches blackout. The box link passes it on to the box.</summary>
    /// <param name="on">True for dark.</param>
    public Task SetBlackout(bool on)
    {
        _desk.SetBlackout(on);
        return Clients.All.BlackoutChanged(on);
    }

    /// <summary>Brings all channel faders and memory faders down to 0.</summary>
    public Task Clear()
    {
        _desk.Clear();
        return TellDeskAsync();
    }

    /// <summary>Stores what the channel faders show in a memory.</summary>
    /// <param name="memory">Memory number, 1-128.</param>
    public Task SaveMemory(int memory)
    {
        using (_history.Change($"storing memory {memory}"))
            _desk.SaveMemory(memory);

        return TellDeskAsync();
    }

    /// <summary>Sets the channel faders to the look of a memory.</summary>
    /// <param name="memory">Memory number, 1-128. The memory must hold a look.</param>
    public Task LoadMemory(int memory)
    {
        _desk.LoadMemory(memory);
        return TellDeskAsync();
    }

    /// <summary>Copies the look of one memory to another.</summary>
    /// <param name="from">Number of the memory to copy, 1-128. It must hold a look.</param>
    /// <param name="to">Number of the memory that receives the copy, 1-128, another one than <paramref name="from"/>.</param>
    public Task CopyMemory(int from, int to)
    {
        using (_history.Change($"copying memory {from} to {to}"))
            _desk.CopyMemory(from, to);

        return TellDeskAsync();
    }

    /// <summary>Empties a memory.</summary>
    /// <param name="memory">Memory number, 1-128.</param>
    public Task DeleteMemory(int memory)
    {
        using (_history.Change($"emptying memory {memory}"))
            _desk.DeleteMemory(memory);

        return TellDeskAsync();
    }

    /// <summary>
    /// Changes the level of one channel in the look of a memory, as the table of the Memories
    /// view does. An empty memory gets a look with only that channel.
    /// </summary>
    /// <param name="memory">Memory number, 1-128. It must not hold a sequence.</param>
    /// <param name="channel">Channel number, 1 to the channels of the project's universes.</param>
    /// <param name="value">The level the look gives the channel, 0-255.</param>
    public Task SetMemoryChannel(int memory, int channel, int value)
    {
        using (_history.Change($"channel {Desk.ChannelLabel(channel)} of memory {memory}"))
        {
            if (!_desk.SetMemoryChannel(memory, channel, value))
                return Task.CompletedTask;
        }

        return TellDeskAsync();
    }

    /// <summary>
    /// What every stored memory holds. A UI asks after "Hello" and again after every
    /// <see cref="IDeskClient.DeskChanged"/>, because that is when a look may have changed.
    /// </summary>
    /// <returns>For each stored memory, by its number: 512 levels, channel 1 first.</returns>
    public Dictionary<int, int[]> GetMemories()
    {
        return _desk.GetMemoryLooks();
    }

    /// <summary>The sequences, for each memory that holds one, by its number.</summary>
    public Dictionary<int, Sequence> GetSequences()
    {
        return _desk.GetSequences();
    }

    /// <summary>Stores a sequence in a memory that is empty or holds a sequence already.</summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="sequence">
    /// The sequence: "loop", "random" (a new order on every round), "speed" (0.1 to 10) and the
    /// steps, the first one first.
    /// </param>
    public Task SetSequence(int memory, Sequence sequence)
    {
        using (_history.Change($"the sequence of memory {memory}"))
            _desk.SetSequence(memory, sequence);

        return TellDeskAsync();
    }

    /// <summary>
    /// Changes how fast the sequence of a memory is played, without starting it again. The UI
    /// moves its speed fader this way, like a fader: the other UIs hear of it, this one does not.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="speed">From 0.1 to 10.</param>
    public async Task SetSequenceSpeed(int memory, double speed)
    {
        double kept;
        using (_history.Change($"the speed of the sequence in memory {memory}", $"speed {memory}"))
            kept = _desk.SetSequenceSpeed(memory, speed);

        await Clients.Others.SequenceSpeedChanged(memory, kept);
        await TellProjectAsync();
    }

    /// <summary>
    /// Sets the rate, a tempo for all sequences (see <see cref="Desk.SetRate"/>): RATE and its
    /// fader, or the taps of TEMPO. The UI sends it like a fader: the other UIs hear of it, this
    /// one does not. It is no part of the project, so the history leaves it alone.
    /// </summary>
    /// <param name="on">True: the rate counts (RATE is lit).</param>
    /// <param name="bpm">The tempo, from 30 to 300 beats per minute.</param>
    public Task SetRate(bool on, double bpm)
    {
        _desk.SetRate(on, bpm);
        return Clients.Others.RateChanged(on, bpm);
    }

    /// <summary>
    /// Puts one more step at the end of the sequence of a memory. An empty memory gets a new
    /// sequence, played in a loop.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="step">The step.</param>
    public Task AddSequenceStep(int memory, Step step)
    {
        using (_history.Change($"a step of the sequence in memory {memory}"))
            _desk.AddSequenceStep(memory, step);

        return TellDeskAsync();
    }

    /// <summary>The stack: its steps in the order of their numbers.</summary>
    public StackState GetStack()
    {
        return _desk.GetStack();
    }

    /// <summary>What changes with time: what the running sequences give, and where the stack stands.</summary>
    public Playback GetPlayback()
    {
        return _desk.GetPlayback();
    }

    /// <summary>Puts a new step at the end of the stack.</summary>
    /// <param name="step">The step.</param>
    /// <returns>The name of the new step.</returns>
    public async Task<int> AddStackStep(Step step)
    {
        int id;
        using (_history.Change("a new step of the stack"))
            id = _desk.AddStackStep(step);

        await TellStackAsync();
        return id;
    }

    /// <summary>
    /// Puts a new step right after another step of the stack; it gets a number that fits there.
    /// One change in the history, however many steps give way.
    /// </summary>
    /// <param name="afterId">The step it goes behind.</param>
    /// <param name="step">The step.</param>
    /// <returns>The name of the new step.</returns>
    public async Task<int> AddStackStepAfter(int afterId, Step step)
    {
        int id;
        using (_history.Change($"a new step after step {CueOf(afterId)} of the stack"))
            id = _desk.AddStackStep(step, afterId);

        await TellStackAsync();
        return id;
    }

    /// <summary>Changes what a step of the stack stands for, its value or its times.</summary>
    /// <param name="id">The name of the step.</param>
    /// <param name="step">The step as it is to be.</param>
    public Task UpdateStackStep(int id, Step step)
    {
        using (_history.Change($"step {CueOf(id)} of the stack"))
            _desk.UpdateStackStep(id, step);

        return TellStackAsync();
    }

    /// <summary>Takes a step out of the stack.</summary>
    /// <param name="id">The name of the step.</param>
    public Task DeleteStackStep(int id)
    {
        using (_history.Change($"taking step {CueOf(id)} out of the stack"))
            _desk.DeleteStackStep(id);

        return TellStackAsync();
    }

    /// <summary>Puts a step of the stack right after another one; it gets a number that fits there.</summary>
    /// <param name="id">The name of the step to move.</param>
    /// <param name="afterId">The step it goes behind; null for the very top.</param>
    public Task MoveStackStep(int id, int? afterId)
    {
        using (_history.Change($"moving step {CueOf(id)} of the stack"))
            _desk.MoveStackStep(id, afterId);

        return TellStackAsync();
    }

    /// <summary>Gives a step of the stack the number its owner typed.</summary>
    /// <param name="id">The name of the step.</param>
    /// <param name="cue">The number as text: "3", "3.0", "2.1".</param>
    public Task RenumberStackStep(int id, string cue)
    {
        using (_history.Change($"renumbering step {CueOf(id)} of the stack"))
            _desk.RenumberStackStep(id, cue);

        return TellStackAsync();
    }

    /// <summary>
    /// Switches the stack live or off. Going live brings every fader down to 0, goes to the
    /// first step and locks everything that is stored; switching it off stops the stack and
    /// leaves the faders where they are. The rate stays as it is either way.
    /// </summary>
    /// <param name="on">True for live.</param>
    public async Task SetStackLive(bool on)
    {
        _desk.SetStackLive(on);

        // Live: every fader went to 0. Off: the sequences play at their own speeds again. Either
        // way the UIs take the whole desk and its sequences again.
        await Clients.All.DeskChanged(_desk.GetState());
        await TellPlaybackAsync();
    }

    /// <summary>
    /// Changes how many universes the project has, 1 to 8 (see <see cref="Desk.SetUniverses"/>).
    /// Every UI gets the whole desk again: it has more channels, or fewer.
    /// </summary>
    /// <param name="universes">1 to 8.</param>
    public async Task SetUniverses(int universes)
    {
        using (_history.Change("the universes"))
            _desk.SetUniverses(universes);

        await TellDeskAsync();
    }

    /// <summary>The boxes as the Boxes window shows them, see <see cref="BoxNetwork.Boxes"/>.</summary>
    public BoxView[] GetBoxes()
    {
        return _boxes.Boxes;
    }

    /// <summary>
    /// Takes the project's list of boxes as the Boxes window changed it (see <see cref="Desk.SetBoxes"/>):
    /// a box added by hand, an output given another universe, a box taken out, or an empty list for
    /// automatic. The desk starts looking for a box that was added; every UI gets the boxes.
    /// </summary>
    /// <param name="boxes">The whole list as it is to be.</param>
    public async Task SetBoxes(BoxPlan[] boxes)
    {
        bool changed;
        using (_history.Change("the boxes"))
            changed = _desk.SetBoxes(boxes);

        if (changed)
            _boxes.Refresh();

        await TellProjectAsync();
    }

    /// <summary>The stage plan of the project: the size of the stage and the lights of the rig on it.</summary>
    public StagePlan GetStage()
    {
        return _desk.Stage;
    }

    /// <summary>
    /// Takes the stage plan as the owner edited it, whole: a light added, moved, changed or taken
    /// away, or the stage made bigger. Every UI gets the plan as the desk keeps it. Changes of the
    /// plan in quick succession are one step of the history.
    /// </summary>
    /// <param name="plan">The plan as it is to be.</param>
    public async Task SetStage(StagePlan plan)
    {
        bool changed;
        using (_history.Change("the stage plan", "stage"))
            changed = _desk.SetStage(plan);

        if (changed)
            await Clients.All.StageChanged(_desk.Stage);

        await TellProjectAsync();
    }

    /// <summary>
    /// The picture under the stage plan, as a data URL; null without one. A UI asks for it when
    /// the plan names another picture (<see cref="StagePlan.PictureId"/>).
    /// </summary>
    public string? GetStagePicture()
    {
        return _desk.StagePicture;
    }

    /// <summary>
    /// Puts a picture under the stage plan, or takes it away. Every UI gets the plan, which names
    /// the new picture, and asks for it.
    /// </summary>
    /// <param name="picture">A data URL of a JPEG, PNG or WebP image; null for none.</param>
    public async Task SetStagePicture(string? picture)
    {
        bool changed;
        using (_history.Change(picture is null ? "taking the picture of the stage away" : "the picture of the stage"))
            changed = _desk.SetStagePicture(picture);

        if (changed)
            await Clients.All.StageChanged(_desk.Stage);

        await TellProjectAsync();
    }

    /// <summary>
    /// Undo: the project goes back to how it was before its last change. See <see cref="UndoHistory"/>
    /// for what counts. Every UI gets the whole desk again.
    /// </summary>
    /// <returns>What was undone, like "storing memory 5".</returns>
    public async Task<string> Undo()
    {
        string what = _history.Undo();
        await TellDeskAsync();
        return what;
    }

    /// <summary>Redo: a change that was undone is made again.</summary>
    /// <returns>What was redone, like "storing memory 5".</returns>
    public async Task<string> Redo()
    {
        string what = _history.Redo();
        await TellDeskAsync();
        return what;
    }

    /// <summary>What undo and redo would do now.</summary>
    public HistoryState GetHistory()
    {
        return _history.State;
    }

    /// <summary>The notes of the project: instructions for the show, as the Notes view wrote them; an empty text for none.</summary>
    public string GetNotes()
    {
        return _desk.Notes;
    }

    /// <summary>
    /// Takes the notes as the owner wrote them on the Notes view. Every UI hears of it, also the
    /// one that wrote them (which goes on writing in its own editor meanwhile). Changes in quick
    /// succession, as while typing, are one step of the history.
    /// </summary>
    /// <param name="notes">The notes as the UI wrote them; empty for none.</param>
    public async Task SetNotes(string notes)
    {
        bool changed;
        using (_history.Change("the notes", "notes"))
            changed = _desk.SetNotes(notes);

        if (changed)
            await Clients.All.NotesChanged(_desk.Notes);

        await TellProjectAsync();
    }

    /// <summary>Whether the autosave keeps the work that is not saved, and how often.</summary>
    public AutosaveOptions GetAutosave()
    {
        return _autosave.Options;
    }

    /// <summary>
    /// Switches the autosave and sets how often it keeps the work. Only a UI on this PC can do
    /// this; the UIs on this PC hear of it.
    /// </summary>
    /// <param name="on">True to keep the work.</param>
    /// <param name="seconds">How often: 5, 30, 60 or 300 seconds.</param>
    public async Task<AutosaveOptions> SetAutosave(bool on, int seconds)
    {
        RequireLocal("The autosave can only be set on the PC.");
        AutosaveOptions options = _autosave.SetOptions(on, seconds);
        await Clients.Group(LanAccess.PcGroup).AutosaveChanged(options);
        return options;
    }

    /// <summary>
    /// When the unsaved work that the desk program brought back at its start was kept, as
    /// "2026-11-20T18:42:05+01:00"; null when nothing was brought back, and once the project was
    /// saved, loaded or started anew. See <see cref="Autosave"/>.
    /// </summary>
    public string? GetRecovered()
    {
        return _projects.Recovered?.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    /// <summary>Play: goes on after a pause, or goes to the next step.</summary>
    public Task StackPlay()
    {
        _desk.StackPlay();
        return TellPlaybackAsync();
    }

    /// <summary>Pause: holds a running fade or wait.</summary>
    public Task StackPause()
    {
        _desk.StackPause();
        return TellPlaybackAsync();
    }

    /// <summary>Next: goes to the next step now.</summary>
    public Task StackNext()
    {
        _desk.StackNext();
        return TellPlaybackAsync();
    }

    /// <summary>Previous: goes to the step before the current one now.</summary>
    public Task StackPrevious()
    {
        _desk.StackPrevious();
        return TellPlaybackAsync();
    }

    /// <summary>
    /// Whether other devices in the network may use the desk. A UI on this PC also gets the
    /// addresses at which they reach it, and the password while LAN is on.
    /// </summary>
    public LanState GetLanAccess()
    {
        return _lan.StateFor(IsLocal);
    }

    /// <summary>
    /// Switches LAN on or off. Off cuts every other device off at once. Only a UI on this PC can
    /// do this.
    /// </summary>
    /// <param name="on">True to let devices in the network in, with the password.</param>
    public async Task SetLanAccess(bool on)
    {
        if (!IsLocal)
            throw new DeskException("LAN can only be switched on the PC.");

        _lan.Switch(on);
        await _lan.TellPcAsync();
    }

    /// <summary>
    /// Chooses the address of this PC at which devices in the network may use the desk, or lets
    /// the desk choose by itself again. Only a UI on this PC can do this.
    /// </summary>
    /// <param name="address">One of the PC's addresses in the network, like "192.168.1.20"; null or empty for "choose by yourself".</param>
    public async Task SetLanAddress(string? address)
    {
        if (!IsLocal)
            throw new DeskException("The LAN address can only be chosen on the PC.");

        _lan.Choose(address);
        await _lan.TellPcAsync();
    }

    /// <summary>
    /// Makes a typed text the LAN password. Every device has to give the new one; those that are
    /// connected lose their connection. Only a UI on this PC can do this.
    /// </summary>
    /// <param name="password">5 to 20 letters and digits.</param>
    public async Task SetLanPassword(string password)
    {
        if (!IsLocal)
            throw new DeskException("The LAN password can only be changed on the PC.");

        _lan.ChangePassword(password);
        await _lan.TellPcAsync();
    }

    /// <summary>Makes a new random LAN password, as <see cref="SetLanPassword"/> does with a typed one.</summary>
    public async Task NewLanPassword()
    {
        if (!IsLocal)
            throw new DeskException("The LAN password can only be changed on the PC.");

        _lan.RenewPassword();
        await _lan.TellPcAsync();
    }

    /// <summary>Lists the projects that can be loaded, the newest first, and the examples.</summary>
    public ProjectList GetProjects()
    {
        return _projects.List();
    }

    /// <summary>
    /// Starts a new project: a clean desk, called "Untitled", without a file. The master and
    /// blackout stay as they are.
    /// </summary>
    /// <returns>The new project.</returns>
    public async Task<ProjectInfo> NewProject()
    {
        ProjectInfo project = _projects.StartNew();
        _history.Clear();
        await TellDeskAsync();
        return project;
    }

    /// <summary>Writes the memories, the notes and the independent keys to a project file.</summary>
    /// <param name="name">
    /// The name to save under; a project of that name is replaced. Null saves to the file the
    /// project already has, which fails for a project that was never saved.
    /// </param>
    /// <returns>The project after the save.</returns>
    public async Task<ProjectInfo> SaveProject(string? name)
    {
        ProjectInfo project = _projects.Save(name);
        await TellProjectAsync();
        return project;
    }

    /// <summary>
    /// Replaces the memories, the notes and the independent keys by those of a project file.
    /// All memory faders go to 0.
    /// </summary>
    /// <param name="name">The name of the project.</param>
    /// <returns>The project after the load.</returns>
    public async Task<ProjectInfo> LoadProject(string name)
    {
        ProjectInfo project = _projects.Load(name);
        _history.Clear();
        await TellDeskAsync();
        return project;
    }

    /// <summary>
    /// Opens one of the example projects that come with the program, in place of the open one, as
    /// loading does. The example itself never changes: the project has its name but no file, and
    /// nothing to save until something is changed.
    /// </summary>
    /// <param name="name">The name of the example, as <see cref="GetProjects"/> lists it.</param>
    /// <returns>The project after the load.</returns>
    public async Task<ProjectInfo> LoadExample(string name)
    {
        ProjectInfo project = _projects.OpenExample(name);
        _history.Clear();
        await TellDeskAsync();
        return project;
    }

    /// <summary>
    /// The open project as a project file, as saving would write it, for Export: the UI hands it
    /// to the user as a file on their device. The project and its file stay as they are.
    /// </summary>
    public string ExportProject()
    {
        return ProjectFormat.WriteText(_desk.GetProject().Content);
    }

    /// <summary>
    /// Opens a project file that the user chose on their device (Import), in place of the open
    /// one, as loading does: all memory faders go to 0. The project has a name but no file until
    /// it is saved.
    /// </summary>
    /// <param name="name">What the project is to be called, like the name of the file it came in.</param>
    /// <param name="text">The project file, as saving or Export writes it.</param>
    /// <returns>The project, now open.</returns>
    public async Task<ProjectInfo> ImportProjectFile(string name, string text)
    {
        ProjectContent content;
        try
        {
            content = ProjectFormat.ReadText(text);
        }
        catch (InvalidDataException e)
        {
            throw new DeskException($"The project cannot be imported. {e.Message}");
        }

        ProjectInfo project = _projects.Import(ProjectName.Suggest(name, ImportedProjectName), content);
        _history.Clear();
        await TellDeskAsync();
        return project;
    }

    /// <summary>
    /// Puts a stage that the user chose on their device (Import) in place of the project's: its
    /// lights, kinds of lights, groups and picture, see <see cref="Desk.ReplaceStage"/>. One step
    /// of the history; every UI gets the plan.
    /// </summary>
    /// <param name="text">The stage as a project file has it under "stage".</param>
    public async Task ImportStage(string text)
    {
        StagePlan plan;
        string? picture;
        try
        {
            (plan, picture) = ProjectFormat.ReadStage(text);
        }
        catch (InvalidDataException e)
        {
            throw new DeskException($"The stage cannot be imported. {e.Message}");
        }

        bool changed;
        using (_history.Change("importing a stage"))
            changed = _desk.ReplaceStage(plan, picture);

        if (changed)
            await Clients.All.StageChanged(_desk.Stage);

        await TellProjectAsync();
    }

    /// <summary>
    /// Makes a project out of a theatre console's show file in the USITT ASCII format (".asc"),
    /// see <see cref="ShowImport"/>, and opens it in place of the open one, as loading does: all
    /// memory faders go to 0. Nothing is saved.
    /// </summary>
    /// <param name="fileName">The name of the file; the project is called after it.</param>
    /// <param name="text">What the file holds.</param>
    /// <returns>The project, and what the import made of the file, for the user to read.</returns>
    public async Task<ImportAnswer> ImportProject(string fileName, string text)
    {
        ImportedShow show = ShowImport.Read(fileName, text);
        ProjectInfo project = _projects.Import(show.Name, show.Content);
        _history.Clear();
        await TellDeskAsync();
        return new ImportAnswer(project, show.Report);
    }

    // True when the UI that calls runs on this PC; otherwise a refusal with the message.
    private void RequireLocal(string message)
    {
        if (!IsLocal)
            throw new DeskException(message);
    }

    // The number of a step of the stack, like "2.1", for what the history says.
    private string CueOf(int id)
    {
        return _desk.GetStack().Steps.FirstOrDefault(step => step.Id == id)?.Cue ?? "?";
    }

    // After a change that is more than one fader or one note: every UI gets the whole desk
    // again, also the UI that asked. A changed memory changes the project too. The project may
    // have other universes or other boxes now (undo, load, import): the boxes follow, and the
    // UIs hear of it if that changes what they play.
    private async Task TellDeskAsync()
    {
        _boxes.Refresh();
        await Clients.All.DeskChanged(_desk.GetState());
        await TellProjectAsync();
    }

    // After a change to the steps of the stack: every UI gets the stack again, and where it
    // stands (a step that was taken out may have been the current or the next one). The stack
    // is part of the project.
    private async Task TellStackAsync()
    {
        await Clients.All.StackChanged(_desk.GetStack());
        await TellPlaybackAsync();
        await TellProjectAsync();
    }

    // After Play, Pause, Next, Previous or the live switch: every UI learns at once where the
    // stack stands. The faders it moves follow from the desk clock.
    private Task TellPlaybackAsync()
    {
        return Clients.All.PlaybackChanged(_desk.GetPlayback());
    }

    // The project goes out only when its name, file or "unsaved" mark really changed, and what
    // undo and redo would do only when that changed.
    private async Task TellProjectAsync()
    {
        if (_projects.TakeChange() is { } project)
            await Clients.All.ProjectChanged(project);

        if (_history.TakeChange() is { } history)
            await Clients.All.HistoryChanged(history);
    }
}
