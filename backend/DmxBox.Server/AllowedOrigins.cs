namespace DmxBox.Server;

/// <summary>
/// The web pages that may use the hub. A page is named by its origin: scheme, host and port,
/// as in "http://localhost:5080".
/// </summary>
/// <remarks>
/// <para>
/// The hub has no login, and the server listens on this PC only. That alone does not keep
/// strangers out: a browser on this PC lets any web page open a WebSocket to localhost, and
/// the rules that browsers apply to ordinary requests (CORS) do not cover WebSockets. So any
/// site that is open in the browser could work the desk.
/// </para>
/// <para>
/// What a browser always does is tell the server which page is asking, in the request's
/// "Origin" header; a page cannot change that header. The server therefore answers a hub
/// request only if its origin is on this list. A request without the header does not come
/// from a web page but from a program, and is let through: a program on this PC needs no
/// browser to do harm.
/// </para>
/// </remarks>
internal sealed class AllowedOrigins
{
    // Every origin in one spelling, see Normalize.
    private readonly HashSet<string> _origins = [];

    /// <summary>Makes the list.</summary>
    /// <param name="origins">The origins to allow, for example "http://localhost:5080".</param>
    /// <exception cref="ArgumentException">One of the texts is not an origin.</exception>
    public AllowedOrigins(IEnumerable<string> origins)
    {
        foreach (string origin in origins)
        {
            string normalized = Normalize(origin) ?? throw new ArgumentException(
                $"\"{origin}\" is not an origin. An origin looks like http://localhost:5080.", nameof(origins));
            _origins.Add(normalized);
        }
    }

    /// <summary>Decides whether a request may use the hub.</summary>
    /// <param name="origin">The request's Origin header; null if the request has none.</param>
    /// <returns>True without an origin, and for an origin whose scheme, host and port are on the list.</returns>
    public bool Allows(string? origin)
    {
        if (origin is null)
            return true;

        return Normalize(origin) is { } normalized && _origins.Contains(normalized);
    }

    // One spelling for every origin, so that two origins are the same exactly when their texts
    // are equal: scheme and host in lower case, and the port always written out, also the
    // usual one ("http://localhost" is port 80). Null for a text that is not an origin.
    private static string? Normalize(string text)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri))
            return null;

        // An origin ends after the port. One slash there is let pass, because people write it
        // in settings; a path, a query or a user name makes the text something else.
        bool hasMore = uri.AbsolutePath != "/" || uri.Query != "" || uri.Fragment != "" || uri.UserInfo != "";
        if (hasMore || uri.Host == "")
            return null;

        return $"{uri.Scheme}://{uri.IdnHost}:{uri.Port}";
    }
}
