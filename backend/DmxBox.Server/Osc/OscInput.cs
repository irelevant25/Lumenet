namespace DmxBox.Server.Osc;

/// <summary>
/// Turns OSC messages into the desk's keys and faders by their addresses: every key and fader a MIDI
/// control can work (<see cref="ControlTarget"/>), its dots written as slashes, after "/lumenet" or
/// without it. "/lumenet/stack/play" is GO ("stack.play"), "/lumenet/fader/master" the master,
/// "/lumenet/fader/channel/12" the fader of channel 12, "/lumenet/memory/5" the key of memory 5.
/// <list type="bullet">
/// <item>A key: a message without an argument (or with an impulse, nil or a text) presses it and lets
/// it go; a number above zero or true presses it, zero or false lets it go (the keys of TouchOSC send
/// 1 and 0). The page on the PC used last presses it, as for a MIDI controller.</item>
/// <item>A fader: a number with a point is its place from 0 (down) to 1 (full), a whole number its place
/// from 0 to 255, as on the page; true is full, false down. It goes there at once: no pickup.</item>
/// </list>
/// Not thread-safe: <see cref="MidiControl"/> holds its lock around it.
/// </summary>
/// <param name="desk">The desk's faders.</param>
/// <param name="pressKey">Presses (true) or lets go (false) a key by its name; false when no page can.</param>
/// <param name="resolve">The fader a target means now (a slot follows the pages shown); null for none.</param>
internal sealed class OscInput(IMidiDesk desk, Func<string, bool, bool> pressKey, Func<ControlTarget, DeskFader?> resolve)
{
    /// <summary>The part an address may begin with: it names the desk.</summary>
    public const string Prefix = "/lumenet";

    // The keys OSC holds down: pressed with 1 and not yet let go with 0.
    private readonly HashSet<string> _held = new(StringComparer.Ordinal);

    /// <summary>The key or fader an address names, "stack.play"; null for an address that names none.</summary>
    /// <param name="address">"/lumenet/stack/play" or "/stack/play".</param>
    public static string? TargetOf(string address)
    {
        if (string.IsNullOrEmpty(address) || address[0] != '/')
            return null;

        string[] parts = address[1..].Split('/');
        if (parts[0].Equals(Prefix[1..], StringComparison.OrdinalIgnoreCase))
            parts = parts[1..];
        if (parts.Length == 0 || parts.Any(part => part.Length == 0))
            return null;

        string name = string.Join('.', parts).ToLowerInvariant();
        return ControlTarget.Parse(name) is null ? null : name;
    }

    /// <summary>The address of a key or fader: "/lumenet/stack/play" for "stack.play".</summary>
    /// <param name="target">Its name.</param>
    public static string AddressOf(string target)
    {
        return $"{Prefix}/{target.Replace('.', '/')}";
    }

    /// <summary>Does what a message names, and says what became of it, as for a MIDI message.</summary>
    /// <param name="message">The message.</param>
    public MidiOutcome Handle(OscMessage message)
    {
        if (TargetOf(message.Address) is not { } name || ControlTarget.Parse(name) is not { } target)
            return new MidiOutcome(null, "unmapped");

        object? argument = message.Arguments.Count > 0 ? message.Arguments[0] : OscMessage.Impulse;
        return target.IsKey ? Key(name, argument) : Fader(target, name, argument);
    }

    /// <summary>Lets go every key OSC holds down: OSC was switched off.</summary>
    public void LetGo()
    {
        foreach (string key in _held)
            pressKey(key, false);
        _held.Clear();
    }

    private MidiOutcome Key(string key, object? argument)
    {
        bool? down = argument switch
        {
            bool flag => flag,
            long number => number > 0,
            double number => number > 0,
            _ => null,   // none, an impulse, nil, a text: pressed and let go
        };

        if (down is null)
        {
            if (_held.Contains(key))
                return new MidiOutcome(key, "down");   // held down already: once is enough
            if (!pressKey(key, true))
                return new MidiOutcome(key, "no-page");
            pressKey(key, false);
            return new MidiOutcome(key, "down");
        }

        if (down.Value)
        {
            if (_held.Contains(key))
                return new MidiOutcome(key, "down");
            if (!pressKey(key, true))
                return new MidiOutcome(key, "no-page");
            _held.Add(key);
            return new MidiOutcome(key, "down");
        }

        if (!_held.Remove(key))
            return new MidiOutcome(key, "up");   // not held: nothing to let go
        return new MidiOutcome(key, pressKey(key, false) ? "up" : "no-page");
    }

    private MidiOutcome Fader(ControlTarget target, string name, object? argument)
    {
        int? position = argument switch
        {
            double number when double.IsFinite(number) => (int)Math.Round(Math.Clamp(number, 0, 1) * FaderScale.Top),
            long number => (int)Math.Clamp(number, 0, FaderScale.Top),
            bool flag => flag ? FaderScale.Top : 0,
            _ => null,
        };
        if (position is not { } place)
            return new MidiOutcome(name, "refused", Reason: "A fader needs a number: 0 to 1 with a point, or 0 to 255.");

        if (resolve(target) is not { } fader || desk.PositionOf(fader) is null)
            return new MidiOutcome(name, "missing");

        FaderMove move = desk.Move(fader, place);
        return move.Position is { } moved
            ? new MidiOutcome(name, "moved", moved)
            : new MidiOutcome(name, "refused", place, Reason: move.Refused);
    }
}
