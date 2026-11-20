using System.Globalization;
using System.Text.Json;
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
    private readonly Desk _desk;
    private readonly ProjectStore _projects;
    private readonly BoxLink _boxLink;
    private readonly LanAccess _lan;
    private readonly UndoHistory _history;
    private readonly Autosave _autosave;

    /// <summary>SignalR makes a hub for every call and fills in the parts of the server.</summary>
    /// <param name="desk">The desk.</param>
    /// <param name="projects">The projects folder and the open project.</param>
    /// <param name="boxLink">The link to the box, for its state.</param>
    /// <param name="lan">Whether other devices in the network may use the desk.</param>
    /// <param name="history">Undo and redo for what the project holds.</param>
    /// <param name="autosave">Keeps the work that is not saved yet.</param>
    public DeskHub(Desk desk, ProjectStore projects, BoxLink boxLink, LanAccess lan, UndoHistory history, Autosave autosave)
    {
        _desk = desk;
        _projects = projects;
        _boxLink = boxLink;
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
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>The first call of a UI: everything it needs to draw itself.</summary>
    public HelloAnswer Hello()
    {
        return new HelloAnswer(AppInfo.Version, _desk.GetState(), _boxLink.Status, _projects.Current);
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
        using (_history.Change($"the note of channel {channel}"))
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
    /// Sets the rate, a speed master for all sequences (see <see cref="Desk.SetRate"/>). The UI
    /// turns its wheel this way, like a fader: the other UIs hear of it, this one does not. It is
    /// no part of the project, so the history leaves it alone.
    /// </summary>
    /// <param name="on">True: the rate counts.</param>
    /// <param name="rate">From 0.1 to 10.</param>
    public Task SetRate(bool on, double rate)
    {
        _desk.SetRate(on, rate);
        return Clients.Others.RateChanged(on, rate);
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

    /// <summary>
    /// The settings of the UI on the PC that belong to the project (its layout and keyboard
    /// shortcuts), as the UI wrote them; null while the desk has none.
    /// </summary>
    public JsonElement? GetSettings()
    {
        return ProjectSettings.Element(_desk.Settings);
    }

    /// <summary>
    /// The owner changed settings of the project in the UI on the PC. The other UIs hear of it;
    /// the project has something to save. Only a UI on this PC can do this: phones and tablets
    /// keep settings of their own.
    /// </summary>
    /// <param name="settings">The settings, a JSON object.</param>
    public async Task SetSettings(JsonElement settings)
    {
        RequireLocal("Only the PC keeps its settings in the project.");
        if (_desk.SetSettings(ProjectSettings.Checked(settings)))
            await Clients.Others.SettingsChanged(ProjectSettings.Element(_desk.Settings));

        await TellProjectAsync();
    }

    /// <summary>
    /// A UI on the PC brings its settings along while the desk has none, as after a start
    /// without a project: they become the project's, without making it unsaved.
    /// </summary>
    /// <param name="settings">The settings, a JSON object.</param>
    /// <returns>True if the desk took them, false if it has settings already (then the UI asks for those).</returns>
    public async Task<bool> AdoptSettings(JsonElement settings)
    {
        RequireLocal("Only the PC keeps its settings in the project.");
        bool taken = _desk.AdoptSettings(ProjectSettings.Checked(settings));
        if (taken)
            await Clients.Others.SettingsChanged(ProjectSettings.Element(_desk.Settings));

        return taken;
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

    /// <summary>Lists the projects that can be loaded, the newest first.</summary>
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
        await Clients.All.SettingsChanged(ProjectSettings.Element(_desk.Settings));   // the project's, or those in use
        await TellDeskAsync();
        return project;
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
    // again, also the UI that asked. A changed memory changes the project too.
    private async Task TellDeskAsync()
    {
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
