using System.Net;
using System.Text.Json;
using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// LAN: other devices in the same network, a phone above all, may use the desk while the owner
/// has switched LAN on at the PC, and only with the password. This PC itself is never asked.
/// </summary>
/// <remarks>
/// The tests' connections come from 127.0.0.1. A test that plays the phone tells the pretend PC
/// to count those as connections of another device.
/// </remarks>
public sealed class LanTests
{
    private const string Negotiate = "/hub/desk/negotiate?negotiateVersion=1";

    // ---------------------------------------------------------------- LAN off

    [Fact]
    public async Task LanIsOffAtTheStartAndAnotherDeviceGetsNoConnectionAtAll()
    {
        await using TestServer server = await TestServer.StartAsync();
        Assert.False(server.Lan.On);
        server.Pc.LoopbackIsAnotherDevice = true;
        using HttpClient phone = server.CreateHttpClient();

        // Closed before the server reads a byte: there is no answer, not even a refusal.
        await Assert.ThrowsAsync<HttpRequestException>(() => phone.GetAsync("/"));
        await Assert.ThrowsAsync<HttpRequestException>(() => phone.GetAsync("/api/health"));
        await Assert.ThrowsAnyAsync<Exception>(() => HubClient.ConnectAsync(server.HubAddress));
    }

    [Fact]
    public async Task ThisPcNeedsNoPasswordWhetherLanIsOnOrOff()
    {
        await using TestServer server = await TestServer.StartAsync();
        using HttpClient pc = server.CreateHttpClient();

        Assert.Equal(HttpStatusCode.OK, (await pc.GetAsync("/api/health")).StatusCode);
        server.Lan.Switch(true);
        Assert.Equal(HttpStatusCode.OK, (await pc.GetAsync("/api/health")).StatusCode);
        await using HubClient ui = await server.ConnectAsync();
        Assert.Equal(JsonValueKind.Object, (await ui.InvokeAsync("Hello")).ValueKind);
    }

    // ---------------------------------------------------------------- LAN on: the password

    [Fact]
    public async Task WithLanOnAnotherDeviceIsAskedForThePasswordAndForNothingElse()
    {
        await using TestServer server = await TestServer.StartAsync();
        server.Lan.Switch(true);
        server.Pc.LoopbackIsAnotherDevice = true;
        using HttpClient phone = server.CreateBrowser(new CookieContainer());

        using HttpResponseMessage page = await phone.GetAsync("/stack");
        Assert.Equal(HttpStatusCode.Unauthorized, page.StatusCode);
        Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
        string html = await page.Content.ReadAsStringAsync();
        Assert.Contains("""<form method="post" action="/lan/login">""", html);
        Assert.Contains("""name="password" """, html);
        Assert.Contains("""name="back" value="/stack">""", html);
        Assert.DoesNotContain(server.Lan.Password, html);

        // The desk itself, the hub and the API, stay shut.
        using HttpResponseMessage health = await phone.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.Unauthorized, health.StatusCode);
        Assert.Equal("The desk asks for the LAN password.", await health.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.PostAsync(Negotiate, null)).StatusCode);
        await Assert.ThrowsAnyAsync<Exception>(() => HubClient.ConnectAsync(server.HubAddress));
    }

    [Fact]
    public async Task RightPasswordGivesTheDeviceACookieAndTheDeskIsOpenToIt()
    {
        await using TestServer server = await TestServer.StartAsync();
        server.Lan.Switch(true);
        server.Pc.LoopbackIsAnotherDevice = true;
        var cookies = new CookieContainer();
        using HttpClient phone = server.CreateBrowser(cookies);

        // Typed on a phone: small letters, a space. It is the same password.
        string typed = server.Lan.Password.ToLowerInvariant().Insert(2, " ");
        using HttpResponseMessage answer = await LogInAsync(phone, typed, back: "/stack");

        Assert.Equal(HttpStatusCode.SeeOther, answer.StatusCode);
        Assert.Equal("/stack", answer.Headers.Location?.OriginalString);
        string setCookie = Assert.Single(answer.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);

        // The cookie holds a random key of 256 bits, not the password.
        Assert.Matches("^lumenet-lan=[0-9A-F]{64};", setCookie);
        Assert.DoesNotContain(server.Lan.Password, setCookie);

        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/health")).StatusCode);
        await using HubClient ui = await server.ConnectAsync(cookies);
        Assert.Equal(JsonValueKind.Object, (await ui.InvokeAsync("Hello")).ValueKind);
    }

    [Fact]
    public async Task WrongPasswordsAreTurnedAwayAndTooManyStopTheGuessing()
    {
        await using TestServer server = await TestServer.StartAsync();
        server.Lan.Switch(true);
        server.Pc.LoopbackIsAnotherDevice = true;
        using HttpClient phone = server.CreateBrowser(new CookieContainer());
        string wrong = server.Lan.Password == "AAAAA" ? "BBBBB" : "AAAAA";

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            using HttpResponseMessage answer = await LogInAsync(phone, wrong, back: "/");
            Assert.Equal(HttpStatusCode.Unauthorized, answer.StatusCode);
            Assert.Contains("That is not the password.", await answer.Content.ReadAsStringAsync());
            Assert.False(answer.Headers.Contains("Set-Cookie"));
        }

        // The sixth try within the minute is not even looked at, the right password neither.
        using HttpResponseMessage blocked = await LogInAsync(phone, server.Lan.Password, back: "/");
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Contains("Too many wrong passwords.", await blocked.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WrongPasswordsFromManyAddressesAreCountedTogetherToo()
    {
        await using TestServer server = await TestServer.StartAsync();
        string wrong = server.Lan.Password == "AAAAA" ? "BBBBB" : "AAAAA";

        // A guesser that takes a new address for every try, which is easy with IPv6.
        for (int device = 1; device <= 20; device++)
            Assert.Equal(LanAccess.Answer.Wrong, server.Lan.LogIn(IPAddress.Parse($"fd00::{device}"), wrong));

        Assert.Equal(LanAccess.Answer.TooMany, server.Lan.LogIn(IPAddress.Parse("fd00::99"), server.Lan.Password));
    }

    [Fact]
    public async Task CookieCannotBeGuessedFromThePassword()
    {
        await using TestServer server = await TestServer.StartAsync();
        server.Lan.Switch(true);
        server.Pc.LoopbackIsAnotherDevice = true;
        using HttpClient phone = server.CreateHttpClient();

        // A cookie can be sent without limit, so it must not be the password, which is short.
        foreach ((string cookie, HttpStatusCode expected) in new[]
        {
            (server.Lan.Password, HttpStatusCode.Unauthorized),
            (server.Lan.CookieValue, HttpStatusCode.OK),
        })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
            request.Headers.Add("Cookie", $"{LanAccess.CookieName}={cookie}");
            using HttpResponseMessage response = await phone.SendAsync(request);
            Assert.Equal(expected, response.StatusCode);
        }
    }

    [Fact]
    public async Task AfterThePasswordTheDeviceOnlyGoesToAPageOfThisServer()
    {
        await using TestServer server = await TestServer.StartAsync();
        server.Lan.Switch(true);
        server.Pc.LoopbackIsAnotherDevice = true;
        using HttpClient phone = server.CreateBrowser(new CookieContainer());

        // Browsers read a backslash as a slash and drop tabs and line breaks from an address.
        string[] elsewhere =
        [
            "//evil.example/", "/\\evil.example", "/a\\..\\\\evil.example", "http://evil.example/", "",
            "/\t/evil.example", "/\n/evil.example", "/\r\n/evil.example", " //evil.example",
        ];
        foreach (string back in elsewhere)
        {
            using HttpResponseMessage answer = await LogInAsync(phone, server.Lan.Password, back);
            Assert.Equal("/", answer.Headers.Location?.OriginalString);
        }

        using HttpResponseMessage kept = await LogInAsync(phone, server.Lan.Password, "/stack?view=1");
        Assert.Equal("/stack?view=1", kept.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task AddressTheDeviceAskedForIsWrittenIntoThePageAsTextOnly()
    {
        await using TestServer server = await TestServer.StartAsync();
        server.Lan.Switch(true);
        server.Pc.LoopbackIsAnotherDevice = true;
        using HttpClient phone = server.CreateBrowser(new CookieContainer());

        string html = await (await phone.GetAsync("/%22%3E%3Cscript%3Ex")).Content.ReadAsStringAsync();

        // It stays in the form of an address, with its signs escaped, and is escaped for HTML on top.
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("\"><", html);
        Assert.Contains("""name="back" value="/%22%3E%3Cscript%3Ex">""", html);
    }

    // ---------------------------------------------------------------- switching

    [Fact]
    public async Task LanOffCutsADeviceThatIsConnected()
    {
        await using TestServer server = await TestServer.StartAsync();
        server.Lan.Switch(true);
        server.Pc.LoopbackIsAnotherDevice = true;
        var cookies = new CookieContainer();
        using HttpClient phone = server.CreateBrowser(cookies);
        await LogInAsync(phone, server.Lan.Password, back: "/");
        await using HubClient ui = await server.ConnectAsync(cookies);

        server.Lan.Switch(false);

        await Assert.ThrowsAnyAsync<Exception>(() => ui.InvokeAsync("Hello"));
        await Assert.ThrowsAsync<HttpRequestException>(() => phone.GetAsync("/api/health"));

        // Back on: the phone kept its cookie and gets in without the password.
        server.Lan.Switch(true);
        Assert.Equal(HttpStatusCode.OK, (await phone.GetAsync("/api/health")).StatusCode);
    }

    [Fact]
    public async Task OnlyThePcSwitchesLanAndOnlyItIsToldTheAddressAndThePassword()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient pc = await server.ConnectAsync();
        int port = server.Address.Port;

        Assert.Equal(
            $$"""{"local":true,"on":false,"addresses":["10.20.30.40:{{port}}"],"password":null}""",
            (await pc.InvokeAsync("GetLanAccess")).GetRawText());

        await pc.InvokeAsync("SetLanAccess", true);
        HubMessage told = await pc.NextMessageAsync();
        Assert.Equal("LanAccessChanged", told.Name);
        string password = server.Lan.Password;
        Assert.Equal(
            $$"""{"local":true,"on":true,"addresses":["10.20.30.40:{{port}}"],"password":"{{password}}"}""",
            told.Arguments[0].GetRawText());

        // A phone gets in, and learns nothing about LAN but that it is on. It cannot switch it.
        server.Pc.LoopbackIsAnotherDevice = true;
        var cookies = new CookieContainer();
        using HttpClient browser = server.CreateBrowser(cookies);
        await LogInAsync(browser, password, back: "/");
        await using HubClient phone = await server.ConnectAsync(cookies);
        Assert.Equal(
            """{"local":false,"on":true,"addresses":[],"password":null}""",
            (await phone.InvokeAsync("GetLanAccess")).GetRawText());
        var error = await Assert.ThrowsAsync<HubCallException>(() => phone.InvokeAsync("SetLanAccess", false));
        Assert.Contains("LAN can only be switched on the PC.", error.Message);
        Assert.True(server.Lan.On);
    }

    [Fact]
    public async Task NewAddressOfThePcIsToldToThePc()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient pc = await server.ConnectAsync();

        server.Pc.MoveTo(IPAddress.Parse("10.20.30.50"));

        HubMessage told = await pc.NextMessageAsync();
        Assert.Equal("LanAccessChanged", told.Name);
        Assert.Equal($"10.20.30.50:{server.Address.Port}", told.Arguments[0].GetProperty("addresses")[0].GetString());
    }

    // ---------------------------------------------------------------- the password itself

    [Fact]
    public async Task PasswordIsFiveEasyCharactersAndIsKeptForTheNextStart()
    {
        // Outside the test server's own folder, which goes when the server does.
        string file = Path.Combine(Path.GetTempPath(), $"lumenet-lan-{Guid.NewGuid():N}.txt");
        try
        {
            string password;
            string cookie;
            await using (TestServer first = await TestServer.StartAsync($"--Lan:PasswordFile={file}"))
            {
                password = first.Lan.Password;
                cookie = first.Lan.CookieValue;
                Assert.Matches("^[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{5}$", password);
                Assert.Equal([password, cookie], File.ReadAllLines(file));
            }

            // A phone that got in before gets in again, with the same cookie.
            await using (TestServer second = await TestServer.StartAsync($"--Lan:PasswordFile={file}"))
            {
                Assert.Equal(password, second.Lan.Password);
                Assert.Equal(cookie, second.Lan.CookieValue);
            }

            // A file without a valid password makes a new password and a new cookie key: every phone is out.
            File.WriteAllLines(file, ["no", cookie]);
            await using TestServer third = await TestServer.StartAsync($"--Lan:PasswordFile={file}");
            Assert.NotEqual(password, third.Lan.Password);
            Assert.NotEqual(cookie, third.Lan.CookieValue);
        }
        finally
        {
            File.Delete(file);
        }
    }

    // ---------------------------------------------------------------- the server's own page and names

    [Fact]
    public async Task PageOfTheServerMayUseTheHubUnderTheAddressOfThePcInTheNetwork()
    {
        await using TestServer server = await TestServer.StartAsync();
        int port = server.Address.Port;
        string address = $"10.20.30.40:{port}";

        // A phone loaded the page from http://10.20.30.40:port and now opens the hub from it.
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(server, Negotiate, host: address, origin: $"http://{address}"));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(server, Negotiate, host: $"pretend-pc.local:{port}", origin: $"http://pretend-pc.local:{port}"));

        // Another page under that host, another port, another address: no.
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(server, Negotiate, host: address, origin: "http://evil.example"));
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(server, Negotiate, host: address, origin: "http://10.20.30.40:9999"));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(server, Negotiate, host: $"10.20.30.41:{port}", origin: $"http://10.20.30.41:{port}"));
    }

    [Fact]
    public async Task UiIsServedFromTheFolderOfTheSetting()
    {
        DirectoryInfo ui = Directory.CreateTempSubdirectory("lumenet-ui-");
        try
        {
            File.WriteAllText(Path.Combine(ui.FullName, "index.html"), "<!doctype html><title>desk</title>");
            File.WriteAllText(Path.Combine(ui.FullName, "main-ABC.js"), "console.log('desk')");
            await using TestServer server = await TestServer.StartAsync($"--Ui:Folder={ui.FullName}");
            using HttpClient http = server.CreateHttpClient();

            Assert.Contains("<title>desk</title>", await http.GetStringAsync("/"));
            Assert.Contains("<title>desk</title>", await http.GetStringAsync("/stack"));   // an address of the UI, no file
            Assert.Equal("console.log('desk')", await http.GetStringAsync("/main-ABC.js"));
        }
        finally
        {
            ui.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task MissingFolderOfTheUiIsReportedAndNothingIsServed()
    {
        string missing = Path.Combine(Path.GetTempPath(), "lumenet-no-ui-" + Guid.NewGuid().ToString("N"));
        await using TestServer server = await TestServer.StartAsync($"--Ui:Folder={missing}");
        using HttpClient http = server.CreateHttpClient();

        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/")).StatusCode);
        Assert.Contains(server.Log.Entries, entry => entry.Message.Contains("The UI's files are not in") && entry.Message.Contains(missing));
    }

    private static Task<HttpResponseMessage> LogInAsync(HttpClient browser, string password, string back)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["password"] = password, ["back"] = back });
        return browser.PostAsync("/lan/login", form);
    }

    private static async Task<HttpStatusCode> StatusAsync(TestServer server, string path, string host, string origin)
    {
        using HttpClient http = server.CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Host = host;
        request.Headers.Add("Origin", origin);
        using HttpResponseMessage response = await http.SendAsync(request);
        return response.StatusCode;
    }
}
