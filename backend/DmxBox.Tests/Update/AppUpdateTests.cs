using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace DmxBox.Tests.Update;

/// <summary>
/// Updates of Lumenet: the newest release on GitHub (a pretend GitHub here), offered when it is newer;
/// its Lumenet.exe downloaded, checked against the size and SHA-256 of the release, and handed to the
/// program to put in its own place; a file that is not right is not taken; the desk of run.cmd only tells.
/// </summary>
public sealed class AppUpdateTests : IDisposable
{
    private const string Repository = "someone/Lumenet";
    private static readonly byte[] NewExe = Encoding.ASCII.GetBytes("the new Lumenet");
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("lumenet-update-");
    private readonly PretendGitHub _github = new();
    private readonly PretendSelf _self;

    public AppUpdateTests()
    {
        _self = new PretendSelf(Path.Combine(_folder.FullName, "Lumenet.exe"));
        _github.Answer = request => request.RequestUri!.Host == "api.github.com"
            ? Text(ReleaseJson("v0.1.45"))
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(NewExe) };
    }

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    private static long Published => DateTimeOffset.Parse("2026-11-26T14:05:33Z").ToUnixTimeMilliseconds();

    [Fact]
    public async Task ANewerReleaseIsOfferedWithItsNotesAndSizeAndGitHubIsNotAskedAgainAtOnce()
    {
        var update = new AppUpdate(new HttpClient(_github), Repository, _self, "0.1.42");

        UpdateInfo info = await update.CheckAsync(CancellationToken.None);

        Assert.Equal(new UpdateInfo("0.1.42", "0.1.45", true, "Notes of 0.1.45", Published, NewExe.Length, true, null, null), info);
        Assert.Equal("https://api.github.com/repos/someone/Lumenet/releases/latest", _github.Asked.Single().ToString());
        Assert.Equal("Lumenet/0.1.42", _github.UserAgents.Single());   // the program and its version, nothing more

        await update.CheckAsync(CancellationToken.None);
        Assert.Single(_github.Asked);
    }

    [Fact]
    public async Task TheSameReleaseOrAnOlderOneIsNoUpdate()
    {
        var update = new AppUpdate(new HttpClient(_github), Repository, _self, "0.1.45");
        UpdateInfo same = await update.CheckAsync(CancellationToken.None);
        Assert.Equal(("0.1.45", false, false), (same.Latest, same.Newer, same.CanUpdate));

        DeskException newest = await Assert.ThrowsAsync<DeskException>(() => update.InstallAsync(_ => { }, CancellationToken.None));
        Assert.Equal("Lumenet 0.1.45 is the newest.", newest.Message);
        Assert.Empty(_self.Replaced);

        Assert.True(AppUpdate.IsNewer("0.1.100", "0.1.99"));
        Assert.True(AppUpdate.IsNewer("0.2.0", "0.1.99"));
        Assert.False(AppUpdate.IsNewer("0.1.9", "0.1.10"));
        Assert.False(AppUpdate.IsNewer("latest", "0.1.10"));
    }

    [Fact]
    public async Task AnUpdateDownloadsTheFileChecksItAndHandsItToTheProgram()
    {
        var update = new AppUpdate(new HttpClient(_github), Repository, _self, "0.1.42");
        var told = new List<int>();

        string version = await update.InstallAsync(told.Add, CancellationToken.None);

        Assert.Equal("0.1.45", version);
        Assert.Equal(["the new Lumenet"], _self.Replaced);
        Assert.Equal(100, told[^1]);
        Assert.Equal(
            ["https://api.github.com/repos/someone/Lumenet/releases/latest", "https://github.com/someone/Lumenet/releases/download/v0.1.45/Lumenet.exe"],
            _github.Asked.Select(address => address.ToString()));
    }

    [Theory]
    [InlineData("digest", "The file is not the one of the release (its SHA-256 differs): it was not taken.")]
    [InlineData("longer", "The file came with 15 bytes, not 20: it was not taken.")]
    [InlineData("shorter", "The file is bigger than the 10 bytes of the release: it was not taken.")]
    public async Task AFileThatIsNotTheOneOfTheReleaseIsNotTaken(string wrong, string message)
    {
        _github.Answer = request => request.RequestUri!.Host == "api.github.com"
            ? Text(wrong switch
            {
                "digest" => ReleaseJson("v0.1.45", digest: "sha256:" + new string('0', 64)),
                "longer" => ReleaseJson("v0.1.45", size: 20),
                _ => ReleaseJson("v0.1.45", size: 10),
            })
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(NewExe) };
        var update = new AppUpdate(new HttpClient(_github), Repository, _self, "0.1.42");

        DeskException refused = await Assert.ThrowsAsync<DeskException>(() => update.InstallAsync(_ => { }, CancellationToken.None));

        Assert.Equal(message, refused.Message);
        Assert.Empty(_self.Replaced);
        Assert.False(File.Exists(_self.ExePath + ".new"));
    }

    [Fact]
    public async Task ADeskThatCannotReplaceItselfOnlyTells()
    {
        // the desk of run.cmd: git brings the new version; GitHub is asked only when it knows a repository
        var fromSource = new AppUpdate(new HttpClient(_github), null, null, "0.1.42");
        Assert.Equal(
            new UpdateInfo("0.1.42", null, false, null, null, null, false, AppUpdate.FromSource, null),
            await fromSource.CheckAsync(CancellationToken.None));
        Assert.Empty(_github.Asked);

        var told = new AppUpdate(new HttpClient(_github), Repository, null, "0.1.42");
        UpdateInfo newer = await told.CheckAsync(CancellationToken.None);
        Assert.Equal(("0.1.45", true, false, AppUpdate.FromSource), (newer.Latest, newer.Newer, newer.CanUpdate, newer.Why));
        Assert.Equal(AppUpdate.FromSource, (await Assert.ThrowsAsync<DeskException>(() => told.InstallAsync(_ => { }, CancellationToken.None))).Message);

        // a Lumenet.exe made on a PC knows no repository
        UpdateInfo madeHere = await new AppUpdate(new HttpClient(_github), "not a repository", _self, "0.1.42").CheckAsync(CancellationToken.None);
        Assert.Equal((null, false), (madeHere.Latest, madeHere.CanUpdate));
        Assert.StartsWith("This Lumenet.exe was made on a PC", madeHere.Problem, StringComparison.Ordinal);

        // a folder it may not write
        _self.CannotReplace = "Lumenet may not write in its folder.";
        UpdateInfo locked = await new AppUpdate(new HttpClient(_github), Repository, _self, "0.1.42").CheckAsync(CancellationToken.None);
        Assert.Equal((true, false, "Lumenet may not write in its folder."), (locked.Newer, locked.CanUpdate, locked.Why));
    }

    [Fact]
    public async Task TheHubOfTheDeskOfRunCmdSaysThatGitBringsTheNewVersion()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        // a stream of one, beside the page's other calls
        JsonElement info = Assert.Single(await ui.StreamResultAsync(await ui.StartStreamAsync("CheckUpdate")));

        Assert.Equal(TestServer.Version, info.GetProperty("current").GetString());
        Assert.Equal((AppUpdate.FromSource, false), (info.GetProperty("why").GetString(), info.GetProperty("canUpdate").GetBoolean()));
        Assert.Equal(JsonValueKind.Null, info.GetProperty("problem").ValueKind);
        HubCallException install = await Assert.ThrowsAsync<HubCallException>(
            async () => await ui.StreamResultAsync(await ui.StartStreamAsync("InstallUpdate")));
        Assert.Contains(AppUpdate.FromSource, install.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePageHearsHowFarTheDownloadIsAndTheVersionLumenetStartsAgainAs()
    {
        var update = new AppUpdate(new HttpClient(_github), Repository, _self, "0.1.42");
        await using TestServer server = await TestServer.StartWithAsync(builder => builder.Services.AddSingleton(update));
        await using HubClient ui = await server.ConnectAsync();

        JsonElement[] steps = await ui.StreamResultAsync(await ui.StartStreamAsync("InstallUpdate"));

        Assert.Equal(["""{"percent":100,"version":null}""", """{"percent":100,"version":"0.1.45"}"""], steps.Select(step => step.GetRawText()));
        Assert.Equal(["the new Lumenet"], _self.Replaced);
    }

    [Fact]
    public async Task AGitHubThatDoesNotAnswerIsAProblemToShow()
    {
        _github.Answer = _ => new HttpResponseMessage(HttpStatusCode.Forbidden);
        UpdateInfo refused = await new AppUpdate(new HttpClient(_github), Repository, _self, "0.1.42").CheckAsync(CancellationToken.None);
        Assert.Equal((null, false, "GitHub could not be asked: it answered 403 (Forbidden)."), (refused.Latest, refused.CanUpdate, refused.Problem));

        _github.Answer = _ => throw new HttpRequestException("No such host is known.");
        UpdateInfo offline = await new AppUpdate(new HttpClient(_github), Repository, _self, "0.1.42").CheckAsync(CancellationToken.None);
        Assert.Equal("GitHub could not be asked: No such host is known.", offline.Problem);

        _github.Answer = _ => Text("""{ "message": "Not Found" }""");
        UpdateInfo nameless = await new AppUpdate(new HttpClient(_github), Repository, _self, "0.1.42").CheckAsync(CancellationToken.None);
        Assert.Equal("GitHub could not be asked: its answer names no version.", nameless.Problem);
    }

    private static HttpResponseMessage Text(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    // A release as the API of GitHub gives it, with another file before its Lumenet.exe.
    private static string ReleaseJson(string tag, string? digest = null, long? size = null)
    {
        return JsonSerializer.Serialize(new
        {
            tag_name = tag,
            name = $"Lumenet {tag[1..]}",
            body = $"Notes of {tag[1..]}",
            published_at = "2026-11-26T14:05:33Z",
            assets = new object[]
            {
                new { name = "Other.zip", browser_download_url = $"https://github.com/{Repository}/releases/download/{tag}/Other.zip", size = 3 },
                new
                {
                    name = AppUpdate.AssetName,
                    browser_download_url = $"https://github.com/{Repository}/releases/download/{tag}/Lumenet.exe",
                    size = size ?? NewExe.Length,
                    digest = digest ?? "sha256:" + Convert.ToHexStringLower(SHA256.HashData(NewExe)),
                },
            },
        });
    }

    // GitHub as the tests answer for it: each request noted.
    private sealed class PretendGitHub : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Answer { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        public List<Uri> Asked { get; } = [];

        public List<string> UserAgents { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked.Add(request.RequestUri!);
            UserAgents.Add(request.Headers.UserAgent.ToString());
            return Task.FromResult(Answer(request));
        }
    }

    // A program that notes the files it was given instead of putting them in its place.
    private sealed class PretendSelf(string exe) : ISelfReplace
    {
        public string ExePath => exe;

        public string? CannotReplace { get; set; }

        public List<string> Replaced { get; } = [];

        public void ReplaceAndRestart(string newExe)
        {
            Replaced.Add(File.ReadAllText(newExe));
            File.Delete(newExe);
        }
    }
}
