using Microsoft.Extensions.Logging.Abstractions;

namespace DmxBox.Tests.Outputs;

/// <summary>
/// The link to a DMX USB interface, with a pretend interface. The tests do the link's rounds
/// themselves and name the time of each, so nothing here waits for a real clock.
/// </summary>
public sealed class UsbLinkTests : IDisposable
{
    private readonly Desk _desk = new();
    private readonly FakeUsbDevice _device = new();
    private readonly List<(string State, string Detail)> _reports = [];
    private readonly UsbLink _link;

    public UsbLinkTests()
    {
        _link = new UsbLink(
            _desk, _device.Name, universe: 1, _device.PortAt, _device.Names,
            (state, detail) => _reports.Add((state, detail)), NullLogger.Instance);
    }

    private (string State, string Detail) State => _reports[^1];

    public void Dispose()
    {
        _link.Dispose();
    }

    [Fact]
    public void OpeningAsksForTheSerialNumberBeforeTheFirstFrame()
    {
        _desk.SetChannel(1, 200);
        _desk.SetMaster(128);

        _link.Step(At(0));

        // The serial number stops the interface's DMX: it comes first, then the frame.
        Assert.Equal([EnttecPro.SerialNumberLabel, EnttecPro.ParametersLabel, EnttecPro.SendDmxLabel], _device.Labels);
        Assert.Equal(512, _device.Frame!.Length);
        Assert.Equal(100, _device.Frame[0]);   // the desk's master is in the frame, as for a box
    }

    [Fact]
    public void InterfaceThatAnswersIsConnectedWithItsSerialNumberAndFirmware()
    {
        _link.Step(At(0));
        _link.Step(At(25));

        Assert.Equal((OutputView.Connected, "Serial number 01234567, firmware 1.44"), State);
    }

    [Fact]
    public void ChangeOfTheDeskIsSentAndAnUnchangedFrameOnceASecond()
    {
        _link.Step(At(0));
        _device.Forget();

        _desk.SetChannel(2, 50);
        _link.Step(At(25));
        Assert.Equal([EnttecPro.SendDmxLabel], _device.Labels);
        Assert.Equal(50, _device.Frame![1]);

        _link.Step(At(50));
        _link.Step(At(1000));
        Assert.Single(_device.Labels);

        _link.Step(At(1025));
        Assert.Equal([EnttecPro.SendDmxLabel, EnttecPro.SendDmxLabel], _device.Labels);
    }

    [Fact]
    public void InterfaceIsAskedForItsParametersEveryTwoSeconds()
    {
        _link.Step(At(0));
        _device.Forget();

        _link.Step(At(1975));
        Assert.DoesNotContain(EnttecPro.ParametersLabel, _device.Labels);

        _link.Step(At(2000));
        Assert.Contains(EnttecPro.ParametersLabel, _device.Labels);
        Assert.DoesNotContain(EnttecPro.SerialNumberLabel, _device.Labels);   // that one would stop its DMX
    }

    [Fact]
    public void AnotherUniverseIsSentAtOnceAndNoneMakesTheCableDark()
    {
        _desk.SetUniverses(2);
        _desk.SetChannel(1, 10);
        _desk.SetChannel(513, 20);
        _link.Step(At(0));
        Assert.Equal(10, _device.Frame![0]);

        _link.Universe = 2;
        _link.Step(At(25));
        Assert.Equal(20, _device.Frame![0]);

        _link.Universe = 0;
        _link.Step(At(50));
        Assert.All(_device.Frame!, level => Assert.Equal(0, level));
    }

    [Fact]
    public void InterfaceThatIsNotPluggedInIsMissingAndLookedForOnceASecond()
    {
        _device.PullOut();

        _link.Step(At(0));
        Assert.Equal((OutputView.Missing, "COM7 is not there: is the interface plugged in?"), State);

        _device.PlugIn();
        _link.Step(At(975));
        Assert.Equal(0, _device.Opens);

        _link.Step(At(1000));
        Assert.Equal(1, _device.Opens);
    }

    [Fact]
    public void InterfacePulledOutIsMissingAndOpensAgainWhenItIsBack()
    {
        _desk.SetChannel(1, 99);
        _link.Step(At(0));
        _link.Step(At(25));
        Assert.Equal(OutputView.Connected, State.State);

        _device.PullOut();
        _desk.SetChannel(1, 100);   // the next frame fails
        _link.Step(At(50));
        Assert.Equal(OutputView.Missing, State.State);
        Assert.False(_device.IsOpen);

        _device.PlugIn();
        _link.Step(At(1050));
        _link.Step(At(1075));

        Assert.Equal(2, _device.Opens);
        Assert.Equal(OutputView.Connected, State.State);
        Assert.Equal(100, _device.Frame![0]);   // it gets the desk's look at once
    }

    [Fact]
    public void InterfacePulledOutIsNoticedWithoutAFrameToSend()
    {
        _link.Step(At(0));
        _link.Step(At(25));

        _device.PullOut();   // and nothing changes on the desk: the list of ports tells
        _link.Step(At(1000));

        Assert.Equal(OutputView.Missing, State.State);
        Assert.False(_device.IsOpen);
    }

    [Fact]
    public void PortHeldByAnotherProgramIsMissingWithTheReason()
    {
        _device.Hold(true);

        _link.Step(At(0));

        Assert.Equal((OutputView.Missing, "COM7 is in use by another program."), State);

        _device.Hold(false);
        _link.Step(At(1000));
        Assert.True(_device.IsOpen);
    }

    [Fact]
    public void InterfaceThatDoesNotAnswerIsMissingButGetsTheFrames()
    {
        _device.Answer(false);
        _desk.SetChannel(1, 77);

        _link.Step(At(0));
        _link.Step(At(975));
        Assert.Equal(OutputView.Missing, State.State);
        Assert.Equal("COM7 is open; waiting for its answer.", State.Detail);

        _link.Step(At(1000));
        Assert.Equal((OutputView.Missing, "COM7 does not answer as a DMX USB Pro does. The frames go to it anyway."), State);
        Assert.Equal(77, _device.Frame![0]);
    }

    [Fact]
    public void InterfaceThatStopsAnsweringIsMissingAfterFiveSeconds()
    {
        _link.Step(At(0));
        _link.Step(At(25));
        _device.Answer(false);

        _link.Step(At(4000));
        Assert.Equal(OutputView.Connected, State.State);   // its last answer, at 25 ms, still counts

        _link.Step(At(5100));
        Assert.Equal(OutputView.Missing, State.State);
    }

    [Fact]
    public async Task OwnThreadOpensSendsAndStops()
    {
        _desk.SetChannel(1, 123);
        using var stop = new CancellationTokenSource();
        var thread = new Thread(() => _link.Run(stop.Token));

        thread.Start();
        await Wait.UntilAsync(() => _device.Frame?[0] == 123, "The link did not send the frame.");

        stop.Cancel();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The link's thread did not stop.");
        _link.Dispose();
        Assert.False(_device.IsOpen);
    }

    private static TimeSpan At(int milliseconds)
    {
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
