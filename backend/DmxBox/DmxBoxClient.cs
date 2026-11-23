using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace DmxBox;

/// <summary>
/// Reliable control connection to a dmxbox: the "DX" protocol on UDP port 7000, for set, fade,
/// presets, master and blackout. Every call waits for the box's ACK and sends the request
/// again when a packet got lost; the box executes each request exactly once.
/// </summary>
/// <remarks>
/// <para>
/// Every method that talks to the box throws <see cref="DmxBoxException"/> if the box rejects
/// the request, and <see cref="DmxBoxTimeoutException"/> if it does not answer.
/// </para>
/// <para>
/// Thread-safe: requests are sent one at a time (stop-and-wait), which is what the box's
/// duplicate detection expects. Separate programs each use their own instance.
/// </para>
/// </remarks>
public sealed class DmxBoxClient : IDisposable
{
    /// <summary>The UDP port of the DX protocol.</summary>
    public const int DefaultPort = 7000;

    /// <summary>Number of DMX channels of the box.</summary>
    public const int ChannelCount = 512;

    // Every message starts with a 12-byte header. All integers are big-endian.
    //    0  "DX"
    //    2  protocol version
    //    3  message type
    //    4  session (u32)
    //    8  sequence (u16)
    //   10  payload length (u16)
    private const int HeaderLength = 12;
    private const byte ProtocolVersion = 1;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromSeconds(1);

    private readonly Lock _lock = new();
    private readonly Socket _socket;
    private readonly IPEndPoint _endPoint;
    private readonly string _host;
    private readonly TimeSpan _timeout;
    private readonly int _attempts;
    private readonly byte[] _receiveBuffer = new byte[Udp.MaxDatagramLength];

    /// <summary>Prepares a connection. Nothing is sent until the first request.</summary>
    /// <param name="host">IPv4 address or host name of the box.</param>
    /// <param name="port">UDP port of the DX protocol.</param>
    /// <param name="timeout">
    /// How long to wait for the ACK before sending a request again. Default: 150 ms.
    /// The wait doubles with every retransmit, up to 1 second.
    /// </param>
    /// <param name="attempts">How many times a request is sent before giving up.</param>
    public DmxBoxClient(string host, int port = DefaultPort, TimeSpan? timeout = null, int attempts = 6)
    {
        _timeout = timeout ?? DefaultTimeout;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_timeout, TimeSpan.Zero, nameof(timeout));
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);

        _attempts = attempts;
        _host = host;
        _endPoint = Udp.Resolve(host, port);
        _socket = Udp.CreateSocket();

        // Never 0: that would tell the box not to detect duplicates.
        Session = (uint)Random.Shared.NextInt64(1, 1L << 32);
        Sequence = (ushort)Random.Shared.Next(1 << 16);
    }

    /// <summary>
    /// Random ID of this connection, never 0. The box uses it to tell its clients apart.
    /// </summary>
    public uint Session { get; }

    /// <summary>
    /// Sequence number of the most recent request. It grows by one with every request
    /// and wraps around after 65535.
    /// </summary>
    public ushort Sequence { get; internal set; }

    /// <summary>How many times a request had to be sent again because no ACK arrived in time.</summary>
    public int Retransmits { get; private set; }

    private static ReadOnlySpan<byte> Magic => "DX"u8;

    /// <summary>
    /// Sends one request and waits for its ACK. All other methods use this one; call it
    /// yourself only for a message type that has no method.
    /// </summary>
    /// <param name="type">Message type, see <see cref="MessageType"/>.</param>
    /// <param name="payload">Payload of the request, at most 65535 bytes.</param>
    /// <returns>The data of the ACK, without the status byte.</returns>
    /// <exception cref="DmxBoxException">The box answered with an error status.</exception>
    /// <exception cref="DmxBoxTimeoutException">No ACK arrived, even after all retransmits.</exception>
    /// <exception cref="SocketException">The request could not be sent, for example because no network leads to the box.</exception>
    public byte[] Request(byte type, ReadOnlySpan<byte> payload = default)
    {
        if (payload.Length > ushort.MaxValue)
            throw new ArgumentException("The payload is longer than 65535 bytes.", nameof(payload));

        lock (_lock)
        {
            Sequence = unchecked((ushort)(Sequence + 1));
            byte[] packet = BuildPacket(type, payload);

            TimeSpan timeout = _timeout;
            for (int attempt = 0; attempt < _attempts; attempt++)
            {
                if (attempt > 0)
                    Retransmits++;

                _socket.SendTo(packet, _endPoint);
                if (WaitForAck(timeout) is { } data)
                    return data;

                // Back off: the box may just be busy. Writing a preset to flash takes about 150 ms.
                timeout *= 2;
                if (timeout > MaxTimeout)
                    timeout = MaxTimeout;
            }

            throw new DmxBoxTimeoutException($"no answer from {_host}:{_endPoint.Port}");
        }
    }

    /// <summary>Asks the box for its version and state. Also the way to check that it is there.</summary>
    public PingInfo Ping()
    {
        // Answer: firmware major, firmware minor, flags, master, uptime in ms (u32),
        // preset mask (u16), channel count (u16).
        byte[] data = Request(MessageType.Ping);
        if (data.Length < 12)
            throw new InvalidDataException($"The answer to PING has {data.Length} bytes instead of 12.");

        byte flags = data[2];
        uint uptimeMilliseconds = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4));
        ushort presetMask = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(8));

        return new PingInfo(
            Firmware: $"{data[0]}.{data[1]}",
            ArtNetLive: (flags & 1) != 0,
            Blackout: (flags & 2) != 0,
            MergeMode: (flags & 4) != 0 ? MergeMode.Override : MergeMode.Htp,
            ArtNetHolding: (flags & 8) != 0,
            Master: data[3],
            Uptime: TimeSpan.FromMilliseconds(uptimeMilliseconds),
            Presets: SlotsIn(presetMask),
            Channels: BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(10)));
    }

    /// <summary>Snaps channels <paramref name="start"/>, start + 1, ... to <paramref name="values"/>.</summary>
    /// <param name="start">First channel, 1-512.</param>
    /// <param name="values">One level per channel; at least one.</param>
    public void Set(int start, ReadOnlySpan<byte> values)
    {
        RequireValues(values);

        byte[] payload = new byte[4 + values.Length];
        WriteChannelRange(payload, start, values.Length);
        values.CopyTo(payload.AsSpan(4));
        Request(MessageType.Set, payload);
    }

    /// <summary>
    /// Fades channels <paramref name="start"/>, start + 1, ... from wherever they are now to
    /// <paramref name="values"/>. The box computes the fade itself, so it stays smooth
    /// whatever the PC does.
    /// </summary>
    /// <param name="start">First channel, 1-512.</param>
    /// <param name="values">One target level per channel; at least one.</param>
    /// <param name="duration">How long the fade takes. Zero snaps like <see cref="Set"/>.</param>
    public void Fade(int start, ReadOnlySpan<byte> values, TimeSpan duration)
    {
        RequireValues(values);

        byte[] payload = new byte[8 + values.Length];
        WriteChannelRange(payload, start, values.Length);
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4), ToMilliseconds(duration));
        values.CopyTo(payload.AsSpan(8));
        Request(MessageType.Fade, payload);
    }

    /// <summary>
    /// Stores the current look (before master and blackout) in the box's flash memory,
    /// where it survives power cycles.
    /// </summary>
    /// <param name="slot">Preset slot, 0-15.</param>
    public void PresetSave(int slot)
    {
        Request(MessageType.PresetSave, [ToByte(slot, nameof(slot))]);
    }

    /// <summary>Fades all channels to a stored preset.</summary>
    /// <param name="slot">Preset slot, 0-15.</param>
    /// <param name="duration">How long the fade takes. Default: no fade, the look snaps.</param>
    public void PresetGo(int slot, TimeSpan duration = default)
    {
        byte[] payload = new byte[5];
        payload[0] = ToByte(slot, nameof(slot));
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(1), ToMilliseconds(duration));
        Request(MessageType.PresetGo, payload);
    }

    /// <summary>Empties a preset slot.</summary>
    /// <param name="slot">Preset slot, 0-15.</param>
    public void PresetClear(int slot)
    {
        Request(MessageType.PresetClear, [ToByte(slot, nameof(slot))]);
    }

    /// <summary>Reads the look stored in a preset slot.</summary>
    /// <param name="slot">Preset slot, 0-15.</param>
    /// <returns>The stored levels of all the box's channels, 512 for each output; channel 1 is at index 0.</returns>
    public byte[] GetPreset(int slot)
    {
        return Request(MessageType.GetPreset, [ToByte(slot, nameof(slot))]);
    }

    /// <summary>Sets the master level, which scales every channel.</summary>
    /// <param name="level">255 = full, 0 = dark.</param>
    public void Master(byte level)
    {
        Request(MessageType.Master, [level]);
    }

    /// <summary>
    /// Switches blackout on or off. During blackout every channel is 0; the look underneath is kept.
    /// </summary>
    /// <param name="on">True for dark, false for the normal look.</param>
    public void Blackout(bool on = true)
    {
        Request(MessageType.Blackout, [on ? (byte)1 : (byte)0]);
    }

    /// <summary>Forgets the last Art-Net look. In HTP mode the box otherwise holds it forever.</summary>
    public void ReleaseArtNet()
    {
        Request(MessageType.ReleaseArtNet);
    }

    /// <summary>Chooses how the box combines Art-Net with its own cue layer.</summary>
    /// <param name="mode">HTP (the default after power-on) or override.</param>
    public void SetMergeMode(MergeMode mode)
    {
        Request(MessageType.MergeMode, [(byte)mode]);
    }

    /// <summary>Reads what is on the DMX line right now, after master and blackout.</summary>
    /// <param name="start">First channel, 1-512.</param>
    /// <param name="count">Number of channels.</param>
    /// <returns>One level per channel; channel <paramref name="start"/> is at index 0.</returns>
    public byte[] GetOutput(int start = 1, int count = ChannelCount)
    {
        byte[] payload = new byte[4];
        WriteChannelRange(payload, start, count);
        return Request(MessageType.GetOutput, payload);
    }

    /// <summary>Closes the socket.</summary>
    public void Dispose() => _socket.Dispose();

    private byte[] BuildPacket(byte type, ReadOnlySpan<byte> payload)
    {
        byte[] packet = new byte[HeaderLength + payload.Length];
        Magic.CopyTo(packet);
        packet[2] = ProtocolVersion;
        packet[3] = type;
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(4), Session);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(8), Sequence);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(10), (ushort)payload.Length);
        payload.CopyTo(packet.AsSpan(HeaderLength));
        return packet;
    }

    // Returns the data of the ACK for the current request, or null if it does not arrive in time.
    private byte[]? WaitForAck(TimeSpan timeout)
    {
        var waited = Stopwatch.StartNew();
        while (Udp.TryReceive(_socket, _receiveBuffer, timeout - waited.Elapsed, out int length))
        {
            ReadOnlySpan<byte> datagram = _receiveBuffer.AsSpan(0, length);
            if (IsAckOfCurrentRequest(datagram))
                return ReadAck(datagram);

            // Otherwise: the late ACK of an earlier request, or not from the box at all. Keep waiting.
        }

        return null;
    }

    private bool IsAckOfCurrentRequest(ReadOnlySpan<byte> datagram)
    {
        return datagram.Length > HeaderLength   // the header and at least the status byte
            && datagram[..2].SequenceEqual(Magic)
            && datagram[2] == ProtocolVersion
            && datagram[3] == MessageType.Ack
            && BinaryPrimitives.ReadUInt32BigEndian(datagram[4..]) == Session
            && BinaryPrimitives.ReadUInt16BigEndian(datagram[8..]) == Sequence;
    }

    // An ACK's payload is the status byte followed by the data.
    private static byte[] ReadAck(ReadOnlySpan<byte> ack)
    {
        byte status = ack[HeaderLength];
        if (status != 0)
            throw new DmxBoxException(status);

        ReadOnlySpan<byte> data = ack[(HeaderLength + 1)..];
        int dataLength = BinaryPrimitives.ReadUInt16BigEndian(ack[10..]) - 1;
        return data[..Math.Clamp(dataLength, 0, data.Length)].ToArray();
    }

    // Bit n of the mask is set if preset slot n holds a look.
    private static List<int> SlotsIn(ushort presetMask)
    {
        var slots = new List<int>();
        for (int slot = 0; slot < 16; slot++)
        {
            if ((presetMask >> slot & 1) != 0)
                slots.Add(slot);
        }

        return slots;
    }

    // A range of channels goes on the wire as: first channel (u16), number of channels (u16).
    // Whether the range exists is for the box to decide; here it only has to fit the fields.
    private static void WriteChannelRange(Span<byte> payload, int start, int count)
    {
        BinaryPrimitives.WriteUInt16BigEndian(payload, ToUInt16(start, nameof(start)));
        BinaryPrimitives.WriteUInt16BigEndian(payload[2..], ToUInt16(count, nameof(count)));
    }

    private static void RequireValues(ReadOnlySpan<byte> values)
    {
        if (values.IsEmpty)
            throw new ArgumentException("At least one value is needed.", nameof(values));
        if (values.Length > ushort.MaxValue)
            throw new ArgumentException("More than 65535 values.", nameof(values));
    }

    private static byte ToByte(int value, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, parameterName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, byte.MaxValue, parameterName);
        return (byte)value;
    }

    private static ushort ToUInt16(int value, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, parameterName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, ushort.MaxValue, parameterName);
        return (ushort)value;
    }

    // The protocol counts durations in whole milliseconds, in 32 bits.
    private static uint ToMilliseconds(TimeSpan duration)
    {
        double milliseconds = Math.Round(duration.TotalMilliseconds);
        if (milliseconds < 0 || milliseconds > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration), duration, "The duration must be between 0 and about 49 days.");
        }

        return (uint)milliseconds;
    }
}
