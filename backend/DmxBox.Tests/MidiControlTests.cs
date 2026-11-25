using DmxBox.Server;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmxBox.Tests;

/// <summary>
/// The MIDI controllers of the PC with pretend devices: which devices the desk opens, devices
/// pulled out and plugged in again, Learn, the page on the PC that presses the keys, the lights
/// and motors sent back, midi.json, and the list of incoming messages.
/// </summary>
public sealed class MidiControlTests : IDisposable
{
    private const string Apc = "APC mini mk2";

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("lumenet-midi-");
    private readonly PretendMidiPorts _ports = new();
    private readonly PretendMidiDesk _desk = new();
    private readonly PretendMidiPages _pages = new();
    private readonly MidiControl _midi;
    private double _now;

    public MidiControlTests()
    {
        _midi = Make();
    }

    private string SetupFile => Path.Combine(_folder.FullName, MidiSetupFile.FileName);

    public void Dispose()
    {
        _midi.Dispose();
        _folder.Delete(recursive: true);
    }

    private MidiControl Make() => new(_ports, new MidiSetupFile(SetupFile, NullLogger.Instance), _desk, _pages, NullLogger.Instance, () => _now);

    // A key lit by the desk, as Learn makes it; a fader without pickup or motor.
    private static MidiMapping Map(string message, int number, string target, int channel = 1) =>
        new() { Device = Apc, Channel = channel, Message = message, Number = number, Target = target, Pickup = false, Feedback = !target.StartsWith("fader.", StringComparison.Ordinal) };

    private PretendMidiDevice PlugAndUse(params MidiMapping[] mappings)
    {
        PretendMidiDevice device = _ports.Plug(Apc);
        _midi.SetMappings(mappings);
        _midi.Use(Apc, use: true);
        return device;
    }

    private MidiDeviceView DeviceView(string name) => _midi.State.Devices.Single(device => device.Name == name);

    [Fact]
    public void TheDeskOpensOnlyTheDevicesItIsToldToUse()
    {
        PretendMidiDevice apc = _ports.Plug(Apc);
        PretendMidiDevice keyboard = _ports.Plug("Keyboard", output: false);
        _midi.Look();
        Assert.Equal("unused", DeviceView(Apc).State);
        Assert.False(apc.IsOpen);

        MidiState state = _midi.Use(Apc, use: true);
        Assert.Equal("open", state.Devices.Single(device => device.Name == Apc).State);
        Assert.True(apc.IsOpen);
        Assert.False(keyboard.IsOpen);
        Assert.Equal(new MidiDeviceView("Keyboard", true, false, false, "unused", null, false), DeviceView("Keyboard"));

        _midi.Use(Apc, use: false);
        Assert.False(apc.IsOpen);
        Assert.Equal("unused", DeviceView(Apc).State);
    }

    [Fact]
    public void ADeviceWithoutInputIsNoControllerAndIsNotListed()
    {
        _ports.Plug("Microsoft GS Wavetable Synth", input: false);
        _ports.Plug(Apc);
        _midi.Look();
        Assert.Equal([Apc], _midi.State.Devices.Select(device => device.Name));
    }

    [Fact]
    public void ADevicePulledOutAndPluggedInAgainIsFoundByItsName()
    {
        PretendMidiDevice apc = PlugAndUse();
        _ports.Unplug(Apc);
        _midi.Look();
        Assert.Equal("missing", DeviceView(Apc).State);
        Assert.False(apc.IsOpen);

        _ports.Plug(Apc);
        _midi.Look();
        Assert.Equal("open", DeviceView(Apc).State);
        Assert.Equal(2, apc.Opened);
    }

    [Fact]
    public void ADeviceThatFailsIsClosedAndOpenedAgain()
    {
        PretendMidiDevice apc = PlugAndUse();
        apc.Fail();
        _midi.Look();   // there, but its connection failed: closed, and opened anew
        Assert.Equal(2, apc.Opened);
        Assert.True(apc.IsOpen);
    }

    [Fact]
    public void ADeviceAnotherProgramHasSaysWhy()
    {
        PretendMidiDevice apc = _ports.Plug(Apc);
        apc.Busy = true;
        _midi.Use(Apc, use: true);
        MidiDeviceView view = DeviceView(Apc);
        Assert.Equal("failed", view.State);
        Assert.Contains("another program", view.Problem, StringComparison.Ordinal);

        apc.Busy = false;
        _midi.Look();
        Assert.Equal("open", DeviceView(Apc).State);
        Assert.Null(DeviceView(Apc).Problem);
    }

    [Fact]
    public void LearnMapsTheNextControlThatMovesToTheKeyOrFader()
    {
        PretendMidiDevice apc = PlugAndUse();
        _midi.Claim("pc");
        Assert.Equal("memory.5", _midi.Learn("memory.5").Learning);

        apc.Note(56, 127);
        MidiMapping learned = Assert.Single(_midi.State.Mappings);
        Assert.Equal((Apc, 1, MidiMapping.Note, 56), learned.Control);
        Assert.Equal("memory.5", learned.Target);
        Assert.Null(_midi.State.Learning);
        Assert.Empty(_pages.Keys);                                // the press that taught it pressed nothing

        apc.Note(56, 0);
        Assert.Empty(_pages.Keys);
        apc.Note(56, 127);
        Assert.Equal([("pc", "memory.5", true)], _pages.Keys);
        Assert.Contains("\"memory.5\"", File.ReadAllText(SetupFile), StringComparison.Ordinal);
    }

    [Fact]
    public void LearnWaitsForAControlThatCanWorkTheTarget()
    {
        PretendMidiDevice apc = PlugAndUse();
        _midi.Learn("fader.master");
        apc.Note(1, 127);                                         // a pad cannot be a fader
        Assert.Equal("fader.master", _midi.State.Learning);

        apc.Controller(48, 10);
        MidiMapping learned = Assert.Single(_midi.State.Mappings);
        Assert.Equal((MidiMapping.Controller, 48, "fader.master", true), (learned.Message, learned.Number, learned.Target, learned.Pickup));
    }

    [Fact]
    public void LearnMakesAPairOf14BitsWhenTheFineControllerFollowsAtOnce()
    {
        PretendMidiDevice apc = PlugAndUse();
        _midi.Learn("fader.channel.1");
        apc.Controller(7, 64);
        _now += 0.05;
        apc.Controller(39, 10);
        MidiMapping learned = Assert.Single(_midi.State.Mappings);
        Assert.Equal((MidiMapping.Controller14, 7), (learned.Message, learned.Number));

        // Too late for a pair: a controller alone.
        _midi.Learn("fader.channel.2");
        apc.Controller(8, 64);
        _now += 1;
        _midi.Tick();
        apc.Controller(40, 10);
        Assert.Contains(_midi.State.Mappings, mapping => mapping is { Target: "fader.channel.2", Message: MidiMapping.Controller, Number: 8 });
    }

    [Fact]
    public void AControlLearnedAgainDoesTheNewThingOnly()
    {
        PretendMidiDevice apc = PlugAndUse(Map(MidiMapping.Note, 56, "memory.5"));
        _midi.Learn("stack.play");
        apc.Note(56, 127);
        Assert.Equal("stack.play", Assert.Single(_midi.State.Mappings).Target);
    }

    [Fact]
    public void TheKeysGoToThePageOnThePcUsedLast()
    {
        PretendMidiDevice apc = PlugAndUse(Map(MidiMapping.Note, 1, "stack.play"), Map(MidiMapping.Note, 2, "memory.3"));
        _midi.Claim("a");
        _midi.Claim("b");
        Assert.Equal(
            [("a", true), ("a", false), ("b", true)],
            _pages.Roles.Select(role => (role.Connection, role.Role.Controls)));
        Assert.Equal(["stack.play", "memory.3"], _pages.Roles[^1].Role.Keys);

        apc.Note(1, 127);
        apc.Note(1, 0);
        Assert.Equal([("b", "stack.play", true), ("b", "stack.play", false)], _pages.Keys);

        _midi.Leave("b");                                         // that page closed: the one before takes over
        Assert.Equal(("a", true), (_pages.Roles[^1].Connection, _pages.Roles[^1].Role.Controls));
        apc.Note(2, 127);
        Assert.Equal(("a", "memory.3", true), _pages.Keys[^1]);
    }

    [Fact]
    public void WithoutAPageOnThePcTheKeysDoNothingAndTheFadersStillMove()
    {
        PretendMidiDevice apc = PlugAndUse(Map(MidiMapping.Note, 1, "stack.play"), Map(MidiMapping.Controller, 48, "fader.master"));
        _midi.Watch("watcher", on: true);
        apc.Note(1, 127);
        apc.Controller(48, 127);
        Assert.Empty(_pages.Keys);
        Assert.Equal(255, _desk[FaderKind.Master]);
        _midi.Tick();
        Assert.Equal(["no-page", "moved"], _pages.Lines.SelectMany(sent => sent.Lines).Select(line => line.Result));
    }

    [Fact]
    public void OnlyThePageThatPressesTheKeysTellsTheLampsAndThePages()
    {
        PretendMidiDevice apc = PlugAndUse(Map(MidiMapping.Note, 1, "memory.5"));
        _midi.Claim("a");
        _midi.ReportLamps("b", [new MidiLamp("memory.5", 3, false)]);
        _midi.ReportPages("b", MidiPages.First with { FirstChannel = 33 });
        _midi.Tick();
        Assert.Empty(apc.Sent);
        Assert.Equal(1, _desk.Pages.FirstChannel);

        _midi.ReportLamps("a", [new MidiLamp("memory.5", 3, false)]);
        _midi.ReportPages("a", MidiPages.First with { FirstChannel = 33 });
        _midi.Tick();
        Assert.Equal([new MidiMessage(MidiKind.NoteOn, 1, 1, 127)], apc.Sent);
        Assert.Equal(33, _desk.Pages.FirstChannel);
    }

    [Fact]
    public void TheLightsFollowTheLampsAndBlinkWhereTheControllerCannot()
    {
        MidiLights apcLights = new(new MidiLight(0), new MidiLight(9, 4), new MidiLight(9, 7), new MidiLight(9, 15));
        PretendMidiDevice apc = PlugAndUse(Map(MidiMapping.Note, 1, "memory.5"), Map(MidiMapping.Note, 2, "memory.6") with { Lights = apcLights });
        _midi.Claim("pc");

        _midi.ReportLamps("pc", [new MidiLamp("memory.5", 2, false), new MidiLamp("memory.6", 2, false)]);
        _midi.Tick();
        Assert.Equal([new(MidiKind.NoteOn, 1, 1, 0), new MidiMessage(MidiKind.NoteOn, 4, 2, 9)], apc.Sent);   // half: off on one color, the half brightness of the APC's channel 4

        apc.ClearSent();
        _midi.Tick();
        Assert.Empty(apc.Sent);                                   // nothing changed, nothing goes

        _midi.ReportLamps("pc", [new MidiLamp("memory.5", 3, true), new MidiLamp("memory.6", 3, true)]);
        _now = 10.1;   // the lit half of the desk's blinking
        _midi.Tick();
        _now = 10.3;   // the dark half
        _midi.Tick();
        Assert.Equal(
            [new(MidiKind.NoteOn, 1, 1, 127), new(MidiKind.NoteOn, 15, 2, 9), new MidiMessage(MidiKind.NoteOn, 1, 1, 0)],
            apc.Sent);   // the plain light is blinked by the desk; the APC's blinks by itself, sent once
    }

    [Fact]
    public void AKeyWithoutALightGetsNoneAndALightGoesWhereItsControllerWantsIt()
    {
        // the X-Touch Mini lights the key of Note 8 (channel 11) with Note 0 on channel 1
        MidiLights mini = new(new MidiLight(0, 1), new MidiLight(0, 1), new MidiLight(1, 1), new MidiLight(2, 1), Number: 0);
        PretendMidiDevice apc = PlugAndUse(
            Map(MidiMapping.Note, 8, "memory.5", channel: 11) with { Lights = mini },
            Map(MidiMapping.Note, 0, "memory.6", channel: 11) with { Feedback = false });
        _midi.Claim("pc");
        _midi.ReportLamps("pc", [new MidiLamp("memory.5", 3, false), new MidiLamp("memory.6", 3, false)]);
        _midi.Tick();
        Assert.Equal([new MidiMessage(MidiKind.NoteOn, 1, 0, 1)], apc.Sent);

        apc.ClearSent();
        _midi.ReportLamps("pc", [new MidiLamp("memory.5", 3, true)]);
        _midi.Tick();
        Assert.Equal([new MidiMessage(MidiKind.NoteOn, 1, 0, 2)], apc.Sent);   // it blinks by itself
    }

    [Fact]
    public void AKeyPressedGetsTheDesksLightAgain()
    {
        // a controller that lights a key itself while it is held: after the press the desk's light goes again
        PretendMidiDevice apc = PlugAndUse(Map(MidiMapping.Note, 1, "stack.live"));
        _midi.Claim("pc");
        _midi.ReportLamps("pc", [new MidiLamp("stack.live", 1, false)]);
        _midi.Tick();
        apc.ClearSent();

        apc.Note(1, 127);
        apc.Note(1, 0);
        _midi.Tick();
        Assert.Equal([new MidiMessage(MidiKind.NoteOn, 1, 1, 0)], apc.Sent);
    }

    [Fact]
    public void AMotorTheDeskDidNotFollowGoesBackToTheDesksValue()
    {
        MidiMapping motor = Map(MidiMapping.PitchBend, 0, "fader.sequence.3") with { Feedback = true };
        PretendMidiDevice apc = PlugAndUse(motor);
        _desk[FaderKind.Sequence, 3] = 128;
        _midi.Tick();
        apc.ClearSent();

        _desk.Refusing = "Memory 3 holds no sequence.";
        apc.Bend(0);                                              // the hand takes it down; the desk does not follow
        _now += MidiFeedback.HandsOff + 0.1;
        _midi.Tick();
        Assert.Equal([new MidiMessage(MidiKind.PitchBend, 1, 0, 8224)], apc.Sent);
    }

    [Fact]
    public void AMotorizedFaderGetsTheDesksValueButNotWhileTheHandMovesIt()
    {
        MidiMapping motor = Map(MidiMapping.PitchBend, 0, "fader.channel.1") with { Feedback = true };
        MidiMapping ring = Map(MidiMapping.Controller, 16, "fader.master") with { Mode = MidiMapping.RelativeTwos, Feedback = true };
        MidiMapping fine = Map(MidiMapping.Controller14, 7, "fader.memory.1") with { Feedback = true };
        PretendMidiDevice apc = PlugAndUse(motor, ring, fine);
        _desk[FaderKind.Channel, 1] = 128;
        _desk[FaderKind.Master] = 255;
        _desk[FaderKind.Memory, 1] = 0;

        _midi.Tick();
        Assert.Equal(
            [
                new(MidiKind.PitchBend, 1, 0, 8224),
                new(MidiKind.ControlChange, 1, 16, 127),
                new(MidiKind.ControlChange, 1, 7, 0),
                new MidiMessage(MidiKind.ControlChange, 1, 39, 0),
            ],
            apc.Sent);

        apc.ClearSent();
        apc.Bend(16383);                                          // the hand moves the fader to the top
        _now += 0.1;
        _midi.Tick();
        Assert.Empty(apc.Sent);                                   // the motor does not fight the hand
        _now += MidiFeedback.HandsOff;
        _midi.Tick();
        Assert.Equal([new MidiMessage(MidiKind.PitchBend, 1, 0, 16383)], apc.Sent);

        apc.ClearSent();
        _desk[FaderKind.Channel, 1] = 0;                          // a page, a phone or the stack took it down
        _midi.Tick();
        Assert.Equal([new MidiMessage(MidiKind.PitchBend, 1, 0, 0)], apc.Sent);
    }

    [Fact]
    public void ADevicePluggedInAgainGetsEverythingAgain()
    {
        PretendMidiDevice apc = PlugAndUse(Map(MidiMapping.Note, 1, "memory.5"));
        _midi.Claim("pc");
        _midi.ReportLamps("pc", [new MidiLamp("memory.5", 3, false)]);
        _midi.Tick();
        _ports.Unplug(Apc);
        _midi.Look();
        _ports.Plug(Apc);
        _midi.Look();
        apc.ClearSent();
        _midi.Tick();
        Assert.Equal([new MidiMessage(MidiKind.NoteOn, 1, 1, 127)], apc.Sent);
    }

    [Fact]
    public void UsingADeviceNoMoreLetsGoOfTheKeysItHolds()
    {
        PretendMidiDevice apc = PlugAndUse(Map(MidiMapping.Note, 1, "master.solo"));
        _midi.Claim("pc");
        apc.Note(1, 127);
        _midi.Use(Apc, use: false);
        Assert.Equal([("pc", "master.solo", true), ("pc", "master.solo", false)], _pages.Keys);
    }

    [Fact]
    public void TheSetupIsKeptInMidiJsonAndReadAgain()
    {
        PlugAndUse(Map(MidiMapping.Note, 1, "memory.5"), Map(MidiMapping.Controller, 48, "fader.slot.channel.1") with { Pickup = true });
        using MidiControl again = Make();
        Assert.Equal([Apc], again.Setup.Devices);
        Assert.Equal(_midi.Setup.Mappings, again.Setup.Mappings);
        Assert.Contains("\"format\": \"lumenet-midi\"", File.ReadAllText(SetupFile), StringComparison.Ordinal);
    }

    [Fact]
    public void AMidiJsonThatCannotBeReadIsPutAside()
    {
        File.WriteAllText(SetupFile, "{ not json");
        using MidiControl again = Make();
        Assert.Empty(again.Setup.Mappings);
        Assert.True(File.Exists(SetupFile + ".bad"));
    }

    [Fact]
    public void AMappingThatIsNotPossibleIsRefusedWithTheReason()
    {
        PlugAndUse(Map(MidiMapping.Note, 1, "memory.5"));
        Assert.Contains("1 to 16", Assert.Throws<DeskException>(() => _midi.SetMappings([Map(MidiMapping.Note, 1, "memory.5", channel: 17)])).Message, StringComparison.Ordinal);
        Assert.Contains("no key or fader", Assert.Throws<DeskException>(() => _midi.SetMappings([Map(MidiMapping.Note, 1, "fader.colour.1")])).Message, StringComparison.Ordinal);
        Assert.Contains("not a note", Assert.Throws<DeskException>(() => _midi.SetMappings([Map(MidiMapping.Note, 1, "fader.master")])).Message, StringComparison.Ordinal);
        Assert.Contains("no key or fader", Assert.Throws<DeskException>(() => _midi.Learn("nothing here")).Message, StringComparison.Ordinal);
        Assert.Equal("memory.5", Assert.Single(_midi.Setup.Mappings).Target);   // as it was
    }

    [Fact]
    public void ThePagesThatShowTheListGetTheIncomingMessages()
    {
        PretendMidiDevice apc = PlugAndUse(Map(MidiMapping.Controller, 48, "fader.master"));
        apc.Controller(48, 10);
        MidiMonitorLine before = Assert.Single(_midi.Watch("a", on: true));   // the last ones at once
        Assert.Equal((Apc, "CC 48 = 10, channel 1", "fader.master", "moved", 20), (before.Device, before.Message, before.Target, before.Result, before.Value));

        apc.Controller(49, 10);
        _midi.Tick();
        (string[] watchers, MidiMonitorLine[] lines) = Assert.Single(_pages.Lines);
        Assert.Equal(["a"], watchers);
        Assert.Equal("unmapped", Assert.Single(lines).Result);

        _midi.Watch("a", on: false);
        apc.Controller(49, 11);
        _midi.Tick();
        Assert.Single(_pages.Lines);
    }

    [Fact]
    public void EveryChangeGoesToThePagesOnThePc()
    {
        _ports.Plug(Apc);
        _midi.Look();
        int before = _pages.States.Count;
        _midi.Use(Apc, use: true);
        _midi.Learn("memory.1");
        Assert.True(_pages.States.Count > before);
        Assert.Equal("memory.1", _pages.Last.Learning);
        Assert.Equal("open", _pages.Last.Devices.Single(device => device.Name == Apc).State);
    }
}
