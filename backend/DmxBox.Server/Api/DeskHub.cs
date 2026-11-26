using System.Globalization;
using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Server.Api;

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
internal sealed partial class DeskHub : Hub<IDeskClient>
{
    private readonly Desk _desk;
    private readonly ProjectStore _projects;
    private readonly BoxNetwork _boxes;
    private readonly OutputNetwork _outputs;
    private readonly LanAccess _lan;
    private readonly UndoHistory _history;
    private readonly Autosave _autosave;
    private readonly MidiControl _midi;
    private readonly OscControl _osc;
    private readonly SoundStore _soundFiles;
    private readonly SoundEngine _sound;

    /// <summary>SignalR makes a hub for every call and fills in the parts of the server.</summary>
    /// <param name="desk">The desk.</param>
    /// <param name="projects">The projects folder and the open project.</param>
    /// <param name="boxes">The boxes, for their state and what they play.</param>
    /// <param name="outputs">The outputs besides the boxes: Art-Net, sACN and DMX USB interfaces.</param>
    /// <param name="lan">Whether other devices in the network may use the desk.</param>
    /// <param name="history">Undo and redo for what the project holds.</param>
    /// <param name="autosave">Keeps the work that is not saved yet.</param>
    /// <param name="midi">The MIDI controllers of the PC.</param>
    /// <param name="osc">OSC from other programs.</param>
    /// <param name="soundFiles">The sound files.</param>
    /// <param name="sound">The sound of the PC: its device.</param>
    public DeskHub(
        Desk desk,
        ProjectStore projects,
        BoxNetwork boxes,
        OutputNetwork outputs,
        LanAccess lan,
        UndoHistory history,
        Autosave autosave,
        MidiControl midi,
        OscControl osc,
        SoundStore soundFiles,
        SoundEngine sound)
    {
        _desk = desk;
        _projects = projects;
        _boxes = boxes;
        _outputs = outputs;
        _lan = lan;
        _history = history;
        _autosave = autosave;
        _midi = midi;
        _osc = osc;
        _soundFiles = soundFiles;
        _sound = sound;
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

        // A page on the PC that pressed the keys of the MIDI controllers hands that to the one used before it.
        _midi.Leave(Context.ConnectionId);

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
        return new HelloAnswer(AppInfo.Version, _desk.GetState(), _boxes.Boxes, _projects.Current, _outputs.Outputs)
        {
            Mixer = _sound.MixerNow(),
            Listen = _sound.ListenNow(),
        };
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

    // True when the UI that calls runs on this PC; otherwise a refusal with the message.
    private void RequireLocal(string message)
    {
        if (!IsLocal)
            throw new DeskException(message);
    }

    // After a change that is more than one fader or one note: every UI gets the whole desk
    // again, also the UI that asked. A changed memory changes the project too. The project may
    // have other universes, other boxes or other outputs now (undo, load, import): they follow,
    // and the UIs hear of it if that changes what they play.
    private async Task TellDeskAsync()
    {
        _boxes.Refresh();
        _outputs.Refresh();
        await Clients.All.DeskChanged(_desk.GetState());
        await TellProjectAsync();
    }
}
