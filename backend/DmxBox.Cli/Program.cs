using System.Globalization;
using System.Net.Sockets;

namespace DmxBox.Cli;

/// <summary>The dmxbox command line tool. "dmxbox --help" lists the commands.</summary>
internal static class Program
{
    // The address the box always has on its USB cable (NET_IP in the firmware's config.h).
    private const string DefaultHost = "192.168.7.1";

    private static readonly TimeSpan ArtNetLinger = TimeSpan.FromMilliseconds(200);

    private static int Main(string[] args)
    {
        if (args.Contains("-h") || args.Contains("--help"))
        {
            Console.WriteLine(Usage.Text);
            return 0;
        }

        try
        {
            Run(new CommandLine(args));
            return 0;
        }
        catch (UsageException e)
        {
            Console.Error.WriteLine(Usage.Text);
            Console.Error.WriteLine();
            Console.Error.WriteLine($"error: {e.Message}");
            return 2;
        }
        catch (DmxBoxException e)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
        catch (SocketException e)
        {
            // For example an unknown host name, or no network that leads to the address.
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }

    // Every command reads all of its arguments before it uses the network,
    // so that a typo never leaves a command half done.
    private static void Run(CommandLine commandLine)
    {
        string host = commandLine.Option("--host") ?? HostFromEnvironment() ?? DefaultHost;
        int port = commandLine.IntOption("--port", DmxBoxClient.DefaultPort, min: 1, max: 65535);
        int artNetPort = commandLine.IntOption("--artnet-port", ArtNetSender.DefaultPort, min: 1, max: 65535);
        string command = commandLine.NextWord("command");

        switch (command)
        {
            case "discover":
                Discover(commandLine, artNetPort);
                break;
            case "artnet":
                SendArtNetFrame(commandLine, host, artNetPort);
                break;
            case "demo":
                RunDemo(commandLine, host, port, artNetPort);
                break;
            default:
                RunBoxCommand(command, commandLine, host, port);
                break;
        }
    }

    private static string? HostFromEnvironment()
    {
        string? host = Environment.GetEnvironmentVariable("DMXBOX_HOST");
        return string.IsNullOrEmpty(host) ? null : host;
    }

    private static void Discover(CommandLine commandLine, int artNetPort)
    {
        string target = commandLine.Option("--target") ?? ArtNetDiscovery.BroadcastAddress;
        commandLine.ExpectEnd();

        IReadOnlyList<ArtNetNode> nodes = ArtNetDiscovery.Discover(target: target, port: artNetPort);
        foreach (ArtNetNode node in nodes)
        {
            Console.WriteLine(
                $"{node.Ip,-15}  {node.ShortName,-18} universe {node.Universe}  fw {node.Firmware}  {node.Mac}");
        }

        if (nodes.Count == 0)
            Console.WriteLine("no Art-Net nodes answered");
    }

    private static void SendArtNetFrame(CommandLine commandLine, string host, int artNetPort)
    {
        int start = commandLine.NextInt("start", min: 1, max: DmxBoxClient.ChannelCount);
        byte[] values = commandLine.RemainingLevels();
        int universe = commandLine.IntOption("--universe", defaultValue: 0, min: 0, max: ArtNetSender.MaxUniverse);
        commandLine.ExpectEnd();

        // An Art-Net frame always begins at channel 1: the channels before "start" are sent as 0.
        byte[] frame = new byte[start - 1 + values.Length];
        values.CopyTo(frame, start - 1);

        using var artNet = new ArtNetSender(host, universe, artNetPort);
        artNet.Send(frame);

        // Do not close the socket at once. On Windows the first datagram of a new socket can be
        // held back for some tens of milliseconds (network inspection by security software),
        // and it is dropped if the socket is gone before the datagram has left.
        Thread.Sleep(ArtNetLinger);
    }

    private static void RunDemo(CommandLine commandLine, string host, int port, int artNetPort)
    {
        string demo = commandLine.NextChoice("demo", "cues", "stream");
        commandLine.ExpectEnd();

        using var box = new DmxBoxClient(host, port);
        if (demo == "cues")
        {
            Demos.Cues(box);
        }
        else
        {
            using var artNet = new ArtNetSender(host, universe: 0, artNetPort);
            Demos.Stream(artNet, box);
        }
    }

    private static void RunBoxCommand(string command, CommandLine commandLine, string host, int port)
    {
        Action<DmxBoxClient> run = ParseBoxCommand(command, commandLine);
        commandLine.ExpectEnd();

        using var box = new DmxBoxClient(host, port);
        run(box);
        if (box.Retransmits > 0)
            Console.WriteLine($"({box.Retransmits} retransmits)");
    }

    // Reads the arguments of a command for the box and returns what to do once the box is there.
    private static Action<DmxBoxClient> ParseBoxCommand(string command, CommandLine commandLine)
    {
        switch (command)
        {
            case "ping":
                return PrintPing;

            case "set":
            {
                int start = NextChannelNumber(commandLine, "start");
                byte[] values = commandLine.RemainingLevels();
                return box => box.Set(start, values);
            }

            case "fade":
            {
                int start = NextChannelNumber(commandLine, "start");
                byte[] values = commandLine.RemainingLevels();
                TimeSpan time = commandLine.SecondsOption("--time", defaultSeconds: 1);
                return box => box.Fade(start, values, time);
            }

            case "preset":
                return ParsePresetCommand(commandLine);

            case "master":
            {
                byte level = (byte)commandLine.NextInt("level", min: 0, max: 255);
                return box => box.Master(level);
            }

            case "blackout":
            {
                bool on = commandLine.NextChoice("state", "on", "off") == "on";
                return box => box.Blackout(on);
            }

            case "release-artnet":
                return box => box.ReleaseArtNet();

            case "merge":
            {
                bool htp = commandLine.NextChoice("mode", "htp", "override") == "htp";
                return box => box.SetMergeMode(htp ? MergeMode.Htp : MergeMode.Override);
            }

            case "get":
            {
                int start = commandLine.HasWords ? NextChannelNumber(commandLine, "start") : 1;
                int count = commandLine.HasWords ? NextChannelNumber(commandLine, "count") : 16;
                return box => Console.WriteLine(FormatChannels(start, box.GetOutput(start, count)));
            }

            default:
                throw new UsageException($"unknown command '{command}'");
        }
    }

    private static Action<DmxBoxClient> ParsePresetCommand(CommandLine commandLine)
    {
        string action = commandLine.NextChoice("action", "save", "go", "clear", "show");

        // Like a channel number, the slot only has to fit its field (one byte) here.
        int slot = commandLine.NextInt("slot", min: 0, max: byte.MaxValue);

        switch (action)
        {
            case "save":
                return box => box.PresetSave(slot);

            case "go":
            {
                TimeSpan time = commandLine.SecondsOption("--time", defaultSeconds: 0);
                return box => box.PresetGo(slot, time);
            }

            case "clear":
                return box => box.PresetClear(slot);

            default:
                return box => PrintPreset(box, slot);
        }
    }

    // A channel number or a channel count only has to fit its 16-bit field here.
    // Whether such channels exist is for the box to say: it answers BAD_ARG if not.
    private static int NextChannelNumber(CommandLine commandLine, string what)
    {
        return commandLine.NextInt(what, min: 0, max: ushort.MaxValue);
    }

    private static void PrintPing(DmxBoxClient box)
    {
        PingInfo info = box.Ping();
        PrintField("firmware", info.Firmware);
        PrintField("artnet_live", info.ArtNetLive);
        PrintField("blackout", info.Blackout);
        PrintField("merge", info.MergeMode == MergeMode.Override ? "override" : "htp");
        PrintField("artnet_holding", info.ArtNetHolding);
        PrintField("master", info.Master);
        PrintField("uptime_s", info.Uptime.TotalSeconds.ToString("0.0##", CultureInfo.InvariantCulture));
        PrintField("presets", $"[{string.Join(", ", info.Presets)}]");
        PrintField("channels", info.Channels);
    }

    private static void PrintField(string name, object value)
    {
        Console.WriteLine($"{name,-15} {value}");
    }

    private static void PrintPreset(DmxBoxClient box, int slot)
    {
        byte[] values = box.GetPreset(slot);

        // Stop after the last channel that is not 0: a preset always has all 512 channels.
        int used = Array.FindLastIndex(values, value => value != 0) + 1;
        Console.WriteLine(used == 0 ? "all zero" : FormatChannels(1, values[..used]));
    }

    // Channel numbers with their levels: "1:255 2:0 3:128".
    private static string FormatChannels(int firstChannel, byte[] values)
    {
        return string.Join(' ', values.Select((value, index) => $"{firstChannel + index}:{value}"));
    }
}
