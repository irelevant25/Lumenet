using Microsoft.Extensions.Logging;

namespace DmxBox.Tests.Api;

/// <summary>
/// What the server writes on its console about calls of the hub. The owner watches that
/// console: a mistake of the user must be one calm line there, and only a real fault in the
/// program may look like one.
/// </summary>
public sealed class ServerLogTests
{
    // "DmxBox.Server.Api.DeskHub": a logger is named after its class, with its namespace.
    private static readonly string HubLog = typeof(DeskHub).FullName!;

    // Where SignalR itself writes an error, with a stack trace, for every call that failed.
    private const string SignalRsOwnLog = "Microsoft.AspNetCore.SignalR";

    [Fact]
    public async Task CallThatSucceedsLeavesNothingInTheLog()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        await ui.InvokeAsync("SetChannel", 1, 200);
        await ui.InvokeAsync("SaveMemory", 1);

        Assert.Empty(AboutHubCalls(server));
    }

    [Fact]
    public async Task RefusedCallIsOneInformationLineWithoutAStackTrace()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync("SaveProject", "a/b"));
        await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync("SetChannel", 600, 1));

        LogEntry[] entries = AboutHubCalls(server);
        Assert.Equal(
            [
                "Refused SaveProject: A project name cannot contain the character \"/\".",
                "Refused SetChannel: The channel must be from 1 to 512, not 600.",
            ],
            entries.Select(entry => entry.Message));
        Assert.All(entries, entry =>
        {
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal(HubLog, entry.Category);
            Assert.Null(entry.Exception);   // an exception here would be a stack trace on the console
        });
        Assert.DoesNotContain(server.Log.Entries, entry => entry.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task CallRefusedWithoutAnAnswerIsLoggedTheSame()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        // Sent the way the UI moves faders: nobody waits for an answer, so the log is the only trace.
        await ui.SendAsync("SetChannelNote", 1, new string('a', 25));
        await ui.InvokeAsync("Hello");   // by now the server has dealt with the call before

        LogEntry entry = Assert.Single(AboutHubCalls(server));
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("Refused SetChannelNote: A note can have at most 24 characters.", entry.Message);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public async Task FaultInTheProgramIsLoggedOnceAsAnErrorWithItsDetails()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectToFaultyHubAsync();

        var error = await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync("Break"));

        // The UI learns that the call failed, and nothing about the inside of the program.
        Assert.Equal("An unexpected error occurred invoking 'Break' on the server.", error.Message);

        // The log has the whole story, and has it once: SignalR's own entry is switched off.
        LogEntry entry = Assert.Single(AboutHubCalls(server));
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(HubLog, entry.Category);
        Assert.Equal("Break failed.", entry.Message);
        var fault = Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Equal(FaultyHub.Fault, fault.Message);
    }

    [Fact]
    public async Task ServerSaysWhereItListens()
    {
        await using TestServer server = await TestServer.StartAsync();

        // The start script (run.mjs) waits for this very line before it announces "Ready".
        LogEntry entry = Assert.Single(server.Log.Entries, line => line.Message.StartsWith("Now listening on"));
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal($"Now listening on: {server.Address.GetLeftPart(UriPartial.Authority)}", entry.Message);
    }

    // What the server's own hub log and SignalR's log hold.
    private static LogEntry[] AboutHubCalls(TestServer server)
    {
        return server.Log.Entries
            .Where(entry => entry.Category == HubLog || entry.Category.StartsWith(SignalRsOwnLog))
            .ToArray();
    }
}
