using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace DmxBox.Sim;

/// <summary>
/// A simulated dmxbox: the box simulator of firmware/sim, which runs the firmware's own engine and
/// protocol code, at 127.0.0.1 on ports of its own. The desk finds it as it finds a box on a USB
/// cable; pulled out, the process ends (and loses what it was sent, as a box does), plugged in, it
/// starts again on the same ports, with its presets kept. Its two buttons are played through the
/// DX protocol, which changes the box the same way: the blackout button switches its own
/// blackout, the preset button fades preset 0 in over 2 seconds.
/// </summary>
internal sealed class BoxSimulator : ISimulator
{
    private const string Host = "127.0.0.1";

    // The firmware's BUTTON_PRESET_SLOT and BUTTON_PRESET_FADE_MS.
    private const int ButtonPresetSlot = 0;
    private static readonly TimeSpan ButtonPresetFade = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan StartupLimit = TimeSpan.FromSeconds(10);

    private readonly BoxModel _model;
    private readonly string _folder;
    private readonly string _executable;
    private readonly int _show;
    private readonly TextWriter _output;
    private readonly int _controlPort;
    private readonly int _artNetPort;
    private readonly SimulatorClaim _claim;
    private Process? _process;

    /// <summary>Prepares the box. Nothing runs until it is plugged in.</summary>
    /// <param name="model">Which box.</param>
    /// <param name="folder">The registry of the simulators; the box keeps its presets there too.</param>
    /// <param name="executable">The simulator program of the box, built in firmware/sim.</param>
    /// <param name="show">How many channels of each output the box writes when they change; 0 for none.</param>
    /// <param name="output">Where the lines go.</param>
    public BoxSimulator(BoxModel model, string folder, string executable, int show, TextWriter output)
    {
        _model = model;
        _folder = folder;
        _executable = executable;
        _show = show;
        _output = output;
        (_controlPort, _artNetPort) = TwoFreePorts();
        _claim = SimulatorRegistry.Claim(
            folder, new SimulatedDevice(SimulatedDevice.Dmxbox, Address, $"{model.Title} (simulator)", Environment.ProcessId));
    }

    /// <summary>The boxes there are.</summary>
    public static IReadOnlyList<BoxModel> Models { get; } =
    [
        new("pico-1-universe", "dmxbox_sim", "dmxbox with the Pico, one output"),
        new("pico-2-universes", "dmxbox_sim_pico2", "dmxbox with the Pico and two outputs (no such hardware exists yet)"),
        new("pic32-1-universe", "dmxbox_sim2_1", "dmxbox 2 (PIC32) with one output"),
        new("pic32-2-universes", "dmxbox_sim2", "dmxbox 2 (PIC32) with two outputs"),
    ];

    /// <summary>Where the desk reaches the box: "127.0.0.1:52011".</summary>
    public string Address => string.Create(CultureInfo.InvariantCulture, $"{Host}:{_controlPort}");

    /// <summary>The box's DX port at 127.0.0.1.</summary>
    public int ControlPort => _controlPort;

    /// <summary>The box's Art-Net port at 127.0.0.1.</summary>
    public int ArtNetPort => _artNetPort;

    /// <inheritdoc />
    public string Title => _model.Title;

    /// <inheritdoc />
    public string Finding => string.Create(
        CultureInfo.InvariantCulture,
        $"The desk finds it by itself, at {Address} (its DX port; Art-Net at port {_artNetPort}), as it finds a box on a USB cable.");

    /// <inheritdoc />
    public IReadOnlyList<(char Key, string What)> Keys { get; } =
        [('B', "the blackout button"), ('P', "the preset button (preset 0 fades in over 2 seconds)")];

    /// <inheritdoc />
    public bool PluggedIn => _process is { HasExited: false };

    /// <inheritdoc />
    public void PlugIn()
    {
        if (PluggedIn)
            return;

        string[] arguments =
        [
            "--host", Host,
            "--ctrl-port", _controlPort.ToString(CultureInfo.InvariantCulture),
            "--artnet-port", _artNetPort.ToString(CultureInfo.InvariantCulture),
            "--presets", Path.Combine(_folder, $"presets-{_model.Device}.bin"),
            // It ends with this program, also when this one is killed.
            "--parent", Environment.ProcessId.ToString(CultureInfo.InvariantCulture),
            .. _show > 0 ? new[] { "--show", _show.ToString(CultureInfo.InvariantCulture) } : ["--quiet"],
        ];
        Directory.CreateDirectory(_folder);

        // The box writes its lines into this window itself.
        _process = Process.Start(new ProcessStartInfo(_executable, arguments) { UseShellExecute = false })
            ?? throw new InvalidOperationException($"{_executable} did not start.");
        WaitUntilItAnswers(_process);
        _claim.PluggedIn = true;
    }

    /// <inheritdoc />
    public void PullOut()
    {
        _claim.PluggedIn = false;
        if (_process is null)
            return;

        if (!_process.HasExited)
        {
            _process.Kill();
            _process.WaitForExit();
        }

        _process.Dispose();
        _process = null;
    }

    /// <inheritdoc />
    public void Press(char key)
    {
        if (!PluggedIn)
        {
            _output.WriteLine("The box is pulled out: its buttons do nothing.");
            return;
        }

        using var box = new DmxBoxClient(Host, _controlPort, TimeSpan.FromMilliseconds(200), attempts: 3);
        if (key == 'B')
        {
            // The button switches the box's own blackout over. A desk that is connected sees it at
            // its next ping, switches its Black out instead, and takes the box's own off again.
            bool on = !box.Ping().Blackout;
            box.Blackout(on);
            if (!on)
            {
                _output.WriteLine("Blackout button: the box's own blackout goes off.");
                return;
            }

            Thread.Sleep(400);
            _output.WriteLine(box.Ping().Blackout
                ? "Blackout button: the box is dark by itself, the attributes too (no desk took it over)."
                : "Blackout button: a desk took it over and switched its Black out.");
        }
        else if (key == 'P')
        {
            box.PresetGo(ButtonPresetSlot, ButtonPresetFade);
            _output.WriteLine("Preset button: preset 0 fades in over 2 seconds.");
        }
    }

    /// <inheritdoc />
    public void Tick()
    {
        if (_process is { HasExited: true })
        {
            _output.WriteLine($"The box simulator stopped (exit code {_process.ExitCode}). Press U to plug it in again.");
            PullOut();
        }
    }

    /// <summary>Pulls the box out for good, and gives its address back.</summary>
    public void Dispose()
    {
        PullOut();
        _claim.Dispose();
    }

    // Pings until the box answers: a fixed sleep is too short on a busy PC and too long on a fast one.
    private void WaitUntilItAnswers(Process process)
    {
        using var box = new DmxBoxClient(Host, _controlPort, TimeSpan.FromMilliseconds(50), attempts: 1);
        var waited = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                box.Ping();
                return;
            }
            catch (DmxBoxTimeoutException)
            {
                if (process.HasExited)
                    throw new InvalidOperationException($"The box simulator stopped at once, with exit code {process.ExitCode}.");
                if (waited.Elapsed > StartupLimit)
                    throw new TimeoutException($"The box simulator did not answer within {StartupLimit.TotalSeconds} seconds.");
            }
        }
    }

    // Two free UDP ports of 127.0.0.1, the box's DX port and its Art-Net port. Both sockets stay
    // open until both ports are known, so the two are certainly different.
    private static (int Control, int ArtNet) TwoFreePorts()
    {
        using Socket first = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        using Socket second = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        first.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        second.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return (((IPEndPoint)first.LocalEndPoint!).Port, ((IPEndPoint)second.LocalEndPoint!).Port);
    }
}

/// <summary>A dmxbox the simulator can play.</summary>
/// <param name="Device">Its name for sim.cmd: "pico-1-universe".</param>
/// <param name="Program">Its simulator program in firmware/sim, without ".exe": "dmxbox_sim".</param>
/// <param name="Title">What it is, for people.</param>
internal sealed record BoxModel(string Device, string Program, string Title);
