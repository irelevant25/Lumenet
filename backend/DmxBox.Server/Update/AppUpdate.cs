using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DmxBox.Server.Update;

/// <summary>The version of Lumenet and the newest release, as the About tab of Settings shows them.</summary>
/// <param name="Current">The version that runs, "0.1.42".</param>
/// <param name="Latest">The newest release, "0.1.45"; null while it is not known.</param>
/// <param name="Newer">True when the newest release is newer than the one that runs.</param>
/// <param name="Notes">What the newest release brings, as GitHub has it (the messages of its commits); null while not known.</param>
/// <param name="Published">When it came out, in milliseconds since 1970 (UTC); null while not known.</param>
/// <param name="Size">The size of its Lumenet.exe in bytes; null while not known.</param>
/// <param name="CanUpdate">True when this Lumenet can put the newest release in its own place now.</param>
/// <param name="Why">Why it cannot update itself (the desk of run.cmd, a folder it cannot write); null when it can.</param>
/// <param name="Problem">Why the newest release is not known (no network, GitHub refused); null when it is.</param>
/// <remarks>Public only because the hub's answers are.</remarks>
public sealed record UpdateInfo(
    string Current,
    string? Latest,
    bool Newer,
    string? Notes,
    long? Published,
    long? Size,
    bool CanUpdate,
    string? Why,
    string? Problem);

/// <summary>A step of an update, as the page hears of it.</summary>
/// <param name="Percent">How far the download is, 0 to 100.</param>
/// <param name="Version">On the last step only: the version Lumenet starts again as; null before.</param>
/// <remarks>Public only because the hub's answers are.</remarks>
public sealed record UpdateStep(int Percent, string? Version);

/// <summary>
/// How the program the desk runs in puts a new file of itself in its own place and starts it:
/// Lumenet.exe has such a way; the desk of run.cmd has none (it is updated with git).
/// </summary>
public interface ISelfReplace
{
    /// <summary>The file of the program that runs; the new one goes in its place.</summary>
    string ExePath { get; }

    /// <summary>Why it cannot put a new file in its place now (a folder it may not write); null when it can.</summary>
    string? CannotReplace { get; }

    /// <summary>
    /// Puts the file at <paramref name="newExe"/> in place of the running one, starts it, and ends this
    /// one a moment later, so that its last answers still go out. When it fails, the running one stays
    /// in its place.
    /// </summary>
    /// <param name="newExe">The new file, whole and checked, in the same folder.</param>
    /// <exception cref="IOException">The files could not be moved.</exception>
    /// <exception cref="UnauthorizedAccessException">The folder may not be written.</exception>
    /// <exception cref="DeskException">The new one could not be started.</exception>
    void ReplaceAndRestart(string newExe);
}

/// <summary>
/// Updates of Lumenet: the newest release of its repository on GitHub (Update:Repository, "owner/name",
/// which a Lumenet.exe of a release carries), asked only when the About tab opens, and, on a press, its
/// Lumenet.exe downloaded, checked against the size and the SHA-256 that GitHub gives, and put in place
/// of the running one (<see cref="ISelfReplace"/>). The requests carry no more than the name and the
/// version of the program.
/// </summary>
/// <remarks>Thread-safe.</remarks>
internal sealed partial class AppUpdate
{
    /// <summary>The file of a release that is Lumenet.</summary>
    public const string AssetName = "Lumenet.exe";

    /// <summary>Why the desk of run.cmd does not update itself.</summary>
    public const string FromSource = "This desk runs from the source code: git pull and run.cmd bring the new version.";

    // How long an answer of GitHub counts before it is asked again.
    private static readonly TimeSpan Fresh = TimeSpan.FromMinutes(2);

    // How long GitHub may take to say which release is the newest.
    private static readonly TimeSpan AskLimit = TimeSpan.FromSeconds(20);

    private readonly Lock _lock = new();
    private readonly HttpClient _http;
    private readonly string? _repository;
    private readonly ISelfReplace? _self;
    private readonly string _current;
    private (Release Release, DateTime At)? _asked;
    private bool _installing;

    /// <summary>Puts the updates together.</summary>
    /// <param name="http">For GitHub: its API, and the file of a release.</param>
    /// <param name="repository">"owner/name" on GitHub; null or not of that form: no updates.</param>
    /// <param name="self">How this program replaces itself; null where it cannot (run.cmd).</param>
    /// <param name="current">The version that runs.</param>
    public AppUpdate(HttpClient http, string? repository, ISelfReplace? self, string current)
    {
        _http = http;
        _repository = repository is not null && RepositoryName().IsMatch(repository.Trim()) ? repository.Trim() : null;
        _self = self;
        _current = current;
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(AppInfo.Name, current));
    }

    /// <summary>The version that runs and the newest release.</summary>
    /// <param name="cancel">Stops it.</param>
    public async Task<UpdateInfo> CheckAsync(CancellationToken cancel)
    {
        string? why = WhyNot();
        if (_repository is null)
        {
            // The desk of run.cmd has git; a Lumenet.exe made on a PC (publish.cmd) knows no releases.
            string? problem = _self is null
                ? null
                : "This Lumenet.exe was made on a PC, not by a release on GitHub: it does not know where to look for a newer one.";
            return new UpdateInfo(_current, null, false, null, null, null, false, why, problem);
        }

        Release release;
        try
        {
            release = await LatestAsync(cancel);
        }
        catch (DeskException e)
        {
            return new UpdateInfo(_current, null, false, null, null, null, false, why, e.Message);
        }

        bool newer = IsNewer(release.Version, _current);
        string? whyNot = why ?? (release.Asset is null ? $"The release has no {AssetName}." : null);
        return new UpdateInfo(
            _current, release.Version, newer, release.Notes, release.Published, release.Asset?.Size, newer && whyNot is null, whyNot, null);
    }

    /// <summary>
    /// Downloads the newest Lumenet.exe, checks it, and puts it in place of the running one, which ends a
    /// moment after this answer: the new one starts and the pages connect to it by themselves.
    /// </summary>
    /// <param name="progress">How far the download is, in per cent.</param>
    /// <param name="cancel">Stops it.</param>
    /// <returns>The version it updates to.</returns>
    /// <exception cref="DeskException">It cannot update itself, nothing newer is out, or the file is not right.</exception>
    public async Task<string> InstallAsync(Action<int> progress, CancellationToken cancel)
    {
        if (WhyNot() is { } why)
            throw new DeskException(why);
        if (_repository is null)
            throw new DeskException("This Lumenet.exe does not know where to look for a newer one.");

        lock (_lock)
        {
            if (_installing)
                throw new DeskException("Lumenet is being updated already.");
            _installing = true;
            _asked = null;   // the newest of now, not of a while ago
        }

        try
        {
            Release release = await LatestAsync(cancel);
            if (!IsNewer(release.Version, _current))
                throw new DeskException($"Lumenet {_current} is the newest.");
            Asset asset = release.Asset ?? throw new DeskException($"The release has no {AssetName}.");

            string target = _self!.ExePath + ".new";
            await DownloadAsync(asset, target, progress, cancel);
            try
            {
                _self.ReplaceAndRestart(target);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                TryDelete(target);
                throw new DeskException($"The new Lumenet could not take the place of this one: {e.Message}");
            }
            catch (DeskException)
            {
                TryDelete(target);
                throw;
            }

            return release.Version;
        }
        finally
        {
            lock (_lock)
                _installing = false;
        }
    }

    /// <summary>True when <paramref name="latest"/> is a version after <paramref name="current"/>.</summary>
    public static bool IsNewer(string latest, string current)
    {
        return Version.TryParse(latest, out Version? a) && Version.TryParse(current, out Version? b) && a > b;
    }

    /// <summary>A release as the API of GitHub gives it ("releases/latest"), or null for an answer without a version.</summary>
    /// <param name="json">The answer.</param>
    /// <exception cref="JsonException">It is no JSON.</exception>
    public static Release? ReleaseOf(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tag_name", out JsonElement tag)
            || tag.ValueKind != JsonValueKind.String || tag.GetString() is not { Length: > 0 } name)
        {
            return null;
        }

        string version = name.TrimStart('v', 'V');
        string? notes = root.TryGetProperty("body", out JsonElement body) && body.ValueKind == JsonValueKind.String ? body.GetString() : null;
        long? published = root.TryGetProperty("published_at", out JsonElement at) && at.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(at.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset when)
            ? when.ToUnixTimeMilliseconds()
            : null;

        Asset? asset = null;
        if (root.TryGetProperty("assets", out JsonElement assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in assets.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("name", out JsonElement assetName) && assetName.ValueKind == JsonValueKind.String
                    && assetName.GetString() == AssetName
                    && item.TryGetProperty("browser_download_url", out JsonElement url) && url.ValueKind == JsonValueKind.String
                    && Uri.TryCreate(url.GetString(), UriKind.Absolute, out Uri? address) && address.Scheme == Uri.UriSchemeHttps)
                {
                    long size = item.TryGetProperty("size", out JsonElement bytes) && bytes.TryGetInt64(out long count) ? count : -1;
                    string? digest = item.TryGetProperty("digest", out JsonElement hash) && hash.ValueKind == JsonValueKind.String
                        ? hash.GetString()
                        : null;
                    asset = new Asset(address, size, digest);
                }
            }
        }

        return new Release(version, notes, published, asset);
    }

    // Why this program cannot update itself; null when it can.
    private string? WhyNot()
    {
        return _self is null ? FromSource : _self.CannotReplace;
    }

    // The newest release, from GitHub or from a recent answer.
    private async Task<Release> LatestAsync(CancellationToken cancel)
    {
        lock (_lock)
        {
            if (_asked is { } asked && DateTime.UtcNow - asked.At < Fresh)
                return asked.Release;
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        limit.CancelAfter(AskLimit);
        Release release;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{_repository}/releases/latest");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using HttpResponseMessage response = await _http.SendAsync(request, limit.Token);
            if (!response.IsSuccessStatusCode)
                throw new DeskException($"GitHub could not be asked: it answered {(int)response.StatusCode} ({response.ReasonPhrase}).");

            release = ReleaseOf(await response.Content.ReadAsStringAsync(limit.Token))
                ?? throw new DeskException("GitHub could not be asked: its answer names no version.");
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            throw new DeskException("GitHub could not be asked: it did not answer in time.");
        }
        catch (Exception e) when (e is HttpRequestException or JsonException)
        {
            throw new DeskException($"GitHub could not be asked: {e.Message}");
        }

        lock (_lock)
            _asked = (release, DateTime.UtcNow);
        return release;
    }

    // The file of the release, beside the running one, checked; a file that is not right goes.
    private async Task DownloadAsync(Asset asset, string target, Action<int> progress, CancellationToken cancel)
    {
        try
        {
            using HttpResponseMessage response = await _http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancel);
            if (!response.IsSuccessStatusCode)
                throw new DeskException($"GitHub did not give the file: it answered {(int)response.StatusCode} ({response.ReasonPhrase}).");

            long total = asset.Size > 0 ? asset.Size : response.Content.Headers.ContentLength ?? -1;
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long written = 0;
            int told = -1;
            await using (Stream from = await response.Content.ReadAsStreamAsync(cancel))
            await using (var to = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = await from.ReadAsync(buffer, cancel)) > 0)
                {
                    if (asset.Size > 0 && written + read > asset.Size)
                        throw new DeskException($"The file is bigger than the {asset.Size} bytes of the release: it was not taken.");

                    await to.WriteAsync(buffer.AsMemory(0, read), cancel);
                    sha.AppendData(buffer, 0, read);
                    written += read;
                    int percent = total > 0 ? (int)Math.Min(100, written * 100 / total) : 0;
                    if (percent != told)
                    {
                        told = percent;
                        progress(percent);
                    }
                }
            }

            if (asset.Size > 0 && written != asset.Size)
                throw new DeskException($"The file came with {written} bytes, not {asset.Size}: it was not taken.");
            if (asset.Digest is { } digest && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Convert.ToHexStringLower(sha.GetHashAndReset()), digest[7..], StringComparison.OrdinalIgnoreCase))
            {
                throw new DeskException("The file is not the one of the release (its SHA-256 differs): it was not taken.");
            }
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException or DeskException)
        {
            TryDelete(target);
            if (e is DeskException)
                throw;
            if (e is OperationCanceledException && cancel.IsCancellationRequested)
                throw new DeskException("The update stopped: the page that asked for it went away.");
            throw new DeskException($"The new Lumenet could not be downloaded: {e.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // it stays beside; the next update writes over it
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RepositoryName();

    /// <summary>A release: its version, its notes, when it came out, and its Lumenet.exe.</summary>
    public sealed record Release(string Version, string? Notes, long? Published, Asset? Asset);

    /// <summary>A file of a release: where it is, its size (-1 when not known), and its SHA-256 as "sha256:...", where GitHub gives one.</summary>
    public sealed record Asset(Uri Url, long Size, string? Digest);
}
