using NAudio.Wave;

namespace DmxBox.Tests.Sounds;

/// <summary>
/// The sound as the mixer makes it, measured on tones written for the test: the level, the volume,
/// the pan, the master and MUTE, the outputs of music and sound effects, a sound's start, end and
/// loop, its speed with the pitch kept, its pitch with the speed kept, the echo and the reverb.
/// </summary>
public sealed class SoundMixerTests : IDisposable
{
    private const int Rate = 48000;
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("lumenet-sound-");
    private readonly List<SoundMixer> _mixers = [];

    public void Dispose()
    {
        // the voices hold their files open
        foreach (SoundMixer mixer in _mixers)
            mixer.Clear();

        _folder.Delete(recursive: true);
    }

    private SoundMixer Mixer(int channels = 2)
    {
        var mixer = new SoundMixer(Rate, channels);
        _mixers.Add(mixer);
        return mixer;
    }

    // A tone of 440 Hz at half of full, mono.
    private string Tone(double seconds = 1, double frequency = 440, string name = "tone.wav") =>
        Wave(name, time => (float)(0.5 * Math.Sin(2 * Math.PI * frequency * time)), seconds);

    // A short burst of tone at the start, then silence.
    private string Burst(double seconds = 1) =>
        Wave("burst.wav", time => time < 0.01 ? (float)(0.5 * Math.Sin(2 * Math.PI * 1000 * time)) : 0, seconds);

    private string Wave(string name, Func<double, float> signal, double seconds)
    {
        string path = Path.Combine(_folder.FullName, name);
        using var writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(Rate, 1));
        float[] samples = [.. Enumerable.Range(0, (int)(seconds * Rate)).Select(frame => signal(frame / (double)Rate))];
        writer.WriteSamples(samples, 0, samples.Length);
        return path;
    }

    private static Sound SoundOf(string path) =>
        new(1, "Tone", Path.GetFileName(path), "0123456789abcdef0123456789abcdef") { Seconds = 1 };

    // Plays a voice in a mixer, at full level: the first read ramps it up and is not measured.
    private SoundMixer Playing(Sound sound, string path, int channels = 2)
    {
        SoundMixer mixer = Mixer(channels);
        mixer.Attach(sound.Number, new SoundVoice(sound, path, Rate));
        mixer.SetLevel(sound.Number, 1);
        Read(mixer, 0.05);
        return mixer;
    }

    private static float[] Read(SoundMixer mixer, double seconds)
    {
        float[] buffer = new float[(int)(seconds * Rate) * mixer.WaveFormat.Channels];
        mixer.Read(buffer);
        return buffer;
    }

    // The loudness of one output of interleaved frames.
    private static double Rms(float[] frames, int channel, int channels = 2)
    {
        double sum = 0;
        int count = 0;
        for (int at = channel; at < frames.Length; at += channels, count++)
            sum += frames[at] * frames[at];
        return Math.Sqrt(sum / Math.Max(1, count));
    }

    // How often a tone goes up, in a second: from under -30 % of its peak to over +30 %, so that the
    // small ripples of a pitch shifter are not counted.
    private static double Frequency(float[] frames, int channels = 2)
    {
        float peak = 0;
        for (int at = 0; at < frames.Length; at += channels)
            peak = Math.Max(peak, Math.Abs(frames[at]));

        float margin = peak * 0.3f;
        int crossings = 0;
        bool high = frames[0] > 0;
        for (int at = 0; at < frames.Length; at += channels)
        {
            if (!high && frames[at] > margin)
            {
                high = true;
                crossings++;
            }
            else if (high && frames[at] < -margin)
            {
                high = false;
            }
        }

        return crossings / (frames.Length / (double)channels / Rate);
    }

    [Fact]
    public void ASoundPlaysAtItsLevelAndVolumeOnBothSides()
    {
        string path = Tone();
        SoundMixer mixer = Playing(SoundOf(path), path);
        float[] full = Read(mixer, 0.1);
        Assert.InRange(Rms(full, 0), 0.34, 0.37);    // a sine at half: 0.354
        Assert.InRange(Rms(full, 1), 0.34, 0.37);    // mono on both sides

        mixer.SetLevel(1, 0.5);
        Read(mixer, 0.05);
        Assert.InRange(Rms(Read(mixer, 0.1), 0), 0.17, 0.185);

        string half = Tone(name: "half.wav");
        SoundMixer quieter = Playing(SoundOf(half) with { Decibels = -6.0206 }, half);   // half the gain
        Assert.InRange(Rms(Read(quieter, 0.1), 0), 0.17, 0.185);
    }

    [Fact]
    public void PanMasterAndMuteActOnTheOutput()
    {
        string path = Tone();
        SoundMixer mixer = Playing(SoundOf(path) with { Pan = 100 }, path);
        float[] right = Read(mixer, 0.1);
        Assert.Equal(0, Rms(right, 0), 3);
        Assert.InRange(Rms(right, 1), 0.34, 0.37);

        mixer.SetMaster(0.5, mute: false);
        Read(mixer, 0.05);
        Assert.InRange(Rms(Read(mixer, 0.1), 1), 0.17, 0.185);

        mixer.SetMaster(0.5, mute: true);
        Read(mixer, 0.05);
        Assert.Equal(0, Rms(Read(mixer, 0.1), 1), 4);
    }

    [Fact]
    public void TheSoundsGoOutOnTheirPairOfOutputs()
    {
        string path = Tone();
        SoundMixer mixer = Playing(SoundOf(path), path, channels: 4);
        mixer.Route(1);
        float[] frames = Read(mixer, 0.1);
        Assert.Equal(0, Rms(frames, 0, 4), 4);
        Assert.Equal(0, Rms(frames, 1, 4), 4);
        Assert.InRange(Rms(frames, 2, 4), 0.34, 0.37);
        Assert.InRange(Rms(frames, 3, 4), 0.34, 0.37);

        mixer.Route(5);   // a pair the device does not have: the last it has
        Assert.InRange(Rms(Read(mixer, 0.1), 2, 4), 0.34, 0.37);
    }

    [Fact]
    public void ASoundGoesOnFromAnotherPlaceOfItsFileBetweenItsStartAndItsEnd()
    {
        string path = Tone(seconds: 3);
        SoundMixer mixer = Playing(SoundOf(path) with { Seconds = 3, Start = 0.5, End = 2.5 }, path);
        mixer.Seek(1, 2);
        Assert.InRange(mixer.StateOf(1).Position, 1.99, 2.01);
        Read(mixer, 0.1);
        Assert.InRange(mixer.StateOf(1).Position, 2.09, 2.11);
        Assert.InRange(Rms(Read(mixer, 0.1), 0), 0.3, 0.37);   // back from silence over one read, then as loud as before

        mixer.Seek(1, 0);                                       // before its start: its start
        Assert.InRange(mixer.StateOf(1).Position, 0.49, 0.51);
        mixer.Seek(1, 9);                                       // after its end: a moment before it
        Read(mixer, 0.2);
        Assert.False(mixer.StateOf(1).Playing);
    }

    [Fact]
    public void APausedSoundFallsSilentHoldsWhereItIsAndGoesOnFromThere()
    {
        string path = Tone(seconds: 2);
        SoundMixer mixer = Playing(SoundOf(path) with { Seconds = 2 }, path);
        mixer.Pause(1, true);
        Read(mixer, 0.05);                                    // out over one read
        double at = mixer.StateOf(1).Position;
        Assert.Equal(0, Rms(Read(mixer, 0.5), 0), 4);         // silent
        Assert.Equal(at, mixer.StateOf(1).Position, 3);       // held where it was, still playing
        Assert.True(mixer.StateOf(1).Playing);

        mixer.Pause(1, false);
        Read(mixer, 0.05);                                    // in again over one read
        Assert.InRange(Rms(Read(mixer, 0.1), 0), 0.34, 0.37);
        Assert.InRange(mixer.StateOf(1).Position, at + 0.14, at + 0.16);
    }

    [Fact]
    public void ASoundPlaysFromItsStartToItsEndOrRoundAgainInALoop()
    {
        string path = Tone(seconds: 1);
        SoundMixer mixer = Playing(SoundOf(path) with { Start = 0.5 }, path);
        Assert.InRange(mixer.StateOf(1).Position, 0.54, 0.56);
        Read(mixer, 0.3);
        Assert.True(mixer.StateOf(1).Playing);
        Read(mixer, 0.3);                            // past its end: over
        Assert.False(mixer.StateOf(1).Playing);
        Assert.Equal(0, Rms(Read(mixer, 0.1), 0), 4);

        SoundMixer looping = Playing(SoundOf(path) with { Start = 0.2, End = 0.4, Loop = true }, path);
        for (int round = 0; round < 10; round++)
        {
            Read(looping, 0.1);
            Assert.True(looping.StateOf(1).Playing);
            Assert.InRange(looping.StateOf(1).Position, 0.2, 0.4);
        }

        Assert.InRange(Rms(Read(looping, 0.1), 0), 0.3, 0.37);
    }

    [Fact]
    public void SpeedKeepsThePitchAndPitchKeepsTheSpeed()
    {
        string path = Tone(seconds: 2);
        SoundMixer faster = Playing(SoundOf(path) with { Speed = 2 }, path);
        Read(faster, 0.15);                                      // the pitch shifter's own delay
        Assert.InRange(Frequency(Read(faster, 0.4)), 400, 480);   // twice as fast, still 440 Hz
        Read(faster, 0.6);                                       // two seconds of file in about one
        Assert.False(faster.StateOf(1).Playing);

        SoundMixer higher = Playing(SoundOf(path) with { Pitch = 12 }, path);
        Read(higher, 0.15);
        Assert.InRange(Frequency(Read(higher, 0.4)), 800, 960);   // an octave up
        Read(higher, 1);
        Assert.True(higher.StateOf(1).Playing);                  // and as long as it was
    }

    [Fact]
    public void AnEchoRepeatsTheSoundAfterItsTimeAndAReverbLeavesATail()
    {
        string path = Burst();
        SoundMixer mixer = Mixer();
        mixer.Attach(1, new SoundVoice(SoundOf(path) with { Echo = 100, EchoTime = 0.3, EchoRepeats = 0 }, path, Rate));
        mixer.SetLevel(1, 1);
        mixer.SetMaster(1, mute: false);
        float[] start = Read(mixer, 0.3);
        float[] echo = Read(mixer, 0.05);
        Assert.Equal(0, Rms(start[(int)(0.1 * Rate * 2)..], 0), 3);   // silence after the burst
        Assert.True(Rms(echo[..(int)(0.01 * Rate * 2)], 0) > 0.05);  // the burst again at 0.3 s

        SoundMixer dry = Mixer();
        dry.Attach(1, new SoundVoice(SoundOf(path), path, Rate));
        dry.SetLevel(1, 1);
        Read(dry, 0.05);
        Assert.Equal(0, Rms(Read(dry, 0.2), 0), 4);

        SoundMixer wet = Mixer();
        wet.Attach(1, new SoundVoice(SoundOf(path) with { Reverb = 100, Room = 80 }, path, Rate));
        wet.SetLevel(1, 1);
        Read(wet, 0.05);
        Assert.True(Rms(Read(wet, 0.2), 0) > 0.001);                  // the room still rings
    }

    [Fact]
    public void TheWaveOfAFileShowsWhereItIsLoudAndHowLongItIs()
    {
        // a second at half of full, a second at a tenth, a second of silence
        string path = Wave("steps.wav", time => (float)((time < 1 ? 0.5 : time < 2 ? 0.1 : 0) * Math.Sin(2 * Math.PI * 440 * time)), 3);
        SoundWave wave = SoundWave.Measure(path);

        Assert.Equal(3, wave.Seconds, 2);
        Assert.Equal(SoundWave.Parts, wave.Peaks.Length);
        Assert.Equal(SoundWave.Parts, wave.Levels.Length);
        Assert.All(wave.Peaks[10..320], peak => Assert.InRange(peak, 124, 128));          // 0.5 of 255
        Assert.All(wave.Levels[10..320], level => Assert.InRange(level, 87, 93));        // 0.5 / √2
        Assert.All(wave.Peaks[345..650], peak => Assert.InRange(peak, 24, 27));
        Assert.All(wave.Peaks[680..], peak => Assert.Equal(0, peak));
    }

    [Fact]
    public void WhatGoesOutGoesToTheDeviceToListenOnAsWellInStereo()
    {
        string path = Tone(seconds: 2);
        SoundMixer mixer = Playing(SoundOf(path), path, channels: 4);
        mixer.Route(1);   // on outputs 3-4
        var tap = new ListenTap(Rate);
        mixer.Tap(tap);
        float[] outputs = Read(mixer, 0.2);

        float[] heard = new float[(int)(0.05 * Rate) * 2];
        tap.Read(heard);
        Assert.InRange(Rms(heard, 0), 0.34, 0.37);   // the tone at half of full: 0.5 / √2
        Assert.Equal(Rms(heard, 0), Rms(heard, 1), 3);
        Assert.Equal(Rms(outputs, 2, 4), Rms(heard, 0), 2);

        // MUTE: nothing goes out, and nothing is heard
        mixer.SetMaster(1, mute: true);
        Read(mixer, 0.05);
        float[] muted = Read(mixer, 0.2);
        tap.Read(heard);
        tap.Read(heard);
        Assert.Equal(0, Rms(muted, 2, 4), 4);
        Assert.Equal(0, Rms(heard, 0), 4);
    }

    [Fact]
    public void TheDeviceToListenOnStaysALittleBehindAndKeepsUp()
    {
        // at 1000 frames a second it stays 80 frames behind, and jumps on beyond 160
        var tap = new ListenTap(1000);
        float[] Frames(int from, int count) => [.. Enumerable.Range(from, count).SelectMany(frame => new[] { frame / 10000f, -frame / 10000f })];
        float[] read = new float[20];

        tap.Write(Frames(0, 50));
        tap.Read(read);
        Assert.All(read, sample => Assert.Equal(0, sample));    // not enough yet: silence

        tap.Write(Frames(50, 50));
        tap.Read(read);
        Assert.Equal(Frames(0, 10), read);                      // from the first frame on

        float[] longer = new float[200];
        tap.Read(longer);
        Assert.Equal(Frames(10, 90), longer[..180]);           // what there is, then silence
        Assert.All(longer[180..], sample => Assert.Equal(0, sample));

        tap.Write(Frames(100, 400));                            // far ahead now: on to 80 behind
        tap.Read(read);
        Assert.Equal(Frames(420, 10), read);
    }
}
