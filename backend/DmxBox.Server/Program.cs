using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Primitives;

namespace DmxBox.Server;

/// <summary>
/// The Lumenet server: the fader desk behind a SignalR hub for the UI in the browser, and the
/// link from the desk to the dmxbox. Settings come from appsettings.json next to the program,
/// from the environment and from the command line, for example "--Box:Host=127.0.0.1".
/// </summary>
internal static class Program
{
    private const string DefaultUrl = "http://localhost:5080";

    // The names by which the server lets itself be called: those of this PC and no others.
    private const string DefaultAllowedHosts = "localhost;127.0.0.1";

    // The address the box always has on its USB cable (NET_IP in the firmware's config.h).
    private const string DefaultBoxHost = "192.168.7.1";

    // Everything SignalR serves is below this path; the desk's hub is the only hub so far.
    private const string HubsPath = "/hub";
    private const string DeskHubPath = HubsPath + "/desk";

    // The name under which SignalR logs the hub calls that failed.
    private const string HubCallLog = "Microsoft.AspNetCore.SignalR.Internal.DefaultHubDispatcher";

    // The pages that may use the hub: the UI handed out by this server, and the UI on the
    // development server ("ng serve", port 4200), each under both names of this PC.
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
    public static WebApplication Build(string[] args, IBox? box = null)
    {
        // The program's own folder counts, not the folder it was started from: that is where
        // appsettings.json is, and "wwwroot" with the UI's files.
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });
        ApplyDefaults(builder.Configuration);
        AllowedOrigins origins = ReadAllowedOrigins(builder.Configuration);

        AddDesk(builder.Services, builder.Configuration, box);
        AddHub(builder.Services, origins);

        WebApplication app = builder.Build();
        RefuseOtherPages(app, origins);
        app.UseCors();
        ServeUi(app);
        app.MapGet("/api/health", () => new HealthAnswer(AppInfo.Name, AppInfo.Version));
        app.MapHub<DeskHub>(DeskHubPath);
        return app;
    }

    // Two settings that ASP.NET Core reads by itself get their default here, for the case
    // that no settings file, environment variable or command line gives them: the address to
    // listen on ("urls"), and the host names the server answers to. The second one keeps out
    // a web page that calls the server by a name of its own, which it has pointed at this PC
    // (DNS rebinding).
    private static void ApplyDefaults(ConfigurationManager settings)
    {
        if (string.IsNullOrEmpty(settings[WebHostDefaults.ServerUrlsKey]))
            settings[WebHostDefaults.ServerUrlsKey] = DefaultUrl;
        if (string.IsNullOrEmpty(settings["AllowedHosts"]))
            settings["AllowedHosts"] = DefaultAllowedHosts;
    }

    // A list that is missing or empty counts as not given, like any other setting.
    private static AllowedOrigins ReadAllowedOrigins(IConfiguration settings)
    {
        string[]? listed = settings.GetSection("Server:AllowedOrigins").Get<string[]>();
        if (listed is null || listed.Length == 0)
            listed = DefaultAllowedOrigins;

        return new AllowedOrigins(listed);
    }

    // The three parts that live as long as the server: the desk, the projects, the box link.
    private static void AddDesk(IServiceCollection services, IConfiguration settings, IBox? box)
    {
        string projectsFolder = Setting(settings, "Projects:Folder", DefaultProjectsFolder());
        string boxHost = Setting(settings, "Box:Host", DefaultBoxHost);
        int boxPort = settings.GetValue("Box:Port", DmxBoxClient.DefaultPort);

        services.AddSingleton<Desk>();
        services.AddSingleton(provider => new ProjectStore(provider.GetRequiredService<Desk>(), projectsFolder));
        services.AddSingleton(provider => new BoxLink(
            provider.GetRequiredService<Desk>(),
            box ?? new RealBox(boxHost, boxPort),
            provider.GetRequiredService<IHubContext<DeskHub, IDeskClient>>().Clients.All,
            boxHost,
            provider.GetRequiredService<ILogger<BoxLink>>()));

        // The same link as above: the host starts and stops it, the hub asks it for the box's state.
        services.AddHostedService(provider => provider.GetRequiredService<BoxLink>());

        // Lets time pass on the desk, for the sequences and the stack.
        services.AddHostedService(provider => new DeskClock(
            provider.GetRequiredService<Desk>(),
            provider.GetRequiredService<IHubContext<DeskHub, IDeskClient>>().Clients.All,
            provider.GetRequiredService<ILogger<DeskClock>>()));
    }

    private static void AddHub(IServiceCollection services, AllowedOrigins origins)
    {
        services.AddSingleton<UserErrorFilter>();
        services.AddSignalR(options => options.AddFilter<UserErrorFilter>());

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

    // Stands in front of the hub: a request from a web page that is not on the list gets
    // "403 Forbidden" and never reaches SignalR, whatever kind of request it is. CORS alone
    // would not do, see AllowedOrigins.
    private static void RefuseOtherPages(WebApplication app, AllowedOrigins origins)
    {
        app.Use(async (context, next) =>
        {
            string? origin = OriginOf(context.Request);
            if (context.Request.Path.StartsWithSegments(HubsPath) && !origins.Allows(origin))
            {
                app.Logger.LogWarning(
                    "Refused a request to {Path} from the web page {Origin}: it is not in Server:AllowedOrigins.",
                    context.Request.Path,
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

    // The UI is built separately. If its files were put next to the program, in "wwwroot", the
    // server hands them out as well. Without them it offers the hub and the API only.
    private static void ServeUi(WebApplication app)
    {
        if (!app.Environment.WebRootFileProvider.GetFileInfo("index.html").Exists)
            return;

        app.UseStaticFiles();

        // The UI has addresses of its own that are no files. They all get the UI's one page.
        app.MapFallbackToFile("index.html");
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
}
