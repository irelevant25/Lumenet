namespace DmxBox.Server.Stack;

/// <summary>
/// A moment of a sound, which a cue of the stack can come at instead of GO (<see cref="Step.At"/>):
/// when the sound plays and reaches it, the stack goes to the cue.
/// </summary>
/// <param name="Sound">The sound, 1-64.</param>
/// <param name="Seconds">Where in its file (of a track: in the track), in seconds from its start.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record SoundTime(int Sound, double Seconds)
{
    /// <summary>The latest moment there can be: more than a day of music.</summary>
    public const double MaxSeconds = 99_999.99;

    /// <summary>Checks a moment from a UI or a file, and rounds it to hundredths of a second.</summary>
    /// <exception cref="DeskException">The sound or the seconds are out of range.</exception>
    internal SoundTime Checked()
    {
        if (Sound < 1 || Sound > Sounds.Sound.MaxSounds)
            throw new DeskException($"A cue comes at a moment of a sound from 1 to {Sounds.Sound.MaxSounds}, not of sound {Sound}.");
        // "Not a number" fails both comparisons, so it is refused as well.
        if (!(Seconds >= 0 && Seconds <= MaxSeconds))
            throw new DeskException(FormattableString.Invariant($"A moment of a sound is from 0 to {MaxSeconds} seconds."));

        return this with { Seconds = Math.Round(Seconds, 2) };
    }
}
