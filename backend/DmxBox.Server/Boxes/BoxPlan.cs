using System.Globalization;
using System.Net;

namespace DmxBox.Server.Boxes;

/// <summary>
/// A box the project wants, and the universe each of its outputs plays. A project lists its boxes
/// once the owner has changed something in the Boxes window or added a box by hand; until then it
/// lists none, and every box that answers plays universes by itself (see <see cref="BoxWiring"/>).
/// </summary>
/// <param name="Address">
/// Where the box answers: "10.1.3.1" for the box with the number 3 on its switch, "192.168.7.1"
/// for the box with the Pico, and a port after a colon when it is not 7000 ("127.0.0.1:7001").
/// </param>
/// <param name="Universes">
/// For each output of the box, output 1 first: the universe it plays, 1 to <see cref="Desk.MaxUniverses"/>,
/// or 0 for none. As many as the box has outputs.
/// </param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record BoxPlan(string Address, int[] Universes)
{
    /// <summary>The most boxes a project can list.</summary>
    public const int MaxBoxes = 64;

    /// <summary>The most outputs a box can have.</summary>
    public const int MaxOutputs = 8;

    /// <summary>The port of the DX protocol, which a box's address leaves out.</summary>
    public const int DefaultPort = DmxBoxClient.DefaultPort;

    /// <summary>Checks a plan from a UI or a file, and writes its address the one way.</summary>
    /// <param name="where">Which box it is, for the message: "Box 2".</param>
    /// <exception cref="DeskException">Something about it is out of range. The message says what.</exception>
    internal BoxPlan Checked(string where)
    {
        string address = NormalAddress(Address)
            ?? throw new DeskException($"{where}: \"{Address}\" is no address like 10.1.3.1 (or 10.1.3.1:7001).");

        int[] universes = Universes ?? [];
        if (universes.Length < 1 || universes.Length > MaxOutputs)
            throw new DeskException($"{where} must have 1 to {MaxOutputs} outputs, not {universes.Length}.");

        foreach (int universe in universes)
        {
            if (universe < 0 || universe > Desk.MaxUniverses)
                throw new DeskException($"{where}: an output plays universe 1 to {Desk.MaxUniverses}, or none (0), not {universe}.");
        }

        return new BoxPlan(address, [.. universes]);
    }

    /// <summary>
    /// The one way of writing an address: the IPv4 address (or a host name, in small letters, as
    /// the simulator may have), and ":port" only when the port is not <see cref="DefaultPort"/>.
    /// Null for text that is no such address.
    /// </summary>
    public static string? NormalAddress(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        string trimmed = text.Trim();
        int port = DefaultPort;
        int colon = trimmed.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            if (!int.TryParse(trimmed[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535)
                return null;
            trimmed = trimmed[..colon];
        }

        string? host = HostOf(trimmed);
        if (host is null)
            return null;

        return port == DefaultPort ? host : $"{host}:{port}";
    }

    // Four numbers with dots (IPAddress alone would take "3" or "10.1" as well), or a host name of
    // letters, digits, dashes and dots; null for anything else.
    private static string? HostOf(string text)
    {
        if (text.Split('.').Length == 4 && IPAddress.TryParse(text, out IPAddress? ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            return ip.ToString();

        if (text.Length > 0 && text.Length <= 253 && text.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.')
            && char.IsAsciiLetter(text[0]) && !text.EndsWith('.') && !text.Contains("..", StringComparison.Ordinal))
        {
            return text.ToLowerInvariant();
        }

        return null;
    }

    /// <summary>Same as another plan: the same address and the same universes on the same outputs.</summary>
    public bool SameAs(BoxPlan other)
    {
        return Address == other.Address && Universes.AsSpan().SequenceEqual(other.Universes);
    }
}
