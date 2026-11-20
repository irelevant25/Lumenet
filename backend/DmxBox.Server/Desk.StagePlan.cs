namespace DmxBox.Server;

/// <summary>
/// The stage plan of the project: the lights of the rig and where they hang, and a picture under
/// them, see <see cref="StagePlan"/>.
/// </summary>
internal sealed partial class Desk
{
    // The stage plan of the project. A new project keeps it: the rig does not change with the show.
    private StagePlan _stage = StagePlan.Empty;

    // The picture under the plan, as a data URL; null without one. The plan names it by its PictureId.
    private string? _stagePicture;

    /// <summary>The stage plan of the project.</summary>
    public StagePlan Stage
    {
        get
        {
            lock (_lock)
            {
                return _stage;
            }
        }
    }

    /// <summary>The picture under the plan, as a data URL; null without one.</summary>
    public string? StagePicture
    {
        get
        {
            lock (_lock)
            {
                return _stagePicture;
            }
        }
    }

    /// <summary>
    /// Takes the stage plan as the owner changed it. It belongs to the project, which then has
    /// something to save; like everything that is stored, it cannot change while the stack is live.
    /// The picture stays as it is: only <see cref="SetStagePicture"/> changes it.
    /// </summary>
    /// <param name="plan">The whole plan as it is to be.</param>
    /// <returns>True if it differs from the plan the desk had.</returns>
    /// <exception cref="DeskException">Something in the plan cannot be used, or the stack is live.</exception>
    public bool SetStage(StagePlan? plan)
    {
        StagePlan kept = (plan ?? throw new DeskException("The plan is missing.")).Checked();

        lock (_lock)
        {
            RequireUnlocked();
            kept = kept with { PictureId = _stage.PictureId };
            if (kept.SameAs(_stage))
                return false;

            _stage = kept;
            _projectChanges++;
            return true;
        }
    }

    /// <summary>
    /// Puts a picture under the plan, a photo or a drawing of the stage, or takes it away. It
    /// belongs to the project, like the plan.
    /// </summary>
    /// <param name="picture">The picture as a data URL of a JPEG, PNG or WebP image; null or empty for none.</param>
    /// <returns>True if it differs from the picture the desk had.</returns>
    /// <exception cref="DeskException">It is no such picture, it is too big, or the stack is live.</exception>
    public bool SetStagePicture(string? picture)
    {
        string? kept = DmxBox.Server.StagePicture.Checked(picture);

        lock (_lock)
        {
            RequireUnlocked();
            if (kept == _stagePicture)
                return false;

            _stagePicture = kept;
            _stage = _stage with { PictureId = kept is null ? null : DmxBox.Server.StagePicture.IdOf(kept) };
            _projectChanges++;
            return true;
        }
    }
}
