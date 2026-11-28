namespace DmxBox.Server.Boxes;

/// <summary>
/// Someone who listens to a box's DMX IN (<see cref="BoxLink.ListenToInput"/>): each channel whose
/// level changed from one frame to the next is handed over, on the link's thread. The first frame
/// only shows where the console stands and hands over nothing, so a button of the console that is
/// held while it is plugged in presses nothing.
/// </summary>
internal sealed class InputListener
{
    private readonly BoxLink _link;
    private readonly Action<int, byte> _changed;
    private volatile bool _gone;

    // The levels of the frame before; null before the first. The link's thread only.
    private byte[]? _before;

    internal InputListener(BoxLink link, Action<int, byte> changed)
    {
        _link = link;
        _changed = changed;
    }

    /// <summary>True once the box is gone, or another listener took its place: nothing more comes.</summary>
    public bool Gone => _gone;

    /// <summary>The number of the last frame taken, for the next question to the box. The link's thread only.</summary>
    internal uint Frame { get; private set; }

    /// <summary>Stops listening. Any thread.</summary>
    public void Stop()
    {
        _gone = true;
        _link.StopListening(this);
    }

    /// <summary>Nothing more comes: the box is gone. Any thread.</summary>
    internal void Lose()
    {
        _gone = true;
    }

    /// <summary>A new frame: the channels that changed are handed over. The link's thread.</summary>
    /// <param name="frame">Its number.</param>
    /// <param name="levels">Its levels, channel 1 at index 0.</param>
    internal void Take(uint frame, byte[] levels)
    {
        Frame = frame;
        byte[]? before = _before;
        _before = levels;
        if (before is null || _gone)
            return;

        for (int index = 0; index < levels.Length; index++)
        {
            // A channel the frame before did not have counts as 0 there, as on the line.
            byte was = index < before.Length ? before[index] : (byte)0;
            if (levels[index] != was)
                _changed(index + 1, levels[index]);
        }
    }
}
