using System.Diagnostics;

namespace DmxBox.Cli;

/// <summary>
/// Two small demos. Both assume four RGB PARs at channels 1-3, 4-6, 7-9 and 10-12.
/// </summary>
internal static class Demos
{
    private const int ParCount = 4;
    private const int ChannelCount = ParCount * 3;

    private static readonly TimeSpan StreamDuration = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1.0 / 40);

    /// <summary>Fades computed on the box: a few commands, a smooth result.</summary>
    public static void Cues(DmxBoxClient box)
    {
        (string Name, byte Red, byte Green, byte Blue)[] looks =
        [
            ("warm", 255, 120, 20),
            ("ocean", 0, 80, 255),
            ("magenta", 255, 0, 180),
        ];

        box.Blackout(false);
        box.Master(255);
        foreach (var look in looks)
        {
            Console.WriteLine($"fading to {look.Name}");
            box.Fade(1, AllPars(look.Red, look.Green, look.Blue), TimeSpan.FromSeconds(3));
            Thread.Sleep(TimeSpan.FromSeconds(4));
        }

        // Chase: each PAR fades up while the previous one fades out.
        for (int step = 0; step < ParCount * 3; step++)
        {
            byte[] frame = new byte[ChannelCount];
            SetPar(frame, step % ParCount, 255, 255, 255);
            box.Fade(1, frame, TimeSpan.FromSeconds(0.4));
            Thread.Sleep(TimeSpan.FromSeconds(0.5));
        }

        box.Fade(1, new byte[ChannelCount], TimeSpan.FromSeconds(2));
        Console.WriteLine($"done ({box.Retransmits} retransmits)");
    }

    /// <summary>An effect computed on the PC: a rolling rainbow, sent as 40 Art-Net frames per second.</summary>
    public static void Stream(ArtNetSender artNet, DmxBoxClient box)
    {
        var clock = Stopwatch.StartNew();
        TimeSpan nextFrame = TimeSpan.Zero;
        while (clock.Elapsed < StreamDuration)
        {
            artNet.Send(RainbowFrame(clock.Elapsed.TotalSeconds));

            // Sleep until the next frame is due, not for a fixed 25 ms: Thread.Sleep is
            // coarse (steps of about 16 ms on Windows), and this way its error does not add up.
            nextFrame += FrameInterval;
            TimeSpan wait = nextFrame - clock.Elapsed;
            if (wait > TimeSpan.Zero)
                Thread.Sleep(wait);
        }

        artNet.Send(new byte[ChannelCount]);
        box.ReleaseArtNet();   // let the cue layer take over again
    }

    // The rainbow rolls across the PARs: each PAR is a quarter of the color circle ahead of its neighbour.
    private static byte[] RainbowFrame(double seconds)
    {
        byte[] frame = new byte[ChannelCount];
        for (int par = 0; par < ParCount; par++)
        {
            double hue = (seconds * 0.2 + (double)par / ParCount) % 1.0;
            SetPar(frame, par, Wave(hue), Wave(hue + 1.0 / 3), Wave(hue + 2.0 / 3));
        }

        return frame;
    }

    // A sine wave from 0 to 254 that repeats every time "phase" grows by 1.
    private static byte Wave(double phase)
    {
        return (byte)(127 + 127 * Math.Sin(2 * Math.PI * phase));
    }

    private static byte[] AllPars(byte red, byte green, byte blue)
    {
        byte[] frame = new byte[ChannelCount];
        for (int par = 0; par < ParCount; par++)
            SetPar(frame, par, red, green, blue);

        return frame;
    }

    private static void SetPar(byte[] frame, int par, byte red, byte green, byte blue)
    {
        frame[par * 3] = red;
        frame[par * 3 + 1] = green;
        frame[par * 3 + 2] = blue;
    }
}
