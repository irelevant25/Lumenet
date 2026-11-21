using System.Net.Sockets;
using DmxBox.Server;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmxBox.Tests;

/// <summary>
/// The link between the desk and the box, with a pretend box. The tests do the link's rounds
/// themselves and name the time of each round, so nothing here waits for a real clock.
/// The last test alone starts the link's own thread.
/// </summary>
public sealed class BoxLinkTests
{
    private const string Host = "pretend-box";

    private readonly Desk _desk = new();
    private readonly FakeBox _box = new();
    private readonly RecordingClient _clients = new();
    private readonly BoxLink _link;

    /// <summary>Every test starts with a new desk, a new pretend box and a link that has done nothing yet.</summary>
    public BoxLinkTests()
    {
        _link = new BoxLink(_desk, _box, _clients, Host, NullLogger<BoxLink>.Instance);
    }

    // ---------------------------------------------------------------- connecting

    [Fact]
    public void BoxCountsAsNotConnectedUntilItAnswers()
    {
        Assert.Equal(new BoxStatus(Connected: false, Host, Firmware: null), _link.Status);
    }

    [Fact]
    public void ConnectingPushesTheDeskStateToTheBox()
    {
        _desk.SetChannel(1, 200);
        _desk.SetMaster(128);
        _box.MasterLevel = 77;   // left like this by some other program

        _link.Step(At(0));

        Assert.Equal(["ping", "master 255", "levels", "blackout off"], _box.Calls);
        Assert.Equal(255, _box.MasterLevel);
        Assert.Equal(100, _box.Levels[0]);   // the desk's master is in the frame: 200 x 128 / 255
        Assert.False(_box.BlackoutOn);
    }

    [Fact]
    public void ConnectingIsReportedWithTheFirmwareVersion()
    {
        _link.Step(At(0));

        var connected = new BoxStatus(Connected: true, Host, Firmware: "1.0");
        Assert.Equal(connected, _link.Status);
        Assert.Equal([connected], _clients.BoxChanges);
    }

    [Fact]
    public void ConnectingInBlackoutGivesTheBoxADarkLook()
    {
        _desk.SetChannel(1, 200);
        _desk.SetBlackout(true);

        _link.Step(At(0));

        // The blackout is in the output: the box's own blackout, that of its button, stays off.
        Assert.Equal(["ping", "master 255", "levels", "blackout off"], _box.Calls);
        Assert.False(_box.BlackoutOn);
        Assert.Equal(0, _box.Levels[0]);
    }

    [Fact]
    public void ConnectingOverrulesABlackoutTheBoxWasLeftIn()
    {
        _box.PressBlackoutButton();   // pressed while nobody was connected

        _link.Step(At(0));

        Assert.False(_box.BlackoutOn);
        Assert.False(_desk.Blackout);
        Assert.Empty(_clients.BlackoutChanges);
    }

    // ---------------------------------------------------------------- sending the output

    [Fact]
    public void ChangeOfTheDeskIsSent()
    {
        Connect();

        _desk.SetChannel(2, 50);
        _link.Step(At(25));

        Assert.Equal(["levels"], _box.Calls);
        Assert.Equal(50, _box.Levels[1]);
    }

    [Fact]
    public void UnchangedOutputIsNotSentAgain()
    {
        Connect();
        _desk.SetChannel(2, 50);
        _link.Step(At(25));
        _box.ForgetCalls();

        _link.Step(At(50));
        _link.Step(At(75));

        Assert.Empty(_box.Calls);
    }

    [Fact]
    public void ChangesBetweenTwoRoundsGoOutAsOneFrame()
    {
        Connect();

        _desk.SetChannel(1, 10);
        _desk.SetChannel(2, 20);
        _desk.SetMemoryLevel(1, 30);
        _link.Step(At(25));

        Assert.Equal(["levels"], _box.Calls);
        Assert.Equal([10, 20], _box.Levels[..2]);
    }

    [Fact]
    public void BoxIsPingedTwiceASecond()
    {
        Connect();

        for (int milliseconds = 25; milliseconds < 500; milliseconds += 25)
            _link.Step(At(milliseconds));
        Assert.Empty(_box.Calls);

        _link.Step(At(500));
        Assert.Equal(["ping"], _box.Calls);

        _link.Step(At(525));
        _link.Step(At(975));
        Assert.Equal(["ping"], _box.Calls);

        _link.Step(At(1000));
        Assert.Equal(["ping", "ping"], _box.Calls);
    }

    // ---------------------------------------------------------------- blackout

    [Fact]
    public void BlackoutFromTheUiGoesToTheBoxAsADarkLook()
    {
        _desk.SetChannel(1, 200);
        Connect();

        _desk.SetBlackout(true);
        _link.Step(At(25));

        Assert.Equal(["levels"], _box.Calls);
        Assert.Equal(0, _box.Levels[0]);
        Assert.False(_box.BlackoutOn);   // the box's own blackout is that of its button

        _desk.SetBlackout(false);
        _link.Step(At(50));

        Assert.Equal(["levels", "levels"], _box.Calls);
        Assert.Equal(200, _box.Levels[0]);
    }

    [Fact]
    public void BlackoutFromTheUiIsNotMistakenForThePressOfTheButton()
    {
        Connect();

        _desk.SetBlackout(true);
        _link.Step(At(500));   // a round in which the ping is due as well

        Assert.Equal(["levels", "ping"], _box.Calls);
        Assert.True(_desk.Blackout);
        Assert.Empty(_clients.BlackoutChanges);
    }

    [Fact]
    public void BlackoutSwitchedOffOnTheUiSwitchesOffThatOfTheButtonToo()
    {
        _desk.SetChannel(1, 200);
        Connect();
        _box.PressBlackoutButton();
        _link.Step(At(500));
        _box.ForgetCalls();

        _desk.SetBlackout(false);
        _link.Step(At(525));

        Assert.Equal(["blackout off", "levels"], _box.Calls);
        Assert.False(_box.BlackoutOn);
        Assert.Equal(200, _box.Levels[0]);
    }

    [Fact]
    public void BlackoutButtonOnTheBoxReachesTheDeskAndTheUis()
    {
        Connect();

        _box.PressBlackoutButton();
        _link.Step(At(500));

        Assert.True(_desk.Blackout);
        Assert.Equal([true], _clients.BlackoutChanges);
        Assert.Equal(["ping"], _box.Calls);   // the box has it already: nothing is sent back

        _box.PressBlackoutButton();
        _link.Step(At(1000));

        Assert.False(_desk.Blackout);
        Assert.Equal([true, false], _clients.BlackoutChanges);
    }

    [Fact]
    public void BlackoutTakenFromTheBoxIsNotSentBackToIt()
    {
        Connect();
        _box.PressBlackoutButton();
        _link.Step(At(500));
        _box.ForgetCalls();

        _link.Step(At(525));
        _link.Step(At(1000));

        // The desk's blackout goes out as a dark look; the box keeps its own blackout as it is.
        Assert.Equal(["levels", "ping"], _box.Calls);
        Assert.True(_box.BlackoutOn);
        Assert.Equal([true], _clients.BlackoutChanges);
    }

    // ---------------------------------------------------------------- a box that restarts

    [Fact]
    public void RestartOfTheBoxSendsEverythingAgain()
    {
        _desk.SetChannel(1, 200);
        Connect();

        _box.Restart();
        _link.Step(At(500));

        Assert.Equal(["ping", "master 255", "levels", "blackout off"], _box.Calls);
        Assert.Equal(200, _box.Levels[0]);
        Assert.True(_link.Status.Connected);
        Assert.Single(_clients.BoxChanges);   // it never counted as gone
    }

    [Fact]
    public void RestartInBlackoutIsNotMistakenForThePressOfTheButton()
    {
        _desk.SetChannel(1, 200);
        _desk.SetBlackout(true);
        Connect();

        _box.Restart();   // the box comes back without blackout
        _link.Step(At(500));

        Assert.Equal(["ping", "master 255", "levels", "blackout off"], _box.Calls);
        Assert.Equal(0, _box.Levels[0]);
        Assert.True(_desk.Blackout);
        Assert.Empty(_clients.BlackoutChanges);
    }

    // ---------------------------------------------------------------- a box that goes away

    [Fact]
    public void MissingBoxIsLookedForOnceASecond()
    {
        _box.Unplug();

        _link.Step(At(0));
        Assert.Equal(["ping"], _box.Calls);

        for (int milliseconds = 25; milliseconds < 1000; milliseconds += 25)
            _link.Step(At(milliseconds));
        Assert.Equal(["ping"], _box.Calls);

        _link.Step(At(1000));
        Assert.Equal(["ping", "ping"], _box.Calls);

        Assert.False(_link.Status.Connected);
        Assert.Empty(_clients.BoxChanges);   // it was never there, so nothing changed
    }

    [Fact]
    public void BoxThatStopsAnsweringCountsAsNotConnected()
    {
        Connect();

        _box.Unplug();
        _desk.SetChannel(1, 10);
        _link.Step(At(25));

        var gone = new BoxStatus(Connected: false, Host, Firmware: null);
        Assert.Equal(gone, _link.Status);
        Assert.Equal(gone, _clients.BoxChanges[^1]);
        Assert.Equal(2, _clients.BoxChanges.Count);
    }

    [Fact]
    public void BoxThatStopsAnsweringIsNoticedByThePingToo()
    {
        Connect();

        _box.Unplug();        // and nothing changes on the desk
        _link.Step(At(475));
        Assert.True(_link.Status.Connected);

        _link.Step(At(500));
        Assert.False(_link.Status.Connected);
    }

    [Fact]
    public void LostBoxIsLookedForOnceASecond()
    {
        Connect();
        _box.Unplug();
        _link.Step(At(500));   // the ping that finds it gone
        _box.ForgetCalls();

        _link.Step(At(525));
        _link.Step(At(1475));
        Assert.Empty(_box.Calls);

        _link.Step(At(1500));
        Assert.Equal(["ping"], _box.Calls);
    }

    [Fact]
    public void BoxThatComesBackIsConnectedAgainAndGetsTheDeskState()
    {
        Connect();
        _box.Unplug();
        _link.Step(At(500));
        _desk.SetChannel(3, 99);   // changed while the box was away
        _box.ForgetCalls();

        _box.PlugIn();
        _link.Step(At(1500));

        Assert.Equal(["ping", "master 255", "levels", "blackout off"], _box.Calls);
        Assert.Equal(99, _box.Levels[2]);
        Assert.True(_link.Status.Connected);
        Assert.Equal([true, false, true], _clients.BoxChanges.Select(status => status.Connected));
    }

    [Fact]
    public void AnyFailureOfTheBoxMeansNotConnectedAndNeverAnException()
    {
        Exception[] failures =
        [
            new DmxBoxException(2),                               // the box answered BAD_ARG
            new SocketException((int)SocketError.NetworkUnreachable),
            new InvalidOperationException("a fault in the program"),
        ];

        int milliseconds = 0;
        foreach (Exception failure in failures)
        {
            _box.PlugIn();
            _link.Step(At(milliseconds += 1000));
            Assert.True(_link.Status.Connected);

            _box.FailWith(failure);
            _link.Step(At(milliseconds += 500));
            Assert.False(_link.Status.Connected);
        }
    }

    [Fact]
    public void FailureWhileConnectingLeavesTheBoxNotConnected()
    {
        _box.FailWith(new InvalidOperationException("a fault in the program"));

        _link.Step(At(0));

        Assert.False(_link.Status.Connected);
        Assert.Empty(_clients.BoxChanges);
    }

    // ---------------------------------------------------------------- the link's own thread

    [Fact]
    public async Task OwnThreadConnectsSendsChangesAndStops()
    {
        _desk.SetChannel(1, 200);

        await _link.StartAsync(CancellationToken.None);
        await Wait.UntilAsync(() => _link.Status.Connected, "The link did not connect to the pretend box.");
        Assert.Equal(200, _box.Levels[0]);

        _desk.SetChannel(1, 100);
        await Wait.UntilAsync(() => _box.Levels[0] == 100, "The link did not send the change.");

        _box.Unplug();
        await Wait.UntilAsync(() => !_link.Status.Connected, "The link did not notice the box going away.");

        await _link.StopAsync(CancellationToken.None);
        _link.Dispose();
        Assert.True(_box.Disposed);
    }

    private static TimeSpan At(int milliseconds)
    {
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    // The first round finds the box; after it the test starts with an empty list of calls.
    private void Connect()
    {
        _link.Step(At(0));
        Assert.True(_link.Status.Connected);
        _box.ForgetCalls();
    }
}
