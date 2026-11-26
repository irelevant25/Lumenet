using System.Net;

namespace DmxBox.Tests.Support;

/// <summary>
/// A PC with addresses of the test's choosing, so that a test does not depend on the network
/// of the PC it runs on. Loopback counts as this PC unless <see cref="LoopbackIsAnotherDevice"/>
/// says otherwise: then every connection of the test looks like one from another device.
/// </summary>
internal sealed class PretendPc : ThisPc
{
    /// <summary>The address this pretend PC has in its pretend home network.</summary>
    public static readonly IPAddress HomeAddress = IPAddress.Parse("10.20.30.40");

    private volatile IPAddress[] _lan = [HomeAddress];

    public PretendPc()
        : base(pretend: true)
    {
    }

    /// <summary>
    /// True: a connection from 127.0.0.1 counts as one from another device in the network. The
    /// tests switch this to try what a phone gets, over a connection of this PC.
    /// </summary>
    public bool LoopbackIsAnotherDevice { get; set; }

    /// <inheritdoc />
    public override IReadOnlyList<IPAddress> LanAddresses => _lan;

    /// <inheritdoc />
    /// <remarks>Every pretend network has 256 addresses; the first one leads to a router.</remarks>
    public override IReadOnlyList<PcNetwork> Networks =>
        [.. _lan.Select((address, index) => new PcNetwork(address, 24, $"Pretend {index + 1}", Router: index == 0))];

    /// <inheritdoc />
    public override bool Owns(IPAddress address)
    {
        IPAddress plain = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        if (IPAddress.IsLoopback(plain))
            return !LoopbackIsAnotherDevice;

        return Array.IndexOf(_lan, plain) >= 0;
    }

    /// <inheritdoc />
    public override bool HasName(string name)
    {
        return string.Equals(name, "pretend-pc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "pretend-pc.local", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The router hands out another address.</summary>
    /// <param name="address">The new one.</param>
    public void MoveTo(IPAddress address)
    {
        _lan = [address];
        RaiseChanged();
    }

    /// <summary>The PC is in more than one network at once, for example by cable and by Wi-Fi.</summary>
    /// <param name="addresses">Its addresses, the best one first.</param>
    public void Have(params IPAddress[] addresses)
    {
        _lan = addresses;
        RaiseChanged();
    }
}
