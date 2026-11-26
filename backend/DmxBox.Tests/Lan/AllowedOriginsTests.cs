namespace DmxBox.Tests.Lan;

/// <summary>
/// Which web pages may use the hub. A page is told by the Origin header of its requests:
/// scheme, host and port have to be on the list, exactly.
/// </summary>
public sealed class AllowedOriginsTests
{
    private readonly AllowedOrigins _origins = new(["http://localhost:5080", "http://127.0.0.1:5080"]);

    [Fact]
    public void OriginOnTheListIsAllowed()
    {
        Assert.True(_origins.Allows("http://localhost:5080"));
        Assert.True(_origins.Allows("http://127.0.0.1:5080"));
    }

    [Fact]
    public void OriginThatIsNotOnTheListIsRefused()
    {
        Assert.False(_origins.Allows("http://evil.example"));
        Assert.False(_origins.Allows("http://evil.example:5080"));
        Assert.False(_origins.Allows("http://192.168.1.20:5080"));
    }

    [Fact]
    public void RequestWithoutOriginIsAllowed()
    {
        // No Origin header: a program, not a web page in a browser.
        Assert.True(_origins.Allows(null));
    }

    [Fact]
    public void AnotherPortIsRefused()
    {
        Assert.False(_origins.Allows("http://localhost:5081"));
        Assert.False(_origins.Allows("http://localhost:4200"));
        Assert.False(_origins.Allows("http://localhost"));
    }

    [Fact]
    public void AnotherSchemeIsRefused()
    {
        Assert.False(_origins.Allows("https://localhost:5080"));
        Assert.False(_origins.Allows("ws://localhost:5080"));
    }

    [Fact]
    public void LookAlikesAreRefused()
    {
        Assert.False(_origins.Allows("http://localhost:5080.evil.example"));
        Assert.False(_origins.Allows("http://localhost.evil.example:5080"));
        Assert.False(_origins.Allows("http://evil-localhost:5080"));
        Assert.False(_origins.Allows("http://localhost:5080@evil.example"));
        Assert.False(_origins.Allows("http://evil.example/http://localhost:5080"));
        Assert.False(_origins.Allows("http://evil.example?http://localhost:5080"));
        Assert.False(_origins.Allows("http://evil.example#http://localhost:5080"));
        Assert.False(_origins.Allows("http://localhost:50800"));
    }

    [Fact]
    public void HostIsComparedWithoutRegardToCase()
    {
        Assert.True(_origins.Allows("http://LOCALHOST:5080"));
        Assert.True(_origins.Allows("http://LocalHost:5080"));
    }

    [Fact]
    public void TrailingSlashMakesNoDifference()
    {
        // In the header, and in the settings, where people tend to write one.
        var listedWithSlash = new AllowedOrigins(["http://localhost:5080/"]);

        Assert.True(_origins.Allows("http://localhost:5080/"));
        Assert.True(listedWithSlash.Allows("http://localhost:5080"));
    }

    [Fact]
    public void AnythingAfterThePortIsRefused()
    {
        Assert.False(_origins.Allows("http://localhost:5080/desk"));
        Assert.False(_origins.Allows("http://localhost:5080//"));
        Assert.False(_origins.Allows("http://localhost:5080?x=1"));
        Assert.False(_origins.Allows("http://user@localhost:5080"));
    }

    [Fact]
    public void TextThatIsNoOriginIsRefused()
    {
        Assert.False(_origins.Allows(""));
        Assert.False(_origins.Allows("null"));   // what a browser sends for a page without an origin, a local file for example
        Assert.False(_origins.Allows("localhost:5080"));
        Assert.False(_origins.Allows("http://"));
        Assert.False(_origins.Allows("http://localhost:5080,http://localhost:5080"));
    }

    [Fact]
    public void UsualPortCountsWhetherItIsWrittenOrNot()
    {
        var origins = new AllowedOrigins(["http://localhost", "https://desk.example:443"]);

        Assert.True(origins.Allows("http://localhost:80"));
        Assert.True(origins.Allows("http://localhost"));
        Assert.True(origins.Allows("https://desk.example"));
        Assert.False(origins.Allows("http://localhost:443"));
    }

    [Fact]
    public void EmptyListAllowsNoWebPage()
    {
        var origins = new AllowedOrigins([]);

        Assert.False(origins.Allows("http://localhost:5080"));
        Assert.True(origins.Allows(null));
    }

    [Fact]
    public void SettingThatIsNoOriginStopsTheServerWithAMessage()
    {
        var error = Assert.Throws<ArgumentException>(() => new AllowedOrigins(["http://localhost:5080", "localhost:4200"]));
        Assert.Contains("\"localhost:4200\" is not an origin", error.Message);

        Assert.Throws<ArgumentException>(() => new AllowedOrigins(["http://localhost:5080/desk"]));
        Assert.Throws<ArgumentException>(() => new AllowedOrigins(["*"]));
        Assert.Throws<ArgumentException>(() => new AllowedOrigins([""]));
    }
}
