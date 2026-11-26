namespace DmxBox.Server.Boxes;

/// <summary>
/// The dmxbox, as far as the box link needs it. Behind an interface so that the tests can put
/// a pretend box in its place. Every method throws when the box does not answer.
/// </summary>
internal interface IBox : IDisposable
{
    /// <summary>Asks the box for its version and state. Also the way to find out whether it is there.</summary>
    PingInfo Ping();

    /// <summary>Sets all channels of the box at once.</summary>
    /// <param name="levels">512 levels for each output of the box, output 1 first; channel 1 is at index 0.</param>
    void SetLevels(byte[] levels);

    /// <summary>Sets the box's own master, which scales every channel once more.</summary>
    /// <param name="level">255 = full, 0 = dark.</param>
    void Master(byte level);

    /// <summary>Switches the box's blackout on or off.</summary>
    /// <param name="on">True for dark.</param>
    void Blackout(bool on);
}
