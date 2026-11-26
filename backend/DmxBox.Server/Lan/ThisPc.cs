using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DmxBox.Server.Lan;

/// <summary>
/// This PC on the network: the addresses that are its own, and the ones at which another
/// device in the same network (a phone, a tablet) reaches it. Kept up to date when the network
/// changes, for example when the router hands out another address.
/// </summary>
/// <remarks>
/// The tests make a PC of their own by overriding the members; see the protected constructor.
/// </remarks>
internal class ThisPc : IDisposable
{
    private readonly bool _watching;
    private volatile Addresses _addresses;

    /// <summary>Reads the addresses of this PC and follows their changes.</summary>
    public ThisPc()
    {
        _addresses = Read();
        NetworkChange.NetworkAddressChanged += OnAddressChanged;
        _watching = true;
    }

    /// <summary>For a pretend PC in the tests: no addresses are read and none are followed.</summary>
    /// <param name="pretend">Only tells this constructor from the other one.</param>
    protected ThisPc(bool pretend)
    {
        _ = pretend;
        _addresses = new Addresses([], [], []);
    }

    /// <summary>Raised after the addresses of this PC changed. It may come on any thread.</summary>
    public event Action? Changed;

    /// <summary>
    /// The addresses at which other devices in the same network reach this PC: IPv4 addresses
    /// of network cards that are up, the ones that lead to a router first. Empty without a network.
    /// </summary>
    public virtual IReadOnlyList<IPAddress> LanAddresses => _addresses.Lan;

    /// <summary>
    /// The networks this PC is in, in the order of <see cref="LanAddresses"/>: for each of those
    /// addresses, the network around it and the name of its adapter. A broadcast or a multicast
    /// of the outputs goes out on one of these.
    /// </summary>
    public virtual IReadOnlyList<PcNetwork> Networks => _addresses.Networks;

    /// <summary>
    /// True for an address of this PC itself: a loopback address, or the address of one of
    /// its network cards. A connection that comes from such an address comes from this PC.
    /// </summary>
    /// <param name="address">The address; an IPv4 address written as IPv6 counts as the IPv4 one.</param>
    public virtual bool Owns(IPAddress address)
    {
        IPAddress plain = Plain(address);
        return IPAddress.IsLoopback(plain) || _addresses.Own.Contains(plain);
    }

    /// <summary>
    /// True for a name of this PC: its computer name, alone or with ".local" (the name a phone
    /// finds it by without a DNS server). Upper and lower case do not matter.
    /// </summary>
    /// <param name="name">A host name, without a port.</param>
    public virtual bool HasName(string name)
    {
        string machine = Environment.MachineName;
        return string.Equals(name, machine, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, machine + ".local", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Stops following the changes of the network.</summary>
    public void Dispose()
    {
        if (_watching)
            NetworkChange.NetworkAddressChanged -= OnAddressChanged;
    }

    /// <summary>
    /// An address as the PC's own lists have it. A server that listens on IPv6 and IPv4 at once
    /// sees an IPv4 address as "::ffff:192.168.1.5"; this gives back "192.168.1.5".
    /// </summary>
    /// <param name="address">The address.</param>
    public static IPAddress Plain(IPAddress address)
    {
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }

    /// <summary>Raises <see cref="Changed"/>; for a pretend PC whose addresses change.</summary>
    protected void RaiseChanged()
    {
        Changed?.Invoke();
    }

    private void OnAddressChanged(object? sender, EventArgs e)
    {
        _addresses = Read();
        RaiseChanged();
    }

    private static Addresses Read()
    {
        var own = new HashSet<IPAddress>();
        var lan = new List<(int Rank, PcNetwork Network)>();
        NetworkInterface[] cards;
        try
        {
            cards = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            return new Addresses([], [], []);   // no network information at all: only loopback is this PC
        }

        foreach (NetworkInterface card in cards)
        {
            IPInterfaceProperties properties;
            try
            {
                properties = card.GetIPProperties();
            }
            catch (NetworkInformationException)
            {
                continue;
            }

            foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
                own.Add(Plain(unicast.Address));

            if (card.OperationalStatus != OperationalStatus.Up
                || card.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            // A card that leads to a router is the one in the home network. A cable straight to
            // another device (the dmxbox on its USB cable is one) comes after it.
            bool router = properties.GatewayAddresses.Any(gateway =>
                gateway.Address.AddressFamily == AddressFamily.InterNetwork && !gateway.Address.Equals(IPAddress.Any));
            foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
            {
                IPAddress address = unicast.Address;
                if (address.AddressFamily == AddressFamily.InterNetwork && !IsLinkLocal(address))
                    lan.Add((router ? 0 : 1, new PcNetwork(address, PrefixOf(unicast), card.Name, router)));
            }
        }

        PcNetwork[] networks = lan
            .OrderBy(entry => entry.Rank)
            .Select(entry => entry.Network)
            .DistinctBy(network => network.Address)
            .ToArray();
        return new Addresses(own, [.. networks.Select(network => network.Address)], networks);
    }

    // The length of the network's prefix, from the mask of the address: 24 for 255.255.255.0. A
    // system that does not say has 24, the size of nearly every home and stage network.
    private static int PrefixOf(UnicastIPAddressInformation unicast)
    {
        try
        {
            byte[]? mask = unicast.IPv4Mask?.GetAddressBytes();
            int prefix = mask?.Sum(part => System.Numerics.BitOperations.PopCount(part)) ?? 0;
            return prefix is > 0 and <= 32 ? prefix : 24;
        }
        catch (Exception e) when (e is PlatformNotSupportedException or NotImplementedException)
        {
            return 24;
        }
    }

    // 169.254.x.x: the address a card gives itself when nothing in its network hands one out.
    // Another device does not reach the PC there.
    private static bool IsLinkLocal(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254;
    }

    private sealed record Addresses(HashSet<IPAddress> Own, IReadOnlyList<IPAddress> Lan, IReadOnlyList<PcNetwork> Networks);
}

/// <summary>A network this PC is in.</summary>
/// <param name="Address">The PC's own IPv4 address in it.</param>
/// <param name="Prefix">The length of the network's prefix: 24 for a mask of 255.255.255.0.</param>
/// <param name="Adapter">The name of the adapter, as the system calls it: "Wi-Fi", "Ethernet".</param>
/// <param name="Router">True when the adapter leads to a router: the PC's home network.</param>
internal sealed record PcNetwork(IPAddress Address, int Prefix, string Adapter, bool Router)
{
    /// <summary>The network as a project keeps it, "192.168.1.0/24", see <see cref="OutputPlan.Network"/>.</summary>
    public string Network => OutputPlan.NetworkText(Address, Prefix);

    /// <summary>The address that reaches every device of the network: 192.168.1.255 for 192.168.1.0/24.</summary>
    public IPAddress Broadcast
    {
        get
        {
            byte[] bytes = Address.GetAddressBytes();
            uint hosts = Prefix >= 32 ? 0 : uint.MaxValue >> Prefix;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(
                bytes, System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes) | hosts);
            return new IPAddress(bytes);
        }
    }

    /// <summary>True when the address is in this network.</summary>
    /// <param name="address">An IPv4 address.</param>
    public bool Holds(IPAddress address)
    {
        return OutputPlan.NetworkText(address, Prefix) == Network;
    }
}
