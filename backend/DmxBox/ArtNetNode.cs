namespace DmxBox;

/// <summary>An Art-Net node that answered <see cref="ArtNetDiscovery.Discover"/>.</summary>
/// <param name="Ip">The IPv4 address the node reports, for example "192.168.7.1".</param>
/// <param name="ShortName">Short name, at most 17 characters. The box calls itself "dmxbox".</param>
/// <param name="LongName">Long name, at most 63 characters.</param>
/// <param name="Universe">The universe (15-bit Port-Address) of the node's first output.</param>
/// <param name="Firmware">Firmware version, for example "1.0".</param>
/// <param name="Mac">MAC address, for example "02:00:00:00:00:01".</param>
public sealed record ArtNetNode(
    string Ip,
    string ShortName,
    string LongName,
    int Universe,
    string Firmware,
    string Mac)
{
    /// <summary>
    /// The universe (15-bit Port-Address) of every port that puts out DMX, in the node's order. A
    /// node with more than four ports answers a poll more than once; <see cref="ArtNetDiscovery.Discover"/>
    /// puts the ports of its answers together. Empty for a node that has no output.
    /// </summary>
    public int[] Ports { get; init; } = [];

    /// <summary>Same node, same names and the same ports, which a record's own equality would not compare one by one.</summary>
    public bool SameAs(ArtNetNode other)
    {
        return Ip == other.Ip && ShortName == other.ShortName && LongName == other.LongName && Universe == other.Universe
            && Firmware == other.Firmware && Mac == other.Mac && Ports.AsSpan().SequenceEqual(other.Ports);
    }
}
