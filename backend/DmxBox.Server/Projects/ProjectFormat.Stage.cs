using System.Text.Encodings.Web;
using System.Text.Json;

namespace DmxBox.Server.Projects;

// The stage plan in a project file, with the kinds of its fixtures, and a plan read from another file.
internal static partial class ProjectFormat
{
    /// <summary>
    /// Reads a stage plan on its own, as a project file has it under "stage": what Import hands over
    /// to put the stage of another project in place of this one's.
    /// </summary>
    /// <param name="text">The plan as JSON.</param>
    /// <returns>The plan, its lights numbered from 1 in the order of the file, and its picture; null for none.</returns>
    /// <exception cref="InvalidDataException">This is no stage plan the program can use. The message says why.</exception>
    public static (StagePlan Stage, string? Picture) ReadStage(string text)
    {
        ProjectFileStage? stage;
        try
        {
            stage = JsonSerializer.Deserialize<ProjectFileStage>(text, Options);
        }
        catch (JsonException e)
        {
            throw Bad($"The stage is damaged: {e.Message}");
        }

        (StagePlan? plan, string? picture) = StageOf(stage);
        return (plan ?? throw Bad("There is no stage in it."), picture);
    }

    // The stage plan and its picture, with the same rules as for a plan made on the UI; none for
    // a file without a plan. The lights, the kinds of lights and the groups are numbered from 1
    // in the order of the file, which is how they name each other there.
    private static (StagePlan? Stage, string? Picture) StageOf(ProjectFileStage? stage)
    {
        if (stage is null)
            return (null, null);

        List<ProjectFileLight?> listed = stage.Lights ?? [];
        var lights = new StageLight[listed.Count];
        for (int index = 0; index < listed.Count; index++)
        {
            ProjectFileLight light = listed[index] ?? throw Bad($"Light {index + 1} of the stage plan in the file is empty.");
            if (light.Address is not int address || light.Channels is null)
                throw Bad($"Light {index + 1} of the stage plan in the file needs an address and its channels.");

            lights[index] = new StageLight(
                index + 1, light.Name ?? "", address, light.Channels, light.Color ?? light.EarlierColor, light.X ?? 0, light.Y ?? 0, light.Angle ?? 0)
            {
                Shape = light.Shape ?? StageLight.Round,
                Width = light.Width ?? 1,
                Depth = light.Depth ?? 1,
                Type = light.Type,
                Limit = light.Limit ?? StageLight.NoLimit,
                Note = light.Note ?? "",
            };
        }

        try
        {
            string? picture = StagePicture.Checked(stage.Picture);
            StagePlan plan = new StagePlan(stage.Width ?? StagePlan.DefaultWidth, stage.Depth ?? StagePlan.DefaultDepth, lights)
            {
                PictureId = picture is null ? null : StagePicture.IdOf(picture),
                PictureOpacity = stage.PictureOpacity ?? StagePlan.DefaultPictureOpacity,
                PictureOver = stage.PictureOver ?? StagePlan.PictureOverStage,
                Types = TypesOf(stage.Types),
                Groups = [.. (stage.Groups ?? []).Select((group, index) =>
                    new LightGroup(index + 1, group?.Name ?? "", [.. group?.Lights ?? []]) { Note = group?.Note ?? "" })],
            };
            return (plan.Checked(), picture);
        }
        catch (DeskException e)
        {
            throw Bad($"The stage plan in the file cannot be used. {e.Message}");
        }
    }

    // The kinds of lights from the fixture library, numbered from 1 in the order of the file.
    private static FixtureType[] TypesOf(List<ProjectFileType?>? listed)
    {
        return [.. (listed ?? []).Select((type, index) => type is null
            ? throw new DeskException($"Kind {index + 1} of the lights is empty.")
            : new FixtureType(
                index + 1,
                type.Maker ?? "",
                type.Model ?? "",
                type.Mode ?? "",
                type.Source ?? "",
                [.. (type.Channels ?? []).Select((channel, number) => channel is null
                    ? throw new DeskException($"Kind {index + 1} of the lights: channel {number + 1} is empty.")
                    : new FixtureChannel(channel.Name ?? "", channel.Code ?? "")
                    {
                        Fine = channel.Fine,
                        Ranges = [.. (channel.Ranges ?? []).Select(range =>
                            new FixtureRange(range?.From ?? 0, range?.To ?? 0, range?.Label ?? "") { Color = range?.Color })],
                    })]))];
    }

    private static ProjectFileStage? FileStage(StagePlan? stage, string? picture)
    {
        if (stage is null)
            return null;

        // In the file the lights and the kinds are named by their place in its lists, from 1.
        Dictionary<int, int> typePlaces = stage.Types.Select((type, index) => (type.Id, Place: index + 1)).ToDictionary();
        Dictionary<int, int> lightPlaces = stage.Lights.Select((light, index) => (light.Id, Place: index + 1)).ToDictionary();

        return new ProjectFileStage
        {
            Width = stage.Width,
            Depth = stage.Depth,
            Lights = stage.Lights.Select(light => new ProjectFileLight
            {
                Name = light.Name,
                Note = light.Note == "" ? null : light.Note,
                Address = light.Address,
                Channels = light.Channels,
                Color = light.Color,
                X = light.X,
                Y = light.Y,
                Angle = light.Angle,
                Shape = light.Shape,
                Width = light.Width,
                Depth = light.Depth,
                Type = light.Type is int type && typePlaces.TryGetValue(type, out int place) ? place : null,
                Limit = light.Limit < StageLight.NoLimit ? light.Limit : null,
            }).ToList<ProjectFileLight?>(),
            // the picture and how it shows only for a plan that has one
            Picture = picture,
            PictureOpacity = picture is null ? null : stage.PictureOpacity,
            PictureOver = picture is null ? null : stage.PictureOver,
            Types = stage.Types.Length == 0 ? null : stage.Types.Select(type => new ProjectFileType
            {
                Maker = type.Maker,
                Model = type.Model,
                Mode = type.Mode,
                Source = type.Source,
                Channels = type.Channels.Select(channel => new ProjectFileTypeChannel
                {
                    Name = channel.Name,
                    Code = channel.Code,
                    Fine = channel.Fine,
                    Ranges = channel.Ranges.Length == 0
                        ? null
                        : channel.Ranges.Select(range => new ProjectFileRange
                        {
                            From = range.From,
                            To = range.To,
                            Label = range.Label,
                            Color = range.Color,
                        }).ToList<ProjectFileRange?>(),
                }).ToList<ProjectFileTypeChannel?>(),
            }).ToList<ProjectFileType?>(),
            Groups = stage.Groups.Length == 0 ? null : stage.Groups.Select(group => new ProjectFileGroup
            {
                Name = group.Name,
                Note = group.Note == "" ? null : group.Note,
                Lights = [.. group.Lights.Where(lightPlaces.ContainsKey).Select(id => lightPlaces[id])],
            }).ToList<ProjectFileGroup?>(),
        };
    }
}
