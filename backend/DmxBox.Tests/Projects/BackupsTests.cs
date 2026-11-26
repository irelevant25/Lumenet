using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;

namespace DmxBox.Tests.Projects;

/// <summary>
/// Backups: everything Export takes, one ZIP each time, into a folder of the PC (one that Google Drive
/// syncs, say); the newest kept, older ones of its own deleted and nothing else; the setup in backup.json;
/// and the page on the PC that sets them and asks for one.
/// </summary>
public sealed class BackupsTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("lumenet-backups-");
    private readonly SoundStore _store;
    private DateTime _now = new(2026, 11, 26, 14, 5, 33);

    public BackupsTests()
    {
        _store = new SoundStore(Path.Combine(_folder.FullName, "sounds"));
    }

    public void Dispose()
    {
        try
        {
            _folder.Delete(recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a folder left in the temp directory is harmless
        }
    }

    private string SetupFile => Path.Combine(_folder.FullName, "pc", BackupSetupFile.FileName);

    private string Target => Path.Combine(_folder.FullName, "My Drive", "Lumenet backups");

    private Backups Make() => new(new BackupSetupFile(SetupFile, NullLogger.Instance), _store, () => _now);

    [Fact]
    public async Task ABackupHoldsEveryPartAndTheSoundsInAZipNamedAfterTheProjectAndTheTime()
    {
        Backups backups = Make();
        backups.Set(new BackupSetup(Target, OnSave: true, Keep: 30));
        SoundFileInfo tone;
        await using (FileStream wave = File.OpenRead(Wave()))
            tone = await _store.AddAsync(wave, "tone.wav", CancellationToken.None);

        BackupMade made = backups.Write(ExportText(tone.Hash), "Concert: night");

        Assert.Equal("Lumenet backup - Concert night - 2026-11-26 14.05.33.zip", made.File);
        Assert.Empty(made.Missing);
        Assert.Equal((1, made.File), (made.Backups.Count, made.Backups.Last));
        Assert.Equal([made.File], Directory.GetFiles(Target).Select(Path.GetFileName));   // whole, nothing beside
        using ZipArchive zip = ZipFile.OpenRead(Path.Combine(Target, made.File));
        Assert.Equal(
            ["lumenet-export.json", "project.json", "stage.json", "settings.json", "keyboard.json", "midi.json", "sounds/tone.wav"],
            zip.Entries.Select(entry => entry.FullName));
    }

    [Fact]
    public void TheNewestAreKeptAndNothingElseGoes()
    {
        Backups backups = Make();
        backups.Set(new BackupSetup(Target, OnSave: true, Keep: 2));
        Directory.CreateDirectory(Target);
        string[] others = ["notes.txt", "Lumenet backup - old.txt", "Concert.zip"];
        foreach (string other in others)
            File.WriteAllText(Path.Combine(Target, other), "mine");

        var made = new List<string>();
        for (int backup = 0; backup < 3; backup++)
        {
            _now = _now.AddMinutes(1);
            made.Add(backups.Write(ExportText(null), "Concert").File);
        }

        Assert.Equal(
            [made[2], made[1]],
            Directory.GetFiles(Target, Backups.Prefix + "*.zip").Select(Path.GetFileName).OrderDescending(StringComparer.Ordinal));
        Assert.All(others, other => Assert.True(File.Exists(Path.Combine(Target, other)), other));
        Assert.Equal((2, made[2]), (backups.View.Count, backups.View.Last));
    }

    [Fact]
    public void TheSetupIsKeptForTheNextStartAndOneThatIsNotPossibleIsRefused()
    {
        Backups backups = Make();
        BackupView first = backups.View;
        Assert.Equal(("", true, BackupSetup.DefaultKeep, 0, null), (first.Folder, first.OnSave, first.Keep, first.Count, first.Problem));

        backups.Set(new BackupSetup($"  {Target}  ", OnSave: false, Keep: 5));
        BackupView again = Make().View;
        Assert.Equal((Target, false, 5, 0, null), (again.Folder, again.OnSave, again.Keep, again.Count, again.Problem));

        Assert.Equal(
            "The folder of the backups has to be a whole path, like G:\\My Drive\\Lumenet backups.",
            Assert.Throws<DeskException>(() => backups.Set(new BackupSetup("My Drive", OnSave: true, Keep: 5))).Message);
        Assert.Equal(
            "A folder keeps 1 to 1000 backups.",
            Assert.Throws<DeskException>(() => backups.Set(new BackupSetup(Target, OnSave: true, Keep: 0))).Message);
        Assert.Equal((Target, 5), (Make().View.Folder, Make().View.Keep));   // the refused ones changed nothing

        // a file that cannot be read counts as none
        File.WriteAllText(SetupFile, "{ not json");
        Assert.Equal("", Make().View.Folder);
    }

    [Fact]
    public void WithoutAFolderThereIsNoBackupAndADriveThatIsNotThereIsSaid()
    {
        Backups backups = Make();
        Assert.Equal(
            "Choose the folder of the backups first.",
            Assert.Throws<DeskException>(() => backups.Write(ExportText(null), "Concert")).Message);

        char? free = FreeDriveLetter();
        if (free is null)
            return;   // every letter has a drive on this PC

        string folder = $@"{free}:\My Drive\Lumenet backups";
        Assert.Equal($@"{free}:\ is not there now: is the drive connected, and Google Drive running?", backups.Set(new BackupSetup(folder, OnSave: true, Keep: 30)).Problem);
        Assert.StartsWith(
            $"The backup cannot be written to {folder}: ",
            Assert.Throws<DeskException>(() => backups.Write(ExportText(null), "Concert")).Message);
    }

    [Fact]
    public async Task ThePageOnThePcSetsTheBackupsAndAsksForOne()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        string folder = Path.Combine(server.ProjectsFolder, "backups");

        JsonElement set = await ui.InvokeAsync("SetBackup", new BackupSetup(folder, OnSave: true, Keep: 3));
        Assert.Equal((folder, true, 3, 0), (set.GetProperty("folder").GetString(), set.GetProperty("onSave").GetBoolean(), set.GetProperty("keep").GetInt32(), set.GetProperty("count").GetInt32()));
        Assert.Equal(set.GetRawText(), (await ui.InvokeAsync("GetBackup")).GetRawText());
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(server.PasswordFile)!, BackupSetupFile.FileName)));

        using HttpClient http = server.CreateHttpClient();
        HttpResponseMessage made = await http.PostAsync("/api/backup?name=Show", new StringContent(ExportText(null)));
        Assert.True(made.IsSuccessStatusCode, await made.Content.ReadAsStringAsync());
        using JsonDocument answer = JsonDocument.Parse(await made.Content.ReadAsStringAsync());
        string file = answer.RootElement.GetProperty("file").GetString()!;
        Assert.StartsWith("Lumenet backup - Show - ", file, StringComparison.Ordinal);
        Assert.Equal(1, answer.RootElement.GetProperty("backups").GetProperty("count").GetInt32());
        Assert.True(File.Exists(Path.Combine(folder, file)));

        HttpResponseMessage bad = await http.PostAsync("/api/backup?name=Show", new StringContent("not json"));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("The file of Export is no JSON.", await bad.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // A text of Export with every part, as the page makes it; with a sound of this hash, when one is given.
    private static string ExportText(string? hash)
    {
        return JsonSerializer.Serialize(new
        {
            format = "lumenet-export",
            version = 1,
            name = "Concert",
            project = new
            {
                format = "lumenet-project",
                version = 9,
                sounds = hash is null ? [] : new object[] { new { number = 1, name = "Tone", category = "effect", file = "tone.wav", hash } },
            },
            stage = new { width = 12, depth = 8, lights = Array.Empty<object>() },
            settings = new { zoom = 1 },
            keyboard = new { shortcuts = new Dictionary<string, string> { ["stack.play"] = "Space" } },
            midi = new { devices = Array.Empty<string>(), mappings = Array.Empty<object>() },
        });
    }

    private string Wave()
    {
        string path = Path.Combine(_folder.FullName, "tone.wav");
        using var writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(48000, 1));
        float[] samples = [.. Enumerable.Range(0, 4800).Select(frame => (float)(0.5 * Math.Sin(frame * 0.0576)))];
        writer.WriteSamples(samples, 0, samples.Length);
        return path;
    }

    // A drive letter that no drive of this PC has; null when all have one.
    private static char? FreeDriveLetter()
    {
        var taken = DriveInfo.GetDrives().Select(drive => char.ToUpperInvariant(drive.Name[0])).ToHashSet();
        foreach (char letter in "QRSTUVWXYJKLMNOP")
        {
            if (!taken.Contains(letter))
                return letter;
        }

        return null;
    }
}
