using System.Diagnostics;
using System.Text.Json;

namespace DmxBox;

/// <summary>
/// A DMX device that a simulator plays on this PC (see sim.cmd at the root of the repository), as
/// the simulator tells the Lumenet desk about it: a device that is plugged in is there for the
/// desk, one that is pulled out is not, and one whose simulator ends is gone.
/// </summary>
/// <param name="Kind"><see cref="Dmxbox"/>, <see cref="ArtNet"/>, <see cref="Sacn"/>, <see cref="Usb"/> or <see cref="Midi"/>.</param>
/// <param name="Address">
/// Where the device is reached: "127.0.0.1:52011" for a dmxbox (its DX port), "127.0.0.2" for an
/// Art-Net node or an sACN receiver, "SIM1" for a DMX USB interface, "MIDI1" for a MIDI controller
/// (its pipe, see <see cref="MidiPipeName"/>). One simulator has it at a time.
/// </param>
/// <param name="Name">What the device is, for people: "dmxbox with the Pico (simulator)".</param>
/// <param name="ProcessId">The simulator's process. A device whose process has ended is gone.</param>
public sealed record SimulatedDevice(string Kind, string Address, string Name, int ProcessId)
{
    /// <summary>The <see cref="Kind"/> of a dmxbox, reached with the DX protocol.</summary>
    public const string Dmxbox = "dmxbox";

    /// <summary>The <see cref="Kind"/> of an Art-Net node.</summary>
    public const string ArtNet = "artnet";

    /// <summary>The <see cref="Kind"/> of an sACN receiver.</summary>
    public const string Sacn = "sacn";

    /// <summary>The <see cref="Kind"/> of a DMX USB interface of the kind of the Enttec DMX USB Pro.</summary>
    public const string Usb = "usb";

    /// <summary>
    /// The <see cref="Kind"/> of a MIDI controller: a device with an input and an output under its
    /// <see cref="Name"/>, played through the named pipe of its address.
    /// </summary>
    public const string Midi = "midi";

    /// <summary>
    /// The named pipe of a simulated MIDI controller, "lumenet-midi-MIDI1": the MIDI bytes go both
    /// ways in it, as no MIDI port can be made on Windows without a driver.
    /// </summary>
    /// <param name="address">Its address, "MIDI1".</param>
    public static string MidiPipeName(string address) => $"lumenet-midi-{address.ToUpperInvariant()}";

    /// <summary>True while the device is plugged in; a pulled-out device keeps its address for its simulator.</summary>
    public bool PluggedIn { get; init; }
}

/// <summary>
/// Where the simulators of DMX devices say that they run: a folder with a small file for each
/// device, named after its address. The Lumenet desk reads it to find the simulated devices that
/// are plugged in, as if they were. A simulator claims its address when it starts (<see cref="Claim"/>),
/// so that no other one takes it, says when its device is plugged in or pulled out, and gives the
/// address back when it ends; the file of a simulator that was killed is passed over, and removed,
/// once its process is gone.
/// </summary>
public static class SimulatorRegistry
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>
    /// The folder of the desk on this PC: "simulators" beside the desk's LAN password, in
    /// %LOCALAPPDATA%\Lumenet (~/.local/share/Lumenet on Linux).
    /// </summary>
    public static string DefaultFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumenet", "simulators");

    /// <summary>
    /// Claims an address for a device, pulled out at first: no other simulator can take it until the
    /// claim is disposed.
    /// </summary>
    /// <param name="folder">The folder of the desk, <see cref="DefaultFolder"/> unless a test desk has another.</param>
    /// <param name="device">The device; its <see cref="SimulatedDevice.ProcessId"/> is this process.</param>
    /// <exception cref="AddressTakenException">Another simulator that runs has the address.</exception>
    public static SimulatorClaim Claim(string folder, SimulatedDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, FileName(device.Address));
        device = device with { PluggedIn = false };
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                // CreateNew: of two simulators that start at the same moment, one gets the address.
                using (var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                    JsonSerializer.Serialize(stream, device, Options);
                return new SimulatorClaim(file, device);
            }
            catch (IOException) when (File.Exists(file))
            {
                SimulatedDevice? holder = ReadFile(file);
                if (holder is not null && Runs(holder.ProcessId))
                    throw new AddressTakenException($"{device.Address} is taken by another simulator ({holder.Name}).");

                TryDelete(file);   // a simulator that was killed, or a file half written: try once more
            }
        }

        throw new AddressTakenException($"{device.Address} could not be claimed.");
    }

    /// <summary>The devices whose simulators run now, plugged in, in the order of their files' names.</summary>
    /// <param name="folder">The folder of the desk. A folder that does not exist has none.</param>
    /// <param name="pulledOutToo">True to have the devices that are pulled out as well: every address that is claimed.</param>
    public static IReadOnlyList<SimulatedDevice> Read(string folder, bool pulledOutToo = false)
    {
        if (!Directory.Exists(folder))
            return [];

        var devices = new List<SimulatedDevice>();
        string[] files;
        try
        {
            files = Directory.GetFiles(folder, "*.json");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        foreach (string file in files.Order(StringComparer.Ordinal))
        {
            SimulatedDevice? device = ReadFile(file);
            if (device is null)
                continue;

            if (!Runs(device.ProcessId))
                TryDelete(file);   // a simulator that was killed: its device is gone
            else if (device.PluggedIn || pulledOutToo)
                devices.Add(device);
        }

        return devices;
    }

    // "127.0.0.1-52011.json", "SIM1.json": one file for each address.
    private static string FileName(string address)
    {
        return string.Concat(address.Select(c => char.IsAsciiLetterOrDigit(c) || c == '.' ? c : '-')) + ".json";
    }

    internal static SimulatedDevice? ReadFile(string file)
    {
        try
        {
            SimulatedDevice? device = JsonSerializer.Deserialize<SimulatedDevice>(File.ReadAllText(file), Options);
            return device is { Kind: not null, Address: not null, Name: not null } ? device : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;   // being written right now, or no file of a simulator: the next look counts
        }
    }

    internal static void Write(string file, SimulatedDevice device)
    {
        // Written beside it and then put in its place, so that a desk never reads half of it. A desk
        // that reads it at that very moment holds it for a moment: then once more.
        string written = $"{file}.{Environment.ProcessId}.tmp";
        File.WriteAllText(written, JsonSerializer.Serialize(device, Options));
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(written, file, overwrite: true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < 10)
            {
                Thread.Sleep(20);
            }
        }
    }

    internal static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Another desk removes it as well, or it is in use: the next look passes over it again.
        }
    }

    private static bool Runs(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}

/// <summary>An address a simulator has claimed (<see cref="SimulatorRegistry.Claim"/>), for as long as it runs.</summary>
public sealed class SimulatorClaim : IDisposable
{
    private readonly string _file;
    private SimulatedDevice _device;
    private bool _disposed;

    internal SimulatorClaim(string file, SimulatedDevice device)
    {
        _file = file;
        _device = device;
    }

    /// <summary>The device as the desk reads it.</summary>
    public SimulatedDevice Device => _device;

    /// <summary>Plugged in, the desk finds the device; pulled out, it does not, and the address stays claimed.</summary>
    public bool PluggedIn
    {
        get => _device.PluggedIn;
        set
        {
            if (value == _device.PluggedIn)
                return;

            ObjectDisposedException.ThrowIf(_disposed, this);
            _device = _device with { PluggedIn = value };
            SimulatorRegistry.Write(_file, _device);
        }
    }

    /// <summary>Gives the address back: the device is gone.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _device = _device with { PluggedIn = false };
        SimulatorRegistry.TryDelete(_file);
    }
}

/// <summary>Another simulator that runs has the address.</summary>
public sealed class AddressTakenException : IOException
{
    /// <summary>Makes the exception.</summary>
    /// <param name="message">Which address, and who has it.</param>
    public AddressTakenException(string message)
        : base(message)
    {
    }
}
