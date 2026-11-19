namespace DmxBox.Server;

/// <summary>Whether the server reaches the box, in the form the UI receives it.</summary>
/// <param name="Connected">True while the box answers.</param>
/// <param name="Host">The address at which the server looks for the box.</param>
/// <param name="Firmware">The box's firmware version, for example "1.0"; null while not connected.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record BoxStatus(bool Connected, string Host, string? Firmware);
