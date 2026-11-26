namespace DmxBox.Tests.Core;

/// <summary>
/// Solo on the desk: keys held with SOLO UP let only their faders shine, at full; keys held with
/// SOLO DOWN take only their faders away. The faders themselves stay where they are.
/// </summary>
public sealed class SoloTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;

    public SoloTests()
    {
        _desk = new Desk(_time);

        // memory 2: a look with channel 5 at 200 and channel 6 at 100, its fader up
        _desk.SetChannel(5, 200);
        _desk.SetChannel(6, 100);
        _desk.SaveMemory(2);
        _desk.SetMemoryLevel(2, 255);

        // the channel faders: 1 at 100, 2 at 50, 5 at 30 (memory 2 holds it higher)
        _desk.SetChannel(1, 100);
        _desk.SetChannel(2, 50);
        _desk.SetChannel(5, 30);
        _desk.SetChannel(6, 0);
    }

    [Fact]
    public void SoloUpLetsOnlyTheHeldFadersShineAtFull()
    {
        Assert.Equal([100, 50, 0, 0, 200, 100], _desk.GetOutput()[..6]);

        Assert.True(_desk.HoldSolo("ui", up: true, "channel", 2, held: true));
        Assert.Equal([0, 255, 0, 0, 0, 0], _desk.GetOutput()[..6]); // channel 2 at full, every other light dark

        // A memory held too: its look at full.
        _desk.HoldSolo("ui", up: true, "memory", 2, held: true);
        Assert.Equal([0, 255, 0, 0, 200, 100], _desk.GetOutput()[..6]);

        // The master still dims it; the faders did not move.
        _desk.SetMaster(128);
        Assert.Equal(128, _desk.GetOutput()[1]);
        Assert.Equal(50, _desk.GetState().Channels[1]);

        // Let go: everything as before.
        _desk.HoldSolo("ui", up: true, "channel", 2, held: false);
        _desk.HoldSolo("ui", up: true, "memory", 2, held: false);
        _desk.SetMaster(255);
        Assert.Equal([100, 50, 0, 0, 200, 100], _desk.GetOutput()[..6]);
    }

    [Fact]
    public void SoloDownTakesOnlyTheHeldFaderAway()
    {
        // Channel 5: its light goes dark, also where the memory holds it up; the rest stays.
        _desk.HoldSolo("ui", up: false, "channel", 5, held: true);
        Assert.Equal([100, 50, 0, 0, 0, 100], _desk.GetOutput()[..6]);
        _desk.HoldSolo("ui", up: false, "channel", 5, held: false);

        // Memory 2: what it gives goes; the channel faders stay.
        _desk.HoldSolo("ui", up: false, "memory", 2, held: true);
        Assert.Equal([100, 50, 0, 0, 30, 0], _desk.GetOutput()[..6]);
    }

    [Fact]
    public void ASequenceHeldUpPlaysAtFullAndOneHeldDownGivesNothing()
    {
        _desk.SetSequence(3, loop: true, [new Step("channel", 8, 200, 0, 1, 0)]);
        _desk.SetMemoryLevel(3, 128);
        _time.Pass(0.1);
        Assert.Equal(100, _desk.GetOutput()[7]);

        _desk.HoldSolo("ui", up: true, "memory", 3, held: true);
        Assert.Equal(200, _desk.GetOutput()[7]);
        Assert.Equal(0, _desk.GetOutput()[0]);
        _desk.HoldSolo("ui", up: true, "memory", 3, held: false);

        _desk.HoldSolo("ui", up: false, "memory", 3, held: true);
        Assert.Equal(0, _desk.GetOutput()[7]);
        Assert.Equal(100, _desk.GetOutput()[0]);
    }

    [Fact]
    public void TheKeysOfAUiThatGoesAreLetGoAndEveryUiSeesWhatIsHeld()
    {
        long changes = _desk.OutputChanges;
        _desk.HoldSolo("phone", up: true, "channel", 2, held: true);
        _desk.HoldSolo("pc", up: false, "memory", 2, held: true);
        Assert.True(_desk.OutputChanges > changes);
        Assert.Equal([new SoloKey("channel", 2)], _desk.Solo.Up);
        Assert.Equal([new SoloKey("memory", 2)], _desk.Solo.Down);

        Assert.True(_desk.DropSolo("phone"));
        Assert.False(_desk.DropSolo("phone"));
        Assert.Empty(_desk.Solo.Up);
        Assert.Equal([new SoloKey("memory", 2)], _desk.GetState().Solo.Down);

        Assert.False(_desk.HoldSolo("pc", up: false, "channel", 9, held: false)); // not held: nothing changes
        Assert.StartsWith(
            "The memory must be from 1 to 128, not 0.",
            Assert.Throws<ArgumentOutOfRangeException>(() => _desk.HoldSolo("pc", up: true, "memory", 0, held: true)).Message);
    }
}
