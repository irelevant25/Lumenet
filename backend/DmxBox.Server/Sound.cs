using System.Globalization;

namespace DmxBox.Server;

/// <summary>
/// A sound: a file (music, a noise, a voice), with how it is to be played. Like an effect it
/// has a fader and a key: from zero its fader starts it, at zero it stops; its key brings the fader
/// up in the fade-in time and down in the fade-out time. Desk.Sounds.cs plays it; SoundEngine.cs
/// makes the sound.
/// </summary>
/// <remarks>
/// A sound belongs to the project. Its file is kept by the desk program (<see cref="SoundStore"/>)
/// under <see cref="Hash"/>, and saved with the project in a folder beside the project's file.
/// What it does while it plays belongs to its playback, see <see cref="SoundPlayback"/>.
/// Public only because <see cref="IDeskClient"/> has to be.
/// </remarks>
/// <param name="Number">Its number, 1 to <see cref="MaxSounds"/>, by which keys and steps name it.</param>
/// <param name="Name">What its owner calls it, up to <see cref="MaxNameLength"/> characters.</param>
/// <param name="File">The name of its file, like "thunder.mp3": as it was added, and as it is saved beside the project.</param>
/// <param name="Hash">What the desk program keeps the file under: the start of its SHA-256, 32 hex digits.</param>
public sealed record Sound(int Number, string Name, string File, string Hash)
{
    /// <summary>The most sounds a project can have.</summary>
    public const int MaxSounds = 64;

    /// <summary>The longest name a sound can have.</summary>
    public const int MaxNameLength = 24;

    /// <summary>The level of a sound that gives nothing, in decibels: the bottom of every level.</summary>
    public const double MinDecibels = -100;

    /// <summary>The highest level of a sound, in decibels: its file as it is.</summary>
    public const double MaxDecibels = 0;

    /// <summary>The slowest a sound can be played (its pitch stays).</summary>
    public const double MinSpeed = 0.5;

    /// <summary>The fastest a sound can be played (its pitch stays).</summary>
    public const double MaxSpeed = 2;

    /// <summary>How far its pitch can go up or down, in semitones (its speed stays).</summary>
    public const int MaxPitch = 12;

    /// <summary>The shortest and the longest time between the repeats of an echo, in seconds.</summary>
    public const double MinEchoTime = 0.02;

    /// <summary>The longest time between the repeats of an echo, in seconds.</summary>
    public const double MaxEchoTime = 2;

    /// <summary>How much of an echo comes back for the next repeat at most, in per cent.</summary>
    public const int MaxEchoRepeats = 90;

    /// <summary>How long the file is, in seconds, as the desk program found it; 0 while it is not known.</summary>
    public double Seconds { get; init; }

    /// <summary>Where it starts in its file, in seconds.</summary>
    public double Start { get; init; }

    /// <summary>Where it ends in its file, in seconds; null to play to the end of the file.</summary>
    public double? End { get; init; }

    /// <summary>True: after its end it starts again at <see cref="Start"/>, until it is stopped.</summary>
    public bool Loop { get; init; }

    /// <summary>
    /// Its level, in decibels: <see cref="MinDecibels"/> (nothing) to <see cref="MaxDecibels"/> (its file as it
    /// is); its fader at full gives this. A track of the mixer plays at this level on PLAYER L/R.
    /// </summary>
    public double Decibels { get; init; }

    /// <summary>The gain of a level in decibels: 0 at <see cref="MinDecibels"/> and below, 1 at 0 dB.</summary>
    internal static double GainOf(double decibels) => decibels <= MinDecibels ? 0 : Math.Pow(10, decibels / 20);

    /// <summary>A level to a tenth of a decibel, as the desk keeps it; never "minus 0".</summary>
    internal static double Tenth(double decibels)
    {
        double kept = Math.Round(decibels, 1);
        return kept == 0 ? 0 : kept;
    }

    /// <summary>The decibels of a gain: <see cref="MinDecibels"/> for nothing.</summary>
    internal static double DecibelsOf(double gain) => gain > 0 ? Math.Max(MinDecibels, 20 * Math.Log10(gain)) : MinDecibels;

    /// <summary>Seconds its key takes to bring it in.</summary>
    public double FadeIn { get; init; }

    /// <summary>Seconds its key takes to take it out.</summary>
    public double FadeOut { get; init; }

    /// <summary>Where it sits between left and right: -100 all left, 0 in the middle, 100 all right.</summary>
    public int Pan { get; init; }

    /// <summary>How fast it is played, <see cref="MinSpeed"/> to <see cref="MaxSpeed"/> times; its pitch stays.</summary>
    public double Speed { get; init; } = 1;

    /// <summary>How much higher (or lower, below 0) it sounds, in semitones; its speed stays.</summary>
    public int Pitch { get; init; }

    /// <summary>How loud its echo is, 0 to 100 per cent; 0 for none.</summary>
    public int Echo { get; init; }

    /// <summary>Seconds between the repeats of its echo.</summary>
    public double EchoTime { get; init; } = 0.3;

    /// <summary>How much of the echo comes back for the next repeat, 0 to <see cref="MaxEchoRepeats"/> per cent.</summary>
    public int EchoRepeats { get; init; } = 40;

    /// <summary>How much reverb it gets, 0 to 100 per cent; 0 for none.</summary>
    public int Reverb { get; init; }

    /// <summary>How big the room of the reverb is, 0 to 100 per cent.</summary>
    public int Room { get; init; } = 50;

    /// <summary>
    /// For a track of the USB player of a Soundcraft Ui mixer (see <see cref="UiTrackPlayer"/>):
    /// the playlist it is in, a folder of the stick as the mixer names it; null for a sound of a file of
    /// the desk program. A track has its name on the stick as <see cref="File"/> and no <see cref="Hash"/>,
    /// and plays as it is: of its settings only its level (sent to PLAYER L/R) and the fades count.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Playlist { get; init; }

    /// <summary>True for a track of the mixer's USB player.</summary>
    internal bool IsTrack => Playlist is not null;

    /// <summary>True when the other sound plays the same: the same file of the desk program, or the same track of the mixer.</summary>
    internal bool SameSource(Sound? other) =>
        other is not null && other.Hash == Hash && other.Playlist == Playlist && (Playlist is null || other.File == File);

    /// <summary>The longest name of a playlist or a track of the mixer.</summary>
    public const int MaxTrackNameLength = 300;

    /// <summary>The sound as the desk keeps it: every part checked, texts trimmed, numbers rounded.</summary>
    /// <returns>A sound the desk can keep.</returns>
    /// <exception cref="DeskException">Something about it cannot be used. The message says what.</exception>
    internal Sound Checked()
    {
        if (Number < 1 || Number > MaxSounds)
            throw new DeskException($"A sound has a number from 1 to {MaxSounds}, not {Number}.");

        string where = $"Sound {Number}";
        if (IsTrack)
            return CheckedTrack(where);

        string hash = (Hash ?? "").Trim().ToLowerInvariant();
        if (!SoundStore.IsHash(hash))
            throw new DeskException($"{where} needs a file of the desk program.");

        string file = SoundStore.CheckedFileName(File, where);
        if (!(Start >= 0 && Start <= 86400))
            throw new DeskException(Text($"{where}: it starts from 0 seconds on, not at {Start}."));
        if (End is double end && !(end > Start && end <= 86400))
            throw new DeskException(Text($"{where}: it ends after its start ({Start} s), not at {end}."));
        CheckDecibels(where);
        if (Pan < -100 || Pan > 100)
            throw new DeskException($"{where}: its pan goes from -100 (left) to 100 (right), not {Pan}.");
        if (!(Speed >= MinSpeed && Speed <= MaxSpeed))
            throw new DeskException(Text($"{where}: its speed goes from {MinSpeed} to {MaxSpeed} times, not {Speed}."));
        if (Pitch < -MaxPitch || Pitch > MaxPitch)
            throw new DeskException($"{where}: its pitch goes from -{MaxPitch} to {MaxPitch} semitones, not {Pitch}.");
        if (Echo < 0 || Echo > 100 || Reverb < 0 || Reverb > 100 || Room < 0 || Room > 100)
            throw new DeskException($"{where}: its echo, its reverb and the room go from 0 to 100 per cent.");
        if (!(EchoTime >= MinEchoTime && EchoTime <= MaxEchoTime))
            throw new DeskException(Text($"{where}: the time of its echo goes from {MinEchoTime} to {MaxEchoTime} seconds, not {EchoTime}."));
        if (EchoRepeats < 0 || EchoRepeats > MaxEchoRepeats)
            throw new DeskException($"{where}: its echo comes back with 0 to {MaxEchoRepeats} per cent, not {EchoRepeats}.");

        return this with
        {
            Name = PlanText.Checked(Name, MaxNameLength, where, "a name"),
            File = file,
            Hash = hash,
            Seconds = Math.Max(0, Math.Round(Seconds, 3)),
            Start = Math.Round(Start, 2),
            End = End is double kept ? Math.Round(kept, 2) : null,
            Decibels = Tenth(Decibels),
            FadeIn = Fade(FadeIn, where, "fade in"),
            FadeOut = Fade(FadeOut, where, "fade out"),
            Speed = Math.Round(Speed, 2),
            EchoTime = Math.Round(EchoTime, 2),
        };
    }

    // A track of the mixer: its playlist and name as the mixer has them (sent back to it exactly), its
    // level and fades; the rest the player cannot do, so it is as on a new sound.
    private Sound CheckedTrack(string where)
    {
        static bool Usable(string? text) =>
            !string.IsNullOrEmpty(text) && text.Length <= MaxTrackNameLength && !text.Any(char.IsControl) && !text.Contains('^');

        if (!Usable(Playlist) || !Usable(File))
            throw new DeskException($"{where}: a track of the mixer needs its playlist and its name, as the mixer lists them.");
        CheckDecibels(where);

        var fresh = new Sound(Number, Name, File!, "");
        return fresh with
        {
            Name = PlanText.Checked(Name, MaxNameLength, where, "a name"),
            Playlist = Playlist,
            Seconds = Math.Max(0, Math.Round(Seconds, 3)),
            Decibels = Tenth(Decibels),
            FadeIn = Fade(FadeIn, where, "fade in"),
            FadeOut = Fade(FadeOut, where, "fade out"),
        };
    }

    private void CheckDecibels(string where)
    {
        // "Not a number" fails both comparisons, so it is refused as well.
        if (!(Decibels >= MinDecibels && Decibels <= MaxDecibels))
            throw new DeskException(Text($"{where}: its level goes from {MinDecibels} dB (nothing) to {MaxDecibels} dB, not {Decibels}."));
    }

    private static double Fade(double time, string where, string which)
    {
        // "Not a number" fails both comparisons, so it is refused as well.
        if (!(time >= 0 && time <= Step.MaxSeconds))
            throw new DeskException(Text($"{where}: the {which} time must be from 0 to {Step.MaxSeconds} seconds."));

        return Math.Round(time, 1);
    }

    // Numbers with a point, as on the UI, whatever the language of the PC.
    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
