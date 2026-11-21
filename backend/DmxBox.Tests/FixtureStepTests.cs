using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// Steps of sequences and of the stack that stand for a fixture or a group of fixtures of the
/// stage plan: a level and a color, which the desk turns into the levels of the lights' channels.
/// </summary>
public sealed class FixtureStepTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;

    public FixtureStepTests()
    {
        _desk = new Desk(_time);
    }

    // A moving head of the library: pan, tilt, a shutter (closed below 8) and a dimmer.
    private static readonly FixtureType Head = new(1, "Maker", "Head", "4-channel", "maker/head",
    [
        new FixtureChannel("Pan", "P"),
        new FixtureChannel("Tilt", "T"),
        new FixtureChannel("Shutter", "S") { Ranges = [new FixtureRange(0, 7, "Closed"), new FixtureRange(8, 255, "Open")] },
        new FixtureChannel("Dimmer", "D"),
    ]);

    // An RGB par at channel 1 (light 5), an RGBW par at channel 4 (light 9), the head at channel 10
    // (light 12), and a group of the two pars.
    private static StagePlan Plan() => new StagePlan(16, 10,
    [
        new StageLight(5, "Par", 1, "RGB", null, 1, 1, 0),
        new StageLight(9, "Wash", 4, "RGBW", null, 3, 1, 0),
        new StageLight(12, "Head", 10, "PTSD", null, 5, 1, 0) { Type = 1 },
    ]) with
    {
        Types = [Head],
        Groups = [new LightGroup(3, "Pars", [5, 9])],
    };

    private static Step Lights(string target, int number, int value, string? color, double up = 0, double wait = 1, double down = 0) =>
        new(target, number, value, up, wait, down) { Color = color };

    private int[] Output(int from, int count) => [.. _desk.GetOutput().Skip(from - 1).Take(count).Select(level => (int)level)];

    private int[] Faders(int from, int count) => [.. _desk.GetState().Channels.Skip(from - 1).Take(count)];

    private void Pass(double seconds)
    {
        _time.Pass(seconds);
        _desk.Tick();
    }

    [Fact]
    public void ASequenceOfFixturesAndGroupsLightsThemInTheirColors()
    {
        _desk.SetStage(Plan());
        _desk.SetSequence(2, loop: true,
        [
            Lights("fixture", 5, 255, "#ff0000"),
            Lights("group", 3, 128, "#ffffff"),
        ]);

        _desk.SetMemoryLevel(2, 255);
        _time.Pass(0.5);
        Assert.Equal([255, 0, 0, 0, 0, 0, 0], Output(1, 7));

        _time.Pass(1);
        // both pars of the group at 128: the RGB one in red, green and blue, the RGBW one in its white
        Assert.Equal([128, 128, 128, 0, 0, 0, 128], Output(1, 7));
    }

    [Fact]
    public void AStepOfAMovingHeadGivesItsDimmerAndOpensItsShutterLeavingWhereItPoints()
    {
        _desk.SetStage(Plan());
        _desk.SetChannel(10, 40);   // pan
        _desk.SetSequence(2, loop: false, [Lights("fixture", 12, 200, null)]);

        _desk.SetMemoryLevel(2, 255);
        _time.Pass(0.5);

        Assert.Equal([40, 0, 8, 200], Output(10, 4));
    }

    [Fact]
    public void TheStackMovesTheFadersOfAFixtureAndSetsItsShutterAtOnce()
    {
        _desk.SetStage(Plan());
        _desk.AddStackStep(Lights("fixture", 5, 255, "#00ff00", up: 2, wait: 0, down: 2));
        _desk.AddStackStep(Lights("fixture", 12, 255, null, up: 2, wait: 0, down: 0));

        _desk.SetStackLive(true);
        Pass(1);
        Assert.Equal([0, 128, 0], Faders(1, 3));
        Pass(1);
        Assert.Equal([0, 255, 0], Faders(1, 3));
        Assert.Equal([0, 255, 0], Output(1, 3));
        Assert.Equal("#00ff00", _desk.GetStack().Steps[0].Color);

        _desk.StackNext();
        Pass(0.1);
        Assert.Equal(8, Faders(12, 1)[0]);   // the shutter at once
        Pass(0.9);
        Assert.Equal(128, Faders(13, 1)[0]); // the dimmer on its way up
        Assert.Equal(127, Faders(2, 1)[0]);  // the par on its way down
    }

    [Fact]
    public void TheProjectFileNamesFixturesAndGroupsByTheirPlaceAndKeepsTheColor()
    {
        _desk.SetStage(Plan());
        _desk.SetSequence(2, loop: true, [Lights("fixture", 9, 255, "#2060FF"), Lights("group", 3, 128, null), Lights("fixture", 44, 1, null)]);

        using var stream = new MemoryStream();
        ProjectFormat.Write(stream, _desk.GetProject().Content);
        string text = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("\"color\": \"#2060ff\"", text);

        using var again = new MemoryStream(stream.ToArray());
        ProjectContent read = ProjectFormat.Read(again);
        Step[] steps = read.Sequences[1]!.Steps;
        // the wash is the second light of the file, the group its first; a light that is gone is 0
        Assert.Equal([("fixture", 2), ("group", 1), ("fixture", 0)], steps.Select(step => (step.Target, step.Number)));
        Assert.Equal("Wash", read.Stage!.Lights.Single(light => light.Id == 2).Name);
        Assert.Equal("#2060ff", steps[0].Color);
    }

    [Fact]
    public void StepsOfFixturesAreCheckedAsEveryStep()
    {
        Assert.Equal("#ff2000", Lights("fixture", 1, 255, "#FF2000").Checked().Color);
        Assert.Null((Lights("channel", 1, 255, "#ff2000")).Checked().Color);   // a channel has no color of its own

        DeskException error = Assert.Throws<DeskException>(() => Lights("fixture", 1, 255, "red").Checked());
        Assert.Equal("\"red\" is no color like #ff2000.", error.Message);
        error = Assert.Throws<DeskException>(() => Lights("light", 1, 255, null).Checked());
        Assert.Equal("A step stands for a channel, a memory, a fixture or a group, not for \"light\".", error.Message);
        error = Assert.Throws<DeskException>(() => Lights("group", -1, 255, null).Checked());
        Assert.Equal("The group of a step must be one of the stage plan, not -1.", error.Message);
    }
}
