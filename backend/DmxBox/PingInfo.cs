namespace DmxBox;

/// <summary>The box's answer to <see cref="DmxBoxClient.Ping"/>: its version and current state.</summary>
/// <param name="Firmware">Firmware version, for example "1.0".</param>
/// <param name="ArtNetLive">True while Art-Net packets are arriving (the last one less than 3 seconds ago).</param>
/// <param name="Blackout">True while blackout is on.</param>
/// <param name="MergeMode">How the Art-Net layer and the cue layer are combined.</param>
/// <param name="ArtNetHolding">True while the Art-Net layer holds a look, live or not.</param>
/// <param name="Master">Master level, 0-255.</param>
/// <param name="Uptime">Time since the box started.</param>
/// <param name="Presets">Numbers of the preset slots that hold a look.</param>
/// <param name="Channels">Number of DMX channels the box sends.</param>
/// <param name="SupplyMillivolts">The supply voltage the box measures, in millivolts; null if it does not say (firmware before 3.0) or cannot measure it (dmxbox 2).</param>
/// <param name="Abilities">What it can do beyond DMX: RDM, MIDI, a chain of boxes (firmware 3.0 and later).</param>
/// <param name="BoxNumber">The number on its switch, 0 to 255; null if it does not say.</param>
/// <param name="Model">Which dmxbox it is, 1 to 4; null if it does not say (firmware before 3.1).</param>
public sealed record PingInfo(
    string Firmware,
    bool ArtNetLive,
    bool Blackout,
    MergeMode MergeMode,
    bool ArtNetHolding,
    byte Master,
    TimeSpan Uptime,
    IReadOnlyList<int> Presets,
    int Channels,
    int? SupplyMillivolts = null,
    BoxAbilities Abilities = BoxAbilities.None,
    int? BoxNumber = null,
    int? Model = null);
