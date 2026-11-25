namespace DmxBox.Server;

/// <summary>
/// The playback of an effect as the UIs see it: whether it runs, its fader, and its live speed, size
/// and spread. None of it is part of the project, like the positions of the faders.
/// </summary>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
/// <param name="Number">The effect, 1 to 64.</param>
/// <param name="Running">True from its start until its fader is down at zero.</param>
/// <param name="Level">Its fader, 0-255: how much of the effect there is.</param>
/// <param name="Speed">Its live speed, in cycles a minute (BPM).</param>
/// <param name="Size">Its live size, 0 to 100 per cent.</param>
/// <param name="Spread">Its live spread, 0 to 100 per cent.</param>
public sealed record EffectPlayback(int Number, bool Running, int Level, double Speed, int Size, int Spread)
{
    /// <summary>True while its key moves its fader up or down, in the effect's fade times.</summary>
    public bool Fading { get; init; }

    /// <summary>How many cycles it has run since it started, for a preview that follows it.</summary>
    public double Phase { get; init; }

    /// <summary>The speed it really runs at, in cycles a minute: its own, scaled by the rate's tempo while RATE is lit.</summary>
    public double Bpm { get; init; }

    /// <summary>True while its live speed, size or spread differ from the effect's own.</summary>
    public bool Changed { get; init; }
}

/// <summary>
/// What the running effects give the channels they touch right now, mixed with the rest of the
/// desk as in the output, before a solo, the limits, the master and blackout: the UIs show it.
/// </summary>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
/// <param name="Channels">The channels, counted from 1 through the universes.</param>
/// <param name="Levels">The level of each, 0-255.</param>
public sealed record EffectLevels(int[] Channels, int[] Levels);

/// <summary>
/// One cycle of an effect, sampled for the preview of the Effects view: what it gives each of its
/// lights (or each pixel) at <see cref="Frames"/> moments of the cycle, at its full level.
/// </summary>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
/// <param name="Kind">The kind of the effect.</param>
/// <param name="Frames">How many moments of the cycle each list has.</param>
/// <param name="Items">The lights, or the pixels of a pixel effect, in the effect's order.</param>
public sealed record EffectPreview(string Kind, int Frames, EffectPreviewItem[] Items);

/// <summary>A light or a pixel of an effect's preview.</summary>
/// <param name="Light">The light of the plan.</param>
/// <param name="Cell">For a pixel, its cell of the light, from 0; -1 for a light.</param>
/// <param name="X">Where it is on the plan: squares from the left of the stage.</param>
/// <param name="Y">Squares from the back of the stage.</param>
public sealed record EffectPreviewItem(int Light, int Cell, double X, double Y)
{
    /// <summary>A movement: where the head points, its pan at each moment, 0 to 1 of its range.</summary>
    public double[] Pan { get; init; } = [];

    /// <summary>A movement: its tilt at each moment, 0 to 1.</summary>
    public double[] Tilt { get; init; } = [];

    /// <summary>An intensity, or a pixel: how bright at each moment, 0 to 1.</summary>
    public double[] Levels { get; init; } = [];

    /// <summary>A color, or a pixel: the color at each moment, like "#ff2000".</summary>
    public string[] Colors { get; init; } = [];
}
