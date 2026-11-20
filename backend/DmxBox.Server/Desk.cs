namespace DmxBox.Server;

/// <summary>
/// The fader desk: 512 channel faders, 128 memories with a fader each, a master fader, a
/// blackout switch, a note under every fader, and 20 independent keys (10 for channels, 10 for
/// memories). It holds this state
/// and computes the DMX output from it. It knows nothing about the box, the project files or
/// the UI.
/// </summary>
/// <remarks>
/// <para>
/// Channels and memories are numbered from 1, as on the UI; levels go from 0 to 255. A number
/// outside its range ends in an <see cref="ArgumentOutOfRangeException"/>, anything else that
/// cannot be done in a <see cref="DeskException"/>. Both carry a message for the user.
/// </para>
/// <para>Thread-safe: the hub and the box link use the same desk from different threads.</para>
/// <para>
/// Two things on the desk change with time: a memory can hold a sequence instead of a look
/// (Desk.Sequences.cs), and the stack moves faders by itself (Desk.Stack.cs). The desk has no
/// timer of its own: <see cref="Tick"/> has to be called many times a second, and
/// <see cref="GetOutput"/> gives the output of the moment it is called.
/// </para>
/// <para>
/// While the stack is live (<see cref="SetStackLive"/>) the desk plays a show, and nothing a
/// project holds can be changed: looks, sequences, the steps of the stack, notes and the
/// bindings of the independent keys. Such a request ends in a <see cref="DeskException"/>.
/// The faders, the master, blackout and Clear work as always.
/// </para>
/// </remarks>
internal sealed partial class Desk
{
    /// <summary>Number of DMX channels, each with a fader.</summary>
    public const int ChannelCount = 512;

    /// <summary>Number of memories, each with a fader.</summary>
    public const int MemoryCount = 128;

    /// <summary>The highest level: a fader that is fully up.</summary>
    public const int MaxLevel = 255;

    /// <summary>
    /// Number of independent keys for channels. Each is bound to one channel, which the UI
    /// switches between 0 and full with the key. The desk only remembers the bindings; a UI shows
    /// as many of the keys as its settings say (10 unless its owner chose another number).
    /// </summary>
    public const int ChannelIndependentCount = 32;

    /// <summary>
    /// Number of independent keys for memories. Each is bound to one memory, whose fader the UI
    /// switches between 0 and full with the key. A UI shows as many as its settings say.
    /// </summary>
    public const int MemoryIndependentCount = 32;

    private readonly Lock _lock = new();
    private readonly byte[] _channels = new byte[ChannelCount];
    private readonly byte[] _memoryLevels = new byte[MemoryCount];

    // The look stored in each memory, one level per channel. Null while the memory is empty.
    private readonly byte[]?[] _memories = new byte[]?[MemoryCount];

    // The note under each fader. An empty text where there is none.
    private readonly string[] _channelNotes = NoNotes(ChannelCount);
    private readonly string[] _memoryNotes = NoNotes(MemoryCount);

    // The channel each channel key and the memory each memory key of the independents is bound to.
    private readonly int[] _independents = DefaultIndependents();
    private readonly int[] _memoryIndependents = DefaultMemoryIndependents();

    // What the owner set up in the UI on this PC for the project (its layout and its keyboard
    // shortcuts), as a JSON object, see ProjectSettings. Null while the desk has none. The desk
    // only keeps it; what is in it is the UI's business.
    private string? _settings;

    private byte _master = MaxLevel;
    private bool _blackout;
    private long _outputChanges;
    private long _projectChanges;

    private readonly TimeProvider _time;
    private readonly long _born;

    /// <summary>A desk that goes by the computer's clock.</summary>
    public Desk()
        : this(TimeProvider.System)
    {
    }

    /// <summary>A desk that goes by the given clock. The tests pass one they can set by hand.</summary>
    /// <param name="time">Tells how much time has passed, for the sequences and the stack.</param>
    public Desk(TimeProvider time)
    {
        _time = time;
        _born = time.GetTimestamp();
    }

    // Seconds since the desk was made. Sequences and the stack count in these.
    private double Now => _time.GetElapsedTime(_born).TotalSeconds;

    /// <summary>
    /// True while blackout is on. <see cref="GetOutput"/> does not show it: the box does the
    /// blackout, so that its own blackout button and the one on the desk are the same switch.
    /// </summary>
    public bool Blackout
    {
        get
        {
            lock (_lock)
            {
                return _blackout;
            }
        }
    }

    /// <summary>
    /// Grows with every change that can alter <see cref="GetOutput"/>. Whoever sends the output
    /// to the box remembers the count and sends again once it has moved. Read the count before
    /// the output, not after: then no change can slip through between the two.
    /// </summary>
    public long OutputChanges
    {
        get
        {
            lock (_lock)
            {
                return _outputChanges;
            }
        }
    }

    /// <summary>
    /// Grows with every change of what a project file holds: a stored look, or a note. The
    /// project compares it with the count at its last save to tell whether there is something
    /// to save.
    /// </summary>
    public long ProjectChanges
    {
        get
        {
            lock (_lock)
            {
                return _projectChanges;
            }
        }
    }

    /// <summary>
    /// The settings of the UI on this PC that belong to the project, as a JSON object; null while
    /// the desk has none.
    /// </summary>
    public string? Settings
    {
        get
        {
            lock (_lock)
            {
                return _settings;
            }
        }
    }

    /// <summary>
    /// Takes settings the owner changed in the UI. They belong to the project, which then has
    /// something to save. They can be changed while the stack is live: they are not the show.
    /// </summary>
    /// <param name="settings">The settings, a JSON object, as <see cref="ProjectSettings.Checked"/> keeps them.</param>
    /// <returns>True if they differ from what the desk had.</returns>
    public bool SetSettings(string settings)
    {
        lock (_lock)
        {
            if (_settings == settings)
                return false;

            _settings = settings;
            _projectChanges++;
            return true;
        }
    }

    /// <summary>
    /// Takes the settings a UI on this PC brings along while the desk has none yet, as after a
    /// start without a project: they become the project's, without counting as a change.
    /// </summary>
    /// <param name="settings">The settings, a JSON object, as <see cref="ProjectSettings.Checked"/> keeps them.</param>
    /// <returns>True if the desk took them, false if it has settings already.</returns>
    public bool AdoptSettings(string settings)
    {
        lock (_lock)
        {
            if (_settings is not null)
                return false;

            _settings = settings;
            return true;
        }
    }

    /// <summary>
    /// The bindings of the channel keys of the independents on a new desk, and in a project that
    /// does not say otherwise: key 1 on channel 1, and so on up to key 32 on channel 32.
    /// </summary>
    public static int[] DefaultIndependents()
    {
        return Enumerable.Range(1, ChannelIndependentCount).ToArray();
    }

    /// <summary>The bindings of the memory keys of the independents on a new desk: key 1 on memory 1, and so on.</summary>
    public static int[] DefaultMemoryIndependents()
    {
        return Enumerable.Range(1, MemoryIndependentCount).ToArray();
    }

    /// <summary>
    /// Moves one channel fader by hand. If the stack was moving that fader, the hand wins: the
    /// stack leaves it alone from here on.
    /// </summary>
    /// <param name="channel">Channel number, 1-512.</param>
    /// <param name="level">The fader's new level, 0-255.</param>
    public void SetChannel(int channel, int level)
    {
        RequireChannel(channel);
        RequireLevel(level);

        lock (_lock)
        {
            _moves.Remove(new Fader(Memory: false, channel));
            _channels[channel - 1] = (byte)level;
            _outputChanges++;
        }
    }

    /// <summary>
    /// Moves the fader of one memory by hand. An empty memory has a fader too; it just adds
    /// nothing. A sequence starts when its fader leaves zero. If the stack was moving the
    /// fader, the hand wins.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="level">The fader's new level, 0-255.</param>
    public void SetMemoryLevel(int memory, int level)
    {
        RequireMemory(memory);
        RequireLevel(level);

        lock (_lock)
        {
            _moves.Remove(new Fader(Memory: true, memory));
            MoveMemoryFader(memory, (byte)level);
            _outputChanges++;
        }
    }

    /// <summary>Moves the master fader, which scales the whole output.</summary>
    /// <param name="level">The fader's new level, 0-255. The desk starts with 255.</param>
    public void SetMaster(int level)
    {
        RequireLevel(level);

        lock (_lock)
        {
            _master = (byte)level;
            _outputChanges++;
        }
    }

    /// <summary>Switches blackout on or off. The faders and the memories stay as they are.</summary>
    /// <param name="on">True for dark.</param>
    public void SetBlackout(bool on)
    {
        lock (_lock)
        {
            _blackout = on;
        }
    }

    /// <summary>
    /// Writes the note under a channel fader. Notes are like tape labels: they stay whatever
    /// else happens on the desk, also through Clear and the memory functions.
    /// </summary>
    /// <param name="channel">Channel number, 1-512.</param>
    /// <param name="text">The text as typed. An empty one takes the note away.</param>
    /// <returns>The note as it is stored: the text without the spaces at its ends.</returns>
    /// <exception cref="DeskException">
    /// The text is too long for a note or has a control character in it, or the stack is live.
    /// </exception>
    public string SetChannelNote(int channel, string? text)
    {
        RequireChannel(channel);
        string note = Note.Clean(text);

        lock (_lock)
        {
            RequireUnlocked();
            StoreNote(_channelNotes, channel - 1, note);
        }

        return note;
    }

    /// <summary>Writes the note under the fader of a memory. It belongs to the fader, not to the look: deleting or copying the memory leaves it.</summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="text">The text as typed. An empty one takes the note away.</param>
    /// <returns>The note as it is stored: the text without the spaces at its ends.</returns>
    /// <exception cref="DeskException">
    /// The text is too long for a note or has a control character in it, or the stack is live.
    /// </exception>
    public string SetMemoryNote(int memory, string? text)
    {
        RequireMemory(memory);
        string note = Note.Clean(text);

        lock (_lock)
        {
            RequireUnlocked();
            StoreNote(_memoryNotes, memory - 1, note);
        }

        return note;
    }

    /// <summary>Binds a channel key of the independents to a channel.</summary>
    /// <param name="independent">Number of the key, 1-10.</param>
    /// <param name="channel">The channel the key switches from now on, 1-512.</param>
    /// <exception cref="DeskException">The stack is live.</exception>
    public void SetIndependent(int independent, int channel)
    {
        RequireRange("independent", independent, 1, ChannelIndependentCount);
        RequireChannel(channel);

        lock (_lock)
        {
            Bind(_independents, independent, channel);
        }
    }

    /// <summary>Binds a memory key of the independents to a memory.</summary>
    /// <param name="independent">Number of the key, 1-10.</param>
    /// <param name="memory">The memory whose fader the key switches from now on, 1-128.</param>
    /// <exception cref="DeskException">The stack is live.</exception>
    public void SetMemoryIndependent(int independent, int memory)
    {
        RequireRange("memory independent", independent, 1, MemoryIndependentCount);
        RequireMemory(memory);

        lock (_lock)
        {
            Bind(_memoryIndependents, independent, memory);
        }
    }

    /// <summary>
    /// Brings every channel fader and every memory fader down to 0, and takes the stack off its
    /// step: no step is on, and Play starts with the first one. A stack that is live stays
    /// live. The stored looks and sequences, the steps of the stack, the notes, the bindings of
    /// the independent keys, the master and blackout stay.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            ZeroFaders();
            StopStack();
            _outputChanges++;
        }
    }

    /// <summary>
    /// Stores a copy of what the channel faders show in a memory, replacing what the memory
    /// held, a sequence too. Faders that are all down make a dark look; the memory still
    /// counts as stored.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <exception cref="DeskException">The stack is live.</exception>
    public void SaveMemory(int memory)
    {
        RequireMemory(memory);

        lock (_lock)
        {
            RequireUnlocked();
            _memories[memory - 1] = Copy(_channels);
            _sequences[memory - 1] = null;
            CountLookChange();
        }
    }

    /// <summary>
    /// Sets the channel faders to the look stored in a memory. That is the first half of
    /// editing a memory, so it is refused while the stack is live, like the storing.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <exception cref="DeskException">The memory is empty or holds a sequence, or the stack is live.</exception>
    public void LoadMemory(int memory)
    {
        RequireMemory(memory);

        lock (_lock)
        {
            RequireUnlocked();
            if (_sequences[memory - 1] is not null)
                throw new DeskException($"Memory {memory} holds a sequence. Change it on the Sequences page.");

            StoredLook(memory).CopyTo(_channels, 0);
            _outputChanges++;
        }
    }

    /// <summary>
    /// Stores a copy of what one memory holds, a look or a sequence, in another memory,
    /// replacing what that one held.
    /// </summary>
    /// <param name="from">Number of the memory to copy, 1-128.</param>
    /// <param name="to">Number of the memory that receives the copy, 1-128.</param>
    /// <exception cref="DeskException">
    /// The memory to copy is empty, both numbers are the same, or the stack is live.
    /// </exception>
    public void CopyMemory(int from, int to)
    {
        RequireMemory(from);
        RequireMemory(to);
        if (from == to)
            throw new DeskException($"Memory {from} cannot be copied onto itself.");

        lock (_lock)
        {
            RequireUnlocked();
            if (_sequences[from - 1] is { } sequence)
            {
                _memories[to - 1] = null;
                StoreSequence(to, sequence with { Steps = Copy(sequence.Steps) });
            }
            else
            {
                _memories[to - 1] = Copy(StoredLook(from));
                _sequences[to - 1] = null;
            }

            CountLookChange();
        }
    }

    /// <summary>Empties a memory. Its fader stays where it is. An empty memory simply stays empty.</summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <exception cref="DeskException">The stack is live.</exception>
    public void DeleteMemory(int memory)
    {
        RequireMemory(memory);

        lock (_lock)
        {
            RequireUnlocked();
            if (_memories[memory - 1] is null && _sequences[memory - 1] is null)
                return;

            _memories[memory - 1] = null;
            _sequences[memory - 1] = null;
            CountLookChange();
        }
    }

    /// <summary>
    /// Makes the desk clean for a new project: every channel fader and memory fader down to 0,
    /// every memory empty, an empty stack, no notes, the independent keys back on channels 1 to
    /// 32 and memories 1 to 32. The master and blackout stay: they belong to the evening, not to
    /// the project. So do the settings: a new project starts with the layout in use.
    /// </summary>
    /// <returns><see cref="ProjectChanges"/> right after the change.</returns>
    /// <exception cref="DeskException">The stack is live.</exception>
    public long StartNewProject()
    {
        lock (_lock)
        {
            RequireUnlocked();
            ZeroFaders();
            Array.Clear(_memories);
            Array.Clear(_sequences);
            _stack.Clear();
            StopStack();
            Array.Fill(_channelNotes, "");
            Array.Fill(_memoryNotes, "");
            DefaultIndependents().CopyTo(_independents, 0);
            DefaultMemoryIndependents().CopyTo(_memoryIndependents, 0);
            CountLookChange();
            return _projectChanges;
        }
    }

    /// <summary>
    /// Replaces everything a project file holds at once, which is what loading a project does:
    /// the looks and sequences of all memories, the stack, all notes, the bindings of the
    /// independent keys and the settings (a project without settings keeps those in use). Every
    /// memory fader goes down to 0 and the stack stands before its first step, so that nothing
    /// of the new project appears on stage unasked. The channel faders stay.
    /// </summary>
    /// <param name="project">What the file held, with an entry for every memory, fader and key.</param>
    /// <returns><see cref="ProjectChanges"/> right after the change.</returns>
    /// <exception cref="DeskException">The stack is live.</exception>
    public long ReplaceProject(ProjectContent project)
    {
        RequireWhole(project);

        lock (_lock)
        {
            RequireUnlocked();
            PutProject(project);
            if (project.Settings is not null)
                _settings = project.Settings;

            Array.Clear(_memoryLevels);
            CountLookChange();
            return _projectChanges;
        }
    }

    /// <summary>
    /// Puts back what the project held at an earlier moment, for undo and redo: the looks and
    /// sequences, the stack, the notes and the bindings of the independent keys. Unlike loading,
    /// the faders stay where they are, and so do the settings, which undo leaves alone.
    /// </summary>
    /// <param name="project">The project as <see cref="GetProject"/> gave it then.</param>
    /// <returns><see cref="ProjectChanges"/> right after the change.</returns>
    /// <exception cref="DeskException">The stack is live.</exception>
    public long RestoreProject(ProjectContent project)
    {
        RequireWhole(project);

        lock (_lock)
        {
            RequireUnlocked();
            PutProject(project);
            CountLookChange();
            return _projectChanges;
        }
    }

    /// <summary>
    /// Copies everything a project file holds, together with the change count it belongs to.
    /// </summary>
    public (ProjectContent Content, long Changes) GetProject()
    {
        lock (_lock)
        {
            byte[]?[] looks = new byte[]?[MemoryCount];
            for (int index = 0; index < MemoryCount; index++)
                looks[index] = _memories[index] is { } look ? Copy(look) : null;

            var content = new ProjectContent(looks, Copy(_channelNotes), Copy(_memoryNotes), Copy(_independents))
            {
                MemoryIndependents = Copy(_memoryIndependents),
                Sequences = Copy(_sequences),
                Stack = _stack.Select(entry => new ProjectStep(entry.Cue, entry.Step)).ToArray(),
                Settings = _settings,
            };
            return (content, _projectChanges);
        }
    }

    /// <summary>Copies the whole state for the UI.</summary>
    public DeskState GetState()
    {
        lock (_lock)
        {
            bool[] stored = new bool[MemoryCount];
            bool[] sequence = new bool[MemoryCount];
            for (int index = 0; index < MemoryCount; index++)
            {
                sequence[index] = _sequences[index] is not null;
                stored[index] = sequence[index] || _memories[index] is not null;
            }

            return new DeskState(
                Channels: Array.ConvertAll(_channels, level => (int)level),
                MemoryLevels: Array.ConvertAll(_memoryLevels, level => (int)level),
                MemoryStored: stored,
                Master: _master,
                Blackout: _blackout,
                ChannelNotes: Copy(_channelNotes),
                MemoryNotes: Copy(_memoryNotes),
                Independents: Copy(_independents),
                MemorySequence: sequence,
                MemoryIndependents: Copy(_memoryIndependents),
                Rate: _rate,
                RateOn: _rateOn);
        }
    }

    /// <summary>
    /// Copies the looks of all memories that hold one, for the UI: it shows what a memory
    /// holds, and which channels a memory fader raises.
    /// </summary>
    /// <returns>For each stored memory, by its number (1-128): 512 levels, channel 1 at index 0.</returns>
    public Dictionary<int, int[]> GetMemoryLooks()
    {
        lock (_lock)
        {
            var looks = new Dictionary<int, int[]>();
            for (int index = 0; index < MemoryCount; index++)
            {
                if (_memories[index] is { } look)
                    looks[index + 1] = Array.ConvertAll(look, level => (int)level);
            }

            return looks;
        }
    }

    /// <summary>
    /// Computes the levels the box has to send at this moment. For every channel the highest
    /// wins: its own fader, or a memory that holds the channel, scaled by that memory's fader.
    /// A memory with a sequence gives what its sequence gives right now. The master then
    /// scales the result. Blackout is left out, see <see cref="Blackout"/>.
    /// </summary>
    /// <returns>512 levels; channel 1 is at index 0.</returns>
    public byte[] GetOutput()
    {
        lock (_lock)
        {
            byte[] output = Copy(_channels);
            for (int index = 0; index < MemoryCount; index++)
            {
                // A memory whose fader is down adds nothing, like an empty one.
                if (_memories[index] is { } look && _memoryLevels[index] > 0)
                    RaiseTo(output, look, _memoryLevels[index]);
            }

            AddSequences(output, Now, stepsOn: null);

            for (int channel = 0; channel < ChannelCount; channel++)
                output[channel] = Scale(output[channel], _master);

            return output;
        }
    }

    // Highest takes precedence: a memory can raise a channel, never lower it.
    private static void RaiseTo(byte[] output, byte[] look, byte memoryLevel)
    {
        for (int channel = 0; channel < ChannelCount; channel++)
            output[channel] = Math.Max(output[channel], Scale(look[channel], memoryLevel));
    }

    // level x factor / 255, rounded to the nearest whole number. The box's own master works the same way.
    private static byte Scale(byte level, byte factor)
    {
        return (byte)((level * factor + 127) / 255);
    }

    private static T[] Copy<T>(T[] items)
    {
        return (T[])items.Clone();
    }

    private static string[] NoNotes(int count)
    {
        string[] notes = new string[count];
        Array.Fill(notes, "");
        return notes;
    }

    // Call with the lock held.
    private byte[] StoredLook(int memory)
    {
        return _memories[memory - 1] ?? throw new DeskException($"Memory {memory} is empty.");
    }

    // Call with the lock held. Writing the text that is there already is no change: the
    // project must not count as unsaved because of it.
    private void StoreNote(string[] notes, int index, string note)
    {
        if (notes[index] == note)
            return;

        notes[index] = note;
        _projectChanges++;
    }

    // Call with the lock held, after a stored look changed. A stored look is part of the
    // output while its memory's fader is up, so the output counts as changed too.
    private void CountLookChange()
    {
        _projectChanges++;
        _outputChanges++;
    }

    // Call with the lock held. The looks and sequences, the stack, the notes and the bindings of
    // the independent keys of a project take the place of the desk's. The project's own arrays
    // are copied where the desk changes its arrays in place, so that what the project came from
    // (a file, or an earlier moment kept for undo) stays as it was.
    private void PutProject(ProjectContent project)
    {
        for (int index = 0; index < MemoryCount; index++)
        {
            _memories[index] = project.Looks[index] is { } look ? Copy(look) : null;
            _sequences[index] = project.Sequences[index];
        }

        ReplaceStack(project.Stack);
        project.ChannelNotes.CopyTo(_channelNotes, 0);
        project.MemoryNotes.CopyTo(_memoryNotes, 0);
        project.Independents.CopyTo(_independents, 0);
        project.MemoryIndependents.CopyTo(_memoryIndependents, 0);
    }

    // Call with the lock held. Every channel fader and memory fader goes to 0. The UIs are sent
    // the whole desk after this, so the fader moves of the stack they have not heard of yet
    // are no news any more.
    private void ZeroFaders()
    {
        Array.Clear(_channels);
        Array.Clear(_memoryLevels);
        _moved.Clear();
    }

    // Call with the lock held. Binding a key to what it has already is no change for the project.
    private void Bind(int[] keys, int key, int number)
    {
        RequireUnlocked();
        if (keys[key - 1] == number)
            return;

        keys[key - 1] = number;
        _projectChanges++;
    }

    // Call with the lock held, before anything a project holds is changed. While the stack is
    // live the desk plays a show, and what the show is made of must not change under it.
    private void RequireUnlocked()
    {
        if (_live)
            throw new DeskException("The stack is live, so nothing that is stored can be changed. Switch Stack off first.");
    }

    // A project that is not whole is a fault in this program, not a mistake of the user:
    // the reader of project files always hands over an entry for every memory and fader.
    private static void RequireWhole(ProjectContent project)
    {
        if (project.Looks.Length != MemoryCount)
            throw new ArgumentException($"There must be {MemoryCount} looks, one per memory.", nameof(project));
        if (project.Looks.Any(look => look is not null && look.Length != ChannelCount))
            throw new ArgumentException($"Every look must have {ChannelCount} levels.", nameof(project));
        if (project.ChannelNotes.Length != ChannelCount || project.MemoryNotes.Length != MemoryCount)
            throw new ArgumentException("There must be a note, or an empty text, for every fader.", nameof(project));
        if (project.Independents.Length != ChannelIndependentCount)
            throw new ArgumentException($"There must be {ChannelIndependentCount} channel independents.", nameof(project));
        if (project.MemoryIndependents.Length != MemoryIndependentCount)
            throw new ArgumentException($"There must be {MemoryIndependentCount} memory independents.", nameof(project));
        if (project.Sequences.Length != MemoryCount)
            throw new ArgumentException($"There must be {MemoryCount} entries for sequences, one per memory.", nameof(project));
        for (int index = 0; index < MemoryCount; index++)
        {
            if (project.Looks[index] is not null && project.Sequences[index] is not null)
                throw new ArgumentException($"Memory {index + 1} cannot hold a look and a sequence.", nameof(project));
        }
    }

    private static void RequireChannel(int channel)
    {
        RequireRange("channel", channel, 1, ChannelCount);
    }

    private static void RequireMemory(int memory)
    {
        RequireRange("memory", memory, 1, MemoryCount);
    }

    private static void RequireLevel(int level)
    {
        RequireRange("level", level, 0, MaxLevel);
    }

    // These numbers come straight from the UI, and the message goes back there as it is. So the
    // exception gets no parameter name: .NET would append "(Parameter 'channel')" to the message.
    private static void RequireRange(string what, int value, int min, int max)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(
                paramName: null, message: $"The {what} must be from {min} to {max}, not {value}.");
        }
    }
}
