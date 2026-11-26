using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DmxBox.Server.Sounds;

/// <summary>
/// One sound while it plays: its file read from its start to its end (round again with a loop),
/// at its speed and pitch, with its echo and reverb, as stereo at the mixer's rate. The mixer gives
/// it its volume and pan (<see cref="SoundMixer"/>).
/// </summary>
/// <remarks>
/// Made on any thread (opening the file can take a moment), used by the mixer's thread only.
/// The parts, in the order the sound goes through them: <see cref="FileSource"/> (the file, its
/// start, end and loop, in stereo), <see cref="RateStage"/> (the speed: read faster or slower,
/// which also changes the pitch), <see cref="PitchStage"/> (the pitch put right again, or moved
/// as asked), <see cref="Echo"/> and <see cref="Reverb"/>.
/// </remarks>
internal sealed class SoundVoice : IDisposable
{
    private readonly FileSource _file;
    private readonly RateStage _rate;
    private readonly PitchStage _pitch;
    private readonly Echo _echo;
    private readonly Reverb _reverb;

    /// <summary>Opens the file of a sound, ready to play from its start.</summary>
    /// <param name="sound">The sound.</param>
    /// <param name="path">Its file.</param>
    /// <param name="rate">The mixer's samples per second.</param>
    public SoundVoice(Sound sound, string path, int rate)
    {
        Sound = sound;
        _file = new FileSource(SoundFiles.Open(path), sound);
        _rate = new RateStage(_file, rate, sound.Speed);
        _pitch = new PitchStage(_rate, PitchFactorOf(sound));
        _echo = new Echo(rate);
        _reverb = new Reverb(rate);
    }

    /// <summary>The sound as the voice plays it now.</summary>
    public Sound Sound { get; private set; }

    /// <summary>True once it has played to its end and no loop takes it round again.</summary>
    public bool Ended { get; private set; }

    /// <summary>Where it is in its file, in seconds.</summary>
    public double Position => _file.Position;

    /// <summary>Its gain right now, ramped by the mixer towards what is asked (no clicks).</summary>
    public float Gain { get; set; }

    /// <summary>True while it fades out to be taken away: a sound started again, or stopped.</summary>
    public bool Leaving { get; set; }

    /// <summary>Goes on from another place of its file, in seconds: between its start and its end.</summary>
    public void Seek(double seconds)
    {
        _file.Seek(seconds);
        Ended = false;
    }

    /// <summary>Takes what can change while it plays: its end and loop, speed, pitch, echo and reverb.</summary>
    /// <param name="sound">The sound as it is now.</param>
    public void Apply(Sound sound)
    {
        Sound = sound;
        _file.Apply(sound);
        _rate.Speed = sound.Speed;
        _pitch.Factor = PitchFactorOf(sound);
    }

    /// <summary>
    /// Fills stereo frames (left, right, left, ...) and gives how many samples it filled; fewer than
    /// asked at its end, after which <see cref="Ended"/> is true.
    /// </summary>
    /// <param name="stereo">The frames to fill.</param>
    public int Render(Span<float> stereo)
    {
        int filled = Ended ? 0 : _pitch.Read(stereo);
        if (filled < stereo.Length)
        {
            // the echo and the reverb still ring when the file is over; the voice ends once they are quiet
            stereo[filled..].Clear();
            if (!Ended && _file.Over)
                Ended = Sound.Echo == 0 && Sound.Reverb == 0;
        }

        Sound sound = Sound;
        if (sound.Echo > 0)
            _echo.Process(stereo, sound.EchoTime, sound.EchoRepeats / 100f, sound.Echo / 100f);
        if (sound.Reverb > 0)
            _reverb.Process(stereo, sound.Room / 100f, sound.Reverb / 100f);

        if (filled < stereo.Length && _file.Over && !Ended)
        {
            // the tail of the echo and the reverb: over once it has died away
            float loudest = 0;
            foreach (float sample in stereo)
                loudest = Math.Max(loudest, Math.Abs(sample));
            if (loudest < 0.0005f)
                Ended = true;
        }

        return stereo.Length;
    }

    public void Dispose()
    {
        _file.Dispose();
    }

    // How far the pitch is moved: as asked in semitones, and back by the speed, which moved it too.
    private static float PitchFactorOf(Sound sound)
    {
        return (float)(Math.Pow(2, sound.Pitch / 12.0) / sound.Speed);
    }

    /// <summary>The file in stereo, from the sound's start to its end, round again with a loop.</summary>
    private sealed class FileSource(WaveStream reader, Sound sound) : ISampleProvider, IDisposable
    {
        private readonly ISampleProvider _samples = reader.ToSampleProvider();
        private readonly int _channels = reader.WaveFormat.Channels;
        private float[] _wide = [];
        private Sound _sound = sound;
        private long _frame = -1;   // the frame of the file read next; -1 before the start was found

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(reader.WaveFormat.SampleRate, 2);

        public bool Over { get; private set; }

        public double Position => Math.Max(0, _frame) / (double)WaveFormat.SampleRate;

        public void Apply(Sound sound)
        {
            _sound = sound;
        }

        public int Read(Span<float> buffer)
        {
            int rate = WaveFormat.SampleRate;
            if (_frame < 0)
                SeekTo(_sound.Start);

            int filled = 0;
            for (int round = 0; filled < buffer.Length && !Over && round < 64; round++)
            {
                long end = _sound.End is double seconds ? (long)(seconds * rate) : long.MaxValue;
                long left = end - _frame;
                int frames = (int)Math.Min((buffer.Length - filled) / 2, Math.Max(0, left));
                int read = frames > 0 ? ReadStereo(buffer.Slice(filled, frames * 2)) : 0;
                filled += read * 2;
                _frame += read;
                if (read == frames && _frame < end)
                    continue;   // more of the file to come; the next round reads it

                // its end, or the end of the file
                if (!_sound.Loop)
                {
                    Over = true;
                    break;
                }

                SeekTo(_sound.Start);
                if (read == 0 && round > 0)
                    break;   // a loop that gives nothing (a start past the file's end) must not spin
            }

            return filled;
        }

        public void Dispose()
        {
            reader.Dispose();
        }

        // Another place: from the sound's start to a moment before its end (or the file's end).
        public void Seek(double seconds)
        {
            double end = Math.Min(_sound.End ?? double.MaxValue, reader.TotalTime.TotalSeconds);
            SeekTo(Math.Clamp(seconds, _sound.Start, Math.Max(_sound.Start, end - 0.05)));
            Over = false;
        }

        private void SeekTo(double seconds)
        {
            TimeSpan at = TimeSpan.FromSeconds(Math.Min(seconds, reader.TotalTime.TotalSeconds));
            reader.CurrentTime = at;
            _frame = (long)(at.TotalSeconds * WaveFormat.SampleRate);
        }

        // Reads frames of the file into stereo: a mono file on both sides, of more channels the first two.
        private int ReadStereo(Span<float> stereo)
        {
            int frames = stereo.Length / 2;
            if (_channels == 2)
                return Fill(stereo) / 2;

            int wanted = frames * _channels;
            if (_wide.Length < wanted)
                _wide = new float[wanted];

            Span<float> wide = _wide.AsSpan(0, wanted);
            int read = Fill(wide) / _channels;
            for (int frame = 0; frame < read; frame++)
            {
                float left = wide[frame * _channels];
                stereo[frame * 2] = left;
                stereo[frame * 2 + 1] = _channels == 1 ? left : wide[frame * _channels + 1];
            }

            return read;
        }

        // Reads until the buffer is full or the file is over: a reader may give less than asked.
        private int Fill(Span<float> buffer)
        {
            int filled = 0;
            while (filled < buffer.Length)
            {
                int read = _samples.Read(buffer[filled..]);
                if (read <= 0)
                    break;
                filled += read;
            }

            return filled - filled % _channels;
        }
    }

    /// <summary>
    /// The speed: reads the file faster or slower and gives it at the mixer's rate, as a tape
    /// would (the pitch moves with it; <see cref="PitchStage"/> puts it right).
    /// </summary>
    private sealed class RateStage : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly WdlResampler _resampler = new();
        private readonly int _fileRate;
        private readonly int _rate;
        private double _speed;

        public RateStage(ISampleProvider source, int rate, double speed)
        {
            _source = source;
            _fileRate = source.WaveFormat.SampleRate;
            _rate = rate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, 2);
            _resampler.SetMode(true, 2, false, 64, 32);
            _resampler.SetFilterParms(0.693f, 0.707f);
            _resampler.SetFeedMode(false);
            Speed = speed;
        }

        public WaveFormat WaveFormat { get; }

        public double Speed
        {
            get => _speed;
            set
            {
                if (value == _speed)
                    return;

                _speed = value;
                _resampler.SetRates(_fileRate * value, _rate);
            }
        }

        public int Read(Span<float> buffer)
        {
            if (_speed == 1 && _fileRate == _rate)
                return _source.Read(buffer);   // nothing to change

            int frames = buffer.Length / 2;
            int needed = _resampler.ResamplePrepare(frames, 2, out Span<float> input);
            int read = _source.Read(input[..(needed * 2)]) / 2;
            return _resampler.ResampleOut(buffer, read, frames, 2) * 2;
        }
    }

    /// <summary>The pitch: moved by a phase vocoder where it has to be, passed through where it does not.</summary>
    private sealed class PitchStage : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private SmbPitchShiftingSampleProvider? _shifter;

        public PitchStage(ISampleProvider source, float factor)
        {
            _source = source;
            Factor = factor;
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public float Factor
        {
            get;
            set
            {
                field = value;
                if (Math.Abs(value - 1) < 0.0001f)
                    return;

                _shifter ??= new SmbPitchShiftingSampleProvider(_source, 4096, 4, value);
                _shifter.PitchFactor = value;
            }
        }

        public int Read(Span<float> buffer)
        {
            return _shifter is not null && Math.Abs(Factor - 1) >= 0.0001f ? _shifter.Read(buffer) : _source.Read(buffer);
        }
    }

    /// <summary>An echo: the sound again after its time, each repeat quieter, on top of it.</summary>
    private sealed class Echo(int rate)
    {
        private readonly float[] _buffer = new float[(int)(Sound.MaxEchoTime * rate + 1) * 2];
        private int _write;

        public void Process(Span<float> stereo, double seconds, float repeats, float level)
        {
            int frames = _buffer.Length / 2;
            int delay = Math.Clamp((int)(seconds * rate), 1, frames - 1);
            for (int at = 0; at + 1 < stereo.Length; at += 2)
            {
                int read = ((_write - delay + frames) % frames) * 2;
                for (int side = 0; side < 2; side++)
                {
                    float input = stereo[at + side];
                    float delayed = _buffer[read + side];
                    _buffer[_write * 2 + side] = input + delayed * repeats;
                    stereo[at + side] = input + delayed * level;
                }

                _write = (_write + 1) % frames;
            }
        }
    }

    /// <summary>
    /// A room's reverb, after Jezar's Freeverb (public domain): eight comb filters and four all-pass
    /// filters on each side, the right side a little apart, added on top of the sound.
    /// </summary>
    private sealed class Reverb
    {
        private static readonly int[] CombTuning = [1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617];
        private static readonly int[] AllPassTuning = [556, 441, 341, 225];
        private const int StereoSpread = 23;
        private const float FixedGain = 0.015f;
        private const float Damping = 0.2f;

        private readonly Comb[][] _combs = new Comb[2][];
        private readonly AllPass[][] _allPasses = new AllPass[2][];

        public Reverb(int rate)
        {
            double scale = rate / 44100.0;
            for (int side = 0; side < 2; side++)
            {
                int spread = side * StereoSpread;
                _combs[side] = [.. CombTuning.Select(length => new Comb((int)((length + spread) * scale)))];
                _allPasses[side] = [.. AllPassTuning.Select(length => new AllPass((int)((length + spread) * scale)))];
            }
        }

        public void Process(Span<float> stereo, float room, float level)
        {
            float feedback = 0.7f + room * 0.28f;
            float wet = level * 3;
            for (int at = 0; at + 1 < stereo.Length; at += 2)
            {
                float input = (stereo[at] + stereo[at + 1]) * FixedGain;
                for (int side = 0; side < 2; side++)
                {
                    float sum = 0;
                    foreach (Comb comb in _combs[side])
                        sum += comb.Process(input, feedback, Damping);
                    foreach (AllPass pass in _allPasses[side])
                        sum = pass.Process(sum);

                    stereo[at + side] += sum * wet;
                }
            }
        }

        private sealed class Comb(int length)
        {
            private readonly float[] _buffer = new float[Math.Max(1, length)];
            private int _at;
            private float _store;

            public float Process(float input, float feedback, float damping)
            {
                float output = _buffer[_at];
                _store = output * (1 - damping) + _store * damping;
                _buffer[_at] = input + _store * feedback;
                _at = (_at + 1) % _buffer.Length;
                return output;
            }
        }

        private sealed class AllPass(int length)
        {
            private readonly float[] _buffer = new float[Math.Max(1, length)];
            private int _at;

            public float Process(float input)
            {
                float buffered = _buffer[_at];
                _buffer[_at] = input + buffered * 0.5f;
                _at = (_at + 1) % _buffer.Length;
                return buffered - input;
            }
        }
    }
}
