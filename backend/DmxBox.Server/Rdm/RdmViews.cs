namespace DmxBox.Server.Rdm;

/// <summary>A box as the RDM page shows it.</summary>
/// <param name="Address">Where the box answers, as in <see cref="BoxPlan.Address"/>.</param>
/// <param name="Number">The number on its switch, as it says; null for a box that does not say.</param>
/// <param name="Rdm">True for a box that does RDM (dmxbox 3).</param>
/// <param name="Searching">True while it searches its line: the list may still grow.</param>
/// <param name="Lights">The IDs of the lights it found, "7FF1:00000001", in the order found.</param>
/// <param name="Error">Why the list is missing: the box does not answer; null when all went well.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record RdmBoxView(string Address, int? Number, bool Rdm, bool Searching, string[] Lights, string? Error);

/// <summary>One mode ("personality") of a light.</summary>
/// <param name="Channels">The DMX channels it takes in this mode; 0 if it does not say.</param>
/// <param name="Name">Its name as the light gives it; empty if it gives none.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record RdmModeView(int Channels, string Name);

/// <summary>A light with RDM, as the RDM page shows it.</summary>
/// <param name="Box">The box on whose line it is, as in <see cref="RdmBoxView.Address"/>.</param>
/// <param name="Id">Its RDM ID, "7FF1:00000001".</param>
/// <param name="Maker">Its maker's name; empty if it gives none.</param>
/// <param name="Model">Its model's name; empty if it gives none.</param>
/// <param name="Label">The name it was given (DEVICE_LABEL); empty for none.</param>
/// <param name="Address">Its DMX start address, 1 to 512; 0 for a device without channels.</param>
/// <param name="Channels">How many channels it takes in its mode.</param>
/// <param name="Mode">Its mode, from 1; 0 if it has none.</param>
/// <param name="Modes">Its modes, mode 1 first.</param>
/// <param name="Identify">True while it shows where it is.</param>
/// <param name="Error">What went wrong reading it, for people; null when all went well.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record RdmLightView(
    string Box, string Id, string Maker, string Model, string Label, int Address, int Channels, int Mode,
    RdmModeView[] Modes, bool Identify, string? Error);

/// <summary>What the RDM page changes on a light: only what is given.</summary>
/// <param name="Label">A new name, at most 32 characters (empty clears it).</param>
/// <param name="Address">A new DMX start address, 1 to 512.</param>
/// <param name="Mode">Another mode, from 1.</param>
/// <param name="Identify">True: the light shows where it is; false: it stops.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record RdmLightChange(string? Label = null, int? Address = null, int? Mode = null, bool? Identify = null);
