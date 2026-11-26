// The sound files of the example projects "8 Playing sounds", "9 Light and sound cues" and "10 Cues on
// the music", made from nothing: noise, filters and sine waves, no recordings and no samples. Rain and
// wind are loops (WAV, so that they go round without a gap); thunder, a church bell, applause and two
// short pieces of music are MP3, made by Windows' own encoder (so this runs on Windows only).
//
//     dotnet run --project backend/ExampleSounds -- <folder>
//
// writes the seven files into the folder, and says of each how long it is and its hash (the start of
// its SHA-256, as the desk program keeps it). The same seed gives the same sounds; the MP3 encoder of
// another Windows may give other bytes, and so other hashes than the project files of the examples
// name: then open each example in the desk with its folder of sounds, and save it again.
using System.Runtime.Versioning;
using System.Security.Cryptography;
using NAudio.MediaFoundation;
using NAudio.Wave;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("The MP3 files need Windows' own encoder: run this on Windows.");
    return 1;
}

string folder = args.Length > 0 ? args[0] : "out";
Directory.CreateDirectory(folder);
string work = Directory.CreateTempSubdirectory("lumenet-example-sounds-").FullName;   // the WAV files the MP3 files are made of
var random = new Random(20261125);
try
{
    Save("rain.wav", Rain(random), 22050, 1, 0, -6);
    Save("wind.wav", Wind(random), 22050, 1, 0, -4);
    Save("thunder.mp3", Thunder(random), 44100, 1, 96000, -1);
    Save("church-bell.mp3", Bell(random), 44100, 1, 96000, -1.5);
    Save("applause.mp3", Applause(random), 44100, 1, 96000, -2);
    Save("calm-night.mp3", Music(random), 44100, 2, 128000, -1);
    Save("puppet-dance.mp3", Dance(random), 44100, 2, 128000, -1);   // last: the others stay as they were
}
finally
{
    Directory.Delete(work, recursive: true);
}

return 0;

[SupportedOSPlatform("windows")]
void Save(string name, float[] samples, int rate, int channels, int bitrate, double peakDb)
{
    Normalize(samples, peakDb);
    string path = Path.Combine(folder, name);
    string wav = bitrate == 0 ? path : Path.Combine(work, Path.GetFileNameWithoutExtension(name) + ".wav");
    WriteWav(wav, samples, rate, channels, random);
    if (bitrate > 0)
    {
        MediaFoundationApi.Startup();
        using var reader = new WaveFileReader(wav);
        MediaFoundationEncoder.EncodeToMp3(reader, path, bitrate);
    }

    // as the desk program will see it (SoundStore.Probe)
    double seconds;
    using (WaveStream check = bitrate == 0 ? new WaveFileReader(path) : new MediaFoundationReader(path))
        seconds = Math.Round(check.TotalTime.TotalSeconds, 3);
    string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))[..32];
    long bytes = new FileInfo(path).Length;
    Console.WriteLine(FormattableString.Invariant($"{name}: {seconds:0.000} s, {bytes / 1024} KB, {hash}"));
}

// ---------------------------------------------------------------- the sounds

// Rain: a hiss and many small drops, 8 seconds that go round.
static float[] Rain(Random random)
{
    const int rate = 22050;
    int n = rate * 8;
    float[] white = Noise(random, n);
    var hiss = new float[n];
    Biquad band = Biquad.BandPass(rate, 2600, 0.5), low = Biquad.LowPass(rate, 7000, 0.707);
    for (int pass = 0; pass < 2; pass++)   // twice round: the filters end as they begin
    {
        for (int i = 0; i < n; i++)
        {
            float y = low.Run(band.Run(white[i]));
            if (pass == 1)
                hiss[i] = y;
        }
    }

    var drops = new float[n];
    for (int k = 0; k < 8 * 170; k++)
    {
        int at = random.Next(n);
        double tau = (0.6 + random.NextDouble() * 2.2) / 1000;
        double amp = 0.15 + Math.Pow(random.NextDouble(), 3) * 0.85;
        Biquad ring = Biquad.BandPass(rate, 1500 + random.NextDouble() * 5000, 2 + random.NextDouble() * 5);
        int length = (int)(tau * 8 * rate) + 40;
        for (int i = 0; i < length; i++)
        {
            double env = Math.Exp(-(i / (double)rate) / tau);
            drops[(at + i) % n] += (float)(ring.Run((float)((random.NextDouble() * 2 - 1) * env)) * amp);
        }
    }

    var result = new float[n];
    for (int i = 0; i < n; i++)
    {
        double t = i / (double)rate;
        double swell = 0.8 + 0.12 * Math.Sin(2 * Math.PI * t / 8) + 0.08 * Math.Sin(2 * Math.PI * 3 * t / 8 + 1);
        result[i] = (float)(hiss[i] * 0.55 * swell + drops[i] * 1.6);
    }

    return result;
}

// Wind: noise through a band that wanders up and down, in gusts, over a low rumble; 8 seconds round.
static float[] Wind(Random random)
{
    const int rate = 22050;
    int n = rate * 8;
    float[] pink = Pink(random, n);
    float[] brown = Brown(random, n);
    var whistle = new float[n];
    var rumble = new float[n];
    var band = new Biquad();
    Biquad low = Biquad.LowPass(rate, 160, 0.707);
    for (int pass = 0; pass < 2; pass++)
    {
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)rate;
            if (i % 16 == 0)
                band.SetBandPass(rate, 420 + 260 * Math.Sin(2 * Math.PI * t / 8 + 0.4) + 140 * Math.Sin(2 * Math.PI * 3 * t / 8 + 1.7), 3);

            float w = band.Run(pink[i]);
            float r = low.Run(brown[i]);
            if (pass == 1)
            {
                whistle[i] = w;
                rumble[i] = r;
            }
        }
    }

    var result = new float[n];
    for (int i = 0; i < n; i++)
    {
        double t = i / (double)rate;
        double gust = 0.55 + 0.3 * Math.Sin(2 * Math.PI * 2 * t / 8 + 0.5) + 0.15 * Math.Sin(2 * Math.PI * 5 * t / 8 + 2.1);
        result[i] = (float)(whistle[i] * gust + rumble[i] * (0.6 + 0.4 * gust) * 0.8);
    }

    return result;
}

// Thunder: a crack, then a deep rumble that rolls a few times and dies away.
static float[] Thunder(Random random)
{
    const int rate = 44100;
    int n = rate * 6;
    var result = new float[n];
    Biquad high = Biquad.HighPass(rate, 1800, 0.707);
    for (int i = 0; i < (int)(0.25 * rate); i++)
    {
        double t = i / (double)rate;
        double env = t < 0.003 ? t / 0.003 : Math.Exp(-(t - 0.003) / 0.05);
        result[i] += (float)(high.Run((float)(random.NextDouble() * 2 - 1)) * env * 0.4);
    }

    Biquad low = Biquad.LowPass(rate, 220, 0.9), low2 = Biquad.LowPass(rate, 220, 0.6), mid = Biquad.BandPass(rate, 500, 0.8);
    double[] rolls = [0.12, 0.55, 1.2, 1.9, 2.8, 3.7];
    double[] heights = [1.0, 0.8, 0.9, 0.6, 0.45, 0.3];
    double brown = 0;
    for (int i = 0; i < n; i++)
    {
        double t = i / (double)rate;
        double white = random.NextDouble() * 2 - 1;
        brown = (brown + 0.02 * white) / 1.02;
        double env = 0;
        for (int r = 0; r < rolls.Length; r++)
        {
            double d = (t - rolls[r]) / (0.25 + 0.12 * r);
            env += heights[r] * Math.Exp(-d * d);
        }

        env *= Math.Exp(-t / 2.6) * Math.Min(1, t / 0.04);
        double deep = low2.Run(low.Run((float)(brown * 3.5)));
        double crackle = mid.Run((float)white) * (0.15 + 0.1 * Math.Sin(t * 37));
        result[i] += (float)((deep * 2.2 + crackle * 0.4) * env);
    }

    FadeOut(result, rate, 1, 0.8);
    return result;
}

// A church bell: the partials of a bell (hum, prime, minor third, fifth, nominal and above), each of
// two halves that beat slowly, each dying away at its own pace; and the knock of the clapper.
static float[] Bell(Random random)
{
    const int rate = 44100;
    int n = rate * 7;
    const double prime = 293.66;   // D4
    (double Ratio, double Amp, double Decay)[] partials =
    [
        (0.5, 0.55, 5.5), (1.0, 0.7, 4.0), (1.183, 0.5, 3.4), (1.506, 0.28, 2.6), (2.0, 0.8, 3.0), (2.514, 0.3, 1.8),
        (2.662, 0.25, 1.6), (3.011, 0.2, 1.4), (4.166, 0.15, 1.0), (5.433, 0.1, 0.7), (6.796, 0.07, 0.5),
    ];
    var result = new float[n];
    foreach ((double ratio, double amp, double decay) in partials)
    {
        double f = prime * ratio;
        double phase1 = random.NextDouble() * 2 * Math.PI, phase2 = random.NextDouble() * 2 * Math.PI;
        double beat = 0.4 + random.NextDouble() * 0.8;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)rate;
            double env = Math.Exp(-t / decay) * Math.Min(1, t / 0.002);
            double v = Math.Sin(2 * Math.PI * (f - beat / 2) * t + phase1) + Math.Sin(2 * Math.PI * (f + beat / 2) * t + phase2);
            result[i] += (float)(v * 0.5 * amp * env);
        }
    }

    Biquad knock = Biquad.BandPass(rate, 2500, 0.7);
    for (int i = 0; i < (int)(0.03 * rate); i++)
    {
        double t = i / (double)rate;
        result[i] += (float)(knock.Run((float)(random.NextDouble() * 2 - 1)) * Math.Exp(-t / 0.006) * 0.8);
    }

    FadeOut(result, rate, 1, 1.0);
    return result;
}

// Applause: many claps, each a short burst of noise of its own color; they grow, hold and thin out.
static float[] Applause(Random random)
{
    const int rate = 44100;
    int n = rate * 6;
    var result = new float[n];
    double at = 0.05;
    while (true)
    {
        double density = at < 0.7 ? 15 + 125 * (at / 0.7) : at < 3.6 ? 140 : Math.Max(4, 140 * (1 - (at - 3.6) / 2.0));
        at += -Math.Log(1 - random.NextDouble()) / density;
        if (at >= 5.8)
            break;

        int from = (int)(at * rate);
        double tau = (3 + random.NextDouble() * 6) / 1000;
        double amp = 0.3 + random.NextDouble() * 0.7;
        double overall = at < 0.7 ? 0.6 + 0.4 * at / 0.7 : at > 3.6 ? Math.Max(0.25, 1 - (at - 3.6) / 2.4) : 1;
        Biquad clap = Biquad.BandPass(rate, 900 + random.NextDouble() * 1800, 0.8 + random.NextDouble() * 1.4);
        int length = (int)(tau * 7 * rate) + 30;
        for (int i = 0; i < length && from + i < n; i++)
        {
            double s = i / (double)rate;
            double env = Math.Min(1, s / 0.0004) * Math.Exp(-s / tau);
            result[from + i] += (float)(clap.Run((float)(random.NextDouble() * 2 - 1)) * env * amp * overall);
        }
    }

    FadeOut(result, rate, 1, 0.4);
    return result;
}

// "Calm night": A minor, F, C, G twice (the second time ending on E), then A minor held, at 84 beats a
// minute. A soft pad from the start, plucked notes from left and right, the bass from the third bar,
// a soft kick from the fifth. Stereo, about 25 seconds.
static float[] Music(Random random)
{
    const int rate = 44100;
    double beat = 60.0 / 84, bar = beat * 4, tail = 2.6;
    (int Bass, int[] Pad, int[] Arp)[] song =
    [
        (45, [57, 60, 64], [69, 72, 76, 81]),
        (41, [57, 60, 65], [65, 69, 72, 77]),
        (48, [55, 60, 64], [67, 72, 76, 79]),
        (43, [55, 59, 62], [67, 71, 74, 79]),
        (45, [57, 60, 64], [69, 72, 76, 81]),
        (41, [57, 60, 65], [65, 69, 72, 77]),
        (48, [55, 60, 64], [67, 72, 76, 79]),
        (40, [56, 59, 64], [64, 68, 71, 76]),
        (45, [57, 60, 64], [69, 72, 76, 81]),
    ];
    int frames = (int)((song.Length * bar + tail) * rate);
    var left = new double[frames];
    var right = new double[frames];
    int[] order = [0, 1, 2, 3, 2, 1, 2, 3];
    for (int b = 0; b < song.Length; b++)
    {
        (int bass, int[] pad, int[] arp) = song[b];
        double start = b * bar;
        bool last = b == song.Length - 1;
        foreach (int note in pad)
            Pad(left, right, rate, Hz(note), start, last ? bar + tail - 1 : bar + 0.25, 0.05);

        if (b >= 2)
        {
            Bass(left, right, rate, Hz(bass), start, last ? bar + tail - 1.2 : beat * 2 - 0.05, 0.3);
            if (!last)
                Bass(left, right, rate, Hz(bass), start + beat * 2, beat * 2 - 0.05, 0.24);
        }

        int notes = last ? 4 : 8;
        for (int k = 0; k < notes; k++)
        {
            double velocity = (k % 2 == 0 ? 0.9 : 0.65) * (last ? 1 - k * 0.15 : 1);
            double at = start + k * (last ? beat : beat / 2);
            Pluck(left, right, rate, Hz(arp[last ? k : order[k]]), at, velocity * 0.16, k % 2 == 0 ? -0.35 : 0.35);
        }

        if (b >= 4 && !last)
        {
            Kick(left, right, rate, start, 0.42);
            Kick(left, right, rate, start + 2 * beat, 0.32);
        }
    }

    var result = new float[frames * 2];
    for (int i = 0; i < frames; i++)
    {
        result[i * 2] = (float)left[i];
        result[i * 2 + 1] = (float)right[i];
    }

    FadeOut(result, rate, 2, 2.5);
    return result;
}

// "Puppet dance", for the cues that come at moments of the music: A minor, F, C, G at 120 beats a minute,
// a bar every 2 seconds. A soft intro, the bass and a kick from 0:08, claps, hi-hats and a tune from 0:16,
// a break that rises from 0:24 with a clap at 0:26 and at 0:27, the last chorus with a crash from 0:28, and
// a chord at 0:36 that dies away. Stereo, about 40 seconds.
static float[] Dance(Random random)
{
    const int rate = 44100;
    const double beat = 0.5, bar = 2, tail = 3.5;
    const int bars = 18;
    int[][] pads = [[57, 60, 64], [57, 60, 65], [55, 60, 64], [55, 59, 62]];
    int[] basses = [45, 41, 48, 43];
    int[][] arps = [[69, 72, 76, 81], [65, 69, 72, 77], [67, 72, 76, 79], [67, 71, 74, 79]];
    (double Beat, int Note)[][] tune =
    [
        [(0, 76), (1, 72), (2, 69), (3, 72), (3.5, 76)],
        [(0, 77), (1, 76), (2, 72), (3, 69)],
        [(0, 79), (1, 76), (2, 72), (3, 76), (3.5, 79)],
        [(0, 74), (1, 71), (2, 67), (3, 71), (3.5, 74)],
    ];
    int[] order = [0, 1, 2, 3, 2, 1, 2, 3];
    int frames = (int)((bars * bar + tail) * rate);
    var left = new double[frames];
    var right = new double[frames];
    for (int b = 0; b < bars; b++)
    {
        int chord = b % 4;
        double start = b * bar;
        bool intro = b < 4, verse = b is >= 4 and < 8, pause = b is 12 or 13, full = b is >= 8 and < 12 or >= 14;
        foreach (int note in pads[pause ? 0 : chord])
            Pad(left, right, rate, Hz(note), start, bar + 0.25, pause ? 0.03 : 0.04);

        if (!intro && !pause)
        {
            Bass(left, right, rate, Hz(basses[chord]), start, beat * 2 - 0.05, 0.3);
            Bass(left, right, rate, Hz(basses[chord]), start + beat * 2, beat * 2 - 0.05, 0.25);
        }

        // plucked notes: quarters from the third bar of the intro, eighths after it, none in the break
        if (!pause && b >= 2)
        {
            int notes = intro ? 4 : 8;
            for (int k = 0; k < notes; k++)
            {
                double at = start + k * (intro ? beat : beat / 2);
                double velocity = (k % 2 == 0 ? 0.9 : 0.65) * (intro ? 0.11 : 0.13);
                Pluck(left, right, rate, Hz(arps[chord][intro ? k : order[k]]), at, velocity, k % 2 == 0 ? -0.35 : 0.35);
            }
        }

        if (verse)
        {
            Kick(left, right, rate, start, 0.45);
            Kick(left, right, rate, start + 2 * beat, 0.4);
        }

        if (full)
        {
            for (int k = 0; k < 4; k++)
            {
                Kick(left, right, rate, start + k * beat, 0.45);
                Hat(left, right, rate, random, start + k * beat + beat / 2, 0.16);
            }

            Clap(left, right, rate, random, start + beat, 0.5);
            Clap(left, right, rate, random, start + 3 * beat, 0.5);
            foreach ((double at, int note) in tune[chord])
                Pluck(left, right, rate, Hz(note), start + at * beat, 0.2, 0);
        }
    }

    // the break: a rise from 0:24 to 0:28, and a clap with a kick at 0:26 and at 0:27
    Riser(left, right, rate, random, 12 * bar, 2 * bar, 0.22);
    foreach (double at in new[] { 13 * bar, 13 * bar + 2 * beat })
    {
        Clap(left, right, rate, random, at, 0.65);
        Kick(left, right, rate, at, 0.45);
    }

    // the last chorus starts with a crash; the end is a chord of A minor that dies away
    Crash(left, right, rate, random, 14 * bar, 2.5, 0.3);
    double end = bars * bar;
    foreach (int note in pads[0])
        Pad(left, right, rate, Hz(note), end, 1.2, 0.05);
    foreach (int note in arps[0])
        Pluck(left, right, rate, Hz(note), end, 0.13, 0);
    Bass(left, right, rate, Hz(45), end, 2, 0.35);
    Kick(left, right, rate, end, 0.5);
    Crash(left, right, rate, random, end, 3.2, 0.35);

    var result = new float[frames * 2];
    for (int i = 0; i < frames; i++)
    {
        result[i * 2] = (float)left[i];
        result[i * 2 + 1] = (float)right[i];
    }

    FadeOut(result, rate, 2, 1.5);
    return result;
}

// A clap: a short burst of noise around 1.2 kHz, opening in three quick hits, as hands that do not meet at once.
static void Clap(double[] left, double[] right, int rate, Random random, double start, double volume)
{
    Biquad band = Biquad.BandPass(rate, 1200, 0.9);
    int from = (int)(start * rate), count = (int)(0.22 * rate);
    for (int i = 0; i < count && from + i < left.Length; i++)
    {
        double t = i / (double)rate;
        double hits = t < 0.015 ? 0.55 + 0.45 * Math.Abs(Math.Sin(t / 0.015 * Math.PI * 3)) : 1;
        double s = band.Run((float)(random.NextDouble() * 2 - 1)) * Math.Exp(-t / 0.05) * hits * volume;
        left[from + i] += s;
        right[from + i] += s;
    }
}

// A closed hi-hat: very short, high noise, a little to the right.
static void Hat(double[] left, double[] right, int rate, Random random, double start, double volume)
{
    Biquad high = Biquad.HighPass(rate, 7000, 0.7);
    int from = (int)(start * rate), count = (int)(0.06 * rate);
    for (int i = 0; i < count && from + i < left.Length; i++)
    {
        double t = i / (double)rate;
        double s = high.Run((float)(random.NextDouble() * 2 - 1)) * Math.Exp(-t / 0.015) * volume;
        left[from + i] += s * 0.8;
        right[from + i] += s;
    }
}

// A crash cymbal: bright noise, different on each side, dying away over "seconds".
static void Crash(double[] left, double[] right, int rate, Random random, double start, double seconds, double volume)
{
    Biquad highLeft = Biquad.HighPass(rate, 3500, 0.6), highRight = Biquad.HighPass(rate, 3500, 0.6);
    int from = (int)(start * rate), count = (int)(seconds * rate);
    for (int i = 0; i < count && from + i < left.Length; i++)
    {
        double t = i / (double)rate;
        double env = Math.Exp(-t / (seconds / 3)) * Math.Min(1, t / 0.002) * volume;
        left[from + i] += highLeft.Run((float)(random.NextDouble() * 2 - 1)) * env;
        right[from + i] += highRight.Run((float)(random.NextDouble() * 2 - 1)) * env;
    }
}

// A riser: noise through a band that climbs from 300 Hz, growing louder over "seconds".
static void Riser(double[] left, double[] right, int rate, Random random, double start, double seconds, double volume)
{
    var band = new Biquad();
    int from = (int)(start * rate), count = (int)(seconds * rate);
    for (int i = 0; i < count && from + i < left.Length; i++)
    {
        double share = i / (double)count;
        if (i % 32 == 0)
            band.SetBandPass(rate, 300 * Math.Pow(20, share), 2);
        double s = band.Run((float)(random.NextDouble() * 2 - 1)) * share * share * volume;
        left[from + i] += s;
        right[from + i] += s;
    }
}

static double Hz(int note) => 440 * Math.Pow(2, (note - 69) / 12.0);

// Three soft saws a little apart, spread from left to right, through a low pass; slow in and out.
static void Pad(double[] left, double[] right, int rate, double hz, double start, double seconds, double volume)
{
    const double attack = 0.6, release = 0.9;
    int from = (int)(start * rate), count = (int)((seconds + release) * rate);
    double[] detune = [-0.006, 0, 0.006];
    double[] pans = [-0.5, 0, 0.5];
    double a = 1 - Math.Exp(-2 * Math.PI * 1300 / rate);
    for (int v = 0; v < 3; v++)
    {
        double dt = hz * (1 + detune[v]) / rate, phase = 0.3 * v, lp1 = 0, lp2 = 0;
        double gl = Math.Sqrt((1 - pans[v]) / 2) * 1.414, gr = Math.Sqrt((1 + pans[v]) / 2) * 1.414;
        for (int i = 0; i < count && from + i < left.Length; i++)
        {
            double t = i / (double)rate;
            double env = t < attack ? t / attack : t < seconds ? 1 : Math.Max(0, 1 - (t - seconds) / release);
            env = env * env * (3 - 2 * env);
            double saw = 2 * phase - 1 - PolyBlep(phase, dt);
            phase += dt;
            if (phase >= 1)
                phase -= 1;
            lp1 += a * (saw - lp1);
            lp2 += a * (lp1 - lp2);
            double s = lp2 * env * volume;
            left[from + i] += s * gl;
            right[from + i] += s * gr;
        }
    }
}

static double PolyBlep(double t, double dt)
{
    if (t < dt)
    {
        t /= dt;
        return t + t - t * t - 1;
    }

    if (t > 1 - dt)
    {
        t = (t - 1) / dt;
        return t * t + t + t + 1;
    }

    return 0;
}

// A round bass note: a sine with a little of its octave and fifth above.
static void Bass(double[] left, double[] right, int rate, double hz, double start, double seconds, double volume)
{
    int from = (int)(start * rate), count = (int)((seconds + 0.15) * rate);
    for (int i = 0; i < count && from + i < left.Length; i++)
    {
        double t = i / (double)rate;
        double held = 0.65 + 0.35 * Math.Exp(-t / 0.25);
        double env = Math.Min(1, t / 0.01) * (t < seconds ? held : Math.Max(0, 1 - (t - seconds) / 0.15) * (0.65 + 0.35 * Math.Exp(-seconds / 0.25)));
        double s = (Math.Sin(2 * Math.PI * hz * t) + 0.25 * Math.Sin(4 * Math.PI * hz * t) + 0.08 * Math.Sin(6 * Math.PI * hz * t)) * env * volume;
        left[from + i] += s;
        right[from + i] += s;
    }
}

// A plucked string: its harmonics, the higher ones dying away faster.
static void Pluck(double[] left, double[] right, int rate, double hz, double start, double volume, double pan)
{
    int from = (int)(start * rate), count = (int)(2.2 * rate);
    double gl = Math.Sqrt((1 - pan) / 2) * 1.414, gr = Math.Sqrt((1 + pan) / 2) * 1.414;
    for (int i = 0; i < count && from + i < left.Length; i++)
    {
        double t = i / (double)rate;
        double s = 0;
        for (int h = 1; h <= 9; h++)
        {
            double fh = hz * h * (1 + 0.0004 * h * h);
            if (fh > rate / 2.2)
                break;
            s += Math.Sin(2 * Math.PI * fh * t) / Math.Pow(h, 1.3) * Math.Exp(-t * (1.6 + 1.1 * h));
        }

        s *= Math.Min(1, t / 0.002) * volume;
        left[from + i] += s * gl;
        right[from + i] += s * gr;
    }
}

// A soft kick: a sine that falls from about 120 to 48 Hz.
static void Kick(double[] left, double[] right, int rate, double start, double volume)
{
    int from = (int)(start * rate), count = (int)(0.45 * rate);
    double phase = 0;
    for (int i = 0; i < count && from + i < left.Length; i++)
    {
        double t = i / (double)rate;
        phase += 2 * Math.PI * (48 + 75 * Math.Exp(-t / 0.035)) / rate;
        double s = Math.Sin(phase) * Math.Exp(-t / 0.16) * Math.Min(1, t / 0.002) * volume;
        left[from + i] += s;
        right[from + i] += s;
    }
}

// ---------------------------------------------------------------- helpers

static float[] Noise(Random random, int n)
{
    var x = new float[n];
    for (int i = 0; i < n; i++)
        x[i] = (float)(random.NextDouble() * 2 - 1);
    return x;
}

// Pink noise (Paul Kellet's filter) of a round of white noise: twice round, so that it ends as it begins.
static float[] Pink(Random random, int n)
{
    float[] white = Noise(random, n);
    var pink = new float[n];
    double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0;
    for (int pass = 0; pass < 2; pass++)
    {
        for (int i = 0; i < n; i++)
        {
            double w = white[i];
            b0 = 0.99886 * b0 + w * 0.0555179;
            b1 = 0.99332 * b1 + w * 0.0750759;
            b2 = 0.96900 * b2 + w * 0.1538520;
            b3 = 0.86650 * b3 + w * 0.3104856;
            b4 = 0.55000 * b4 + w * 0.5329522;
            b5 = -0.7616 * b5 - w * 0.0168980;
            double p = b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362;
            b6 = w * 0.115926;
            if (pass == 1)
                pink[i] = (float)(p * 0.11);
        }
    }

    return pink;
}

static float[] Brown(Random random, int n)
{
    float[] white = Noise(random, n);
    var brown = new float[n];
    double s = 0;
    for (int pass = 0; pass < 2; pass++)
    {
        for (int i = 0; i < n; i++)
        {
            s = (s + 0.02 * white[i]) / 1.02;
            if (pass == 1)
                brown[i] = (float)(s * 3.5);
        }
    }

    return brown;
}

static void FadeOut(float[] samples, int rate, int channels, double seconds)
{
    int frames = samples.Length / channels, fade = (int)(seconds * rate);
    for (int i = Math.Max(0, frames - fade); i < frames; i++)
    {
        double share = (frames - i) / (double)fade;
        double gain = 0.5 - 0.5 * Math.Cos(Math.PI * share);
        for (int c = 0; c < channels; c++)
            samples[i * channels + c] *= (float)gain;
    }
}

static void Normalize(float[] samples, double peakDb)
{
    float peak = 0;
    foreach (float sample in samples)
        peak = Math.Max(peak, Math.Abs(sample));
    if (peak <= 0)
        return;

    float gain = (float)(Math.Pow(10, peakDb / 20) / peak);
    for (int i = 0; i < samples.Length; i++)
        samples[i] *= gain;
}

// 16 bits, with a little triangular dither so that quiet tails stay smooth.
static void WriteWav(string path, float[] samples, int rate, int channels, Random random)
{
    using var writer = new WaveFileWriter(path, new WaveFormat(rate, 16, channels));
    var bytes = new byte[samples.Length * 2];
    for (int i = 0; i < samples.Length; i++)
    {
        double dither = random.NextDouble() - random.NextDouble();
        int value = (int)Math.Round(samples[i] * 32767 + dither);
        short clipped = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        bytes[i * 2] = (byte)clipped;
        bytes[i * 2 + 1] = (byte)(clipped >> 8);
    }

    writer.Write(bytes, 0, bytes.Length);
}

/// <summary>A filter of two poles and two zeros (Robert Bristow-Johnson's cookbook).</summary>
internal sealed class Biquad
{
    private double _b0, _b1, _b2, _a1, _a2, _z1, _z2;

    public static Biquad LowPass(double rate, double f, double q) => Made(rate, f, q, 0);

    public static Biquad HighPass(double rate, double f, double q) => Made(rate, f, q, 1);

    public static Biquad BandPass(double rate, double f, double q) => Made(rate, f, q, 2);

    public void SetBandPass(double rate, double f, double q) => Set(rate, f, q, 2);

    public float Run(float x)
    {
        double y = _b0 * x + _z1;
        _z1 = _b1 * x - _a1 * y + _z2;
        _z2 = _b2 * x - _a2 * y;
        return (float)y;
    }

    private static Biquad Made(double rate, double f, double q, int mode)
    {
        var filter = new Biquad();
        filter.Set(rate, f, q, mode);
        return filter;
    }

    private void Set(double rate, double f, double q, int mode)
    {
        double w = 2 * Math.PI * f / rate, cos = Math.Cos(w), alpha = Math.Sin(w) / (2 * q), a0 = 1 + alpha;
        switch (mode)
        {
            case 0:
                _b0 = (1 - cos) / 2;
                _b1 = 1 - cos;
                _b2 = (1 - cos) / 2;
                break;
            case 1:
                _b0 = (1 + cos) / 2;
                _b1 = -(1 + cos);
                _b2 = (1 + cos) / 2;
                break;
            default:
                _b0 = alpha;
                _b1 = 0;
                _b2 = -alpha;
                break;
        }

        _a1 = -2 * cos;
        _a2 = 1 - alpha;
        _b0 /= a0;
        _b1 /= a0;
        _b2 /= a0;
        _a1 /= a0;
        _a2 /= a0;
    }
}
