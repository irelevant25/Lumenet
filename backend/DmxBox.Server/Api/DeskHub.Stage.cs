using System.Globalization;
using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Server.Api;

// The hub's part for the stage plan, its picture, and a plan taken from another project.
internal partial class DeskHub
{
    /// <summary>The stage plan of the project: the size of the stage and the lights of the rig on it.</summary>
    public StagePlan GetStage()
    {
        return _desk.Stage;
    }

    /// <summary>
    /// Takes the stage plan as the owner edited it, whole: a light added, moved, changed or taken
    /// away, or the stage made bigger. Every UI gets the plan as the desk keeps it. Changes of the
    /// plan in quick succession are one step of the history.
    /// </summary>
    /// <param name="plan">The plan as it is to be.</param>
    public async Task SetStage(StagePlan plan)
    {
        bool changed;
        using (_history.Change("the stage plan", "stage"))
            changed = _desk.SetStage(plan);

        if (changed)
            await Clients.All.StageChanged(_desk.Stage);

        await TellProjectAsync();
    }

    /// <summary>
    /// The picture under the stage plan, as a data URL; null without one. A UI asks for it when
    /// the plan names another picture (<see cref="StagePlan.PictureId"/>).
    /// </summary>
    public string? GetStagePicture()
    {
        return _desk.StagePicture;
    }

    /// <summary>
    /// Puts a picture under the stage plan, or takes it away. Every UI gets the plan, which names
    /// the new picture, and asks for it.
    /// </summary>
    /// <param name="picture">A data URL of a JPEG, PNG or WebP image; null for none.</param>
    public async Task SetStagePicture(string? picture)
    {
        bool changed;
        using (_history.Change(picture is null ? "taking the picture of the stage away" : "the picture of the stage"))
            changed = _desk.SetStagePicture(picture);

        if (changed)
            await Clients.All.StageChanged(_desk.Stage);

        await TellProjectAsync();
    }

    /// <summary>
    /// Puts a stage that the user chose on their device (Import) in place of the project's: its
    /// lights, kinds of lights, groups and picture, see <see cref="Desk.ReplaceStage"/>. One step
    /// of the history; every UI gets the plan.
    /// </summary>
    /// <param name="text">The stage as a project file has it under "stage".</param>
    public async Task ImportStage(string text)
    {
        StagePlan plan;
        string? picture;
        try
        {
            (plan, picture) = ProjectFormat.ReadStage(text);
        }
        catch (InvalidDataException e)
        {
            throw new DeskException($"The stage cannot be imported. {e.Message}");
        }

        bool changed;
        using (_history.Change("importing a stage"))
            changed = _desk.ReplaceStage(plan, picture);

        if (changed)
            await Clients.All.StageChanged(_desk.Stage);

        await TellProjectAsync();
    }
}
