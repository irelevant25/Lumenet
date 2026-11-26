using System.Text.Json;

namespace DmxBox.Tests.Outputs;

/// <summary>
/// The outputs besides the boxes through the hub, as the Outputs window uses them: the list, its
/// states, the search for Art-Net nodes, the serial ports and the networks. The server sends from
/// 127.0.0.1 only; a pretend node and a pretend receiver listen there.
/// </summary>
public sealed class OutputHubTests : IDisposable
{
    private readonly FakeArtNetNode _node = new();
    private readonly FakeSacnReceiver _receiver = new();

    public void Dispose()
    {
        _node.Dispose();
        _receiver.Dispose();
    }

    [Fact]
    public async Task OutputAddedInTheWindowPlaysItsUniverseAndEveryUiHearsOfIt()
    {
        await using TestServer server = await StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await using HubClient other = await server.ConnectAsync();
        await ui.InvokeAsync("SetChannel", 1, 200);

        await ui.InvokeAsync("SetOutputs", (object)new[] { new { kind = "artnet", target = "127.0.0.1", ports = new[] { new { universe = 1, address = 3 } } } });

        await Wait.UntilAsync(() => _node.Frames.Any(frame => frame.PortAddress == 3 && frame.Levels[0] == 200), "The node did not get the universe.");
        JsonElement outputs = await NextOutputsAsync(other, "connected");
        JsonElement output = Assert.Single(outputs.EnumerateArray());
        Assert.Equal(
            """{"kind":"artnet","target":"127.0.0.1","ports":[{"universe":1,"address":3}],"network":"","priority":100}""",
            output.GetProperty("plan").GetRawText());
        Assert.Equal("Pretend node, firmware 1.2. No port of it listens to 0:0:3; its ports are at 0:0:0, 0:0:1.", output.GetProperty("detail").GetString());
        Assert.Equal(outputs.GetRawText(), (await ui.InvokeAsync("GetOutputs")).GetRawText());
        Assert.Contains("\"outputs\"", (await ui.InvokeAsync("ExportProject")).GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UndoTakesTheOutputAwayAndSacnSaysThatItEnds()
    {
        await using TestServer server = await StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await ui.InvokeAsync("SetOutputs", (object)new[] { new { kind = "sacn", target = "127.0.0.1", ports = new[] { new { universe = 1, address = 9 } } } });
        await Wait.UntilAsync(() => _receiver.Packets.Length > 0, "The receiver got nothing.");

        Assert.Equal("the outputs", (await ui.InvokeAsync("Undo")).GetString());

        await Wait.UntilAsync(
            () => _receiver.Packets.Count(packet => packet.Terminated && packet.Universe == 9) == 3,
            "The receiver was not told that the universe ends.");
        Assert.Equal("[]", (await ui.InvokeAsync("GetOutputs")).GetRawText());
    }

    [Fact]
    public async Task ArtNetToADmxboxIsRefused()
    {
        await using TestServer server = await StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        var error = await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync(
            "SetOutputs", (object)new[] { new { kind = "artnet", target = "192.168.7.1", ports = new[] { new { universe = 1, address = 0 } } } }));

        Assert.EndsWith(
            "HubException: 192.168.7.1 is a dmxbox, which the desk plays with its own protocol already. Give it its universes in the dmxbox section.",
            error.Message);
        Assert.Equal("[]", (await ui.InvokeAsync("GetOutputs")).GetRawText());
    }

    [Fact]
    public async Task OutputsCannotChangeWhileTheStackIsLive()
    {
        await using TestServer server = await StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await ui.InvokeAsync("AddStackStep", new { target = "channel", number = 1, value = 255, up = 1, wait = 0, down = 1 });
        await ui.InvokeAsync("SetStackLive", true);

        var error = await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync(
            "SetOutputs", (object)new[] { new { kind = "sacn", target = "multicast", ports = new[] { new { universe = 1, address = 1 } } } }));

        Assert.Contains("The stack is live", error.Message);
    }

    [Fact]
    public async Task SearchFindsTheNodesThatAnswerAndTellsADmxbox()
    {
        await using TestServer server = await StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        JsonElement found = await SearchAsync(ui);

        JsonElement node = Assert.Single(found.EnumerateArray());
        Assert.Equal(
            """{"ip":"127.0.0.1","shortName":"Pretend node","longName":"Pretend node, a node of the tests","firmware":"1.2","ports":[0,1],"dmxbox":false}""",
            node.GetRawText());

        _node.ShortName = "dmxbox 2";
        found = await SearchAsync(ui);
        Assert.True(Assert.Single(found.EnumerateArray()).GetProperty("dmxbox").GetBoolean());
    }

    [Fact]
    public async Task TheSearchHoldsUpNothingElseOfTheUi()
    {
        await using TestServer server = await StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        // The search takes a second and a half; a fader moved meanwhile is answered at once.
        string search = await ui.StartStreamAsync("FindArtNetNodes");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        await ui.InvokeAsync("SetChannel", 1, 200);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), $"The fader waited {clock.Elapsed.TotalSeconds:0.00} s for the search.");

        JsonElement found = Assert.Single(await ui.StreamResultAsync(search));
        Assert.Single(found.EnumerateArray());
    }

    // The search, as the Outputs panel asks for it: a stream of one answer.
    private static async Task<JsonElement> SearchAsync(HubClient ui)
    {
        return Assert.Single(await ui.StreamResultAsync(await ui.StartStreamAsync("FindArtNetNodes")));
    }

    [Fact]
    public async Task WindowGetsTheSerialPortsAndTheNetworks()
    {
        await using TestServer server = await StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        Assert.Equal(JsonValueKind.Array, (await ui.InvokeAsync("GetSerialPorts")).ValueKind);
        Assert.Equal(
            """[{"network":"10.20.30.0/24","address":"10.20.30.40","adapter":"Pretend 1","router":true}]""",
            (await ui.InvokeAsync("GetNetworks")).GetRawText());
    }

    private Task<TestServer> StartAsync()
    {
        return TestServer.StartAsync($"--Outputs:ArtNetPort={_node.Port}", $"--Outputs:SacnPort={_receiver.Port}");
    }

    // The next "OutputsChanged" whose first output is in the state, passing over the others.
    private static async Task<JsonElement> NextOutputsAsync(HubClient ui, string state)
    {
        while (true)
        {
            HubMessage message = await ui.NextMessageAsync();
            if (message.Name != "OutputsChanged")
                continue;

            JsonElement outputs = Assert.Single(message.Arguments);
            if (outputs.GetArrayLength() > 0 && outputs[0].GetProperty("state").GetString() == state)
                return outputs;
        }
    }
}
