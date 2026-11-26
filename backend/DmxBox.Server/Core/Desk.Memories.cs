namespace DmxBox.Server.Core;

// The looks of the memories: stored, loaded, copied, emptied, and one level of one changed.
internal partial class Desk
{
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

    // Call with the lock held.
    private byte[] StoredLook(int memory)
    {
        return _memories[memory - 1] ?? throw new DeskException($"Memory {memory} is empty.");
    }

    // Call with the lock held, after a stored look changed. A stored look is part of the
    // output while its memory's fader is up, so the output counts as changed too.
    private void CountLookChange()
    {
        _projectChanges++;
        _outputChanges++;
    }
}
