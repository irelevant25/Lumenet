using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DmxBox.Tests.Outputs;

/// <summary>
/// The packets of the outputs besides the boxes, byte by byte, as the specifications have them:
/// sACN (ANSI E1.31), Art-Net 4 (ArtDmx, ArtPoll, ArtPollReply and the Port-Address), and the
/// messages of the Enttec DMX USB Pro. Nothing here is sent.
/// </summary>
public sealed class OutputProtocolTests
{
    private static readonly Guid Cid = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

    // ---------------------------------------------------------------- sACN

    [Fact]
    public void SacnDataPacketHasTheLayersOfE131()
    {
        var sender = new SacnSender(Cid, "Lumenet");
        byte[] levels = new byte[512];
        levels[0] = 255;
        levels[511] = 7;

        byte[] packet = sender.Packet(universe: 258, levels, priority: 150, terminated: false);

        Assert.Equal(638, packet.Length);

        // The root layer.
        Assert.Equal(0x0010, BinaryPrimitives.ReadUInt16BigEndian(packet));
        Assert.Equal(0x0000, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(2)));
        Assert.Equal("ASC-E1.17\0\0\0"u8.ToArray(), packet[4..16]);
        Assert.Equal(0x7000 | 622, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(16)));
        Assert.Equal(0x00000004u, BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(18)));
        Assert.Equal(Cid.ToByteArray(bigEndian: true), packet[22..38]);
        Assert.Equal(0x01, packet[22]);   // the CID as it is written: 01234567-...

        // The framing layer.
        Assert.Equal(0x7000 | 600, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(38)));
        Assert.Equal(0x00000002u, BinaryPrimitives.ReadUInt32BigEndian(packet.AsSpan(40)));
        Assert.Equal("Lumenet", Encoding.UTF8.GetString(packet, 44, 64).TrimEnd('\0'));
        Assert.Equal(150, packet[108]);
        Assert.Equal(0, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(109)));   // no synchronization
        Assert.Equal(0, packet[111]);                                              // the first sequence number
        Assert.Equal(0, packet[112]);                                              // no options
        Assert.Equal(258, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(113)));

        // The DMP layer.
        Assert.Equal(0x7000 | 523, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(115)));
        Assert.Equal(0x02, packet[117]);
        Assert.Equal(0xa1, packet[118]);
        Assert.Equal(0x0000, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(119)));
        Assert.Equal(0x0001, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(121)));
        Assert.Equal(513, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(123)));
        Assert.Equal(0, packet[125]);   // the start code
        Assert.Equal(levels, packet[126..]);
    }

    [Fact]
    public void SacnSequenceGrowsForEachUniverseOnItsOwnAndWrapsAround()
    {
        var sender = new SacnSender(Cid, "Lumenet");

        Assert.Equal(0, sender.Packet(1, [], 100, false)[111]);
        Assert.Equal(1, sender.Packet(1, [], 100, false)[111]);
        Assert.Equal(0, sender.Packet(2, [], 100, false)[111]);   // another universe: its own count
        Assert.Equal(2, sender.Packet(1, [], 100, false)[111]);

        for (int packet = 3; packet < 256; packet++)
            sender.Packet(1, [], 100, false);
        Assert.Equal(0, sender.Packet(1, [], 100, false)[111]);
    }

    [Fact]
    public void SacnTerminatedPacketSaysSoInItsOptions()
    {
        var sender = new SacnSender(Cid, "Lumenet");

        Assert.Equal(0x40, sender.Packet(1, [], 100, terminated: true)[112]);
    }

    [Theory]
    [InlineData(1, "239.255.0.1")]
    [InlineData(256, "239.255.1.0")]
    [InlineData(63999, "239.255.249.255")]
    public void SacnUniverseHasItsMulticastAddress(int universe, string address)
    {
        Assert.Equal(IPAddress.Parse(address), SacnSender.MulticastAddress(universe));
    }

    [Fact]
    public void SacnPacketOfFewerLevelsStillCarriesTheWholeUniverse()
    {
        var sender = new SacnSender(Cid, "Lumenet");

        byte[] packet = sender.Packet(1, [7, 8], 100, terminated: false);

        Assert.Equal(638, packet.Length);
        Assert.Equal(513, BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(123)));
        Assert.Equal([7, 8], packet[126..128]);
        Assert.All(packet[128..], level => Assert.Equal(0, level));
    }

    [Fact]
    public void SacnSourceNameIsCutToItsField()
    {
        var sender = new SacnSender(Cid, new string('x', 100));

        byte[] packet = sender.Packet(1, [], 100, false);

        Assert.Equal(63, Encoding.UTF8.GetString(packet, 44, 64).TrimEnd('\0').Length);
        Assert.Equal(0, packet[107]);   // the name ends with a zero byte
    }

    // ---------------------------------------------------------------- Art-Net

    [Fact]
    public void ArtDmxHasTheHeaderOfArtNet4AndThePortAddressOfItsPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        var sender = new ArtNetSender(socket, new IPEndPoint(IPAddress.Loopback, 6454), ArtNetSender.PortAddress(1, 2, 3));

        byte[] packet = sender.Packet([10, 20, 30]);

        byte[] header = [.. "Art-Net\0"u8, 0x00, 0x50, 0x00, 14];   // OpDmx low byte first, version 14 high byte first
        Assert.Equal(header, packet[..12]);
        Assert.Equal(1, packet[12]);      // the sequence of the first frame
        Assert.Equal(0, packet[13]);      // the physical port
        Assert.Equal(0x23, packet[14]);   // SubUni: Sub-Net 2, Universe 3
        Assert.Equal(1, packet[15]);      // Net
        Assert.Equal([0, 4], packet[16..18]);   // the length, high byte first: three channels made even
        Assert.Equal([10, 20, 30, 0], packet[18..]);
    }

    [Fact]
    public void ArtDmxHasTwoChannelsAtLeastAnd512AtMost()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        var sender = new ArtNetSender(socket, new IPEndPoint(IPAddress.Loopback, 6454), 0);

        Assert.Equal([0, 2, 0, 0], sender.Packet([])[16..]);
        byte[] full = sender.Packet(new byte[600]);
        Assert.Equal(18 + 512, full.Length);
        Assert.Equal([2, 0], full[16..18]);
    }

    [Fact]
    public void ArtDmxIsNumberedOneTo255AndNeverZero()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        var sender = new ArtNetSender(socket, new IPEndPoint(IPAddress.Loopback, 6454), 0);

        byte[] sequences = [.. Enumerable.Range(0, 256).Select(_ => sender.Packet([1])[12])];

        Assert.Equal(1, sequences[0]);
        Assert.Equal(255, sequences[254]);
        Assert.Equal(1, sequences[255]);
        Assert.DoesNotContain((byte)0, sequences);
    }

    [Fact]
    public void ArtPollIsTheShortPollEveryNodeAnswers()
    {
        // The header, OpPoll 0x2000 low byte first, version 14, then Flags and DiagPriority at 0:
        // "answer this poll, and only this one".
        byte[] poll = [.. "Art-Net\0"u8, 0x00, 0x20, 0x00, 14, 0, 0];

        Assert.Equal(poll, ArtNetDiscovery.BuildPoll());
    }

    [Theory]
    [InlineData(0, 0, 0, 0, "0:0:0")]
    [InlineData(0, 1, 1, 17, "0:1:1")]
    [InlineData(1, 0, 0, 256, "1:0:0")]
    [InlineData(127, 15, 15, 32767, "127:15:15")]
    public void PortAddressIsTheNetTheSubNetAndTheUniverse(int net, int subNet, int universe, int portAddress, string text)
    {
        Assert.Equal(portAddress, ArtNetSender.PortAddress(net, subNet, universe));
        Assert.Equal(text, ArtNetSender.PortAddressText(portAddress));
    }

    [Fact]
    public void PollReplyGivesEveryPortThatPutsOutDmx()
    {
        // A node at Net 1, Sub-Net 2 with four ports: an output at universe 3, an input only,
        // outputs at universes 5 and 6.
        byte[] reply = new byte[239];
        Encoding.ASCII.GetBytes("Art-Net\0").CopyTo(reply, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(reply.AsSpan(8), 0x2100);
        new byte[] { 192, 168, 1, 50 }.CopyTo(reply, 10);
        reply[16] = 1;
        reply[17] = 4;
        reply[18] = 1;
        reply[19] = 2;
        Encoding.ASCII.GetBytes("Node 4").CopyTo(reply, 26);
        Encoding.ASCII.GetBytes("A node with four ports").CopyTo(reply, 44);
        reply[173] = 4;
        reply[174] = 0x80;
        reply[175] = 0x40;
        reply[176] = 0x80;
        reply[177] = 0x80;
        reply[190] = 3;
        reply[191] = 4;
        reply[192] = 5;
        reply[193] = 6;

        Assert.True(ArtNetDiscovery.TryParseReply(reply, out ArtNetNode? node));
        Assert.Equal("192.168.1.50", node.Ip);
        Assert.Equal("Node 4", node.ShortName);
        Assert.Equal("1.4", node.Firmware);
        Assert.Equal(0x123, node.Universe);
        Assert.Equal([0x123, 0x125, 0x126], node.Ports);
    }

    [Fact]
    public void AnswersOfANodeWithMorePortsArePutTogether()
    {
        var nodes = new List<ArtNetNode>();
        var first = new ArtNetNode("10.0.0.5", "Big", "", 0, "1.0", "") { Ports = [0, 1, 2, 3] };
        var second = first with { Ports = [4, 5] };
        var other = new ArtNetNode("10.0.0.6", "Small", "", 0, "1.0", "") { Ports = [0] };

        ArtNetDiscovery.Add(nodes, first);
        ArtNetDiscovery.Add(nodes, other);
        ArtNetDiscovery.Add(nodes, second);
        ArtNetDiscovery.Add(nodes, first);   // the same answer once more

        Assert.Equal(2, nodes.Count);
        Assert.Equal([0, 1, 2, 3, 4, 5], nodes[0].Ports);
        Assert.Equal([0], nodes[1].Ports);
    }

    [Fact]
    public void SomethingElseIsNoPollReply()
    {
        Assert.False(ArtNetDiscovery.TryParseReply(ArtNetDiscovery.BuildPoll(), out _));
        Assert.False(ArtNetDiscovery.TryParseReply(new byte[300], out _));
    }

    // ---------------------------------------------------------------- the Enttec DMX USB Pro

    [Fact]
    public void DmxMessageIsLabelSixWithTheStartCodeAndTheSlots()
    {
        byte[] levels = new byte[512];
        levels[0] = 9;
        levels[511] = 8;

        byte[] message = EnttecPro.DmxMessage(levels);

        Assert.Equal(518, message.Length);
        Assert.Equal([0x7E, 6, 0x01, 0x02, 0], message[..5]);   // 513 bytes: 0x0201, low byte first
        Assert.Equal(9, message[5]);
        Assert.Equal(8, message[516]);
        Assert.Equal(0xE7, message[^1]);
    }

    [Fact]
    public void ShortFrameIsFilledUpToTwentyFourSlots()
    {
        byte[] message = EnttecPro.DmxMessage([1, 2, 3]);

        Assert.Equal(25, message[2]);   // the start code and 24 slots
        Assert.Equal([0, 1, 2, 3, 0], message[4..9]);
        Assert.Equal(4 + 25 + 1, message.Length);
    }

    [Fact]
    public void RequestsHaveTheirLabels()
    {
        Assert.Equal([0x7E, 3, 2, 0, 0, 0, 0xE7], EnttecPro.ParametersRequest());
        Assert.Equal([0x7E, 10, 0, 0, 0xE7], EnttecPro.SerialNumberRequest());
    }

    [Fact]
    public void AnswersAreTakenOutOfWhatCameInAndGarbageIsSkipped()
    {
        byte[] serialNumber = EnttecPro.Message(10, [0x67, 0x45, 0x23, 0x01]);
        var received = new List<byte> { 0x00, 0x13 };   // garbage before the first answer
        received.AddRange(EnttecPro.Message(3, [44, 1, 9, 1, 40]));
        received.AddRange(serialNumber[..5]);   // only begun

        Assert.True(EnttecPro.TryTake(received, out byte label, out byte[] data));
        Assert.Equal(3, label);
        Assert.Equal("1.44", EnttecPro.FirmwareOf(data));

        Assert.False(EnttecPro.TryTake(received, out _, out _));   // the rest has not come yet
        received.AddRange(serialNumber[5..]);

        Assert.True(EnttecPro.TryTake(received, out label, out data));
        Assert.Equal(10, label);
        Assert.Equal("01234567", EnttecPro.SerialNumberOf(data));
        Assert.Empty(received);
    }

    [Fact]
    public void MessageWithoutItsEndIsThrownAway()
    {
        var received = new List<byte>(EnttecPro.Message(3, [1, 2]));
        received[^1] = 0x00;   // no 0xE7 where the length says
        received.AddRange(EnttecPro.Message(10, [0x10, 0x32, 0x54, 0x76]));

        Assert.True(EnttecPro.TryTake(received, out byte label, out byte[] data));
        Assert.Equal(10, label);
        Assert.Equal("76543210", EnttecPro.SerialNumberOf(data));
    }

    [Fact]
    public void SerialNumberOfAnUnprogrammedInterfaceIsNone()
    {
        Assert.Null(EnttecPro.SerialNumberOf([0xFF, 0xFF, 0xFF, 0xFF]));
    }
}
