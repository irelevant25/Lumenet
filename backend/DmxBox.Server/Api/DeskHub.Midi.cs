using System.Globalization;
using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Server.Api;

// The hub's part for the MIDI controllers of the PC.
internal partial class DeskHub
{
    /// <summary>
    /// The MIDI controllers of the PC: the devices, what their controls do, the ready-made mappings.
    /// A page on another device sees them; only a page on the PC changes them (they belong to it).
    /// </summary>
    public MidiState GetMidi()
    {
        return _midi.State;
    }

    /// <summary>Uses a MIDI device (the desk opens it) or leaves it to other programs. Only on the PC.</summary>
    /// <param name="name">The device, by its name.</param>
    /// <param name="use">True to use it.</param>
    public MidiState SetMidiDevice(string name, bool use)
    {
        RequireLocal("MIDI can only be set on the PC.");
        return _midi.Use(name, use);
    }

    /// <summary>What the controls do, all of them: they take the place of those before. Only on the PC.</summary>
    /// <param name="mappings">Every mapping.</param>
    public MidiState SetMidiMappings(MidiMapping[] mappings)
    {
        RequireLocal("MIDI can only be set on the PC.");
        return _midi.SetMappings(mappings);
    }

    /// <summary>
    /// Learn: the next control that moves on a device the desk uses is to work this key or fader;
    /// null stops learning. Only on the PC.
    /// </summary>
    /// <param name="target">The key ("memory.5") or fader ("fader.master"), see <see cref="ControlTarget"/>.</param>
    public MidiState LearnMidi(string? target)
    {
        RequireLocal("MIDI can only be set on the PC.");
        return _midi.Learn(target);
    }

    /// <summary>Gives a device the ready-made mapping of its controller. Only on the PC.</summary>
    /// <param name="preset">The preset, like "apc-mini-mk2".</param>
    /// <param name="device">The device, by its name.</param>
    public MidiState UseMidiPreset(string preset, string device)
    {
        RequireLocal("MIDI can only be set on the PC.");
        return _midi.UsePreset(preset, device);
    }

    /// <summary>The MIDI setup of the PC, for Export. Only on the PC.</summary>
    public MidiSetup GetMidiSetup()
    {
        RequireLocal("MIDI can only be exported on the PC.");
        return _midi.Setup;
    }

    /// <summary>A MIDI setup takes the place of the PC's (Import). Only on the PC.</summary>
    /// <param name="setup">The setup.</param>
    public MidiState SetMidiSetup(MidiSetup setup)
    {
        RequireLocal("MIDI can only be imported on the PC.");
        return _midi.SetSetup(setup);
    }

    /// <summary>
    /// A page on the PC was used: it presses the keys of the controllers from now on, as the one
    /// used last. A page on another device never does.
    /// </summary>
    public void ClaimMidi()
    {
        if (IsLocal)
            _midi.Claim(Context.ConnectionId);
    }

    /// <summary>The page that presses the keys of the controllers tells how the desk's keys are lit: the controllers' lights follow.</summary>
    /// <param name="lamps">The lamps that changed, or all of them.</param>
    public void ReportMidiLamps(MidiLamp[] lamps)
    {
        _midi.ReportLamps(Context.ConnectionId, lamps);
    }

    /// <summary>The page that presses the keys of the controllers tells which pages of faders it shows: the slots follow.</summary>
    /// <param name="pages">The pages.</param>
    public void ReportMidiPages(MidiPages pages)
    {
        _midi.ReportPages(Context.ConnectionId, pages);
    }

    /// <summary>
    /// OSC from other programs: whether the desk receives it, and on which UDP port (kept in osc.json).
    /// Only on the PC.
    /// </summary>
    /// <param name="on">True to receive OSC.</param>
    /// <param name="port">The UDP port, 1 to 65535.</param>
    public MidiState SetOsc(bool on, int port)
    {
        RequireLocal("OSC can only be set on the PC.");
        return _osc.Set(on, port);
    }

    /// <summary>The page shows the list of incoming MIDI messages, or no more. Only on the PC.</summary>
    /// <param name="on">True while it shows it.</param>
    /// <returns>The last messages, while it shows them.</returns>
    public MidiMonitorLine[] WatchMidi(bool on)
    {
        RequireLocal("The MIDI messages can only be shown on the PC.");
        return _midi.Watch(Context.ConnectionId, on);
    }
}
