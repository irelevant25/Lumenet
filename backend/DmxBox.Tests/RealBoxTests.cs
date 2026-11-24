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
    public void BoxWithTwoOutputsPlaysTheSecondUniverseOnItsSecondOutput()
    {
        // The simulator of the PIC32 box: the same code as its firmware, with 1024 channels.
        using var sim = Simulator.Start(outputs: 2);
        using var box = new RealBox(Loopback, sim.ControlPort);
        using DmxBoxClient observer = sim.CreateClient();
        var link = new BoxLink(_desk, box, _clients, Loopback, NullLogger<BoxLink>.Instance);
        _desk.SetUniverses(2);
        _desk.SetChannel(1, 10);
        _desk.SetChannel(513, 200);    // channel 1 (U2)
        _desk.SetChannel(1024, 7);     // channel 512 (U2)

        link.Step(TimeSpan.Zero);

        Assert.True(link.Status.Connected);
        Assert.Equal(2, link.Status.Outputs);
        Assert.Equal([10], observer.GetOutput(1, 1));
        Assert.Equal([200], observer.GetOutput(513, 1));
        Assert.Equal([7], observer.GetOutput(1024, 1));
        Assert.Equal(new byte[510], observer.GetOutput(514, 510));
    }

    [Fact]
    public void BlackoutTravelsBothWaysOnTheSimulator()
    {
        using var sim = Simulator.Start();
        using var box = new RealBox(Loopback, sim.ControlPort);
        using DmxBoxClient observer = sim.CreateClient();
        var link = new BoxLink(_desk, box, _clients, Loopback, NullLogger<BoxLink>.Instance);
        _desk.SetChannel(1, 200);
        link.Step(TimeSpan.Zero);

        // From the UI: a dark look; the box's own blackout stays off.
        _desk.SetBlackout(true);
        link.Step(TimeSpan.FromMilliseconds(25));
        Assert.Equal([0], observer.GetOutput(1, 1));
        Assert.False(observer.Ping().Blackout);

        // The button: the box goes dark by itself, the desk's blackout switches over, and the box
        // lets go of its own once it has the desk's output.
        observer.Blackout(true);   // like a press of the button on the box
        link.Step(TimeSpan.FromMilliseconds(100));
        Assert.False(_desk.Blackout);   // it was on: the press switched it off
        Assert.False(observer.Ping().Blackout);
        Assert.Equal([200], observer.GetOutput(1, 1));

        observer.Blackout(true);   // and once more
        link.Step(TimeSpan.FromMilliseconds(200));
        Assert.True(_desk.Blackout);
        Assert.False(observer.Ping().Blackout);
        Assert.Equal([0], observer.GetOutput(1, 1));
        Assert.Equal([false, true], _clients.BlackoutChanges);
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
        int reports = 0;
        var link = new BoxLink(_desk, box, _clients, "nowhere", NullLogger<BoxLink>.Instance, changed: () => reports++);

        link.Step(TimeSpan.Zero);
        link.Step(TimeSpan.FromSeconds(1));

        Assert.False(link.Status.Connected);
        Assert.Equal(0, reports);
    }

    // A UDP port of this PC that is free right now.
    private static int PortWhereNobodyListens()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
