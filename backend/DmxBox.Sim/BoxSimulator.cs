using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace DmxBox.Sim;

/// <summary>
/// A simulated dmxbox 4: the box simulator of firmware/sim, which runs the firmware's own engine,
/// protocol and RDM code, at 127.0.0.1 on ports of its own, with lights on its DMX line that exist
/// only in its memory and answer RDM, and a port for its DMX IN (<see cref="ConsolePort"/>). The desk finds it as it finds a box on a USB cable; pulled
/// out, the process ends (and loses what it was sent, as a box does), plugged in, it starts again
/// on the same ports, with its presets kept.
/// </summary>
internal sealed class BoxSimulator : ISimulator
{
    private const string Host = "127.0.0.1";

    private static readonly TimeSpan StartupLimit = TimeSpan.FromSeconds(10);

    private readonly BoxModel _model;
    private readonly string _folder;
    private readonly string _executable;
    private readonly int _show;
    private readonly int _lights;
    private readonly int _boxNumber;
    private readonly TextWriter _output;
    private readonly int _controlPort;
    private readonly int _artNetPort;
    private readonly int _consolePort;
    private readonly SimulatorClaim _claim;
    private readonly DmxConsole? _console;
    private Process? _process;

    /// <summary>Prepares the box. Nothing runs until it is plugged in.</summary>
    /// <param name="model">Which box.</param>
    /// <param name="folder">The registry of the simulators; the box keeps its presets there too.</param>
    /// <param name="executable">The simulator program of the box, built in firmware/sim.</param>
    /// <param name="show">How many channels of each output the box writes when they change; 0 for none.</param>
    /// <param name="output">Where the lines go.</param>
    /// <param name="lights">How many lights with RDM are on its line, 0 to 64.</param>
    /// <param name="boxNumber">The number on its switch, 0 to 255: its Art-Net universe.</param>
    /// <param name="console">True for a console on its DMX IN, with a page of its own (<see cref="DmxConsole"/>).</param>
    public BoxSimulator(BoxModel model, string folder, string executable, int show, TextWriter output, int lights = 3, int boxNumber = 0, bool console = false)
    {
        _model = model;
        _folder = folder;
        _executable = executable;
        _show = show;
        _lights = lights;
        _boxNumber = boxNumber;
        _output = output;
        (_controlPort, _artNetPort, _consolePort) = ThreeFreePorts();
        _claim = SimulatorRegistry.Claim(
            folder, new SimulatedDevice(SimulatedDevice.Dmxbox, Address, $"{model.Title} (simulator)", Environment.ProcessId));
        _console = console ? new DmxConsole(_consolePort, output) : null;
    }

    /// <summary>The console on its DMX IN; null for a box without one.</summary>
    public DmxConsole? Console => _console;

    /// <summary>The boxes there are.</summary>
    public static IReadOnlyList<BoxModel> Models { get; } =
    [
        new("dmxbox", "dmxbox_sim", "dmxbox 4, one output, lights with RDM on its line"),
        new("dmxbox-2-universes", "dmxbox_sim2", "dmxbox 4 with two outputs (no such box: for tests)"),
    ];

    /// <summary>The UDP port of the box's DMX IN at 127.0.0.1: a datagram of 1 to 512 levels there is a frame from a console.</summary>
    public int ConsolePort => _consolePort;

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
    public IReadOnlyList<(char Key, string What)> Keys => _console is null ? [] : [('C', "opens the page of the console on its DMX IN")];

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
            "--console-port", _consolePort.ToString(CultureInfo.InvariantCulture),
            "--presets", Path.Combine(_folder, $"presets-{_model.Device}.bin"),
            "--rdm-lights", _lights.ToString(CultureInfo.InvariantCulture),
            "--box", _boxNumber.ToString(CultureInfo.InvariantCulture),
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
        if (_console is not null)
            _console.Sending = true;
    }

    /// <inheritdoc />
    public void PullOut()
    {
        _claim.PluggedIn = false;
        if (_console is not null)
            _console.Sending = false;
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
        // The boxes have no buttons: the one key is the console's page.
        if (key == 'C')
            _console?.OpenPage();
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
        _console?.Dispose();
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
    private static (int Control, int ArtNet, int Console) ThreeFreePorts()
    {
        using Socket first = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        using Socket second = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        using Socket third = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        first.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        second.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        third.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return (((IPEndPoint)first.LocalEndPoint!).Port, ((IPEndPoint)second.LocalEndPoint!).Port,
            ((IPEndPoint)third.LocalEndPoint!).Port);
    }
}

/// <summary>A dmxbox the simulator can play.</summary>
/// <param name="Device">Its name for sim.cmd: "dmxbox".</param>
/// <param name="Program">Its simulator program in firmware/sim, without ".exe": "dmxbox_sim".</param>
/// <param name="Title">What it is, for people.</param>
internal sealed record BoxModel(string Device, string Program, string Title);
