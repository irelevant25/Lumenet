using NAudio.Wave;

namespace DmxBox.Server.Sounds;

/// <summary>
/// How loud a sound file is along its length, in <see cref="Parts"/> parts of the same length: the
/// wave the Sounds view draws, with a line where the sound is while it plays.
/// </summary>
/// <param name="Seconds">How long the file is, as it was read.</param>
/// <param name="Peaks">The loudest sample of each part, 0 to 255 (255: full scale).</param>
/// <param name="Levels">The mean loudness of each part (its RMS), 0 to 255.</param>
/// <remarks>Public only because the answer of GET /api/sounds/{hash}/wave is made of it.</remarks>
public sealed record SoundWave(double Seconds, byte[] Peaks, byte[] Levels)
{
    /// <summary>How many parts a wave has.</summary>
    public const int Parts = 1000;

    // The file is read in blocks of this many frames; the parts are made of them at the end, when it
    // is known how many there are (the length a reader tells beforehand is not always exact).
    private const int BlockFrames = 256;

    /// <summary>Reads a file from its start to its end and measures it.</summary>
    /// <param name="path">The file.</param>
    /// <returns>Its wave.</returns>
    internal static SoundWave Measure(string path)
    {
        using WaveStream reader = SoundFiles.Open(path);
        ISampleProvider samples = reader.ToSampleProvider();
        int channels = Math.Max(1, samples.WaveFormat.Channels);
        int rate = samples.WaveFormat.SampleRate;

        var peaks = new List<float>();
        var squares = new List<double>();
        var frames = new List<int>();
        float[] buffer = new float[BlockFrames * channels];
        long total = 0;
        int filled = 0;
        float peak = 0;
        double square = 0;
        int read;
        while ((read = samples.Read(buffer.AsSpan(filled))) > 0)
        {
            filled += read;
            if (filled < buffer.Length)
                continue;   // a reader may give less than asked: the block is filled first

            Take(buffer.AsSpan(0, filled));
            filled = 0;
        }

        if (filled >= channels)
            Take(buffer.AsSpan(0, filled - (filled % channels)));

        byte[] peakParts = new byte[Parts];
        byte[] levelParts = new byte[Parts];
        int blocks = peaks.Count;
        for (int part = 0; part < Parts && blocks > 0; part++)
        {
            // the blocks of this part; a file of fewer blocks than parts gives each part the nearest one
            int first = (int)((long)part * blocks / Parts);
            int after = Math.Max(first + 1, (int)((long)(part + 1) * blocks / Parts));
            float loudest = 0;
            double sum = 0;
            long count = 0;
            for (int block = first; block < after && block < blocks; block++)
            {
                loudest = Math.Max(loudest, peaks[block]);
                sum += squares[block];
                count += frames[block];
            }

            peakParts[part] = ToByte(loudest);
            levelParts[part] = ToByte(count > 0 ? (float)Math.Sqrt(sum / count) : 0);
        }

        return new SoundWave(Math.Round(total / (double)rate, 3), peakParts, levelParts);

        // One block: its loudest sample, and the sum of the squares of its frames (the channels mixed).
        void Take(Span<float> block)
        {
            int blockFrames = block.Length / channels;
            for (int frame = 0; frame < blockFrames; frame++)
            {
                float mixed = 0;
                for (int channel = 0; channel < channels; channel++)
                {
                    float sample = block[frame * channels + channel];
                    peak = Math.Max(peak, Math.Abs(sample));
                    mixed += sample;
                }

                mixed /= channels;
                square += mixed * mixed;
            }

            peaks.Add(peak);
            squares.Add(square);
            frames.Add(blockFrames);
            total += blockFrames;
            peak = 0;
            square = 0;
        }
    }

    private static byte ToByte(float share)
    {
        return (byte)Math.Round(Math.Clamp(share, 0, 1) * 255, MidpointRounding.AwayFromZero);
    }
}
