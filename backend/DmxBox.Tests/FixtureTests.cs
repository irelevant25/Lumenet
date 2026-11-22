using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// The lights of the stage plan as fixtures: the attributes of a moving head (a pan, a tilt) come
/// from the fader that took them last and keep their value through the master, blackout and a
/// solo; a light's limit; the kinds of lights from the library and the groups in the plan and in
/// the project file.
/// </summary>
public sealed class FixtureTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;

    public FixtureTests()
    {
        _desk = new Desk(_time);
    }

    // A moving head at channel 1: pan, tilt, dimmer.
    private static StageLight Head(int id = 1, int address = 1) => new(id, "Spot", address, "PTD", null, 4, 4, 0);

    private static StagePlan Plan(params StageLight[] lights) => new(16, 10, lights);

    private int[] Output(int from, int count) => [.. _desk.GetOutput().Skip(from - 1).Take(count).Select(level => (int)level)];

    // Memory 1 holds the head at pan 10, tilt 20 and full; memory 2 at pan 200, tilt 220 and full.
    private void StoreTwoPlaces()
    {
        _desk.SetStage(Plan(Head()));
        _desk.SetChannel(1, 10);
        _desk.SetChannel(2, 20);
        _desk.SetChannel(3, 255);
        _desk.SaveMemory(1);
        _desk.SetChannel(1, 200);
        _desk.SetChannel(2, 220);
        _desk.SaveMemory(2);
        _desk.Clear();
    }

    [Fact]
    public void MasterBlackoutAndSoloDimTheLightAndLeaveTheHeadWhereItIs()
    {
        _desk.SetStage(Plan(Head()));
        _desk.SetChannel(1, 100);
        _desk.SetChannel(2, 50);
        _desk.SetChannel(3, 255);

        _desk.SetMaster(128);
        Assert.Equal([100, 50, 128], Output(1, 3));

        _desk.SetBlackout(true);
        Assert.Equal([100, 50, 0], Output(1, 3));

        _desk.SetBlackout(false);
        _desk.SetMaster(255);
        _desk.HoldSolo("a page", up: true, "channel", 9, held: true);   // another light alone
        Assert.Equal([100, 50, 0], Output(1, 3));
    }

    [Fact]
    public void TheMemoryRaisedLastGivesTheHeadItsPlaceAsStoredNotScaledByItsFader()
    {
        StoreTwoPlaces();

        _desk.SetMemoryLevel(1, 128);
        Assert.Equal([10, 20, 128], Output(1, 3));   // the dimmer at half, the place as stored

        _desk.SetMemoryLevel(2, 255);
        Assert.Equal([200, 220, 255], Output(1, 3));

        _desk.SetMemoryLevel(1, 255);   // moved, but it did not leave zero: memory 2 keeps the head
        Assert.Equal([200, 220, 255], Output(1, 3));

        _desk.SetMemoryLevel(2, 0);     // back to the one that took it before
        Assert.Equal([10, 20, 255], Output(1, 3));

        _desk.SetMemoryLevel(1, 0);     // no memory left: the channel faders
        Assert.Equal([0, 0, 0], Output(1, 3));
    }

    [Fact]
    public void AFaderMovedLaterTakesItsOwnChannelOnly()
    {
        StoreTwoPlaces();
        _desk.SetMemoryLevel(1, 255);

        _desk.SetChannel(1, 99);
        Assert.Equal([99, 20, 255], Output(1, 3));

        _desk.SetMemoryLevel(2, 255);   // raised later still
        Assert.Equal([200, 220, 255], Output(1, 3));
    }

    [Fact]
    public void AMemoryThatDoesNotHoldTheHeadLeavesItAlone()
    {
        StoreTwoPlaces();
        _desk.SetChannel(9, 255);
        _desk.SaveMemory(3);   // channel 9 only
        _desk.SetChannel(9, 0);

        _desk.SetMemoryLevel(1, 255);
        _desk.SetMemoryLevel(3, 255);

        Assert.Equal([10, 20, 255], Output(1, 3));
        Assert.Equal(255, _desk.GetOutput()[8]);
    }

    [Fact]
    public void ASequenceGivesTheHeadWhatItsStepGivesAndTellsTheUisSo()
    {
        StoreTwoPlaces();
        _desk.SetSequence(3, loop: true, [new Step("memory", 1, 255, 0, 1, 0), new Step("memory", 2, 255, 0, 1, 0)]);

        _desk.SetMemoryLevel(3, 255);
        _time.Pass(0.5);
        Assert.Equal([10, 20], Output(1, 2));

        _time.Pass(1);
        Assert.Equal([200, 220], Output(1, 2));
        Assert.Equal([200, 220], _desk.GetPlayback().Levels[..2]);
    }

    [Fact]
    public void ALimitHoldsDimmersAndDevicesAndDimsTheColorsOfACellTogether()
    {
        _desk.SetStage(Plan(
            new StageLight(1, "Hazer", 10, "X", null, 1, 1, 0) { Limit = 60 },
            new StageLight(2, "Par", 20, "RGB", null, 3, 1, 0) { Limit = 50 },
            new StageLight(3, "Front", 30, "D", null, 5, 1, 0) { Limit = 50 }));
        _desk.SetChannel(10, 255);
        _desk.SetChannel(20, 255);
        _desk.SetChannel(21, 128);
        _desk.SetChannel(30, 200);

        Assert.Equal(153, _desk.GetOutput()[9]);
        Assert.Equal([128, 64, 0], Output(20, 3));   // orange stays orange
        Assert.Equal(128, _desk.GetOutput()[29]);

        _desk.SetChannel(10, 100);   // below the limit: as it is
        Assert.Equal(100, _desk.GetOutput()[9]);

        _desk.SetMaster(128);        // the master scales what the limit leaves
        _desk.SetChannel(10, 255);
        Assert.Equal(77, _desk.GetOutput()[9]);
    }

    [Fact]
    public void ThePlanKeepsKindsOfLightsThatAreUsedAndGroupsOfLightsThatAreOnIt()
    {
        FixtureType spot = SpotType(1);
        FixtureType unused = SpotType(2);
        StagePlan plan = Plan(Head() with { Type = 1, Limit = 70 }, Head(2, 4)) with
        {
            Types = [spot, unused],
            Groups = [new LightGroup(1, " Heads ", [2, 1, 7, 2])],
        };

        StagePlan kept = plan.Checked();

        Assert.Equal([1], kept.Types.Select(type => type.Id));
        Assert.Equal("Heads", kept.Groups[0].Name);
        Assert.Equal([2, 1], kept.Groups[0].Lights);
        Assert.Equal(70, kept.Lights[0].Limit);

        AssertRefused("Light 1 of the plan is of a kind of light the plan does not have, or its channels do not match it.",
            plan with { Lights = [Head() with { Type = 1, Channels = "PT" }] });
        AssertRefused("Light 1 of the plan is of a kind of light the plan does not have, or its channels do not match it.",
            plan with { Lights = [Head() with { Type = 3 }] });
        AssertRefused("Light 1 of the plan can be limited to 1 to 100 per cent, not 0.",
            plan with { Lights = [Head() with { Limit = 0 }] });
        AssertRefused("Kind 1 of the plan's lights, channel 2 is the fine part of a channel the light does not have.",
            plan with { Types = [spot with { Channels = [spot.Channels[0], spot.Channels[1] with { Fine = 1 }, spot.Channels[2]] }] });
        AssertRefused("Kind 1 of the plan's lights, channel 1, range 1 must lie within 0 to 255, not 10 to 5.",
            plan with { Types = [spot with { Channels = [spot.Channels[0] with { Ranges = [new FixtureRange(10, 5, "Open")] }, spot.Channels[1], spot.Channels[2]] }] });
    }

    [Fact]
    public void TheProjectFileKeepsKindsGroupsAndLimits()
    {
        var wheel = new FixtureChannel("Color Wheel", "X") { Ranges = [new FixtureRange(0, 7, "Open"), new FixtureRange(8, 14, "Red") { Color = "#ff0000" }] };
        var type = new FixtureType(1, "Maker", "Spot", "4-channel", "maker/spot",
            [new FixtureChannel("Pan", "P"), new FixtureChannel("Pan fine", "F") { Fine = 0 }, new FixtureChannel("Dimmer", "D"), wheel]);
        StagePlan plan = Plan(
            new StageLight(1, "Spot 1", 1, "PFDX", null, 4, 4, 0) { Type = 1 },
            new StageLight(2, "Hazer", 9, "X", null, 1, 1, 0) { Limit = 60 }) with
        {
            Types = [type],
            Groups = [new LightGroup(1, "All", [1, 2])],
        };
        _desk.SetStage(plan);

        using var stream = new MemoryStream();
        ProjectFormat.Write(stream, _desk.GetProject().Content);
        string text = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("\"type\": 1", text);
        Assert.Contains("\"limit\": 60", text);
        Assert.Contains("\"fine\": 0", text);
        Assert.Contains("\"color\": \"#ff0000\"", text);

        using var again = new MemoryStream(stream.ToArray());
        StagePlan read = ProjectFormat.Read(again).Stage!;
        Assert.True(read.SameAs(_desk.Stage));
    }

    [Fact]
    public void AKindComesFromTheOpenFixtureLibraryOrFromQlcPlus()
    {
        StagePlan plan = Plan(new StageLight(1, "Bar", 1, "PTD", null, 4, 4, 0) { Type = 1 });
        StagePlan With(string source) => plan with { Types = [SpotType(1) with { Source = source }] };

        Assert.Equal("maker/spot", With("maker/spot").Checked().Types[0].Source);
        Assert.Equal(
            "qlcplus:Cameo/Cameo-Studio-PAR-64-Q-8W-(CLPST64Q8W).qxf",
            With("qlcplus:Cameo/Cameo-Studio-PAR-64-Q-8W-(CLPST64Q8W).qxf").Checked().Types[0].Source);
        AssertRefused("Kind 1 of the plan's lights: \"maker/<spot>\" is not where a light is in the library.", With("maker/<spot>"));
        AssertRefused("Kind 1 of the plan's lights: \"a b\" is not where a light is in the library.", With("a b"));
    }

    [Fact]
    public void FadersThatTakeSomethingGetEverLaterNumbersInTheState()
    {
        _desk.SetChannel(5, 1);
        _desk.SetMemoryLevel(2, 1);
        _desk.SetChannel(6, 1);

        DeskState state = _desk.GetState();
        Assert.True(state.ChannelClaims[4] > 0);
        Assert.True(state.MemoryClaims[1] > state.ChannelClaims[4]);
        Assert.True(state.ChannelClaims[5] > state.MemoryClaims[1]);
        Assert.Equal(0, state.ChannelClaims[0]);
    }

    private static FixtureType SpotType(int id) => new(id, "Maker", "Spot", "3-channel", "maker/spot",
        [new FixtureChannel("Pan", "P"), new FixtureChannel("Tilt", "T"), new FixtureChannel("Dimmer", "D")]);

    private static void AssertRefused(string message, StagePlan plan)
    {
        DeskException error = Assert.Throws<DeskException>(() => plan.Checked());
        Assert.Equal(message, error.Message);
    }
}
