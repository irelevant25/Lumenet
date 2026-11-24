using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DmxBox.Sim;

/// <summary>Small things the simulators share: how levels are written, and which address of this PC each takes.</summary>
internal static class Shown
{
    /// <summary>The first levels of a universe in a line: "  0 255 128 ...".</summary>
    /// <param name="levels">The levels, channel 1 first.</param>
    /// <param name="count">How many to write.</param>
    public static string Levels(ReadOnlySpan<byte> levels, int count)
    {
        var line = new StringBuilder();
        for (int index = 0; index < count; index++)
            line.Append(CultureInfo.InvariantCulture, $" {(index < levels.Length ? levels[index] : 0),3}");
        return line.ToString();
    }

    /// <summary>
    /// Claims an address of this PC for a simulated network device, for as long as the simulator
    /// runs: the one asked for, or else the first that no other simulator has, 127.0.0.2, 127.0.0.3
    /// and on. Such an address never leaves the PC, and Windows does not ask about its firewall for it.
    /// </summary>
    /// <param name="folder">The registry of the simulators.</param>
    /// <param name="kind">The kind of the device.</param>
    /// <param name="name">What the device is.</param>
    /// <param name="asked">The address asked for; null for the first free one.</param>
    /// <exception cref="AddressTakenException">The address asked for is another simulator's.</exception>
    public static (IPAddress Address, SimulatorClaim Claim) ClaimLoopbackAddress(string folder, string kind, string name, IPAddress? asked)
    {
        if (asked is not null)
            return (asked, SimulatorRegistry.Claim(folder, new SimulatedDevice(kind, asked.ToString(), name, Environment.ProcessId)));

        for (int last = 2; last < 255; last++)
        {
            var address = new IPAddress([127, 0, 0, (byte)last]);
            try
            {
                return (address, SimulatorRegistry.Claim(folder, new SimulatedDevice(kind, address.ToString(), name, Environment.ProcessId)));
            }
            catch (AddressTakenException)
            {
                // Another simulator's: the next one.
            }
        }

        throw new InvalidOperationException("Every address from 127.0.0.2 to 127.0.0.254 has a simulator.");
    }

    /// <summary>A UDP socket at an address and port of this PC, which another program may share.</summary>
    /// <param name="address">The address.</param>
    /// <param name="port">The port; 0 for any free one.</param>
    public static Socket SharedSocket(IPAddress address, int port)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { EnableBroadcast = true };
        try
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Bind(new IPEndPoint(address, port));
            socket.ReceiveTimeout = 200;
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
