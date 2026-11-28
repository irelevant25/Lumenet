using System.Diagnostics;
using System.Text.Json;
using DmxBox.Sim;
using DmxBox.Tests.Boxes;

namespace DmxBox.Tests.Api;

/// <summary>
/// RDM through the hub, as the RDM page uses it: a simulated dmxbox 4 with lights on its line,
/// which the desk finds as it finds a box on a USB cable (sim.cmd's way, through the simulators'
/// folder beside the LAN password).
/// </summary>
public sealed class RdmHubTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    // Calls GetRdmBoxes until the box is there with its lights found.
    private static async Task<JsonElement> BoxWhenFoundAsync(HubClient ui, int lights)
    {
        var waited = Stopwatch.StartNew();
        while (true)
        {
            JsonElement boxes = await ui.InvokeAsync("GetRdmBoxes");
            if (boxes.GetArrayLength() == 1 && boxes[0] is var box && box.GetProperty("rdm").GetBoolean()
                && !box.GetProperty("searching").GetBoolean() && box.GetProperty("lights").GetArrayLength() == lights)
            {
                return box;
            }

            Assert.True(waited.Elapsed < Patience, $"The desk did not find the box with its {lights} lights: {boxes}");
            await Task.Delay(50);
        }
    }

    private static BoxSimulator PlugInBox(TestServer server, int lights, int boxNumber)
    {
        string simulators = Path.Combine(Path.GetDirectoryName(server.PasswordFile)!, "simulators");
        BoxModel model = BoxSimulator.Models[0];
        var box = new BoxSimulator(model, simulators, SimulatorTests.BoxProgram(model.Program), show: 0, TextWriter.Null, lights, boxNumber);
        box.PlugIn();
        return box;
    }

    [Fact]
    public async Task ThePageListsTheBoxesWithTheirLightsAndReadsALight()
    {
        await using TestServer server = await TestServer.StartAsync();
        using BoxSimulator sim = PlugInBox(server, lights: 2, boxNumber: 4);
        await using HubClient ui = await server.ConnectAsync();

        JsonElement box = await BoxWhenFoundAsync(ui, lights: 2);
        Assert.Equal(sim.Address, box.GetProperty("address").GetString());
        Assert.Equal(4, box.GetProperty("number").GetInt32());
        Assert.Equal(JsonValueKind.Null, box.GetProperty("error").ValueKind);

        JsonElement light = await ui.InvokeAsync("GetRdmLight", sim.Address, "7FF1:00000002");
        Assert.Equal("7FF1:00000002", light.GetProperty("id").GetString());
        Assert.Equal("Lumenet test", light.GetProperty("maker").GetString());
        Assert.Equal("Moving head", light.GetProperty("model").GetString());
        Assert.Equal(5, light.GetProperty("address").GetInt32());     // after light 1's 4 channels
        Assert.Equal(16, light.GetProperty("channels").GetInt32());
        Assert.Equal(1, light.GetProperty("mode").GetInt32());
        JsonElement modes = light.GetProperty("modes");
        Assert.Equal(2, modes.GetArrayLength());
        Assert.Equal((16, "16 channels"), (modes[0].GetProperty("channels").GetInt32(), modes[0].GetProperty("name").GetString()));
        Assert.Equal(11, modes[1].GetProperty("channels").GetInt32());
        Assert.False(light.GetProperty("identify").GetBoolean());
        Assert.Equal(JsonValueKind.Null, light.GetProperty("error").ValueKind);
    }

    [Fact]
    public async Task ThePageChangesAddressModeNameAndIdentify()
    {
        await using TestServer server = await TestServer.StartAsync();
        using BoxSimulator sim = PlugInBox(server, lights: 1, boxNumber: 0);
        await using HubClient ui = await server.ConnectAsync();
        await BoxWhenFoundAsync(ui, lights: 1);
        const string Id = "7FF1:00000001";

        JsonElement light = await ui.InvokeAsync("SetRdmLight", sim.Address, Id, new { mode = 2 });
        Assert.Equal((2, 8), (light.GetProperty("mode").GetInt32(), light.GetProperty("channels").GetInt32()));

        light = await ui.InvokeAsync("SetRdmLight", sim.Address, Id, new { address = 101 });
        Assert.Equal(101, light.GetProperty("address").GetInt32());

        light = await ui.InvokeAsync("SetRdmLight", sim.Address, Id, new { label = "Predný ľavý" });
        Assert.Equal("Predny lavy", light.GetProperty("label").GetString());   // RDM keeps plain ASCII

        light = await ui.InvokeAsync("SetRdmLight", sim.Address, Id, new { identify = true });
        Assert.True(light.GetProperty("identify").GetBoolean());

        // Read again from the light: all of it stayed.
        light = await ui.InvokeAsync("GetRdmLight", sim.Address, Id);
        Assert.Equal((2, 101, "Predny lavy", true), (light.GetProperty("mode").GetInt32(), light.GetProperty("address").GetInt32(),
            light.GetProperty("label").GetString(), light.GetProperty("identify").GetBoolean()));
    }

    [Fact]
    public async Task WhatTheLightsOrTheDeskRefuseIsToldInWords()
    {
        await using TestServer server = await TestServer.StartAsync();
        using BoxSimulator sim = PlugInBox(server, lights: 1, boxNumber: 0);
        await using HubClient ui = await server.ConnectAsync();
        await BoxWhenFoundAsync(ui, lights: 1);

        var mode = await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync("SetRdmLight", sim.Address, "7FF1:00000001", new { mode = 9 }));
        Assert.Contains("The light 7FF1:00000001 refused: the value is out of its range.", mode.Message, StringComparison.Ordinal);

        var address = await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync("SetRdmLight", sim.Address, "7FF1:00000001", new { address = 600 }));
        Assert.Contains("A DMX address is 1 to 512, not 600.", address.Message, StringComparison.Ordinal);

        var label = await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync("SetRdmLight", sim.Address, "7FF1:00000001", new { label = new string('x', 33) }));
        Assert.Contains("at most 32 characters", label.Message, StringComparison.Ordinal);

        var stranger = await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync("GetRdmLight", "10.9.9.1", "7FF1:00000001"));
        Assert.Contains("The desk knows no box at 10.9.9.1.", stranger.Message, StringComparison.Ordinal);

        var id = await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync("GetRdmLight", sim.Address, "light 1"));
        Assert.Contains("is no RDM ID like 7FF1:00000001", id.Message, StringComparison.Ordinal);

        JsonElement missing = await ui.InvokeAsync("GetRdmLight", sim.Address, "7FF1:00000099");
        Assert.Equal("The light does not answer.", missing.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ASearchFromThePageFindsTheLightsAgain()
    {
        await using TestServer server = await TestServer.StartAsync();
        using BoxSimulator sim = PlugInBox(server, lights: 3, boxNumber: 0);
        await using HubClient ui = await server.ConnectAsync();
        await BoxWhenFoundAsync(ui, lights: 3);

        await ui.InvokeAsync("SearchRdm", sim.Address);

        await BoxWhenFoundAsync(ui, lights: 3);
    }
}
