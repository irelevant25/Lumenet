using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Sockets;
using System.Text;

namespace DmxBox.Server.Rdm;

/// <summary>
/// RDM for the desk: the lights with RDM on the DMX lines of the boxes (dmxbox 3), read and changed
/// through the boxes. Each box gets a connection of its own here (its own DX session), so RDM never
/// waits behind the levels the box link sends, nor they behind RDM.
/// </summary>
/// <remarks>
/// Every method talks to the boxes and waits for them: call it off the hub's thread. A box answers
/// a request between two DMX frames, a few tens of milliseconds; reading one light takes several.
/// </remarks>
internal sealed class RdmService : IDisposable
{
    /// <summary>The longest name RDM keeps (DEVICE_LABEL).</summary>
    public const int MaxLabel = 32;

    // A light with more modes: only the first ones get their names read.
    private const int MaxModes = 32;

    private static readonly TimeSpan RetryWait = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan LongestTimerWait = TimeSpan.FromSeconds(1);

    private readonly BoxNetwork _boxes;
    private readonly ConcurrentDictionary<string, DmxBoxClient> _clients = new(StringComparer.Ordinal);

    /// <summary>Makes the service. It talks to no box until it is asked.</summary>
    /// <param name="boxes">The boxes the desk knows: only those are asked.</param>
    public RdmService(BoxNetwork boxes)
    {
        _boxes = boxes;
    }

    /// <summary>The boxes that answer, each with the lights with RDM it found on its line.</summary>
    public RdmBoxView[] Boxes()
    {
        return [.. _boxes.Boxes.Where(box => box.Connected).Select(box => Box(box.Address))];
    }

    /// <summary>The box searches its line again. <see cref="Boxes"/> says when it is done.</summary>
    /// <param name="address">The box, as in <see cref="BoxView.Address"/>.</param>
    public void Search(string address)
    {
        Talk(address, null, client => client.RdmDiscover());
    }

    /// <summary>One light as the RDM page shows it, read from the light itself.</summary>
    /// <param name="address">The box on whose line it is.</param>
    /// <param name="id">Its RDM ID, "7FF1:00000001".</param>
    public RdmLightView Light(string address, string id)
    {
        RdmUid uid = ParseId(id);
        DmxBoxClient client = Client(address);
        try
        {
            return Read(client, address, uid);
        }
        catch (DmxBoxException e) when (e.StatusCode == 6)
        {
            return Missing(address, uid, "The light does not answer.");
        }
        catch (DmxBoxTimeoutException)
        {
            return Missing(address, uid, "The box does not answer.");
        }
        catch (SocketException)
        {
            return Missing(address, uid, "The box cannot be reached.");
        }
    }

    /// <summary>Changes a light and answers it as it is afterwards.</summary>
    /// <param name="address">The box on whose line it is.</param>
    /// <param name="id">Its RDM ID.</param>
    /// <param name="change">What changes; what is not given stays.</param>
    public RdmLightView Change(string address, string id, RdmLightChange change)
    {
        RdmUid uid = ParseId(id);
        if (change.Address is < 1 or > Desk.UniverseSize)
            throw new DeskException($"A DMX address is 1 to {Desk.UniverseSize}, not {change.Address}.");
        if (change.Mode is < 1 or > 255)
            throw new DeskException($"There is no mode {change.Mode}.");

        byte[]? label = change.Label is null ? null : LabelBytes(change.Label);
        return Talk(address, uid, client =>
        {
            // The mode before the address: the mode decides how many channels the light takes.
            if (change.Mode is { } mode)
                Set(client, uid, RdmParameter.DmxPersonality, [(byte)mode]);
            if (change.Address is { } start)
            {
                byte[] value = new byte[2];
                BinaryPrimitives.WriteUInt16BigEndian(value, (ushort)start);
                Set(client, uid, RdmParameter.DmxStartAddress, value);
            }

            if (label is not null)
                Set(client, uid, RdmParameter.DeviceLabel, label);
            if (change.Identify is { } on)
                Set(client, uid, RdmParameter.IdentifyDevice, [on ? (byte)1 : (byte)0]);
            return Read(client, address, uid);
        });
    }

    /// <summary>Closes the connections.</summary>
    public void Dispose()
    {
        foreach (DmxBoxClient client in _clients.Values)
            client.Dispose();
        _clients.Clear();
    }

    /// <summary>
    /// The name as RDM keeps it: letters without their accents ("Predný" is "Predny"), at most
    /// <see cref="MaxLabel"/> characters; RDM's texts are plain ASCII.
    /// </summary>
    /// <param name="label">The name as typed.</param>
    internal static byte[] LabelBytes(string label)
    {
        string plain = new([.. label.Trim().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => c is >= ' ' and <= '~' ? c : '?')]);
        if (plain.Length > MaxLabel)
            throw new DeskException($"A name for RDM has at most {MaxLabel} characters, this one has {plain.Length}.");
        return Encoding.ASCII.GetBytes(plain);
    }

    // What a light's refusal (NACK) means, E1.20 table A-17.
    internal static string Refusal(ushort? reason) => reason switch
    {
        0x0000 => "it does not know that setting",
        0x0001 => "the value has the wrong form",
        0x0002 => "it has a hardware fault",
        0x0004 => "it is write-protected",
        0x0005 => "that setting cannot be changed",
        0x0006 => "the value is out of its range",
        0x0007 => "it is busy, try again",
        _ => string.Create(CultureInfo.InvariantCulture, $"reason {reason ?? 0:X4}"),
    };

    private RdmBoxView Box(string address)
    {
        try
        {
            DmxBoxClient client = Client(address);
            PingInfo ping = client.Ping();
            if (!ping.Abilities.HasFlag(BoxAbilities.Rdm))
                return new RdmBoxView(address, ping.BoxNumber, false, false, [], null);

            RdmLightList list = client.RdmLights();
            return new RdmBoxView(address, ping.BoxNumber, true, list.Searching, [.. list.Lights.Select(light => light.ToString())], null);
        }
        catch (DmxBoxException e) when (e.StatusCode is 3 or 9)
        {
            return new RdmBoxView(address, null, false, false, [], null);   // firmware without RDM
        }
        catch (DmxBoxTimeoutException)
        {
            return new RdmBoxView(address, null, false, false, [], "The box does not answer.");
        }
        catch (SocketException)
        {
            return new RdmBoxView(address, null, false, false, [], "The box cannot be reached.");
        }
    }

    // A request to a box, with its failures as messages for the user.
    private T Talk<T>(string address, RdmUid? uid, Func<DmxBoxClient, T> request)
    {
        DmxBoxClient client = Client(address);
        try
        {
            return request(client);
        }
        catch (DmxBoxTimeoutException)
        {
            throw new DeskException($"The box at {address} does not answer.");
        }
        catch (DmxBoxException e) when (e.StatusCode == 6)
        {
            throw new DeskException($"The light {uid} does not answer.");
        }
        catch (DmxBoxException e) when (e.StatusCode is 3 or 9)
        {
            throw new DeskException($"The box at {address} has no RDM.");
        }
        catch (DmxBoxException e)
        {
            throw new DeskException($"The box at {address} answered {e.StatusName}.");
        }
        catch (SocketException)
        {
            throw new DeskException($"The box at {address} cannot be reached.");
        }
    }

    private void Talk(string address, RdmUid? uid, Action<DmxBoxClient> request)
    {
        Talk<bool>(address, uid, client =>
        {
            request(client);
            return true;
        });
    }

    // The connection to a box the desk knows; never to an address a page made up.
    private DmxBoxClient Client(string address)
    {
        if (!_boxes.Boxes.Any(box => box.Address == address))
            throw new DeskException($"The desk knows no box at {address}.");

        return _clients.GetOrAdd(address, key =>
        {
            int colon = key.IndexOf(':', StringComparison.Ordinal);
            return colon < 0
                ? new DmxBoxClient(key)
                : new DmxBoxClient(key[..colon], int.Parse(key[(colon + 1)..], CultureInfo.InvariantCulture));
        });
    }

    private static RdmUid ParseId(string id)
    {
        if (!RdmUid.TryParse(id, out RdmUid uid))
            throw new DeskException($"\"{id}\" is no RDM ID like 7FF1:00000001.");
        return uid;
    }

    private static RdmLightView Missing(string address, RdmUid uid, string error)
    {
        return new RdmLightView(address, uid.ToString(), "", "", "", 0, 0, 0, [], false, error);
    }

    private static RdmLightView Read(DmxBoxClient client, string address, RdmUid uid)
    {
        RdmResponse info = Ask(client, uid, RdmCommandClass.Get, RdmParameter.DeviceInfo);
        RdmDeviceInfo? device = info.Type == RdmResponseType.Ack ? RdmDeviceInfo.Parse(info.Data) : null;
        if (device is null)
            return Missing(address, uid, "The light does not tell what it is (DEVICE_INFO).");

        var modes = new List<RdmModeView>();
        for (int mode = 1; mode <= Math.Min(device.Modes, MaxModes); mode++)
        {
            RdmResponse answer = Ask(client, uid, RdmCommandClass.Get, RdmParameter.DmxPersonalityDescription, [(byte)mode]);
            modes.Add(answer.Type == RdmResponseType.Ack && answer.Data.Length >= 3
                ? new RdmModeView(BinaryPrimitives.ReadUInt16BigEndian(answer.Data.AsSpan(1)), Text(answer.Data.AsSpan(3)))
                : new RdmModeView(mode == device.Mode ? device.Footprint : 0, ""));
        }

        RdmResponse identify = Ask(client, uid, RdmCommandClass.Get, RdmParameter.IdentifyDevice);
        return new RdmLightView(
            address,
            uid.ToString(),
            GetText(client, uid, RdmParameter.ManufacturerLabel),
            GetText(client, uid, RdmParameter.DeviceModelDescription),
            GetText(client, uid, RdmParameter.DeviceLabel),
            device.StartAddress is >= 1 and <= Desk.UniverseSize ? device.StartAddress : 0,
            device.Footprint,
            device.Mode,
            [.. modes],
            identify.Type == RdmResponseType.Ack && identify.Data.Length >= 1 && identify.Data[0] == 1,
            null);
    }

    // A text parameter; empty when the light has none (it refuses the request).
    private static string GetText(DmxBoxClient client, RdmUid uid, ushort parameter)
    {
        RdmResponse answer = Ask(client, uid, RdmCommandClass.Get, parameter);
        return answer.Type == RdmResponseType.Ack ? Text(answer.Data) : "";
    }

    private static string Text(ReadOnlySpan<byte> bytes)
    {
        return Encoding.Latin1.GetString(bytes).TrimEnd('\0', ' ');
    }

    private static void Set(DmxBoxClient client, RdmUid uid, ushort parameter, byte[] value)
    {
        RdmResponse answer = Ask(client, uid, RdmCommandClass.Set, parameter, value);
        if (answer.Type == RdmResponseType.NackReason)
            throw new DeskException($"The light {uid} refused: {Refusal(answer.NackReason)}.");
    }

    // One request, asked again when its answer came spoiled or the box was busy with another
    // program's request, and after the wait a light asks for (ACK_TIMER).
    private static RdmResponse Ask(DmxBoxClient client, RdmUid uid, RdmCommandClass commandClass, ushort parameter, byte[]? value = null)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                RdmResponse answer = client.RdmRequest(uid, commandClass, parameter, value);
                if (answer.Type != RdmResponseType.AckTimer || attempt >= 3)
                    return answer;

                int tenths = answer.Data.Length >= 2 ? BinaryPrimitives.ReadUInt16BigEndian(answer.Data) : 1;
                TimeSpan wait = TimeSpan.FromMilliseconds(100 * Math.Max(1, tenths));
                Thread.Sleep(wait < LongestTimerWait ? wait : LongestTimerWait);
            }
            catch (DmxBoxException e) when (e.StatusCode is 7 or 8 && attempt < 3)
            {
                Thread.Sleep(RetryWait);
            }
        }
    }
}
