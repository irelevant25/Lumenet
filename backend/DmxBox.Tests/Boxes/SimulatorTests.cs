using System.Net;
using System.Net.Sockets;
using DmxBox.Sim;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmxBox.Tests.Boxes;

/// <summary>
/// The simulators of sim.cmd, played in the test's own process, and the desk finding them as if
/// they were plugged in: the registry of the simulators, a simulated box in the box network, a
/// simulated Art-Net node in Find nodes and as an output, a simulated sACN receiver, and a
/// simulated Enttec DMX USB Pro at a port "SIM" through a pipe. The two box simulators of
/// firmware/sim run as their own programs.
/// </summary>
public sealed class SimulatorTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("lumenet-sim-");

    public void Dispose()
    {
        try
        {
            _folder.Delete(recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A leftover folder in the temp directory is harmless.
        }
    }

    private string Folder => _folder.FullName;

    // ---------------------------------------------------------------- the registry

    [Fact]
    public void DeviceIsThereForTheDeskWhilePluggedInAndItsAddressStaysClaimed()
    {
        var device = new SimulatedDevice(SimulatedDevice.Usb, "SIM4", "Enttec DMX USB Pro (simulator)", Environment.ProcessId);

        SimulatorClaim claim = SimulatorRegistry.Claim(Folder, device);
        Assert.Empty(SimulatorRegistry.Read(Folder));   // pulled out at first
        Assert.Single(SimulatorRegistry.Read(Folder, pulledOutToo: true));

        claim.PluggedIn = true;
        Assert.Equal([device with { PluggedIn = true }], SimulatorRegistry.Read(Folder));

        claim.PluggedIn = false;
        Assert.Empty(SimulatorRegistry.Read(Folder));
        Assert.Throws<AddressTakenException>(() => SimulatorRegistry.Claim(Folder, device with { Kind = SimulatedDevice.Usb }));

        claim.Dispose();
        Assert.Empty(SimulatorRegistry.Read(Folder, pulledOutToo: true));
    }

    [Fact]
    public void OneAddressIsOneSimulatorsAlsoForAnotherKind()
    {
        using SimulatorClaim node = SimulatorRegistry.Claim(
            Folder, new SimulatedDevice(SimulatedDevice.ArtNet, "127.0.0.2", "a node", Environment.ProcessId));

        AddressTakenException taken = Assert.Throws<AddressTakenException>(() => SimulatorRegistry.Claim(
            Folder, new SimulatedDevice(SimulatedDevice.Sacn, "127.0.0.2", "a receiver", Environment.ProcessId)));
        Assert.Equal("127.0.0.2 is taken by another simulator (a node).", taken.Message);
    }

    [Fact]
    public void SimulatorThatWasKilledIsGoneAndItsAddressFree()
    {
        using var ended = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("dotnet", "--version") { CreateNoWindow = true, RedirectStandardOutput = true })!;
        ended.WaitForExit();
        SimulatorClaim killed = SimulatorRegistry.Claim(Folder, new SimulatedDevice(SimulatedDevice.Dmxbox, "127.0.0.1:7011", "a box", ended.Id));
        killed.PluggedIn = true;
        File.WriteAllText(Path.Combine(Folder, "broken.json"), "{ not json");

        Assert.Empty(SimulatorRegistry.Read(Folder));
        Assert.Equal(["broken.json"], Directory.GetFiles(Folder).Select(Path.GetFileName));

        using SimulatorClaim again = SimulatorRegistry.Claim(
            Folder, new SimulatedDevice(SimulatedDevice.Dmxbox, "127.0.0.1:7011", "a box", Environment.ProcessId));
    }

    [Fact]
    public void ClaimOfAKilledSimulatorIsTakenOver()
    {
        using var ended = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo("dotnet", "--version") { CreateNoWindow = true, RedirectStandardOutput = true })!;
        ended.WaitForExit();
        SimulatorRegistry.Claim(Folder, new SimulatedDevice(SimulatedDevice.ArtNet, "127.0.0.5", "a node", ended.Id));

        using SimulatorClaim claim = SimulatorRegistry.Claim(
            Folder, new SimulatedDevice(SimulatedDevice.ArtNet, "127.0.0.5", "another node", Environment.ProcessId));

        Assert.Equal("another node", Assert.Single(SimulatorRegistry.Read(Folder, pulledOutToo: true)).Name);
    }

    [Fact]
    public void SimulatedPortIsAPortOfTheProject()
    {
        OutputPlan plan = new OutputPlan(OutputPlan.Usb, "sim2", [new OutputPort(1, 0)]).Checked("Output 1");

        Assert.Equal("SIM2", plan.Target);
        Assert.Equal(PipeDmxPort.PipeName("SIM2"), EnttecProSimulator.PipeName("sim2"));
    }

    // ---------------------------------------------------------------- a simulated box

    [Fact]
    public async Task BoxNetworkLooksForASimulatedBoxForAsLongAsItRuns()
    {
        var desk = new Desk();
        using var pc = new PretendPc();
        var simulated = new FakeBox();
        var simulators = new Simulators(Folder);
        using SimulatorClaim plugged = SimulatorRegistry.Claim(
            Folder, new SimulatedDevice(SimulatedDevice.Dmxbox, "127.0.0.1:7011", "a box", Environment.ProcessId));
        plugged.PluggedIn = true;
        var missing = new FakeBox();
        missing.Unplug();
        using var network = new BoxNetwork(
            desk, new RecordingClient(), pc, "192.168.7.1", BoxPlan.DefaultPort, discover: false, missing,
            NullLoggerFactory.Instance, address => address == "127.0.0.1:7011" ? simulated : new FakeBox(), simulators);

        network.Refresh();
        await Wait.UntilAsync(() => network.Boxes.Any(box => box is { Address: "127.0.0.1:7011", Connected: true }), "The simulated box was not found.");

        plugged.PluggedIn = false;
        await Wait.UntilAsync(
            () =>
            {
                network.Refresh();
                return simulated.Disposed;
            },
            "The link of the simulated box did not stop.");
    }

    [Theory]
    [InlineData("dmxbox", 512, "dmxbox 3", "dmxbox 3 - RP2040 USB Art-Net node with RDM and MIDI")]
    [InlineData("dmxbox-2-universes", 1024, "dmxbox 3", "dmxbox 3 - RP2040 USB Art-Net node with RDM and MIDI")]
    public void EachBoxSimulatorIsItsBox(string device, int channels, string shortName, string longName)
    {
        BoxModel model = BoxSimulator.Models.Single(item => item.Device == device);
        using var box = new BoxSimulator(model, Folder, BoxProgram(model.Program), show: 0, TextWriter.Null);

        box.PlugIn();

        Assert.Equal(box.Address, Assert.Single(SimulatorRegistry.Read(Folder)).Address);
        using var client = new DmxBoxClient("127.0.0.1", box.ControlPort);
        Assert.Equal(channels, client.Ping().Channels);
        ArtNetNode node = Assert.Single(ArtNetDiscovery.Discover(TimeSpan.FromSeconds(0.5), "127.0.0.1", box.ArtNetPort));
        Assert.Equal(shortName, node.ShortName);
        Assert.Equal(longName, node.LongName);
        Assert.Equal("127.0.0.1", node.Ip);
    }

    [Fact]
    public async Task SimulatedBoxHasItsNumberAndLightsWithRdmOnItsLine()
    {
        BoxModel model = BoxSimulator.Models[0];
        using var box = new BoxSimulator(model, Folder, BoxProgram(model.Program), show: 0, TextWriter.Null, lights: 5, boxNumber: 9);
        box.PlugIn();
        using var client = new DmxBoxClient("127.0.0.1", box.ControlPort);

        PingInfo ping = client.Ping();
        Assert.Equal(9, ping.BoxNumber);
        Assert.True(ping.Abilities.HasFlag(BoxAbilities.Rdm));
        await Wait.UntilAsync(() => client.RdmLights() is { Searching: false, Lights.Count: 5 }, "The box did not find its 5 lights.");
        Assert.Empty(box.Keys);   // dmxbox 3 has no buttons

        // Box 9 plays universe 9: Art-Net for another universe does not reach it.
        using var other = new ArtNetSender("127.0.0.1", universe: 0, port: box.ArtNetPort);
        using var own = new ArtNetSender("127.0.0.1", universe: 9, port: box.ArtNetPort);
        other.Send([77]);
        own.Send([55]);
        await Wait.UntilAsync(() => client.GetOutput(1, 1)[0] == 55, "Art-Net for universe 9 did not reach box 9.");
    }

    [Fact]
    public void SimulatedBoxPulledOutIsGoneAndComesBackOnItsPort()
    {
        BoxModel model = BoxSimulator.Models[0];
        using var box = new BoxSimulator(model, Folder, BoxProgram(model.Program), show: 0, TextWriter.Null);
        box.PlugIn();
        string address = box.Address;

        box.PullOut();
        Assert.Empty(SimulatorRegistry.Read(Folder));
        using (var client = new DmxBoxClient("127.0.0.1", box.ControlPort, TimeSpan.FromMilliseconds(100), attempts: 1))
            Assert.Throws<DmxBoxTimeoutException>(() => client.Ping());

        box.PlugIn();
        Assert.Equal(address, Assert.Single(SimulatorRegistry.Read(Folder)).Address);
    }

    // ---------------------------------------------------------------- a simulated Art-Net node

    [Fact]
    public async Task SimulatedNodeAnswersFindNodesAndPlaysWhatTheDeskSends()
    {
        int port = FreeUdpPort(IPAddress.Parse("127.0.0.2"));
        using var node = new ArtNetNodeSimulator(Folder, "Sim node", [0, 1], IPAddress.Parse("127.0.0.2"), port, show: 16, TextWriter.Null);
        node.PlugIn();
        var desk = new Desk();
        using var pc = new PretendPc();
        var clients = new RecordingClient();
        var settings = new OutputSettings { Bind = IPAddress.Loopback, ArtNetPort = port, ArtNetReplyPort = 0, CidFile = Path.Combine(Folder, "cid.txt") };
        using var outputs = new OutputNetwork(desk, clients, pc, settings, _ => false, NullLoggerFactory.Instance, simulators: new Simulators(Folder));

        FoundNode found = Assert.Single(await outputs.FindArtNetNodesAsync(), item => item.Ip == "127.0.0.2");
        Assert.Equal("Sim node", found.ShortName);
        Assert.Equal([0, 1], found.Ports);

        desk.SetChannel(1, 77);
        desk.SetOutputs([new OutputPlan(OutputPlan.ArtNet, "127.0.0.2", [new OutputPort(1, 1)])]);
        outputs.Refresh();
        await Wait.UntilAsync(() => node.LevelsOf(1)?[0] == 77, "The simulated node did not get the universe.");
        await Wait.UntilAsync(
            () => clients.OutputsChanges is [.., [{ State: OutputView.Connected, Detail: "Sim node, firmware 1.0" }]],
            "The simulated node did not show as connected.");
    }

    // ---------------------------------------------------------------- a simulated sACN receiver

    [Fact]
    public async Task SimulatedReceiverTakesTheDesksSacnAndHearsItEnd()
    {
        int port = FreeUdpPort(IPAddress.Parse("127.0.0.3"));
        using var receiver = new SacnReceiverSimulator(Folder, [5], multicast: false, IPAddress.Parse("127.0.0.3"), port, show: 16, TextWriter.Null);
        receiver.PlugIn();
        var desk = new Desk();
        using var pc = new PretendPc();
        var settings = new OutputSettings { Bind = IPAddress.Loopback, SacnPort = port, CidFile = Path.Combine(Folder, "cid.txt") };
        using var outputs = new OutputNetwork(desk, new RecordingClient(), pc, settings, _ => false, NullLoggerFactory.Instance);
        outputs.Refresh();

        desk.SetChannel(1, 33);
        desk.SetOutputs([new OutputPlan(OutputPlan.Sacn, "127.0.0.3", [new OutputPort(1, 5)]) { Priority = 150 }]);
        outputs.Refresh();
        await Wait.UntilAsync(() => receiver.SourcesOf(5) is [{ Name: "Lumenet", Priority: 150 } source] && source.Levels[0] == 33,
            "The simulated receiver did not take the desk's universe.");

        desk.SetOutputs([]);
        outputs.Refresh();
        await Wait.UntilAsync(() => receiver.SourcesOf(5) is [{ Terminated: true }], "The simulated receiver did not hear the universe end.");
    }

    // ---------------------------------------------------------------- a simulated Enttec DMX USB Pro

    [Fact]
    public async Task SimulatedInterfaceIsAPortOfTheDeskAndCanBePulledOut()
    {
        string name = $"SIM{Random.Shared.Next(100, 1000)}";
        using var simulated = new EnttecProSimulator(Folder, name, "87654321", show: 16, TextWriter.Null);
        simulated.PlugIn();
        var desk = new Desk();
        var simulators = new Simulators(Folder);
        var states = new List<(string State, string Detail)>();
        var link = new UsbLink(
            desk, name, universe: 1, port => new PipeDmxPort(port), () => simulators.AddressesOf(SimulatedDevice.Usb),
            (state, detail) => { lock (states) states.Add((state, detail)); }, NullLogger.Instance);
        using var stop = new CancellationTokenSource();
        var thread = new Thread(() => link.Run(stop.Token));
        desk.SetChannel(1, 123);

        try
        {
            thread.Start();
            await Wait.UntilAsync(() => simulated.Frame?[0] == 123, "The simulated interface got no frame.");
            await Wait.UntilAsync(() => LastState(states) == (OutputView.Connected, "Serial number 87654321, firmware 1.44"), "It did not show as connected.");

            simulated.PullOut();
            await Wait.UntilAsync(() => LastState(states).State == OutputView.Missing, "Pulled out, it did not show as missing.");

            simulated.PlugIn();
            desk.SetChannel(1, 45);
            await Wait.UntilAsync(() => simulated.Frame?[0] == 45 && LastState(states).State == OutputView.Connected, "Plugged in again, it did not play.");
        }
        finally
        {
            stop.Cancel();
            thread.Join(TimeSpan.FromSeconds(5));
            link.Dispose();
        }
    }

    // ---------------------------------------------------------------- a simulated MIDI controller

    [Fact]
    public async Task SimulatedControllerIsAMidiDeviceOfTheDeskBothWays()
    {
        using var apc = new MidiControllerSimulator(MidiControllers.Find("apc-mini-mk2")!, Folder, port: 0, TextWriter.Null);
        apc.PlugIn();
        var ports = new SimulatedMidiPorts(new Simulators(Folder));
        Assert.Contains("APC mini mk2 (simulator)", ports.List().Inputs);
        var received = new List<MidiMessage>();
        using IMidiConnection connection = ports.Open("APC mini mk2 (simulator)", message =>
        {
            lock (received)
                received.Add(message);
        });
        await Wait.UntilAsync(() => apc.DeskConnected, "The simulator did not see the desk.");

        // a pad pressed and a fader moved on its page
        apc.PressControl("n56", down: true);
        apc.MoveControl("c48", 100);
        await Wait.UntilAsync(() => Count(received) == 2, "The desk did not hear the controller.");
        Assert.Equal([new MidiMessage(MidiKind.NoteOn, 1, 56, 127), new MidiMessage(MidiKind.ControlChange, 1, 48, 100)], received);

        // the desk lights the pad of memory 1 red at half, as the APC reads it
        Assert.True(connection.Send(new MidiMessage(MidiKind.NoteOn, 3, 32, 5)));
        await Wait.UntilAsync(() => apc.Look("n32").Color == "#FF0000", "The pad was not lit.");
        Assert.Equal((0.5, ""), (apc.Look("n32").Level, apc.Look("n32").Motion));

        // a second one gets a name of its own
        using var another = new MidiControllerSimulator(MidiControllers.Find("apc-mini-mk2")!, Folder, port: 0, TextWriter.Null);
        Assert.Equal("APC mini mk2 2 (simulator)", another.Name);

        apc.PullOut();
        await Wait.UntilAsync(() => connection.Failed, "Pulled out, the desk's connection did not end.");
        await Wait.UntilAsync(() => !ports.List().Inputs.Contains("APC mini mk2 (simulator)"), "Pulled out, it was still listed.");
    }

    [Fact]
    public async Task TheXTouchCompactSimulatorTakesTheLightsAndMotorsWhereTheRealOneDoes()
    {
        using var compact = new MidiControllerSimulator(MidiControllers.Find("x-touch-compact")!, Folder, port: 0, TextWriter.Null);
        compact.PlugIn();
        var ports = new SimulatedMidiPorts(new Simulators(Folder));
        using IMidiConnection connection = ports.Open("X-TOUCH COMPACT (simulator)", _ => { });
        await Wait.UntilAsync(() => compact.DeskConnected, "The simulator did not see the desk.");

        connection.Send(new MidiMessage(MidiKind.NoteOn, 2, 38, 2));        // Play (Note 54) lit
        connection.Send(new MidiMessage(MidiKind.ControlChange, 1, 1, 127)); // fader 1 to the top
        connection.Send(new MidiMessage(MidiKind.ControlChange, 1, 10, 64)); // knob 1 half way
        await Wait.UntilAsync(() => compact.Look("c10").Value == 64, "The desk's messages did not arrive.");
        Assert.NotNull(compact.Look("n54").Color);
        Assert.Equal(127, compact.Look("c1").Value);
    }

    private static int Count(List<MidiMessage> messages)
    {
        lock (messages)
            return messages.Count;
    }

    private static (string State, string Detail) LastState(List<(string State, string Detail)> states)
    {
        lock (states)
        {
            return states.Count == 0 ? ("", "") : states[^1];
        }
    }

    // A UDP port that is free at an address of this PC right now.
    private static int FreeUdpPort(IPAddress address)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(address, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    // firmware/sim/<program>.exe in the first folder above the tests that has it; the release
    // workflow builds them before the tests, as for the other tests of the box simulator.
    internal static string BoxProgram(string program)
    {
        string relativePath = Path.Combine("firmware", "sim", OperatingSystem.IsWindows() ? $"{program}.exe" : program);
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            string candidate = Path.Combine(folder.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException(
            $"{relativePath} was not found above {AppContext.BaseDirectory}. Build it with firmware\\sim\\build.cmd (Windows) or make -C firmware/sim.");
    }
}
