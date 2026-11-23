using System.Net;

namespace DmxBox.Server;

/// <summary>
/// Which universe every output of every box plays. While the project lists no boxes, every box
/// that answers plays universes by itself: the boxes in their order (<see cref="Compare"/>), each
/// output the next universe, U1, U2 and on, as far as the project has universes; outputs beyond
/// that play none. Once the project lists boxes (the owner changed something in the Boxes window,
/// or added a box by hand), only those play, as the list says; a box that answers without being
/// listed is "new" and plays nothing until it is added.
/// </summary>
internal static class BoxWiring
{
    /// <summary>The <see cref="BoxView.Kind"/> of a box while the project lists none.</summary>
    public const string Automatic = "automatic";

    /// <summary>The <see cref="BoxView.Kind"/> of a box of the project's list.</summary>
    public const string Listed = "listed";

    /// <summary>The <see cref="BoxView.Kind"/> of a box that answers but is not on the project's list.</summary>
    public const string New = "new";

    // The box with the Pico, which has no switch, comes first.
    private const string PicoBox = "192.168.7.1";

    /// <summary>
    /// The order of boxes for the automatic wiring: the box with the Pico first, then the others by
    /// their addresses as numbers (10.1.2.1 before 10.1.10.1), so the numbers on their switches
    /// decide; names last, in alphabetical order.
    /// </summary>
    public static int Compare(string a, string b)
    {
        return Key(a).CompareTo(Key(b));
    }

    // The Pico box; then addresses by their four numbers and the port; then names.
    private static (int Group, long Number, int Port, string Name) Key(string address)
    {
        int colon = address.IndexOf(':', StringComparison.Ordinal);
        string host = colon < 0 ? address : address[..colon];
        int port = colon < 0 ? BoxPlan.DefaultPort : int.Parse(address[(colon + 1)..], System.Globalization.CultureInfo.InvariantCulture);
        if (host == PicoBox)
            return (0, 0, port, "");
        if (IPAddress.TryParse(host, out IPAddress? ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            byte[] bytes = ip.GetAddressBytes();
            return (1, ((long)bytes[0] << 24) | ((long)bytes[1] << 16) | ((long)bytes[2] << 8) | bytes[3], port, "");
        }

        return (2, 0, port, host);
    }

    /// <summary>
    /// The boxes as the Boxes window shows them, with what each output plays: the project's boxes
    /// in the project's order, then the new ones; or, while the project lists none, every box that
    /// answers in the automatic order.
    /// </summary>
    /// <param name="listed">The boxes the project lists; empty for none.</param>
    /// <param name="links">What the links know of every box they look for, by its address.</param>
    /// <param name="universes">How many universes the project has.</param>
    public static BoxView[] Views(IReadOnlyList<BoxPlan> listed, IReadOnlyList<BoxStatus> links, int universes)
    {
        Dictionary<string, BoxStatus> byAddress = links.GroupBy(link => link.Host).ToDictionary(group => group.Key, group => group.First());
        if (listed.Count == 0)
        {
            int next = 1;
            return [.. links
                .Where(link => link.Connected)
                .OrderBy(link => link.Host, Comparer<string>.Create(Compare))
                .Select(link =>
                {
                    int[] plays = new int[link.Outputs];
                    for (int output = 0; output < plays.Length; output++)
                        plays[output] = next <= universes ? next++ : 0;
                    return new BoxView(link.Host, true, link.Firmware, link.Outputs, plays, Automatic);
                })];
        }

        var views = new List<BoxView>();
        foreach (BoxPlan plan in listed)
        {
            byAddress.TryGetValue(plan.Address, out BoxStatus? link);
            bool connected = link?.Connected == true;
            int outputs = connected ? link!.Outputs : plan.Universes.Length;
            int[] plays = new int[outputs];
            Array.Copy(plan.Universes, plays, Math.Min(outputs, plan.Universes.Length));
            views.Add(new BoxView(plan.Address, connected, connected ? link!.Firmware : null, outputs, plays, Listed));
        }

        views.AddRange(links
            .Where(link => link.Connected && !listed.Any(plan => plan.Address == link.Host))
            .OrderBy(link => link.Host, Comparer<string>.Create(Compare))
            .Select(link => new BoxView(link.Host, true, link.Firmware, link.Outputs, new int[link.Outputs], New)));
        return [.. views];
    }
}
