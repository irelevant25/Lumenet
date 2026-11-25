using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json;
using DmxBox.Server;
using NAudio.Wave;

namespace DmxBox.Tests;

/// <summary>
/// Sounds on the desk: the fader that starts and stops a sound, its key with the fade times, a sound
/// played to its end, a change while it plays, the master volume and MUTE, Clear and a load; the
/// project file and the folder of sounds beside it; and the hub with a file sent over HTTP.
/// </summary>
public sealed class SoundDeskTests : IDisposable
{
    private const string Hash = "0123456789abcdef0123456789abcdef";
    private readonly ManualTime _time = new();
    private readonly Desk _desk;
    private readonly PretendSoundPlayer _player = new();
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("lumenet-sounds-");

    public SoundDeskTests()
    {
        _desk = new Desk(_time);
        _desk.UseSoundPlayer(_player);
        _player.Calls.Clear();
    }

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    private static Sound Tone(int number = 1, string hash = Hash) => new(number, "Tone", "tone.wav", hash) { Seconds = 1 };

    private string Wave(string name = "tone.wav", double seconds = 1)
    {
        string path = Path.Combine(_folder.FullName, name);
        using var writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(48000, 1));
        float[] samples = [.. Enumerable.Range(0, (int)(seconds * 48000)).Select(frame => (float)(0.5 * Math.Sin(frame * 0.0576)))];
        writer.WriteSamples(samples, 0, samples.Length);
        return path;
    }

    [Fact]
    public void ASoundStartsWhenItsFaderLeavesZeroAndStopsAtZero()
    {
        _desk.SetSound(Tone());
        Assert.Empty(_player.Calls);

        SoundPlayback playing = _desk.SetSoundControl(1, "level", 50);
        Assert.Equal(["start 1"], _player.Calls);
        Assert.Equal(128 / 255.0, _player.Levels[0], 3);
        Assert.Equal((true, 128), (playing.Playing, playing.Level));

        _desk.SetSoundControl(1, "level", 100);
        Assert.Equal(["start 1"], _player.Calls);   // up further: it plays on
        Assert.Equal(1, _player.Levels[0], 3);

        _desk.SetSoundControl(1, "level", 0);
        Assert.Equal(["start 1", "stop 1"], _player.Calls);
        Assert.False(_desk.GetSoundPlayback(1).Playing);
    }

    [Fact]
    public void ASoundPlayedToItsEndTakesItsFaderDownAndItsKeyPlaysItAgain()
    {
        _desk.SetSound(Tone());
        _desk.SetSoundControl(1, "level", 100);
        _player.End(1);
        Assert.Contains(_desk.Tick(), moved => moved.Sound && moved.Number == 1 && moved.Level == 0);   // the UIs and the motors hear of it
        _desk.Tick();
        Assert.Equal(["start 1"], _player.Calls);   // over, its fader down: it does not start again
        Assert.Equal((false, 0), (_desk.GetSoundPlayback(1).Playing, _desk.GetSoundPlayback(1).Level));

        _desk.SetSoundControl(1, "toggle", 0);       // its key plays it again, from its start
        Assert.Equal(["start 1", "start 1"], _player.Calls);
        Assert.True(_desk.GetSoundPlayback(1).Playing);

        _player.End(1);
        _desk.Tick();
        _desk.SetSoundControl(1, "level", 20);       // its fader from zero starts it too
        Assert.Equal(3, _player.Calls.Count(call => call == "start 1"));
    }

    [Fact]
    public void ASoundGoesDownWithItsEndInItsFadeOutTimeAndASeekTakesThatBack()
    {
        _desk.SetSound(Tone() with { Seconds = 10, FadeOut = 4 });
        _desk.SetSoundControl(1, "level", 100);
        _player.Position[0] = 4;                     // 6 s to go: up as it is
        _desk.Tick();
        Assert.Equal(255, _desk.GetSoundPlayback(1).Level);

        _player.Position[0] = 8;                     // 2 s to go: down in those 2 s, as its key would
        _desk.Tick();
        _time.Pass(1);
        _player.Position[0] = 9;
        _desk.Tick();
        Assert.InRange(_desk.GetSoundPlayback(1).Level, 127, 128);
        Assert.True(_desk.GetSoundPlayback(1).Fading);

        SoundPlayback back = _desk.SeekSound(1, 2);  // a double click on its wave: further back, up again
        Assert.Equal(["start 1", "seek 1 2"], _player.Calls);
        Assert.Equal((255, false), (back.Level, back.Fading));

        _player.Position[0] = 9;
        _desk.Tick();
        _time.Pass(1);
        _desk.Tick();
        _time.Pass(0.01);
        _desk.Tick();
        Assert.Equal(["start 1", "seek 1 2", "stop 1"], _player.Calls);   // at zero with its end
        Assert.Throws<DeskException>(() => _desk.SeekSound(1, 3));         // only a sound that plays

        _desk.SetSound(Tone() with { Seconds = 10, FadeOut = 4, Loop = true });
        _desk.SetSoundControl(1, "level", 100);
        _player.Position[0] = 9;
        _desk.Tick();
        Assert.Equal((255, false), (_desk.GetSoundPlayback(1).Level, _desk.GetSoundPlayback(1).Fading));   // a loop has no end
    }

    [Fact]
    public void ItsKeyBringsItInAndTakesItOutInItsFadeTimes()
    {
        _desk.SetSound(Tone() with { FadeIn = 2, FadeOut = 4, Seconds = 60 });
        SoundPlayback started = _desk.SetSoundControl(1, "go", 1);
        Assert.True(started.Fading);
        _time.Pass(1);
        Assert.Contains(_desk.Tick(), moved => moved.Sound && moved.Number == 1 && moved.Level == 128);   // the UIs hear of the fader
        Assert.Equal(128 / 255.0, _player.Levels[0], 2);
        _time.Pass(1);
        _desk.Tick();
        Assert.Equal(255, _desk.GetSoundPlayback(1).Level);

        _desk.SetSoundControl(1, "toggle", 0);       // down in four seconds
        _time.Pass(2);
        _desk.Tick();
        Assert.Equal(128, _desk.GetSoundPlayback(1).Level);
        _time.Pass(2);
        _desk.Tick();
        Assert.Equal(["start 1", "stop 1"], _player.Calls);
        Assert.False(_desk.Animating);
    }

    [Fact]
    public void ASoundThatPlaysTakesAChangeOrStartsAgainWithAnotherFile()
    {
        _desk.SetSound(Tone());
        _desk.SetSoundControl(1, "level", 100);
        _desk.SetSound(Tone() with { Decibels = -6, Pitch = 3 });
        Assert.Equal(["start 1", "change 1"], _player.Calls);

        _desk.SetSound(Tone(hash: "fedcba9876543210fedcba9876543210"));
        Assert.Equal(["start 1", "change 1", "start 1"], _player.Calls);

        Assert.True(_desk.DeleteSound(1));
        Assert.Equal("stop 1", _player.Calls[^1]);
        Assert.Empty(_desk.GetSounds());
        DeskException error = Assert.Throws<DeskException>(() => _desk.SetSoundControl(1, "go", 1));
        Assert.Equal("There is no sound 1 to play.", error.Message);
    }

    [Fact]
    public void TheMasterVolumeAndMuteBelongToTheEveningAndClearStopsTheSounds()
    {
        _desk.SetSoundVolume(-20);
        _desk.SetSoundMute(true);
        Assert.Equal(["master 0.1 True"], _player.Calls[^1..]);   // -20 dB: a tenth of the gain
        Assert.Equal((-20, true), (_desk.GetState().SoundDecibels, _desk.GetState().SoundMute));
        Assert.Throws<DeskException>(() => _desk.SetSoundVolume(0.5));
        Assert.Throws<DeskException>(() => _desk.SetSoundVolume(-101));
        Assert.Throws<DeskException>(() => _desk.SetSoundVolume(double.NaN));

        _desk.SetSound(Tone());
        _desk.SetSoundControl(1, "level", 100);
        _desk.Clear();
        Assert.Equal("stop 1", _player.Calls[^1]);
        Assert.Equal(0, _desk.GetSoundPlayback(1).Level);

        // A project loaded: the faders are at zero, the master stays.
        _desk.SetSoundControl(1, "level", 100);
        _desk.ReplaceProject(_desk.GetProject().Content);
        Assert.Equal("stop 1", _player.Calls[^1]);
        Assert.Equal(-20, _desk.SoundMaster.Decibels);
    }

    [Fact]
    public void ASoundIsCheckedAndCopied()
    {
        DeskException error = Assert.Throws<DeskException>(() => _desk.SetSound(Tone() with { Number = 65 }));
        Assert.Equal("A sound has a number from 1 to 64, not 65.", error.Message);
        Assert.Throws<DeskException>(() => _desk.SetSound(Tone() with { File = "notes.txt" }));
        Assert.Throws<DeskException>(() => _desk.SetSound(Tone() with { Start = 2, End = 1 }));
        Assert.Throws<DeskException>(() => _desk.SetSound(Tone() with { Pitch = 13 }));
        Assert.Throws<DeskException>(() => _desk.SetSound(Tone() with { Decibels = 1 }));     // 0 dB, its file as it is, at most
        Assert.Throws<DeskException>(() => _desk.SetSound(Tone() with { Decibels = -100.5 }));
        Assert.Equal(-7.3, _desk.SetSound(Tone() with { Decibels = -7.26 }).Decibels);         // to a tenth of a decibel

        _desk.SetSound(Tone() with { Name = "Rain", Loop = true });
        Sound copy = _desk.CopySound(1, 5);
        Assert.Equal((5, "Rain", true), (copy.Number, copy.Name, copy.Loop));
    }

    [Fact]
    public void TheSoundKeysOfTheIndependentsAreBoundToSoundsAndKeptInTheProject()
    {
        Assert.Equal(Enumerable.Range(1, Desk.SoundIndependentCount), _desk.GetState().SoundIndependents);
        Assert.DoesNotContain("soundIndependents", ProjectFormat.WriteText(_desk.GetProject().Content), StringComparison.Ordinal);

        _desk.SetSoundIndependent(3, 12);
        Assert.Equal(12, _desk.GetState().SoundIndependents[2]);
        Assert.Throws<ArgumentOutOfRangeException>(() => _desk.SetSoundIndependent(33, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _desk.SetSoundIndependent(1, 65));
        string text = ProjectFormat.WriteText(_desk.GetProject().Content);
        Assert.Contains("\"version\": 9", text, StringComparison.Ordinal);
        Assert.Contains("\"soundIndependents\"", text, StringComparison.Ordinal);

        var other = new Desk(new ManualTime());
        other.ReplaceProject(ProjectFormat.ReadText(text));
        Assert.Equal(12, other.GetState().SoundIndependents[2]);
        other.StartNewProject();
        Assert.Equal(3, other.GetState().SoundIndependents[2]);
    }

    [Fact]
    public void AProjectWithSoundsIsVersion9AndItsFilesGoBesideIt()
    {
        var store = new SoundStore(Path.Combine(_folder.FullName, "store"));
        SoundFileInfo info;
        using (FileStream file = File.OpenRead(Wave()))
            info = store.Add(file, "tone.wav");
        Assert.InRange(info.Seconds, 0.99, 1.01);
        Assert.Equal((1, 48000), (info.Channels, info.Rate));
        Assert.Throws<DeskException>(() => store.Add(new MemoryStream([1, 2, 3]), "noise.wav"));   // no sound in it

        _desk.SetSound(new Sound(2, "Rain", "tone.wav", info.Hash) { Seconds = info.Seconds, Loop = true, Decibels = -12.5, Echo = 30 });
        string text = ProjectFormat.WriteText(_desk.GetProject().Content);
        Assert.Contains("\"version\": 9", text);
        Assert.Contains($"\"hash\": \"{info.Hash}\"", text);
        Assert.DoesNotContain("\"pan\"", text);   // what is as usual stays out
        Assert.Contains("\"decibels\": -12.5", text);

        var projects = new ProjectStore(_desk, Path.Combine(_folder.FullName, "projects"), soundFiles: store);
        projects.Save("Show");
        string beside = Path.Combine(_folder.FullName, "projects", "Show sounds", "tone.wav");
        Assert.True(File.Exists(beside));

        // On another PC: a store without the file takes it in from beside the project.
        var otherStore = new SoundStore(Path.Combine(_folder.FullName, "other"));
        var otherDesk = new Desk(new ManualTime());
        var other = new ProjectStore(otherDesk, Path.Combine(_folder.FullName, "projects"), soundFiles: otherStore);
        other.Load("Show");
        Sound loaded = Assert.Single(otherDesk.GetSounds());
        Assert.Equal((2, "Rain", true, -12.5, 30, info.Hash), (loaded.Number, loaded.Name, loaded.Loop, loaded.Decibels, loaded.Echo, loaded.Hash));
        Assert.NotNull(otherStore.PathOf(info.Hash));
    }

    [Fact]
    public void TheVolumeInPerCentOfAFileOfBeforeIsReadAsDecibels()
    {
        _desk.SetSound(Tone());
        string text = ProjectFormat.WriteText(_desk.GetProject().Content);
        Assert.DoesNotContain("\"decibels\"", text);   // 0 dB, as usual, stays out

        // 50 % was half the gain, 6 dB less; 120 % (louder than the file) is 0 dB, the most now; 0 % nothing
        int at = text.IndexOf("\"hash\"", StringComparison.Ordinal);
        foreach ((int percent, double decibels) in new[] { (50, -6.0), (120, 0.0), (0, -100.0) })
        {
            string before = text.Insert(at, $"\"volume\": {percent},\n      ");
            var other = new Desk(new ManualTime());
            other.ReplaceProject(ProjectFormat.ReadText(before));
            Assert.Equal(decibels, Assert.Single(other.GetSounds()).Decibels);
            Assert.DoesNotContain("\"volume\"", ProjectFormat.WriteText(other.GetProject().Content));
        }
    }

    [Fact]
    public async Task APageSendsTheFileAndTheHubStoresAndPlaysTheSound()
    {
        await using TestServer server = await TestServer.StartAsync();
        using HttpClient http = server.CreateHttpClient();
        using var content = new ByteArrayContent(File.ReadAllBytes(Wave()));
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        HttpResponseMessage sent = await http.PostAsync("/api/sounds?name=tone.wav", content);
        Assert.True(sent.IsSuccessStatusCode, await sent.Content.ReadAsStringAsync());
        using JsonDocument answer = JsonDocument.Parse(await sent.Content.ReadAsStringAsync());
        string hash = answer.RootElement.GetProperty("hash").GetString()!;

        HttpResponseMessage bad = await http.PostAsync("/api/sounds?name=tone.txt", new ByteArrayContent([1]));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, bad.StatusCode);

        await using HubClient ui = await server.ConnectAsync();
        JsonElement kept = await ui.InvokeAsync("SetSound", new Sound(1, "Tone", "tone.wav", hash));
        Assert.InRange(kept.GetProperty("seconds").GetDouble(), 0.99, 1.01);   // from the file, not from the page
        Assert.Equal(1, (await ui.InvokeAsync("GetSounds")).GetArrayLength());

        JsonElement playback = await ui.InvokeAsync("SetSoundControl", 1, "go", 1);
        Assert.True(playback.GetProperty("playing").GetBoolean());
        JsonElement sought = await ui.InvokeAsync("SeekSound", 1, 0.5);   // a double click on its wave
        Assert.Equal(0.5, sought.GetProperty("position").GetDouble());

        JsonElement listen = await ui.InvokeAsync("SetSoundListen", true);   // the key LISTEN, from any page
        Assert.True(listen.GetProperty("listen").GetBoolean());
        Assert.True((await ui.InvokeAsync("Hello")).GetProperty("listen").GetProperty("listen").GetBoolean());
        await ui.InvokeAsync("SetSoundListen", false);

        await ui.InvokeAsync("SetSoundIndependent", 2, 1);
        Assert.Equal(1, (await ui.InvokeAsync("Hello")).GetProperty("desk").GetProperty("soundIndependents")[1].GetInt32());

        HttpResponseMessage file = await http.GetAsync($"/api/sounds/{hash}");
        Assert.Equal("audio/wav", file.Content.Headers.ContentType?.MediaType);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await http.GetAsync("/api/sounds/ffffffffffffffffffffffffffffffff")).StatusCode);

        // the wave the Sounds view draws: its parts as Base64, how long the file is
        using (JsonDocument wave = JsonDocument.Parse(await http.GetStringAsync($"/api/sounds/{hash}/wave")))
        {
            Assert.InRange(wave.RootElement.GetProperty("seconds").GetDouble(), 0.99, 1.01);
            Assert.Equal(SoundWave.Parts, wave.RootElement.GetProperty("peaks").GetBytesFromBase64().Length);
            Assert.Equal(SoundWave.Parts, wave.RootElement.GetProperty("levels").GetBytesFromBase64().Length);
        }

        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await http.GetAsync("/api/sounds/ffffffffffffffffffffffffffffffff/wave")).StatusCode);

        await ui.InvokeAsync("SetSoundVolume", -6.5);
        await ui.InvokeAsync("SetSoundMute", true);
        JsonElement state = (await ui.InvokeAsync("Hello")).GetProperty("desk");
        Assert.Equal((-6.5, true), (state.GetProperty("soundDecibels").GetDouble(), state.GetProperty("soundMute").GetBoolean()));
        Assert.Equal("off", (await ui.InvokeAsync("GetSoundOutput")).GetProperty("state").GetString());
    }

    [Fact]
    public async Task ExportPacksTheSoundsOfItsProjectAndImportTakesThemIn()
    {
        var here = new SoundStore(Path.Combine(_folder.FullName, "here"));
        var there = new SoundStore(Path.Combine(_folder.FullName, "there"));
        SoundFileInfo info;
        await using (FileStream wave = File.OpenRead(Wave()))
            info = await here.AddAsync(wave, "tone.wav", CancellationToken.None);
        string json = ExportWithSounds(info.Hash);

        // the file of Export at the top, each file of a sound once, one the store lacks left out
        using var pack = new MemoryStream();
        Assert.Equal(["gone.mp3"], SoundPack.Write(pack, "Show: export", json, here));
        pack.Position = 0;
        using (var zip = new ZipArchive(pack, ZipArchiveMode.Read, leaveOpen: true))
            Assert.Equal(["Show export.json", "Show export sounds/tone.wav"], zip.Entries.Select(entry => entry.FullName));

        // another desk program: the files go into its store, the file of Export comes back as it was
        pack.Position = 0;
        (string read, string[] leftOut) = await SoundPack.ReadAsync(pack, there, CancellationToken.None);
        Assert.Equal(json, read);
        Assert.Empty(leftOut);
        Assert.NotNull(there.PathOf(info.Hash));

        DeskException notZip = await Assert.ThrowsAsync<DeskException>(() => SoundPack.ReadAsync(new MemoryStream([1, 2, 3]), there, CancellationToken.None));
        Assert.Equal("The file is no ZIP of Export.", notZip.Message);
        Assert.Throws<DeskException>(() => SoundPack.Write(new MemoryStream(), "Show", "not json", here));
    }

    [Fact]
    public async Task ThePagePacksAnExportWithItsSoundsAndUnpacksIt()
    {
        await using TestServer server = await TestServer.StartAsync();
        using HttpClient http = server.CreateHttpClient();
        using var content = new ByteArrayContent(File.ReadAllBytes(Wave()));
        string hash;
        using (JsonDocument answer = JsonDocument.Parse(await (await http.PostAsync("/api/sounds?name=tone.wav", content)).Content.ReadAsStringAsync()))
            hash = answer.RootElement.GetProperty("hash").GetString()!;
        string json = ExportWithSounds(hash);

        HttpResponseMessage packed = await http.PostAsync("/api/export/pack?name=Show", new StringContent(json));
        Assert.True(packed.IsSuccessStatusCode, await packed.Content.ReadAsStringAsync());
        Assert.Equal("application/zip", packed.Content.Headers.ContentType?.MediaType);
        Assert.Equal("gone.mp3", Uri.UnescapeDataString(packed.Headers.GetValues("X-Missing-Sounds").Single()));
        byte[] zip = await packed.Content.ReadAsByteArrayAsync();

        HttpResponseMessage unpacked = await http.PostAsync("/api/import/unpack", new ByteArrayContent(zip));
        Assert.True(unpacked.IsSuccessStatusCode, await unpacked.Content.ReadAsStringAsync());
        using JsonDocument got = JsonDocument.Parse(await unpacked.Content.ReadAsStringAsync());
        Assert.Equal(json, got.RootElement.GetProperty("text").GetString());
        Assert.Equal(0, got.RootElement.GetProperty("leftOut").GetArrayLength());

        HttpResponseMessage bad = await http.PostAsync("/api/import/unpack", new ByteArrayContent([1, 2, 3]));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public void ListeningOnASecondDeviceIsKeptByThePcAndChangesOnlyTheListening()
    {
        string setupFile = Path.Combine(_folder.FullName, SoundEngine.SetupFileName);
        var store = new SoundStore(Path.Combine(_folder.FullName, "store"));
        using (var engine = new SoundEngine(store, setupFile, devices: false, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance))
        {
            SoundOutputView quiet = engine.View();
            Assert.Equal((false, "", ""), (quiet.Listen, quiet.ListenDevice, quiet.ListenState));

            SoundOutputView view = engine.SetSetup(new SoundSetup { Outputs = 1, Listen = true, ListenDevice = " headphones " });
            Assert.Equal((true, "headphones"), (view.Listen, view.ListenDevice));
            Assert.Equal(SoundOutputView.Off, view.ListenState);   // a desk without devices hears nothing
        }

        // the PC keeps it, as it keeps the device of the sound
        using var again = new SoundEngine(store, setupFile, devices: false, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
        Assert.Equal((true, "headphones"), (again.View().Listen, again.View().ListenDevice));
        Assert.Contains("\"listen\": true", File.ReadAllText(setupFile), StringComparison.Ordinal);
    }

    // A file of Export whose project has sound 1 and 2 on one file, and sound 3 on a file no store has.
    private static string ExportWithSounds(string hash)
    {
        return JsonSerializer.Serialize(new
        {
            format = "lumenet-export",
            version = 1,
            name = "Show",
            project = new
            {
                format = "lumenet-project",
                version = 9,
                sounds = new object[]
                {
                    new { number = 1, name = "Tone", category = "effect", file = "tone.wav", hash },
                    new { number = 2, name = "Again", category = "music", file = "tone.wav", hash },
                    new { number = 3, name = "Gone", category = "effect", file = "gone.mp3", hash = new string('e', 32) },
                },
            },
        });
    }
}
