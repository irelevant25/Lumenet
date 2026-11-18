namespace DmxBox;

/// <summary>
/// The box answered a request with an error status, for example BAD_ARG for a channel
/// outside 1-512. <see cref="DmxBoxTimeoutException"/> is the case where it did not answer at all.
/// </summary>
public class DmxBoxException : Exception
{
    /// <summary>Creates the exception for an error status sent by the box.</summary>
    /// <param name="statusCode">The status byte of the box's ACK.</param>
    public DmxBoxException(byte statusCode)
        : base($"the box answered {NameOf(statusCode)}")
    {
        StatusCode = statusCode;
        StatusName = NameOf(statusCode);
    }

    /// <summary>Creates the exception for a failure without a status from the box.</summary>
    /// <param name="message">What went wrong.</param>
    protected DmxBoxException(string message)
        : base(message)
    {
    }

    /// <summary>The status byte the box sent; null if the box did not answer.</summary>
    public byte? StatusCode { get; }

    /// <summary>
    /// The protocol's name for <see cref="StatusCode"/>, for example "BAD_ARG";
    /// null if the box did not answer.
    /// </summary>
    public string? StatusName { get; }

    private static string NameOf(byte statusCode) => statusCode switch
    {
        0 => "OK",
        1 => "BAD_LENGTH",
        2 => "BAD_ARG",
        3 => "UNKNOWN_TYPE",
        4 => "STORAGE_ERROR",
        5 => "EMPTY_SLOT",
        _ => $"status {statusCode}",
    };
}
