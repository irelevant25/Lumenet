using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DmxBox.Sim;

/// <summary>
/// A console on the DMX IN of a simulated box (sim.cmd dmxbox --console): 24 faders and 8 buttons
/// on a page of its own in the browser. While the box is plugged in, it sends the box a frame of all
/// 512 levels 40 times a second, as a console on the DMX IN socket does: channels 1 to 24 are the
/// faders, 25 to 32 the buttons (255 while one is held), the others stay at 0.
/// </summary>
internal sealed class DmxConsole : IDisposable
{
    /// <summary>The faders, on channels 1 to 24.</summary>
    public const int Faders = 24;

    /// <summary>The buttons, on the channels after the faders.</summary>
    public const int Buttons = 8;

    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(25);

    private readonly Lock _lock = new();
    private readonly byte[] _levels = new byte[512];
    private readonly Socket _socket;   // kept open: Windows may drop a datagram whose socket is closed at once
    private readonly IPEndPoint _box;
    private readonly WebApplication _page;
    private readonly Timer _timer;
    private readonly TextWriter _output;
    private volatile bool _sending;

    /// <summary>Starts the page and the frames; nothing goes out until <see cref="Sending"/>.</summary>
    /// <param name="consolePort">The UDP port of the box's DMX IN at 127.0.0.1.</param>
    /// <param name="output">Where the lines go.</param>
    public DmxConsole(int consolePort, TextWriter output)
    {
        _output = output;
        _box = new IPEndPoint(IPAddress.Loopback, consolePort);
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _page = StartPage();
        Url = _page.Urls.First().Replace("127.0.0.1", "localhost", StringComparison.Ordinal) + "/";
        _timer = new Timer(_ => SendFrame(), null, FrameInterval, FrameInterval);
    }

    /// <summary>The address of its page.</summary>
    public string Url { get; }

    /// <summary>True while the box is plugged in: the console sends it its frames.</summary>
    public bool Sending
    {
        get => _sending;
        set => _sending = value;
    }

    /// <summary>The level of a channel: a fader moved, or a button pressed (255) or let go (0).</summary>
    /// <param name="channel">1 to 512.</param>
    /// <param name="level">0 to 255.</param>
    public void Set(int channel, byte level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(channel, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(channel, _levels.Length);
        lock (_lock)
            _levels[channel - 1] = level;
    }

    /// <summary>Opens the page in the browser of this PC.</summary>
    public void OpenPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            _output.WriteLine($"The browser did not open ({e.Message}); the console's page is at {Url}");
        }
    }

    /// <summary>Stops the frames and the page.</summary>
    public void Dispose()
    {
        _sending = false;
        _timer.Dispose();
        _page.StopAsync().GetAwaiter().GetResult();
        ((IDisposable)_page).Dispose();
        _socket.Dispose();
    }

    private void SendFrame()
    {
        if (!_sending)
            return;

        byte[] frame;
        lock (_lock)
            frame = (byte[])_levels.Clone();
        try
        {
            _socket.SendTo(frame, _box);
        }
        catch (Exception e) when (e is SocketException or ObjectDisposedException)
        {
            // The box is being pulled out, or the console closed: the next frame tries again.
        }
    }

    private WebApplication StartPage()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        WebApplication page = builder.Build();

        string html = PageText();
        page.MapGet("/", () => Results.Content(html, "text/html; charset=utf-8"));
        page.MapPost("/level", (LevelInput input) =>
        {
            if (input.Channel is not (>= 1 and <= Faders + Buttons) || input.Level is not (>= 0 and <= 255))
                return Results.BadRequest();
            Set(input.Channel, (byte)input.Level);
            return Results.NoContent();
        });

        page.StartAsync().GetAwaiter().GetResult();
        return page;
    }

    // The page, built into this program.
    private static string PageText()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("dmx-console.html")
            ?? throw new InvalidOperationException("The page of the console is not built into the simulator.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("{{FADERS}}", Faders.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{{BUTTONS}}", Buttons.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    // What the page sends: a channel and its level.
    private sealed record LevelInput(int Channel, int Level);
}
