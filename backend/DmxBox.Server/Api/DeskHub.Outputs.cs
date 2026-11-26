using System.Globalization;
using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Server.Api;

// The hub's part for the outputs: the universes, the boxes, and Art-Net, sACN and the DMX USB interfaces.
internal partial class DeskHub
{
    /// <summary>
    /// Changes how many universes the project has, 1 to 8 (see <see cref="Desk.SetUniverses"/>).
    /// Every UI gets the whole desk again: it has more channels, or fewer.
    /// </summary>
    /// <param name="universes">1 to 8.</param>
    public async Task SetUniverses(int universes)
    {
        using (_history.Change("the universes"))
            _desk.SetUniverses(universes);

        await TellDeskAsync();
    }

    /// <summary>The boxes as the Boxes window shows them, see <see cref="BoxNetwork.Boxes"/>.</summary>
    public BoxView[] GetBoxes()
    {
        return _boxes.Boxes;
    }

    /// <summary>
    /// Takes the project's list of boxes as the Boxes window changed it (see <see cref="Desk.SetBoxes"/>):
    /// a box added by hand, an output given another universe, a box taken out, or an empty list for
    /// automatic. The desk starts looking for a box that was added; every UI gets the boxes.
    /// </summary>
    /// <param name="boxes">The whole list as it is to be.</param>
    public async Task SetBoxes(BoxPlan[] boxes)
    {
        bool changed;
        using (_history.Change("the boxes"))
            changed = _desk.SetBoxes(boxes);

        if (changed)
            _boxes.Refresh();

        await TellProjectAsync();
    }

    /// <summary>The outputs besides the boxes as the Outputs window shows them, see <see cref="OutputNetwork.Outputs"/>.</summary>
    public OutputView[] GetOutputs()
    {
        return _outputs.Outputs;
    }

    /// <summary>
    /// Takes the project's outputs besides the boxes as the Outputs window changed them (see
    /// <see cref="Desk.SetOutputs"/>): an Art-Net node, sACN or a USB interface added, a port given
    /// another universe or address, an output taken out. An Art-Net output to the address of a
    /// dmxbox is refused: the desk plays the box with its own protocol already. Every UI gets the
    /// outputs.
    /// </summary>
    /// <param name="outputs">The whole list as it is to be.</param>
    public async Task SetOutputs(OutputPlan[] outputs)
    {
        foreach (OutputPlan output in outputs ?? [])
        {
            string target = output?.Target?.Trim() ?? "";
            if (output?.Kind == OutputPlan.ArtNet && _boxes.IsBoxAddress(target))
            {
                throw new DeskException(
                    $"{target} is a dmxbox, which the desk plays with its own protocol already. Give it its universes in the dmxbox section.");
            }
        }

        bool changed;
        using (_history.Change("the outputs"))
            changed = _desk.SetOutputs(outputs);

        if (changed)
            _outputs.Refresh();

        await TellProjectAsync();
    }

    /// <summary>
    /// Asks every Art-Net node on the networks of this PC to answer, and gives those that did, for
    /// the Outputs panel to add. It takes a second and a half, so it answers as a stream of one:
    /// SignalR takes one call of a page at a time, and a call this long would hold up that page's
    /// faders and keys meanwhile (and the keys of a MIDI controller it presses); a stream runs
    /// beside them.
    /// </summary>
    public async IAsyncEnumerable<FoundNode[]> FindArtNetNodes()
    {
        FoundNode[] found;
        try
        {
            found = await _outputs.FindArtNetNodesAsync();
        }
        catch (DeskException e)
        {
            // UserErrorFilter sees only the start of a stream: the refusal is said here as it says them.
            throw new HubException(e.Message);
        }

        yield return found;
    }

    /// <summary>The serial ports of the PC, for a DMX USB interface: "COM3", "/dev/ttyUSB0".</summary>
    public string[] GetSerialPorts()
    {
        return _outputs.SerialPorts();
    }

    /// <summary>The networks of the PC a broadcast or a multicast can go out on, for the Outputs window.</summary>
    public NetworkChoice[] GetNetworks()
    {
        return _outputs.Networks();
    }
}
