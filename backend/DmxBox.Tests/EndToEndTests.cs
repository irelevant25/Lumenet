using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

// Several tests measure fades in real time. Run side by side, they would compete
// for the processor and fail now and then, so the tests run one after the other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace DmxBox.Tests;

/// <summary>
/// End-to-end tests: the client against the simulator, which runs the same C engine and
/// protocol code as the Pico firmware. Every test starts its own simulator.
/// </summary>
public sealed class EndToEndTests
{
    [Fact]
    public void Ping()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient();

        PingInfo info = box.Ping();

        Assert.Equal("1.0", info.Firmware);
        Assert.Equal(512, info.Channels);
        Assert.Equal(255, info.Master);
    }

    [Fact]
    public void SetAndGet()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient();

        box.Set(1, [255, 128, 7]);
        box.Set(512, [42]);

        Assert.Equal([255, 128, 7, 0], box.GetOutput(1, 4));
        Assert.Equal([42], box.GetOutput(512, 1));
    }

    [Fact]
    public void FadeRunsOnTheBox()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient();

        box.Fade(1, [200], TimeSpan.FromSeconds(0.6));
        Thread.Sleep(300);
        byte halfWay = box.GetOutput(1, 1)[0];
        Assert.InRange(halfWay, 61, 139);

        Thread.Sleep(400);
        Assert.Equal([200], box.GetOutput(1, 1));
    }

    [Fact]
    public void FadeInterruptStartsFromCurrentValue()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient();

        box.Set(1, [255]);
        box.Fade(1, [0], TimeSpan.FromSeconds(1));
        Thread.Sleep(500);
        box.Fade(1, [255], TimeSpan.FromSeconds(1));   // reverse in the middle of the fade: no jump

        byte level = box.GetOutput(1, 1)[0];
        Assert.InRange(level, 91, 169);
    }

    [Fact]
    public void MasterAndBlackout()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient();

        box.Set(1, [200, 100]);
        box.Master(128);
        Assert.Equal([100, 50], box.GetOutput(1, 2));

        box.Blackout(true);
        Assert.Equal([0, 0], box.GetOutput(1, 2));
        Assert.True(box.Ping().Blackout);

        box.Blackout(false);
        box.Master(255);
        Assert.Equal([200, 100], box.GetOutput(1, 2));
    }

    [Fact]
    public void BadArgumentsAreRejected()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient();

        AssertRejected("BAD_ARG", () => box.Set(0, [1]));
        AssertRejected("BAD_ARG", () => box.Set(511, [1, 2, 3]));
        AssertRejected("EMPTY_SLOT", () => box.PresetGo(3));
        AssertRejected("UNKNOWN_TYPE", () => box.Request(0x7E));
    }

    [Fact]
    public void PresetsPersistAcrossRestart()
    {
        using var sim = Simulator.Start();
        using (var box = sim.CreateClient())
        {
            box.Set(1, [10, 20, 30]);
            box.PresetSave(5);
            box.Set(1, [0, 0, 0]);
            box.PresetGo(5);
            Assert.Equal([10, 20, 30], box.GetOutput(1, 3));
        }

        sim.Restart();   // the "power cycle"

        using (var box = sim.CreateClient())
        {
            Assert.Contains(5, box.Ping().Presets);
            Assert.Equal([10, 20, 30, 0], box.GetPreset(5)[..4]);

            box.PresetGo(5, TimeSpan.FromSeconds(0.2));
            Thread.Sleep(300);
            Assert.Equal([10, 20, 30], box.GetOutput(1, 3));

            box.PresetClear(5);
            Assert.DoesNotContain(5, box.Ping().Presets);
        }
    }

    [Fact]
    public void RetransmittedRequestIsNotExecutedTwice()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient();
        box.Set(1, [0]);

        using Socket raw = ConnectRawSocket(sim, receiveTimeoutMilliseconds: 500);
        ushort sequence = (ushort)(box.Sequence + 10);   // newer than anything the client used

        // Channel 1, one value, 1000 ms (0x03E8), to level 250.
        byte[] fade = BuildRequest(box.Session, MessageType.Fade, sequence, [0, 1, 0, 1, 0, 0, 0x03, 0xE8, 250]);

        raw.Send(fade);
        byte[] firstAck = Receive(raw);
        Thread.Sleep(600);
        raw.Send(fade);                                  // the duplicate: it must NOT restart the fade
        byte[] secondAck = Receive(raw);
        Assert.Equal(firstAck, secondAck);

        box.Sequence = sequence;                         // keep the client's own sequence ahead of ours
        Thread.Sleep(500);
        Assert.Equal([250], box.GetOutput(1, 1));        // a restarted fade would be about half way
    }

    [Fact]
    public void StaleRequestIsDropped()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient();
        using Socket raw = ConnectRawSocket(sim, receiveTimeoutMilliseconds: 300);

        // Sequence 500 sets channel 1 to 0x11. Then the older sequence 499 tries to set it to 0x99.
        raw.Send(BuildRequest(box.Session, MessageType.Set, 500, [0, 1, 0, 1, 0x11]));
        Receive(raw);
        raw.Send(BuildRequest(box.Session, MessageType.Set, 499, [0, 1, 0, 1, 0x99]));

        var noAnswer = Assert.Throws<SocketException>(() => Receive(raw));
        Assert.Equal(SocketError.TimedOut, noAnswer.SocketErrorCode);

        box.Sequence = 500;
        Assert.Equal([0x11], box.GetOutput(1, 1));
    }

    [Fact]
    public void ReliableOverLossyNetwork()
    {
        // The simulator loses 30 % of the packets in each direction, so an attempt gets its ACK
        // with a probability of 0.7 x 0.7 = 0.49. The Python test allows 12 attempts; then about
        // one run in 50 fails by bad luck alone (0.51^12 for each of the 61 requests).
        // With 20 attempts it is about one run in 10 000.
        using var sim = Simulator.Start(drop: 0.3);
        using var box = sim.CreateClient(TimeSpan.FromMilliseconds(50), attempts: 20);

        var random = new Random(1);
        byte[] expected = new byte[32];
        for (int i = 0; i < 60; i++)
        {
            int channel = random.Next(1, 33);
            byte level = (byte)random.Next(256);
            box.Set(channel, [level]);
            expected[channel - 1] = level;
        }

        Assert.Equal(expected, box.GetOutput(1, 32));
        Assert.True(box.Retransmits > 0);   // loss really happened, and was recovered
    }

    [Fact]
    public void ArtNetHtpAndOverride()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient();
        using var artNet = sim.CreateArtNetSender();

        box.Set(1, [100, 100]);
        artNet.Send([50, 200]);
        WaitUntilArtNetArrived(box);
        Assert.Equal([100, 200], box.GetOutput(1, 2));   // HTP: the highest wins
        Assert.True(box.Ping().ArtNetLive);

        box.SetMergeMode(MergeMode.Override);
        Assert.Equal([50, 200], box.GetOutput(1, 2));    // Art-Net replaces the cue layer

        box.SetMergeMode(MergeMode.Htp);
        box.ReleaseArtNet();
        Assert.Equal([100, 100], box.GetOutput(1, 2));
    }

    [Fact]
    public void ArtNetOtherUniverseIsIgnored()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient();
        using var artNet = sim.CreateArtNetSender(universe: 3);

        artNet.Send([255]);
        Thread.Sleep(200);   // an ignored frame shows no effect to wait for, so give it ample time to arrive

        Assert.Equal([0], box.GetOutput(1, 1));
    }

    [Fact]
    public void Discover()
    {
        using var sim = Simulator.Start();

        IReadOnlyList<ArtNetNode> nodes = ArtNetDiscovery.Discover(
            timeout: TimeSpan.FromSeconds(0.5),
            target: "127.0.0.1",
            port: sim.ArtNetPort);

        ArtNetNode node = Assert.Single(nodes);
        Assert.Equal("dmxbox", node.ShortName);
        Assert.Equal(0, node.Universe);
    }

    private static void AssertRejected(string expectedStatus, Action request)
    {
        DmxBoxException rejection = Assert.Throws<DmxBoxException>(request);
        Assert.Equal(expectedStatus, rejection.StatusName);
    }

    // Art-Net is not acknowledged, and on Windows the first datagram of a new socket can take
    // some tens of milliseconds. So wait for the frame's effect instead of sleeping a fixed time.
    private static void WaitUntilArtNetArrived(DmxBoxClient box)
    {
        var waited = Stopwatch.StartNew();
        while (!box.Ping().ArtNetHolding)
        {
            Assert.True(waited.Elapsed < TimeSpan.FromSeconds(2), "The simulator did not receive the Art-Net frame.");
            Thread.Sleep(10);
        }
    }

    // A bare UDP socket, for DX packets that the client itself would never send.
    private static Socket ConnectRawSocket(Simulator sim, int receiveTimeoutMilliseconds)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Connect(IPAddress.Loopback, sim.ControlPort);
        socket.ReceiveTimeout = receiveTimeoutMilliseconds;
        return socket;
    }

    private static byte[] Receive(Socket socket)
    {
        byte[] buffer = new byte[2048];
        int length = socket.Receive(buffer);
        return buffer[..length];
    }

    // A DX request made by hand: the 12-byte header (see DEV.md, section 5), then the payload.
    private static byte[] BuildRequest(uint session, byte type, ushort sequence, byte[] payload)
    {
        byte[] packet = new byte[12 + payload.Length];
        packet[0] = (byte)'D';
        packet[1] = (byte)'X';
        packet[2] = 1;
        packet[3] = type;
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4), session);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(8), sequence);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(10), (ushort)payload.Length);
        payload.CopyTo(packet, 12);
        return packet;
    }
}
