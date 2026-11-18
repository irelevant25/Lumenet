using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace DmxBox.Tests;

/// <summary>
/// A running dmxbox simulator (sim/dmxbox_sim): the firmware's engine and protocol code behind
/// ordinary UDP sockets. Every instance has its own two UDP ports and its own presets file, so
/// tests cannot disturb each other. Dispose stops the process and deletes the file.
/// </summary>
internal sealed class Simulator : IDisposable
{
    private const string Host = "127.0.0.1";

    private static readonly TimeSpan StartupLimit = TimeSpan.FromSeconds(10);

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("dmxbox-test-");
    private readonly double _drop;
    private Process? _process;

    private Simulator(double drop)
    {
        _drop = drop;
        (ArtNetPort, ControlPort) = FindTwoFreeUdpPorts();
    }

    /// <summary>The UDP port on which this simulator listens for Art-Net.</summary>
    public int ArtNetPort { get; }

    /// <summary>The UDP port on which this simulator listens for the DX protocol.</summary>
    public int ControlPort { get; }

    /// <summary>Starts a simulator and waits until it answers.</summary>
    /// <param name="drop">Fraction of the control packets that the simulator loses, in each direction.</param>
    public static Simulator Start(double drop = 0.0)
    {
        var simulator = new Simulator(drop);
        try
        {
            simulator.StartProcess();
            return simulator;
        }
        catch
        {
            simulator.Dispose();
            throw;
        }
    }

    /// <summary>Stops the process, like switching the box off. The presets file stays.</summary>
    public void Stop()
    {
        if (_process is null)
            return;

        _process.Kill();
        _process.WaitForExit();
        _process.Dispose();
        _process = null;
    }

    /// <summary>A power cycle: a new process with the same ports and the same presets file.</summary>
    public void Restart()
    {
        Stop();
        StartProcess();
    }

    /// <summary>Creates a client that talks to this simulator.</summary>
    public DmxBoxClient CreateClient(TimeSpan? timeout = null, int attempts = 6)
    {
        return new DmxBoxClient(Host, ControlPort, timeout, attempts);
    }

    /// <summary>Creates an Art-Net sender that sends to this simulator.</summary>
    public ArtNetSender CreateArtNetSender(int universe = 0)
    {
        return new ArtNetSender(Host, universe, ArtNetPort);
    }

    public void Dispose()
    {
        Stop();
        try
        {
            _folder.Delete(recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A leftover folder in the temp directory is harmless. It must not fail a test.
        }
    }

    private void StartProcess()
    {
        string[] arguments =
        [
            "--artnet-port", $"{ArtNetPort}",
            "--ctrl-port", $"{ControlPort}",
            // The simulator reads "0.3". $"{_drop}" would write "0,3" in many languages.
            "--drop", _drop.ToString(CultureInfo.InvariantCulture),
            "--presets", Path.Combine(_folder.FullName, "presets.bin"),
            "--quiet",
        ];
        var startInfo = new ProcessStartInfo(FindExecutable(), arguments)
        {
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("The simulator did not start.");
        _process.BeginOutputReadLine();   // read and drop what it prints, so that it never waits on a full pipe
        WaitUntilItAnswers(_process);
    }

    // Pings until the simulator answers: a fixed sleep is too short on a busy PC and too long on a fast one.
    private void WaitUntilItAnswers(Process process)
    {
        using var box = new DmxBoxClient(Host, ControlPort, TimeSpan.FromMilliseconds(50), attempts: 1);
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
                {
                    throw new InvalidOperationException(
                        $"The simulator stopped with exit code {process.ExitCode}: " +
                        process.StandardError.ReadToEnd().Trim());
                }

                if (waited.Elapsed > StartupLimit)
                {
                    throw new TimeoutException(
                        $"The simulator did not answer a ping within {StartupLimit.TotalSeconds} seconds.");
                }
            }
        }
    }

    // DMXBOX_SIM names the simulator. Without it, the simulator is sim/dmxbox_sim in the first
    // folder, going up from the test assembly, that has such a file: the root of the repository.
    private static string FindExecutable()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable("DMXBOX_SIM");
        if (!string.IsNullOrEmpty(fromEnvironment))
        {
            return File.Exists(fromEnvironment)
                ? fromEnvironment
                : throw NotFound($"DMXBOX_SIM is {fromEnvironment}, and that file does not exist");
        }

        string relativePath = Path.Combine("sim", OperatingSystem.IsWindows() ? "dmxbox_sim.exe" : "dmxbox_sim");
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            string candidate = Path.Combine(folder.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;
        }

        throw NotFound($"no folder above {AppContext.BaseDirectory} contains {relativePath}");
    }

    private static FileNotFoundException NotFound(string detail)
    {
        return new FileNotFoundException(
            $"The dmxbox simulator was not found: {detail}. Build it with sim\\build.cmd (Windows) or " +
            "make -C sim (Linux, macOS), or set DMXBOX_SIM to the path of the simulator.");
    }

    // Both sockets stay open until both ports are known, so the two ports are certainly different.
    private static (int First, int Second) FindTwoFreeUdpPorts()
    {
        using Socket first = BindToFreePort();
        using Socket second = BindToFreePort();
        return (PortOf(first), PortOf(second));
    }

    private static Socket BindToFreePort()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));   // port 0: the system picks a free port
        return socket;
    }

    private static int PortOf(Socket socket)
    {
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }
}
