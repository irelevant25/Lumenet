using System.Net;
using DmxBox.Server;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmxBox.Tests;

/// <summary>
/// The boxes all together, with pretend boxes: which of them the desk looks for, and which
/// universe every output plays. The links run on their own threads here, so the tests wait for
/// what they expect.
/// </summary>
public sealed class BoxNetworkTests : IDisposable
{
    private const string PicoBox = "192.168.7.1";

    private readonly Desk _desk = new();
    private readonly RecordingClient _clients = new();
    private readonly PretendPc _pc = new();
    private readonly FakeBox _pico = new();
    private readonly Dictionary<string, FakeBox> _boxes = [];
    private readonly List<string> _asked = [];
    private BoxNetwork? _network;

    /// <summary>Stops every link of the test's network.</summary>
    public void Dispose()
    {
        _network?.Dispose();
        _pc.Dispose();
    }

    [Theory]
    [InlineData("10.1.3.2", "10.1.3.1")]
    [InlineData("10.1.63.20", "10.1.63.1")]
    [InlineData("::ffff:10.1.7.2", "10.1.7.1")]
    [InlineData("10.1.3.1", null)]          // that would be the box itself
    [InlineData("10.2.3.2", null)]
    [InlineData("192.168.7.2", null)]       // the box with the Pico is in the settings
    [InlineData("10.20.30.40", null)]
    public void BoxBesideAnAddressOfThePcIsTheFirstOfItsNetwork(string pcAddress, string? box)
    {
        Assert.Equal(box, BoxNetwork.BoxBeside(IPAddress.Parse(pcAddress)));
    }

    [Fact]
    public async Task BoxesPlayTheUniversesInTheirOrderWhileTheProjectListsNone()
    {
        FakeBox second = Plug("10.1.3.1", outputs: 2);
        _pc.Have(IPAddress.Parse("10.1.3.2"), PretendPc.HomeAddress);
        _desk.SetUniverses(3);
        _desk.SetChannel(1, 11);
        _desk.SetChannel(513, 22);
        _desk.SetChannel(1025, 33);

        BoxNetwork network = Start();

        await Wait.UntilAsync(() => network.Boxes.Count(box => box.Connected) == 2, "The two boxes did not connect.");
        Assert.Equal(
            [
                new BoxView(PicoBox, true, "1.0", 1, [1], BoxWiring.Automatic),
                new BoxView("10.1.3.1", true, "1.0", 2, [2, 3], BoxWiring.Automatic),
            ],
            network.Boxes,
            SameView);
        await Wait.UntilAsync(
            () => _pico.Levels[0] == 11 && second.Levels[0] == 22 && second.Levels[512] == 33,
            "The boxes did not get the levels of their universes.");
    }

    [Fact]
    public async Task OutputsBeyondTheUniversesOfTheProjectPlayNoneUntilThereAreMore()
    {
        FakeBox second = Plug("10.1.3.1", outputs: 2);
        _pc.Have(IPAddress.Parse("10.1.3.2"));
        _desk.SetChannel(1, 11);

        BoxNetwork network = Start();
        await Wait.UntilAsync(() => network.Boxes.Count(box => box.Connected) == 2, "The two boxes did not connect.");
        Assert.Equal([0, 0], network.Boxes[1].Universes);

        _desk.SetUniverses(2);
        _desk.SetChannel(513, 22);
        network.Refresh();

        Assert.Equal([2, 0], network.Boxes[1].Universes);
        await Wait.UntilAsync(() => second.Levels[0] == 22, "The box did not play the new universe.");
    }

    [Fact]
    public async Task BoxesOfTheProjectPlayAsItsListSaysAndOthersPlayNothing()
    {
        FakeBox second = Plug("10.1.3.1", outputs: 2);
        _pc.Have(IPAddress.Parse("10.1.3.2"));
        _desk.SetUniverses(2);
        _desk.SetChannel(1, 11);
        _desk.SetChannel(513, 22);
        _desk.SetBoxes([new BoxPlan("10.1.3.1", [2, 1]), new BoxPlan("10.1.9.1", [1])]);

        BoxNetwork network = Start();

        await Wait.UntilAsync(() => network.Boxes.Count(box => box.Connected) == 2, "The two boxes did not connect.");
        Assert.Equal(
            [
                new BoxView("10.1.3.1", true, "1.0", 2, [2, 1], BoxWiring.Listed),
                new BoxView("10.1.9.1", false, null, 1, [1], BoxWiring.Listed),   // listed, but it does not answer
                new BoxView(PicoBox, true, "1.0", 1, [0], BoxWiring.New),
            ],
            network.Boxes,
            SameView);
        await Wait.UntilAsync(
            () => second.Levels[0] == 22 && second.Levels[512] == 11 && _pico.Calls.Contains("levels"),
            "The listed box did not get its universes.");
        Assert.All(_pico.Levels, level => Assert.Equal(0, level));
        Assert.Contains("10.1.9.1", Asked());   // the listed box is looked for
    }

    [Fact]
    public async Task UisHearWhenABoxComesAndGoes()
    {
        Start();
        await Wait.UntilAsync(
            () => _clients.BoxesChanges is [.., [{ Connected: true, Address: PicoBox }]],
            "The UIs did not hear of the box.");

        _pico.Unplug();

        await Wait.UntilAsync(() => _clients.BoxesChanges is [.., []], "The UIs did not hear that the box is gone.");
    }

    [Fact]
    public async Task BoxOfAnAdapterThatWentAwayIsNoLongerLookedFor()
    {
        FakeBox second = Plug("10.1.3.1", outputs: 1);
        _pc.Have(IPAddress.Parse("10.1.3.2"));
        BoxNetwork network = Start();
        await Wait.UntilAsync(() => network.Boxes.Length == 2, "The two boxes did not connect.");

        _pc.Have(PretendPc.HomeAddress);   // the box's cable was pulled out
        network.Refresh();

        await Wait.UntilAsync(() => second.Disposed, "The link of the box that went away did not stop.");
        Assert.Equal([PicoBox], network.Boxes.Select(box => box.Address));
    }

    [Fact]
    public async Task WithoutDiscoveryTheAdaptersOfThePcAreLeftAlone()
    {
        Plug("10.1.3.1", outputs: 1);
        _pc.Have(IPAddress.Parse("10.1.3.2"));

        BoxNetwork network = Start(discover: false);

        await Wait.UntilAsync(() => network.Boxes.Length == 1, "The box of the settings did not connect.");
        Assert.DoesNotContain("10.1.3.1", Asked());
    }

    // A pretend box at an address, plugged in.
    private FakeBox Plug(string address, int outputs)
    {
        var box = new FakeBox(outputs);
        lock (_boxes)
        {
            _boxes[address] = box;
        }

        return box;
    }

    // The network with the Pico box of the settings, which starts looking at once.
    private BoxNetwork Start(bool discover = true)
    {
        _network = new BoxNetwork(_desk, _clients, _pc, PicoBox, BoxPlan.DefaultPort, discover, _pico, NullLoggerFactory.Instance, BoxAt);
        _network.Refresh();
        return _network;
    }

    // The pretend box at an address; one that nobody plugged in does not answer.
    private IBox BoxAt(string address)
    {
        lock (_boxes)
        {
            _asked.Add(address);
            if (_boxes.TryGetValue(address, out FakeBox? box))
                return box;

            var missing = new FakeBox();
            missing.Unplug();
            return missing;
        }
    }

    private string[] Asked()
    {
        lock (_boxes)
        {
            return [.. _asked];
        }
    }

    private static bool SameView(BoxView expected, BoxView actual)
    {
        return expected.SameAs(actual);
    }
}
