namespace DmxBox.Server;

/// <summary>Whether devices in the same network may use the desk, in the form a UI receives it.</summary>
/// <param name="Local">
/// True for a UI on this PC itself. Only such a UI can switch LAN on or off, and only it is
/// told the addresses and the password.
/// </param>
/// <param name="On">True while devices in the network may connect.</param>
/// <param name="Addresses">
/// Where another device reaches the desk, for example "192.168.1.20:5080", the best one
/// first. Empty for a UI on another device, and without a network.
/// </param>
/// <param name="Password">The password another device has to give; null while LAN is off, and for a UI on another device.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record LanState(bool Local, bool On, string[] Addresses, string? Password);
