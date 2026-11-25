using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// The messages of a controller turned into the desk's keys and faders: the scales, 14 bits, the
/// endless encoders, the soft takeover of faders without motors, the slots that follow the pages,
/// and keys pressed and let go.
/// </summary>
public sealed class MidiMapperTests
{
    private const string Device = "Controller";

    private readonly PretendMidiDesk _desk = new();
    private readonly List<(string Key, bool Down)> _keys = [];
    private readonly MidiMapper _mapper;
    private bool _pageThere = true;
    private double _now;

    public MidiMapperTests()
    {
        _mapper = new MidiMapper(_desk, (key, down) =>
        {
            if (!_pageThere)
                return false;
            _keys.Add((key, down));
            return true;
        });
    }

    private static MidiMapping Map(string message, int number, string target, string mode = MidiMapping.Absolute, bool pickup = false, int channel = 1) =>
        MidiSetupCheck.Checked(new MidiMapping { Device = Device, Channel = channel, Message = message, Number = number, Target = target, Mode = mode, Pickup = pickup });

    private MidiOutcome Play(MidiKind kind, int number, int value, int channel = 1, string device = Device)
    {
        _now += 0.01;
        return _mapper.Handle(device, new MidiMessage(kind, channel, number, value), _now);
    }

    private MidiOutcome Controller(int number, int value) => Play(MidiKind.ControlChange, number, value);

    [Fact]
    public void AControllerFromZeroTo127MovesAFaderFromZeroToFull()
    {
        _mapper.Use([Map(MidiMapping.Controller, 7, "fader.master")]);

        Assert.Equal(new MidiOutcome("fader.master", "moved", 0), Controller(7, 0));
        Assert.Equal(129, Controller(7, 64).Value);
        Assert.Equal(255, Controller(7, 127).Value);
        Assert.Equal(255, _desk[FaderKind.Master]);
        Assert.Equal("unmapped", Controller(8, 64).Result);
        Assert.Equal("unmapped", Play(MidiKind.ControlChange, 7, 64, channel: 2).Result);   // another channel
        Assert.Equal("unmapped", Play(MidiKind.ControlChange, 7, 64, device: "Other").Result);
    }

    [Fact]
    public void FourteenBitsComeFromACoarseControllerAndTheOne32AboveIt()
    {
        _mapper.Use([Map(MidiMapping.Controller14, 7, "fader.channel.5")]);

        Controller(7, 64);                                       // 64 x 128 = 8192: half way
        Assert.Equal(128, _desk[FaderKind.Channel, 5]);
        Controller(39, 127);                                     // and the fine half: 8319
        Assert.Equal(129, _desk[FaderKind.Channel, 5]);
        Controller(7, 127);
        Controller(39, 127);                                     // 16383: full
        Assert.Equal(255, _desk[FaderKind.Channel, 5]);
        Controller(7, 0);
        Assert.Equal(0, _desk[FaderKind.Channel, 5]);            // the coarse half alone stands for a fine half of 0
    }

    [Fact]
    public void ThePitchBendOfAMackieFaderIsFourteenBits()
    {
        _mapper.Use([Map(MidiMapping.PitchBend, 0, "fader.memory.3", channel: 2)]);

        Play(MidiKind.PitchBend, 0, 8192, channel: 2);
        Assert.Equal(128, _desk[FaderKind.Memory, 3]);
        Play(MidiKind.PitchBend, 0, 16383, channel: 2);
        Assert.Equal(255, _desk[FaderKind.Memory, 3]);
        Assert.Equal("unmapped", Play(MidiKind.PitchBend, 0, 0, channel: 1).Result);
    }

    [Theory]
    [InlineData(MidiMapping.RelativeTwos, 1, 127, 3)]
    [InlineData(MidiMapping.RelativeOffset, 65, 63, 67)]
    [InlineData(MidiMapping.RelativeSign, 1, 65, 3)]
    public void AnEndlessEncoderMovesTheFaderFromWhereItIs(string mode, int up, int down, int threeUp)
    {
        _mapper.Use([Map(MidiMapping.Controller, 16, "fader.master", mode)]);
        _desk[FaderKind.Master] = 100;

        Controller(16, up);
        Assert.Equal(100 + MidiMapper.RelativeStep, _desk[FaderKind.Master]);
        Controller(16, down);
        Assert.Equal(100, _desk[FaderKind.Master]);
        Controller(16, threeUp);                                 // turned faster: three ticks at once
        Assert.Equal(100 + (3 * MidiMapper.RelativeStep), _desk[FaderKind.Master]);

        _desk[FaderKind.Master] = 254;
        Controller(16, up);
        Assert.Equal(255, _desk[FaderKind.Master]);              // no further than full
        _desk[FaderKind.Master] = 1;
        Controller(16, down);
        Assert.Equal(0, _desk[FaderKind.Master]);
    }

    [Fact]
    public void AFaderWithoutAMotorTakesOverOnlyWhenItReachesTheDesksValue()
    {
        _mapper.Use([Map(MidiMapping.Controller, 1, "fader.channel.1", pickup: true)]);
        _desk[FaderKind.Channel, 1] = 200;

        Assert.Equal(new MidiOutcome("fader.channel.1", "pickup", 12, 200), Controller(1, 6));
        Assert.Equal("pickup", Controller(1, 50).Result);
        Assert.Equal(200, _desk[FaderKind.Channel, 1]);          // the light did not jump
        Assert.Equal("moved", Controller(1, 99).Result);         // 199: near enough
        Assert.Equal(199, _desk[FaderKind.Channel, 1]);
        Assert.Equal("moved", Controller(1, 30).Result);         // from now on it follows the hand
        Assert.Equal(60, _desk[FaderKind.Channel, 1]);
    }

    [Fact]
    public void AFaderThatPassesTheDesksValueTakesOverAsItPasses()
    {
        _mapper.Use([Map(MidiMapping.Controller, 1, "fader.channel.1", pickup: true)]);
        _desk[FaderKind.Channel, 1] = 100;

        Assert.Equal("pickup", Controller(1, 5).Result);         // 10, below the desk's 100
        Assert.Equal("moved", Controller(1, 120).Result);        // a quick move to 241 passed 100
        Assert.Equal(241, _desk[FaderKind.Channel, 1]);
    }

    [Fact]
    public void ATakeoverIsLostWhenSomethingElseMovesTheFader()
    {
        _mapper.Use([Map(MidiMapping.Controller, 1, "fader.channel.1", pickup: true)]);
        _desk[FaderKind.Channel, 1] = 0;
        Controller(1, 0);
        Controller(1, 30);
        Assert.Equal(60, _desk[FaderKind.Channel, 1]);

        _desk[FaderKind.Channel, 1] = 200;                       // a page, a phone or the stack moved it
        Assert.Equal("pickup", Controller(1, 31).Result);
        Assert.Equal(200, _desk[FaderKind.Channel, 1]);
    }

    [Fact]
    public void AFaderWithoutPickupFollowsAtOnce()
    {
        _mapper.Use([Map(MidiMapping.Controller, 1, "fader.channel.1", pickup: false)]);
        _desk[FaderKind.Channel, 1] = 200;
        Assert.Equal("moved", Controller(1, 6).Result);
        Assert.Equal(12, _desk[FaderKind.Channel, 1]);
    }

    [Fact]
    public void ASlotFollowsThePageThePcShows()
    {
        _mapper.Use([Map(MidiMapping.Controller, 1, "fader.slot.channel.1"), Map(MidiMapping.Controller, 2, "fader.slot.channel.40"), Map(MidiMapping.Controller, 3, "fader.slot.memory.2")]);
        _desk.Pages = MidiPages.First with { FirstChannel = 33, FirstMemory = 17 };

        Controller(1, 127);
        Assert.Equal(255, _desk[FaderKind.Channel, 33]);
        Controller(3, 127);
        Assert.Equal(255, _desk[FaderKind.Memory, 18]);
        Assert.Equal("missing", Controller(2, 127).Result);       // a page of 32 has no 40th fader

        _desk.Pages = _desk.Pages with { FirstChannel = 65 };     // the page keys moved on
        Controller(1, 64);
        Assert.Equal(129, _desk[FaderKind.Channel, 65]);
        Assert.Equal(255, _desk[FaderKind.Channel, 33]);
    }

    [Fact]
    public void ASlotOnAnotherFaderTakesItOverAnew()
    {
        _mapper.Use([Map(MidiMapping.Controller, 1, "fader.slot.channel.1", pickup: true)]);
        _desk[FaderKind.Channel, 1] = 0;
        _desk[FaderKind.Channel, 33] = 255;
        Controller(1, 0);
        Controller(1, 10);
        Assert.Equal(20, _desk[FaderKind.Channel, 1]);

        _desk.Pages = MidiPages.First with { FirstChannel = 33 };
        Assert.Equal("pickup", Controller(1, 11).Result);         // channel 33 stands at full: the hand has to reach it
        Assert.Equal(255, _desk[FaderKind.Channel, 33]);
    }

    [Fact]
    public void AFaderThatIsNotThereMovesNothing()
    {
        _mapper.Use([Map(MidiMapping.Controller, 1, "fader.sequence.4"), Map(MidiMapping.Controller, 2, "fader.channel.600")]);
        _desk.Missing.Add(new DeskFader(FaderKind.Sequence, 4));
        Assert.Equal("missing", Controller(1, 64).Result);       // memory 4 holds no sequence
        Assert.Equal("missing", Controller(2, 64).Result);       // a project of one universe has 512 channels
        Assert.Empty(_desk.Moves);
    }

    [Fact]
    public void AMoveTheDeskRefusesSaysWhy()
    {
        _mapper.Use([Map(MidiMapping.Controller, 1, "fader.sequence.4")]);
        _desk.Refusing = "The stack is live: nothing that is stored can be changed.";
        MidiOutcome outcome = Controller(1, 64);
        Assert.Equal("refused", outcome.Result);
        Assert.Equal(_desk.Refusing, outcome.Reason);
    }

    [Fact]
    public void KeysArePressedAndLetGoByNotesControllersAndPrograms()
    {
        _mapper.Use([Map(MidiMapping.Note, 56, "memory.5"), Map(MidiMapping.Controller, 41, "stack.play"), Map(MidiMapping.Program, 3, "view.stage")]);

        Assert.Equal("down", Play(MidiKind.NoteOn, 56, 127).Result);
        Play(MidiKind.NoteOn, 56, 100);                             // pressed again without letting go: once is enough
        Assert.Equal("up", Play(MidiKind.NoteOff, 56, 0).Result);
        Controller(41, 127);
        Controller(41, 0);
        Play(MidiKind.ProgramChange, 3, 0);                         // no letting go of its own: at once

        Assert.Equal([("memory.5", true), ("memory.5", false), ("stack.play", true), ("stack.play", false), ("view.stage", true), ("view.stage", false)], _keys);
    }

    [Fact]
    public void WithoutAPageOnThePcAKeyDoesNothing()
    {
        _mapper.Use([Map(MidiMapping.Note, 56, "memory.5")]);
        _pageThere = false;
        Assert.Equal("no-page", Play(MidiKind.NoteOn, 56, 127).Result);
        _pageThere = true;
        Assert.Equal("up", Play(MidiKind.NoteOff, 56, 0).Result);
        Assert.Empty(_keys);                                        // nothing was held, so nothing is let go
    }

    [Fact]
    public void ADeviceThatGoesLetsGoOfTheKeysItHolds()
    {
        _mapper.Use([Map(MidiMapping.Note, 1, "master.solo"), Map(MidiMapping.Note, 2, "channel.3")]);
        Play(MidiKind.NoteOn, 2, 127);
        _mapper.LetGo("Other");
        Assert.Equal([("channel.3", true)], _keys);
        _mapper.LetGo(Device);
        Assert.Equal([("channel.3", true), ("channel.3", false)], _keys);
    }

    [Fact]
    public void AMappingTakenAwayLetsGoOfItsKey()
    {
        _mapper.Use([Map(MidiMapping.Note, 2, "channel.3")]);
        Play(MidiKind.NoteOn, 2, 127);
        _mapper.Use([]);
        Assert.Equal([("channel.3", true), ("channel.3", false)], _keys);
    }

    [Fact]
    public void APairOf14BitsTakesItsControllersFromSingleOnes()
    {
        _mapper.Use([Map(MidiMapping.Controller, 39, "fader.master"), Map(MidiMapping.Controller14, 7, "fader.channel.1")]);
        Controller(39, 127);
        Assert.Equal(0, _desk[FaderKind.Master]);                 // CC 39 is the fine half of the pair now
    }
}
