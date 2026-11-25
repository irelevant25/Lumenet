using DmxBox.Server;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmxBox.Tests;

/// <summary>
/// The ready-made mappings of the APC mini mk2, the nanoKONTROL2 and the two X-Touch: each is a
/// setup the desk takes, one thing per control; one takes the place of what a device did, and its
/// faders and lights go where the controller takes them.
/// </summary>
public sealed class MidiPresetsTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("lumenet-midi-presets-");
    private readonly PretendMidiPorts _ports = new();
    private readonly PretendMidiDesk _desk = new();
    private readonly PretendMidiPages _pages = new();
    private readonly MidiControl _midi;

    public MidiPresetsTests()
    {
        _midi = new MidiControl(
            _ports, new MidiSetupFile(Path.Combine(_folder.FullName, MidiSetupFile.FileName), NullLogger.Instance), _desk, _pages, NullLogger.Instance, () => 0);
    }

    public void Dispose()
    {
        _midi.Dispose();
        _folder.Delete(recursive: true);
    }

    [Fact]
    public void EveryPresetIsASetupTheDeskTakesWithOneThingPerControl()
    {
        Assert.Equal(["apc-mini-mk2", "nanokontrol2", "x-touch-mini", "x-touch-compact"], MidiPresets.All.Select(preset => preset.Id));
        foreach (MidiPreset preset in MidiPresets.All)
        {
            MidiMapping[] mappings = [.. preset.Mappings.Select(mapping => mapping with { Device = preset.Device })];
            MidiSetup setup = MidiSetupCheck.Checked(new MidiSetup([preset.Device], mappings));
            Assert.Equal(mappings, setup.Mappings);              // as the desk keeps them, and no control twice
            Assert.All(preset.Mappings, mapping => Assert.Equal("", mapping.Device));
            Assert.Contains(preset.Mappings, mapping => mapping.Target == "fader.slot.channel.1");
            Assert.Equal(preset.Mappings.Length, preset.Mappings.Select(mapping => mapping.Target).Distinct().Count());
        }
    }

    [Fact]
    public void APresetTakesThePlaceOfWhatTheDevicesControlsDidAndTheDeskUsesIt()
    {
        _ports.Plug("X-TOUCH MINI");
        _midi.SetMappings(
        [
            new MidiMapping { Device = "X-TOUCH MINI", Channel = 11, Message = MidiMapping.Note, Number = 99, Target = "memory.1" },
            new MidiMapping { Device = "Other", Message = MidiMapping.Note, Number = 1, Target = "memory.2" },
        ]);

        MidiState state = _midi.UsePreset("x-touch-mini", "X-TOUCH MINI");
        Assert.DoesNotContain(state.Mappings, mapping => mapping.Number == 99);
        Assert.Contains(state.Mappings, mapping => mapping.Device == "Other");
        Assert.Equal(MidiPresets.Find("x-touch-mini")!.Mappings.Length, state.Mappings.Count(mapping => mapping.Device == "X-TOUCH MINI"));
        Assert.Equal("open", state.Devices.Single(device => device.Name == "X-TOUCH MINI").State);
        Assert.Contains("x-touch-mini", state.Presets.Select(preset => preset.Id));

        Assert.Contains("no ready-made", Assert.Throws<DeskException>(() => _midi.UsePreset("nothing", "X-TOUCH MINI")).Message, StringComparison.Ordinal);
        Assert.Contains("which MIDI device", Assert.Throws<DeskException>(() => _midi.UsePreset("x-touch-mini", " ")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheApcMiniLightsAPadInTheColorOfItsKindAtTheBrightnessOfTheDesksKey()
    {
        PretendMidiDevice apc = _ports.Plug("APC mini mk2");
        _midi.UsePreset("apc-mini-mk2", "APC mini mk2");
        _midi.Claim("pc");
        _midi.ReportLamps("pc", [new MidiLamp("memory.1", 2, false), new MidiLamp("memory.32", 3, false), new MidiLamp("stack.play", 1, false)]);
        _midi.Tick();
        Assert.Contains(new MidiMessage(MidiKind.NoteOn, 3, 32, 5), apc.Sent);   // memory 1: the top left of the memories, red at half
        Assert.Contains(new MidiMessage(MidiKind.NoteOn, 7, 15, 5), apc.Sent);   // memory 32: the bottom right of them, red at full
        Assert.Contains(new MidiMessage(MidiKind.NoteOn, 1, 58, 13), apc.Sent);  // Play in the top row, yellow and faint

        apc.ClearSent();
        apc.Controller(56, 0);                                     // the master fader, picked up at the desk's value
        Assert.Equal([(new DeskFader(FaderKind.Master), 0)], _desk.Moves);
    }

    [Fact]
    public void TheXTouchCompactGetsItsMotorsAndLightsWhereItTakesThem()
    {
        PretendMidiDevice compact = _ports.Plug("X-TOUCH COMPACT");
        _midi.UsePreset("x-touch-compact", "X-TOUCH COMPACT");
        _midi.Claim("pc");
        _desk[FaderKind.Channel, 1] = 255;
        _midi.ReportLamps("pc", [new MidiLamp("stack.play", 3, false)]);
        _midi.Tick();
        Assert.Contains(new MidiMessage(MidiKind.ControlChange, 1, 1, 127), compact.Sent);   // fader 1 up, as channel 1 is
        Assert.Contains(new MidiMessage(MidiKind.NoteOn, 2, 38, 2), compact.Sent);           // Play (Note 54) lit by Note 38 on channel 2
        Assert.DoesNotContain(compact.Sent, message => message is { Kind: MidiKind.NoteOn, Channel: 1 });   // the knobs pressed have no light
    }
}
