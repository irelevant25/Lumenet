using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading.Channels;

namespace DmxBox.Tests.Sounds;

/// <summary>
/// A pretend Soundcraft Ui mixer for the tests: <see cref="Sockets"/> makes the sockets the link
/// uses. The mixer notes every line it gets and says what a test tells it to, as the real one does
/// over its websocket: lines in frames that start with "3:::".
/// </summary>
internal sealed class PretendUiMixer
{
    private readonly ConcurrentQueue<string> _got = new();
    private volatile PretendSocket? _socket;
    private int _connections;

    /// <summary>True: no mixer at the address, a connection fails.</summary>
    public bool Away { get; set; }

    /// <summary>The address the link connected to last, like "ws://10.10.1.1/".</summary>
    public string? Address { get; private set; }

    /// <summary>How many times the link connected.</summary>
    public int Connections => Volatile.Read(ref _connections);

    /// <summary>Every line the mixer got, without "3:::" and without ALIVE.</summary>
    public string[] Got => [.. _got.Where(line => line != "ALIVE")];

    /// <summary>True when ALIVE came.</summary>
    public bool HeardAlive => _got.Contains("ALIVE");

    /// <summary>The sockets of the link.</summary>
    public Func<IUiSocket> Sockets => () => new PretendSocket(this);

    /// <summary>The mixer says these lines, in one frame.</summary>
    public void Say(params string[] lines)
    {
        Assert.True(_socket?.Incoming.Writer.TryWrite("3:::" + string.Join('\n', lines)), "no link to say it on");
    }

    /// <summary>The mixer closes its socket.</summary>
    public void Close()
    {
        _socket?.Incoming.Writer.TryWrite(null);
    }

    /// <summary>What it got so far is forgotten.</summary>
    public void Forget()
    {
        _got.Clear();
    }

    private sealed class PretendSocket(PretendUiMixer mixer) : IUiSocket
    {
        public Channel<string?> Incoming { get; } = Channel.CreateUnbounded<string?>();

        public Task ConnectAsync(Uri address, CancellationToken cancel)
        {
            if (mixer.Away)
                throw new WebSocketException("No mixer there.");

            mixer.Address = address.ToString();
            mixer._socket = this;
            Interlocked.Increment(ref mixer._connections);
            return Task.CompletedTask;
        }

        public Task SendAsync(string frame, CancellationToken cancel)
        {
            Assert.StartsWith("3:::", frame, StringComparison.Ordinal);
            mixer._got.Enqueue(frame[4..]);
            return Task.CompletedTask;
        }

        public async Task<string?> ReceiveAsync(CancellationToken cancel)
        {
            return await Incoming.Reader.ReadAsync(cancel);
        }

        public ValueTask DisposeAsync()
        {
            if (mixer._socket == this)
                mixer._socket = null;
            return ValueTask.CompletedTask;
        }
    }
}
