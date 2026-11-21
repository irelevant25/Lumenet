namespace DmxBox.Server;

/// <summary>
/// Solo: while a key of a channel or a memory is held on a UI with SOLO UP lit, only the faders
/// of the held keys shine, at full, and every other light is dark; with SOLO DOWN lit, the faders
/// of the held keys give nothing and every other light stays as it is. Let go, everything is as
/// before. Like blackout, a solo changes what goes out, not the faders.
/// </summary>
/// <remarks>
/// The keys are held by UIs: each holds its own, under its name (its connection), and the keys of
/// a UI that goes away are let go with it, so that no solo stays on by itself.
/// </remarks>
internal sealed partial class Desk
{
    // The keys held for a solo, by the UI that holds them.
    private readonly Dictionary<string, HashSet<SoloHold>> _soloHolds = [];

    // A key held for a solo: SOLO UP or SOLO DOWN, and the fader of its channel or memory.
    private readonly record struct SoloHold(bool Up, Fader Fader);

    /// <summary>The keys held for a solo now, on every UI.</summary>
    public SoloState Solo
    {
        get
        {
            lock (_lock)
            {
                return SoloNow();
            }
        }
    }

    /// <summary>A key of a channel or a memory is held for a solo, or let go.</summary>
    /// <param name="owner">Who holds it: a UI, by the name of its connection.</param>
    /// <param name="up">True for SOLO UP, false for SOLO DOWN.</param>
    /// <param name="target">"channel" or "memory".</param>
    /// <param name="number">The channel (1-512) or the memory (1-128).</param>
    /// <param name="held">True while it is held, false when it is let go.</param>
    /// <returns>True if the keys held for a solo changed.</returns>
    /// <exception cref="DeskException">No such channel or memory.</exception>
    public bool HoldSolo(string owner, bool up, string target, int number, bool held)
    {
        bool memory = target switch
        {
            "channel" => false,
            "memory" => true,
            _ => throw new DeskException($"A solo is for a channel or a memory, not \"{target}\"."),
        };
        if (memory)
            RequireMemory(number);
        else
            RequireChannel(number);

        var hold = new SoloHold(up, new Fader(memory, number));
        lock (_lock)
        {
            bool changed;
            if (held)
            {
                if (!_soloHolds.TryGetValue(owner, out HashSet<SoloHold>? holds))
                    _soloHolds[owner] = holds = [];
                changed = holds.Add(hold);
            }
            else
            {
                changed = _soloHolds.TryGetValue(owner, out HashSet<SoloHold>? holds) && holds.Remove(hold);
                if (holds is { Count: 0 })
                    _soloHolds.Remove(owner);
            }

            if (changed)
                _outputChanges++;
            return changed;
        }
    }

    /// <summary>Lets go every key a UI held for a solo: it went away.</summary>
    /// <param name="owner">The UI, by the name of its connection.</param>
    /// <returns>True if it held any.</returns>
    public bool DropSolo(string owner)
    {
        lock (_lock)
        {
            if (!_soloHolds.Remove(owner))
                return false;

            _outputChanges++;
            return true;
        }
    }

    // Call with the lock held. The keys held for a solo, for the UIs.
    private SoloState SoloNow()
    {
        SoloHold[] holds = [.. _soloHolds.Values.SelectMany(held => held).Distinct()];
        return new SoloState(KeysOf(holds, up: true), KeysOf(holds, up: false));

        static SoloKey[] KeysOf(SoloHold[] holds, bool up) =>
        [
            .. holds
                .Where(hold => hold.Up == up)
                .OrderBy(hold => hold.Fader.Memory)
                .ThenBy(hold => hold.Fader.Number)
                .Select(hold => new SoloKey(hold.Fader.Memory ? "memory" : "channel", hold.Fader.Number)),
        ];
    }

    // Call with the lock held. The faders held with SOLO UP and those held with SOLO DOWN.
    private (HashSet<Fader> Up, HashSet<Fader> Down) SoloFaders()
    {
        var up = new HashSet<Fader>();
        var down = new HashSet<Fader>();
        foreach (SoloHold hold in _soloHolds.Values.SelectMany(held => held))
            (hold.Up ? up : down).Add(hold.Fader);

        return (up, down);
    }

    // Call with the lock held. What goes out with SOLO UP, before the master: only the faders of
    // the held keys, at full. A channel at full; a memory's look at full, or its sequence as it
    // plays at full.
    private byte[] SoloUpOutput(HashSet<Fader> up, double now)
    {
        byte[] output = new byte[ChannelCount];
        foreach (Fader fader in up)
        {
            if (!fader.Memory)
                output[fader.Number - 1] = MaxLevel;
            else if (_memories[fader.Number - 1] is { } look)
                RaiseTo(output, look, MaxLevel);
            else
                AddSequence(output, fader.Number - 1, now, MaxLevel, stepsOn: null);
        }

        return output;
    }
}
