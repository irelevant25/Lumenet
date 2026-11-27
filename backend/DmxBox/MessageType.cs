namespace DmxBox;

/// <summary>
/// Message types of the DX protocol, for <see cref="DmxBoxClient.Request"/>.
/// Integers in a payload are big-endian; channels are numbered from 1.
/// </summary>
public static class MessageType
{
    /// <summary>
    /// No payload. Answer: firmware major, firmware minor, flags, master, uptime in ms (u32),
    /// preset mask (u16), channel count (u16); since dmxbox 3 also the supply voltage in mV (u16),
    /// what the box can do (u8, see <see cref="BoxAbilities"/>) and its number (u8).
    /// </summary>
    public const byte Ping = 0x01;

    /// <summary>Payload: start (u16), count (u16), then one value per channel.</summary>
    public const byte Set = 0x10;

    /// <summary>Payload: start (u16), count (u16), duration in ms (u32), then one value per channel.</summary>
    public const byte Fade = 0x11;

    /// <summary>Payload: slot (u8).</summary>
    public const byte PresetSave = 0x20;

    /// <summary>Payload: slot (u8), duration in ms (u32).</summary>
    public const byte PresetGo = 0x21;

    /// <summary>Payload: slot (u8).</summary>
    public const byte PresetClear = 0x22;

    /// <summary>Payload: level (u8).</summary>
    public const byte Master = 0x30;

    /// <summary>Payload: 1 = on, 0 = off (u8).</summary>
    public const byte Blackout = 0x31;

    /// <summary>No payload.</summary>
    public const byte ReleaseArtNet = 0x32;

    /// <summary>Payload: 0 = HTP, 1 = override (u8).</summary>
    public const byte MergeMode = 0x33;

    /// <summary>Payload: start (u16), count (u16). Answer: one value per channel.</summary>
    public const byte GetOutput = 0x40;

    /// <summary>Payload: slot (u8). Answer: the 512 stored values.</summary>
    public const byte GetPreset = 0x41;

    /// <summary>No payload: the box searches its DMX line for lights with RDM again (dmxbox 3).</summary>
    public const byte RdmDiscover = 0x50;

    /// <summary>No payload. Answer: searching (u8), count (u16), then the IDs, 6 bytes each.</summary>
    public const byte RdmTod = 0x51;

    /// <summary>
    /// Payload: the light's ID (6 bytes), sub-device (u16), command class (u8), parameter (u16),
    /// data length (u8), data. Answer, when the light has answered: response type (u8), command
    /// class (u8), parameter (u16), data length (u8), data.
    /// </summary>
    public const byte RdmRequest = 0x52;

    /// <summary>No payload: the box restarts into its USB boot loader after the answer, for new firmware.</summary>
    public const byte Bootloader = 0x60;

    /// <summary>The box's answer to every request. Payload: status (u8), then the answer's data.</summary>
    public const byte Ack = 0x80;
}
