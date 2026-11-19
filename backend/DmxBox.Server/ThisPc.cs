using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DmxBox.Server;

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
        _addresses = new Addresses([], []);
    }

    /// <summary>Raised after the addresses of this PC changed. It may come on any thread.</summary>
    public event Action? Changed;

    /// <summary>
    /// The addresses at which other devices in the same network reach this PC: IPv4 addresses
    /// of network cards that are up, the ones that lead to a router first. Empty without a network.
    /// </summary>
    public virtual IReadOnlyList<IPAddress> LanAddresses => _addresses.Lan;

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

    // A server that listens on IPv6 and IPv4 at once sees an IPv4 client as "::ffff:192.168.1.5".
    private static IPAddress Plain(IPAddress address)
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
        var lan = new List<(int Rank, IPAddress Address)>();
        NetworkInterface[] cards;
        try
        {
            cards = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            return new Addresses([], []);   // no network information at all: only loopback is this PC
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
                    lan.Add((router ? 0 : 1, address));
            }
        }

        IPAddress[] ordered = lan.OrderBy(entry => entry.Rank).Select(entry => entry.Address).Distinct().ToArray();
        return new Addresses(own, ordered);
    }

    // 169.254.x.x: the address a card gives itself when nothing in its network hands one out.
    // Another device does not reach the PC there.
    private static bool IsLinkLocal(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254;
    }

    private sealed record Addresses(HashSet<IPAddress> Own, IReadOnlyList<IPAddress> Lan);
}
