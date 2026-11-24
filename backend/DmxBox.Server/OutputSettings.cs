using System.Net;

namespace DmxBox.Server;

/// <summary>
/// Where the outputs besides the boxes send from and to, as the settings give it. The real desk
/// takes the defaults; a test desk beside it sends from 127.0.0.1 only, to ports of its own.
/// </summary>
internal sealed record OutputSettings
{
    /// <summary>
    /// The address of this PC the network outputs send from: any (0.0.0.0), or 127.0.0.1 for a test
    /// desk, which then sends nothing to the network (setting "Outputs:Bind").
    /// </summary>
    public IPAddress Bind { get; init; } = IPAddress.Any;

    /// <summary>The port the Art-Net nodes listen at, 6454 ("Outputs:ArtNetPort").</summary>
    public int ArtNetPort { get; init; } = ArtNetSender.DefaultPort;

    /// <summary>The port the desk hears the nodes' answers at, 6454; 0 for any free one ("Outputs:ArtNetReplyPort").</summary>
    public int ArtNetReplyPort { get; init; } = ArtNetSender.DefaultPort;

    /// <summary>The port the sACN receivers listen at, 5568 ("Outputs:SacnPort").</summary>
    public int SacnPort { get; init; } = SacnSender.DefaultPort;

    /// <summary>
    /// The file that keeps the desk's CID as an sACN source, see <see cref="SacnSource"/>: next to
    /// the LAN password. It is read, or made, when the first sACN packet goes out.
    /// </summary>
    public string CidFile { get; init; } = SacnSource.FileName;
}
