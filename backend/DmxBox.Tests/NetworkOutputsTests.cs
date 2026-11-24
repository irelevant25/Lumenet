using System.Net;
using DmxBox.Server;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmxBox.Tests;

/// <summary>
/// The Art-Net and sACN outputs, with a pretend node and a pretend receiver on 127.0.0.1. The
/// tests do the rounds themselves and name their times; the packets travel for real, so the tests
/// wait for them to arrive.
/// </summary>
/// <remarks>
/// Windows holds the first packet of a new flow (a socket and a receiver) for a moment while its
/// firewall looks at it, and drops what follows meanwhile; the desk's repeats make up for that.
/// So a test first plays its outputs for the first three rounds (<see cref="WarmUpAsync"/>), and
/// only then counts what arrives, from 100 ms on. The next frame that does not change is due at
/// 850 ms: 0.8 seconds after the last repeat, at 50 ms.
/// </remarks>
public sealed class NetworkOutputsTests : IDisposable
{
    private readonly Desk _desk = new();
    private readonly PretendPc _pc = new();
    private readonly FakeArtNetNode _node = new();
    private readonly FakeSacnReceiver _receiver = new();
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("lumenet-outputs-");
    private readonly Dictionary<string, (string State, string Detail)> _states = [];
    private readonly Lock _statesLock = new();
    private readonly HashSet<string> _boxes = ["192.168.7.1"];
    private readonly NetworkOutputs _outputs;
    private ArtNetSocket? _artNet;

    public NetworkOutputsTests()
    {
        var settings = new OutputSettings
        {
            Bind = IPAddress.Loopback,
            ArtNetPort = _node.Port,
            ArtNetReplyPort = 0,
            SacnPort = _receiver.Port,
            CidFile = Path.Combine(_folder.FullName, SacnSource.FileName),
        };
        _outputs = new NetworkOutputs(
            _desk, _pc, settings, ArtNet, _boxes.Contains, Report, NullLogger.Instance);
    }

    public void Dispose()
    {
        _outputs.Dispose();
        _artNet?.Dispose();
        _node.Dispose();
        _receiver.Dispose();
        _pc.Dispose();
        _folder.Delete(recursive: true);
    }

    // ---------------------------------------------------------------- Art-Net

    [Fact]
    public async Task ArtNetNodeGetsTheUniverseOfEachPortAtItsPortAddress()
    {
        _desk.SetUniverses(2);
        await WarmUpAsync(ArtNetTo("127.0.0.1", new OutputPort(1, 0), new OutputPort(2, 17)));

        _desk.SetChannel(1, 11);
        _desk.SetChannel(513, 22);
        _outputs.Step(At(100));

        await FramesAsync(2);
        ArtDmxFrame first = _node.Frames.Single(frame => frame.PortAddress == 0);
        ArtDmxFrame second = _node.Frames.Single(frame => frame.PortAddress == 17);
        Assert.Equal(512, first.Levels.Length);
        Assert.Equal(11, first.Levels[0]);
        Assert.Equal(22, second.Levels[0]);
    }

    [Fact]
    public async Task ChangeIsSentThreeTimesAndThenAgainEveryEightTenthsOfASecond()
    {
        await WarmUpAsync(ArtNetTo("127.0.0.1", new OutputPort(1, 0)));

        _desk.SetChannel(1, 50);
        for (int milliseconds = 100; milliseconds <= 175; milliseconds += 25)
            _outputs.Step(At(milliseconds));

        // The change, and twice more in the next rounds; then nothing until it is due again.
        await FramesAsync(3);
        await Task.Delay(100);
        Assert.Equal([50, 50, 50], _node.Frames.Select(frame => (int)frame.Levels[0]));

        _outputs.Step(At(925));   // 0.775 s after the last repeat, at 150 ms
        await Task.Delay(100);
        Assert.Equal(3, _node.Frames.Length);

        _outputs.Step(At(950));
        Assert.Equal(50, (await FramesAsync(4))[3].Levels[0]);
    }

    [Fact]
    public async Task UnchangedUniverseGoesOutAgainAfterEightTenthsOfASecond()
    {
        await WarmUpAsync(ArtNetTo("127.0.0.1", new OutputPort(1, 0)));

        _outputs.Step(At(825));
        await Task.Delay(100);
        Assert.Empty(_node.Frames);

        _outputs.Step(At(850));
        await FramesAsync(1);
    }

    [Fact]
    public async Task FramesAreNumberedOneAfterTheOther()
    {
        await WarmUpAsync(ArtNetTo("127.0.0.1", new OutputPort(1, 0)));

        for (int round = 0; round < 3; round++)
        {
            _desk.SetChannel(1, round + 1);
            _outputs.Step(At(100 + round * 25));
        }

        // The warm-up sent 1, 2 and 3.
        ArtDmxFrame[] frames = await FramesAsync(3);
        Assert.Equal([4, 5, 6], frames.Select(frame => (int)frame.Sequence));
        Assert.Equal([1, 2, 3], frames.Select(frame => (int)frame.Levels[0]));
    }

    [Fact]
    public async Task NodeThatAnswersThePollIsConnectedWithItsName()
    {
        await WarmUpAsync(ArtNetTo("127.0.0.1", new OutputPort(1, 0), new OutputPort(1, 1)));
        await Wait.UntilAsync(() => _artNet?.AnswerOf("127.0.0.1") is not null, "The node did not answer the poll.");

        _outputs.Step(At(100));

        Assert.Equal((OutputView.Connected, "Pretend node, firmware 1.2"), StateOf("artnet 127.0.0.1 "));
    }

    [Fact]
    public async Task NodeWithoutAPortAtAnAddressIsSaidToLetThoseFramesPass()
    {
        _node.Ports = [0];
        await WarmUpAsync(ArtNetTo("127.0.0.1", new OutputPort(1, 0), new OutputPort(1, 17)));
        await Wait.UntilAsync(() => _artNet?.AnswerOf("127.0.0.1") is not null, "The node did not answer the poll.");

        _outputs.Step(At(100));

        Assert.Equal(
            (OutputView.Connected, "Pretend node, firmware 1.2. No port of it listens to 0:1:1; its ports are at 0:0:0."),
            StateOf("artnet 127.0.0.1 "));
    }

    [Fact]
    public async Task NodeThatDoesNotAnswerIsMissingButGetsTheFrames()
    {
        _node.Answers = false;
        await WarmUpAsync(ArtNetTo("127.0.0.1", new OutputPort(1, 0)));

        _desk.SetChannel(1, 9);
        _outputs.Step(At(100));

        Assert.Equal(9, (await FramesAsync(1))[0].Levels[0]);
        Assert.Equal(
            (OutputView.Missing, "No answer to the Art-Net poll. The frames go out anyway; a firewall on this PC may hold the answers back."),
            StateOf("artnet 127.0.0.1 "));
    }

    [Fact]
    public async Task NodeIsPolledEveryTwoSeconds()
    {
        await WarmUpAsync(ArtNetTo("127.0.0.1", new OutputPort(1, 0)));
        Assert.Equal(1, _node.Polls);

        _outputs.Step(At(1975));
        await Task.Delay(100);
        Assert.Equal(1, _node.Polls);

        _outputs.Step(At(2000));
        await Wait.UntilAsync(() => _node.Polls == 2, "The node was not polled again.");
    }

    [Fact]
    public async Task NodeThatCallsItselfADmxboxGetsNothing()
    {
        _node.ShortName = "dmxbox";
        await WarmUpAsync(ArtNetTo("127.0.0.1", new OutputPort(1, 0)));
        await Wait.UntilAsync(() => _artNet?.AnswerOf("127.0.0.1") is not null, "The node did not answer the poll.");

        _desk.SetChannel(1, 255);
        _outputs.Step(At(100));
        _outputs.Step(At(2000));   // polled again: still a dmxbox
        await Wait.UntilAsync(() => _node.Polls == 2, "The node was not polled again.");
        _outputs.Step(At(2025));
        await Task.Delay(100);

        Assert.Empty(_node.Frames);
        Assert.Equal(OutputView.Conflict, StateOf("artnet 127.0.0.1 ").State);
    }

    [Fact]
    public async Task AddressOfABoxGetsNothing()
    {
        _boxes.Add("127.0.0.1");
        Play(ArtNetTo("127.0.0.1", new OutputPort(1, 0)));

        _outputs.Step(At(0));
        await Task.Delay(200);

        Assert.Empty(_node.Frames);
        Assert.Equal(0, _node.Polls);
        Assert.Equal(OutputView.Conflict, StateOf("artnet 127.0.0.1 ").State);
    }

    [Fact]
    public async Task PortThatPlaysNoUniverseSendsNothing()
    {
        await WarmUpAsync(ArtNetTo("127.0.0.1", new OutputPort(0, 0), new OutputPort(1, 1)));

        _desk.SetChannel(1, 1);
        _outputs.Step(At(100));
        await FramesAsync(1);
        await Task.Delay(100);

        Assert.Equal([1], _node.Frames.Select(frame => frame.PortAddress));
    }

    [Fact]
    public void BroadcastOfATestDeskThatSendsFromLoopbackOnlyIsMissing()
    {
        Play(new OutputPlan(OutputPlan.ArtNet, OutputPlan.Broadcast, [new OutputPort(1, 0)]));

        _outputs.Step(At(0));

        Assert.Equal(
            (OutputView.Missing, "The desk sends from 127.0.0.1 only (the setting Outputs:Bind)."),
            StateOf("artnet broadcast "));
    }

    // ---------------------------------------------------------------- sACN

    [Fact]
    public async Task SacnGoesToTheReceiverWithTheDesksNameCidAndPriority()
    {
        await WarmUpAsync(SacnTo("127.0.0.1", priority: 150, new OutputPort(1, 7)));

        _desk.SetChannel(3, 33);
        _outputs.Step(At(100));

        SacnPacket packet = (await PacketsAsync(1))[0];
        Assert.Equal("Lumenet", packet.Source);
        Assert.Equal(150, packet.Priority);
        Assert.Equal(7, packet.Universe);
        Assert.False(packet.Terminated);
        Assert.Equal(33, packet.Levels[2]);
        Assert.Equal(Guid.Parse(File.ReadAllText(Path.Combine(_folder.FullName, SacnSource.FileName)).Trim()), packet.Cid);
        Assert.Equal((OutputView.Sending, "To 127.0.0.1"), StateOf("sacn 127.0.0.1 "));
    }

    [Fact]
    public async Task SacnSequenceGrowsWithEveryPacketOfAUniverse()
    {
        await WarmUpAsync(SacnTo("127.0.0.1", priority: 100, new OutputPort(1, 1), new OutputPort(1, 2)));

        _desk.SetChannel(1, 1);
        _outputs.Step(At(100));
        _outputs.Step(At(125));

        // The warm-up sent 0, 1 and 2 of each universe.
        SacnPacket[] packets = await PacketsAsync(4);
        Assert.Equal([3, 4], packets.Where(packet => packet.Universe == 1).Select(packet => (int)packet.Sequence));
        Assert.Equal([3, 4], packets.Where(packet => packet.Universe == 2).Select(packet => (int)packet.Sequence));
    }

    [Fact]
    public async Task SacnUniverseThatGoesAwayIsEndedWithThreePackets()
    {
        _desk.SetChannel(1, 80);
        await WarmUpAsync(SacnTo("127.0.0.1", priority: 100, new OutputPort(1, 1), new OutputPort(1, 2)));

        Play(SacnTo("127.0.0.1", priority: 100, new OutputPort(1, 2)));   // universe 1 is taken out
        _outputs.Step(At(100));

        SacnPacket[] ended = (await PacketsAsync(3)).Where(packet => packet.Universe == 1).ToArray();
        Assert.Equal(3, ended.Length);
        Assert.All(ended, packet => Assert.True(packet.Terminated));
        Assert.All(ended, packet => Assert.Equal(80, packet.Levels[0]));   // the last levels
        Assert.Equal([3, 4, 5], ended.Select(packet => (int)packet.Sequence));

        await Task.Delay(100);
        Assert.Equal(3, _receiver.Packets.Length);   // universe 2 goes on, and is not due yet
    }

    [Fact]
    public async Task SacnUniverseIsEndedWhenItsPortPlaysNoneAndWhenTheDeskStops()
    {
        await WarmUpAsync(SacnTo("127.0.0.1", priority: 100, new OutputPort(1, 1), new OutputPort(1, 2)));

        Play(SacnTo("127.0.0.1", priority: 100, new OutputPort(0, 1), new OutputPort(1, 2)));
        _outputs.Step(At(100));
        await PacketsAsync(3);
        await Task.Delay(100);
        Assert.All(_receiver.Packets, packet => Assert.True(packet.Terminated && packet.Universe == 1));
        _receiver.Forget();

        _outputs.Dispose();   // the desk program stops

        SacnPacket[] last = await PacketsAsync(3);
        Assert.All(last, packet => Assert.True(packet.Terminated && packet.Universe == 2));
    }

    [Fact]
    public async Task SacnPriorityChangeGoesOutAtOnceWithoutEndingTheUniverse()
    {
        await WarmUpAsync(SacnTo("127.0.0.1", priority: 100, new OutputPort(1, 1)));

        Play(SacnTo("127.0.0.1", priority: 120, new OutputPort(1, 1)));
        _outputs.Step(At(100));

        SacnPacket packet = (await PacketsAsync(1))[0];
        Assert.False(packet.Terminated);
        Assert.Equal(120, packet.Priority);
    }

    // ---------------------------------------------------------------- the same DMX as a box

    [Fact]
    public async Task EveryKindOfOutputGivesTheSameLevelsAsTheBox()
    {
        // A moving head and a light held to half: the master and blackout darken the light and
        // leave the head where it is; the limit holds the light.
        _desk.SetStage(new StagePlan(16, 10,
        [
            new StageLight(1, "Spot", 1, "PTD", null, 4, 4, 0),
            new StageLight(2, "Par", 4, "D", null, 8, 4, 0) { Limit = 50 },
        ]));
        await WarmUpAsync(ArtNetTo("127.0.0.1", new OutputPort(1, 0)), SacnTo("127.0.0.1", 100, new OutputPort(1, 1)));

        var box = new FakeBox();
        var link = new BoxLink(_desk, box, new RecordingClient(), "pretend-box", NullLogger<BoxLink>.Instance);
        var device = new FakeUsbDevice();
        using var usb = new UsbLink(_desk, device.Name, 1, device.PortAt, device.Names, (_, _) => { }, NullLogger.Instance);
        link.Step(At(0));
        usb.Step(At(0));

        _desk.SetChannel(1, 100);
        _desk.SetChannel(2, 50);
        _desk.SetChannel(3, 255);
        _desk.SetChannel(4, 255);
        _desk.SetMaster(200);
        foreach (bool blackout in new[] { false, true })
        {
            _desk.SetBlackout(blackout);
            _node.Forget();
            _receiver.Forget();
            TimeSpan now = At(blackout ? 125 : 100);
            link.Step(now);
            usb.Step(now);
            _outputs.Step(now);

            byte[] expected = box.Levels[..512];
            int[] first = blackout ? [100, 50, 0, 0] : [100, 50, 200, 100];
            Assert.Equal(first, expected[..4].Select(level => (int)level));
            Assert.Equal(expected, device.Frame);
            Assert.Equal(expected, (await FramesAsync(1))[0].Levels);
            Assert.Equal(expected, (await PacketsAsync(1))[0].Levels);
        }
    }

    // ---------------------------------------------------------------- the network of a broadcast

    [Fact]
    public void AutomaticNetworkIsTheOneOfTheRouterAndNeverABoxCable()
    {
        PcNetwork box = new(IPAddress.Parse("10.1.3.2"), 24, "USB", Router: false);
        PcNetwork pico = new(IPAddress.Parse("192.168.7.2"), 24, "USB 2", Router: false);
        PcNetwork stage = new(IPAddress.Parse("2.0.0.5"), 8, "Ethernet", Router: false);
        PcNetwork home = new(IPAddress.Parse("192.168.1.20"), 24, "Wi-Fi", Router: true);
        var automatic = new OutputPlan(OutputPlan.ArtNet, OutputPlan.Broadcast, [new OutputPort(1, 0)]);

        Assert.Equal(home, NetworkOutputs.NetworkFor(automatic, [box, pico, stage, home]));
        Assert.Equal(stage, NetworkOutputs.NetworkFor(automatic, [box, pico, stage]));
        Assert.Null(NetworkOutputs.NetworkFor(automatic, [box, pico]));
        Assert.Equal(stage, NetworkOutputs.NetworkFor(automatic with { Network = "2.0.0.0/8" }, [home, stage]));
        Assert.Null(NetworkOutputs.NetworkFor(automatic with { Network = "10.1.3.0/24" }, [box, home]));
        Assert.Equal(IPAddress.Parse("2.255.255.255"), stage.Broadcast);
        Assert.Equal(IPAddress.Parse("192.168.1.255"), home.Broadcast);
    }

    private static OutputPlan ArtNetTo(string ip, params OutputPort[] ports) => new(OutputPlan.ArtNet, ip, ports);

    private static OutputPlan SacnTo(string ip, int priority, params OutputPort[] ports) =>
        new(OutputPlan.Sacn, ip, ports) { Priority = priority };

    private static TimeSpan At(int milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    private void Play(params OutputPlan[] plans)
    {
        _outputs.Play([.. plans.Select((plan, index) => plan.Checked($"Output {index + 1}"))]);
    }

    // Plays the outputs for their first three rounds, at 0, 25 and 50 ms, until each flow is open:
    // the node has the first poll, the receiver the first packet. Then forgets what came in.
    private async Task WarmUpAsync(params OutputPlan[] plans)
    {
        Play(plans);
        _outputs.Step(At(0));
        if (plans.Any(plan => plan.Kind == OutputPlan.ArtNet))
            await Wait.UntilAsync(() => _node.Polls > 0 || _node.Frames.Length > 0, "The node got nothing.");
        if (plans.Any(plan => plan.Kind == OutputPlan.Sacn))
            await Wait.UntilAsync(() => _receiver.Packets.Length > 0, "The receiver got nothing.");

        await Task.Delay(50);
        _outputs.Step(At(25));
        _outputs.Step(At(50));
        await Task.Delay(100);
        _node.Forget();
        _receiver.Forget();
    }

    private ArtNetSocket ArtNet()
    {
        return _artNet ??= new ArtNetSocket(IPAddress.Loopback, 0, NullLogger.Instance);
    }

    private void Report(string identity, string state, string detail)
    {
        lock (_statesLock)
        {
            _states[identity] = (state, detail);
        }
    }

    private (string State, string Detail) StateOf(string identity)
    {
        lock (_statesLock)
        {
            return _states[identity];
        }
    }

    // Waits until the node has got this many frames, and gives them.
    private async Task<ArtDmxFrame[]> FramesAsync(int count)
    {
        await Wait.UntilAsync(() => _node.Frames.Length >= count, $"The node did not get {count} frames.");
        return _node.Frames;
    }

    private async Task<SacnPacket[]> PacketsAsync(int count)
    {
        await Wait.UntilAsync(() => _receiver.Packets.Length >= count, $"The receiver did not get {count} packets.");
        return _receiver.Packets;
    }
}
