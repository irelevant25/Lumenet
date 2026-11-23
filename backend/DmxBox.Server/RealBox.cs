namespace DmxBox.Server;

/// <summary>The real dmxbox, reached with the library's <see cref="DmxBoxClient"/>.</summary>
/// <remarks>Not thread-safe: the box link uses it from its one thread.</remarks>
internal sealed class RealBox : IBox
{
    // The library's own patience (6 attempts, several seconds) suits a command line tool.
    // Here a box that was unplugged has to be noticed within about a second: the first
    // attempt waits 250 ms for the answer, the second and last one 500 ms.
    private const int Attempts = 2;

    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromMilliseconds(250);

    private readonly string _host;
    private readonly int _port;
    private DmxBoxClient? _client;

    /// <summary>Prepares the connection. Nothing is sent, and a host name is not looked up yet.</summary>
    /// <param name="host">IPv4 address or host name of the box.</param>
    /// <param name="port">UDP port of the box's control protocol.</param>
    public RealBox(string host, int port)
    {
        _host = host;
        _port = port;
    }

    /// <inheritdoc />
    public PingInfo Ping()
    {
        return Client().Ping();
    }

    /// <inheritdoc />
    public void SetLevels(byte[] levels)
    {
        // One request for each output of 512: a packet of all of them could be too big for the
        // network and come in pieces, which a box's small network stack may not put together.
        for (int start = 0; start < levels.Length; start += Desk.UniverseSize)
            Client().Set(start + 1, levels.AsSpan(start, Math.Min(Desk.UniverseSize, levels.Length - start)));
    }

    /// <inheritdoc />
    public void Master(byte level)
    {
        Client().Master(level);
    }

    /// <inheritdoc />
    public void Blackout(bool on)
    {
        Client().Blackout(on);
    }

    /// <summary>Closes the socket.</summary>
    public void Dispose()
    {
        _client?.Dispose();
    }

    // The client is made at the first request and not in the constructor, because making it can
    // fail: a host name that cannot be looked up right now, for example. That must not stop the
    // server from starting. This way it fails inside a request, and the box link tries again later.
    private DmxBoxClient Client()
    {
        return _client ??= new DmxBoxClient(_host, _port, AnswerTimeout, Attempts);
    }
}
