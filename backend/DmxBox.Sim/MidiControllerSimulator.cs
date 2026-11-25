using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DmxBox.Sim;

/// <summary>
/// A simulated MIDI controller (<see cref="MidiControllers"/>): a MIDI device "APC mini mk2
/// (simulator)" that the desk lists while it is plugged in, played through a named pipe
/// (<see cref="SimulatedDevice.MidiPipeName"/>), as no MIDI port can be made on Windows without a
/// driver. Its controls are on a page of its own in the browser: pads and keys to press, faders
/// and knobs to move. The desk's messages light its keys and move its motorized faders and the
/// rings of its knobs there, as the real controller reads them.
/// </summary>
internal sealed class MidiControllerSimulator : ISimulator
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ControllerModel _model;
    private readonly SimulatorClaim _claim;
    private readonly string _pipeName;
    private readonly TextWriter _output;
    private readonly WebApplication _page;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, SimControl> _controls;
    private readonly Dictionary<string, ControlState> _states;
    private readonly List<Channel<string>> _watchers = [];
    private readonly List<string> _news = [];
    private CancellationTokenSource? _stop;
    private Task? _serving;
    private NamedPipeServerStream? _desk;
    private long _fromDesk;
    private string _lastFromDesk = "";

    /// <summary>Claims a MIDI device for the controller and starts its page. Nothing reaches the desk until it is plugged in.</summary>
    /// <param name="model">The controller.</param>
    /// <param name="folder">The registry of the simulators.</param>
    /// <param name="port">The port of its page on this PC; 0 for any free one.</param>
    /// <param name="output">Where the lines go.</param>
    public MidiControllerSimulator(ControllerModel model, string folder, int port, TextWriter output)
    {
        _model = model;
        _output = output;
        _controls = model.Controls.ToDictionary(control => control.Id);
        _states = model.Controls.ToDictionary(control => control.Id, _ => new ControlState());
        (Name, _claim) = ClaimDevice(folder, model);
        _pipeName = SimulatedDevice.MidiPipeName(_claim.Device.Address);
        _page = StartPage(port);
        Url = _page.Urls.First().Replace("127.0.0.1", "localhost", StringComparison.Ordinal) + "/";
    }

    /// <summary>The name the desk lists it under: "APC mini mk2 (simulator)".</summary>
    public string Name { get; }

    /// <summary>The address of its page: "http://localhost:52110/".</summary>
    public string Url { get; }

    /// <summary>True while the desk has it open.</summary>
    public bool DeskConnected
    {
        get
        {
            lock (_lock)
            {
                return _desk is not null;
            }
        }
    }

    /// <inheritdoc />
    public string Title => $"{_model.Title}, simulated as \"{Name}\"";

    /// <inheritdoc />
    public string Finding =>
        $"In the desk: the MIDI view lists \"{Name}\"; tick it to use it, and Ready-made mapping gives its controls their keys and faders.\n"
        + $"Its controls: {Url}";

    /// <inheritdoc />
    public IReadOnlyList<(char Key, string What)> Keys { get; } = [('O', "opens its page")];

    /// <inheritdoc />
    public bool PluggedIn => _stop is not null;

    /// <inheritdoc />
    public void PlugIn()
    {
        if (PluggedIn)
            return;

        _stop = new CancellationTokenSource();
        CancellationToken stop = _stop.Token;
        _serving = Task.Run(() => ServeAsync(stop));
        _claim.PluggedIn = true;
        Publish(Status());
    }

    /// <inheritdoc />
    public void PullOut()
    {
        _claim.PluggedIn = false;
        if (_stop is null)
            return;

        _stop.Cancel();
        try
        {
            _serving?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // It ended with the pipe.
        }

        _stop.Dispose();
        _stop = null;
        _serving = null;
        lock (_lock)
        {
            foreach (ControlState state in _states.Values)
                state.Light = null;   // no power, no lights
        }

        Publish(Everything());
    }

    /// <inheritdoc />
    public void Press(char key)
    {
        if (key == 'O')
            OpenPage();
    }

    /// <inheritdoc />
    public void Tick()
    {
        string[] lines;
        lock (_lock)
        {
            lines = [.. _news];
            _news.Clear();
        }

        foreach (string line in lines)
            _output.WriteLine(line);
    }

    /// <summary>Pulls the controller out for good, ends its page and gives its device back.</summary>
    public void Dispose()
    {
        PullOut();
        lock (_lock)
        {
            foreach (Channel<string> watcher in _watchers)
                watcher.Writer.TryComplete();
        }

        _page.StopAsync().GetAwaiter().GetResult();
        ((IDisposable)_page).Dispose();
        _claim.Dispose();
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
            Say($"The browser did not open ({e.Message}); the page is at {Url}");
        }
    }

    /// <summary>A key or pad pressed (true) or let go on the page; its message goes to the desk.</summary>
    /// <param name="id">The control.</param>
    /// <param name="down">Pressed or let go.</param>
    public void PressControl(string id, bool down)
    {
        SimControl control = Control(id);
        byte status = (byte)((control.Note ? (down ? 0x90 : 0x80) : 0xB0) | (control.Channel - 1));
        lock (_lock)
        {
            _states[id].Down = down;
        }

        Send([status, (byte)control.Number, (byte)(down ? 127 : 0)]);
        Publish(StateOf(id));
    }

    /// <summary>A fader or knob moved on the page; its message goes to the desk.</summary>
    /// <param name="id">The control.</param>
    /// <param name="value">0-127.</param>
    public void MoveControl(string id, int value)
    {
        SimControl control = Control(id);
        value = Math.Clamp(value, 0, 127);
        lock (_lock)
        {
            _states[id].Value = value;
        }

        Send([(byte)(0xB0 | (control.Channel - 1)), (byte)control.Number, (byte)value]);
        Publish(StateOf(id));
    }

    /// <summary>How a control looks now: its light (a color, how bright, blinking or pulsing) and where it stands.</summary>
    /// <param name="id">The control.</param>
    public (string? Color, double Level, string Motion, int Value) Look(string id)
    {
        lock (_lock)
        {
            ControlState state = _states[id];
            return (state.Light?.Color, state.Light?.Level ?? 0, state.Light?.Motion ?? "", state.Value);
        }
    }

    // A free address MIDI1, MIDI2 ..., and a name no other simulator has: "APC mini mk2 (simulator)", then "APC mini mk2 2 (simulator)".
    private static (string Name, SimulatorClaim Claim) ClaimDevice(string folder, ControllerModel model)
    {
        var taken = SimulatorRegistry.Read(folder, pulledOutToo: true).Select(device => device.Name).ToHashSet(StringComparer.Ordinal);
        string name = Enumerable.Range(1, 99)
            .Select(count => count == 1 ? $"{model.Name} (simulator)" : string.Create(CultureInfo.InvariantCulture, $"{model.Name} {count} (simulator)"))
            .First(candidate => !taken.Contains(candidate));

        for (int number = 1; number < 100; number++)
        {
            string address = string.Create(CultureInfo.InvariantCulture, $"MIDI{number}");
            try
            {
                return (name, SimulatorRegistry.Claim(folder, new SimulatedDevice(SimulatedDevice.Midi, address, name, Environment.ProcessId)));
            }
            catch (AddressTakenException)
            {
                // Another simulator's: the next one.
            }
        }

        throw new InvalidOperationException("Every address from MIDI1 to MIDI99 has a simulator.");
    }

    private SimControl Control(string id)
    {
        return _controls.TryGetValue(id, out SimControl? control) ? control : throw new ArgumentException($"The controller has no control \"{id}\".", nameof(id));
    }

    // ---------------------------------------------------------------- the desk, through the pipe

    // Takes the desk while it is plugged in: one connection at a time, as a MIDI port.
    private async Task ServeAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            try
            {
                await pipe.WaitForConnectionAsync(stop);
                lock (_lock)
                {
                    _desk = pipe;
                }

                Say("The desk uses it now.");
                Publish(Status());
                await ListenAsync(pipe, stop);
                Say("The desk let it go.");
            }
            catch (OperationCanceledException)
            {
                return;   // pulled out: the pipe closes, and the desk finds the device gone
            }
            catch (IOException)
            {
                Say("The desk let it go.");
            }
            finally
            {
                lock (_lock)
                {
                    _desk = null;
                }

                Publish(Status());
            }
        }
    }

    private async Task ListenAsync(NamedPipeServerStream pipe, CancellationToken stop)
    {
        var reader = new MidiBytes();
        byte[] buffer = new byte[256];
        int count;
        while ((count = await pipe.ReadAsync(buffer, stop)) > 0)
        {
            foreach ((int status, int data1, int data2) in reader.Take(buffer.AsSpan(0, count)))
                FromDesk(status, data1, data2);
        }
    }

    private void Send(byte[] message)
    {
        NamedPipeServerStream? desk;
        lock (_lock)
        {
            desk = _desk;
        }

        if (desk is null || !PluggedIn)
            return;

        try
        {
            lock (desk)
            {
                desk.Write(message);
                desk.Flush();
            }
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The desk let it go meanwhile.
        }
    }

    // A message from the desk: a light, a motorized fader, the ring of a knob.
    private void FromDesk(int status, int data1, int data2)
    {
        int kind = status & 0xF0;
        int channel = (status & 0x0F) + 1;
        bool note = kind is 0x90 or 0x80;
        if (!note && kind != 0xB0)
            return;

        int value = kind == 0x80 ? 0 : data2;
        var changed = new List<string>();
        lock (_lock)
        {
            _fromDesk++;
            _lastFromDesk = string.Create(CultureInfo.InvariantCulture, $"{(note ? "Note" : "CC")} {data1} = {value}, channel {channel}");
            foreach (SimControl control in _controls.Values)
            {
                if (control.Light is { } light && light.Note == note && light.Number == data1 && (light.Channel == 0 || light.Channel == channel))
                {
                    _states[control.Id].Light = Lit(light, channel, value);
                    changed.Add(control.Id);
                }
                else if (control.Driven && !note && control.Channel == channel && control.Number == data1)
                {
                    _states[control.Id].Value = value;
                    changed.Add(control.Id);
                }
            }
        }

        foreach (string id in changed)
            Publish(StateOf(id));
        Publish(Status());
    }

    // How a light reads a value.
    private static LightState? Lit(SimLight light, int channel, int value)
    {
        switch (light.Reading)
        {
            case LightReading.ApcPad:
                if (value == 0)
                    return null;
                double[] brightness = [0.1, 0.25, 0.5, 0.65, 0.75, 0.9, 1];
                string motion = channel switch
                {
                    >= 12 => "blink",
                    >= 8 => "pulse",
                    _ => "",
                };
                return new LightState(MidiControllers.ApcPalette[value & 0x7F], channel <= 7 ? brightness[channel - 1] : 1, motion);
            case LightReading.OffOnBlink:
                return value switch
                {
                    0 => null,
                    2 => new LightState(light.Color, 1, "blink"),
                    _ => new LightState(light.Color, 1, ""),
                };
            case LightReading.OffBlinkOn:
                return value switch
                {
                    0 => null,
                    1 => new LightState(light.Color, 1, "blink"),
                    _ => new LightState(light.Color, 1, ""),
                };
            default:
                return value > 0 ? new LightState(light.Color, 1, "") : null;
        }
    }

    // ---------------------------------------------------------------- the page

    private WebApplication StartPage(int port)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}"));
        WebApplication page = builder.Build();

        string html = PageText();
        page.MapGet("/", () => Results.Content(html, "text/html; charset=utf-8"));
        page.MapGet("/layout", () => Results.Text(JsonSerializer.Serialize(Layout(), Json), "application/json"));
        page.MapGet("/events", WatchAsync);
        page.MapPost("/control", (ControlInput input) =>
        {
            if (input.Id is null || !_controls.ContainsKey(input.Id))
                return Results.NotFound();
            if (input.Value is { } value)
                MoveControl(input.Id, value);
            else
                PressControl(input.Id, input.Down ?? false);
            return Results.NoContent();
        });

        page.StartAsync().GetAwaiter().GetResult();
        return page;
    }

    // The page, built into this program.
    private static string PageText()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("midi-controller.html")
            ?? throw new InvalidOperationException("The page of the MIDI controllers is not built into the simulator.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private object Layout()
    {
        return new
        {
            _model.Title,
            Name,
            Sections = _model.Sections.Select(section => new
            {
                section.Title,
                section.Row,
                section.Columns,
                Controls = section.Controls.Select(control => new
                {
                    control.Id,
                    Kind = control.Kind.ToString().ToLowerInvariant(),
                    control.Label,
                    control.Driven,
                    Lit = control.Light is not null,
                    Message = string.Create(
                        CultureInfo.InvariantCulture, $"{(control.Note ? "Note" : "CC")} {control.Number}, channel {control.Channel}"),
                }),
            }),
        };
    }

    // The events of the page: everything at once, then what changes.
    private async Task WatchAsync(HttpContext context)
    {
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        var watcher = Channel.CreateUnbounded<string>();
        lock (_lock)
        {
            _watchers.Add(watcher);
        }

        foreach (string data in Everything())
            watcher.Writer.TryWrite(data);
        try
        {
            await context.Response.Body.FlushAsync(context.RequestAborted);
            await foreach (string data in watcher.Reader.ReadAllAsync(context.RequestAborted))
            {
                await context.Response.WriteAsync($"data: {data}\n\n", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or IOException)
        {
            // The page closed.
        }
        finally
        {
            lock (_lock)
            {
                _watchers.Remove(watcher);
            }
        }
    }

    private string[] Everything()
    {
        return [Status(), .. _controls.Keys.Select(StateOf)];
    }

    private string Status()
    {
        lock (_lock)
        {
            return JsonSerializer.Serialize(new { Type = "status", PluggedIn = _claim.PluggedIn, Desk = _desk is not null, FromDesk = _fromDesk, Last = _lastFromDesk }, Json);
        }
    }

    private string StateOf(string id)
    {
        lock (_lock)
        {
            ControlState state = _states[id];
            return JsonSerializer.Serialize(
                new { Type = "control", Id = id, state.Value, state.Down, state.Light?.Color, Level = state.Light?.Level ?? 0, Motion = state.Light?.Motion ?? "" },
                Json);
        }
    }

    private void Publish(string data)
    {
        lock (_lock)
        {
            foreach (Channel<string> watcher in _watchers)
                watcher.Writer.TryWrite(data);
        }
    }

    private void Publish(IEnumerable<string> data)
    {
        foreach (string item in data)
            Publish(item);
    }

    private void Say(string line)
    {
        lock (_lock)
        {
            _news.Add(line);
        }
    }

    private sealed class ControlState
    {
        public int Value { get; set; }

        public bool Down { get; set; }

        public LightState? Light { get; set; }
    }

    // A light that is on: its color, how bright (0-1), and "blink", "pulse" or "" for steady.
    private sealed record LightState(string Color, double Level, string Motion);

    // What the page sends: a press ("down") or a move ("value").
    private sealed record ControlInput(string? Id, bool? Down, int? Value);
}

/// <summary>MIDI bytes as messages: running status kept, system messages passed over.</summary>
internal sealed class MidiBytes
{
    private int _status;
    private int _first = -1;
    private bool _inSystem;

    /// <summary>The messages the bytes complete: status, first data byte, second (0 for a message of one).</summary>
    /// <param name="bytes">Bytes as they came.</param>
    public List<(int Status, int Data1, int Data2)> Take(ReadOnlySpan<byte> bytes)
    {
        var messages = new List<(int, int, int)>();
        foreach (byte value in bytes)
        {
            if (value >= 0xF8)
                continue;   // real time: clock, start, stop

            if (value >= 0x80)
            {
                _inSystem = value >= 0xF0;
                _status = _inSystem ? 0 : value;
                _first = -1;
                continue;
            }

            if (_inSystem || _status == 0)
                continue;

            bool one = (_status & 0xF0) is 0xC0 or 0xD0;
            if (one)
            {
                messages.Add((_status, value, 0));
            }
            else if (_first < 0)
            {
                _first = value;
            }
            else
            {
                messages.Add((_status, _first, value));
                _first = -1;
            }
        }

        return messages;
    }
}
