using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using DmxBox.Server;

namespace Lumenet;

/// <summary>
/// Lumenet.exe: the desk program in the notification area of the taskbar, with the page and the
/// examples inside it (see Lumenet.csproj and Tray). It takes the settings of the desk program, on
/// the command line ("Lumenet.exe --Box:Host=127.0.0.1") or in an appsettings.json beside the exe,
/// and one of its own: "--Tray:OpenPage=false" starts it without opening the page. It updates
/// itself from the About tab of Settings (SelfReplace).
/// </summary>
internal static class TrayProgram
{
    // Started a second time, Lumenet asks the first one to open the page, and ends.
    private const string OpenPageSignal = @"Local\Lumenet.OpenPage";

    // How long the desk program may take to stop after Exit, as after Ctrl+C.
    private static readonly TimeSpan StopTime = TimeSpan.FromSeconds(10);

    [STAThread]
    private static int Main(string[] args)
    {
        // Started by an update: the Lumenet before ends first, and leaves its port, icon and signal.
        SelfReplace.WaitForTheOneBefore(args);

        using var openPage = new EventWaitHandle(false, EventResetMode.AutoReset, OpenPageSignal, out bool first);
        if (!first)
        {
            openPage.Set();
            return 0;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var log = new LogBook();
        Application.ThreadException += (_, e) =>
            log.Add(LogLevel.Error, BuiltIn.LogName, "Something in the tray or the log window failed.", e.Exception);

        var self = new SelfReplace(args);
        WebApplication desk;
        try
        {
            desk = DmxBox.Server.Program.Build(args, adjust: builder => BuiltIn.Adjust(builder, log, self));
        }
        catch (Exception e)
        {
            return Refuse($"The settings could not be read. {e.Message}");
        }

        // The desk of run.cmd (or another program) may have the port already. Found out before
        // the desk starts: started, it would talk to the box at the same time as the other one.
        string? taken = TakenAddress(desk.Configuration);
        if (taken is not null)
        {
            return Refuse(
                $"Another program uses the port of {taken}: Lumenet itself, or the desk of run.cmd. "
                + "Only one desk can run on this PC at a time.");
        }

        try
        {
            // On a thread of the pool: nothing of the start may wait for this thread, the one of the windows.
            Task.Run(() => desk.StartAsync()).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Stop(desk);
            return Refuse(e.Message);
        }

        string page = PageAddress(desk);
        log.Add(LogLevel.Information, BuiltIn.LogName, $"Lumenet {AppInfo.Version} runs; the page is at {page}");
        self.CleanUp();

        using (var tray = new Tray(page, AppInfo.Version, log))
        {
            self.UseExit(tray.ExitSoon);
            RegisteredWaitHandle waiting = ThreadPool.RegisterWaitForSingleObject(
                openPage, (_, _) => tray.OpenPageSoon(), null, Timeout.Infinite, executeOnlyOnce: false);

            if (desk.Configuration.GetValue("Tray:OpenPage", true))
                tray.OpenPage();

            Application.Run(tray);
            waiting.Unregister(null);
        }

        Stop(desk);
        return 0;
    }

    private static int Refuse(string reason)
    {
        MessageBox.Show($"Lumenet could not start.\n\n{reason}", "Lumenet", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return 1;
    }

    // The desk program stops as it does after Ctrl+C, and finishes what it is writing.
    private static void Stop(WebApplication desk)
    {
        Task.Run(async () =>
        {
            using var timeout = new CancellationTokenSource(StopTime);
            try
            {
                await desk.StopAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                // took too long: it ends with the program
            }

            await desk.DisposeAsync();
        }).GetAwaiter().GetResult();
    }

    // The first address of the settings whose port another program has, or null. "http://*:5080"
    // is no address that Uri reads, so the port is taken from its end.
    private static string? TakenAddress(IConfiguration settings)
    {
        string urls = settings[WebHostDefaults.ServerUrlsKey] ?? "";
        foreach (string url in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Match port = Regex.Match(url, @":(\d+)/?$");
            if (port.Success && PortIsTaken(int.Parse(port.Groups[1].ValueSpan)))
                return url;
        }

        return null;
    }

    private static bool PortIsTaken(int port)
    {
        var probe = new TcpListener(IPAddress.IPv6Any, port);
        try
        {
            probe.Server.DualMode = true;
            probe.Start();
            return false;
        }
        catch (SocketException e) when (e.SocketErrorCode is SocketError.AddressAlreadyInUse or SocketError.AccessDenied)
        {
            return true;
        }
        catch (SocketException)
        {
            // No IPv6 here, for example: the desk finds out by itself, and says so.
            return false;
        }
        finally
        {
            probe.Stop();
        }
    }

    // The address of the page on this PC: "http://localhost:5080/" for the usual "http://*:5080".
    private static string PageAddress(WebApplication desk)
    {
        string? url = desk.Urls.FirstOrDefault(address => address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            ?? desk.Urls.FirstOrDefault();
        if (url is null)
            return "http://localhost:5080/";

        var address = new UriBuilder(url);
        if (address.Host is "[::]" or "0.0.0.0" or "*" or "+")
            address.Host = "localhost";

        return address.Uri.GetLeftPart(UriPartial.Authority) + "/";
    }
}
