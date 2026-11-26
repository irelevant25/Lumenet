using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmxBox.Tests.Osc;

/// <summary>
/// OSC on a real UDP port of 127.0.0.1: switched on and off, a port another program has, messages of
/// this PC and of other devices (only while LAN is on), the list of incoming messages, and osc.json.
/// </summary>
public sealed class OscControlTests : IDisposable
{
    private static readonly IPAddress Phone = IPAddress.Parse("192.168.7.23");

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("lumenet-osc-");
    private readonly PretendMidiDesk _desk = new();
    private readonly PretendMidiPages _pages = new();
    private readonly MidiControl _midi;
    private readonly List<OscControl> _made = [];

    // One sender for the whole test: Windows may hold the first packet of a new flow back for a
    // moment, and drops it if its socket is closed meanwhile.
    private readonly UdpClient _sender = new(AddressFamily.InterNetwork);
    private bool _lan;

    public OscControlTests()
    {
        _midi = new MidiControl(new PretendMidiPorts(), new MidiSetupFile(Path.Combine(_folder.FullName, MidiSetupFile.FileName), NullLogger.Instance), _desk, _pages, NullLogger.Instance, () => 0);
        _midi.Claim("pc");   // the page on the PC that presses the keys
        _midi.Watch("pc", true);
    }

    private string SetupFile => Path.Combine(_folder.FullName, OscSetupFile.FileName);

    public void Dispose()
    {
        foreach (OscControl osc in _made)
            osc.Dispose();
        _sender.Dispose();
        _midi.Dispose();
        _folder.Delete(recursive: true);
    }

    private async Task<OscControl> StartAsync()
    {
        var osc = new OscControl(new OscSetupFile(SetupFile, NullLogger.Instance), _midi, IPAddress.IsLoopback, () => _lan, IPAddress.Loopback, NullLogger.Instance);
        _made.Add(osc);
        await osc.StartAsync(CancellationToken.None);
        return osc;
    }

    // A port no program listens on now.
    private static int FreePort()
    {
        using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    private async Task SendAsync(int port, byte[] packet)
    {
        await _sender.SendAsync(packet, new IPEndPoint(IPAddress.Loopback, port));
    }

    private MidiMonitorLine[] Lines() => _midi.Watch("pc", true);

    [Fact]
    public async Task OscIsOffUntilSwitchedOnAndThenAMessageOfThisPcPressesAKey()
    {
        OscControl osc = await StartAsync();
        Assert.Equal(new OscView(false, OscSetup.DefaultPort, OscView.Off, null), _midi.State.Osc);
        Assert.Equal(0, osc.ListeningPort);

        int port = FreePort();
        MidiState state = osc.Set(true, port);
        Assert.Equal(new OscView(true, port, OscView.Listening, null), state.Osc);
        Assert.Equal(port, osc.ListeningPort);
        Assert.Equal(state.Osc, _pages.Last.Osc);   // every page on the PC hears of it

        await SendAsync(port, OscPacket.Write("/lumenet/stack/play"));
        await Wait.UntilAsync(() => _pages.Keys.Count == 2, "GO was not pressed.");
        Assert.Equal([("pc", "stack.play", true), ("pc", "stack.play", false)], _pages.Keys);

        await SendAsync(port, OscPacket.Bundle(OscPacket.Write("/lumenet/fader/master", 0.5f), OscPacket.Write("/lumenet/fader/channel/3", 255)));
        await Wait.UntilAsync(() => _desk.Moves.Count == 2, "The faders did not move.");
        Assert.Equal([(new DeskFader(FaderKind.Master), 128), (new DeskFader(FaderKind.Channel, 3), 255)], _desk.Moves);
    }

    [Fact]
    public async Task TheMessagesAreListedWithThoseOfTheControllers()
    {
        OscControl osc = await StartAsync();
        int port = FreePort();
        osc.Set(true, port);

        await SendAsync(port, OscPacket.Write("/lumenet/fader/master", 0.5f));
        await Wait.UntilAsync(() => Lines().Length == 1, "The message was not listed.");
        MidiMonitorLine line = Lines()[0];
        Assert.Equal(("OSC from 127.0.0.1", "/lumenet/fader/master 0.5", "fader.master", "moved", (int?)128), (line.Device, line.Message, line.Target, line.Result, line.Value));
    }

    [Fact]
    public void AMessageOfAnotherDeviceCountsOnlyWhileLanIsOn()
    {
        var osc = new OscControl(new OscSetupFile(SetupFile, NullLogger.Instance), _midi, IPAddress.IsLoopback, () => _lan, IPAddress.Loopback, NullLogger.Instance);
        _made.Add(osc);
        byte[] go = OscPacket.Write("/lumenet/stack/play");

        osc.Take(go, Phone);
        MidiMonitorLine refused = Assert.Single(Lines());
        Assert.Equal(("OSC from 192.168.7.23", "stack.play", "refused", OscControl.LanOff), (refused.Device, refused.Target, refused.Result, refused.Reason));
        Assert.Empty(_pages.Keys);

        _lan = true;
        osc.Take(go, Phone);
        Assert.Equal([("pc", "stack.play", true), ("pc", "stack.play", false)], _pages.Keys);

        // this PC always: also written as IPv6
        _lan = false;
        osc.Take(go, IPAddress.Loopback.MapToIPv6());
        Assert.Equal(4, _pages.Keys.Count);
        Assert.Equal("OSC from 127.0.0.1", Lines()[^1].Device);
    }

    [Fact]
    public async Task APortThatAnotherProgramHasIsSaidSo()
    {
        using var other = new UdpClient(AddressFamily.InterNetwork);
        other.Client.ExclusiveAddressUse = true;
        other.Client.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        int taken = ((IPEndPoint)other.Client.LocalEndPoint!).Port;

        OscControl osc = await StartAsync();
        OscView view = osc.Set(true, taken).Osc;

        Assert.Equal((true, taken, OscView.Failed), (view.On, view.Port, view.State));
        Assert.Equal($"UDP port {taken} is taken by another program: choose another port, in both programs.", view.Problem);
        Assert.Equal(0, osc.ListeningPort);

        // another port: it listens
        int free = FreePort();
        Assert.Equal(OscView.Listening, osc.Set(true, free).Osc.State);
    }

    [Fact]
    public async Task SwitchedOffItListensNoMoreAndLetsGoOfTheKeysItHolds()
    {
        OscControl osc = await StartAsync();
        int port = FreePort();
        osc.Set(true, port);
        await SendAsync(port, OscPacket.Write("/lumenet/master/solo", 1f));
        await Wait.UntilAsync(() => _pages.Keys.Count == 1, "Solo was not held.");

        Assert.Equal(new OscView(false, port, OscView.Off, null), osc.Set(false, port).Osc);
        Assert.Equal([("pc", "master.solo", true), ("pc", "master.solo", false)], _pages.Keys);
        Assert.Equal(0, osc.ListeningPort);

        // the port is free again: another program may have it
        using var other = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
    }

    [Fact]
    public async Task TheSetupIsKeptInOscJsonAndComesBackAtTheNextStart()
    {
        OscControl first = await StartAsync();
        int port = FreePort();
        first.Set(true, port);
        Assert.Contains("\"format\": \"lumenet-osc\"", File.ReadAllText(SetupFile), StringComparison.Ordinal);
        await first.StopAsync(CancellationToken.None);
        Assert.Equal(0, first.ListeningPort);

        OscControl again = await StartAsync();
        Assert.Equal(new OscView(true, port, OscView.Listening, null), _midi.State.Osc);
        Assert.Equal(port, again.ListeningPort);
    }

    [Fact]
    public async Task AFileThatCannotBeReadLeavesOscOffAndIsPutAside()
    {
        File.WriteAllText(SetupFile, "{ this is no JSON");

        await StartAsync();

        Assert.Equal(new OscView(false, OscSetup.DefaultPort, OscView.Off, null), _midi.State.Osc);
        Assert.True(File.Exists(SetupFile + ".bad"));
        Assert.False(File.Exists(SetupFile));
    }

    [Fact]
    public async Task APortThatIsNoPortIsRefused()
    {
        OscControl osc = await StartAsync();
        Assert.Equal("The port of OSC is a number from 1 to 65535.", Assert.Throws<DeskException>(() => osc.Set(true, 0)).Message);
        Assert.Throws<DeskException>(() => osc.Set(true, 65536));
        Assert.False(File.Exists(SetupFile));
    }
}
