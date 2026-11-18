using System.Net;
using System.Net.Sockets;

namespace DmxBox;

/// <summary>UDP details shared by the DX client and the Art-Net classes.</summary>
internal static class Udp
{
    /// <summary>
    /// Room for the largest possible UDP datagram. With a smaller buffer, a big datagram
    /// from somebody else would make the receive call fail instead of being ignored.
    /// </summary>
    public const int MaxDatagramLength = 65535;

    /// <summary>Creates an IPv4 UDP socket.</summary>
    /// <param name="allowBroadcast">True if the socket has to send to broadcast addresses.</param>
    public static Socket CreateSocket(bool allowBroadcast = false)
    {
        return new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            EnableBroadcast = allowBroadcast,
        };
    }

    /// <summary>Turns an IPv4 address or a host name into an endpoint.</summary>
    /// <exception cref="SocketException">The host name is unknown.</exception>
    public static IPEndPoint Resolve(string host, int port)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);

        if (!IPAddress.TryParse(host, out IPAddress? address))
        {
            // The box speaks IPv4 only, so IPv6 addresses of the host are of no use.
            address = Dns.GetHostAddresses(host, AddressFamily.InterNetwork).FirstOrDefault()
                ?? throw new SocketException((int)SocketError.HostNotFound);
        }

        return new IPEndPoint(address, port);
    }

    /// <summary>
    /// Waits up to <paramref name="timeout"/> for one datagram and stores it in <paramref name="buffer"/>.
    /// </summary>
    /// <param name="socket">The socket to listen on. It must have sent something already.</param>
    /// <param name="buffer">Receives the datagram.</param>
    /// <param name="timeout">How long to wait. Zero or less: do not wait at all.</param>
    /// <param name="length">Length of the datagram in <paramref name="buffer"/>.</param>
    /// <returns>False if nothing arrived in time.</returns>
    public static bool TryReceive(Socket socket, byte[] buffer, TimeSpan timeout, out int length)
    {
        length = 0;
        if (timeout <= TimeSpan.Zero || !socket.Poll(timeout, SelectMode.SelectRead))
            return false;

        try
        {
            length = socket.Receive(buffer);
        }
        catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionReset)
        {
            // Windows only: an earlier datagram of ours went to a port where nobody listens,
            // and Windows reports that as an error of the next receive call. It is not an
            // answer, so hand out an empty datagram: the callers skip it like any other
            // garbage and keep waiting until their time is up.
        }

        return true;
    }
}
