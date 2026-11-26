namespace DmxBox.Tests.Osc;

/// <summary>
/// What OSC messages do to the desk's keys and faders, with a pretend desk: the addresses, keys pressed
/// and let go, faders from 0 to 1 and from 0 to 255, the slots, and what is refused or not there.
/// </summary>
public sealed class OscInputTests
{
    private readonly PretendMidiDesk _desk = new();
    private readonly List<(string Key, bool Down)> _keys = [];
    private readonly OscInput _osc;
    private bool _page = true;

    public OscInputTests()
    {
        var mapper = new MidiMapper(_desk, (_, _) => true);
        _osc = new OscInput(_desk, PressKey, mapper.Resolve);
    }

    private bool PressKey(string key, bool down)
    {
        if (!_page)
            return false;
        _keys.Add((key, down));
        return true;
    }

    private MidiOutcome Send(string address, params object?[] arguments) => _osc.Handle(OscPacket.Read(OscPacket.Write(address, arguments)).Single());

    [Fact]
    public void AnAddressNamesAKeyOrAFaderWithOrWithoutLumenetBeforeIt()
    {
        Assert.Equal("stack.play", OscInput.TargetOf("/lumenet/stack/play"));
        Assert.Equal("stack.play", OscInput.TargetOf("/stack/play"));
        Assert.Equal("stack.play", OscInput.TargetOf("/Lumenet/Stack/Play"));
        Assert.Equal("fader.channel.12", OscInput.TargetOf("/lumenet/fader/channel/12"));
        Assert.Equal("fader.slot.memory.3", OscInput.TargetOf("/lumenet/fader/slot/memory/3"));
        Assert.Equal("master.solo-down", OscInput.TargetOf("/lumenet/master/solo-down"));

        Assert.Null(OscInput.TargetOf("/lumenet"));
        Assert.Null(OscInput.TargetOf("/lumenet/fader/nothing"));
        Assert.Null(OscInput.TargetOf("/lumenet//play"));
        Assert.Null(OscInput.TargetOf("lumenet/stack/play"));
        Assert.Null(OscInput.TargetOf("/lumenet/fader/channel/5000"));

        Assert.Equal("/lumenet/fader/channel/12", OscInput.AddressOf("fader.channel.12"));
        Assert.Equal("/lumenet/stack/play", OscInput.AddressOf("stack.play"));
    }

    [Fact]
    public void AMessageWithoutArgumentPressesAKeyAndLetsItGo()
    {
        Assert.Equal(new MidiOutcome("stack.play", "down"), Send("/lumenet/stack/play"));
        Assert.Equal(new MidiOutcome("stack.play", "down"), Send("/lumenet/stack/play", OscMessage.Impulse));
        Assert.Equal(new MidiOutcome("stack.play", "down"), Send("/stack/play", "go"));   // a text is no number: pressed and let go
        Assert.Equal([("stack.play", true), ("stack.play", false), ("stack.play", true), ("stack.play", false), ("stack.play", true), ("stack.play", false)], _keys);
    }

    [Fact]
    public void OnePressesAKeyAndZeroLetsItGoAsTheKeysOfTouchOscSendThem()
    {
        Assert.Equal("down", Send("/lumenet/master/solo", 1f).Result);
        Assert.Equal("down", Send("/lumenet/master/solo", 1f).Result);   // held down already: once is enough
        Assert.Equal([("master.solo", true)], _keys);

        Assert.Equal("up", Send("/lumenet/master/solo", 0f).Result);
        Assert.Equal("up", Send("/lumenet/master/solo", 0f).Result);     // not held: nothing to let go
        Assert.Equal([("master.solo", true), ("master.solo", false)], _keys);

        Send("/lumenet/memory/5", 1);
        Send("/lumenet/memory/5", false);
        Send("/lumenet/memory/6", true);
        Send("/lumenet/memory/6", 0);
        Assert.Equal([("memory.5", true), ("memory.5", false), ("memory.6", true), ("memory.6", false)], _keys.Skip(2));
    }

    [Fact]
    public void WithoutAPageOnThePcAKeyDoesNothing()
    {
        _page = false;
        Assert.Equal(new MidiOutcome("stack.play", "no-page"), Send("/lumenet/stack/play"));
        Assert.Equal(new MidiOutcome("stack.play", "no-page"), Send("/lumenet/stack/play", 1));
        Assert.Empty(_keys);

        // the press did not take: a page that comes now is pressed afresh
        _page = true;
        Assert.Equal("down", Send("/lumenet/stack/play", 1).Result);
        Assert.Equal([("stack.play", true)], _keys);
    }

    [Fact]
    public void AFaderTakesANumberWithAPointFromZeroToOneOrAWholeNumberTo255()
    {
        Assert.Equal(new MidiOutcome("fader.master", "moved", 128), Send("/lumenet/fader/master", 0.5f));
        Assert.Equal(new MidiOutcome("fader.channel.12", "moved", 200), Send("/lumenet/fader/channel/12", 200));
        Assert.Equal(new MidiOutcome("fader.channel.12", "moved", 255), Send("/lumenet/fader/channel/12", 300));
        Assert.Equal(new MidiOutcome("fader.channel.12", "moved", 0), Send("/lumenet/fader/channel/12", -1.5));
        Assert.Equal(new MidiOutcome("fader.memory.3", "moved", 255), Send("/lumenet/fader/memory/3", 1.0));
        Assert.Equal(new MidiOutcome("fader.memory.3", "moved", 0), Send("/lumenet/fader/memory/3", false));
        Assert.Equal(
            [(new DeskFader(FaderKind.Master), 128), (new DeskFader(FaderKind.Channel, 12), 200), (new DeskFader(FaderKind.Channel, 12), 255),
             (new DeskFader(FaderKind.Channel, 12), 0), (new DeskFader(FaderKind.Memory, 3), 255), (new DeskFader(FaderKind.Memory, 3), 0)],
            _desk.Moves);

        MidiOutcome said = Send("/lumenet/fader/master", "half");
        Assert.Equal("refused", said.Result);
        Assert.Equal("A fader needs a number: 0 to 1 with a point, or 0 to 255.", said.Reason);
        Assert.Equal("refused", Send("/lumenet/fader/master").Result);   // no number at all
        Assert.Equal(6, _desk.Moves.Count);
    }

    [Fact]
    public void ASlotFollowsThePageOfFadersThePcShows()
    {
        _desk.Pages = MidiPages.First with { FirstChannel = 33 };
        Assert.Equal("moved", Send("/lumenet/fader/slot/channel/2", 1f).Result);
        Assert.Equal((new DeskFader(FaderKind.Channel, 34), 255), Assert.Single(_desk.Moves));

        // a slot beyond the page: no fader
        Assert.Equal(new MidiOutcome("fader.slot.channel.40", "missing"), Send("/lumenet/fader/slot/channel/40", 1f));
    }

    [Fact]
    public void AFaderTheDeskRefusesOrThatIsNotThereIsSaidSo()
    {
        _desk.Missing.Add(new DeskFader(FaderKind.Effect, 9));
        Assert.Equal(new MidiOutcome("fader.effect.9", "missing"), Send("/lumenet/fader/effect/9", 1f));

        _desk.Refusing = "The stack is live: the speed of a sequence waits.";
        Assert.Equal(
            new MidiOutcome("fader.sequence.2", "refused", 255, Reason: "The stack is live: the speed of a sequence waits."),
            Send("/lumenet/fader/sequence/2", 1f));
        Assert.Empty(_desk.Moves);
    }

    [Fact]
    public void AnAddressThatCannotNameAKeyOrAFaderIsUnmapped()
    {
        Assert.Equal(new MidiOutcome(null, "unmapped"), Send("/lumenet/fader/nothing", 1f));
        Assert.Equal(new MidiOutcome(null, "unmapped"), Send("/lumenet/stack play"));
        Assert.Equal(new MidiOutcome(null, "unmapped"), Send("/lumenet/9lives"));
        Assert.Empty(_keys);

        // A name like a key's goes to the page, as the names of MIDI do: the keys are the page's own,
        // and it passes over one it does not have (its list of messages says so).
        Assert.Equal(new MidiOutcome("stack.ply", "down"), Send("/lumenet/stack/ply"));
    }

    [Fact]
    public void LetGoLetsGoOfEveryKeyOscHoldsDown()
    {
        Send("/lumenet/master/solo", 1);
        Send("/lumenet/memory/2", 1);
        _osc.LetGo();
        Assert.Equal([("master.solo", true), ("memory.2", true), ("master.solo", false), ("memory.2", false)], _keys);

        _osc.LetGo();   // none held any more
        Assert.Equal(4, _keys.Count);
    }
}
