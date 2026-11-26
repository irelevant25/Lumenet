using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace DmxBox.Server.Outputs;

/// <summary>
/// An output of the project that is no dmxbox: an Art-Net node, a stream of sACN (E1.31), or a DMX
/// USB interface of the kind of the Enttec DMX USB Pro. Like an output of a box, each of its ports
/// plays a universe of the project; two ports, of the same output or of two, may play the same
/// universe. The project lists these outputs itself, and nothing goes out on the network unless
/// it lists one.
/// </summary>
/// <param name="Kind"><see cref="ArtNet"/>, <see cref="Sacn"/> or <see cref="Usb"/>.</param>
/// <param name="Target">
/// Where the output goes. Art-Net: the node's IPv4 address, or <see cref="Broadcast"/> for every
/// node of a network (older gear). sACN: <see cref="Multicast"/>, the address of each universe, or
/// the IPv4 address of one receiver. USB: the interface's serial port, "COM3" on Windows and
/// "/dev/ttyUSB0" or "/dev/cu.usbserial-EN123456" on Linux and macOS.
/// </param>
/// <param name="Ports">
/// What the output plays: for each port the universe of the project, 1 to 8, or 0 for none, and
/// where it goes (<see cref="OutputPort.Address"/>). A USB interface has one port.
/// </param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed partial record OutputPlan(string Kind, string Target, OutputPort[] Ports)
{
    /// <summary>The <see cref="Kind"/> of an Art-Net node (Art-Net 4, ArtDmx on UDP port 6454).</summary>
    public const string ArtNet = "artnet";

    /// <summary>The <see cref="Kind"/> of sACN, ANSI E1.31, on UDP port 5568.</summary>
    public const string Sacn = "sacn";

    /// <summary>The <see cref="Kind"/> of a DMX USB interface that speaks the protocol of the Enttec DMX USB Pro.</summary>
    public const string Usb = "usb";

    /// <summary>The <see cref="Target"/> of an Art-Net output that goes to every node of a network.</summary>
    public const string Broadcast = "broadcast";

    /// <summary>The <see cref="Target"/> of an sACN output that goes to the multicast address of each universe.</summary>
    public const string Multicast = "multicast";

    /// <summary>The most outputs a project can list besides its boxes.</summary>
    public const int MaxOutputs = 64;

    /// <summary>The most ports an output can have.</summary>
    public const int MaxPorts = 32;

    /// <summary>The priority of an sACN output that does not say otherwise.</summary>
    public const int DefaultPriority = SacnSender.DefaultPriority;

    /// <summary>
    /// For a broadcast or multicast: the network of this PC it goes out on, as "192.168.1.0/24";
    /// an empty text for "automatic", the network that leads to the router. Empty for any other.
    /// </summary>
    public string Network { get; init; } = "";

    /// <summary>
    /// For sACN: 0 to 200, 100 unless the owner changed it. Of two sources of a universe, a
    /// receiver takes the one with the higher priority. 100 for any other kind.
    /// </summary>
    public int Priority { get; init; } = DefaultPriority;

    /// <summary>True for a broadcast or a multicast: those go out on a network of this PC, see <see cref="Network"/>.</summary>
    internal bool Shared => Target is Broadcast or Multicast;

    /// <summary>
    /// What tells two outputs apart: the kind, where it goes, and for a broadcast or a multicast
    /// the network. A project cannot list the same twice.
    /// </summary>
    internal string Identity => $"{Kind} {Target} {Network}";

    /// <summary>
    /// The output as a message names it, with a capital: "The Art-Net node at 192.168.1.50",
    /// "The Art-Net broadcast on 192.168.1.0/24", "sACN by multicast", "The USB interface at COM3".
    /// </summary>
    internal string Said
    {
        get
        {
            string on = Network.Length > 0 ? $" on {Network}" : "";
            return (Kind, Target) switch
            {
                (ArtNet, Broadcast) => $"The Art-Net broadcast{on}",
                (ArtNet, _) => $"The Art-Net node at {Target}",
                (Sacn, Multicast) => $"sACN by multicast{on}",
                (Sacn, _) => $"sACN to {Target}",
                _ => $"The USB interface at {Target}",
            };
        }
    }

    /// <summary>Checks a plan from a UI or a file, and writes it the one way.</summary>
    /// <param name="where">Which output it is, for the message: "Output 2".</param>
    /// <exception cref="DeskException">Something about it is out of range. The message says what.</exception>
    internal OutputPlan Checked(string where)
    {
        string kind = Kind is ArtNet or Sacn or Usb
            ? Kind
            : throw new DeskException($"{where}: \"{Kind}\" is no kind of output; it is artnet, sacn or usb.");
        string target = kind switch
        {
            ArtNet => AddressOr(Target, Broadcast)
                ?? throw new DeskException($"{where}: \"{Target}\" is no address of a node like 192.168.1.50, nor broadcast."),
            Sacn => AddressOr(Target, Multicast)
                ?? throw new DeskException($"{where}: \"{Target}\" is no address of a receiver like 192.168.1.50, nor multicast."),
            _ => SerialPortOf(Target)
                ?? throw new DeskException($"{where}: \"{Target}\" is no serial port like COM3 or /dev/ttyUSB0."),
        };

        OutputPort[] ports = Ports ?? [];
        int most = kind == Usb ? 1 : MaxPorts;
        if (ports.Length < 1 || ports.Length > most)
        {
            throw new DeskException(kind == Usb
                ? $"{where}: a USB interface has one port, not {ports.Length}."
                : $"{where} must have 1 to {MaxPorts} ports, not {ports.Length}.");
        }

        var kept = new OutputPort[ports.Length];
        for (int index = 0; index < ports.Length; index++)
        {
            OutputPort port = ports[index] ?? throw new DeskException($"{where}: port {index + 1} is missing.");
            if (port.Universe < 0 || port.Universe > Desk.MaxUniverses)
                throw new DeskException($"{where}: a port plays universe 1 to {Desk.MaxUniverses}, or none (0), not {port.Universe}.");

            int address = kind switch
            {
                ArtNet => CheckedRange(port.Address, 0, ArtNetSender.MaxUniverse, $"{where}: a Port-Address is 0 to {ArtNetSender.MaxUniverse} (0:0:0 to 127:15:15)"),
                Sacn => CheckedRange(port.Address, SacnSender.MinUniverse, SacnSender.MaxUniverse, $"{where}: an sACN universe is {SacnSender.MinUniverse} to {SacnSender.MaxUniverse}"),
                _ => 0,
            };
            kept[index] = new OutputPort(port.Universe, address);
            if (kind != Usb && kept[..index].Any(other => other.Address == address))
                throw new DeskException($"{where}: two ports go to {AddressText(kind, address)}.");
        }

        bool shared = target is Broadcast or Multicast;
        string network = shared
            ? NetworkOf(Network) ?? throw new DeskException($"{where}: \"{Network}\" is no network like 192.168.1.0/24.")
            : "";
        int priority = kind == Sacn
            ? CheckedRange(Priority, 0, SacnSender.MaxPriority, $"{where}: the priority is 0 to {SacnSender.MaxPriority}")
            : DefaultPriority;
        return new OutputPlan(kind, target, kept) { Network = network, Priority = priority };
    }

    /// <summary>Same as another plan in every field, the ports included.</summary>
    public bool SameAs(OutputPlan other)
    {
        return Kind == other.Kind && Target == other.Target && Network == other.Network && Priority == other.Priority
            && Ports.AsSpan().SequenceEqual(other.Ports);
    }

    /// <summary>Where a port goes, as people read it: the Port-Address "0:0:1" of Art-Net, "sACN 1", or nothing for USB.</summary>
    /// <param name="kind">The kind of the output.</param>
    /// <param name="address">The port's <see cref="OutputPort.Address"/>.</param>
    public static string AddressText(string kind, int address)
    {
        return kind switch
        {
            ArtNet => $"Port-Address {ArtNetSender.PortAddressText(address)}",
            Sacn => string.Create(CultureInfo.InvariantCulture, $"sACN universe {address}"),
            _ => "",
        };
    }

    /// <summary>
    /// A network in the one way of writing it, "192.168.1.0/24": the address with the bits of the
    /// hosts at zero, and the length of its prefix. An empty text stays empty (automatic). Null for
    /// text that is no such network.
    /// </summary>
    /// <param name="text">"192.168.1.20/24", "192.168.1.0/24", or empty.</param>
    public static string? NetworkOf(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        string[] parts = text.Trim().Split('/');
        if (parts.Length != 2 || IPv4Of(parts[0]) is not { } address
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int prefix) || prefix > 32)
        {
            return null;
        }

        return NetworkText(address, prefix);
    }

    /// <summary>The network an address is in, "192.168.1.0/24" for 192.168.1.20 with a prefix of 24.</summary>
    /// <param name="address">An IPv4 address.</param>
    /// <param name="prefix">The length of the network's prefix, 0 to 32.</param>
    public static string NetworkText(IPAddress address, int prefix)
    {
        uint mask = prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);
        byte[] bytes = address.GetAddressBytes();
        BinaryPrimitives.WriteUInt32BigEndian(bytes, BinaryPrimitives.ReadUInt32BigEndian(bytes) & mask);
        return string.Create(CultureInfo.InvariantCulture, $"{new IPAddress(bytes)}/{prefix}");
    }

    /// <summary>An IPv4 address written with its four numbers (IPAddress alone would take "3" or "10.1" as well); null for anything else.</summary>
    /// <param name="text">The text.</param>
    public static IPAddress? IPv4Of(string? text)
    {
        string trimmed = text?.Trim() ?? "";
        return trimmed.Split('.').Length == 4 && IPAddress.TryParse(trimmed, out IPAddress? ip) && ip.AddressFamily == AddressFamily.InterNetwork
            ? ip
            : null;
    }

    // The keyword (in any case), or an IPv4 address that a single device can have: not 0.0.0.0,
    // not a multicast address, not the broadcast of every network (that is what the keyword is
    // for). A directed broadcast such as 2.255.255.255 is taken, for older Art-Net gear.
    private static string? AddressOr(string? text, string keyword)
    {
        if (string.Equals(text?.Trim(), keyword, StringComparison.OrdinalIgnoreCase))
            return keyword;

        if (IPv4Of(text) is not { } ip)
            return null;

        byte[] bytes = ip.GetAddressBytes();
        bool usable = !ip.Equals(IPAddress.Any) && !ip.Equals(IPAddress.Broadcast) && bytes[0] is < 224 or > 239;
        return usable ? ip.ToString() : null;
    }

    // "COM3" (in capitals) on Windows, a device under /dev on Linux and macOS, "SIM1" for a simulated
    // interface (see PipeDmxPort); null for anything else.
    private static string? SerialPortOf(string? text)
    {
        string trimmed = text?.Trim() ?? "";
        if (WindowsPort().IsMatch(trimmed) || SimulatedPort().IsMatch(trimmed))
            return trimmed.ToUpperInvariant();

        return UnixPort().IsMatch(trimmed) ? trimmed : null;
    }

    private static int CheckedRange(int value, int min, int max, string message)
    {
        return value >= min && value <= max ? value : throw new DeskException($"{message}, not {value}.");
    }

    [GeneratedRegex("^COM[1-9][0-9]{0,2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WindowsPort();

    [GeneratedRegex("^/dev/[A-Za-z0-9._-]{1,60}$", RegexOptions.CultureInvariant)]
    private static partial Regex UnixPort();

    [GeneratedRegex("^SIM[1-9][0-9]{0,2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SimulatedPort();
}

/// <summary>A port of an output: the universe of the project it plays, and where it goes.</summary>
/// <param name="Universe">The universe of the project, 1 to 8, or 0 for none: the port sends nothing.</param>
/// <param name="Address">
/// Art-Net: the 15-bit Port-Address of the node's port, 0 to 32767 (Net, Sub-Net and Universe,
/// see <see cref="ArtNetSender.PortAddress"/>). sACN: the universe number, 1 to 63999. USB: 0.
/// </param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record OutputPort(int Universe, int Address);
