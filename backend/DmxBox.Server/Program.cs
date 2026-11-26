using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace DmxBox.Server;

/// <summary>
/// The Lumenet server: the fader desk behind a SignalR hub for the UI in the browser, and the
/// link from the desk to the dmxbox. Settings come from appsettings.json next to the program,
/// from the environment and from the command line, for example "--Box:Host=127.0.0.1".
/// </summary>
internal static class Program
{
    // The server listens on every network card: a phone in the same network reaches it while
    // LAN is switched on (see LanAccess), and while LAN is off its connections are refused.
    private const string DefaultUrl = "http://*:5080";

    // The names by which the server lets itself be called, besides the addresses and the
    // computer name of this PC, which it always answers to (see AllowedHosts).
    private const string DefaultAllowedHosts = "localhost;127.0.0.1";
    private const string AllowedHostsKey = "AllowedHosts";

    // The address the box always has on its USB cable (NET_IP in the firmware's config.h).
    private const string DefaultBoxHost = "192.168.7.1";

    // Everything SignalR serves is below this path; the desk's hub is the only hub so far.
    private const string HubsPath = "/hub";
    private const string DeskHubPath = HubsPath + "/desk";

    /// <summary>
    /// The largest message a UI may send to the hub: room for a show file of 8 MB, the largest
    /// the UI sends (its IMPORT_LIMIT_BYTES), in JSON.
    /// </summary>
    public const long MaxMessageBytes = 10 * 1024 * 1024;

    // The name under which SignalR logs the hub calls that failed.
    private const string HubCallLog = "Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcher";

    // The pages that may use the hub besides the one this server hands out itself: the UI on
    // the development server ("ng serve", port 4200), and the server's own UI under both names
    // of this PC.
    private static readonly string[] DefaultAllowedOrigins =
    [
        "http://localhost:5080",
        "http://127.0.0.1:5080",
        "http://localhost:4200",
        "http://127.0.0.1:4200",
    ];

    private static void Main(string[] args)
    {
        Build(args).Run();
    }

    /// <summary>Puts the server together without starting it.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="box">
    /// The box to use. The tests pass a pretend one; null stands for the real box of the settings.
    /// </param>
    /// <param name="pc">
    /// This PC on the network. The tests pass a pretend one; null stands for the real one.
    /// </param>
    /// <param name="adjust">
    /// Changes before anything is read from the settings. Lumenet.exe gives the server its log
    /// window, the page and the examples it carries this way (see backend/Lumenet); null for none.
    /// </param>
    public static WebApplication Build(
        string[] args, IBox? box = null, ThisPc? pc = null, Action<WebApplicationBuilder>? adjust = null)
    {
        // The program's own folder counts, not the folder it was started from: that is where
        // appsettings.json is, and "wwwroot" with the UI's files.
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });
        adjust?.Invoke(builder);
        AllowedHosts hosts = ReadAllowedHosts(builder.Configuration);
        ApplyDefaults(builder.Configuration);
        AllowedOrigins origins = ReadAllowedOrigins(builder.Configuration);
        string passwordFile = Setting(builder.Configuration, "Lan:PasswordFile", DefaultPasswordFile());

        // The simulated devices of sim.cmd say that they run in "simulators" beside the LAN
        // password: %LOCALAPPDATA%\Lumenet\simulators for the desk of this PC, a folder of its own
        // for a test desk.
        builder.Services.AddSingleton(new Simulators(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(passwordFile)) ?? "", "simulators")));

        AddSounds(builder.Services, builder.Configuration, passwordFile);
        AddDesk(builder.Services, builder.Configuration, box);
        AddOutputs(builder.Services, builder.Configuration, passwordFile);
        AddMidi(builder.Services, passwordFile);
        AddOsc(builder.Services, builder.Configuration, passwordFile);
        AddHub(builder.Services, origins);
        AddLan(builder, pc, passwordFile);

        WebApplication app = builder.Build();

        // The desk plays its sounds through the engine of this PC; what a hand sets on the mixer's PLAYER
        // L/R of a Soundcraft Ui mixer (the volume of all sounds, MUTE) every page hears of.
        Desk soundDesk = app.Services.GetRequiredService<Desk>();
        soundDesk.UseSoundPlayer(app.Services.GetRequiredService<SoundEngine>());
        IHubContext<DeskHub, IDeskClient> pages = app.Services.GetRequiredService<IHubContext<DeskHub, IDeskClient>>();
        soundDesk.SoundMasterHeard += (decibels, mute) => _ = pages.Clients.All.SoundMasterChanged(decibels, mute);

        // Work that was not saved when the program stopped comes back before any UI connects.
        app.Services.GetRequiredService<Autosave>().Restore();

        RefuseForeignHosts(app, hosts);
        app.Use((context, next) => LanGate.InvokeAsync(context, app.Services.GetRequiredService<LanAccess>(), next));
        RefuseOtherPages(app, origins);
        app.UseCors();
        ServeUi(app);
        app.MapGet("/api/health", () => new HealthAnswer(AppInfo.Name, AppInfo.Version));
        MapSoundFiles(app);
        app.MapHub<DeskHub>(DeskHubPath);
        return app;
    }

    // The sound files: their store (by default %LOCALAPPDATA%\Lumenet\sounds, beside the LAN password,
    // so that a test desk with a password file of its own has its own), and the engine that plays them
    // on the device of this PC, whose setup is kept beside the LAN password too (sound.json). The
    // setting "Sound:Output" at "none" opens no device at all: the tests, a test desk beside the real one.
    private static void AddSounds(IServiceCollection services, IConfiguration settings, string passwordFile)
    {
        string beside = Path.GetDirectoryName(Path.GetFullPath(passwordFile)) ?? "";
        string folder = Setting(settings, "Sound:Folder", Path.Combine(beside, "sounds"));
        bool devices = !string.Equals(settings["Sound:Output"], "none", StringComparison.OrdinalIgnoreCase);
        services.AddSingleton(new SoundStore(folder));
        services.AddSingleton(provider =>
        {
            var engine = new SoundEngine(
                provider.GetRequiredService<SoundStore>(),
                Path.Combine(beside, SoundEngine.SetupFileName),
                devices,
                provider.GetRequiredService<ILoggerFactory>().CreateLogger<SoundEngine>());
            IHubContext<DeskHub, IDeskClient> hub = provider.GetRequiredService<IHubContext<DeskHub, IDeskClient>>();
            engine.Changed += view =>
            {
                _ = hub.Clients.Group(LanAccess.PcGroup).SoundOutputChanged(view);
                _ = hub.Clients.All.SoundListenChanged(new ListenView(view.Listen, view.ListenState));
            };
            engine.MixerChanged += mixer => _ = hub.Clients.All.MixerChanged(mixer);
            return engine;
        });

        // The same engine: the host starts and stops it, the desk and the hub use it.
        services.AddHostedService(provider => provider.GetRequiredService<SoundEngine>());
    }

    // POST /api/sounds?name=thunder.mp3 takes a sound file into the store (a page sends it: the hub's
    // messages are too small for songs) and answers what it is; GET /api/sounds/{hash} gives it back,
    // and GET /api/sounds/{hash}/wave its wave. All behind the LAN gate, like everything else.
    private static void MapSoundFiles(WebApplication app)
    {
        app.MapPost("/api/sounds", async (HttpContext context, SoundStore store) =>
        {
            if (context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
                limit.MaxRequestBodySize = SoundStore.MaxBytes;

            try
            {
                return Results.Ok(await store.AddAsync(context.Request.Body, context.Request.Query["name"], context.RequestAborted));
            }
            catch (DeskException e)
            {
                return Results.BadRequest(new { message = e.Message });
            }
        });

        app.MapGet("/api/sounds/{hash}", (string hash, SoundStore store) =>
            store.PathOf(hash) is { } path
                ? Results.File(path, SoundContentType(path), enableRangeProcessing: true)
                : Results.NotFound());

        // GET /api/sounds/{hash}/wave: how loud the file is along its length, for the wave the Sounds
        // view draws (SoundWave). Reading a whole file takes a moment the first time: not on the hub.
        app.MapGet("/api/sounds/{hash}/wave", async (string hash, SoundStore store, CancellationToken cancel) =>
        {
            try
            {
                return await Task.Run(() => store.WaveOf(hash), cancel) is { } wave ? Results.Ok(wave) : Results.NotFound();
            }
            catch (DeskException e)
            {
                return Results.BadRequest(new { message = e.Message });
            }
        });

        // POST /api/export/pack?name=Concert: the text of Export in the body; the answer is a ZIP of it
        // with the sound files of its project (SoundPack). Made in a file of its own that goes when sent.
        app.MapPost("/api/export/pack", async (HttpContext context, SoundStore store) =>
        {
            if (context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
                limit.MaxRequestBodySize = SoundPack.MaxJsonBytes;

            string json;
            using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
                json = await reader.ReadToEndAsync(context.RequestAborted);

            string name = SoundPack.SafeName(context.Request.Query["name"]);
            var pack = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose);
            try
            {
                string[] missing = await Task.Run(() => SoundPack.Write(pack, name, json, store), context.RequestAborted);
                if (missing.Length > 0)
                    context.Response.Headers["X-Missing-Sounds"] = Uri.EscapeDataString(string.Join("\n", missing));
                pack.Position = 0;
                return Results.File(pack, "application/zip", name + ".zip");
            }
            catch (DeskException e)
            {
                await pack.DisposeAsync();
                return Results.BadRequest(new { message = e.Message });
            }
        });

        // POST /api/import/unpack: a ZIP of Export in the body. Its sound files go into the store; the
        // answer is its file of Export, to be imported as any, and the files that could not be taken.
        app.MapPost("/api/import/unpack", async (HttpContext context, SoundStore store) =>
        {
            if (context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
                limit.MaxRequestBodySize = SoundPack.MaxPackBytes;

            await using var pack = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose);
            await context.Request.Body.CopyToAsync(pack, context.RequestAborted);
            pack.Position = 0;
            try
            {
                (string json, string[] leftOut) = await SoundPack.ReadAsync(pack, store, context.RequestAborted);
                return Results.Ok(new { text = json, leftOut });
            }
            catch (DeskException e)
            {
                return Results.BadRequest(new { message = e.Message });
            }
        });
    }

    private static string SoundContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".aif" or ".aiff" => "audio/aiff",
            ".flac" => "audio/flac",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            ".wma" => "audio/x-ms-wma",
            _ => "application/octet-stream",
        };
    }

    // The address to listen on ("urls") gets its default here, for the case that no settings
    // file, environment variable or command line gives one.
    //
    // ASP.NET Core checks the setting "AllowedHosts" by itself, against a fixed list. This
    // program does that check itself (RefuseForeignHosts), because it also answers to this PC's
    // own addresses, which can change while it runs. So ASP.NET Core's check is switched off,
    // after ReadAllowedHosts has taken the setting over.
    private static void ApplyDefaults(ConfigurationManager settings)
    {
        if (string.IsNullOrEmpty(settings[WebHostDefaults.ServerUrlsKey]))
            settings[WebHostDefaults.ServerUrlsKey] = DefaultUrl;

        settings[AllowedHostsKey] = "*";
    }

    private static AllowedHosts ReadAllowedHosts(IConfiguration settings)
    {
        return new AllowedHosts(Setting(settings, AllowedHostsKey, DefaultAllowedHosts));
    }

    // A list that is missing or empty counts as not given, like any other setting.
    private static AllowedOrigins ReadAllowedOrigins(IConfiguration settings)
    {
        string[]? listed = settings.GetSection("Server:AllowedOrigins").Get<string[]>();
        if (listed is null || listed.Length == 0)
            listed = DefaultAllowedOrigins;

        return new AllowedOrigins(listed);
    }

    // The parts that live as long as the server: the desk, the projects with their undo history
    // and their autosave, the boxes. The outputs besides the boxes follow in AddOutputs.
    private static void AddDesk(IServiceCollection services, IConfiguration settings, IBox? box)
    {
        string projectsFolder = Setting(settings, "Projects:Folder", DefaultProjectsFolder());
        string examplesFolder = Setting(settings, "Projects:Examples", Path.Combine(AppContext.BaseDirectory, "examples"));
        string autosaveFile = Setting(settings, "Autosave:File", DefaultAutosaveFile());
        string boxHost = Setting(settings, "Box:Host", DefaultBoxHost);
        int boxPort = settings.GetValue("Box:Port", DmxBoxClient.DefaultPort);

        // False for a test desk beside the real one: it must not find the real boxes on the PC's
        // adapters (10.1.n.x) and play on them.
        bool discover = settings.GetValue("Box:Discover", true);

        services.AddSingleton<Desk>();
        services.AddSingleton(provider => new ProjectStore(
            provider.GetRequiredService<Desk>(), projectsFolder, examplesFolder, provider.GetRequiredService<SoundStore>()));
        services.AddSingleton(provider => new UndoHistory(provider.GetRequiredService<Desk>()));
        services.AddSingleton(provider => new Autosave(
            provider.GetRequiredService<Desk>(),
            provider.GetRequiredService<ProjectStore>(),
            autosaveFile,
            provider.GetRequiredService<ILogger<Autosave>>()));
        services.AddHostedService(provider => provider.GetRequiredService<Autosave>());
        services.AddSingleton(provider => new BoxNetwork(
            provider.GetRequiredService<Desk>(),
            provider.GetRequiredService<IHubContext<DeskHub, IDeskClient>>().Clients.All,
            provider.GetRequiredService<ThisPc>(),
            boxHost,
            boxPort,
            discover,
            box,
            provider.GetRequiredService<ILoggerFactory>(),
            simulators: provider.GetRequiredService<Simulators>()));

        // The same network as above: the host starts and stops it, the hub asks it for the boxes.
        services.AddHostedService(provider => provider.GetRequiredService<BoxNetwork>());

        // Lets time pass on the desk, for the sequences and the stack.
        services.AddHostedService(provider => new DeskClock(
            provider.GetRequiredService<Desk>(),
            provider.GetRequiredService<IHubContext<DeskHub, IDeskClient>>().Clients.All,
            provider.GetRequiredService<ILogger<DeskClock>>()));
    }

    // The outputs besides the boxes: Art-Net nodes, sACN and DMX USB interfaces. A test desk beside
    // the real one sends from 127.0.0.1 only ("--Outputs:Bind=127.0.0.1"), to ports of its own.
    // The desk's CID as an sACN source is kept next to the LAN password.
    private static void AddOutputs(IServiceCollection services, IConfiguration settings, string passwordFile)
    {
        string bind = Setting(settings, "Outputs:Bind", IPAddress.Any.ToString());
        var outputSettings = new OutputSettings
        {
            Bind = IPAddress.TryParse(bind, out IPAddress? address) ? address : IPAddress.Any,
            ArtNetPort = settings.GetValue("Outputs:ArtNetPort", ArtNetSender.DefaultPort),
            ArtNetReplyPort = settings.GetValue("Outputs:ArtNetReplyPort", ArtNetSender.DefaultPort),
            SacnPort = settings.GetValue("Outputs:SacnPort", SacnSender.DefaultPort),
            CidFile = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(passwordFile)) ?? "", SacnSource.FileName),
        };

        services.AddSingleton(provider =>
        {
            BoxNetwork boxes = provider.GetRequiredService<BoxNetwork>();
            return new OutputNetwork(
                provider.GetRequiredService<Desk>(),
                provider.GetRequiredService<IHubContext<DeskHub, IDeskClient>>().Clients.All,
                provider.GetRequiredService<ThisPc>(),
                outputSettings,
                boxes.IsBoxAddress,
                provider.GetRequiredService<ILoggerFactory>(),
                simulators: provider.GetRequiredService<Simulators>());
        });

        // The same outputs: the host starts and stops them, the hub asks them for their states.
        services.AddHostedService(provider => provider.GetRequiredService<OutputNetwork>());
    }

    // The MIDI controllers of this PC: the system's (DryWetMIDI, on Windows and macOS) and the
    // simulated ones of sim.cmd. Their setup belongs to the PC and is kept beside the LAN password,
    // in midi.json: a test desk with a password file of its own has a setup of its own too.
    private static void AddMidi(IServiceCollection services, string passwordFile)
    {
        string file = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(passwordFile)) ?? "", MidiSetupFile.FileName);
        services.AddSingleton(provider =>
        {
            ILogger log = provider.GetRequiredService<ILoggerFactory>().CreateLogger<MidiControl>();
            IHubContext<DeskHub, IDeskClient> hub = provider.GetRequiredService<IHubContext<DeskHub, IDeskClient>>();
            return new MidiControl(
                new CombinedMidiPorts(new DryWetMidiPorts(), new SimulatedMidiPorts(provider.GetRequiredService<Simulators>())),
                new MidiSetupFile(file, log),
                new MidiDeskFaders(
                    provider.GetRequiredService<Desk>(),
                    provider.GetRequiredService<ProjectStore>(),
                    provider.GetRequiredService<UndoHistory>(),
                    hub.Clients.All,
                    log),
                new HubMidiPages(hub, log),
                log);
        });

        // The same MIDI: the host starts and stops it, the hub asks it and tells it.
        services.AddHostedService(provider => provider.GetRequiredService<MidiControl>());
    }

    // OSC from other programs: off until a page on the PC switches it on. Its setup belongs to the PC,
    // beside midi.json (osc.json). A test desk listens at 127.0.0.1 only ("--Osc:Bind=127.0.0.1"): no
    // other device reaches it, and Windows asks nothing about the network.
    private static void AddOsc(IServiceCollection services, IConfiguration settings, string passwordFile)
    {
        string file = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(passwordFile)) ?? "", OscSetupFile.FileName);
        IPAddress bind = IPAddress.TryParse(Setting(settings, "Osc:Bind", IPAddress.Any.ToString()), out IPAddress? address) ? address : IPAddress.Any;
        services.AddSingleton(provider =>
        {
            ILogger log = provider.GetRequiredService<ILoggerFactory>().CreateLogger<OscControl>();
            LanAccess lan = provider.GetRequiredService<LanAccess>();
            return new OscControl(new OscSetupFile(file, log), provider.GetRequiredService<MidiControl>(), lan.IsLocal, () => lan.On, bind, log);
        });

        // The same OSC: the host starts and stops it, the hub switches it.
        services.AddHostedService(provider => provider.GetRequiredService<OscControl>());
    }

    private static void AddHub(IServiceCollection services, AllowedOrigins origins)
    {
        services.AddSingleton<UserErrorFilter>();
        services.AddSignalR(options =>
        {
            options.AddFilter<UserErrorFilter>();

            // A show file to import comes in one message (see DeskHub.ImportProject). SignalR's
            // limit of 32 KB would close the connection for a show of a few dozen cues.
            options.MaximumReceiveMessageSize = MaxMessageBytes;
        });

        // SignalR writes an error with a stack trace for every hub call that ends in an
        // exception, also when the hub only refused a mistake of the user. UserErrorFilter
        // writes one line for a refusal and the whole error for a real fault, so SignalR's
        // own entries would only say the same again, and louder.
        services.AddLogging(logging => logging.AddFilter(HubCallLog, LogLevel.None));

        // CORS: a page from another address than the server's own, like the UI on the
        // development server, may only talk to the server if the server says so. The same
        // list decides as for the hub. SignalR needs "credentials" allowed on top of that.
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .SetIsOriginAllowed(origins.Allows)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));
    }

    // LAN: whether other devices in the network may use the desk. While it is off, a connection
    // from another device is closed the moment it is made, before a byte of it is read.
    private static void AddLan(WebApplicationBuilder builder, ThisPc? pc, string passwordFile)
    {
        // A pretend PC from the tests is theirs to dispose of; the real one the host disposes.
        if (pc is null)
            builder.Services.AddSingleton(_ => new ThisPc());
        else
            builder.Services.AddSingleton(pc);

        builder.Services.AddSingleton(provider => new LanAccess(
            provider.GetRequiredService<ThisPc>(),
            passwordFile,
            provider.GetRequiredService<IServer>(),
            provider.GetRequiredService<IHubContext<DeskHub, IDeskClient>>(),
            provider.GetRequiredService<ILogger<LanAccess>>()));

        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ConfigureEndpointDefaults(listen => listen.Use(next =>
        {
            LanAccess lan = listen.ApplicationServices.GetRequiredService<LanAccess>();
            return connection =>
            {
                IPAddress? remote = (connection.RemoteEndPoint as IPEndPoint)?.Address;
                IPAddress? local = (connection.LocalEndPoint as IPEndPoint)?.Address;
                return lan.MayConnect(remote, local) ? next(connection) : Task.CompletedTask;
            };
        })));
    }

    // Stands at the very front: a request that calls the server by a name that is not one of
    // this PC's gets "400 Bad Request" and nothing else.
    private static void RefuseForeignHosts(WebApplication app, AllowedHosts hosts)
    {
        ThisPc pc = app.Services.GetRequiredService<ThisPc>();
        app.Use(async (context, next) =>
        {
            HostString host = context.Request.Host;
            if (!hosts.Allows(host, pc))
            {
                app.Logger.LogWarning(
                    "Refused a request for {Path} under the host name {Host}: it is not a name of this PC, nor in AllowedHosts.",
                    context.Request.Path,
                    host.Value);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            await next(context);
        });
    }

    // Stands in front of the hub: a request from a web page that is not on the list, and is not
    // the page this server handed out itself, gets "403 Forbidden" and never reaches SignalR,
    // whatever kind of request it is. CORS alone would not do, see AllowedOrigins.
    private static void RefuseOtherPages(WebApplication app, AllowedOrigins origins)
    {
        app.Use(async (context, next) =>
        {
            HttpRequest request = context.Request;
            string? origin = OriginOf(request);
            if (request.Path.StartsWithSegments(HubsPath)
                && !origins.Allows(origin)
                && !AllowedOrigins.IsServerItself(origin, $"{request.Scheme}://{request.Host}"))
            {
                app.Logger.LogWarning(
                    "Refused a request to {Path} from the web page {Origin}: it is not in Server:AllowedOrigins.",
                    request.Path,
                    origin);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next(context);
        });
    }

    // Null if the request has no Origin header: then it does not come from a web page.
    private static string? OriginOf(HttpRequest request)
    {
        StringValues origin = request.Headers.Origin;
        return origin.Count == 0 ? null : origin.ToString();
    }

    // The UI is built separately. The server hands its files out too: those in the folder of
    // the setting "Ui:Folder" (run.mjs builds the UI and names its folder there), or else those
    // next to the program, in "wwwroot" (Lumenet.exe carries them inside itself instead). Another
    // device gets the UI this way. Without the files the server offers the hub and the API only.
    private static void ServeUi(WebApplication app)
    {
        string? folder = app.Configuration["Ui:Folder"];
        IFileProvider files;
        if (string.IsNullOrWhiteSpace(folder))
        {
            files = app.Environment.WebRootFileProvider;
        }
        else
        {
            string fullPath = Path.GetFullPath(folder, app.Environment.ContentRootPath);
            if (!Directory.Exists(fullPath))
            {
                // Said once the server runs, where it stands out after the lines of the start.
                app.Lifetime.ApplicationStarted.Register(() => app.Logger.LogWarning(
                    "The UI's files are not in {Folder}, so other devices get no page. Build the UI with \"npm run build\" in frontend.",
                    fullPath));
                return;
            }

            files = new PhysicalFileProvider(fullPath);
        }

        if (!files.GetFileInfo("index.html").Exists)
            return;

        var options = new StaticFileOptions { FileProvider = files };
        app.UseStaticFiles(options);

        // The UI has addresses of its own that are no files. They all get the UI's one page.
        app.MapFallbackToFile("index.html", options);
    }

    // A setting that is missing or empty counts as not given.
    private static string Setting(IConfiguration settings, string key, string defaultValue)
    {
        string? value = settings[key];
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static string DefaultProjectsFolder()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppInfo.Name);
    }

    // On this PC only, not in the documents: those may be copied to a cloud.
    private static string DefaultPasswordFile()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.Name, "lan-password.txt");
    }

    // On this PC only too: it is no project, only the work that is not saved yet.
    private static string DefaultAutosaveFile()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.Name, "autosave.json");
    }
}
