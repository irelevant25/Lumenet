namespace DmxBox.Server.Core;

/// <summary>
/// The boxes the project lists: which box plays which universe on which output (see
/// <see cref="BoxPlan"/>). They belong to the project like its looks, so that a project brings its
/// wiring along; an empty list leaves every box that answers to play universes by itself
/// (<see cref="BoxWiring"/>). The desk only keeps the list; the box links play it.
/// </summary>
internal sealed partial class Desk
{
    private BoxPlan[] _boxes = [];

    /// <summary>The boxes the project lists; empty when it lists none.</summary>
    public BoxPlan[] Boxes
    {
        get
        {
            lock (_lock)
            {
                return _boxes;
            }
        }
    }

    /// <summary>
    /// Puts a new list of boxes in place of the project's, as the Boxes window changed it: a box
    /// added by hand, an output given another universe, a box taken out, or none for automatic.
    /// </summary>
    /// <param name="boxes">The whole list as it is to be.</param>
    /// <returns>True if it differs from the list the desk had.</returns>
    /// <exception cref="DeskException">Something in the list cannot be used, or the stack is live.</exception>
    public bool SetBoxes(BoxPlan[]? boxes)
    {
        BoxPlan[] list = boxes ?? throw new DeskException("The list of boxes is missing.");
        if (list.Length > BoxPlan.MaxBoxes)
            throw new DeskException($"A project can have {BoxPlan.MaxBoxes} boxes, not {list.Length}.");

        var kept = new BoxPlan[list.Length];
        for (int index = 0; index < list.Length; index++)
        {
            BoxPlan box = list[index] ?? throw new DeskException($"Box {index + 1} is missing.");
            kept[index] = box.Checked($"Box {index + 1}");
            if (kept[..index].Any(other => other.Address == kept[index].Address))
                throw new DeskException($"The box at {kept[index].Address} is in the list twice.");
        }

        lock (_lock)
        {
            RequireUnlocked();
            if (kept.Length == _boxes.Length && kept.Zip(_boxes).All(pair => pair.First.SameAs(pair.Second)))
                return false;

            _boxes = kept;
            _projectChanges++;
            _outputChanges++;
            return true;
        }
    }
}
