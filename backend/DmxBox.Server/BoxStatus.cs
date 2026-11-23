namespace DmxBox.Server;

/// <summary>Whether the server reaches a box, as its link knows it.</summary>
/// <param name="Connected">True while the box answers.</param>
/// <param name="Host">The address at which the server looks for the box, as in <see cref="BoxPlan.Address"/>.</param>
/// <param name="Firmware">The box's firmware version, for example "1.0"; null while not connected.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record BoxStatus(bool Connected, string Host, string? Firmware)
{
    /// <summary>
    /// How many outputs the box has, each a universe: its channels, as it last said them, divided
    /// by 512. 1 before it ever answered.
    /// </summary>
    public int Outputs { get; init; } = 1;
}

/// <summary>A box as the Boxes window shows it.</summary>
/// <param name="Address">Where the box answers, as in <see cref="BoxPlan.Address"/>.</param>
/// <param name="Connected">True while it answers.</param>
/// <param name="Firmware">Its firmware version while it answers; null otherwise.</param>
/// <param name="Outputs">How many outputs it has: as it said, or as the project plans them while it does not answer.</param>
/// <param name="Universes">For each output, output 1 first: the universe it plays now, 1 to 8, or 0 for none.</param>
/// <param name="Kind">
/// "automatic" while the project lists no boxes; "listed" for a box of the project's list;
/// "new" for a box that answers but is not on the project's list, and so plays nothing.
/// </param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record BoxView(string Address, bool Connected, string? Firmware, int Outputs, int[] Universes, string Kind)
{
    /// <summary>The same as another view in every field, the universes included.</summary>
    public bool SameAs(BoxView other)
    {
        return Address == other.Address && Connected == other.Connected && Firmware == other.Firmware
            && Outputs == other.Outputs && Kind == other.Kind && Universes.AsSpan().SequenceEqual(other.Universes);
    }
}
