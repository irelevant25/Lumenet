using System.Diagnostics;
using System.Globalization;
using System.Net;

namespace DmxBox.Sim;

/// <summary>
/// sim.cmd: starts a simulator of a DMX device, as if the device were plugged into this PC. The
/// desk finds it by itself; U pulls it out and plugs it in again, Q (or Ctrl+C) ends it.
/// </summary>
internal static class Program
{
    private const string Usage = """
        Starts a simulated DMX device, as if it were plugged into this PC. The desk finds it by itself.

          sim.cmd <device> [options]

        Devices:
          pico-1-universe     dmxbox with the Pico, one output
          pico-2-universes    dmxbox with the Pico and two outputs (no such hardware exists yet)
          pic32-1-universe    dmxbox 2 (PIC32) with one output
          pic32-2-universes   dmxbox 2 (PIC32) with two outputs
          artnet              an Art-Net node
          sacn                an sACN receiver
          enttec              an Enttec DMX USB Pro

        Options:
          --show 16                   how many channels it writes when they change (1 to 512)
          --folder <folder>           where the desk looks for simulators: "simulators" beside its LAN
                                      password, %LOCALAPPDATA%\Lumenet\simulators unless the desk was
                                      given another --Lan:PasswordFile
          artnet: --name "Sim node"   its short name
                  --ports 2           how many ports, 1 to 4
                  --at 0:0:0          the Port-Address of its first port (Net:Sub-Net:Universe); the
                                      others follow it
                  --address 127.0.0.2 --port 6454
          sacn:   --universes 1-4     the sACN universes it takes, "1-4" or "1,3,5"
                  --multicast         take the desk's multicast too (Windows may ask about its firewall)
                  --address 127.0.0.2 --port 5568
          enttec: --serial 12345678   its serial number
                  --name SIM1         its port

        Keys while it runs: U pulls it out and plugs it in again, Q ends it (Ctrl+C too); a dmxbox
        also has B, its blackout button, and P, its preset button.
        """;

    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help" or "/?")
        {
            Console.WriteLine(Usage);
            return 0;
        }

        try
        {
            var options = Options.Parse(args[1..]);
            string folder = options.Text("--folder") ?? SimulatorRegistry.DefaultFolder;
            int show = options.Number("--show", 16, 1, 512);
            using ISimulator simulator = Make(args[0].ToLowerInvariant(), options, folder, show);
            options.RequireAllUsed();
            return Run(simulator, folder);
        }
        catch (UsageException e)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine("sim.cmd without anything lists the devices and options.");
            return 2;
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or TimeoutException
            or System.Net.Sockets.SocketException or ArgumentException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"The simulator cannot run: {e.Message}");
            return 1;
        }
    }

    private static ISimulator Make(string device, Options options, string folder, int show)
    {
        string singular = device.EndsWith("-universe", StringComparison.Ordinal) ? device + "s" : device;
        if (BoxSimulator.Models.FirstOrDefault(model => model.Device == device || model.Device == singular) is { } box)
        {
            options.RequireAllUsed();   // before a build that may take a while
            return new BoxSimulator(box, folder, BoxPrograms.Find(box.Program), show, Console.Out);
        }

        return device switch
        {
            "artnet" or "art-net" => ArtNetNode(options, folder, show),
            "sacn" => new SacnReceiverSimulator(
                folder,
                options.Universes("--universes", "1-8"),
                options.Flag("--multicast"),
                options.Address("--address"),
                options.Number("--port", SacnSender.DefaultPort, 1, 65535),
                show,
                Console.Out),
            "enttec" or "enttec-pro" or "usb" => new EnttecProSimulator(
                folder, options.Text("--name")?.ToUpperInvariant(), options.Text("--serial") ?? "12345678", show, Console.Out),
            _ => throw new UsageException($"\"{device}\" is no device the simulator knows."),
        };
    }

    private static ArtNetNodeSimulator ArtNetNode(Options options, string folder, int show)
    {
        int count = options.Number("--ports", 2, 1, 4);
        int first = options.PortAddress("--at", 0);
        if ((first & 0x0F) + count > 16)
            throw new UsageException("The ports of a node share one Net and one Sub-Net: start lower with --at, or take fewer ports.");

        return new ArtNetNodeSimulator(
            folder,
            options.Text("--name") ?? "Sim node",
            [.. Enumerable.Range(first, count)],
            options.Address("--address"),
            options.Number("--port", ArtNetSender.DefaultPort, 1, 65535),
            show,
            Console.Out);
    }

    // Plugs the device in, writes what it does, and takes the keys until Q, Esc or Ctrl+C.
    private static int Run(ISimulator simulator, string folder)
    {
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler onCtrlC = (_, press) =>
        {
            press.Cancel = true;   // pulled out properly below, not killed
            stop.Cancel();
        };
        Console.CancelKeyPress += onCtrlC;
        try
        {
            return Play(simulator, folder, stop.Token);
        }
        finally
        {
            Console.CancelKeyPress -= onCtrlC;
        }
    }

    private static int Play(ISimulator simulator, string folder, CancellationToken stopping)
    {
        simulator.PlugIn();
        Console.WriteLine($"{simulator.Title}: plugged in.");
        Console.WriteLine(simulator.Finding);
        string ownKeys = string.Concat(simulator.Keys.Select(key => $"{key.Key} {key.What}, "));
        Console.WriteLine($"Keys: {ownKeys}U pulls it out and plugs it in again, Q ends it.");
        if (folder != SimulatorRegistry.DefaultFolder)
            Console.WriteLine($"(For the desk whose simulators are in {folder}.)");

        // Keys come from the keyboard; from a script, as lines ("u", "b", "q") on the input.
        var keys = new System.Collections.Concurrent.ConcurrentQueue<char>();
        if (Console.IsInputRedirected)
            new Thread(() => ReadLines(keys)) { IsBackground = true, Name = "input" }.Start();

        bool quit = false;
        while (!quit && !stopping.IsCancellationRequested)
        {
            while (!Console.IsInputRedirected && Console.KeyAvailable)
                keys.Enqueue(Console.ReadKey(intercept: true).KeyChar);

            while (!quit && keys.TryDequeue(out char pressed))
            {
                char key = char.ToUpperInvariant(pressed);
                if (key is 'Q' || key == (char)27)
                    quit = true;
                else if (key == 'U')
                    Replug(simulator);
                else if (simulator.Keys.Any(own => own.Key == key))
                    Try(() => simulator.Press(key));
            }

            simulator.Tick();
            stopping.WaitHandle.WaitOne(50);
        }

        simulator.PullOut();
        Console.WriteLine($"{simulator.Title}: pulled out.");
        return 0;
    }

    // The first letter of every line of the input; its end counts as Q.
    private static void ReadLines(System.Collections.Concurrent.ConcurrentQueue<char> keys)
    {
        string? line;
        while ((line = Console.In.ReadLine()) is not null)
        {
            if (line.Trim().Length > 0)
                keys.Enqueue(line.Trim()[0]);
        }

        keys.Enqueue('Q');
    }

    private static void Replug(ISimulator simulator)
    {
        if (simulator.PluggedIn)
        {
            simulator.PullOut();
            Console.WriteLine("Pulled out. U plugs it in again.");
        }
        else
        {
            Try(simulator.PlugIn);
            if (simulator.PluggedIn)
                Console.WriteLine("Plugged in again.");
        }
    }

    // A key that fails says why, and the simulator goes on.
    private static void Try(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or TimeoutException
            or System.Net.Sockets.SocketException or DmxBoxException)
        {
            Console.WriteLine($"That did not work: {e.Message}");
        }
    }
}

/// <summary>A mistake on the command line; its message says which.</summary>
internal sealed class UsageException(string message) : Exception(message);

/// <summary>The options after the device: "--name value" pairs and flags.</summary>
internal sealed class Options
{
    private static readonly string[] Flags = ["--multicast"];

    private readonly Dictionary<string, string?> _given = new(StringComparer.Ordinal);
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);

    public static Options Parse(string[] args)
    {
        var options = new Options();
        for (int index = 0; index < args.Length; index++)
        {
            string name = args[index];
            if (!name.StartsWith("--", StringComparison.Ordinal))
                throw new UsageException($"\"{name}\" is no option; options begin with --.");
            if (Flags.Contains(name))
                options._given[name] = null;
            else if (index + 1 < args.Length)
                options._given[name] = args[++index];
            else
                throw new UsageException($"{name} needs a value.");
        }

        return options;
    }

    public string? Text(string name)
    {
        _used.Add(name);
        return _given.TryGetValue(name, out string? value) ? value : null;
    }

    public bool Flag(string name)
    {
        _used.Add(name);
        return _given.ContainsKey(name);
    }

    public int Number(string name, int defaultValue, int min, int max)
    {
        string? text = Text(name);
        if (text is null)
            return defaultValue;
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value >= min && value <= max
            ? value
            : throw new UsageException($"{name} is a number from {min} to {max}, not \"{text}\".");
    }

    public IPAddress? Address(string name)
    {
        string? text = Text(name);
        if (text is null)
            return null;
        return IPAddress.TryParse(text, out IPAddress? address) && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? address
            : throw new UsageException($"{name} is an IPv4 address of this PC, like 127.0.0.2, not \"{text}\".");
    }

    // "0:0:1" or 1.
    public int PortAddress(string name, int defaultValue)
    {
        string? text = Text(name);
        if (text is null)
            return defaultValue;

        string[] parts = text.Split(':');
        if (parts.Length == 3 && parts.All(part => int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            int net = int.Parse(parts[0], CultureInfo.InvariantCulture);
            int subNet = int.Parse(parts[1], CultureInfo.InvariantCulture);
            int universe = int.Parse(parts[2], CultureInfo.InvariantCulture);
            if (net <= 127 && subNet <= 15 && universe <= 15)
                return ArtNetSender.PortAddress(net, subNet, universe);
        }
        else if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value <= ArtNetSender.MaxUniverse)
        {
            return value;
        }

        throw new UsageException($"{name} is a Port-Address, Net:Sub-Net:Universe like 0:0:1, not \"{text}\".");
    }

    // "1-4" or "1,3,5".
    public int[] Universes(string name, string defaultValue)
    {
        string text = Text(name) ?? defaultValue;
        var universes = new List<int>();
        foreach (string part in text.Split(','))
        {
            string[] ends = part.Split('-');
            if (ends.Length is 1 or 2
                && int.TryParse(ends[0], NumberStyles.None, CultureInfo.InvariantCulture, out int from)
                && int.TryParse(ends[^1], NumberStyles.None, CultureInfo.InvariantCulture, out int to)
                && from >= SacnSender.MinUniverse && to <= SacnSender.MaxUniverse && from <= to && to - from < 512)
            {
                universes.AddRange(Enumerable.Range(from, to - from + 1));
                continue;
            }

            throw new UsageException($"{name} lists sACN universes from 1 to 63999, like 1-4 or 1,3,5, not \"{text}\".");
        }

        return [.. universes.Distinct()];
    }

    // An option that no device asked for is a mistake: "--port" for a dmxbox, a typo.
    public void RequireAllUsed()
    {
        string? unused = _given.Keys.FirstOrDefault(name => !_used.Contains(name));
        if (unused is not null)
            throw new UsageException($"{unused} is no option of this device.");
    }
}

/// <summary>The box simulators of firmware/sim, built from the firmware's own code.</summary>
internal static class BoxPrograms
{
    /// <summary>
    /// The path of a box simulator, built first when it is missing or older than its sources:
    /// with firmware\sim\build.cmd (Visual Studio's C compiler) on Windows, make elsewhere.
    /// </summary>
    /// <param name="program">"dmxbox_sim".</param>
    public static string Find(string program)
    {
        string folder = SimFolder();
        string path = Path.Combine(folder, OperatingSystem.IsWindows() ? $"{program}.exe" : program);
        if (File.Exists(path) && File.GetLastWriteTimeUtc(path) >= NewestSource(folder))
            return path;

        Console.WriteLine("Building the box simulators from the firmware's code first...");
        var build = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", ["/c", Path.Combine(folder, "build.cmd")])
            : new ProcessStartInfo("make", ["-C", folder]);
        build.UseShellExecute = false;
        using Process process = Process.Start(build) ?? throw new InvalidOperationException("The build did not start.");
        process.WaitForExit();
        if (process.ExitCode != 0 || !File.Exists(path))
        {
            throw new InvalidOperationException(OperatingSystem.IsWindows()
                ? "The box simulators were not built. firmware\\sim\\build.cmd needs Visual Studio with \"Desktop development with C++\"."
                : "The box simulators were not built: make -C firmware/sim needs a C compiler.");
        }

        return path;
    }

    // firmware/sim in the first folder above this program that has it: the repository.
    private static string SimFolder()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            string candidate = Path.Combine(folder.FullName, "firmware", "sim");
            if (File.Exists(Path.Combine(candidate, "sim.c")))
                return candidate;
        }

        throw new InvalidOperationException("firmware/sim was not found above this program: run it from the repository (sim.cmd).");
    }

    // The newest of the files a box simulator is built from.
    private static DateTime NewestSource(string simFolder)
    {
        string firmware = Path.GetDirectoryName(simFolder)!;
        IEnumerable<string> sources = Directory.GetFiles(Path.Combine(firmware, "common"))
            .Append(Path.Combine(simFolder, "sim.c"))
            .Append(Path.Combine(firmware, "pico", "src", "config.h"))
            .Append(Path.Combine(firmware, "pic32", "src", "config.h"))
            .Where(File.Exists);
        return sources.Max(File.GetLastWriteTimeUtc);
    }
}
