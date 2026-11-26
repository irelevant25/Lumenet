using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DmxBox.Tests.Outputs;

/// <summary>An ArtDmx frame as a pretend node got it.</summary>
internal sealed record ArtDmxFrame(int PortAddress, byte Sequence, byte[] Levels);

/// <summary>An sACN data packet as a pretend receiver got it.</summary>
internal sealed record SacnPacket(Guid Cid, string Source, int Priority, byte Sequence, bool Terminated, int Universe, byte[] Levels);

/// <summary>
/// Listens at a free UDP port of 127.0.0.1 on a thread of its own, and keeps what comes in. The
/// pretend Art-Net node and the pretend sACN receiver are made of it.
/// </summary>
internal abstract class UdpCatcher : IDisposable
{
    private readonly Socket _socket;
    private readonly Thread _thread;
    private volatile bool _stopping;

    protected UdpCatcher()
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { ReceiveTimeout = 200 };
        _socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        Port = ((IPEndPoint)_socket.LocalEndPoint!).Port;
        _thread = new Thread(Listen) { IsBackground = true, Name = GetType().Name };
        _thread.Start();
    }

    /// <summary>The port it listens at, on 127.0.0.1.</summary>
    public int Port { get; }

    /// <summary>Lock for what the catcher keeps.</summary>
    protected Lock Gate { get; } = new();

    public void Dispose()
    {
        _stopping = true;
        _thread.Join(TimeSpan.FromSeconds(2));
        _socket.Dispose();
    }

    /// <summary>Handles one datagram, on the catcher's thread.</summary>
    protected abstract void Take(byte[] datagram, IPEndPoint from);

    /// <summary>Sends a datagram back.</summary>
    protected void Answer(byte[] datagram, IPEndPoint to)
    {
        _socket.SendTo(datagram, to);
    }

    private void Listen()
    {
        byte[] buffer = new byte[2048];
        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
        while (!_stopping)
        {
            int length;
            try
            {
                length = _socket.ReceiveFrom(buffer, ref from);
            }
            catch (SocketException)
            {
                continue;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            Take(buffer[..length], (IPEndPoint)from);
        }
    }
}

/// <summary>
/// A pretend Art-Net node: it keeps the ArtDmx frames it gets, and answers every poll with an
/// ArtPollReply with its name and its ports, unless a test silences it.
/// </summary>
internal sealed class FakeArtNetNode : UdpCatcher
{
    private readonly List<ArtDmxFrame> _frames = [];
    private int _polls;

    /// <summary>The short name it answers with. A dmxbox calls itself "dmxbox".</summary>
    public string ShortName { get; set; } = "Pretend node";

    /// <summary>The Port-Addresses of its ports, all with the Net and Sub-Net of the first.</summary>
    public int[] Ports { get; set; } = [0, 1];

    /// <summary>False: polls get no answer, as from a node that is switched off.</summary>
    public bool Answers { get; set; } = true;

    public ArtDmxFrame[] Frames
    {
        get
        {
            lock (Gate)
            {
                return [.. _frames];
            }
        }
    }

    public int Polls
    {
        get
        {
            lock (Gate)
            {
                return _polls;
            }
        }
    }

    public void Forget()
    {
        lock (Gate)
        {
            _frames.Clear();
        }
    }

    protected override void Take(byte[] datagram, IPEndPoint from)
    {
        if (datagram.Length < 12 || Encoding.ASCII.GetString(datagram, 0, 8) != "Art-Net\0")
            return;

        ushort opCode = BinaryPrimitives.ReadUInt16LittleEndian(datagram.AsSpan(8));
        if (opCode == 0x5000)
        {
            int length = BinaryPrimitives.ReadUInt16BigEndian(datagram.AsSpan(16));
            var frame = new ArtDmxFrame(
                BinaryPrimitives.ReadUInt16LittleEndian(datagram.AsSpan(14)), datagram[12], datagram[18..(18 + length)]);
            lock (Gate)
            {
                _frames.Add(frame);
            }
        }
        else if (opCode == 0x2000)
        {
            lock (Gate)
            {
                _polls++;
            }

            if (Answers)
                Answer(Reply(), from);
        }
    }

    private byte[] Reply()
    {
        byte[] reply = new byte[239];
        Encoding.ASCII.GetBytes("Art-Net\0").CopyTo(reply, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(reply.AsSpan(8), 0x2100);
        IPAddress.Loopback.GetAddressBytes().CopyTo(reply, 10);
        BinaryPrimitives.WriteUInt16LittleEndian(reply.AsSpan(14), 6454);
        reply[16] = 1;
        reply[17] = 2;
        reply[18] = (byte)(Ports[0] >> 8);
        reply[19] = (byte)((Ports[0] >> 4) & 0x0F);
        Encoding.ASCII.GetBytes(ShortName).CopyTo(reply, 26);
        Encoding.ASCII.GetBytes($"{ShortName}, a node of the tests").CopyTo(reply, 44);
        reply[173] = (byte)Math.Min(Ports.Length, 4);
        for (int port = 0; port < Math.Min(Ports.Length, 4); port++)
        {
            reply[174 + port] = 0x80;
            reply[190 + port] = (byte)(Ports[port] & 0x0F);
        }

        return reply;
    }
}

/// <summary>A pretend sACN receiver: it keeps every data packet it gets.</summary>
internal sealed class FakeSacnReceiver : UdpCatcher
{
    private readonly List<SacnPacket> _packets = [];

    public SacnPacket[] Packets
    {
        get
        {
            lock (Gate)
            {
                return [.. _packets];
            }
        }
    }

    public void Forget()
    {
        lock (Gate)
        {
            _packets.Clear();
        }
    }

    protected override void Take(byte[] datagram, IPEndPoint from)
    {
        if (datagram.Length < 126 || Encoding.ASCII.GetString(datagram, 4, 9) != "ASC-E1.17")
            return;

        int count = BinaryPrimitives.ReadUInt16BigEndian(datagram.AsSpan(123)) - 1;
        var packet = new SacnPacket(
            new Guid(datagram.AsSpan(22, 16), bigEndian: true),
            Encoding.UTF8.GetString(datagram, 44, 64).TrimEnd('\0'),
            datagram[108],
            datagram[111],
            (datagram[112] & 0x40) != 0,
            BinaryPrimitives.ReadUInt16BigEndian(datagram.AsSpan(113)),
            datagram[126..(126 + count)]);
        lock (Gate)
        {
            _packets.Add(packet);
        }
    }
}
