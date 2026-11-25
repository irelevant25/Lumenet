using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// How effects mix with the rest of the desk, frame by frame, as the plan of 2026-11-24 says:
/// intensity highest wins, a color effect takes over the color and lights a dark light by its fader
/// (unless an intensity effect plays it), a pixel pattern paints only where it is bright, a movement
/// moves a head from where it points; the master, blackout, a solo and the limits act on light only,
/// so a head never stops or jumps.
/// </summary>
public sealed class EffectRuleTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;

    public EffectRuleTests()
    {
        _desk = new Desk(_time);
    }

    // A PAR with a dimmer and red, green, blue at 1-4.
    internal static StageLight Par(int id = 1, int address = 1, double x = 2) => new(id, "PAR", address, "DRGB", null, x, 2, 0);

    // A light of red, green and blue without a dimmer at 11-13.
    internal static StageLight Wash(int id = 2, int address = 11) => new(id, "Wash", address, "RGB", null, 4, 2, 0);

    // A moving head of letters at 31-33: pan, tilt, dimmer; 8 bits.
    internal static StageLight Head(int id = 3, int address = 31) => new(id, "Head", address, "PTD", null, 6, 2, 0);

    // A bar of four cells of red, green and blue at 41-52, four squares wide.
    internal static StageLight Bar(int id = 4, int address = 41) => new(id, "Bar", address, "RGBRGBRGBRGB", null, 8, 1, 0) { Shape = StageLight.Square, Width = 4 };

    // A moving head from the library at 61-67: pan, its fine part, tilt, its fine part, dimmer, a
    // shutter (closed at 0-7, open at 8-15, strobe above) and a color wheel.
    internal static StageLight Spot(int id = 5, int address = 61) => new(id, "Spot", address, "PFTFDSX", null, 10, 2, 0) { Type = 1 };

    internal static FixtureType SpotType() => new(1, "Maker", "Spot", "7-channel", "maker/spot",
    [
        new FixtureChannel("Pan", "P"),
        new FixtureChannel("Pan fine", "F") { Fine = 0 },
        new FixtureChannel("Tilt", "T"),
        new FixtureChannel("Tilt fine", "F") { Fine = 2 },
        new FixtureChannel("Dimmer", "D"),
        new FixtureChannel("Shutter", "S") { Ranges = [new(0, 7, "Closed"), new(8, 15, "Open"), new(16, 255, "Strobe slow to fast")] },
        new FixtureChannel("Color wheel", "X") { Ranges = [new(0, 9, "White") { Color = "#ffffff" }, new(10, 19, "Red") { Color = "#ff0000" }, new(20, 29, "Blue") { Color = "#0000ff" }] },
    ]);

    internal static StagePlan Plan(params StageLight[] lights) => new(16, 10, lights) { Types = lights.Any(light => light.Type is not null) ? [SpotType()] : [] };

    private int[] Output(int from, int count) => [.. _desk.GetOutput().Skip(from - 1).Take(count).Select(level => (int)level)];

    // An effect at one cycle a second, without a fade, on the lights given.
    private static Effect On(int number, string kind, string shape, params int[] lights) =>
        new(number, $"Effect {number}", kind, shape) { Lights = lights, Speed = 60, Spread = 0 };

    private void Start(Effect effect)
    {
        _desk.SetEffect(effect);
        _desk.SetEffectControl(effect.Number, "go", 1);
    }

    [Fact]
    public void AnIntensityEffectIsLikeAFaderThatMovesByItselfAndTheHigherWins()
    {
        _desk.SetStage(Plan(Par(), Par(id: 6, address: 5, x: 3)));
        _desk.SetChannel(1, 153);   // the first PAR at 60 %, red
        _desk.SetChannel(2, 255);
        Start(On(1, Effect.IntensityKind, "rampup", 1, 6));

        Assert.Equal([153, 255, 0, 0], Output(1, 4));   // the ramp starts at dark: the look stays
        Assert.Equal([0, 0, 0, 0], Output(5, 4));

        _time.Pass(0.4);   // the ramp at 40 %
        Assert.Equal([153, 255, 0, 0], Output(1, 4));   // less than the look
        Assert.Equal([102, 255, 255, 255], Output(5, 4)); // a dark light without a color comes up white

        _time.Pass(0.4);   // at 80 %
        Assert.Equal([204, 255, 0, 0], Output(1, 4));   // above the look, in its own red
        Assert.Equal([204, 255, 255, 255], Output(5, 4));
    }

    [Fact]
    public void TheFaderScalesTheWaveAndTheSizeSaysHowDeepItGoes()
    {
        _desk.SetStage(Plan(Par()));
        Start(On(1, Effect.IntensityKind, "sine", 1) with { Size = 20 });

        Assert.Equal(204, Output(1, 1)[0]);   // at the bottom of the wave: 80 %
        _time.Pass(0.5);
        Assert.Equal(255, Output(1, 1)[0]);

        _desk.SetEffectControl(1, "level", 50);
        Assert.Equal(128, Output(1, 1)[0]);   // the top at half
    }

    [Fact]
    public void ALightWithoutADimmerKeepsTheMixOfItsColors()
    {
        _desk.SetStage(Plan(Wash()));
        _desk.SetChannel(11, 100);
        _desk.SetChannel(12, 50);
        Start(On(1, Effect.IntensityKind, "pulse", 2));   // full for the first half of each cycle

        Assert.Equal([255, 128, 0], Output(11, 3));   // orange, brighter
    }

    [Fact]
    public void AColorEffectTakesOverTheColorAndLightsADarkLightByItsFader()
    {
        _desk.SetStage(Plan(Par(), Wash(), Par(id: 6, address: 5, x: 3)));
        _desk.SetChannel(1, 255);   // the first PAR up, in blue
        _desk.SetChannel(4, 255);
        _desk.SetChannel(5, 255);   // the second up, without a color: lit, in black
        Start(On(1, Effect.ColorKind, "chase", 1, 2, 6) with { Colors = ["#ff0000"] });

        Assert.Equal([255, 255, 0, 0], Output(1, 4));   // red where it was blue
        Assert.Equal([255, 255, 0, 0], Output(5, 4));   // its dimmer is up: it shows the effect's color
        Assert.Equal([255, 0, 0], Output(11, 3));       // nothing lights the wash: the effect does, in red

        _desk.SetChannel(5, 90);
        Assert.Equal([90, 255, 0, 0], Output(5, 4));    // a lit light keeps its intensity

        // A fade of the rest through dark: no jump, the effect's light gives way as the rest's comes.
        int[] through = [.. new byte[] { 0, 4, 8, 12, 16 }.Select(level =>
        {
            _desk.SetChannel(5, level);
            return Output(5, 1)[0];
        })];
        Assert.Equal([255, 191, 128, 64, 16], through);

        // Nothing lights the first PAR any more: the effect does, as bright as its fader.
        _desk.SetChannel(1, 0);
        _desk.SetChannel(4, 0);
        _desk.SetEffectControl(1, "level", 50);
        Assert.Equal([128, 255, 0, 0], Output(1, 4));
        Assert.Equal([128, 0, 0], Output(11, 3));

        // An intensity effect on the wash gives its brightness: dark at the start of its ramp,
        // half way up after half a cycle, in the color effect's red.
        _desk.SetEffectControl(1, "level", 100);
        Start(On(2, Effect.IntensityKind, "rampup", 2));
        Assert.Equal([0, 0, 0], Output(11, 3));
        _time.Pass(0.5);
        Assert.Equal([128, 0, 0], Output(11, 3));
    }

    [Fact]
    public void TheFaderOfAColorEffectMixesItWithTheColorTheRestGives()
    {
        _desk.SetStage(Plan(Par()));
        _desk.SetChannel(1, 255);
        _desk.SetChannel(4, 255);   // blue
        Start(On(1, Effect.ColorKind, "chase", 1) with { Colors = ["#ff0000"] });

        _desk.SetEffectControl(1, "level", 50);   // 128 of 255
        int[] mixed = Output(1, 4);
        Assert.Equal([255, 255, 0], mixed[..3]);  // half red, half blue: the light as bright as before
        Assert.InRange(mixed[3], 250, 255);
    }

    [Fact]
    public void AColorIsMixedAsTheFixturesViewMixesItAndUltravioletTakesNoPart()
    {
        _desk.SetStage(Plan(new StageLight(1, "RGBWU", 1, "DRGBWU", null, 2, 2, 0)));
        _desk.SetChannel(1, 255);
        _desk.SetChannel(6, 255);   // UV on
        Start(On(1, Effect.ColorKind, "chase", 1) with { Colors = ["#ffffff"] });

        Assert.Equal([255, 0, 0, 0, 255, 0], Output(1, 6));   // white goes to the white channel; UV fades out
    }

    [Fact]
    public void TheColorEffectStartedLastTakesTheLight()
    {
        _desk.SetStage(Plan(Par()));
        _desk.SetChannel(1, 255);
        Start(On(2, Effect.ColorKind, "chase", 1) with { Colors = ["#ff0000"] });
        Start(On(1, Effect.ColorKind, "chase", 1) with { Colors = ["#00ff00"] });

        Assert.Equal([255, 0, 255, 0], Output(1, 4));   // green: effect 1 started last, whatever its number

        _desk.SetEffectControl(2, "go", 0);
        _desk.SetEffectControl(2, "go", 1);
        Assert.Equal([255, 255, 0, 0], Output(1, 4));   // started again, red has the last word
    }

    [Fact]
    public void APixelPatternPaintsOnlyWhereItIsBright()
    {
        _desk.SetStage(Plan(Bar()));
        for (int cell = 0; cell < 4; cell++)
            _desk.SetChannel(43 + (cell * 3), 255);   // every cell blue

        // Red bars, half of the length: at the start the cells at 0, 1/3 and 1 of the bar's length are in a bar.
        Start(new Effect(1, "Bars", Effect.PixelKind, "bars") { Lights = [4], Speed = 60, Colors = ["#ff0000"] });

        Assert.Equal([255, 0, 0], Output(41, 3));   // in a bar: red
        Assert.Equal([0, 0, 255], Output(47, 3));   // between the bars: blue, as the rest has it
    }

    [Fact]
    public void APixelPatternLightsDarkCellsItself()
    {
        _desk.SetStage(Plan(Bar()));
        Start(new Effect(1, "Rainbow", Effect.PixelKind, "rainbow") { Lights = [4], Speed = 60 });

        Assert.Equal([255, 0, 0], Output(41, 3));    // the first cell red: the start of the color circle
        Assert.NotEqual([0, 0, 0], Output(44, 3));
    }

    [Fact]
    public void AMovementMovesAHeadFromWhereItPointsAndItsFaderScalesTheShape()
    {
        _desk.SetStage(Plan(Head()));
        _desk.SetChannel(31, 100);
        _desk.SetChannel(32, 100);
        Start(On(1, Effect.MovementKind, "pan", 3) with { Size = 40 });

        Assert.Equal([100, 100], Output(31, 2));   // a pan swing starts in the middle
        _time.Pass(0.25);
        Assert.Equal([151, 100], Output(31, 2));   // a fifth of the range to the right; the tilt stays

        _desk.SetEffectControl(1, "level", 50);
        Assert.Equal([126, 100], Output(31, 2));   // half as far
    }

    [Fact]
    public void AHeadFromTheLibraryMovesIn16Bits()
    {
        _desk.SetStage(Plan(Spot()));
        _desk.SetChannel(61, 128);
        Start(On(1, Effect.MovementKind, "pan", 5) with { Size = 10 });

        _time.Pass(0.25);
        int[] pan = Output(61, 2);
        int whole = (int)Math.Round(((128 * 256) / 65535.0 + 0.05) * 65535, MidpointRounding.AwayFromZero);
        Assert.Equal([whole >> 8, whole & 0xFF], pan);
        Assert.NotEqual(0, pan[1]);   // the fine channel moves too
    }

    [Fact]
    public void BlackoutTheMasterAndASoloDimTheLightOfEffectsAndLeaveTheHeadsMoving()
    {
        _desk.SetStage(Plan(Head(), Par()));
        _desk.SetChannel(33, 255);
        Start(On(1, Effect.MovementKind, "pan", 3) with { Size = 40 });
        Start(On(2, Effect.IntensityKind, "rampup", 1));
        _time.Pass(0.25);

        _desk.SetMaster(128);
        Assert.Equal([51, 0, 128], Output(31, 3));    // the head moves (a fifth of its range); its dimmer at half
        Assert.Equal(32, Output(1, 1)[0]);            // the ramp at a quarter, at half

        _desk.SetBlackout(true);
        Assert.Equal([51, 0, 0], Output(31, 3));
        Assert.Equal(0, Output(1, 1)[0]);

        _desk.SetBlackout(false);
        _desk.SetMaster(255);
        _desk.HoldSolo("a page", up: true, "channel", 99, held: true);   // another light alone
        Assert.Equal([51, 0, 0], Output(31, 3));
        Assert.Equal(0, Output(1, 1)[0]);
        _desk.DropSolo("a page");

        _desk.HoldSolo("a page", up: false, "channel", 1, held: true);   // the PAR's dimmer held down
        Assert.Equal(0, Output(1, 1)[0]);
    }

    [Fact]
    public void TheLimitOfALightHoldsTheLightOfAnEffect()
    {
        _desk.SetStage(Plan(Par() with { Limit = 50 }));
        Start(On(1, Effect.IntensityKind, "sine", 1));
        _time.Pass(0.5);

        Assert.Equal(128, Output(1, 1)[0]);
    }

    [Fact]
    public void AMovementAroundACenterOfItsOwnTakesTheHeadsThereAsItsFaderComesUp()
    {
        _desk.SetStage(Plan(Head()));
        _desk.SetEffect(On(1, Effect.MovementKind, "circle", 3) with { Size = 0, CenterPan = 100, CenterTilt = 100, FadeIn = 2 });
        _desk.SetEffectControl(1, "go", 1);

        Assert.Equal([0, 0], Output(31, 2));
        _time.Pass(1);
        Assert.Equal([128, 128], Output(31, 2));   // half way there, in the fade-in
        _time.Pass(1);
        Assert.Equal([255, 255], Output(31, 2));

        _desk.SetEffectControl(1, "level", 50);
        Assert.Equal([128, 128], Output(31, 2));   // the fader at half: half way there
    }

    [Fact]
    public void OfTwoMovementsTheOneStartedLastTakesEachAxisItUses()
    {
        _desk.SetStage(Plan(Head()));
        _desk.SetChannel(31, 100);
        _desk.SetChannel(32, 100);
        Start(On(1, Effect.MovementKind, "pan", 3) with { Size = 40 });
        Start(On(2, Effect.MovementKind, "tilt", 3) with { Size = 40 });
        _time.Pass(0.25);

        Assert.Equal([151, 151], Output(31, 2));   // the pan swing keeps the pan, the tilt swing takes the tilt
    }

    [Fact]
    public void AnIntensityOpensAClosedShutterAndAColorEffectTurnsTheColorWheel()
    {
        _desk.SetStage(Plan(Spot()));
        Start(On(1, Effect.IntensityKind, "pulse", 5));
        Assert.Equal([255, 8], Output(65, 2));   // the dimmer up, the shutter open

        _desk.SetChannel(66, 20);                // a strobe: left as it is
        Assert.Equal([255, 20], Output(65, 2));

        Start(On(2, Effect.ColorKind, "chase", 5) with { Colors = ["#0000ff"] });
        Assert.Equal(20, Output(67, 1)[0]);      // the wheel on blue

        // The color effect alone: it lights the head by its fader and opens its closed shutter.
        _desk.SetEffectControl(1, "go", 0);
        _desk.SetChannel(66, 0);
        Assert.Equal([255, 8, 20], Output(65, 3));
    }

    [Fact]
    public void ADeviceTakesNoPartInEffects()
    {
        _desk.SetStage(Plan(new StageLight(1, "Hazer", 1, "XX", null, 2, 2, 0)));
        Start(On(1, Effect.IntensityKind, "pulse", 1));

        Assert.Equal([0, 0], Output(1, 2));
    }
}
