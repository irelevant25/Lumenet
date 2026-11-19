using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Server;

/// <summary>
/// What a UI can ask of the server, at /hub/desk. Every method does its work on the desk or
/// the project and then tells the UIs that have to know; <see cref="IDeskClient"/> lists
/// those messages.
/// </summary>
/// <remarks>
/// A request that cannot be carried out ends in an exception from the desk or the project
/// store. <see cref="UserErrorFilter"/> hands its message to the UI that asked, and writes
/// the line about it in the log.
/// </remarks>
internal sealed class DeskHub : Hub<IDeskClient>
{
    private readonly Desk _desk;
    private readonly ProjectStore _projects;
    private readonly BoxLink _boxLink;
    private readonly LanAccess _lan;

    /// <summary>SignalR makes a hub for every call and fills in the parts of the server.</summary>
    /// <param name="desk">The desk.</param>
    /// <param name="projects">The projects folder and the open project.</param>
    /// <param name="boxLink">The link to the box, for its state.</param>
    /// <param name="lan">Whether other devices in the network may use the desk.</param>
    public DeskHub(Desk desk, ProjectStore projects, BoxLink boxLink, LanAccess lan)
    {
        _desk = desk;
        _projects = projects;
        _boxLink = boxLink;
        _lan = lan;
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
        string note = _desk.SetChannelNote(channel, text);
        await Clients.Others.ChannelNoteChanged(channel, note);
        await TellProjectAsync();
    }

    /// <summary>Writes the note under the fader of a memory. An empty text takes the note away.</summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="text">The text as typed: at most 24 characters, not counting spaces at its ends.</param>
    public async Task SetMemoryNote(int memory, string text)
    {
        string note = _desk.SetMemoryNote(memory, text);
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
        _desk.CopyMemory(from, to);
        return TellDeskAsync();
    }

    /// <summary>Empties a memory.</summary>
    /// <param name="memory">Memory number, 1-128.</param>
    public Task DeleteMemory(int memory)
    {
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
        double kept = _desk.SetSequenceSpeed(memory, speed);
        await Clients.Others.SequenceSpeedChanged(memory, kept);
        await TellProjectAsync();
    }

    /// <summary>
    /// Puts one more step at the end of the sequence of a memory. An empty memory gets a new
    /// sequence, played in a loop.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="step">The step.</param>
    public Task AddSequenceStep(int memory, Step step)
    {
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
        int id = _desk.AddStackStep(step);
        await TellStackAsync();
        return id;
    }

    /// <summary>Changes what a step of the stack stands for, its value or its times.</summary>
    /// <param name="id">The name of the step.</param>
    /// <param name="step">The step as it is to be.</param>
    public Task UpdateStackStep(int id, Step step)
    {
        _desk.UpdateStackStep(id, step);
        return TellStackAsync();
    }

    /// <summary>Takes a step out of the stack.</summary>
    /// <param name="id">The name of the step.</param>
    public Task DeleteStackStep(int id)
    {
        _desk.DeleteStackStep(id);
        return TellStackAsync();
    }

    /// <summary>Puts a step of the stack right after another one; it gets a number that fits there.</summary>
    /// <param name="id">The name of the step to move.</param>
    /// <param name="afterId">The step it goes behind; null for the very top.</param>
    public Task MoveStackStep(int id, int? afterId)
    {
        _desk.MoveStackStep(id, afterId);
        return TellStackAsync();
    }

    /// <summary>Gives a step of the stack the number its owner typed.</summary>
    /// <param name="id">The name of the step.</param>
    /// <param name="cue">The number as text: "3", "3.0", "2.1".</param>
    public Task RenumberStackStep(int id, string cue)
    {
        _desk.RenumberStackStep(id, cue);
        return TellStackAsync();
    }

    /// <summary>
    /// Switches the stack live or off. Going live brings every fader down to 0, goes to the
    /// first step and locks everything that is stored; switching it off stops the stack and
    /// leaves the faders where they are.
    /// </summary>
    /// <param name="on">True for live.</param>
    public async Task SetStackLive(bool on)
    {
        _desk.SetStackLive(on);
        if (on)
            await Clients.All.DeskChanged(_desk.GetState());   // every fader went to 0

        await TellPlaybackAsync();
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
        await TellDeskAsync();
        return new ImportAnswer(project, show.Report);
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

    // The project goes out only when its name, file or "unsaved" mark really changed.
    private async Task TellProjectAsync()
    {
        if (_projects.TakeChange() is { } project)
            await Clients.All.ProjectChanged(project);
    }
}
