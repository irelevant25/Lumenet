using System.Net.Sockets;
using DmxBox.Server;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmxBox.Tests;

/// <summary>
/// The link between the desk and a box, with a pretend box. The tests do the link's rounds
/// themselves and name the time of each round, so nothing here waits for a real clock.
/// The last test alone runs the link on a thread of its own.
/// </summary>
public sealed class BoxLinkTests
{
    private const string Host = "pretend-box";

    private readonly Desk _desk = new();
    private readonly FakeBox _box = new();
    private readonly RecordingClient _clients = new();
    private readonly BoxLink _link;
    private readonly Lock _reportsLock = new();
    private readonly List<BoxStatus> _reports = [];

    /// <summary>Every test starts with a new desk, a new pretend box and a link that has done nothing yet.</summary>
    public BoxLinkTests()
    {
        _link = new BoxLink(_desk, _box, _clients, Host, NullLogger<BoxLink>.Instance, changed: NoteReport);
    }

    // What the link reported, in order: the box came, or went.
    private BoxStatus[] Reports
    {
        get
        {
            lock (_reportsLock)
            {
                return [.. _reports];
            }
        }
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
        Assert.Equal([connected], Reports);
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
    public void BoxIsPingedTenTimesASecond()
    {
        Connect();

        for (int milliseconds = 25; milliseconds < 100; milliseconds += 25)
            _link.Step(At(milliseconds));
        Assert.Empty(_box.Calls);

        _link.Step(At(100));
        Assert.Equal(["ping"], _box.Calls);

        _link.Step(At(125));
        _link.Step(At(175));
        Assert.Equal(["ping"], _box.Calls);

        _link.Step(At(200));
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
        _link.Step(At(100));   // a round in which the ping is due as well

        Assert.Equal(["levels", "ping"], _box.Calls);
        Assert.True(_desk.Blackout);
        Assert.Empty(_clients.BlackoutChanges);
    }

    [Fact]
    public void BlackoutButtonOnTheBoxSwitchesTheDesksBlackoutForTheDeskAndTheUis()
    {
        _desk.SetChannel(1, 200);
        Connect();

        _box.PressBlackoutButton();
        _link.Step(At(100));

        Assert.True(_desk.Blackout);
        Assert.Equal([true], _clients.BlackoutChanges);

        _box.PressBlackoutButton();
        _link.Step(At(200));

        Assert.False(_desk.Blackout);
        Assert.Equal([true, false], _clients.BlackoutChanges);
        Assert.Equal(200, _box.Levels[0]);
    }

    [Fact]
    public void BoxLetsGoOfItsOwnBlackoutOnceItHasTheDesksOutput()
    {
        _desk.SetChannel(1, 200);
        Connect();

        _box.PressBlackoutButton();   // the box goes dark by itself, everything
        _link.Step(At(100));

        // First the dark look of the desk, then the box's own blackout off: the light never flashes.
        Assert.Equal(["ping", "levels", "blackout off"], _box.Calls);
        Assert.False(_box.BlackoutOn);
        Assert.Equal(0, _box.Levels[0]);
    }

    [Fact]
    public void BlackoutButtonLeavesTheHeadWhereItIsAsTheDesksBlackoutDoes()
    {
        _desk.SetStage(new StagePlan(16, 10, [new StageLight(1, "Spot", 1, "PTD", null, 4, 4, 0)]));
        _desk.SetChannel(1, 100);   // pan
        _desk.SetChannel(2, 50);    // tilt
        _desk.SetChannel(3, 255);   // dimmer
        Connect();

        _box.PressBlackoutButton();
        _link.Step(At(100));

        Assert.False(_box.BlackoutOn);
        Assert.Equal([100, 50, 0], _box.Levels[..3]);
    }

    [Fact]
    public void BlackoutButtonPressedInTheDesksBlackoutEndsIt()
    {
        _desk.SetChannel(1, 200);
        _desk.SetBlackout(true);   // on a UI
        Connect();

        _box.PressBlackoutButton();
        _link.Step(At(100));

        Assert.False(_desk.Blackout);
        Assert.Equal([false], _clients.BlackoutChanges);
        Assert.Equal(["ping", "levels", "blackout off"], _box.Calls);
        Assert.False(_box.BlackoutOn);
        Assert.Equal(200, _box.Levels[0]);
    }

    [Fact]
    public void BlackoutButtonPressedTwiceBetweenTwoPingsChangesNothing()
    {
        Connect();

        _box.PressBlackoutButton();
        _box.PressBlackoutButton();
        _link.Step(At(100));

        Assert.False(_desk.Blackout);
        Assert.Empty(_clients.BlackoutChanges);
        Assert.Equal(["ping"], _box.Calls);
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
        Assert.Single(Reports);   // it never counted as gone
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
        Assert.Empty(Reports);   // it was never there, so nothing changed
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
        Assert.Equal(gone, Reports[^1]);
        Assert.Equal(2, Reports.Length);
    }

    [Fact]
    public void BoxThatStopsAnsweringIsNoticedByThePingToo()
    {
        Connect();

        _box.Unplug();        // and nothing changes on the desk
        _link.Step(At(75));
        Assert.True(_link.Status.Connected);

        _link.Step(At(100));
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
        Assert.Equal([true, false, true], Reports.Select(status => status.Connected));
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
        Assert.Empty(Reports);
    }

    // ---------------------------------------------------------------- outputs and universes

    [Fact]
    public void BoxWithTwoOutputsGetsAUniverseOnEach()
    {
        var box = new FakeBox(outputs: 2);
        var link = new BoxLink(_desk, box, _clients, Host, NullLogger<BoxLink>.Instance);
        _desk.SetUniverses(2);
        _desk.SetChannel(1, 10);
        _desk.SetChannel(513, 20);

        link.Step(At(0));

        Assert.Equal(2, link.Status.Outputs);
        byte[] levels = box.Levels;
        Assert.Equal(1024, levels.Length);
        Assert.Equal(10, levels[0]);
        Assert.Equal(20, levels[512]);
    }

    [Fact]
    public void OutputBeyondTheUniversesOfTheProjectStaysDark()
    {
        var box = new FakeBox(outputs: 2);
        var link = new BoxLink(_desk, box, _clients, Host, NullLogger<BoxLink>.Instance);
        _desk.SetChannel(1, 10);   // the desk has one universe

        link.Step(At(0));

        Assert.Equal(10, box.Levels[0]);
        Assert.All(box.Levels[512..], level => Assert.Equal(0, level));
    }

    [Fact]
    public void OutputsPlayTheUniversesOfTheWiringAndGetThemAgainWhenItChanges()
    {
        int[] plays = [2, 0];
        var box = new FakeBox(outputs: 2);
        var link = new BoxLink(_desk, box, _clients, Host, NullLogger<BoxLink>.Instance, () => plays);
        _desk.SetUniverses(2);
        _desk.SetChannel(1, 10);
        _desk.SetChannel(513, 20);

        link.Step(At(0));
        Assert.Equal(20, box.Levels[0]);
        Assert.Equal(0, box.Levels[512]);

        // Nothing changed on the desk, only the wiring.
        plays = [1, 2];
        link.ResendSoon();
        box.ForgetCalls();
        link.Step(At(25));

        Assert.Equal(["levels"], box.Calls);
        Assert.Equal(10, box.Levels[0]);
        Assert.Equal(20, box.Levels[512]);
    }

    // ---------------------------------------------------------------- the link's own thread

    [Fact]
    public async Task OwnThreadConnectsSendsChangesAndStops()
    {
        _desk.SetChannel(1, 200);
        using var stop = new CancellationTokenSource();
        var thread = new Thread(() => _link.Run(stop.Token));

        thread.Start();
        try
        {
            await Wait.UntilAsync(() => _link.Status.Connected, "The link did not connect to the pretend box.");
            Assert.Equal(200, _box.Levels[0]);

            _desk.SetChannel(1, 100);
            await Wait.UntilAsync(() => _box.Levels[0] == 100, "The link did not send the change.");

            _box.Unplug();
            await Wait.UntilAsync(() => !_link.Status.Connected, "The link did not notice the box going away.");
        }
        finally
        {
            // also when a wait failed: a thread left running would wait on the stop after it is gone,
            // which ends the whole test run
            stop.Cancel();
            Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The link's thread did not stop.");
        }

        _link.Dispose();
        Assert.True(_box.Disposed);
    }

    private static TimeSpan At(int milliseconds)
    {
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private void NoteReport()
    {
        lock (_reportsLock)
        {
            _reports.Add(_link.Status);
        }
    }

    // The first round finds the box; after it the test starts with an empty list of calls.
    private void Connect()
    {
        _link.Step(At(0));
        Assert.True(_link.Status.Connected);
        _box.ForgetCalls();
    }
}
