namespace DmxBox.Server.Core;

/// <summary>
/// The fader desk: a channel fader for every channel of its universes (512 each, 1 to 8
/// universes), 128 memories with a fader each, a master fader, a blackout switch, a note under
/// every fader, and the independent keys. It holds this state
/// and computes the DMX output from it. It knows nothing about the box, the project files or
/// the UI.
/// </summary>
/// <remarks>
/// <para>
/// Channels and memories are numbered from 1, as on the UI; levels go from 0 to 255. A number
/// outside its range ends in an <see cref="ArgumentOutOfRangeException"/>, anything else that
/// cannot be done in a <see cref="DeskException"/>. Both carry a message for the user.
/// </para>
/// <para>
/// The channels of all universes are counted on: universe 1 has the channels 1 to 512, universe 2
/// the channels 513 to 1024, and so on (the UI writes them as "1 (U2)" if it likes). How many
/// universes there are is the project's (<see cref="Universes"/>); the desk keeps room for the
/// most there can be, so that a change of the number moves nothing.
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
    /// <summary>The channels of one DMX universe: one cable, one output of a box.</summary>
    public const int UniverseSize = 512;

    /// <summary>The most universes a project can have.</summary>
    public const int MaxUniverses = 8;

    /// <summary>The most channels the desk can have: all channels of the most universes.</summary>
    public const int MaxChannels = UniverseSize * MaxUniverses;

    /// <summary>
    /// A channel as the user reads it: its number 1 to 512 in its universe, and the universe.
    /// Channel 625 is "113 (U2)". Inside, channels are numbered on through the universes.
    /// </summary>
    public static string ChannelLabel(int channel) =>
        $"{(channel - 1) % UniverseSize + 1} (U{(channel - 1) / UniverseSize + 1})";

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

    // The channel arrays have room for every channel there can be (MaxChannels); only the first
    // Channels of them are in use. Those of universes the project does not have stay at 0 or empty.
    private readonly byte[] _channels = new byte[MaxChannels];
    private readonly byte[] _memoryLevels = new byte[MemoryCount];
    private int _universes = 1;

    // The look stored in each memory, one level per channel (MaxChannels of them). Null while the memory is empty.
    private readonly byte[]?[] _memories = new byte[]?[MemoryCount];

    // The note under each fader. An empty text where there is none.
    private readonly string[] _channelNotes = NoNotes(MaxChannels);
    private readonly string[] _memoryNotes = NoNotes(MemoryCount);

    // The channel each channel key and the memory each memory key of the independents is bound to.
    private readonly int[] _independents = DefaultIndependents();
    private readonly int[] _memoryIndependents = DefaultMemoryIndependents();

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

    // The channels in use now. Call with the lock held.
    private int Channels => _universes * UniverseSize;

    /// <summary>How many universes the project has, 1 to <see cref="MaxUniverses"/>.</summary>
    public int Universes
    {
        get
        {
            lock (_lock)
            {
                return _universes;
            }
        }
    }

    /// <summary>How many channels there are now: 512 for every universe of the project.</summary>
    public int ChannelCount
    {
        get
        {
            lock (_lock)
            {
                return Channels;
            }
        }
    }

    /// <summary>
    /// True while blackout is on: <see cref="GetOutput"/> gives every channel that gives light 0,
    /// and leaves the attributes of the lights (a pan, a tilt) where they are, so that no moving
    /// head moves. The blackout button of the box switches it too, see <see cref="BoxLink"/>.
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
    /// <param name="channel">Channel number, 1 to <see cref="ChannelCount"/>.</param>
    /// <param name="level">The fader's new level, 0-255.</param>
    public void SetChannel(int channel, int level)
    {
        RequireChannel(channel);
        RequireLevel(level);

        lock (_lock)
        {
            _moves.Remove(new Fader(Memory: false, channel));
            LeaveCrossfade(new Fader(Memory: false, channel));
            _channels[channel - 1] = (byte)level;
            ClaimChannel(channel);
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
            LeaveCrossfade(new Fader(Memory: true, memory));
            MoveMemoryFader(memory, (byte)level);
            _outputChanges++;
        }
    }

    /// <summary>Moves the master fader, which scales the channels that give light (not the attributes of the lights).</summary>
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
            if (_blackout == on)
                return;

            _blackout = on;
            _outputChanges++;
        }
    }

    /// <summary>Switches blackout over: on when it was off, off when it was on. The blackout button of a box does this.</summary>
    /// <returns>True when blackout is on now.</returns>
    public bool ToggleBlackout()
    {
        lock (_lock)
        {
            _blackout = !_blackout;
            _outputChanges++;
            return _blackout;
        }
    }

    /// <summary>
    /// Writes the note under a channel fader. Notes are like tape labels: they stay whatever
    /// else happens on the desk, also through Clear and the memory functions.
    /// </summary>
    /// <param name="channel">Channel number, 1 to <see cref="ChannelCount"/>.</param>
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
    /// <param name="channel">The channel the key switches from now on, 1 to <see cref="ChannelCount"/>.</param>
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
            long claim = ++_claims;
            Array.Fill(_channelClaims, claim);
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
    /// Changes the level one channel has in the look of a memory, as the table of the Memories
    /// view does. An empty memory gets a look of its own with only that channel (a level of 0
    /// leaves it empty); a memory that holds a sequence is refused.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="channel">Channel number, 1 to <see cref="ChannelCount"/>.</param>
    /// <param name="level">The level the look gives the channel, 0-255.</param>
    /// <returns>True when the memory changed.</returns>
    /// <exception cref="DeskException">The memory holds a sequence, or the stack is live.</exception>
    public bool SetMemoryChannel(int memory, int channel, int level)
    {
        RequireMemory(memory);
        RequireChannel(channel);
        RequireLevel(level);

        lock (_lock)
        {
            RequireUnlocked();
            if (_sequences[memory - 1] is not null)
                throw new DeskException($"Memory {memory} holds a sequence. Change it on the Sequences page.");

            byte[]? look = _memories[memory - 1];
            if (look is null ? level == 0 : look[channel - 1] == level)
                return false; // the level it has already: the project must not count as unsaved

            look ??= _memories[memory - 1] = new byte[MaxChannels];
            look[channel - 1] = (byte)level;
            CountLookChange();
            return true;
        }
    }

    /// <summary>
    /// Makes the desk clean for a new project: every channel fader and memory fader down to 0,
    /// every memory empty, an empty stack, no notes under the faders and none of the project, the
    /// independent keys back on channels 1 to 32 and memories 1 to 32. The master and blackout
    /// stay: they belong to the evening, not to the project. The stage plan belongs to the
    /// project: a new one has an empty stage, without lights and picture.
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
            PutEffects([], DefaultEffectIndependents());
            PutSounds([], DefaultSoundIndependents());
            PutStage(StagePlan.Empty);
            _stagePicture = null;
            _notes = "";
            CountLookChange();
            return _projectChanges;
        }
    }

    /// <summary>
    /// Replaces everything a project file holds at once, which is what loading a project does:
    /// the looks and sequences of all memories, the stack, all notes, the bindings of the
    /// independent keys and the stage plan (a project without one has an empty stage). Every
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
            Array.Clear(_memoryLevels);
            StopEffects();
            StopSounds();
            CountLookChange();
            return _projectChanges;
        }
    }

    /// <summary>
    /// Puts back what the project held at an earlier moment, for undo and redo: the looks and
    /// sequences, the stack, the notes, the bindings of the independent keys and the stage plan.
    /// Unlike loading, the faders stay where they are.
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
            // The looks as long as the project's channels go.
            byte[]?[] looks = new byte[]?[MemoryCount];
            for (int index = 0; index < MemoryCount; index++)
                looks[index] = _memories[index] is { } look ? look[..Channels] : null;

            var content = new ProjectContent(looks, _channelNotes[..Channels], Copy(_memoryNotes), Copy(_independents))
            {
                Universes = _universes,
                Boxes = _boxes,
                Outputs = _outputs,
                MemoryIndependents = Copy(_memoryIndependents),
                Sequences = Copy(_sequences),
                Effects = Copy(_effects),
                EffectIndependents = Copy(_effectIndependents),
                Sounds = Copy(_sounds),
                SoundIndependents = Copy(_soundIndependents),
                Stack = _stack.Select(entry => new ProjectStep(entry.Cue, entry.Step) { Releases = ReleasedCue(entry.Step) }).ToArray(),
                Stage = _stage,
                StagePicture = _stagePicture,
                Notes = _notes,
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

            int channels = Channels;
            return new DeskState(
                Channels: Array.ConvertAll(_channels[..channels], level => (int)level),
                MemoryLevels: Array.ConvertAll(_memoryLevels, level => (int)level),
                MemoryStored: stored,
                Master: _master,
                Blackout: _blackout,
                ChannelNotes: _channelNotes[..channels],
                MemoryNotes: Copy(_memoryNotes),
                Independents: Copy(_independents),
                MemorySequence: sequence,
                MemoryIndependents: Copy(_memoryIndependents),
                Bpm: _bpm,
                RateOn: _rateOn,
                Solo: SoloNow(),
                ChannelClaims: _channelClaims[..channels],
                MemoryClaims: Copy(_memoryClaims))
            {
                Universes = _universes,
                EffectIndependents = Copy(_effectIndependents),
                SoundIndependents = Copy(_soundIndependents),
                Effects = EffectPlaybacksNow(Now),
                Sounds = SoundPlaybacksNow(),
                SoundDecibels = _soundDecibels,
                SoundMute = _soundMute,
            };
        }
    }

    /// <summary>
    /// Copies the looks of all memories that hold one, for the UI: it shows what a memory
    /// holds, and which channels a memory fader raises.
    /// </summary>
    /// <returns>For each stored memory, by its number (1-128): a level for every channel, channel 1 at index 0.</returns>
    public Dictionary<int, int[]> GetMemoryLooks()
    {
        lock (_lock)
        {
            var looks = new Dictionary<int, int[]>();
            for (int index = 0; index < MemoryCount; index++)
            {
                if (_memories[index] is { } look)
                    looks[index + 1] = Array.ConvertAll(look[..Channels], level => (int)level);
            }

            return looks;
        }
    }

    /// <summary>
    /// Computes the levels the box has to send at this moment. For every channel that gives
    /// light the highest wins: its own fader, or a memory that holds the channel, scaled by that
    /// memory's fader. While the stack goes from one step to the next, the faders of the two
    /// steps give their light through its crossfade instead (Desk.Stack.cs), which is the highest
    /// of the rest too. A memory with a sequence gives what its sequence gives right now. A solo
    /// changes that (see Desk.Solo.cs): with keys held with SOLO UP only their faders count, at
    /// full; with keys held with SOLO DOWN their faders give nothing. The running effects work on
    /// that light (Desk.Effects.cs). Then each light is held to its limit, the master scales the
    /// result, and blackout makes it dark. The attributes of the lights (a pan, a tilt) come from
    /// the fader that took them last, untouched by all of that, see Desk.Attributes.cs; a movement
    /// effect moves a head from there.
    /// </summary>
    /// <returns>A level for every channel of the project's universes; channel 1 is at index 0.</returns>
    public byte[] GetOutput()
    {
        lock (_lock)
        {
            double now = Now;
            (HashSet<Fader> up, HashSet<Fader> down) = SoloFaders();
            EffectFrame? effects = EffectFrameAt(now, down);
            byte[] output;
            if (up.Count > 0)
            {
                // Only the held keys shine: the effects give no light, but heads still move.
                output = SoloUpOutput(up, now);
            }
            else
            {
                output = FadersOutput(now, down);
                effects?.PutLight(output, touched: null);

                // A channel held with SOLO DOWN is dark, whatever holds it up, an effect too.
                foreach (Fader fader in down.Where(fader => fader.Channel))
                    output[fader.Number - 1] = 0;
            }

            FinishLight(output);
            if (_roles.AttributeLights.Length > 0)
                PutAttributes(output, now, down);

            effects?.PutAttributes(output, touched: null);
            return output;
        }
    }

    // Call with the lock held. The light the faders give, highest wins: the channel faders, the
    // memories, the stack's crossfade and the sequences; the memories in "down" (held with SOLO
    // DOWN) give nothing.
    private byte[] FadersOutput(double now, HashSet<Fader> down)
    {
        // While the stack crossfades, the faders taking part give their light through the
        // crossfade (Desk.Stack.cs), not each on its own. A key held with SOLO DOWN on one of
        // them stops that: then every fader counts on its own again.
        Crossfade? cross = _crossfade is { } running && !running.Faders.Keys.Any(down.Contains) ? running : null;
        byte[] output = _channels[..Channels];
        foreach (Fader fader in cross?.Faders.Keys.Where(fader => fader.Channel && fader.Number - 1 < Channels) ?? [])
            output[fader.Number - 1] = 0;

        for (int index = 0; index < MemoryCount; index++)
        {
            // A memory whose fader is down adds nothing, like an empty one.
            var fader = new Fader(true, index + 1);
            if (_memories[index] is { } look && _memoryLevels[index] > 0 && !down.Contains(fader)
                && cross?.Faders.ContainsKey(fader) != true)
            {
                RaiseTo(output, look, _memoryLevels[index]);
            }
        }

        for (int channel = 0; cross is not null && channel < output.Length && channel < cross.From.Length; channel++)
            output[channel] = Math.Max(output[channel], cross.ValueAt(channel));

        AddSequences(output, now, stepsOn: null, leftOut: down);
        return output;
    }

    // Highest takes precedence: a memory can raise a channel, never lower it. As far as the output goes.
    private static void RaiseTo(byte[] output, byte[] look, byte memoryLevel)
    {
        for (int channel = 0; channel < output.Length; channel++)
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

    // Call with the lock held. The looks and sequences, the stack, the notes, the bindings of the
    // independent keys and the stage plan with its picture of a project take the place of the
    // desk's; a project without a plan (a file from before plans, an imported show) has an empty
    // stage. The project's own arrays are copied where the desk changes its arrays in place, so
    // that what the project came from (a file, or an earlier moment kept for undo) stays as it
    // was. The plan is never changed in place.
    private void PutProject(ProjectContent project)
    {
        _universes = project.Universes;
        _boxes = project.Boxes;
        _outputs = project.Outputs;
        PutStage(project.Stage ?? StagePlan.Empty);
        _stagePicture = project.Stage is null ? null : project.StagePicture;
        _notes = project.Notes;

        for (int index = 0; index < MemoryCount; index++)
        {
            _memories[index] = project.Looks[index] is { } look ? Widened(look) : null;
            _sequences[index] = project.Sequences[index];
        }

        ReplaceStack(project.Stack);
        Array.Fill(_channelNotes, "");
        project.ChannelNotes.CopyTo(_channelNotes, 0);
        project.MemoryNotes.CopyTo(_memoryNotes, 0);
        project.Independents.CopyTo(_independents, 0);
        project.MemoryIndependents.CopyTo(_memoryIndependents, 0);
        PutEffects(project.Effects, project.EffectIndependents);
        PutSounds(project.Sounds, project.SoundIndependents);
        ClearAbove(Channels);
    }

    // A look of the project's channels, with room for all there can be.
    private static byte[] Widened(byte[] look)
    {
        byte[] wide = new byte[MaxChannels];
        look.CopyTo(wide, 0);
        return wide;
    }

    // Call with the lock held. The faders, claims and notes of channels that the project does not
    // have go back to nothing, so that they do not come back when universes are added again.
    private void ClearAbove(int channels)
    {
        Array.Clear(_channels, channels, MaxChannels - channels);
        Array.Clear(_channelClaims, channels, MaxChannels - channels);
        Array.Fill(_channelNotes, "", channels, MaxChannels - channels);
        foreach (byte[]? look in _memories)
        {
            if (look is not null)
                Array.Clear(look, channels, MaxChannels - channels);
        }
    }

    /// <summary>
    /// Changes how many universes the project has. More universes add channels, at 0 and without
    /// notes. Fewer take the channels of the universes that go away, with the levels the memories
    /// hold there and the notes of those channels (Undo brings them back); refused while a step,
    /// a light of the stage plan or a channel key of the independents is on such a channel.
    /// </summary>
    /// <param name="universes">1 to <see cref="MaxUniverses"/>.</param>
    /// <returns><see cref="ProjectChanges"/> right after the change.</returns>
    /// <exception cref="DeskException">Something is on a channel that would go, or the stack is live.</exception>
    public long SetUniverses(int universes)
    {
        RequireRange("number of universes", universes, 1, MaxUniverses);

        lock (_lock)
        {
            RequireUnlocked();
            if (universes == _universes)
                return _projectChanges;

            int channels = universes * UniverseSize;
            if (universes < _universes)
            {
                string? use = UseAbove(
                    channels, _sequences, _stack.Select(entry => (entry.Cue, entry.Step)), _stage, _independents);
                if (use is not null)
                {
                    string going = universes + 1 == _universes
                        ? $"Universe {_universes} is"
                        : $"Universes {universes + 1} to {_universes} are";
                    throw new DeskException($"{going} still in use: {use}. Take it out of there first.");
                }

                ClearAbove(channels);
            }

            _universes = universes;
            CountLookChange();
            return _projectChanges;
        }
    }

    // The first thing of a project that is on a channel above the given number, said for the
    // user: "step 2 of the stack is on channel 7 (U2)"; null if nothing is. Looks and notes do
    // not count: they simply end there.
    internal static string? UseAbove(
        int channels, Sequence?[] sequences, IEnumerable<(CueNumber Cue, Step Step)> stack, StagePlan stage, int[] independents)
    {
        for (int memory = 0; memory < sequences.Length; memory++)
        {
            Step[] steps = sequences[memory]?.Steps ?? [];
            for (int index = 0; index < steps.Length; index++)
            {
                if (steps[index].Target == Step.Channel && steps[index].Number > channels)
                    return $"step {index + 1} of the sequence of memory {memory + 1} is on channel {ChannelLabel(steps[index].Number)}";
            }
        }

        foreach ((CueNumber cue, Step step) in stack)
        {
            if (step.Target == Step.Channel && step.Number > channels)
                return $"step {cue} of the stack is on channel {ChannelLabel(step.Number)}";
        }

        foreach (StageLight light in stage.Lights)
        {
            if (light.Address + light.Channels.Length - 1 > channels)
            {
                string which = light.Name.Length > 0 ? $"the light \"{light.Name}\"" : "a light";
                return $"{which} of the stage is at address {ChannelLabel(light.Address)}";
            }
        }

        for (int key = 0; key < independents.Length; key++)
        {
            if (independents[key] > channels)
                return $"channel key {key + 1} of the independents is on channel {ChannelLabel(independents[key])}";
        }

        return null;
    }

    // "1 universe", "2 universes".
    internal static string UniversesSaid(int universes) => universes == 1 ? "1 universe" : $"{universes} universes";

    // Call with the lock held: a channel step must be on a channel of the project's universes.
    private void RequireStepFits(Step step)
    {
        if (step.Target == Step.Channel && step.Number > Channels)
        {
            throw new DeskException(
                $"Channel {ChannelLabel(step.Number)} is not in the project, which has {UniversesSaid(_universes)}. " +
                "Add universes in Settings first.");
        }
    }

    // Call with the lock held. Every channel fader, memory fader and effect fader goes to 0, so
    // every effect stops. The UIs are sent the whole desk after this, so the fader moves of the
    // stack they have not heard of yet are no news any more.
    private void ZeroFaders()
    {
        Array.Clear(_channels);
        Array.Clear(_memoryLevels);
        StopEffects();
        StopSounds();
        _moved.Clear();
        _crossfade = null;
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
        if (project.Universes < 1 || project.Universes > MaxUniverses)
            throw new ArgumentException($"A project has 1 to {MaxUniverses} universes, not {project.Universes}.", nameof(project));

        int channels = project.Universes * UniverseSize;
        if (project.Looks.Length != MemoryCount)
            throw new ArgumentException($"There must be {MemoryCount} looks, one per memory.", nameof(project));
        if (project.Looks.Any(look => look is not null && look.Length != channels))
            throw new ArgumentException($"Every look must have {channels} levels.", nameof(project));
        if (project.ChannelNotes.Length != channels || project.MemoryNotes.Length != MemoryCount)
            throw new ArgumentException("There must be a note, or an empty text, for every fader.", nameof(project));
        if (UseAbove(channels, project.Sequences, project.Stack.Select(step => (step.Cue, step.Step)), project.Stage ?? StagePlan.Empty, project.Independents)
            is { } use)
        {
            throw new ArgumentException($"In a project of {UniversesSaid(project.Universes)}, {use}.", nameof(project));
        }
        if (project.Independents.Length != ChannelIndependentCount)
            throw new ArgumentException($"There must be {ChannelIndependentCount} channel independents.", nameof(project));
        if (project.MemoryIndependents.Length != MemoryIndependentCount)
            throw new ArgumentException($"There must be {MemoryIndependentCount} memory independents.", nameof(project));
        if (project.Sounds.Length != Sound.MaxSounds)
            throw new ArgumentException($"There must be {Sound.MaxSounds} entries for sounds, one per number.", nameof(project));
        if (project.Effects.Length != Effect.MaxEffects)
            throw new ArgumentException($"There must be {Effect.MaxEffects} entries for effects, one per number.", nameof(project));
        if (project.EffectIndependents.Length != EffectIndependentCount)
            throw new ArgumentException($"There must be {EffectIndependentCount} effect independents.", nameof(project));
        if (project.SoundIndependents.Length != SoundIndependentCount)
            throw new ArgumentException($"There must be {SoundIndependentCount} sound independents.", nameof(project));
        if (project.Sequences.Length != MemoryCount)
            throw new ArgumentException($"There must be {MemoryCount} entries for sequences, one per memory.", nameof(project));
        for (int index = 0; index < MemoryCount; index++)
        {
            if (project.Looks[index] is not null && project.Sequences[index] is not null)
                throw new ArgumentException($"Memory {index + 1} cannot hold a look and a sequence.", nameof(project));
        }
    }

    // Against the channels of the project's universes; read without the lock, as an int is read whole.
    private void RequireChannel(int channel)
    {
        RequireRange("channel", channel, 1, Volatile.Read(ref _universes) * UniverseSize);
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
