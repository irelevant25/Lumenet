namespace DmxBox;

/// <summary>
/// The box did not acknowledge a request, even after all retransmits:
/// it is switched off, the address is wrong, the cable is unplugged...
/// </summary>
public sealed class DmxBoxTimeoutException : DmxBoxException
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">Which box did not answer.</param>
    public DmxBoxTimeoutException(string message)
        : base(message)
    {
    }
}
