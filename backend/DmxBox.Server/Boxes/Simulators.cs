using System.Diagnostics;

namespace DmxBox.Server.Boxes;

/// <summary>
/// The simulated DMX devices that run on this PC (sim.cmd, see <see cref="SimulatorRegistry"/>):
/// the desk finds them as if they were plugged in. A simulated dmxbox is looked for like a box on
/// a network adapter, a simulated Art-Net node answers Find nodes, and a simulated DMX USB
/// interface is a serial port in the list ("SIM1"). The folder is read at most twice a second.
/// </summary>
/// <remarks>Thread-safe.</remarks>
internal sealed class Simulators
{
    private static readonly TimeSpan ReadEvery = TimeSpan.FromMilliseconds(500);

    private readonly string _folder;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Lock _lock = new();
    private IReadOnlyList<SimulatedDevice> _devices = [];
    private TimeSpan? _readAt;

    /// <summary>Reads the simulators of a folder.</summary>
    /// <param name="folder">"simulators" beside the LAN password: %LOCALAPPDATA%\Lumenet\simulators for the desk of this PC.</param>
    public Simulators(string folder)
    {
        _folder = folder;
    }

    /// <summary>The folder.</summary>
    public string Folder => _folder;

    /// <summary>The devices that run now.</summary>
    public IReadOnlyList<SimulatedDevice> Devices
    {
        get
        {
            lock (_lock)
            {
                TimeSpan now = _clock.Elapsed;
                if (_readAt is not { } readAt || now - readAt >= ReadEvery)
                {
                    _devices = SimulatorRegistry.Read(_folder);
                    _readAt = now;
                }

                return _devices;
            }
        }
    }

    /// <summary>The addresses of the devices of one kind that run now.</summary>
    /// <param name="kind">One of the kinds of <see cref="SimulatedDevice"/>.</param>
    public string[] AddressesOf(string kind)
    {
        return [.. Devices.Where(device => device.Kind == kind).Select(device => device.Address)];
    }
}
