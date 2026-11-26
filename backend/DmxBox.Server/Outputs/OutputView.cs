namespace DmxBox.Server.Outputs;

/// <summary>An output besides the boxes, as the Outputs window and the top bar show it.</summary>
/// <param name="Plan">The output as the project lists it.</param>
/// <param name="State">
/// <see cref="Connected"/>: it answers. <see cref="Missing"/>: it does not answer, or cannot be
/// reached. <see cref="Sending"/>: it goes out, and gets no answers to tell more (sACN, an Art-Net
/// broadcast). <see cref="Conflict"/>: it would reach a dmxbox, which the desk plays with its own
/// protocol, so nothing goes out.
/// </param>
/// <param name="Detail">What the state means here, for people: the node's name, or why it is missing.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record OutputView(OutputPlan Plan, string State, string Detail)
{
    /// <summary>The output answers: an Art-Net node to the polls, a USB interface on its port.</summary>
    public const string Connected = "connected";

    /// <summary>The output does not answer, or cannot be reached.</summary>
    public const string Missing = "missing";

    /// <summary>The output goes out, and has no way to answer: sACN, an Art-Net broadcast.</summary>
    public const string Sending = "sending";

    /// <summary>The output would reach a dmxbox; nothing goes out.</summary>
    public const string Conflict = "conflict";

    /// <summary>The same as another view in every field.</summary>
    public bool SameAs(OutputView other)
    {
        return Plan.SameAs(other.Plan) && State == other.State && Detail == other.Detail;
    }
}

/// <summary>An Art-Net node that answered the search of the Outputs window.</summary>
/// <param name="Ip">Its IPv4 address.</param>
/// <param name="ShortName">The short name it gives itself.</param>
/// <param name="LongName">The long name it gives itself.</param>
/// <param name="Firmware">Its firmware version, "1.2".</param>
/// <param name="Ports">The Port-Address of each of its ports that puts out DMX.</param>
/// <param name="Dmxbox">True for a dmxbox: the desk plays it with its own protocol, so it cannot be added here.</param>
/// <remarks>Public for SignalR, which writes it as JSON.</remarks>
public sealed record FoundNode(string Ip, string ShortName, string LongName, string Firmware, int[] Ports, bool Dmxbox);

/// <summary>A network of this PC that a broadcast or a multicast can go out on.</summary>
/// <param name="Network">The network as a project keeps it, "192.168.1.0/24".</param>
/// <param name="Address">This PC's address in it, "192.168.1.20".</param>
/// <param name="Adapter">The name of the adapter: "Wi-Fi", "Ethernet".</param>
/// <param name="Router">True for the network that leads to a router: the automatic choice.</param>
/// <remarks>Public for SignalR, which writes it as JSON.</remarks>
public sealed record NetworkChoice(string Network, string Address, string Adapter, bool Router);
