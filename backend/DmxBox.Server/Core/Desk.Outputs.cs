namespace DmxBox.Server.Core;

/// <summary>
/// The project's outputs that are no dmxboxes: Art-Net nodes, sACN and DMX USB interfaces (see
/// <see cref="OutputPlan"/>). They belong to the project like its boxes, so that a project brings
/// its wiring along. The desk only keeps the list; <see cref="OutputNetwork"/> plays it.
/// </summary>
internal sealed partial class Desk
{
    private OutputPlan[] _outputs = [];

    /// <summary>The outputs the project lists besides its boxes; empty for none.</summary>
    public OutputPlan[] Outputs
    {
        get
        {
            lock (_lock)
            {
                return _outputs;
            }
        }
    }

    /// <summary>
    /// Puts a new list of outputs in place of the project's, as the Outputs window changed it: an
    /// output added, a port given another universe or address, an output taken out.
    /// </summary>
    /// <param name="outputs">The whole list as it is to be.</param>
    /// <returns>True if it differs from the list the desk had.</returns>
    /// <exception cref="DeskException">Something in the list cannot be used, or the stack is live.</exception>
    public bool SetOutputs(OutputPlan[]? outputs)
    {
        OutputPlan[] kept = CheckedOutputs(
            outputs ?? throw new DeskException("The list of outputs is missing."), number => $"Output {number}");

        lock (_lock)
        {
            RequireUnlocked();
            if (kept.Length == _outputs.Length && kept.Zip(_outputs).All(pair => pair.First.SameAs(pair.Second)))
                return false;

            _outputs = kept;
            _projectChanges++;
            _outputChanges++;
            return true;
        }
    }

    /// <summary>
    /// Checks a list of outputs from a UI or a file: each one, how many, and that none is listed
    /// twice. Gives the list written the one way.
    /// </summary>
    /// <param name="outputs">The list.</param>
    /// <param name="name">How the entry with a number, 1 for the first, is called in a message: "Output 2".</param>
    /// <exception cref="DeskException">Something in the list cannot be used. The message says what.</exception>
    internal static OutputPlan[] CheckedOutputs(OutputPlan?[] outputs, Func<int, string> name)
    {
        if (outputs.Length > OutputPlan.MaxOutputs)
            throw new DeskException($"A project can have {OutputPlan.MaxOutputs} outputs besides its boxes, not {outputs.Length}.");

        var kept = new OutputPlan[outputs.Length];
        for (int index = 0; index < outputs.Length; index++)
        {
            OutputPlan output = outputs[index] ?? throw new DeskException($"{name(index + 1)} is missing.");
            kept[index] = output.Checked(name(index + 1));
            if (kept[..index].FirstOrDefault(other => other.Identity == kept[index].Identity) is not null)
                throw new DeskException($"{kept[index].Said} is in the list twice.");
        }

        return kept;
    }
}
