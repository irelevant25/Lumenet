using System.Buffers.Binary;
using System.Globalization;

namespace DmxBox;

/// <summary>
/// The 48-bit ID of a device with RDM (ANSI E1.20): the maker's number (ESTA's list) and the
/// device's own. Written "7FF1:00000001", as RDM tools do.
/// </summary>
/// <param name="Manufacturer">The maker's number; 7FF0 to 7FFF are kept for prototypes.</param>
/// <param name="Device">The device's own number.</param>
public readonly record struct RdmUid(ushort Manufacturer, uint Device) : IComparable<RdmUid>
{
    /// <summary>In the order of the 48-bit numbers: by maker, then by device.</summary>
    public int CompareTo(RdmUid other)
    {
        int maker = Manufacturer.CompareTo(other.Manufacturer);
        return maker != 0 ? maker : Device.CompareTo(other.Device);
    }

    /// <summary>The ID as the 6 bytes of a packet, the maker's number first.</summary>
    public byte[] ToBytes()
    {
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, Manufacturer);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(2), Device);
        return bytes;
    }

    /// <summary>The ID from the 6 bytes of a packet.</summary>
    public static RdmUid FromBytes(ReadOnlySpan<byte> bytes)
    {
        return new RdmUid(BinaryPrimitives.ReadUInt16BigEndian(bytes), BinaryPrimitives.ReadUInt32BigEndian(bytes[2..]));
    }

    /// <summary>The ID from "7FF1:00000001" (hex, either case); false for anything else.</summary>
    public static bool TryParse(string? text, out RdmUid uid)
    {
        uid = default;
        string[] parts = (text ?? "").Trim().Split(':');
        if (parts.Length != 2 || parts[0].Length is < 1 or > 4 || parts[1].Length is < 1 or > 8)
            return false;
        if (!ushort.TryParse(parts[0], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort maker)
            || !uint.TryParse(parts[1], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint device))
        {
            return false;
        }

        uid = new RdmUid(maker, device);
        return true;
    }

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Manufacturer:X4}:{Device:X8}");
}

/// <summary>What an RDM request does with its parameter.</summary>
public enum RdmCommandClass : byte
{
    /// <summary>Reads it.</summary>
    Get = 0x20,

    /// <summary>Changes it.</summary>
    Set = 0x30,
}

/// <summary>How a device answered an RDM request (the "response type").</summary>
public enum RdmResponseType : byte
{
    /// <summary>Done; the answer's data is the parameter.</summary>
    Ack = 0x00,

    /// <summary>The device needs more time; its data says how long (in tenths of a second).</summary>
    AckTimer = 0x01,

    /// <summary>Refused; the data holds the reason (E1.20 table A-17), see <see cref="RdmResponse.NackReason"/>.</summary>
    NackReason = 0x02,

    /// <summary>Done, but there is more: the same request again brings the rest.</summary>
    AckOverflow = 0x03,
}

/// <summary>A device's answer to one RDM request, as the box handed it on.</summary>
/// <param name="Type">Done, refused, or later.</param>
/// <param name="CommandClass">The answer's command class: GET_COMMAND_RESPONSE (0x21) or SET_COMMAND_RESPONSE (0x31).</param>
/// <param name="Parameter">The parameter it is about (PID).</param>
/// <param name="Data">The parameter data.</param>
public sealed record RdmResponse(RdmResponseType Type, byte CommandClass, ushort Parameter, byte[] Data)
{
    /// <summary>For a refusal, its reason (E1.20 table A-17, 0x0000 = unknown parameter); null otherwise.</summary>
    public ushort? NackReason => Type == RdmResponseType.NackReason && Data.Length >= 2
        ? BinaryPrimitives.ReadUInt16BigEndian(Data)
        : null;
}

/// <summary>The lights with RDM a box has found on its DMX line.</summary>
/// <param name="Searching">True while the box is still searching: the list may grow.</param>
/// <param name="Lights">Their IDs, in the order the box found them.</param>
public sealed record RdmLightList(bool Searching, IReadOnlyList<RdmUid> Lights);

/// <summary>The parameters (PIDs) of E1.20 the desk uses.</summary>
public static class RdmParameter
{
    /// <summary>Which optional parameters the device has: a list of PIDs.</summary>
    public const ushort SupportedParameters = 0x0050;

    /// <summary>Model, category, software, footprint, mode and start address, in 19 bytes (see <see cref="RdmDeviceInfo"/>).</summary>
    public const ushort DeviceInfo = 0x0060;

    /// <summary>The model's name, text.</summary>
    public const ushort DeviceModelDescription = 0x0080;

    /// <summary>The maker's name, text.</summary>
    public const ushort ManufacturerLabel = 0x0081;

    /// <summary>The name the user gave the device, text; GET and SET.</summary>
    public const ushort DeviceLabel = 0x0082;

    /// <summary>The software version as text.</summary>
    public const ushort SoftwareVersionLabel = 0x00C0;

    /// <summary>The mode ("personality") in use and how many there are; SET with the mode, 1 to n.</summary>
    public const ushort DmxPersonality = 0x00E0;

    /// <summary>One mode: GET with its number; answers the number, its channels (u16) and its name.</summary>
    public const ushort DmxPersonalityDescription = 0x00E1;

    /// <summary>The DMX start address, 1 to 512 (u16); GET and SET.</summary>
    public const ushort DmxStartAddress = 0x00F0;

    /// <summary>The device shows where it is (blinks, moves): 1 on, 0 off; GET and SET.</summary>
    public const ushort IdentifyDevice = 0x1000;
}

/// <summary>The answer to DEVICE_INFO (E1.20 10.5.1).</summary>
/// <param name="Model">The model's number at its maker.</param>
/// <param name="Category">What kind of device (E1.20 table A-5): 0x0101 a fixed light, 0x0102 a moving head, 0x0500 a dimmer ...</param>
/// <param name="SoftwareVersion">The maker's number of its software.</param>
/// <param name="Footprint">How many DMX channels it takes in its mode.</param>
/// <param name="Mode">The mode ("personality") in use, from 1.</param>
/// <param name="Modes">How many modes it has.</param>
/// <param name="StartAddress">Its first DMX channel, 1 to 512; 0xFFFF for a device without channels.</param>
/// <param name="SubDevices">How many sub-devices it has.</param>
public sealed record RdmDeviceInfo(
    ushort Model, ushort Category, uint SoftwareVersion, int Footprint, int Mode, int Modes, int StartAddress, int SubDevices)
{
    /// <summary>The info from the 19 bytes of the answer; null if they are fewer.</summary>
    public static RdmDeviceInfo? Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 19)
            return null;

        return new RdmDeviceInfo(
            Model: BinaryPrimitives.ReadUInt16BigEndian(data[2..]),
            Category: BinaryPrimitives.ReadUInt16BigEndian(data[4..]),
            SoftwareVersion: BinaryPrimitives.ReadUInt32BigEndian(data[6..]),
            Footprint: BinaryPrimitives.ReadUInt16BigEndian(data[10..]),
            Mode: data[12],
            Modes: data[13],
            StartAddress: BinaryPrimitives.ReadUInt16BigEndian(data[14..]),
            SubDevices: BinaryPrimitives.ReadUInt16BigEndian(data[16..]));
    }
}

/// <summary>What a box can do beyond DMX, as its PING says (firmware 3.0 and later).</summary>
[Flags]
public enum BoxAbilities
{
    /// <summary>Nothing more, or a box that does not say (firmware before 3.0).</summary>
    None = 0,

    /// <summary>RDM on its DMX line: <see cref="DmxBoxClient.RdmLights"/> and the other RDM requests.</summary>
    Rdm = 1,

    /// <summary>A USB MIDI port: the computer sees it as a MIDI interface.</summary>
    Midi = 2,

    /// <summary>A USB hub with an OUT socket for the next box.</summary>
    Chain = 4,

    /// <summary>A DMX input: what a console sends to it, <see cref="DmxBoxClient.GetInput"/> (dmxbox 4, firmware 3.2 and later).</summary>
    DmxIn = 8,
}
