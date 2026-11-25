using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// What the shapes give and where each light stands in an effect's run: the orders, wings and
/// blocks, the shapes of every kind, the pixels of the cells of a light as the Stage view draws them,
/// and the random numbers that are the same for every output at the same moment.
/// </summary>
public sealed class EffectEngineTests
{
    // Eight PARs in a row from the left, their addresses from the right: 8 is at the left.
    private static StagePlan Row() => new(16, 10, [.. Enumerable.Range(1, 8).Select(id =>
        new StageLight(id, $"PAR {id}", 1 + ((8 - id) * 4), "DRGB", null, id, 2, 0))]);

    private static double[] Places(Effect effect) => [.. EffectRig.Of(Row(), effect).Targets.Select(target => target.Place)];

    private static int[] Order(Effect effect) => [.. EffectRig.Of(Row(), effect).Targets.Select(target => target.Light.Id)];

    private static Effect Chase(params int[] lights) => new(1, "Chase", Effect.IntensityKind, "sine") { Lights = lights.Length > 0 ? lights : [.. Enumerable.Range(1, 8)] };

    [Fact]
    public void TheLightsGoInTheOrderOfTheirAddressesFromTheLeftOrAtRandomTheSameEveryTime()
    {
        Assert.Equal([8, 7, 6, 5, 4, 3, 2, 1], Order(Chase()));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], Order(Chase() with { Order = Effect.LeftToRight }));

        int[] random = Order(Chase() with { Order = Effect.RandomOrder, Seed = 7 });
        Assert.Equal(random, Order(Chase() with { Order = Effect.RandomOrder, Seed = 7 }));
        Assert.NotEqual(random, Order(Chase() with { Order = Effect.RandomOrder, Seed = 8 }));
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], random.Order());
    }

    [Fact]
    public void EachLightHasItsPlaceInTheRunAndBlocksMoveAsOne()
    {
        Assert.Equal([0, 0.125, 0.25, 0.375, 0.5, 0.625, 0.75, 0.875], Places(Chase()));
        Assert.Equal([0, 0, 0.25, 0.25, 0.5, 0.5, 0.75, 0.75], Places(Chase() with { Blocks = 2 }));
    }

    [Fact]
    public void WingsSplitTheRunAndEverySecondWingGoesTheOtherWay()
    {
        Assert.Equal([0, 0.25, 0.5, 0.75, 0.75, 0.5, 0.25, 0], Places(Chase() with { Wings = 2 }));
        Assert.Equal([0, 0.5, 0.5, 0, 0, 0.5, 0.5, 0], Places(Chase() with { Wings = 4 }));
    }

    [Fact]
    public void FromTheMiddleOutwardsLightsAsFarOutGoTogether()
    {
        Effect effect = Chase() with { Order = Effect.FromCenter };
        Dictionary<int, double> places = EffectRig.Of(Row(), effect).Targets.ToDictionary(target => target.Light.Id, target => target.Place);

        Assert.Equal(places[4], places[5]);   // both half a square from the middle
        Assert.Equal(places[1], places[8]);   // both at the ends
        Assert.Equal(0, places[4]);
        Assert.True(places[1] > places[3]);
    }

    [Fact]
    public void TheSpreadPutsEveryLightBehindByItsPlaceAndBackRunsTheOtherWay()
    {
        Assert.Equal(0.1, EffectEngine.PhaseOf(0.6, 0.5, 1, backwards: false), 6);
        Assert.Equal(0.6, EffectEngine.PhaseOf(0.6, 0.5, 0, backwards: false), 6);
        Assert.Equal(-0.6, EffectEngine.PhaseOf(0.6, 0.5, 0, backwards: true), 6);
    }

    [Fact]
    public void TheIntensityShapesGoFromDarkToFull()
    {
        Effect effect = Chase() with { Duty = 25 };
        Assert.Equal(0, EffectEngine.Wave(effect, 0, 1), 6);
        Assert.Equal(1, EffectEngine.Wave(effect, 0.5, 1), 6);
        Assert.Equal(0.3, EffectEngine.Wave(effect with { Shape = "rampup" }, 2.3, 1), 6);
        Assert.Equal(0.7, EffectEngine.Wave(effect with { Shape = "rampdown" }, 0.3, 1), 6);
        Assert.Equal(1, EffectEngine.Wave(effect with { Shape = "pulse" }, 0.2, 1));
        Assert.Equal(0, EffectEngine.Wave(effect with { Shape = "pulse" }, 0.3, 1));

        Effect random = effect with { Shape = "random" };
        Assert.Equal(EffectEngine.Wave(random, 3.1, 4), EffectEngine.Wave(random, 3.9, 4));   // one level a cycle
        Assert.InRange(EffectEngine.Wave(random, 3.1, 4), 0, 1);

        Assert.Equal(0.8, EffectEngine.Intensity(effect, 0, 1, size: 20), 6);   // only the top 20 %
    }

    [Fact]
    public void TheMovementsDrawTheirShapesAroundTheMiddle()
    {
        var circle = new Effect(1, "Circle", Effect.MovementKind, "circle");
        Assert.Equal((0.0, -0.5), Rounded(EffectEngine.Movement(circle, 0, 1)));      // the top
        Assert.Equal((0.5, 0.0), Rounded(EffectEngine.Movement(circle, 0.25, 1)));    // the right: clockwise
        Assert.Equal(0.25, Rounded(EffectEngine.Movement(circle with { Shape = "eight" }, 0.125, 1)).Item2);
        Assert.Equal((0.5, 0.0), Rounded(EffectEngine.Movement(circle with { Shape = "pan" }, 0.25, 1)));
        Assert.Equal((0.0, 0.5), Rounded(EffectEngine.Movement(circle with { Shape = "tilt" }, 0.25, 1)));
        Assert.Equal((0.0, 0.5), Rounded(EffectEngine.Movement(circle with { Shape = "line", Angle = 90 }, 0.25, 1)));

        (double pan, double tilt) = EffectEngine.Movement(circle with { Shape = "random" }, 2.5, 3);
        Assert.InRange(pan, -0.5, 0.5);
        Assert.InRange(tilt, -0.5, 0.5);
        Assert.False(EffectEngine.UsesTilt(circle with { Shape = "pan" }));
        Assert.True(EffectEngine.UsesTilt(circle with { Shape = "pan", CenterPan = 50, CenterTilt = 50 }));
    }

    [Fact]
    public void TheColorShapesGoRoundTheCircleThroughTheListOrFade()
    {
        var rainbow = new Effect(1, "Rainbow", Effect.ColorKind, "rainbow");
        Assert.Equal("#ff0000", EffectEngine.ColorOf(rainbow, 0, 100).Hex);
        Assert.Equal("#00ff00", EffectEngine.ColorOf(rainbow, 1.0 / 3, 100).Hex);
        Assert.Equal("#ffffff", EffectEngine.ColorOf(rainbow, 0.5, 0).Hex);   // size 0: no color at all
        Assert.Equal("#ff8080", EffectEngine.ColorOf(rainbow, 0, 50).Hex);

        Effect chase = rainbow with { Shape = "chase", Colors = ["#ff0000", "#00ff00", "#0000ff"] };
        Assert.Equal("#00ff00", EffectEngine.ColorOf(chase, 0.4, 100).Hex);
        Assert.Equal("#0000ff", EffectEngine.ColorOf(chase, 0.99, 100).Hex);

        Effect fade = rainbow with { Shape = "fade", Colors = ["#ff0000", "#0000ff"] };
        Assert.Equal("#ff0000", EffectEngine.ColorOf(fade, 0, 100).Hex);
        Assert.Equal("#0000ff", EffectEngine.ColorOf(fade, 0.5, 100).Hex);
        Assert.Equal("#800080", EffectEngine.ColorOf(fade, 0.25, 100).Hex);   // half way, eased
        Assert.Equal("#ff0000", EffectEngine.ColorOf(rainbow with { Shape = "chase" }, 0.2, 100).Hex);   // red and blue unless it says
    }

    [Fact]
    public void TheCellsOfALightArePixelsAlongItsLongerSideTurnedWithIt()
    {
        var bar = new StageLight(1, "Bar", 1, "RGBRGBRGBRGB", null, 8, 3, 90) { Shape = StageLight.Square, Width = 4 };
        var plan = new StagePlan(16, 10, [bar]);
        EffectRig rig = EffectRig.Of(plan, new Effect(1, "Bars", Effect.PixelKind, "bars") { Lights = [1] });

        // Turned a quarter clockwise, the bar runs from the back to the front.
        Assert.Equal([(8.0, 1.5), (8.0, 2.5), (8.0, 3.5), (8.0, 4.5)], rig.Pixels.Select(pixel => (Math.Round(pixel.X, 6), Math.Round(pixel.Y, 6))));
        Assert.Equal(3, rig.Span, 6);
        Assert.Equal((8.0, 3.0), (Math.Round(rig.CenterX, 6), Math.Round(rig.CenterY, 6)));
    }

    [Fact]
    public void APixelPatternRunsOverThePlanByWhereThePixelsAre()
    {
        var bar = new StageLight(1, "Bar", 1, "RGBRGBRGBRGB", null, 8, 1, 0) { Shape = StageLight.Square, Width = 4 };
        var plan = new StagePlan(16, 10, [bar]);
        var bars = new Effect(1, "Bars", Effect.PixelKind, "bars") { Lights = [1], Colors = ["#ff0000", "#0000ff"], Size = 50, Duty = 50 };
        EffectRig rig = EffectRig.Of(plan, bars);

        string[] Colors(Effect effect, double cycles) =>
            [.. rig.Pixels.Select(pixel => EffectEngine.Pattern(effect, rig, pixel, cycles, effect.Size).Color.Hex)];

        // Bars half the bar's length long: red on blue, moving on by a quarter of a length each eighth of a cycle.
        Assert.Equal(["#ff0000", "#0000ff", "#ff0000", "#ff0000"], Colors(bars, 0));
        Assert.Equal(Colors(bars, 0), Colors(bars, 1));   // one cycle later the same

        // A ripple from the left end: the cell there is at the top of its ring at the start.
        Effect ripple = bars with { Shape = "ripple", Colors = ["#ffffff"], PointX = 6.5, PointY = 1, Size = 100 };
        Assert.Equal(1, EffectEngine.Pattern(ripple, rig, rig.Pixels[0], 0, 100).Brightness, 6);
        Assert.Equal(0, EffectEngine.Pattern(ripple, rig, rig.Pixels[0], 0.5, 100).Brightness, 6);

        // Sparkle: as many flash as the size says, each at its own moment of the cycle, fading fast.
        Effect sparkle = bars with { Shape = "sparkle", Colors = [], Size = 100 };
        double[] flashes = [.. Enumerable.Range(0, 64).Select(step => EffectEngine.Pattern(sparkle, rig, rig.Pixels[1], 3 + (step / 64.0), 100).Brightness)];
        Assert.Contains(flashes, bright => bright > 0.8);
        Assert.Contains(flashes, bright => bright == 0);
    }

    [Fact]
    public void TheRandomNumbersAreTheSameForTheSameSeedLightCycleAndUse()
    {
        double first = EffectEngine.Random01(5, 2, 10, 0);
        Assert.Equal(first, EffectEngine.Random01(5, 2, 10, 0));
        Assert.NotEqual(first, EffectEngine.Random01(5, 2, 11, 0));
        Assert.NotEqual(first, EffectEngine.Random01(6, 2, 10, 0));
        Assert.All(Enumerable.Range(0, 1000).Select(cycle => EffectEngine.Random01(1, 1, cycle, 0)), value => Assert.True(value is >= 0 and < 1));
        Assert.InRange(Enumerable.Range(0, 1000).Average(cycle => EffectEngine.Random01(1, 1, cycle, 0)), 0.45, 0.55);
    }

    private static (double, double) Rounded((double Pan, double Tilt) offset) => (Math.Round(offset.Pan, 6) + 0.0, Math.Round(offset.Tilt, 6) + 0.0);
}
