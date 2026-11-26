namespace DmxBox.Server.Midi;

/// <summary>
/// The pages of channels, memories and effects the desk shows on the PC: the slots of a controller
/// ("fader.slot.channel.3") follow them, as the PC's page keys move them.
/// </summary>
/// <param name="FirstChannel">The first channel of the page shown, 1-4096.</param>
/// <param name="ChannelsPerPage">How many channel faders a page shows.</param>
/// <param name="FirstMemory">The first memory of the page shown, 1-128.</param>
/// <param name="MemoriesPerPage">How many memory faders a page shows.</param>
/// <param name="FirstEffect">The first effect of the page shown, 1-64.</param>
/// <param name="EffectsPerPage">How many effect faders a page shows.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record MidiPages(int FirstChannel, int ChannelsPerPage, int FirstMemory, int MemoriesPerPage, int FirstEffect, int EffectsPerPage)
{
    /// <summary>The first pages, as a page of the desk opens with them on a PC's screen.</summary>
    public static readonly MidiPages First = new(1, 32, 1, 16, 1, 16);
}

/// <summary>A fader of the desk: its kind and number (0 for the master and the rate).</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Number">The channel, memory or effect.</param>
internal readonly record struct DeskFader(FaderKind Kind, int Number = 0);

/// <summary>A fader moved, or why not.</summary>
/// <param name="Position">Where it stands after the move, 0-255; null when it was refused.</param>
/// <param name="Refused">Why the desk refused it (the stack is live); null when it moved.</param>
internal readonly record struct FaderMove(int? Position, string? Refused);

/// <summary>
/// What MIDI may do to the desk's faders. The desk program's goes through the desk and tells the
/// pages (<see cref="MidiDeskFaders"/>); the tests have a pretend one.
/// </summary>
internal interface IMidiDesk
{
    /// <summary>The pages the PC shows, for the slots: the page on the PC that presses the keys tells them.</summary>
    MidiPages Pages { get; set; }

    /// <summary>How many channels the project's universes have.</summary>
    int ChannelCount { get; }

    /// <summary>
    /// The desk may have changed since it was last read: the next <see cref="PositionOf"/> reads it
    /// again. MIDI reads the desk once for each message and each round of feedback.
    /// </summary>
    void Refresh();

    /// <summary>Where a fader stands now, 0-255; null when there is no such fader (a memory without a sequence, an empty effect).</summary>
    /// <param name="fader">The fader.</param>
    int? PositionOf(DeskFader fader);

    /// <summary>Moves a fader to a place, 0-255.</summary>
    /// <param name="fader">The fader.</param>
    /// <param name="position">Where to.</param>
    FaderMove Move(DeskFader fader, int position);
}

/// <summary>
/// What became of one message, for the list of incoming messages: the target it worked (a key or
/// a fader by name, as the page names them) and what happened.
/// </summary>
/// <param name="Target">The mapping's target; null when nothing is mapped to the control.</param>
/// <param name="Result">
/// "unmapped", "down" and "up" (a key pressed and let go), "no-page" (no page of the desk is open
/// on the PC to press the key), "moved" (a fader, to <paramref name="Value"/>), "pickup" (the
/// fader waits until the control reaches the desk's value, <paramref name="Desk"/>), "refused"
/// (<paramref name="Reason"/> says why), "missing" (no such fader: an empty slot), or "learned".
/// </param>
/// <param name="Value">Where the control or the fader stands, 0-255.</param>
/// <param name="Desk">Where the desk's fader stands, 0-255, while the control has to reach it.</param>
/// <param name="Reason">Why the desk refused.</param>
internal readonly record struct MidiOutcome(string? Target, string Result, int? Value = null, int? Desk = null, string? Reason = null);

/// <summary>
/// Turns the messages of the controllers into the desk's keys and faders, by the mappings:
/// <list type="bullet">
/// <item>A key: Note On presses it and Note Off lets it go, a controller above zero presses it and
/// zero lets it go, a program change presses it and lets it go at once. The page on the PC
/// presses it (<c>pressKey</c>), as its keyboard shortcut would: a locked key stays locked, and
/// while SOLO is lit a key held down holds its fader.</item>
/// <item>A fader: a controller's 0-127, a pair of controllers' 14 bits or the pitch bend's 14
/// bits become 0-255. An endless encoder moves it from where it is, <see cref="RelativeStep"/> a
/// tick. A fader without a motor takes over only when it reaches the desk's value, or passes it
/// (<see cref="MidiMapping.Pickup"/>): a light never jumps to where the hand left the fader.</item>
/// </list>
/// Not thread-safe: <see cref="MidiControl"/> holds its lock around it.
/// </summary>
internal sealed class MidiMapper
{
    /// <summary>How near the desk's value (of 255) a fader must come to take over: 1.2 %.</summary>
    public const int PickupWindow = 3;

    /// <summary>How far (of 255) a tick of an endless encoder moves a fader: about 1 %.</summary>
    public const int RelativeStep = 2;

    private readonly IMidiDesk _desk;
    private readonly Func<string, bool, bool> _pressKey;
    private readonly Dictionary<(string Device, int Channel, string Message, int Number), ControlState> _states = [];
    private Dictionary<(string Device, int Channel, MidiKind Kind, int Number), (MidiMapping Mapping, bool Fine)> _byMessage = [];

    /// <summary>Makes the mapper.</summary>
    /// <param name="desk">The desk's faders.</param>
    /// <param name="pressKey">Presses (true) or lets go (false) a key by its name; false when no page can.</param>
    public MidiMapper(IMidiDesk desk, Func<string, bool, bool> pressKey)
    {
        _desk = desk;
        _pressKey = pressKey;
    }

    /// <summary>The mappings now: they take the place of those before.</summary>
    /// <param name="mappings">The mappings, checked.</param>
    public void Use(IEnumerable<MidiMapping> mappings)
    {
        var byMessage = new Dictionary<(string, int, MidiKind, int), (MidiMapping, bool)>();
        foreach (MidiMapping mapping in mappings)
        {
            switch (mapping.Message)
            {
                case MidiMapping.Note:
                    byMessage[(mapping.Device, mapping.Channel, MidiKind.NoteOn, mapping.Number)] = (mapping, false);
                    byMessage[(mapping.Device, mapping.Channel, MidiKind.NoteOff, mapping.Number)] = (mapping, false);
                    break;
                case MidiMapping.Controller:
                    byMessage.TryAdd((mapping.Device, mapping.Channel, MidiKind.ControlChange, mapping.Number), (mapping, false));
                    break;
                case MidiMapping.Controller14:
                    // The pair takes its two controllers over from single ones.
                    byMessage[(mapping.Device, mapping.Channel, MidiKind.ControlChange, mapping.Number)] = (mapping, false);
                    byMessage[(mapping.Device, mapping.Channel, MidiKind.ControlChange, mapping.Number + 32)] = (mapping, true);
                    break;
                case MidiMapping.PitchBend:
                    byMessage[(mapping.Device, mapping.Channel, MidiKind.PitchBend, 0)] = (mapping, false);
                    break;
                case MidiMapping.Program:
                    byMessage[(mapping.Device, mapping.Channel, MidiKind.ProgramChange, mapping.Number)] = (mapping, false);
                    break;
            }
        }

        _byMessage = byMessage;

        // The states of controls that are mapped no more go; a key held down is let go first.
        var controls = new HashSet<(string, int, string, int)>(byMessage.Values.Select(entry => entry.Item1.Control));
        foreach (var (control, state) in _states.ToArray())
        {
            if (controls.Contains(control))
                continue;
            if (state.HeldKey is { } key)
                _pressKey(key, false);
            _states.Remove(control);
        }
    }

    /// <summary>Does what a message of a device is mapped to.</summary>
    /// <param name="device">The device it came from.</param>
    /// <param name="message">The message.</param>
    /// <param name="now">Seconds, on any clock that goes on: when a fader last moved counts for the feedback.</param>
    public MidiOutcome Handle(string device, MidiMessage message, double now)
    {
        int number = message.Kind == MidiKind.PitchBend ? 0 : message.Number;
        if (!_byMessage.TryGetValue((device, message.Channel, message.Kind, number), out var entry))
            return new MidiOutcome(null, "unmapped");

        MidiMapping mapping = entry.Mapping;
        ControlTarget target = ControlTarget.Parse(mapping.Target) ?? throw new InvalidOperationException("A mapping that was checked has a target.");
        ControlState state = StateOf(mapping);
        return target.IsKey
            ? Key(mapping, state, message)
            : Fader(mapping, target, state, message, entry.Fine, now);
    }

    /// <summary>Lets go every key a device holds down: it was pulled out, or is used no more.</summary>
    /// <param name="device">The device.</param>
    public void LetGo(string device)
    {
        foreach (var (control, state) in _states)
        {
            if (control.Device == device && state.HeldKey is { } key)
            {
                state.HeldKey = null;
                _pressKey(key, false);
            }
        }
    }

    /// <summary>True while a fader's control is moved by hand (it moved within <paramref name="within"/> seconds): its feedback waits.</summary>
    /// <param name="mapping">The control's mapping.</param>
    /// <param name="now">Seconds, on the clock of <see cref="Handle"/>.</param>
    /// <param name="within">Seconds.</param>
    public bool MovedLately(MidiMapping mapping, double now, double within)
    {
        return _states.TryGetValue(mapping.Control, out ControlState? state) && now - state.MovedAt < within;
    }

    /// <summary>The fader a target means now: a slot follows the pages shown; null for a slot beyond them.</summary>
    /// <param name="target">A fader's target.</param>
    public DeskFader? Resolve(ControlTarget target)
    {
        if (target.Fader is not { } kind)
            return null;
        if (!target.Slot)
        {
            return kind switch
            {
                FaderKind.Channel when target.Number > _desk.ChannelCount => null,
                FaderKind.Memory or FaderKind.Sequence when target.Number > Desk.MemoryCount => null,
                FaderKind.Effect or FaderKind.EffectSpeed or FaderKind.EffectSize or FaderKind.EffectSpread when target.Number > Effect.MaxEffects => null,
                FaderKind.Sound when target.Number > Sound.MaxSounds => null,
                _ => new DeskFader(kind, target.Number),
            };
        }

        MidiPages pages = _desk.Pages;
        (int first, int perPage, int last) = kind switch
        {
            FaderKind.Channel => (pages.FirstChannel, pages.ChannelsPerPage, _desk.ChannelCount),
            FaderKind.Memory => (pages.FirstMemory, pages.MemoriesPerPage, Desk.MemoryCount),
            _ => (pages.FirstEffect, pages.EffectsPerPage, Effect.MaxEffects),
        };
        int number = first + target.Number - 1;
        return target.Number <= perPage && number <= last ? new DeskFader(kind, number) : null;
    }

    private ControlState StateOf(MidiMapping mapping)
    {
        if (!_states.TryGetValue(mapping.Control, out ControlState? state))
        {
            state = new ControlState();
            _states[mapping.Control] = state;
        }

        return state;
    }

    private MidiOutcome Key(MidiMapping mapping, ControlState state, MidiMessage message)
    {
        bool down = message.IsPress;
        if (down && state.HeldKey is not null)
            return new MidiOutcome(mapping.Target, "down");   // pressed once more without a letting go: once is enough
        if (!down && state.HeldKey is null)
            return new MidiOutcome(mapping.Target, "up");

        if (!_pressKey(mapping.Target, down))
        {
            state.HeldKey = null;
            return new MidiOutcome(mapping.Target, "no-page");
        }

        state.HeldKey = down ? mapping.Target : null;
        if (down && message.Kind == MidiKind.ProgramChange)
        {
            // A program change has no letting go: the key is let go at once.
            _pressKey(mapping.Target, false);
            state.HeldKey = null;
        }

        return new MidiOutcome(mapping.Target, down ? "down" : "up");
    }

    private MidiOutcome Fader(MidiMapping mapping, ControlTarget target, ControlState state, MidiMessage message, bool fine, double now)
    {
        if (Resolve(target) is not { } fader || _desk.PositionOf(fader) is not { } current)
            return new MidiOutcome(mapping.Target, "missing");

        if (state.Fader != fader)
        {
            // A slot that shows another fader now: what was taken over counts no more.
            state.Fader = fader;
            state.Applied = null;
        }

        state.MovedAt = now;
        int position;
        bool relative = false;
        switch (mapping.Message)
        {
            case MidiMapping.Controller when mapping.Mode != MidiMapping.Absolute:
                relative = true;
                position = Math.Clamp(current + (Ticks(mapping.Mode, message.Value) * RelativeStep), 0, FaderScale.Top);
                break;
            case MidiMapping.Controller:
                position = Scale(message.Value, 127);
                break;
            case MidiMapping.Controller14:
                if (fine)
                {
                    state.Fine = message.Value;
                }
                else
                {
                    // The coarse half alone stands for a fine half of zero (MIDI has it so); the fine half follows.
                    state.Coarse = message.Value;
                    state.Fine = 0;
                }

                position = Scale((state.Coarse << 7) | state.Fine, MidiMessage.MaxPitchBend);
                break;
            default:
                position = Scale(message.Value, MidiMessage.MaxPitchBend);
                break;
        }

        int? hardware = state.Hardware;
        state.Hardware = position;
        bool takes = relative
            || !mapping.Pickup
            || state.Applied == current
            || Math.Abs(position - current) <= PickupWindow
            || (hardware is { } before && (before - current) * (position - current) <= 0);
        if (!takes)
            return new MidiOutcome(mapping.Target, "pickup", position, current);

        FaderMove move = _desk.Move(fader, position);
        if (move.Position is not { } moved)
            return new MidiOutcome(mapping.Target, "refused", position, Reason: move.Refused);

        state.Applied = moved;
        return new MidiOutcome(mapping.Target, "moved", moved);
    }

    // How many ticks a message of an endless encoder means, up or down.
    private static int Ticks(string mode, int value)
    {
        return mode switch
        {
            MidiMapping.RelativeTwos => value is > 0 and < 64 ? value : value > 64 ? value - 128 : 0,
            MidiMapping.RelativeOffset => value - 64,
            MidiMapping.RelativeSign => value is > 0 and < 64 ? value : value > 64 ? -(value - 64) : 0,
            _ => 0,
        };
    }

    // A value of 0 to top as a place of the fader, 0-255.
    private static int Scale(int value, int top)
    {
        return (int)Math.Round(Math.Clamp(value, 0, top) * (double)FaderScale.Top / top);
    }

    // What the mapper keeps of one control between two of its messages.
    private sealed class ControlState
    {
        // The key it holds down; null while none.
        public string? HeldKey { get; set; }

        // The fader it worked last, for a slot that changes.
        public DeskFader? Fader { get; set; }

        // Where the control stood (0-255) at its last message; null before the first.
        public int? Hardware { get; set; }

        // Where the desk's fader stood after this control moved it; null when it has not.
        public int? Applied { get; set; }

        // The two halves of 14 bits.
        public int Coarse { get; set; }

        public int Fine { get; set; }

        // When it last moved (seconds); the feedback waits a moment after that.
        public double MovedAt { get; set; } = double.NegativeInfinity;
    }
}
