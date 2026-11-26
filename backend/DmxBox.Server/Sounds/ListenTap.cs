using NAudio.Wave;

namespace DmxBox.Server.Sounds;

/// <summary>
/// The way from the mixer to the device to listen on (LISTEN ON of the Sounds view): the mixer
/// writes what goes out into it, in stereo, as its device reads it; the device to listen on reads it
/// as its own clock says. Two devices never run at quite the same pace, so the reader stays a little
/// behind the writer: when it falls too far behind it jumps ahead, and when it catches up it waits
/// (a moment of silence) until there is enough again.
/// </summary>
/// <remarks>One thread writes (the mixer's), one reads (the device's); no lock between them.</remarks>
internal sealed class ListenTap
{
    // The reader stays this far behind the writer, and jumps back to it when it lags more than twice that.
    private const double BehindSeconds = 0.08;

    private readonly float[] _ring;
    private readonly int _capacity;
    private readonly long _behind;
    private long _written;   // frames written since the start: the writer's alone, read by the reader
    private long _read;      // frames read since the start: the reader's alone
    private bool _waiting = true;

    /// <summary>A tap for a mixer of this rate.</summary>
    /// <param name="rate">The mixer's samples per second.</param>
    public ListenTap(int rate)
    {
        Rate = rate;
        _capacity = Math.Max(1024, rate);   // a second
        _ring = new float[_capacity * 2];
        _behind = Math.Max(1, (long)(rate * BehindSeconds));
    }

    /// <summary>The mixer's samples per second.</summary>
    public int Rate { get; }

    /// <summary>What it gives, in stereo at the mixer's rate.</summary>
    public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(Rate, 2);

    /// <summary>On the mixer's thread: stereo frames (left, right, ...) that went out.</summary>
    public void Write(ReadOnlySpan<float> stereo)
    {
        long written = _written;
        int frames = stereo.Length / 2;
        for (int frame = 0; frame < frames; frame++)
        {
            int at = (int)((written + frame) % _capacity) * 2;
            _ring[at] = stereo[frame * 2];
            _ring[at + 1] = stereo[frame * 2 + 1];
        }

        Volatile.Write(ref _written, written + frames);
    }

    /// <summary>On the listening device's thread: fills stereo frames with what went out, silence where there is nothing yet.</summary>
    /// <returns>The samples filled: all of them.</returns>
    public int Read(Span<float> stereo)
    {
        long written = Volatile.Read(ref _written);
        long ready = written - _read;
        if (ready > 2 * _behind)
        {
            _read = written - _behind;   // fallen behind: on to where it should be
            ready = _behind;
        }

        int frames = stereo.Length / 2;
        if (_waiting && ready < _behind)
            frames = 0;   // after a start or a gap: silence until there is enough
        else
            _waiting = false;

        int given = (int)Math.Min(frames, Math.Max(0, ready));
        for (int frame = 0; frame < given; frame++)
        {
            int at = (int)((_read + frame) % _capacity) * 2;
            stereo[frame * 2] = _ring[at];
            stereo[frame * 2 + 1] = _ring[at + 1];
        }

        _read += given;
        if (given < stereo.Length / 2)
        {
            stereo[(given * 2)..].Clear();
            if (frames > 0)
                _waiting = true;   // caught up with the writer: wait until there is enough again
        }

        return stereo.Length;
    }
}

/// <summary>What a device to listen on plays: the tap, at the device's rate, on its first two outputs.</summary>
internal sealed class ListenSource : ISampleProvider
{
    private readonly ISampleProvider _stereo;
    private readonly int _channels;
    private float[] _pairs = [];

    /// <summary>The tap made fit for a device.</summary>
    /// <param name="tap">The tap.</param>
    /// <param name="rate">The device's samples per second.</param>
    /// <param name="channels">The device's outputs.</param>
    public ListenSource(ListenTap tap, int rate, int channels)
    {
        ISampleProvider source = new TapReader(tap);
        _stereo = rate == tap.Rate ? source : new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(source, rate);
        _channels = Math.Max(2, channels);
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, _channels);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(Span<float> buffer)
    {
        if (_channels == 2)
            return Fill(buffer);

        int frames = buffer.Length / _channels;
        if (_pairs.Length < frames * 2)
            _pairs = new float[frames * 2];

        Span<float> pairs = _pairs.AsSpan(0, frames * 2);
        Fill(pairs);
        buffer.Clear();
        for (int frame = 0; frame < frames; frame++)
        {
            buffer[frame * _channels] = pairs[frame * 2];
            buffer[frame * _channels + 1] = pairs[frame * 2 + 1];
        }

        return frames * _channels;
    }

    // The resampler may give less than asked: it is asked again, and the rest stays silent.
    private int Fill(Span<float> stereo)
    {
        int filled = 0;
        for (int round = 0; filled < stereo.Length && round < 8; round++)
        {
            int read = _stereo.Read(stereo[filled..]);
            if (read <= 0)
                break;
            filled += read;
        }

        stereo[filled..].Clear();
        return stereo.Length;
    }

    private sealed class TapReader(ListenTap tap) : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = tap.WaveFormat;

        public int Read(Span<float> buffer) => tap.Read(buffer);
    }
}
