using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using DmxBox.Server;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmxBox.Tests;

/// <summary>
/// The box link with the real client code behind it. The simulator stands in for the box:
/// it runs the same engine and protocol code as the firmware.
/// </summary>
public sealed class RealBoxTests
{
    private const string Loopback = "127.0.0.1";

    private readonly Desk _desk = new();
    private readonly RecordingClient _clients = new();

    [Fact]
    public void DeskStateArrivesOnTheSimulator()
    {
        using var sim = Simulator.Start();
        using var box = new RealBox(Loopback, sim.ControlPort);
        using DmxBoxClient observer = sim.CreateClient();
        observer.Master(100);   // left like this by some other program
        var link = new BoxLink(_desk, box, _clients, Loopback, NullLogger<BoxLink>.Instance);
        _desk.SetChannel(1, 200);
        _desk.SetChannel(512, 7);
        _desk.SetMaster(128);

        link.Step(TimeSpan.Zero);

        Assert.Equal(new BoxStatus(Connected: true, Loopback, Firmware: "1.0"), link.Status);
        Assert.Equal(255, observer.Ping().Master);
        Assert.Equal([100], observer.GetOutput(1, 1));     // 200 x 128 / 255 = 100.4
        Assert.Equal([4], observer.GetOutput(512, 1));     // 7 x 128 / 255 = 3.5
        Assert.Equal(new byte[510], observer.GetOutput(2, 510));
    }

    [Fact]
    public void BlackoutTravelsBothWaysOnTheSimulator()
    {
        using var sim = Simulator.Start();
        using var box = new RealBox(Loopback, sim.ControlPort);
        using DmxBoxClient observer = sim.CreateClient();
        var link = new BoxLink(_desk, box, _clients, Loopback, NullLogger<BoxLink>.Instance);
        link.Step(TimeSpan.Zero);

        _desk.SetBlackout(true);
        link.Step(TimeSpan.FromMilliseconds(25));
        Assert.True(observer.Ping().Blackout);

        observer.Blackout(false);   // like a press of the button on the box
        link.Step(TimeSpan.FromMilliseconds(500));
        Assert.False(_desk.Blackout);
        Assert.Equal([false], _clients.BlackoutChanges);
    }

    [Fact]
    public void RestartedSimulatorGetsTheDeskStateAgain()
    {
        using var sim = Simulator.Start();
        using var box = new RealBox(Loopback, sim.ControlPort);
        var link = new BoxLink(_desk, box, _clients, Loopback, NullLogger<BoxLink>.Instance);
        _desk.SetChannel(1, 200);

        // The link tells a restart by an uptime that went backwards. So its first ping has to
        // find the simulator running for longer than the restarted one will have been.
        Thread.Sleep(1000);
        link.Step(TimeSpan.Zero);
        sim.Restart();
        link.Step(TimeSpan.FromMilliseconds(500));

        using DmxBoxClient observer = sim.CreateClient();
        Assert.Equal([200], observer.GetOutput(1, 1));
        Assert.True(link.Status.Connected);
    }

    [Fact]
    public void MissingBoxIsNoticedWithinAboutASecond()
    {
        using var box = new RealBox(Loopback, PortWhereNobodyListens());

        var waited = Stopwatch.StartNew();
        Assert.Throws<DmxBoxTimeoutException>(() => box.Ping());

        // Two attempts: 250 ms and 500 ms of waiting.
        Assert.InRange(waited.Elapsed, TimeSpan.FromMilliseconds(700), TimeSpan.FromMilliseconds(1500));
    }

    [Fact]
    public void BoxWithAnAddressThatCannotBeUsedIsJustNotConnected()
    {
        // Making the client fails here, before anything is sent. That must not stop the
        // server from starting: the link has to treat it like a box that is not there.
        using var box = new RealBox(host: "", port: 7000);
        var link = new BoxLink(_desk, box, _clients, "nowhere", NullLogger<BoxLink>.Instance);

        link.Step(TimeSpan.Zero);
        link.Step(TimeSpan.FromSeconds(1));

        Assert.False(link.Status.Connected);
        Assert.Empty(_clients.BoxChanges);
    }

    // A UDP port of this PC that is free right now.
    private static int PortWhereNobodyListens()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
