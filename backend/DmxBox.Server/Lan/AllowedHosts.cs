using System.Net;
using Microsoft.AspNetCore.Http;

namespace DmxBox.Server.Lan;

/// <summary>
/// The names by which the server lets itself be called, in the request's "Host" header: the
/// names of the setting "AllowedHosts", and this PC's own network addresses and computer name.
/// </summary>
/// <remarks>
/// <para>
/// A web page can give this PC a name of its own (DNS rebinding) and then call the server
/// under that name, from the browser of whoever has the page open. The server answers to the
/// names of this PC only, so such a page gets nothing.
/// </para>
/// <para>
/// The addresses come from <see cref="ThisPc"/> at the moment of the request, because a
/// router can hand out a new one while the server runs. A web page cannot make a browser send
/// an address of this PC as the host of the page's own requests: that is exactly what makes
/// such an address safe to answer to. ASP.NET Core's own check of "AllowedHosts" knows a fixed
/// list only, so the program switches it off and does the check itself.
/// </para>
/// </remarks>
internal sealed class AllowedHosts
{
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _anyName;

    /// <summary>Makes the list.</summary>
    /// <param name="setting">The setting: names separated by ";", for example "localhost;127.0.0.1". "*" lets every name in.</param>
    public AllowedHosts(string setting)
    {
        foreach (string name in setting.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (name == "*")
                _anyName = true;
            else
                _names.Add(name);
        }
    }

    /// <summary>Decides whether the server answers a request with this Host header.</summary>
    /// <param name="host">The header; its port does not matter.</param>
    /// <param name="pc">This PC, for its addresses and its name.</param>
    public bool Allows(HostString host, ThisPc pc)
    {
        if (_anyName)
            return true;
        if (!host.HasValue)
            return false;

        string name = host.Host;
        if (_names.Contains(name))
            return true;

        // An address in the header: "192.168.1.20", or "[fe80::1]" for IPv6.
        if (IPAddress.TryParse(name.Trim('[', ']'), out IPAddress? address))
            return pc.Owns(address);

        return pc.HasName(name);
    }
}
