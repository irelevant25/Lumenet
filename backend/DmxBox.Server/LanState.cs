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
/// <param name="Chosen">
/// The address of this PC the owner chose for LAN, like "192.168.1.20": devices are let in at
/// that address only. Null while the desk chooses by itself: then they are let in at every
/// address, and the first of <see cref="Addresses"/> is the one to give them. When the chosen
/// address is not among <see cref="Addresses"/> (the network gave the PC another one), the desk
/// chooses by itself until it is back. Null for a UI on another device.
/// </param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record LanState(bool Local, bool On, string[] Addresses, string? Password, string? Chosen = null);
