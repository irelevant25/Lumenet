using System.Globalization;
using System.Text.RegularExpressions;

namespace DmxBox.Server;

/// <summary>The faders a control of a controller can move.</summary>
internal enum FaderKind
{
    /// <summary>A channel fader, 1-4096.</summary>
    Channel,

    /// <summary>The fader of a memory, 1-128.</summary>
    Memory,

    /// <summary>The master fader.</summary>
    Master,

    /// <summary>The rate's fader: the tempo, 30 to 300 BPM.</summary>
    Rate,

    /// <summary>The speed fader of the sequence in a memory, 0.1x to 10x.</summary>
    Sequence,

    /// <summary>The fader of an effect, 1-64: how much of it there is.</summary>
    Effect,

    /// <summary>The live speed of an effect, 0.5 to 600 BPM.</summary>
    EffectSpeed,

    /// <summary>The live size of an effect, 0-100 %.</summary>
    EffectSize,

    /// <summary>The live spread of an effect, 0-100 %.</summary>
    EffectSpread,

    /// <summary>The fader of a sound, 1-64: its level.</summary>
    Sound,

    /// <summary>The master volume of the sound, 0 to 200 %.</summary>
    SoundVolume,
}

/// <summary>
/// What a control works, from its name (<see cref="MidiMapping.Target"/>). The keys of the desk
/// are named as the page names them ("memory.5", "stack.play", "page.channel.2": desk-actions.ts),
/// and the page presses them, as its keyboard shortcuts do. The faders are named here, and the desk
/// program moves them itself:
/// <list type="bullet">
/// <item>fader.channel.N (1-4096), fader.memory.N (1-128), fader.master, fader.rate (the tempo),
/// fader.sequence.N (the speed of the sequence in memory N), fader.effect.N (1-64, how much of it),
/// fader.effect-speed.N, fader.effect-size.N, fader.effect-spread.N, fader.sound.N (1-64, its
/// level), fader.sound-volume (the master volume of the sound);</item>
/// <item>fader.slot.channel.K, fader.slot.memory.K, fader.slot.effect.K: the K-th fader of the
/// page of channels, memories or effects the PC shows, which follows its page keys.</item>
/// </list>
/// OSC is to use the same names later.
/// </summary>
/// <param name="Name">The name, as written.</param>
/// <param name="Fader">The kind of fader; null for a key.</param>
/// <param name="Number">The channel, memory or effect (for a slot: K), 0 for the master and the rate.</param>
/// <param name="Slot">True for a slot of the page shown.</param>
internal readonly partial record struct ControlTarget(string Name, FaderKind? Fader, int Number, bool Slot)
{
    /// <summary>The most a fader can be numbered: the channels of 8 universes.</summary>
    private const int MaxNumber = 4096;

    /// <summary>True for a key of the desk, which the page presses.</summary>
    public bool IsKey => Fader is null;

    /// <summary>The target of a name; null for a name that is neither a key nor a fader.</summary>
    /// <param name="name">"memory.5", "fader.channel.12", "fader.slot.memory.3".</param>
    public static ControlTarget? Parse(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64)
            return null;

        Match fader = FaderName().Match(name);
        if (fader.Success)
        {
            bool slot = fader.Groups["slot"].Success;
            string kind = fader.Groups["kind"].Value;
            int number = fader.Groups["number"].Success ? int.Parse(fader.Groups["number"].Value, CultureInfo.InvariantCulture) : 0;
            FaderKind? found = kind switch
            {
                "channel" => FaderKind.Channel,
                "memory" => FaderKind.Memory,
                "master" when !slot => FaderKind.Master,
                "rate" when !slot => FaderKind.Rate,
                "sequence" when !slot => FaderKind.Sequence,
                "effect" => FaderKind.Effect,
                "effect-speed" when !slot => FaderKind.EffectSpeed,
                "effect-size" when !slot => FaderKind.EffectSize,
                "effect-spread" when !slot => FaderKind.EffectSpread,
                "sound" when !slot => FaderKind.Sound,
                "sound-volume" when !slot => FaderKind.SoundVolume,
                _ => null,
            };
            bool numbered = found is not (FaderKind.Master or FaderKind.Rate or FaderKind.SoundVolume);
            if (found is null || numbered != fader.Groups["number"].Success || (numbered && number is < 1 or > MaxNumber))
                return null;

            return new ControlTarget(name, found, number, slot);
        }

        // "fader." names faders only: one that is none is no key either.
        return !name.StartsWith("fader.", StringComparison.Ordinal) && KeyName().IsMatch(name)
            ? new ControlTarget(name, null, 0, false)
            : null;
    }

    [GeneratedRegex(@"^fader\.(?<slot>slot\.)?(?<kind>[a-z-]+)(\.(?<number>[0-9]{1,4}))?$", RegexOptions.CultureInvariant)]
    private static partial Regex FaderName();

    // A name like the page gives its keys: "memory.5", "master.solo-down", "lan".
    [GeneratedRegex(@"^[a-z][a-z0-9-]*(\.[a-z0-9-]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyName();
}

/// <summary>
/// Where a fader stands, 0-255, for a value of the desk, and back: as the page's faders have it.
/// A channel, a memory, the master and an effect's fader are their levels; the rate goes from 30
/// to 300 BPM; a sequence's speed has 1x in the middle (0.1x to 1x from 0 to 127, 1x to 10x from
/// 128 to 255: both middle places are 1x, easy to find by hand); an effect's live speed goes in
/// octaves from 0.5 to 600 BPM; its size and spread are per cent. A sound's fader is its level; the
/// master volume of the sound goes from 0 to 200 %.
/// </summary>
internal static class FaderScale
{
    /// <summary>The top of a fader.</summary>
    public const int Top = 255;

    // A sequence's speed: 0 to Half is 0.1x to 1x, Half + 1 to Top is 1x to 10x.
    private const int Half = 127;

    private const double MinBpm = 30;
    private const double MaxBpm = 300;
    private const double MinSpeed = 0.1;
    private const double MaxSpeed = 10;
    private const double SpeedStep = 0.05;
    private const double MinEffectSpeed = 0.5;
    private const double MaxEffectSpeed = 600;

    /// <summary>Where the fader stands for a value of the desk.</summary>
    /// <param name="kind">The kind of fader.</param>
    /// <param name="value">The level, BPM, speed or per cent, as the desk has it.</param>
    public static int PositionOf(FaderKind kind, double value)
    {
        if (kind == FaderKind.Sequence)
        {
            double speed = Math.Clamp(value, MinSpeed, MaxSpeed);
            return speed <= 1
                ? (int)Math.Round((speed - MinSpeed) / (1 - MinSpeed) * Half)
                : Half + 1 + (int)Math.Round((speed - 1) / (MaxSpeed - 1) * Half);
        }

        double share = kind switch
        {
            FaderKind.Rate => (value - MinBpm) / (MaxBpm - MinBpm),
            FaderKind.EffectSpeed => Math.Log(Math.Clamp(value, MinEffectSpeed, MaxEffectSpeed) / MinEffectSpeed) / Math.Log(MaxEffectSpeed / MinEffectSpeed),
            FaderKind.EffectSize or FaderKind.EffectSpread => value / 100,
            FaderKind.SoundVolume => value <= Sound.MinDecibels ? 0 : Math.Pow(10, value / 40),   // the gain is the place squared
            _ => value / Top,
        };
        return (int)Math.Round(Math.Clamp(share, 0, 1) * Top);
    }

    /// <summary>The value of the desk for a place of the fader.</summary>
    /// <param name="kind">The kind of fader.</param>
    /// <param name="position">0-255.</param>
    /// <returns>The level, BPM, speed or per cent the desk is to get.</returns>
    public static double ValueAt(FaderKind kind, int position)
    {
        int place = Math.Clamp(position, 0, Top);
        double share = place / (double)Top;
        return kind switch
        {
            FaderKind.Rate => Math.Round(MinBpm + (share * (MaxBpm - MinBpm))),
            FaderKind.Sequence => Stepped(place <= Half
                ? MinSpeed + ((1 - MinSpeed) * place / Half)
                : 1 + ((MaxSpeed - 1) * (place - Half - 1) / Half)),
            FaderKind.EffectSpeed => Math.Round(MinEffectSpeed * Math.Pow(MaxEffectSpeed / MinEffectSpeed, share), 1),
            FaderKind.Effect or FaderKind.Sound => Math.Round(share * 100, 1),
            FaderKind.SoundVolume => share <= 0 ? Sound.MinDecibels : Math.Max(Sound.MinDecibels, Math.Round(40 * Math.Log10(share), 1)),
            FaderKind.EffectSize or FaderKind.EffectSpread => Math.Round(share * 100),
            _ => Math.Clamp(position, 0, Top),
        };
    }

    // A speed on a step of 0.05, as the desk keeps it.
    private static double Stepped(double speed)
    {
        return Math.Clamp(Math.Round(Math.Round(speed / SpeedStep) * SpeedStep, 2), MinSpeed, MaxSpeed);
    }
}
