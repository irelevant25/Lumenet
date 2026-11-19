using System.Net.WebSockets;
using System.Text.Json;

namespace DmxBox.Tests;

/// <summary>
/// A UI as the server sees it, for the tests: a WebSocket that speaks SignalR's JSON protocol,
/// just enough of it to call hub methods and to receive what the server sends. Written by hand
/// so that the tests see the very JSON a browser would get, and need no extra package.
/// </summary>
/// <remarks>
/// The protocol in short: every message is one JSON object followed by the character 0x1E.
/// The client opens with {"protocol":"json","version":1} and the server answers {}.
/// After that, "type" tells the messages apart: 1 calls a method (the UI one of the hub, or
/// the server one of the UI, which is how its messages arrive), 3 carries the result or the
/// error of a call, 6 is a keep-alive.
/// </remarks>
internal sealed class HubClient : IAsyncDisposable
{
    private const byte EndOfMessage = 0x1E;
    private const int MethodCall = 1;
    private const int Completion = 3;

    // Longer than anything here should take. A test that waits for a message that never
    // comes fails after this time instead of hanging.
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    private readonly ClientWebSocket _socket = new();
    private readonly MemoryStream _incoming = new();        // a message that has arrived in part
    private readonly Queue<JsonElement> _unread = new();    // whole messages nobody has looked at
    private readonly Queue<HubMessage> _messages = new();   // what the server sent on its own
    private int _calls;

    private HubClient()
    {
    }

    /// <summary>Connects to a hub the way a browser does once it has chosen WebSockets.</summary>
    /// <param name="hub">The hub's address, for example ws://127.0.0.1:5080/hub/desk.</param>
    public static async Task<HubClient> ConnectAsync(Uri hub)
    {
        var client = new HubClient();
        try
        {
            using var patience = new CancellationTokenSource(Patience);
            await client._socket.ConnectAsync(hub, patience.Token);
            await client.WriteAsync(new { protocol = "json", version = 1 });
            await client.ReadAsync();   // the server's {}: it accepts the protocol
            return client;
        }
        catch
        {
            await client.DisposeAsync();
            throw;
        }
    }

    /// <summary>Calls a hub method and waits for its answer.</summary>
    /// <returns>The method's result as JSON; an undefined element for a method without a result.</returns>
    /// <exception cref="HubCallException">The server answered with an error.</exception>
    public async Task<JsonElement> InvokeAsync(string method, params object?[] arguments)
    {
        string id = (++_calls).ToString();
        await WriteAsync(new { type = MethodCall, invocationId = id, target = method, arguments });

        while (true)
        {
            JsonElement message = await ReadAsync();
            if (!IsCompletionOf(message, id))
            {
                Keep(message);
                continue;
            }

            if (message.TryGetProperty("error", out JsonElement error))
                throw new HubCallException(error.GetString()!);

            return message.TryGetProperty("result", out JsonElement result) ? result : default;
        }
    }

    /// <summary>
    /// Calls a hub method without asking for an answer. The UI moves its faders this way.
    /// </summary>
    public Task SendAsync(string method, params object?[] arguments)
    {
        return WriteAsync(new { type = MethodCall, target = method, arguments });
    }

    /// <summary>Waits for the next message the server sent on its own, for example "DeskChanged".</summary>
    public async Task<HubMessage> NextMessageAsync()
    {
        while (_messages.Count == 0)
            Keep(await ReadAsync());

        return _messages.Dequeue();
    }

    /// <summary>Drops the connection.</summary>
    public async ValueTask DisposeAsync()
    {
        _socket.Abort();   // no polite goodbye: the test is over
        _socket.Dispose();
        await _incoming.DisposeAsync();
    }

    private static bool IsCompletionOf(JsonElement message, string id)
    {
        return message.GetProperty("type").GetInt32() == Completion
            && message.GetProperty("invocationId").GetString() == id;
    }

    // Puts a message of the server aside for NextMessageAsync. Keep-alives are dropped.
    private void Keep(JsonElement message)
    {
        if (message.GetProperty("type").GetInt32() != MethodCall)
            return;

        _messages.Enqueue(new HubMessage(
            message.GetProperty("target").GetString()!,
            message.GetProperty("arguments").EnumerateArray().ToArray()));
    }

    private async Task WriteAsync(object message)
    {
        using var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, message);
        stream.WriteByte(EndOfMessage);

        using var patience = new CancellationTokenSource(Patience);
        await _socket.SendAsync(
            stream.ToArray().AsMemory(), WebSocketMessageType.Text, endOfMessage: true, patience.Token);
    }

    private async Task<JsonElement> ReadAsync()
    {
        while (_unread.Count == 0)
            await ReceiveAsync();

        return _unread.Dequeue();
    }

    // Takes one piece from the socket. A piece can hold several messages, or only a part of one.
    private async Task ReceiveAsync()
    {
        byte[] buffer = new byte[16 * 1024];
        ValueWebSocketReceiveResult piece;
        using var patience = new CancellationTokenSource(Patience);
        try
        {
            piece = await _socket.ReceiveAsync(buffer.AsMemory(), patience.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"The hub sent nothing for {Patience.TotalSeconds} seconds.");
        }

        if (piece.MessageType == WebSocketMessageType.Close)
            throw new InvalidOperationException($"The server closed the connection: {_socket.CloseStatusDescription}");

        _incoming.Write(buffer, 0, piece.Count);
        if (piece.EndOfMessage)
            SplitIncoming();
    }

    // Everything that has come in is whole messages now, each with 0x1E at its end.
    private void SplitIncoming()
    {
        byte[] bytes = _incoming.ToArray();
        _incoming.SetLength(0);

        int start = 0;
        while (start < bytes.Length)
        {
            int end = Array.IndexOf(bytes, EndOfMessage, start);
            using JsonDocument document = JsonDocument.Parse(bytes.AsMemory(start, end - start));
            _unread.Enqueue(document.RootElement.Clone());
            start = end + 1;
        }
    }
}
