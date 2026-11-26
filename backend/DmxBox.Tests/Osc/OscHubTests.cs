using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace DmxBox.Tests.Osc;

/// <summary>OSC through the whole desk program: switched on from a page, a message moves a fader, and every page hears of it.</summary>
public sealed class OscHubTests
{
    [Fact]
    public async Task APageSwitchesOscOnAndAMessageMovesAFaderThatEveryPageHearsOf()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        int port;
        using (var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            port = ((IPEndPoint)probe.Client.LocalEndPoint!).Port;

        JsonElement state = await ui.InvokeAsync("SetOsc", true, port);
        Assert.Equal(
            $$"""{"on":true,"port":{{port}},"state":"listening","problem":null}""",
            state.GetProperty("osc").GetRawText());
        Assert.Equal(state.GetProperty("osc").GetRawText(), (await ui.InvokeAsync("GetMidi")).GetProperty("osc").GetRawText());

        using var sender = new UdpClient(AddressFamily.InterNetwork);
        await sender.SendAsync(OscPacket.Write("/lumenet/fader/channel/5", 200), new IPEndPoint(IPAddress.Loopback, port));

        HubMessage message;
        do
            message = await ui.NextMessageAsync();
        while (message.Name != "ChannelChanged");
        Assert.Equal([5, 200], message.Arguments.Select(argument => argument.GetInt32()));
        Assert.Equal(200, (await ui.InvokeAsync("Hello")).GetProperty("desk").GetProperty("channels")[4].GetInt32());

        // off again: the port is free
        await ui.InvokeAsync("SetOsc", false, port);
        using var other = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
    }
}
