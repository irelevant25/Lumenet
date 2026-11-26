using System.IO.Pipes;

namespace DmxBox.Server.Outputs;

/// <summary>
/// The "serial port" of a simulated DMX USB interface (sim.cmd enttec): a named pipe of the
/// simulator, which the desk lists as a port "SIM1", "SIM2" while the simulator runs. The link
/// writes and reads it as it does a real port, so the simulated interface goes through all of
/// <see cref="UsbLink"/>: opening, the questions, the frames, pulling out and plugging in.
/// </summary>
/// <remarks>Not thread-safe for writing: the link uses it from its one thread. A task of its own reads.</remarks>
internal sealed class PipeDmxPort : IDmxPort
{
    // How long opening waits for the simulator to take the connection.
    private const int ConnectTimeoutMs = 500;

    private readonly string _name;
    private readonly Lock _lock = new();
    private readonly Queue<byte> _received = new();
    private NamedPipeClientStream? _pipe;
    private volatile bool _closed;

    /// <summary>Prepares the port. Nothing happens until it is opened.</summary>
    /// <param name="name">"SIM1".</param>
    public PipeDmxPort(string name)
    {
        _name = name;
    }

    /// <summary>True for the name of a simulated port: "SIM" and a number.</summary>
    /// <param name="name">A port's name.</param>
    public static bool IsSimulated(string name)
    {
        return name.StartsWith("SIM", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The pipe of a simulated port: "lumenet-sim-SIM1".</summary>
    /// <param name="name">"SIM1".</param>
    public static string PipeName(string name)
    {
        return $"lumenet-sim-{name.ToUpperInvariant()}";
    }

    /// <inheritdoc />
    /// <exception cref="UnauthorizedAccessException">The simulator has another program connected already.</exception>
    public void Open()
    {
        var pipe = new NamedPipeClientStream(".", PipeName(_name), PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            pipe.Connect(ConnectTimeoutMs);
        }
        catch (TimeoutException)
        {
            pipe.Dispose();
            throw new UnauthorizedAccessException($"{_name} has another program connected.");
        }

        _pipe = pipe;
        _ = Task.Run(() => ReadAsync(pipe));
    }

    /// <inheritdoc />
    public void Write(byte[] data)
    {
        NamedPipeClientStream pipe = _pipe ?? throw new InvalidOperationException($"{_name} is not open.");
        if (_closed)
            throw new IOException($"{_name} went away: the simulated interface was pulled out.");

        pipe.Write(data);
        pipe.Flush();
    }

    /// <inheritdoc />
    public int Read(byte[] buffer)
    {
        lock (_lock)
        {
            if (_received.Count == 0 && _closed)
                throw new IOException($"{_name} went away: the simulated interface was pulled out.");

            int count = 0;
            while (count < buffer.Length && _received.TryDequeue(out byte value))
                buffer[count++] = value;
            return count;
        }
    }

    /// <summary>Closes the pipe.</summary>
    public void Dispose()
    {
        _closed = true;
        _pipe?.Dispose();
    }

    // Takes what the simulator sends until it closes the pipe.
    private async Task ReadAsync(NamedPipeClientStream pipe)
    {
        byte[] buffer = new byte[1024];
        try
        {
            int count;
            while ((count = await pipe.ReadAsync(buffer)) > 0)
            {
                lock (_lock)
                {
                    for (int index = 0; index < count; index++)
                        _received.Enqueue(buffer[index]);
                }
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // Pulled out, or closed by the link.
        }

        _closed = true;
    }
}
