using System.Text.Json;
using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// The outputs besides the boxes as the project keeps them: what a plan may say, the list on the
/// desk (one step of the history, locked while the stack is live), and the project file.
/// </summary>
public sealed class OutputPlanTests
{
    private readonly Desk _desk = new();

    [Fact]
    public void PlanIsWrittenTheOneWay()
    {
        OutputPlan art = new OutputPlan("artnet", " BROADCAST ", [new OutputPort(1, 17)]) { Network = "192.168.1.20/24", Priority = 7 }
            .Checked("Output 1");
        OutputPlan usb = new OutputPlan("usb", "com3", [new OutputPort(2, 99)]) { Network = "10.0.0.0/8" }.Checked("Output 2");
        OutputPlan sacn = new OutputPlan("sacn", "192.168.1.50", [new OutputPort(1, 1)]) { Network = "192.168.1.0/24", Priority = 150 }
            .Checked("Output 3");

        Assert.Equal("broadcast", art.Target);
        Assert.Equal("192.168.1.0/24", art.Network);   // the network, not the PC's address in it
        Assert.Equal(OutputPlan.DefaultPriority, art.Priority);   // only sACN has a priority
        Assert.Equal("COM3", usb.Target);
        Assert.Equal("", usb.Network);
        Assert.Equal(0, usb.Ports[0].Address);   // a USB interface has no address for its port
        Assert.Equal("", sacn.Network);   // a receiver of its own needs no network
        Assert.Equal(150, sacn.Priority);
    }

    [Theory]
    [InlineData("dmx", "192.168.1.5", 1, 0, "is no kind of output")]
    [InlineData("artnet", "10.1", 1, 0, "is no address of a node")]
    [InlineData("artnet", "0.0.0.0", 1, 0, "is no address of a node")]
    [InlineData("artnet", "239.255.0.1", 1, 0, "is no address of a node")]
    [InlineData("artnet", "255.255.255.255", 1, 0, "is no address of a node")]
    [InlineData("artnet", "192.168.1.5", 1, 32768, "a Port-Address is 0 to 32767")]
    [InlineData("artnet", "192.168.1.5", 9, 0, "universe 1 to 8, or none (0)")]
    [InlineData("sacn", "broadcast", 1, 1, "nor multicast")]
    [InlineData("sacn", "multicast", 1, 0, "an sACN universe is 1 to 63999")]
    [InlineData("sacn", "multicast", 1, 64000, "an sACN universe is 1 to 63999")]
    [InlineData("usb", "COM0", 1, 0, "is no serial port")]
    [InlineData("usb", "/etc/passwd", 1, 0, "is no serial port")]
    public void PlanThatCannotBeUsedIsRefused(string kind, string target, int universe, int address, string expectedInMessage)
    {
        var plan = new OutputPlan(kind, target, [new OutputPort(universe, address)]);

        DeskException refusal = Assert.Throws<DeskException>(() => plan.Checked("Output 4"));

        Assert.StartsWith("Output 4", refusal.Message);
        Assert.Contains(expectedInMessage, refusal.Message);
    }

    [Fact]
    public void PortsPriorityAndNetworkHaveTheirLimits()
    {
        Assert.Contains("two ports go to Port-Address 0:0:1",
            Assert.Throws<DeskException>(() => new OutputPlan("artnet", "10.0.0.9", [new(1, 1), new(2, 1)]).Checked("Output 1")).Message);
        Assert.Contains("a USB interface has one port",
            Assert.Throws<DeskException>(() => new OutputPlan("usb", "COM4", [new(1, 0), new(2, 0)]).Checked("Output 1")).Message);
        Assert.Contains("1 to 32 ports",
            Assert.Throws<DeskException>(() => new OutputPlan("sacn", "multicast", []).Checked("Output 1")).Message);
        Assert.Contains("the priority is 0 to 200",
            Assert.Throws<DeskException>(() => new OutputPlan("sacn", "multicast", [new(1, 1)]) { Priority = 201 }.Checked("Output 1")).Message);
        Assert.Contains("is no network like 192.168.1.0/24",
            Assert.Throws<DeskException>(() => new OutputPlan("sacn", "multicast", [new(1, 1)]) { Network = "192.168.1.0/33" }.Checked("Output 1")).Message);
    }

    [Fact]
    public void ListOfTheDeskIsOneOfEachAndChangesTheProject()
    {
        OutputPlan node = new("artnet", "192.168.1.50", [new OutputPort(1, 0)]);
        long before = _desk.ProjectChanges;

        Assert.True(_desk.SetOutputs([node]));
        Assert.False(_desk.SetOutputs([node with { Target = " 192.168.1.50 " }]));   // the same, written another way

        Assert.Equal(before + 1, _desk.ProjectChanges);
        DeskException twice = Assert.Throws<DeskException>(() => _desk.SetOutputs([node, node with { Ports = [new(2, 1)] }]));
        Assert.Equal("The Art-Net node at 192.168.1.50 is in the list twice.", twice.Message);

        // Two broadcasts on two networks are two outputs; sACN by multicast is one per network too.
        OutputPlan broadcast = new("artnet", "broadcast", [new OutputPort(1, 0)]);
        Assert.True(_desk.SetOutputs([broadcast, broadcast with { Network = "2.0.0.0/8" }]));
    }

    [Fact]
    public void ListCannotChangeWhileTheStackIsLive()
    {
        _desk.AddStackStep(new Step(Step.Channel, 1, 255, 0, 0, 0));
        _desk.SetStackLive(true);

        DeskException refusal = Assert.Throws<DeskException>(
            () => _desk.SetOutputs([new OutputPlan("sacn", "multicast", [new OutputPort(1, 1)])]));

        Assert.Contains("The stack is live", refusal.Message);
    }

    [Fact]
    public void OutputsComeBackFromTheProjectFileAsTheyWere()
    {
        _desk.SetUniverses(2);
        OutputPlan[] outputs =
        [
            new("artnet", "192.168.1.50", [new OutputPort(1, 0), new OutputPort(2, 17)]),
            new("artnet", "broadcast", [new OutputPort(1, 0)]) { Network = "2.0.0.0/8" },
            new("sacn", "multicast", [new OutputPort(2, 5)]) { Priority = 120 },
            new("usb", "COM3", [new OutputPort(0, 0)]),
        ];
        _desk.SetOutputs(outputs);

        string text = ProjectFormat.WriteText(_desk.GetProject().Content);
        OutputPlan[] read = ProjectFormat.ReadText(text).Outputs;

        Assert.Equal(outputs.Length, read.Length);
        Assert.All(outputs.Zip(read), pair => Assert.True(pair.First.SameAs(pair.Second), $"{pair.First} came back as {pair.Second}"));

        // The file says only what is not as usual: no network for automatic, no priority of 100,
        // no address for the port of a USB interface.
        using JsonDocument file = JsonDocument.Parse(text);
        JsonElement[] listed = [.. file.RootElement.GetProperty("outputs").EnumerateArray()];
        Assert.False(listed[0].TryGetProperty("network", out _));
        Assert.False(listed[0].TryGetProperty("priority", out _));
        Assert.Equal("2.0.0.0/8", listed[1].GetProperty("network").GetString());
        Assert.Equal(120, listed[2].GetProperty("priority").GetInt32());
        Assert.False(listed[3].GetProperty("ports")[0].TryGetProperty("address", out _));
        Assert.Equal(5, file.RootElement.GetProperty("version").GetInt32());   // no new version for them
    }

    [Fact]
    public void ProjectWithoutOutputsWritesNoneAndAnOlderFileHasNone()
    {
        string text = ProjectFormat.WriteText(_desk.GetProject().Content);

        Assert.DoesNotContain("\"outputs\"", text);
        Assert.Empty(ProjectFormat.ReadText("""{ "format": "lumenet-project", "version": 4, "memories": [] }""").Outputs);
    }

    [Theory]
    [InlineData("""[ { "kind": "artnet", "target": "10.1", "ports": [ { "universe": 1, "address": 0 } ] } ]""", "Output 1 of the file: \"10.1\" is no address")]
    [InlineData("""[ { "kind": "sacn", "target": "multicast" } ]""", "Output 1 of the file has no ports.")]
    [InlineData("""[ null ]""", "The list of outputs in the file has an empty entry.")]
    [InlineData("""[ { "kind": "usb", "target": "COM3", "ports": [ { "universe": 1 } ] }, { "kind": "usb", "target": "com3", "ports": [ { "universe": 2 } ] } ]""", "The USB interface at COM3 is in the list twice.")]
    public void FileWithOutputsThatCannotBeUsedIsRefused(string outputs, string expectedInMessage)
    {
        string text = $$"""{ "format": "lumenet-project", "version": 5, "outputs": {{outputs}}, "memories": [] }""";

        InvalidDataException refusal = Assert.Throws<InvalidDataException>(() => ProjectFormat.ReadText(text));

        Assert.Contains(expectedInMessage, refusal.Message);
    }

    [Fact]
    public void UndoBringsTheOutputsBack()
    {
        var history = new UndoHistory(_desk);
        using (history.Change("the outputs"))
            _desk.SetOutputs([new OutputPlan("sacn", "multicast", [new OutputPort(1, 1)])]);

        Assert.Equal("the outputs", history.Undo());

        Assert.Empty(_desk.Outputs);
    }
}
