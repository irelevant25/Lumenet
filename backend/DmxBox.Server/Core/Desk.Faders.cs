namespace DmxBox.Server.Core;

// The desk's faders and keys: channels, memories, the master, blackout, the notes under the faders, the
// independents and Clear.
internal partial class Desk
{
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

    // Call with the lock held. Writing the text that is there already is no change: the
    // project must not count as unsaved because of it.
    private void StoreNote(string[] notes, int index, string note)
    {
        if (notes[index] == note)
            return;

        notes[index] = note;
        _projectChanges++;
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
}
