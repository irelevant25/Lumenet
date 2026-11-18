namespace DmxBox;

/// <summary>How the box combines the Art-Net layer with its own cue layer.</summary>
public enum MergeMode : byte
{
    /// <summary>
    /// Highest takes precedence: for each channel the higher value wins. The last Art-Net look
    /// is held until <see cref="DmxBoxClient.ReleaseArtNet"/>.
    /// </summary>
    Htp = 0,

    /// <summary>
    /// Art-Net replaces the cue layer while Art-Net packets keep arriving. After 3 seconds
    /// of silence the cue layer takes over again.
    /// </summary>
    Override = 1,
}
