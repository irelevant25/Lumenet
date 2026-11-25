using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace DmxBox.Server;

/// <summary>A Soundcraft Ui mixer found in the network: its address, and what it says it is.</summary>
/// <param name="Address">Its IP address, as the desk asks for it.</param>
/// <param name="Name">What the mixer is, as it says, like "ui16, firmware 1.0.7548".</param>
/// <remarks>Public only because the hub answers with it.</remarks>
public sealed record FoundMixer(string Address, string Name);

/// <summary>
/// Looks for Soundcraft Ui mixers in the networks of this PC, for the Find key of Settings. Every address
/// of each network of up to 254 addresses (of a bigger one, the 254 of the 24 bits around the PC's own
/// address), and 10.10.1.1, where a mixer's own Wi-Fi puts it. An address that takes a connection on
/// port 80 is asked as the link asks a mixer (a websocket with frames "3:::", see <see cref="UiMixer"/>):
/// one that says its model is a mixer. It takes about three seconds, and changes nothing anywhere.
/// </summary>
internal static class UiMixerFinder
{
    /// <summary>Where a mixer's own Wi-Fi puts it.</summary>
    public static readonly IPAddress OwnWifi = IPAddress.Parse("10.10.1.1");

    private static readonly TimeSpan KnockTime = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan AskTime = TimeSpan.FromSeconds(2.5);

    /// <summary>The mixers that answer, in the order of their addresses.</summary>
    /// <param name="sockets">Makes the websockets: real ones, or a pretend mixer in the tests.</param>
    /// <param name="cancel">Gives up.</param>
    /// <param name="candidates">The addresses to try; those of this PC's networks when none are given.</param>
    /// <param name="knock">Whether an address takes a connection on port 80; a real try when none is given.</param>
    public static async Task<FoundMixer[]> FindAsync(
        Func<IUiSocket> sockets,
        CancellationToken cancel,
        IEnumerable<IPAddress>? candidates = null,
        Func<IPAddress, CancellationToken, Task<bool>>? knock = null)
    {
        IPAddress[] addresses = [.. candidates ?? Candidates()];
        knock ??= KnockAsync;
        bool[] open = await Task.WhenAll(addresses.Select(address => knock(address, cancel))).ConfigureAwait(false);
        IPAddress[] web = [.. addresses.Where((_, index) => open[index])];
        FoundMixer?[] found = await Task.WhenAll(web.Select(address => AskAsync(sockets, address.ToString(), cancel))).ConfigureAwait(false);
        return [.. found.OfType<FoundMixer>().OrderBy(mixer => ToNumber(IPAddress.Parse(mixer.Address)))];
    }

    /// <summary>The addresses to try: of the networks of this PC (not its own), and <see cref="OwnWifi"/>.</summary>
    internal static IEnumerable<IPAddress> Candidates()
    {
        var own = new HashSet<IPAddress>();
        var tried = new HashSet<IPAddress> { OwnWifi };
        foreach (NetworkInterface face in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (face.OperationalStatus != OperationalStatus.Up || face.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            foreach (UnicastIPAddressInformation unicast in face.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                own.Add(unicast.Address);
                tried.UnionWith(Around(unicast.Address, unicast.PrefixLength));
            }
        }

        tried.ExceptWith(own);
        return tried;
    }

    /// <summary>The other addresses of a network: all of one of up to 254, the 254 of the 24 bits around the address in a bigger one.</summary>
    /// <param name="address">An address in it.</param>
    /// <param name="prefix">The bits of its network, 24 for 255.255.255.0.</param>
    internal static IEnumerable<IPAddress> Around(IPAddress address, int prefix)
    {
        int bits = Math.Clamp(prefix, 24, 32);
        uint mask = bits == 32 ? uint.MaxValue : ~(uint.MaxValue >> bits);
        uint network = ToNumber(address) & mask;
        uint count = 1u << (32 - bits);
        for (uint host = 1; host + 1 < count; host++)
            yield return FromNumber(network + host);
    }

    // Whether the address takes a connection on port 80, within a moment.
    private static async Task<bool> KnockAsync(IPAddress address, CancellationToken cancel)
    {
        using var client = new TcpClient(AddressFamily.InterNetwork);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(KnockTime);
        try
        {
            await client.ConnectAsync(address, 80, timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception e) when (e is SocketException or OperationCanceledException)
        {
            return false;
        }
    }

    // The address asked as a mixer: it tells its whole state when it is connected to, its model among it.
    private static async Task<FoundMixer?> AskAsync(Func<IUiSocket> sockets, string address, CancellationToken cancel)
    {
        IUiSocket socket = sockets();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(AskTime);
        try
        {
            await socket.ConnectAsync(new Uri($"ws://{address}"), timeout.Token).ConfigureAwait(false);
            string model = "";
            string firmware = "";
            while (await socket.ReceiveAsync(timeout.Token).ConfigureAwait(false) is { } frame)
            {
                if (!frame.StartsWith("3:::", StringComparison.Ordinal))
                    continue;

                foreach (string line in frame[4..].Split('\n'))
                {
                    string[] parts = line.TrimEnd('\r').Split('^');
                    if (parts.Length >= 3 && parts[0] is "SETS" or "SETD")
                    {
                        if (parts[1] == "model")
                            model = parts[2];
                        else if (parts[1] == "firmware")
                            firmware = parts[2];
                    }
                }

                if (model.Length > 0)
                    return new FoundMixer(address, firmware.Length > 0 ? $"{model}, firmware {firmware}" : model);
            }
        }
        catch (Exception e) when (e is WebSocketException or OperationCanceledException or IOException or InvalidOperationException or HttpRequestException)
        {
            // no mixer there
        }
        finally
        {
            await socket.DisposeAsync().ConfigureAwait(false);
        }

        return null;
    }

    private static uint ToNumber(IPAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    private static IPAddress FromNumber(uint number) =>
        new([(byte)(number >> 24), (byte)(number >> 16), (byte)(number >> 8), (byte)number]);
}
