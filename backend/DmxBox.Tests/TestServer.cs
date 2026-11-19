using DmxBox.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DmxBox.Tests;

/// <summary>
/// The whole server, running inside the test: the real program, on a free port of this PC,
/// with a pretend box and a projects folder of its own. Dispose stops it and deletes the folder.
/// </summary>
internal sealed class TestServer : IAsyncDisposable
{
    private const string FaultyHubPath = "/hub/faulty";

    private readonly WebApplication _app;
    private readonly DirectoryInfo _folder;

    private TestServer(WebApplication app, DirectoryInfo folder, FakeBox box, RecordingLog log)
    {
        _app = app;
        _folder = folder;
        Box = box;
        Log = log;
        Address = new Uri(app.Urls.Single());
    }

    /// <summary>
    /// The version the server has to report: the one its assembly carries, which comes from
    /// &lt;Version&gt; in its project file. The tests do not repeat the number, so that a new
    /// version stays a single edit.
    /// </summary>
    public static string Version { get; } = typeof(Desk).Assembly.GetName().Version!.ToString(3);

    /// <summary>The server's pretend box. It starts unplugged, so that no test is surprised by a "BoxChanged".</summary>
    public FakeBox Box { get; }

    /// <summary>Everything the server wrote into its log, which is what its console shows.</summary>
    public RecordingLog Log { get; }

    /// <summary>Where the server listens, for example http://127.0.0.1:50123/.</summary>
    public Uri Address { get; }

    /// <summary>The server's projects folder.</summary>
    public string ProjectsFolder => _folder.FullName;

    /// <summary>The address of the desk's hub for a WebSocket.</summary>
    public Uri HubAddress => new($"ws://{Address.Authority}/hub/desk");

    /// <summary>Starts a server.</summary>
    /// <param name="moreSettings">Settings as on the command line, for example "--AllowedHosts=desk.example".</param>
    public static async Task<TestServer> StartAsync(params string[] moreSettings)
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory("lumenet-test-");
        var box = new FakeBox();
        box.Unplug();

        // Port 0: the system picks a free one.
        string[] settings =
        [
            "--urls=http://127.0.0.1:0",
            $"--Projects:Folder={folder.FullName}",
            "--Box:Host=pretend-box",
            .. moreSettings,
        ];
        WebApplication app = Program.Build(settings, box);

        // Two additions the real server does not have: a log the tests can read, next to the
        // console, and a second hub whose one method always fails.
        var log = new RecordingLog();
        app.Services.GetRequiredService<ILoggerFactory>().AddProvider(log);
        app.MapHub<FaultyHub>(FaultyHubPath);

        await app.StartAsync();
        return new TestServer(app, folder, box, log);
    }

    /// <summary>Connects a UI to the desk's hub.</summary>
    public async Task<HubClient> ConnectAsync()
    {
        HubClient ui = await HubClient.ConnectAsync(HubAddress);

        // The server lists a new UI as connected a moment after it has greeted it. Once it
        // answers a call of the UI, it has done so, and no message can be missed any more.
        await ui.InvokeAsync("Hello");
        return ui;
    }

    /// <summary>Connects to the hub whose method "Break" always fails, see <see cref="FaultyHub"/>.</summary>
    public Task<HubClient> ConnectToFaultyHubAsync()
    {
        return HubClient.ConnectAsync(new Uri($"ws://{Address.Authority}{FaultyHubPath}"));
    }

    /// <summary>Makes a client for plain HTTP requests to the server.</summary>
    public HttpClient CreateHttpClient()
    {
        return new HttpClient { BaseAddress = Address };
    }

    /// <summary>Stops the server and deletes its projects folder.</summary>
    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        try
        {
            _folder.Delete(recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A leftover folder in the temp directory is harmless. It must not fail a test.
        }
    }
}
