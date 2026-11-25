using System.Collections.Concurrent;
using NAudio.Wave;

namespace DmxBox.Server;

/// <summary>
/// Mixes the sounds that play into what goes out: each sound at its level and its own level in
/// decibels, panned, onto one pair of outputs, under the volume of all sounds and MUTE.
/// </summary>
/// <remarks>
/// <para>
/// The output's thread reads it (<see cref="Read"/>) and is the only one that touches the voices.
/// Everything else posts its changes (<see cref="Post"/>), which the next read takes in first; the
/// levels and the master are plain numbers it reads each time. A gain never jumps: it moves to
/// what is asked over the frames of one read, so nothing clicks.
/// </para>
/// </remarks>
internal sealed class SoundMixer : ISampleProvider
{
    // How long a voice takes to fade out when it is stopped or started again, in seconds.
    private const double LeaveSeconds = 0.015;

    private readonly ConcurrentQueue<Action> _posted = new();
    private readonly Dictionary<int, SoundVoice> _voices = [];
    private readonly List<SoundVoice> _leaving = [];
    private readonly double[] _levels = new double[Sound.MaxSounds];
    private readonly SoundVoiceState[] _states = new SoundVoiceState[Sound.MaxSounds];
    private readonly Lock _statesLock = new();
    private float[] _voiceBuffer = [];
    private float[] _bus = [];
    private float[] _listen = [];
    private ListenTap? _tap;
    private float _master = 1;
    private double _volume = 1;
    private bool _mute;
    private int _pair;

    /// <summary>A mixer of the given rate and outputs.</summary>
    /// <param name="rate">Samples per second.</param>
    /// <param name="channels">Outputs, 2 or more.</param>
    public SoundMixer(int rate, int channels)
    {
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Math.Max(2, channels));
    }

    public WaveFormat WaveFormat { get; private set; }

    /// <summary>Hands a change to the output's thread, which makes it before its next read.</summary>
    public void Post(Action change)
    {
        _posted.Enqueue(change);
    }

    /// <summary>The level of a sound, 0 to 1: its fader, from any thread. Its own level in decibels comes on top.</summary>
    public void SetLevel(int number, double level)
    {
        Volatile.Write(ref _levels[number - 1], Math.Clamp(level, 0, 1));
    }

    /// <summary>The master volume as a gain (0 to 1, which is 0 dB) and MUTE, from any thread.</summary>
    public void SetMaster(double volume, bool mute)
    {
        Volatile.Write(ref _volume, Math.Clamp(volume, 0, 1));
        Volatile.Write(ref _mute, mute);
    }

    /// <summary>The pair of outputs the sounds go out on, counted from 0, from any thread.</summary>
    public void Route(int pair)
    {
        Volatile.Write(ref _pair, Math.Max(0, pair));
    }

    /// <summary>
    /// Where a copy of what goes out goes as well, in stereo (under the volume of all sounds and MUTE):
    /// the device to listen on; null for none. From any thread.
    /// </summary>
    public void Tap(ListenTap? tap)
    {
        Volatile.Write(ref _tap, tap);
    }

    /// <summary>Whether a sound plays, and where it is; from any thread.</summary>
    public SoundVoiceState StateOf(int number)
    {
        lock (_statesLock)
        {
            return _states[number - 1];
        }
    }

    /// <summary>Marks a sound as on its way: started, its file still being opened.</summary>
    public void MarkStarting(int number)
    {
        lock (_statesLock)
        {
            _states[number - 1] = new SoundVoiceState(Playing: true, Position: 0, Missing: false);
        }
    }

    /// <summary>Marks a sound as stopped, at once: its voice fades out on the output's thread.</summary>
    public void MarkStopped(int number)
    {
        lock (_statesLock)
        {
            _states[number - 1] = default;
        }
    }

    /// <summary>Marks a sound whose file could not be found or opened.</summary>
    public void MarkMissing(int number)
    {
        lock (_statesLock)
        {
            _states[number - 1] = new SoundVoiceState(Playing: false, Position: 0, Missing: true);
        }
    }

    /// <summary>
    /// On the output's thread: the voice of a sound goes on from another place of its file, coming in
    /// again from silence over one read, so that the jump does not click.
    /// </summary>
    public void Seek(int number, double seconds)
    {
        if (!_voices.TryGetValue(number, out SoundVoice? voice))
            return;

        voice.Seek(seconds);
        voice.Gain = 0;
        lock (_statesLock)
            _states[number - 1] = new SoundVoiceState(Playing: true, voice.Position, Missing: false);
    }

    /// <summary>On the output's thread: a voice for a sound, in place of the one it had (which fades out).</summary>
    public void Attach(int number, SoundVoice voice)
    {
        Detach(number);
        voice.Gain = 0;
        _voices[number] = voice;
    }

    /// <summary>On the output's thread: the voice of a sound fades out and goes.</summary>
    public void Detach(int number)
    {
        if (!_voices.Remove(number, out SoundVoice? voice))
            return;

        voice.Leaving = true;
        _leaving.Add(voice);
    }

    /// <summary>On the output's thread: what a voice plays changes, as far as it can while it plays.</summary>
    public void Apply(int number, Sound sound)
    {
        if (_voices.TryGetValue(number, out SoundVoice? voice))
            voice.Apply(sound);
    }

    /// <summary>On the output's thread: every voice goes at once (the output closes, or the desk program ends).</summary>
    public void Clear()
    {
        foreach (SoundVoice voice in _voices.Values.Concat(_leaving))
            voice.Dispose();

        _voices.Clear();
        _leaving.Clear();
        lock (_statesLock)
        {
            Array.Clear(_states);
        }
    }

    /// <summary>On the output's thread, before it reads: the mixer's rate and outputs for a new device.</summary>
    public void Reshape(int rate, int channels)
    {
        Clear();
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(rate, Math.Max(2, channels));
    }

    public int Read(Span<float> buffer)
    {
        while (_posted.TryDequeue(out Action? change))
            change();

        int channels = WaveFormat.Channels;
        int frames = buffer.Length / channels;
        buffer.Clear();
        if (frames == 0)
            return buffer.Length;

        // the bus of all sounds, in stereo
        if (_bus.Length < frames * 2)
            _bus = new float[frames * 2];
        if (_voiceBuffer.Length < frames * 2)
            _voiceBuffer = new float[frames * 2];

        Span<float> bus = _bus.AsSpan(0, frames * 2);
        bus.Clear();

        float leaveStep = (float)(1 / (LeaveSeconds * WaveFormat.SampleRate));
        foreach ((int number, SoundVoice voice) in _voices)
        {
            float target = (float)(Volatile.Read(ref _levels[number - 1]) * Sound.GainOf(voice.Sound.Decibels));
            MixVoice(voice, bus, frames, target, 0);
        }

        for (int index = _leaving.Count - 1; index >= 0; index--)
        {
            SoundVoice voice = _leaving[index];
            MixVoice(voice, bus, frames, 0, leaveStep);
            if (voice.Gain <= 0)
            {
                voice.Dispose();
                _leaving.RemoveAt(index);
            }
        }

        // the bus onto its pair of outputs, under the master
        int pair = Math.Min(Volatile.Read(ref _pair), channels / 2 - 1);
        float target2 = Volatile.Read(ref _mute) ? 0 : (float)Volatile.Read(ref _volume);
        float master = _master;
        float masterStep = (target2 - master) / frames;
        ListenTap? tap = Volatile.Read(ref _tap);
        if (tap is not null && _listen.Length < frames * 2)
            _listen = new float[frames * 2];
        for (int frame = 0; frame < frames; frame++)
        {
            master += masterStep;
            int at = frame * channels;
            buffer[at + pair * 2] += bus[frame * 2] * master;
            buffer[at + pair * 2 + 1] += bus[frame * 2 + 1] * master;
            if (tap is not null)
            {
                _listen[frame * 2] = Math.Clamp(bus[frame * 2] * master, -1f, 1f);
                _listen[frame * 2 + 1] = Math.Clamp(bus[frame * 2 + 1] * master, -1f, 1f);
            }
        }

        _master = target2;
        for (int index = 0; index < buffer.Length; index++)
            buffer[index] = Math.Clamp(buffer[index], -1f, 1f);

        tap?.Write(_listen.AsSpan(0, frames * 2));

        PublishStates();
        return buffer.Length;
    }

    // Renders one voice into its bus at its gain, moving the gain towards "target" over the frames
    // (or down by "fall" each frame while it leaves), panned.
    private void MixVoice(SoundVoice voice, Span<float> bus, int frames, float target, float fall)
    {
        Span<float> stereo = _voiceBuffer.AsSpan(0, frames * 2);
        voice.Render(stereo);
        Sound sound = voice.Sound;
        float left = sound.Pan > 0 ? 1 - sound.Pan / 100f : 1;
        float right = sound.Pan < 0 ? 1 + sound.Pan / 100f : 1;
        float gain = voice.Gain;
        float step = fall > 0 ? -fall : (target - gain) / frames;
        for (int frame = 0; frame < frames; frame++)
        {
            gain = Math.Max(0, gain + step);
            bus[frame * 2] += stereo[frame * 2] * gain * left;
            bus[frame * 2 + 1] += stereo[frame * 2 + 1] * gain * right;
        }

        voice.Gain = fall > 0 ? gain : target;
    }

    // What the desk asks: which sounds play and where they are; a voice at its end goes.
    private void PublishStates()
    {
        int[] ended = [.. _voices.Where(pair => pair.Value.Ended).Select(pair => pair.Key)];
        lock (_statesLock)
        {
            foreach ((int number, SoundVoice voice) in _voices)
                _states[number - 1] = new SoundVoiceState(Playing: !voice.Ended, voice.Position, Missing: false);

            foreach (int number in ended)
                _states[number - 1] = _states[number - 1] with { Playing = false };
        }

        foreach (int number in ended)
        {
            _voices.Remove(number, out SoundVoice? voice);
            voice?.Dispose();
        }
    }
}

/// <summary>Whether a sound plays, as the mixer tells the desk.</summary>
/// <param name="Playing">True from its start until its end, or until it is stopped.</param>
/// <param name="Position">Where it is in its file, in seconds.</param>
/// <param name="Missing">True when its file could not be found or opened, or its track cannot be played (see <see cref="UiTrackPlayer"/>).</param>
/// <param name="Length">For a track of the mixer: how long it is, in seconds, as the mixer said; 0 when unknown.</param>
internal readonly record struct SoundVoiceState(bool Playing, double Position, bool Missing, double Length = 0);
