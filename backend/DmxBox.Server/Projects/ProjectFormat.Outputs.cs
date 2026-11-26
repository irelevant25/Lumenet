using System.Text.Encodings.Web;
using System.Text.Json;

namespace DmxBox.Server.Projects;

// The universes of a project file, its boxes and its outputs besides the boxes.
internal static partial class ProjectFormat
{
    // The universes of the project: 1 for a file before version 5.
    private static int UniversesOf(ProjectFile file)
    {
        int universes = file.Universes ?? 1;
        if (universes < 1 || universes > Desk.MaxUniverses)
            throw Bad($"The file says {universes} universes; a project has 1 to {Desk.MaxUniverses}.");

        return universes;
    }

    // The boxes the project lists; none when the file lists none.
    private static BoxPlan[] BoxesOf(List<ProjectFileBox?>? listed)
    {
        if (listed is null)
            return [];
        if (listed.Count > BoxPlan.MaxBoxes)
            throw Bad($"The file lists {listed.Count} boxes; a project can have {BoxPlan.MaxBoxes}.");

        var boxes = new List<BoxPlan>();
        for (int index = 0; index < listed.Count; index++)
        {
            ProjectFileBox box = listed[index] ?? throw Bad("The list of boxes in the file has an empty entry.");
            try
            {
                BoxPlan plan = new BoxPlan(box.Address ?? "", [.. box.Universes ?? []]).Checked($"Box {index + 1} of the file");
                if (boxes.Any(other => other.Address == plan.Address))
                    throw Bad($"The file lists the box at {plan.Address} twice.");
                boxes.Add(plan);
            }
            catch (DeskException e)
            {
                throw Bad(e.Message);
            }
        }

        return [.. boxes];
    }

    // An output besides the boxes as the file has it: the network and the priority only where
    // they say something (a broadcast or multicast on a chosen network, sACN at another priority
    // than 100), the address of a port only for Art-Net and sACN.
    private static ProjectFileOutput FileOutput(OutputPlan output)
    {
        return new ProjectFileOutput
        {
            Kind = output.Kind,
            Target = output.Target,
            Network = output.Network.Length > 0 ? output.Network : null,
            Priority = output.Priority != OutputPlan.DefaultPriority ? output.Priority : null,
            Ports = output.Ports
                .Select(port => new ProjectFilePort { Universe = port.Universe, Address = output.Kind == OutputPlan.Usb ? null : port.Address })
                .ToList<ProjectFilePort?>(),
        };
    }

    // The outputs besides the boxes; none for a file without them, as every file before them is.
    private static OutputPlan[] OutputsOf(List<ProjectFileOutput?>? listed)
    {
        if (listed is null)
            return [];

        var plans = new OutputPlan?[listed.Count];
        for (int index = 0; index < listed.Count; index++)
        {
            ProjectFileOutput output = listed[index] ?? throw Bad("The list of outputs in the file has an empty entry.");
            OutputPort[] ports = (output.Ports ?? throw Bad($"Output {index + 1} of the file has no ports."))
                .Select(port => port is null
                    ? throw Bad($"The ports of output {index + 1} in the file have an empty entry.")
                    : new OutputPort(port.Universe ?? 0, port.Address ?? 0))
                .ToArray();
            plans[index] = new OutputPlan(output.Kind ?? "", output.Target ?? "", ports)
            {
                Network = output.Network ?? "",
                Priority = output.Priority ?? OutputPlan.DefaultPriority,
            };
        }

        try
        {
            return Desk.CheckedOutputs(plans, number => $"Output {number} of the file");
        }
        catch (DeskException e)
        {
            throw Bad(e.Message);
        }
    }
}
