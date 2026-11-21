using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>The stage plan: what it can hold, the desk that keeps it, and the project file.</summary>
public sealed class StagePlanTests
{
    private readonly Desk _desk = new(new ManualTime());

    private static StageLight Light(int id, int address, string channels, double x = 1, double y = 1) =>
        new(id, $"Light {id}", address, channels, null, x, y, 0);

    [Fact]
    public void ACheckedPlanKeepsItsLightsTidy()
    {
        StagePlan plan = new StagePlan(20, 12, [new StageLight(1, "  LED bar ", 30, " rgbW ", "#2060FF", 8.123, -1.5, -90)]).Checked();

        StageLight light = Assert.Single(plan.Lights);
        Assert.Equal(new StageLight(1, "LED bar", 30, "RGBW", "#2060ff", 8.12, -1.5, 270), light);
        Assert.Null(new StagePlan(8, 8, [Light(1, 1, "D") with { Colour = "" }]).Checked().Lights[0].Colour);
    }

    [Theory]
    [InlineData(3, 10, "D", 1, 1, 1, "The stage can be 4 to 64 squares wide and deep, not 3 by 10.")]
    [InlineData(16, 65, "D", 1, 1, 1, "The stage can be 4 to 64 squares wide and deep, not 16 by 65.")]
    [InlineData(16, 10, "", 1, 1, 1, "Light 1 of the plan needs 1 to 64 letters for its channels, like RGB.")]
    [InlineData(16, 10, "R1B", 1, 1, 1, "Light 1 of the plan needs 1 to 64 letters for its channels, like RGB.")]
    [InlineData(16, 10, "D", 0, 1, 1, "Light 1 of the plan at address 0 with 1 channel does not fit into the channels 1 to 512.")]
    [InlineData(16, 10, "RGBW", 510, 1, 1, "Light 1 of the plan at address 510 with 4 channels does not fit into the channels 1 to 512.")]
    [InlineData(16, 10, "D", 1, -2.5, 1, "Light 1 of the plan is not on the plan.")]
    [InlineData(16, 10, "D", 1, 18.5, 1, "Light 1 of the plan is not on the plan.")]
    [InlineData(16, 10, "D", 1, 1, 14.5, "Light 1 of the plan is not on the plan.")]
    public void APlanThatCannotBeUsedIsRefusedWithAMessage(int width, int depth, string channels, int address, double x, double y, string message)
    {
        var error = Assert.Throws<DeskException>(() => new StagePlan(width, depth, [Light(1, address, channels, x, y)]).Checked());
        Assert.Equal(message, error.Message);
    }

    [Fact]
    public void ANameAColourAndTheNumbersOfTheLightsAreChecked()
    {
        StagePlan plan = new(16, 10, [Light(1, 1, "D")]);
        Assert.Equal(
            "Light 1 of the plan: a name can have at most 24 characters.",
            Assert.Throws<DeskException>(() => (plan with { Lights = [Light(1, 1, "D") with { Name = new string('x', 25) }] }).Checked()).Message);
        Assert.Equal(
            "Light 1 of the plan: a name cannot contain a tab, a line break or the like.",
            Assert.Throws<DeskException>(() => (plan with { Lights = [Light(1, 1, "D") with { Name = "a\tb" }] }).Checked()).Message);
        Assert.Equal(
            "Light 1 of the plan: \"red\" is no colour like #ff2000.",
            Assert.Throws<DeskException>(() => (plan with { Lights = [Light(1, 1, "D") with { Colour = "red" }] }).Checked()).Message);
        Assert.Equal(
            "Two lights of the plan have the same number, 1.",
            Assert.Throws<DeskException>(() => (plan with { Lights = [Light(1, 1, "D"), Light(1, 2, "D")] }).Checked()).Message);
        Assert.Equal(
            "Light 1 of the plan needs a number of 1 or more.",
            Assert.Throws<DeskException>(() => (plan with { Lights = [Light(0, 1, "D")] }).Checked()).Message);
    }

    [Fact]
    public void ALightIsRoundOrSquareAndOneTo32SquaresBig()
    {
        StagePlan plan = new(16, 10, [Light(1, 1, "RGB") with { Shape = "SQUARE", Width = 4, Depth = 1 }]);
        StageLight bar = plan.Checked().Lights[0];
        Assert.Equal(("square", 4, 1), (bar.Shape, bar.Width, bar.Depth));
        Assert.Equal(("round", 1, 1), (Light(1, 1, "D").Shape, Light(1, 1, "D").Width, Light(1, 1, "D").Depth));

        Assert.Equal(
            "Light 1 of the plan can be round or square, not \"triangle\".",
            Assert.Throws<DeskException>(() => (plan with { Lights = [Light(1, 1, "D") with { Shape = "triangle" }] }).Checked()).Message);
        Assert.Equal(
            "Light 1 of the plan can be 1 to 32 squares wide and deep, not 0 by 1.",
            Assert.Throws<DeskException>(() => (plan with { Lights = [Light(1, 1, "D") with { Width = 0 }] }).Checked()).Message);
        Assert.Equal(
            "Light 1 of the plan can be 1 to 32 squares wide and deep, not 1 by 33.",
            Assert.Throws<DeskException>(() => (plan with { Lights = [Light(1, 1, "D") with { Depth = 33 }] }).Checked()).Message);
    }

    [Fact]
    public void APictureIsAJpegPngOrWebpImageOfASensibleSize()
    {
        Assert.Null(StagePicture.Checked(null));
        Assert.Null(StagePicture.Checked(""));
        Assert.Equal("data:image/jpeg;base64,/9j/4AA=", StagePicture.Checked("data:image/jpeg;base64,/9j/4AA="));
        Assert.Equal(
            "The picture must be a JPEG, PNG or WebP image.",
            Assert.Throws<DeskException>(() => StagePicture.Checked("data:text/html;base64,PGI+")).Message);
        Assert.Equal(
            "The picture is damaged.",
            Assert.Throws<DeskException>(() => StagePicture.Checked("data:image/png;base64,not base64!")).Message);
        Assert.Throws<DeskException>(() => StagePicture.Checked("data:image/png;base64," + new string('A', StagePicture.MaxLength)));

        // the same picture has the same name, another one another name
        Assert.Equal(StagePicture.IdOf("data:image/png;base64,AAAA"), StagePicture.IdOf("data:image/png;base64,AAAA"));
        Assert.NotEqual(StagePicture.IdOf("data:image/png;base64,AAAA"), StagePicture.IdOf("data:image/png;base64,AAAB"));
    }

    [Fact]
    public void ThePictureIsSetApartFromThePlanAndAPlanFromAUiCannotChangeIt()
    {
        const string picture = "data:image/png;base64,iVBORw0KGgo=";
        Assert.True(_desk.SetStagePicture(picture));
        Assert.False(_desk.SetStagePicture(picture));
        Assert.Equal(StagePicture.IdOf(picture), _desk.Stage.PictureId);

        _desk.SetStage(new StagePlan(16, 10, []) { PictureId = null, PictureOpacity = 0.25, PictureOver = "PLAN" });
        Assert.Equal(StagePicture.IdOf(picture), _desk.Stage.PictureId);
        Assert.Equal((0.25, "plan"), (_desk.Stage.PictureOpacity, _desk.Stage.PictureOver));
        Assert.Equal(
            "The picture can show from 0.1 to 1, not 0.05.",
            Assert.Throws<DeskException>(() => _desk.SetStage(new StagePlan(16, 10, []) { PictureOpacity = 0.05 })).Message);

        Assert.True(_desk.SetStagePicture(null));
        Assert.Null(_desk.Stage.PictureId);
        Assert.Null(_desk.StagePicture);
    }

    [Fact]
    public void ThePictureAndTheShapesOfTheLightsGoIntoTheProjectFile()
    {
        const string picture = "data:image/jpeg;base64,/9j/4AA=";
        _desk.SetStage(new StagePlan(16, 10, [Light(1, 1, "RGB") with { Shape = "square", Width = 4 }]) { PictureOpacity = 0.4 });
        _desk.SetStagePicture(picture);

        ProjectContent back = Read(Write(_desk.GetProject().Content));
        Assert.Equal(picture, back.StagePicture);
        Assert.Equal(StagePicture.IdOf(picture), back.Stage!.PictureId);
        Assert.Equal((0.4, "stage"), (back.Stage.PictureOpacity, back.Stage.PictureOver));
        Assert.Equal(("square", 4, 1), (back.Stage.Lights[0].Shape, back.Stage.Lights[0].Width, back.Stage.Lights[0].Depth));

        // A plan without a picture writes nothing about one.
        _desk.SetStagePicture(null);
        string without = Write(_desk.GetProject().Content);
        Assert.DoesNotContain("picture", without);
        Assert.Null(Read(without).StagePicture);
    }

    [Fact]
    public void TheDeskKeepsThePlanAsAChangeOfTheProject()
    {
        Assert.Equal(StagePlan.Empty, _desk.Stage);
        long changes = _desk.ProjectChanges;

        var plan = new StagePlan(20, 12, [Light(1, 9, "D"), Light(2, 30, "RGBRGBRGBRGB")]);
        Assert.True(_desk.SetStage(plan));
        Assert.False(_desk.SetStage(plan with { Lights = [.. plan.Lights] })); // the same plan again: no change
        Assert.Equal(changes + 1, _desk.ProjectChanges);
        Assert.True(plan.SameAs(_desk.Stage));
    }

    [Fact]
    public void ThePlanIsLockedWhileTheStackIsLive()
    {
        _desk.AddStackStep(new Step("channel", 1, 255, 0, 0, 0));
        _desk.SetStackLive(true);

        var error = Assert.Throws<DeskException>(() => _desk.SetStage(new StagePlan(20, 12, [Light(1, 1, "D")])));
        Assert.StartsWith("The stack is live", error.Message);
        Assert.Equal(StagePlan.Empty, _desk.Stage);
    }

    [Fact]
    public void TheStageIsTheProjectsANewProjectAndOneWithoutAPlanHaveAnEmptyStage()
    {
        var rig = new StagePlan(20, 12, [Light(1, 9, "D")]);
        _desk.SetStage(rig);
        _desk.SetStagePicture("data:image/png;base64,iVBORw0KGgo=");
        ProjectContent withPlan = _desk.GetProject().Content;

        _desk.StartNewProject();
        Assert.Equal(StagePlan.Empty, _desk.Stage);
        Assert.Null(_desk.StagePicture);

        // A project with a plan brings it, with its picture; one from before plans has an empty stage.
        _desk.ReplaceProject(withPlan);
        Assert.True(rig.Checked().SameAs(_desk.Stage with { PictureId = null }));
        Assert.NotNull(_desk.StagePicture);
        _desk.ReplaceProject(withPlan with { Stage = null });
        Assert.Equal(StagePlan.Empty, _desk.Stage);
        Assert.Null(_desk.StagePicture);
    }

    [Fact]
    public void UndoBringsThePlanBack()
    {
        var history = new UndoHistory(_desk);
        using (history.Change("the stage plan", "stage"))
            _desk.SetStage(new StagePlan(20, 12, [Light(1, 9, "D")]));

        Assert.Equal("the stage plan", history.Undo());
        Assert.Equal(StagePlan.Empty, _desk.Stage);
        history.Redo();
        Assert.Single(_desk.Stage.Lights);
    }

    [Fact]
    public void ThePlanGoesIntoTheProjectFileAndComesBackWithItsLightsNumberedFromOne()
    {
        _desk.SetStage(new StagePlan(20, 12,
        [
            new StageLight(7, "LED bar", 30, "RGBRGBRGBRGB", null, 8, 1, 90),
            new StageLight(3, "Back blue 1", 6, "D", "#2060ff", 2, 1.5, 0),
        ]));

        string file = Write(_desk.GetProject().Content);
        Assert.Contains("\"channels\": \"RGBRGBRGBRGB\"", file);
        Assert.DoesNotContain("\"id\"", file);
        Assert.DoesNotContain("\"colour\": null", file);

        StagePlan back = Read(file).Stage!;
        Assert.Equal(20, back.Width);
        Assert.Equal(12, back.Depth);
        Assert.Equal(
            new[] { new StageLight(1, "LED bar", 30, "RGBRGBRGBRGB", null, 8, 1, 90), new StageLight(2, "Back blue 1", 6, "D", "#2060ff", 2, 1.5, 0) },
            back.Lights);
    }

    [Fact]
    public void AFileWithoutAPlanHasNoneAndOneWithADamagedPlanIsRefused()
    {
        const string start = """{ "format": "lumenet-project", "version": 4, "memories": [] """;
        Assert.Null(Read(start + "}").Stage);

        // The size may be left out; a light needs its address and its channels.
        StagePlan plan = Read(start + """, "stage": { "lights": [ { "address": 5, "channels": "d" } ] } }""").Stage!;
        Assert.Equal(new StagePlan(16, 10, [new StageLight(1, "", 5, "D", null, 0, 0, 0)]).Checked().Lights, plan.Lights);

        var missing = Assert.Throws<InvalidDataException>(() => Read(start + """, "stage": { "lights": [ { "channels": "D" } ] } }"""));
        Assert.Equal("Light 1 of the stage plan in the file needs an address and its channels.", missing.Message);
        var beyond = Assert.Throws<InvalidDataException>(() => Read(start + """, "stage": { "lights": [ { "address": 512, "channels": "RGB" } ] } }"""));
        Assert.Equal(
            "The stage plan in the file cannot be used. Light 1 of the plan at address 512 with 3 channels does not fit into the channels 1 to 512.",
            beyond.Message);
    }

    private static string Write(ProjectContent project)
    {
        using var stream = new MemoryStream();
        ProjectFormat.Write(stream, project);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static ProjectContent Read(string text)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
        return ProjectFormat.Read(stream);
    }
}
