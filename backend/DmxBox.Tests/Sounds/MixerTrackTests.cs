using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;

namespace DmxBox.Tests.Sounds;

/// <summary>
/// The tracks of a Soundcraft Ui mixer: the link to it (its lists, its player, its PLAYER
/// channel), the law of its faders, the tracks that play on its player one at a time, PLAYER L/R as the
/// sound section both ways (the volume of all sounds with the own level and the fader of the track that
/// plays on it, MUTE; a hand on the mixer sets them), a mixer found again, the engine that plays files
/// and tracks side by side, the finder of mixers, tracks in the project file, and the hub. Against a
/// pretend mixer: never a real one.
/// </summary>
public sealed class MixerTrackTests : IDisposable
{
    private readonly PretendUiMixer _mixer = new();
    private readonly UiMixer _link;
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("lumenet-player-");
    private double _now;

    public MixerTrackTests()
    {
        _link = new UiMixer(_mixer.Sockets, NullLogger.Instance);
    }

    public void Dispose()
    {
        _link.Dispose();
        _folder.Delete(recursive: true);
    }

    private static Sound Track(int number = 1, string file = "Intro.mp3", string playlist = "Show") =>
        new(number, "Intro", file, "") { Playlist = playlist };

    private async Task ConnectAsync()
    {
        _link.Connect("10.10.1.1");
        await Wait.UntilAsync(() => _link.IsConnected, "the link did not connect");
    }

    private Task GotAsync(params string[] lines) =>
        Wait.UntilAsync(() => _mixer.Got.Length >= lines.Length && _mixer.Got[^lines.Length..].SequenceEqual(lines), $"the mixer did not get {string.Join(", ", lines)}; it got {string.Join(", ", _mixer.Got)}");

    // These lines one after the other, also with others after them (a link found again asks for the lists after them).
    private Task GotAmongAsync(params string[] lines) =>
        Wait.UntilAsync(
            () =>
            {
                string[] got = _mixer.Got;
                return Enumerable.Range(0, Math.Max(0, got.Length - lines.Length + 1)).Any(at => got.Skip(at).Take(lines.Length).SequenceEqual(lines));
            },
            $"the mixer did not get {string.Join(", ", lines)}; it got {string.Join(", ", _mixer.Got)}");

    // Of the mixer's channels only PLAYER L/R got something ("SETD^p.0.mix", "SETD^p.1.mute" ...): no input, no master.
    private void OnlyPlayerTouched()
    {
        Assert.DoesNotContain(_mixer.Got, line => (line.StartsWith("SETD^p.", StringComparison.Ordinal) && !line.StartsWith("SETD^p.0.", StringComparison.Ordinal) && !line.StartsWith("SETD^p.1.", StringComparison.Ordinal))
            || line.StartsWith("SETD^i.", StringComparison.Ordinal) || line.StartsWith("SETD^m.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheLinkConnectsAndListsTheStickAndWorksThePlayer()
    {
        await ConnectAsync();
        Assert.Equal("ws://10.10.1.1/", _mixer.Address);
        await GotAsync("MEDIA_GET_PLISTS");   // the lists at once
        await Wait.UntilAsync(() => _mixer.HeardAlive, "no ALIVE");

        _mixer.Say("SETS^model^ui16", "SETS^firmware^1.0.7548-ui16", "SETD^var.currentState^0");
        await Wait.UntilAsync(() => _link.Name == "ui16, firmware 1.0.7548-ui16", "the mixer did not say what it is");

        Task<string[]> playlists = _link.PlaylistsAsync(CancellationToken.None);
        _mixer.Say("PLISTS^Show^~all~^~root~^");   // an empty entry at the end, as the mixer sends it
        Assert.Equal(["Show", "~all~", "~root~"], await playlists);

        Task<string[]> tracks = _link.TracksAsync("Show", CancellationToken.None);
        await GotAsync("MEDIA_GET_PLIST_TRACKS^Show");
        _mixer.Say("PLIST_TRACKS^Show^Intro.mp3^Cafe\u0301.wav^");
        Assert.Equal(["Intro.mp3", "Cafe\u0301.wav"], await tracks);   // exactly as the mixer has them
        _mixer.Forget();
        Assert.Equal(2, (await _link.TracksAsync("Show", CancellationToken.None)).Length);
        Assert.Empty(_mixer.Got);   // asked once, then kept

        _link.PlayTrack("Show", "Intro.mp3");
        await GotAsync("SETD^settings.playMode^0", "MEDIA_SWITCH_TRACK^Show^Intro.mp3", "MEDIA_PLAY");
        _link.JumpTo(0.25);
        _link.StopPlayer();
        await GotAsync("MEDIA_JUMP_TO^0.25", "MEDIA_STOP");

        _mixer.Say("SETD^var.currentState^2", "SETS^var.currentPlaylist^Show", "SETS^var.currentTrack^Intro.mp3", "SETD^var.currentLength^200", "SETD^var.currentTrackPos^0.25");
        await Wait.UntilAsync(() => _link.Player?.Track == "Intro.mp3" && _link.Player?.Part == 0.25, "the player was not told");
        Assert.Equal((true, 50.0), (_link.Player!.Value.Plays, _link.Player.Value.Position));
    }

    [Fact]
    public async Task AMixerThatIsLostIsLookedForAgainAndOneThatIsNotThereGetsNothing()
    {
        await ConnectAsync();
        _mixer.Say("SETS^model^ui16");
        await Wait.UntilAsync(() => _link.Name == "ui16", "no name");

        _mixer.Close();
        await Wait.UntilAsync(() => _link.State == UiMixer.Connecting, "the lost mixer was not noticed");
        Assert.Equal("", _link.Name);   // what it said went with it
        await Wait.UntilAsync(() => _link.IsConnected && _mixer.Connections == 2, "the mixer was not found again");

        _link.Connect("");
        Assert.Equal(UiMixer.None, _link.State);
        _mixer.Away = true;
        _link.Connect("10.10.1.2");
        Assert.Equal(UiMixer.Connecting, _link.State);
        Assert.Empty(await _link.PlaylistsAsync(CancellationToken.None));
        _mixer.Forget();
        _link.PlayTrack("Show", "Intro.mp3");
        Assert.Empty(_mixer.Got);
    }

    [Fact]
    public async Task ATrackPlaysOnThePlayerOfTheMixerUntilThePlayerStopsAndGoesWhereItIsSent()
    {
        await ConnectAsync();
        var player = new UiTrackPlayer(_link, () => _now);
        player.Start(Track());
        await GotAsync("SETD^settings.playMode^0", "MEDIA_SWITCH_TRACK^Show^Intro.mp3", "MEDIA_PLAY");
        Assert.True(player.StateOf(1).Playing);   // the player loads it
        player.Seek(1, 30);                        // not before the mixer told how long it is

        _mixer.Say("SETD^var.currentState^2", "SETS^var.currentTrack^Intro.mp3", "SETD^var.currentLength^120", "SETD^var.currentTrackPos^0.5");
        await Wait.UntilAsync(() => player.StateOf(1).Position == 60, "the position did not come");
        Assert.Equal(new SoundVoiceState(Playing: true, 60, Missing: false, 120), player.StateOf(1));
        player.Seek(1, 30);
        await GotAsync("MEDIA_JUMP_TO^0.25");
        Assert.Single(_mixer.Got, line => line.StartsWith("MEDIA_JUMP_TO", StringComparison.Ordinal));

        _now = 30;
        _mixer.Say("SETD^var.currentState^0");   // at its end
        await Wait.UntilAsync(() => !player.StateOf(1).Playing, "the track did not end");
        Assert.False(player.StateOf(1).Missing);
        await GotAsync(Mix(0));   // after the track PLAYER L/R stand at the volume of all sounds (0 dB here)
        _mixer.Forget();
        player.Stop(1);   // over already: no stop for the player
        player.Start(Track(2, "Outro.mp3"));   // from silence: PLAYER L/R at the bottom until its fader comes up
        await GotAsync("SETD^p.0.mix^0", "SETD^p.1.mix^0", "SETD^settings.playMode^0", "MEDIA_SWITCH_TRACK^Show^Outro.mp3", "MEDIA_PLAY");
        Assert.Equal(5, _mixer.Got.Length);
        OnlyPlayerTouched();
    }

    [Fact]
    public async Task PauseOfTheStackPausesThePlayerOnceAndLetsItGoOnOnce()
    {
        await ConnectAsync();
        var player = new UiTrackPlayer(_link, () => _now);
        player.Start(Track());
        await GotAsync("MEDIA_SWITCH_TRACK^Show^Intro.mp3", "MEDIA_PLAY");
        _mixer.Say("SETD^var.currentState^2", "SETS^var.currentTrack^Intro.mp3", "SETD^var.currentLength^120", "SETD^var.currentTrackPos^0.5");
        await Wait.UntilAsync(() => player.StateOf(1).Position == 60, "the position did not come");
        _mixer.Forget();

        player.Pause(1, true);
        player.Pause(1, true);   // once
        player.Pause(2, true);   // not the track that plays
        await GotAsync("MEDIA_PAUSE");
        _mixer.Say("SETD^var.currentState^3");   // the player holds a pause: the track still counts as playing
        await Wait.UntilAsync(() => _link.Player?.State == 3, "no pause");
        Assert.Equal(new SoundVoiceState(Playing: true, 60, Missing: false, 120), player.StateOf(1));

        player.Pause(1, false);
        await GotAsync("MEDIA_PAUSE", "MEDIA_PLAY");
        Assert.Equal(2, _mixer.Got.Length);
        OnlyPlayerTouched();
    }

    [Fact]
    public async Task ATrackThePlayerNeverTakesIsMissingAndAnotherTrackEndsTheOneBefore()
    {
        await ConnectAsync();
        var player = new UiTrackPlayer(_link, () => _now);
        _mixer.Say("SETD^var.currentState^0");
        await Wait.UntilAsync(() => _link.Player is not null, "no player");

        player.Start(Track(1, "Gone.mp3"));
        _now = 2;
        Assert.True(player.StateOf(1).Playing);
        _now = UiTrackPlayer.StartTime + 0.5;
        Assert.Equal(new SoundVoiceState(Playing: false, 0, Missing: true), player.StateOf(1));

        player.Start(Track(1));
        player.Start(Track(2, "Outro.mp3"));   // one at a time
        Assert.False(player.StateOf(1).Playing);
        Assert.True(player.StateOf(2).Playing);
        await GotAsync("MEDIA_SWITCH_TRACK^Show^Outro.mp3", "MEDIA_PLAY");
        _mixer.Forget();
        player.Stop(1);   // not the track that plays: nothing goes to the player
        player.Stop(2);
        await GotAsync(["MEDIA_STOP", .. Mix(0)]);   // the player stops, PLAYER L/R at the volume of all sounds
        Assert.Equal(3, _mixer.Got.Length);

        // without the mixer a track is missing at once
        player.Start(Track(3));
        Assert.True(player.StateOf(3).Playing);
        _link.Connect("");
        player.Start(Track(4));
        Assert.Equal(new SoundVoiceState(Playing: false, 0, Missing: true), player.StateOf(4));
    }

    // The value of the fader of PLAYER L/R for these decibels, as the desk sends it.
    private static string Fader(double decibels, int digits = 3) =>
        Math.Round(UiFader.ValueForDecibels(decibels), digits).ToString(System.Globalization.CultureInfo.InvariantCulture);

    // PLAYER L and R at these decibels, as the desk sends them.
    private static string[] Mix(double decibels) => [$"SETD^p.0.mix^{Fader(decibels)}", $"SETD^p.1.mix^{Fader(decibels)}"];

    // A player of the tracks over a link that has not found the mixer yet, noting what it heard of a hand
    // on the mixer.
    private (UiTrackPlayer Player, List<string> Heard) PlayerOf(UiMixer? link = null)
    {
        var player = new UiTrackPlayer(link ?? _link, () => _now);
        var heard = new List<string>();
        player.VolumeHeard += decibels =>
        {
            lock (heard)
                heard.Add(FormattableString.Invariant($"volume {decibels}"));
        };
        player.MuteHeard += mute =>
        {
            lock (heard)
                heard.Add($"mute {mute}");
        };
        return (player, heard);
    }

    // A player of the tracks over the link, which then finds the mixer with PLAYER L/R at these decibels:
    // that is the volume of all sounds.
    private async Task<(UiTrackPlayer Player, List<string> Heard)> FoundAtAsync(double decibels)
    {
        (UiTrackPlayer player, List<string> heard) = PlayerOf();
        await ConnectAsync();
        _mixer.Say($"SETD^p.0.mix^{Fader(decibels, 4)}", $"SETD^p.1.mix^{Fader(decibels, 4)}");
        string volume = FormattableString.Invariant($"volume {decibels}");
        await Wait.UntilAsync(() => Noted(heard).Contains(volume), "PLAYER did not come");
        return (player, heard);
    }

    private static string[] Noted(List<string> heard)
    {
        lock (heard)
            return [.. heard];
    }

    // Nothing of PLAYER L/R went to the mixer.
    private void NothingOfPlayerSent()
    {
        Assert.DoesNotContain(_mixer.Got, line => line.StartsWith("SETD^p.", StringComparison.Ordinal));
    }

    [Fact]
    public void PlayerStandsAtTheVolumeWithTheOwnLevelOfTheTrackAndItsFaderOnIt()
    {
        // the law of the mixer's faders: 0 dB at three quarters, +10 dB at the top
        Assert.Equal(0, UiFader.GainAt(0));
        Assert.InRange(UiFader.GainAt(0.7647), 0.99, 1.01);
        Assert.InRange(UiFader.DecibelsAt(1), 9.9, 10.1);
        Assert.Equal(UiFader.Silence, UiFader.DecibelsAt(0));
        Assert.InRange(UiFader.DecibelsAt(UiFader.ValueForDecibels(-20)), -20.01, -19.99);
        Assert.Equal(0, UiFader.ValueForDecibels(-120));

        // the volume of all sounds at -20 dB (a tenth of the gain): so while no track plays (0 dB on it);
        // a track at -6 dB: -26 dB; its fader at half: 6 dB less; nothing at the bottom of any of them
        Assert.Equal(-20, UiTrackPlayer.TargetDecibels(0, 0.1, 1), 6);
        Assert.Equal(-26, UiTrackPlayer.TargetDecibels(-6, 0.1, 1), 6);
        Assert.Equal(-32.02, UiTrackPlayer.TargetDecibels(-6, 0.1, 0.5), 2);
        Assert.Equal(double.NegativeInfinity, UiTrackPlayer.TargetDecibels(-6, 0, 1));
        Assert.Equal(double.NegativeInfinity, UiTrackPlayer.TargetDecibels(-6, 0.1, 0));
        Assert.Equal(double.NegativeInfinity, UiTrackPlayer.TargetDecibels(Sound.MinDecibels, 1, 1));
    }

    [Fact]
    public async Task WhenTheMixerIsFoundPlayerAsItStandsIsTheVolumeAndMute()
    {
        (UiTrackPlayer player, List<string> heard) = PlayerOf();
        await ConnectAsync();
        _mixer.Say($"SETD^p.0.mix^{Fader(-15, 4)}", $"SETD^p.1.mix^{Fader(-15, 4)}", "SETD^p.0.mute^1", "SETD^p.1.mute^1");   // as the owner set them
        await Wait.UntilAsync(() => Noted(heard).Contains("mute True"), "PLAYER L/R did not come");
        Assert.Equal(["volume -15", "mute True"], Noted(heard));   // the desk takes them over
        _mixer.Forget();
        player.SetMaster(Sound.GainOf(-15), mute: true);          // as the desk then has them: nothing goes back
        await Task.Delay(150);
        NothingOfPlayerSent();
    }

    [Fact]
    public async Task ATrackPlaysAtTheVolumeWithItsOwnLevelAndPlayerGoesBackToTheVolumeAfter()
    {
        (UiTrackPlayer player, List<string> heard) = await FoundAtAsync(-15);   // the volume of all sounds: -15 dB

        player.Start(Track() with { Decibels = -6 });
        await GotAsync("SETD^p.0.mix^0", "SETD^p.1.mix^0", "SETD^settings.playMode^0", "MEDIA_SWITCH_TRACK^Show^Intro.mp3", "MEDIA_PLAY");
        player.SetLevel(1, 1);                                     // its fader up: the volume and its own level
        await GotAsync(Mix(-21));

        _now = 1;
        player.SetMaster(Sound.GainOf(-10), mute: false);          // the volume at -10 dB
        await GotAsync(Mix(-16));
        _now = 2;
        player.SetMaster(Sound.GainOf(-10), mute: true);           // MUTE: PLAYER L/R muted
        player.SetMaster(Sound.GainOf(-10), mute: false);          // and back
        await GotAsync("SETD^p.0.mute^1", "SETD^p.1.mute^1", "SETD^p.0.mute^0", "SETD^p.1.mute^0");

        _now = 3;
        player.Change(Track() with { Decibels = -3 });             // its own level changed while it plays
        await GotAsync(Mix(-13));
        _mixer.Forget();
        player.Change(Track(2) with { Decibels = -30 });           // another sound: nothing
        _now = 4;
        player.SetLevel(1, 0.5);                                   // its fader half way down (a fade): 6 dB less
        await GotAsync(Mix(-13 + 20 * Math.Log10(0.5)));
        Assert.Equal(2, _mixer.Got.Length);

        player.Stop(1);                                            // after the track: PLAYER L/R at the volume again
        await GotAsync(["MEDIA_STOP", .. Mix(-10)]);
        Assert.Equal(["volume -15"], Noted(heard));                // nothing of the desk's own came back as a hand
    }

    [Fact]
    public async Task AStoppedTrackKeepsPlayerAtTheBottomWhileThePlayerSoundsOn()
    {
        (UiTrackPlayer player, _) = await FoundAtAsync(-15);       // the volume: -15 dB
        player.Start(Track() with { Decibels = -6 });
        player.SetLevel(1, 0.3);                                   // a fade-out, nearly over
        await GotAsync(Mix(-21 + 20 * Math.Log10(0.3)));
        _mixer.Say("SETD^var.currentState^2", "SETS^var.currentTrack^Intro.mp3");
        await Wait.UntilAsync(() => _link.Player?.Plays == true, "the player did not play");

        // the fade is over and the desk stops the track: PLAYER L/R to the bottom first, then the player
        // stops; while the mixer says its player still plays, PLAYER L/R stay at the bottom
        _mixer.Forget();
        var stopped = System.Diagnostics.Stopwatch.StartNew();
        player.Stop(1);
        await GotAsync("SETD^p.0.mix^0", "SETD^p.1.mix^0", "MEDIA_STOP");
        await Task.Delay(UiTrackPlayer.QuietTime + TimeSpan.FromMilliseconds(300));
        Assert.Equal(3, _mixer.Got.Length);
        _mixer.Say("SETD^var.currentState^0");                    // quiet now: PLAYER L/R back at the volume
        await GotAsync(Mix(-15));
        Assert.True(stopped.Elapsed >= UiTrackPlayer.QuietTime);

        // a track stopped at once (Next) and the next one started at once: the next one gets its level
        // only when the mixer says it plays that one, so nothing of the one before is heard at it
        player.Start(Track() with { Decibels = -6 });
        player.SetLevel(1, 1);
        await GotAsync(Mix(-21));
        _mixer.Forget();
        player.Stop(1);
        player.Start(Track(2, "Outro.mp3") with { Decibels = -6 });
        player.SetLevel(2, 1);
        await GotAsync("MEDIA_SWITCH_TRACK^Show^Outro.mp3", "MEDIA_PLAY");
        await Task.Delay(200);
        Assert.DoesNotContain(_mixer.Got, line => line.StartsWith("SETD^p.0.mix^", StringComparison.Ordinal) && line != "SETD^p.0.mix^0");
        _mixer.Say("SETD^var.currentState^2", "SETS^var.currentTrack^Outro.mp3");
        await Wait.UntilAsync(() => player.StateOf(2).Playing && _mixer.Got.Length >= 2 && _mixer.Got[^2..].SequenceEqual(Mix(-21)), "the next track did not get its level");
    }

    [Fact]
    public async Task AHandOnPlayerSetsTheVolumeAndMute()
    {
        (UiTrackPlayer player, List<string> heard) = await FoundAtAsync(-15);

        _mixer.Say($"SETD^p.0.mix^{Fader(-25, 4)}");             // a hand while no track plays: the volume
        await Wait.UntilAsync(() => Noted(heard).Contains("volume -25"), "the hand was not heard");
        player.Start(Track() with { Decibels = -6 });
        player.SetLevel(1, 1);
        await GotAsync(Mix(-31));

        _mixer.Forget();
        _mixer.Say($"SETD^p.1.mix^{Fader(-36, 4)}");             // a hand on PLAYER R while the track plays:
        await Wait.UntilAsync(() => Noted(heard).Contains("volume -30"), "the hand on R was not heard");   // what is left after its level
        _mixer.Say("SETD^p.0.mute^1");                             // a hand mutes PLAYER L: MUTE
        await Wait.UntilAsync(() => Noted(heard).Contains("mute True"), "the mute was not heard");
        player.SetMaster(Sound.GainOf(-30), mute: true);          // the desk takes them: nothing goes back
        player.SetLevel(1, 1);
        await Task.Delay(150);
        NothingOfPlayerSent();

        _now = 1;
        player.SetLevel(1, 0.5);                                   // the desk moves PLAYER on from the volume the hand set
        await GotAsync(Mix(-36 + 20 * Math.Log10(0.5)));
        _mixer.Say("SETD^p.0.mute^0", "SETD^p.1.mute^0");          // a hand unmutes them
        await Wait.UntilAsync(() => Noted(heard).Contains("mute False"), "the unmute was not heard");

        player.SetLevel(1, 0);                                     // the track at nothing: a hand says nothing of the volume
        int count = Noted(heard).Length;
        _mixer.Say($"SETD^p.0.mix^{Fader(-5, 4)}");
        await Task.Delay(150);
        Assert.Equal(count, Noted(heard).Length);
    }

    [Fact]
    public async Task AMixerFoundAgainGetsWhatTheDeskWantsAndWhatItTellsAtOnceIsNoHand()
    {
        using var link = new UiMixer(_mixer.Sockets, NullLogger.Instance, TimeSpan.FromMilliseconds(600));
        (UiTrackPlayer player, List<string> heard) = PlayerOf(link);
        link.Connect("10.10.1.1");
        await Wait.UntilAsync(() => link.IsConnected, "the link did not connect");
        _mixer.Say($"SETD^p.0.mix^{Fader(-15, 4)}");
        await Wait.UntilAsync(() => Noted(heard).Contains("volume -15"), "PLAYER did not come");
        player.Start(Track() with { Decibels = -6 });
        player.SetLevel(1, 1);
        await GotAsync(Mix(-21));

        _mixer.Close();                                            // lost
        await Wait.UntilAsync(() => link.State == UiMixer.Connecting, "the lost mixer was not noticed");
        _mixer.Forget();
        player.SetMaster(Sound.GainOf(-10), mute: false);          // meanwhile the desk's volume changes
        await Wait.UntilAsync(() => link.IsConnected && _mixer.Connections == 2, "the mixer was not found again");
        await GotAmongAsync(Mix(-16));                             // found again: what the desk wants goes to it

        _mixer.Say($"SETD^p.0.mix^{Fader(-21, 4)}", "SETD^p.0.mute^1");   // its state as it was: no hand
        await Task.Delay(150);
        Assert.Equal(["volume -15"], Noted(heard));
        await Task.Delay(600);
        _mixer.Say($"SETD^p.0.mix^{Fader(-26, 4)}");             // a moment later its word is a hand's again
        await Wait.UntilAsync(() => Noted(heard).Contains("volume -20"), "the hand was not heard");
    }

    [Fact]
    public async Task ALostMixerPlaysOnAndTheEngineTellsOfAHand()
    {
        string setupFile = Path.Combine(_folder.FullName, SoundEngine.SetupFileName);
        var store = new SoundStore(Path.Combine(_folder.FullName, "store"));
        using var engine = new SoundEngine(store, setupFile, devices: false, NullLogger.Instance, _mixer.Sockets, () => _now);
        var heard = new List<double>();
        engine.VolumeHeard += decibels =>
        {
            lock (heard)
                heard.Add(decibels);
        };
        await engine.StartAsync(CancellationToken.None);
        engine.SetSetup(new SoundSetup { Mixer = "10.10.1.1" });
        await Wait.UntilAsync(() => engine.MixerNow().State == UiMixer.Connected, "the engine did not connect");
        _mixer.Say($"SETD^p.0.mix^{Fader(-20, 4)}");             // found for the first time: PLAYER is the volume
        await Wait.UntilAsync(() => { lock (heard) return heard.Contains(-20); }, "the volume did not come");

        engine.SetMaster(Sound.GainOf(-20), mute: false);          // as the desk takes it
        engine.Start(Track() with { Decibels = -10 });
        engine.SetLevel(1, 1);
        await GotAsync(Mix(-30));
        _mixer.Say("SETD^var.currentState^2", "SETS^var.currentTrack^Intro.mp3", "SETD^var.currentLength^120", "SETD^var.currentTrackPos^0.1");
        await Wait.UntilAsync(() => engine.StateOf(1).Position > 0, "the track did not play");

        // the mixer is lost: its player plays on by itself, and the desk takes it as playing
        _mixer.Close();
        await Wait.UntilAsync(() => engine.MixerNow().State == UiMixer.Connecting, "the lost mixer was not noticed");
        _now = 100;
        Assert.True(engine.StateOf(1).Playing);

        // back: the mixer gets what the desk wants, and after the track PLAYER stands at the volume
        _mixer.Forget();
        await Wait.UntilAsync(() => engine.MixerNow().State == UiMixer.Connected, "the mixer was not found again");
        await GotAmongAsync(Mix(-30));
        engine.Stop(1);
        await GotAsync(["MEDIA_STOP", .. Mix(-20)]);
        await engine.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void AVolumeOrMuteSetOnTheMixerIsTheDesksAndThePagesHearOfIt()
    {
        var player = new PretendSoundPlayer();
        var desk = new Desk(new ManualTime());
        desk.UseSoundPlayer(player);
        Assert.Equal((Desk.DefaultSoundDecibels, false), desk.SoundMaster);   // -20 dB at first
        var told = new List<string>();
        desk.SoundMasterHeard += (decibels, mute) => told.Add(FormattableString.Invariant($"{decibels} {mute}"));

        player.Hear(volume: -12.34);
        player.Hear(mute: true);
        Assert.Equal(["-12.3 False", "-12.3 True"], told);
        Assert.Equal((-12.3, true), desk.SoundMaster);
        Assert.StartsWith("master 0.24", player.Calls[^1], StringComparison.Ordinal);   // the engine has it too
        Assert.EndsWith("True", player.Calls[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void TheTracksOfAPlaylistBecomeSoundsInTheFreeNumbers()
    {
        var desk = new Desk(new ManualTime());
        desk.SetSound(new Sound(1, "Rain", "rain.wav", "0123456789abcdef0123456789abcdef"));
        desk.SetSound(Track(3, "Intro.mp3"));

        int[] added = desk.AddTracks("Show", ["Intro.mp3", "Cafe\u0301 night.mp3", "A very long name of a track indeed.wav"]);
        Assert.Equal([2, 4], added);   // Intro was a sound already; 1 and 3 are taken
        Sound night = desk.GetSounds().Single(sound => sound.Number == 2);
        Assert.Equal(("Caf\u00e9 night", "Show", "Cafe\u0301 night.mp3"), (night.Name, night.Playlist, night.File));
        Assert.Equal(Sound.MaxNameLength, desk.GetSounds().Single(sound => sound.Number == 4).Name.Length);
        Assert.Equal(0, night.Decibels);   // as any sound: the volume of all sounds keeps it down
        Assert.Empty(desk.AddTracks("Show", ["Intro.mp3"]));   // all of them are sounds now
    }

    [Fact]
    public void ThePlaylistIsKeptByThePcAndEveryPageHearsOfIt()
    {
        string setupFile = Path.Combine(_folder.FullName, SoundEngine.SetupFileName);
        var store = new SoundStore(Path.Combine(_folder.FullName, "store"));
        using (var engine = new SoundEngine(store, setupFile, devices: false, NullLogger.Instance, _mixer.Sockets, () => _now))
        {
            var heard = new List<MixerView>();
            engine.MixerChanged += heard.Add;
            Assert.Throws<DeskException>(() => engine.SetSetup(new SoundSetup { Playlist = "a^b" }));
            engine.SetSetup(new SoundSetup { Playlist = "Show" });
            Assert.Equal("Show", engine.MixerNow().Playlist);
            Assert.Equal("Show", Assert.Single(heard).Playlist);
            Assert.Equal("Show", engine.View().Playlist);
        }

        using var again = new SoundEngine(store, setupFile, devices: false, NullLogger.Instance, _mixer.Sockets, () => _now);
        Assert.Equal("Show", again.MixerNow().Playlist);
    }

    [Fact]
    public void TheTrackOfThePlayerIsFoundAlsoInAnotherFormOfItsName()
    {
        Assert.True(UiTrackPlayer.SameTrack("Café.mp3", "Café.mp3"));
        Assert.True(UiTrackPlayer.SameTrack("Music/Intro.mp3", "Intro.mp3"));
        Assert.True(UiTrackPlayer.SameTrack("Intro", "Intro.mp3"));
        Assert.False(UiTrackPlayer.SameTrack("Intro.wav", "Intro.mp3"));
        Assert.False(UiTrackPlayer.SameTrack("", "Intro.mp3"));
    }

    [Fact]
    public async Task FilesPlayOnThePcAndTracksOnTheMixerSideBySide()
    {
        string setupFile = Path.Combine(_folder.FullName, SoundEngine.SetupFileName);
        var store = new SoundStore(Path.Combine(_folder.FullName, "store"));
        string wave = Path.Combine(_folder.FullName, "tone.wav");
        using (var writer = new WaveFileWriter(wave, WaveFormat.CreateIeeeFloatWaveFormat(48000, 1)))
            writer.WriteSamples([.. Enumerable.Range(0, 48000).Select(frame => (float)(0.5 * Math.Sin(frame * 0.06)))], 0, 48000);
        SoundFileInfo info;
        using (FileStream file = File.OpenRead(wave))
            info = store.Add(file, "tone.wav");

        using var engine = new SoundEngine(store, setupFile, devices: false, NullLogger.Instance, _mixer.Sockets, () => _now);
        await engine.StartAsync(CancellationToken.None);

        // no mixer: a track is quiet, a file plays
        engine.Start(Track());
        Assert.Equal(new SoundVoiceState(Playing: false, 0, Missing: true), engine.StateOf(1));
        engine.Start(new Sound(2, "Tone", "tone.wav", info.Hash) { Seconds = info.Seconds });
        Assert.True(engine.StateOf(2).Playing);

        // the mixer: the track plays on its player, and the file plays on beside it
        Assert.Throws<DeskException>(() => engine.SetSetup(new SoundSetup { Mixer = "http://10.10.1.1/x" }));
        SoundOutputView view = engine.SetSetup(new SoundSetup { Mixer = " 10.10.1.1 " });
        Assert.Equal("10.10.1.1", view.Mixer);
        await Wait.UntilAsync(() => engine.MixerNow().State == UiMixer.Connected, "the engine did not connect");
        Assert.Equal("connected", engine.View().MixerState);
        engine.Start(Track());
        await GotAsync("SETD^settings.playMode^0", "MEDIA_SWITCH_TRACK^Show^Intro.mp3", "MEDIA_PLAY");
        Assert.True(engine.StateOf(1).Playing);
        Assert.True(engine.StateOf(2).Playing);

        string kept = File.ReadAllText(setupFile);
        Assert.Contains("\"mixer\": \"10.10.1.1\"", kept, StringComparison.Ordinal);
        Assert.DoesNotContain("playerMode", kept, StringComparison.Ordinal);

        engine.Stop(2);   // the file goes, and is let go of, before the test's folder does
        await Wait.UntilAsync(() => !engine.StateOf(2).Playing, "the file did not stop");
        await Task.Delay(200);
        await engine.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TheFinderAsksTheAddressesOfThePcsNetworksAndKeepsTheMixersThatAnswer()
    {
        // the addresses of a network of 254 around the PC's, and of a bigger one, the 254 of its 24 bits
        IPAddress[] small = [.. UiMixerFinder.Around(IPAddress.Parse("192.168.7.23"), 24)];
        Assert.Equal((254, "192.168.7.1", "192.168.7.254"), (small.Length, small[0].ToString(), small[^1].ToString()));
        IPAddress[] big = [.. UiMixerFinder.Around(IPAddress.Parse("10.1.2.3"), 16)];
        Assert.Equal((254, "10.1.2.1"), (big.Length, big[0].ToString()));
        Assert.Equal(["192.168.1.5", "192.168.1.6"], UiMixerFinder.Around(IPAddress.Parse("192.168.1.5"), 30).Select(address => address.ToString()));
        Assert.Contains(UiMixerFinder.OwnWifi, UiMixerFinder.Candidates());   // a mixer's own Wi-Fi

        // .4 takes no connection, .6 is a web server that is no mixer, .5 a mixer
        IPAddress[] tried = [IPAddress.Parse("10.0.0.4"), IPAddress.Parse("10.0.0.5"), IPAddress.Parse("10.0.0.6")];
        var knocked = new List<string>();
        FoundMixer[] found = await UiMixerFinder.FindAsync(
            () => new FinderSocket(),
            CancellationToken.None,
            tried,
            (address, _) =>
            {
                lock (knocked)
                    knocked.Add(address.ToString());
                return Task.FromResult(address.ToString() != "10.0.0.4");
            });
        Assert.Equal(3, knocked.Count);
        Assert.Equal([new FoundMixer("10.0.0.5", "ui16, firmware 1.0.7548")], found);
    }

    // A socket for the finder: the mixer at 10.0.0.5 tells its state as a Ui16 does, any other address
    // is no websocket.
    private sealed class FinderSocket : IUiSocket
    {
        private readonly Queue<string> _frames = new();

        public Task ConnectAsync(Uri address, CancellationToken cancel)
        {
            if (address.Host != "10.0.0.5")
                throw new WebSocketException("No websocket here.");

            _frames.Enqueue("1::");
            _frames.Enqueue("3:::SETS^model^ui16\nSETS^firmware^1.0.7548\nSETD^p.0.mix^0.5");
            return Task.CompletedTask;
        }

        public Task SendAsync(string frame, CancellationToken cancel) => Task.CompletedTask;

        public Task<string?> ReceiveAsync(CancellationToken cancel) => Task.FromResult(_frames.Count > 0 ? _frames.Dequeue() : null);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public void ADeskStartsATrackAgainWhenItsSoundTakesAnotherTrack()
    {
        var player = new PretendSoundPlayer();
        var desk = new Desk(new ManualTime());
        desk.UseSoundPlayer(player);
        desk.SetSound(Track());
        desk.SetSoundControl(1, "level", 100);
        player.Calls.Clear();

        desk.SetSound(Track() with { Decibels = -6 });   // the same track: it plays on, at its new level
        desk.SetSound(Track(1, "Outro.mp3"));            // another one: it starts
        desk.SetSound(Track(1, "Outro.mp3", "Encore"));  // the same name in another playlist: another track
        Assert.Equal(["change 1", "start 1", "start 1"], player.Calls);
    }

    [Fact]
    public void ATrackIsCheckedAndKeptInTheProjectWithoutAHash()
    {
        var desk = new Desk(new ManualTime());
        Assert.Throws<DeskException>(() => desk.SetSound(Track(1, "a^b.mp3")));
        Assert.Throws<DeskException>(() => desk.SetSound(Track(1, "Intro.mp3", "")));
        Assert.Throws<DeskException>(() => desk.SetSound(Track() with { Decibels = 3 }));
        Sound kept = desk.SetSound(Track() with { Pan = 50, Speed = 2, Echo = 40, Loop = true, FadeIn = 2, Decibels = -12.5 });
        Assert.Equal((0, 1.0, 0, false, 2.0, -12.5), (kept.Pan, kept.Speed, kept.Echo, kept.Loop, kept.FadeIn, kept.Decibels));   // the player plays it as it is, at its level

        string text = ProjectFormat.WriteText(desk.GetProject().Content);
        Assert.Contains("\"playlist\": \"Show\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"hash\"", text, StringComparison.Ordinal);

        var other = new Desk(new ManualTime());
        other.ReplaceProject(ProjectFormat.ReadText(text));
        Sound loaded = Assert.Single(other.GetSounds());
        Assert.Equal(("Show", "Intro.mp3", "", -12.5), (loaded.Playlist, loaded.File, loaded.Hash, loaded.Decibels));
    }

    [Fact]
    public async Task EveryPageHearsOfTheMixerAndCanListTheStick()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        JsonElement mixer = (await ui.InvokeAsync("Hello")).GetProperty("mixer");
        Assert.Equal(("", ""), (mixer.GetProperty("state").GetString(), mixer.GetProperty("name").GetString()));
        Assert.False(mixer.TryGetProperty("playerMode", out _));   // no player mode: each sound plays where it belongs
        Assert.Equal(0, (await ui.InvokeAsync("GetMixerPlaylists")).GetArrayLength());   // no mixer
        Assert.Equal(0, (await ui.InvokeAsync("GetMixerTracks", "Show")).GetArrayLength());

        // a track is stored without a file of the desk program
        JsonElement kept = await ui.InvokeAsync("SetSound", Track());
        Assert.Equal("Show", kept.GetProperty("playlist").GetString());

        JsonElement view = await ui.InvokeAsync("SetSoundOutput", new SoundSetup { Playlist = "Show" });
        Assert.Equal("Show", view.GetProperty("playlist").GetString());
        HubMessage message = await ui.NextMessageAsync();
        while (message.Name != "MixerChanged")
            message = await ui.NextMessageAsync();
        Assert.Equal("Show", message.Arguments[0].GetProperty("playlist").GetString());   // every page, also one elsewhere
        Assert.Equal("Show", (await ui.InvokeAsync("Hello")).GetProperty("mixer").GetProperty("playlist").GetString());
    }
}
