using System.Net;
using System.Net.WebSockets;

namespace DmxBox.Tests;

/// <summary>
/// The server's plain HTTP side, and what keeps strangers away from the hub. The hub has no
/// login, so two rules are its protection: a web page may use it only if the page's origin
/// is on the list, and the server answers only to its own host names.
/// </summary>
public sealed class ServerProtectionTests
{
    private const string Negotiate = "/hub/desk/negotiate?negotiateVersion=1";
    private const string OtherSite = "http://evil.example";

    // ---------------------------------------------------------------- the plain HTTP side

    [Fact]
    public async Task HealthNamesTheServerAndItsVersion()
    {
        await using TestServer server = await TestServer.StartAsync();
        using HttpClient http = server.CreateHttpClient();

        using HttpResponseMessage response = await http.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            $$"""{"name":"Lumenet","version":"{{TestServer.Version}}"}""",
            await response.Content.ReadAsStringAsync());
        Assert.Matches(@"^\d+\.\d+\.\d+$", TestServer.Version);   // three numbers, like 0.1.0
    }

    [Fact]
    public async Task WithoutTheFilesOfTheUiNothingIsServedAtTheRoot()
    {
        await using TestServer server = await TestServer.StartAsync();
        using HttpClient http = server.CreateHttpClient();

        using HttpResponseMessage response = await http.GetAsync("/");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------------------------------------------------------------- web pages of other sites

    [Fact]
    public async Task HubRefusesAWebPageOfAnotherSite()
    {
        await using TestServer server = await TestServer.StartAsync();

        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(server, HttpMethod.Post, Negotiate, OtherSite));
        Assert.Equal(
            HttpStatusCode.Forbidden,
            await StatusAsync(server, HttpMethod.Post, Negotiate, "http://localhost:5080.evil.example"));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(server, HttpMethod.Post, Negotiate, "https://localhost:5080"));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(server, HttpMethod.Post, Negotiate, "http://localhost:5081"));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(server, HttpMethod.Post, Negotiate, "null"));
    }

    [Fact]
    public async Task HubAnswersTheUiAtEachOfItsAddresses()
    {
        await using TestServer server = await TestServer.StartAsync();

        string[] uiAddresses =
        [
            "http://localhost:5080", "http://127.0.0.1:5080",   // the UI handed out by the server
            "http://localhost:4200", "http://127.0.0.1:4200",   // the UI on the development server
        ];
        foreach (string origin in uiAddresses)
            Assert.Equal(HttpStatusCode.OK, await StatusAsync(server, HttpMethod.Post, Negotiate, origin));
    }

    [Fact]
    public async Task HubAnswersAProgramThatSendsNoOrigin()
    {
        await using TestServer server = await TestServer.StartAsync();

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(server, HttpMethod.Post, Negotiate, origin: null));
    }

    [Fact]
    public async Task HubRefusesAnotherSiteWhateverTheRequest()
    {
        await using TestServer server = await TestServer.StartAsync();

        // The requests of the transports without WebSockets, a preflight, another spelling
        // of the path, and a path below /hub where no hub is.
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(server, HttpMethod.Get, "/hub/desk?id=abc", OtherSite));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(server, HttpMethod.Post, "/hub/desk?id=abc", OtherSite));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(server, HttpMethod.Delete, "/hub/desk?id=abc", OtherSite));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(server, HttpMethod.Options, Negotiate, OtherSite));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(server, HttpMethod.Post, "/HUB/Desk/negotiate", OtherSite));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(server, HttpMethod.Get, "/hub", OtherSite));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(server, HttpMethod.Get, "/hub/other", OtherSite));
    }

    [Fact]
    public async Task WebSocketOfAnotherSiteIsRefused()
    {
        await using TestServer server = await TestServer.StartAsync();
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Origin", OtherSite);
        socket.Options.CollectHttpResponseDetails = true;

        await Assert.ThrowsAsync<WebSocketException>(() => socket.ConnectAsync(server.HubAddress, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Forbidden, socket.HttpStatusCode);
    }

    [Fact]
    public async Task WebSocketOfTheUiIsAccepted()
    {
        await using TestServer server = await TestServer.StartAsync();
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Origin", "http://localhost:4200");

        await socket.ConnectAsync(server.HubAddress, CancellationToken.None);

        Assert.Equal(WebSocketState.Open, socket.State);
    }

    [Fact]
    public async Task OriginRuleGuardsTheHubOnly()
    {
        await using TestServer server = await TestServer.StartAsync();
        using HttpClient http = server.CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("Origin", OtherSite);

        using HttpResponseMessage response = await http.SendAsync(request);

        // The health answer is no secret, so the request is served. But the browser of the
        // other site's page is not told that the page may read the answer.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task AllowedOriginsComeFromTheSettings()
    {
        await using TestServer server = await TestServer.StartAsync(
            "--Server:AllowedOrigins:0=http://desk.example:8080", "--Server:AllowedOrigins:1=http://localhost:9000/");

        Assert.Equal(HttpStatusCode.OK, await StatusAsync(server, HttpMethod.Post, Negotiate, "http://desk.example:8080"));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(server, HttpMethod.Post, Negotiate, "http://localhost:9000"));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(server, HttpMethod.Post, Negotiate, "http://localhost:5080"));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(server, HttpMethod.Post, Negotiate, "http://desk.example"));
    }

    // ---------------------------------------------------------------- cross-origin requests of the UI

    [Fact]
    public async Task UiOnTheDevelopmentServerGetsTheHeadersTheBrowserAsksFor()
    {
        await using TestServer server = await TestServer.StartAsync();
        using HttpClient http = server.CreateHttpClient();

        // The browser's question before the real request ("preflight") ...
        using var preflight = new HttpRequestMessage(HttpMethod.Options, Negotiate);
        preflight.Headers.Add("Origin", "http://localhost:4200");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "x-requested-with,x-signalr-user-agent");
        using HttpResponseMessage allowed = await http.SendAsync(preflight);

        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        Assert.Equal("http://localhost:4200", Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Credentials")));
        Assert.Equal("POST", Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Methods")));
        Assert.Equal(
            "x-requested-with,x-signalr-user-agent",
            Assert.Single(allowed.Headers.GetValues("Access-Control-Allow-Headers")));

        // ... and the real request.
        using var request = new HttpRequestMessage(HttpMethod.Post, Negotiate);
        request.Headers.Add("Origin", "http://127.0.0.1:4200");
        using HttpResponseMessage response = await http.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("http://127.0.0.1:4200", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }

    // ---------------------------------------------------------------- host names

    [Fact]
    public async Task RequestUnderAForeignHostNameIsRefused()
    {
        await using TestServer server = await TestServer.StartAsync();

        // A web page can give this PC a name of its own (DNS rebinding) and then call the
        // server under that name. The server answers to its own names only.
        Assert.Equal(HttpStatusCode.BadRequest, await StatusForHostAsync(server, "/api/health", "evil.example"));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusForHostAsync(server, "/api/health", "evil.example:5080"));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusForHostAsync(server, Negotiate, "localhost.evil.example"));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusForHostAsync(server, "/api/health", "192.168.1.20"));
    }

    [Fact]
    public async Task RequestUnderTheNamesOfThisPcIsAnswered()
    {
        await using TestServer server = await TestServer.StartAsync();

        Assert.Equal(HttpStatusCode.OK, await StatusForHostAsync(server, "/api/health", "localhost"));
        Assert.Equal(HttpStatusCode.OK, await StatusForHostAsync(server, "/api/health", "localhost:5080"));
        Assert.Equal(HttpStatusCode.OK, await StatusForHostAsync(server, "/api/health", "127.0.0.1:5080"));
        Assert.Equal(HttpStatusCode.OK, await StatusForHostAsync(server, "/api/health", "LOCALHOST:4200"));
    }

    [Fact]
    public async Task AllowedHostsComeFromTheSettings()
    {
        await using TestServer server = await TestServer.StartAsync("--AllowedHosts=desk.example");

        Assert.Equal(HttpStatusCode.OK, await StatusForHostAsync(server, "/api/health", "desk.example"));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusForHostAsync(server, "/api/health", "localhost"));
    }

    // The status of a request that carries the Origin header a browser would send for a page
    // of that origin; without the header if origin is null.
    private static async Task<HttpStatusCode> StatusAsync(TestServer server, HttpMethod method, string path, string? origin)
    {
        using HttpClient http = server.CreateHttpClient();
        using var request = new HttpRequestMessage(method, path);
        if (origin is not null)
            request.Headers.Add("Origin", origin);

        using HttpResponseMessage response = await http.SendAsync(request);
        return response.StatusCode;
    }

    // The status of a GET or POST that calls the server by another name than its address.
    private static async Task<HttpStatusCode> StatusForHostAsync(TestServer server, string path, string host)
    {
        using HttpClient http = server.CreateHttpClient();
        HttpMethod method = path == Negotiate ? HttpMethod.Post : HttpMethod.Get;
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Host = host;

        using HttpResponseMessage response = await http.SendAsync(request);
        return response.StatusCode;
    }
}
