using System.Globalization;
using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Server.Api;

// The hub's part for the sounds: the sounds, playing them, the device of the PC and a Soundcraft Ui mixer.
internal partial class DeskHub
{
    /// <summary>Binds a sound key of the independents to a sound.</summary>
    /// <param name="index">Number of the key, 1-32.</param>
    /// <param name="sound">The sound the key starts and stops from now on, 1-64.</param>
    public async Task SetSoundIndependent(int index, int sound)
    {
        using (_history.Change($"sound key {index} of the independents"))
            _desk.SetSoundIndependent(index, sound);

        await Clients.Others.SoundIndependentChanged(index, sound);
        await TellProjectAsync();
    }

    /// <summary>The sounds of the project, in the order of their numbers.</summary>
    public Sound[] GetSounds()
    {
        return _desk.GetSounds();
    }

    /// <summary>
    /// Stores a sound under its number, new or in place of the one there, as the Sounds view changes
    /// it. Its file must be one the desk program has (POST /api/sounds brings it). The length of the
    /// file comes from the file; a name of a file that another sound of the project uses for another
    /// file gets a number ("thunder (2).mp3"), as the files lie side by side when the project is saved.
    /// Changes of one sound in quick succession are one step of the history.
    /// </summary>
    /// <param name="sound">The sound as it is to be.</param>
    /// <returns>The sound as the desk keeps it.</returns>
    public async Task<Sound> SetSound(Sound sound)
    {
        if (sound is null)
            throw new DeskException("The sound is missing.");

        Sound named = sound;
        if (sound.Playlist is null)
        {
            SoundFileInfo info = _soundFiles.InfoOf(sound.Hash?.Trim().ToLowerInvariant())
                ?? throw new DeskException($"Sound {sound.Number}: the desk program does not have its file. Add the file again.");
            named = sound with { Seconds = info.Seconds, File = UniqueFileName(sound) };
        }

        Sound kept;
        using (_history.Change($"sound {sound.Number}", $"sound {sound.Number}"))
            kept = _desk.SetSound(named);

        await TellSoundsAsync(kept.Number);
        return kept;
    }

    /// <summary>Takes a sound out of the project; it stops.</summary>
    /// <param name="number">The sound, 1-64.</param>
    public async Task DeleteSound(int number)
    {
        using (_history.Change($"taking sound {number} out"))
        {
            if (!_desk.DeleteSound(number))
                return;
        }

        await TellSoundsAsync(number);
    }

    /// <summary>Stores a copy of a sound under another number.</summary>
    /// <param name="from">The sound to copy, 1-64.</param>
    /// <param name="to">The number for the copy, 1-64.</param>
    public async Task CopySound(int from, int to)
    {
        using (_history.Change($"copying sound {from} to {to}"))
            _desk.CopySound(from, to);

        await TellSoundsAsync(to);
    }

    /// <summary>
    /// Works the playback of a sound: its key and its fader, see <see cref="Desk.SetSoundControl"/>.
    /// A fader moved goes to the other UIs, like a fader; a key to every UI.
    /// </summary>
    /// <param name="number">The sound, 1-64.</param>
    /// <param name="control">"go", "toggle" or "level".</param>
    /// <param name="value">1 or 0 for "go", per cent for "level".</param>
    /// <returns>The playback after the change.</returns>
    public async Task<SoundPlayback> SetSoundControl(int number, string control, double value)
    {
        SoundPlayback playback = _desk.SetSoundControl(number, control, value);
        await (control == "level" ? Clients.Others : Clients.All).SoundPlaybackChanged(playback);
        return playback;
    }

    /// <summary>
    /// A sound that plays goes on from another place: a double click on its wave. No part of the
    /// project: it works while the stack is live. Every UI hears of the playback.
    /// </summary>
    /// <param name="number">The sound, 1-64.</param>
    /// <param name="seconds">The place in its file, in seconds.</param>
    public async Task<SoundPlayback> SeekSound(int number, double seconds)
    {
        SoundPlayback playback = _desk.SeekSound(number, seconds);
        await Clients.Others.SoundPlaybackChanged(playback);
        return playback;
    }

    /// <summary>The master volume of the sound, -100 dB to 0 dB. It goes to the other UIs, like a fader.</summary>
    /// <param name="decibels">The volume, in decibels.</param>
    public Task SetSoundVolume(double decibels)
    {
        _desk.SetSoundVolume(decibels);
        (double kept, bool mute) = _desk.SoundMaster;
        return Clients.Others.SoundMasterChanged(kept, mute);
    }

    /// <summary>MUTE of the sound, on or off. Every UI hears of it.</summary>
    /// <param name="mute">True for on.</param>
    public Task SetSoundMute(bool mute)
    {
        _desk.SetSoundMute(mute);
        (double decibels, bool kept) = _desk.SoundMaster;
        return Clients.All.SoundMasterChanged(decibels, kept);
    }

    /// <summary>The device of the sound and the devices of the PC. Only for a page on the PC.</summary>
    public SoundOutputView GetSoundOutput()
    {
        RequireLocal(SoundDeviceOnThePc);
        return _sound.View();
    }

    /// <summary>Takes another device of the sound, other outputs, another mixer or another playlist. Only from a page on the PC.</summary>
    /// <param name="setup">The device ("" for Windows' default), the pair of outputs from 1, the device to listen on, the mixer and its playlist.</param>
    public SoundOutputView SetSoundOutput(SoundSetup setup)
    {
        RequireLocal(SoundDeviceOnThePc);
        return _sound.SetSetup(setup);   // the pages hear of it from the engine
    }

    /// <summary>
    /// The Soundcraft Ui mixers in the networks of the PC: the key Find of Settings. It takes about three
    /// seconds and changes nothing. Only from a page on the PC, as the mixer belongs to it.
    /// </summary>
    public Task<FoundMixer[]> FindMixers()
    {
        RequireLocal(SoundDeviceOnThePc);
        return _sound.FindMixersAsync(Context.ConnectionAborted);
    }

    /// <summary>
    /// LISTEN ON on or off: the key LISTEN of the sound section, from any page (the device to listen on is
    /// chosen on the PC, in Settings). Every page hears of it.
    /// </summary>
    /// <param name="listen">True: what goes out plays on the device to listen on as well.</param>
    public ListenView SetSoundListen(bool listen)
    {
        _sound.SetListen(listen);
        return _sound.ListenNow();
    }

    /// <summary>
    /// The playlists of the USB stick of the Soundcraft Ui mixer, as the mixer names them: its folders,
    /// "~all~" (every track) and "~root~" (the tracks outside the folders). Empty while the mixer does not
    /// answer. For any page: the tracks become sounds there.
    /// </summary>
    public Task<string[]> GetMixerPlaylists()
    {
        return _sound.MixerPlaylistsAsync(Context.ConnectionAborted);
    }

    /// <summary>
    /// Every track of a playlist of the mixer's USB stick that is no sound yet becomes one, in the free
    /// numbers from the lowest on, named after the track. As one step of Undo; refused while the stack is
    /// live.
    /// </summary>
    /// <param name="playlist">The playlist, as <see cref="GetMixerPlaylists"/> gave it; empty for all the tracks of the stick.</param>
    /// <returns>How many sounds came: fewer than its tracks where some were sounds already, or the numbers ran out.</returns>
    public async Task<int> AddMixerTracks(string playlist)
    {
        if (string.IsNullOrEmpty(playlist))
            playlist = AllTracks;

        string[] tracks = await _sound.MixerTracksAsync(playlist, Context.ConnectionAborted);
        if (tracks.Length == 0)
            throw new DeskException($"The mixer lists no tracks in {playlist}: is it there, and does it answer?");

        int[] numbers;
        using (_history.Change($"the tracks of {playlist}"))
            numbers = _desk.AddTracks(playlist, tracks);

        if (numbers.Length > 0)
            await TellSoundsAsync(numbers[0]);
        return numbers.Length;
    }

    /// <summary>The tracks of one playlist of the mixer's USB stick, by their names there. Empty while the mixer does not answer.</summary>
    /// <param name="playlist">The playlist, as <see cref="GetMixerPlaylists"/> gave it.</param>
    public Task<string[]> GetMixerTracks(string playlist)
    {
        return _sound.MixerTracksAsync(playlist ?? "", Context.ConnectionAborted);
    }

    private const string SoundDeviceOnThePc =
        "The device of the sound belongs to the PC the desk program runs on: only a page on that PC sets it.";

    // The playlist of every track of a mixer's stick, as the mixer names it.
    private const string AllTracks = "~all~";

    // The name a sound's file gets in the project: its own, unless another sound of the project has
    // that name for another file; then "thunder (2).mp3", "thunder (3).mp3", ...
    private string UniqueFileName(Sound sound)
    {
        string name = SoundStore.CheckedFileName(sound.File, $"Sound {sound.Number}");
        Sound[] others = [.. _desk.GetSounds().Where(other => other.Number != sound.Number && other.Hash != sound.Hash && !other.IsTrack)];
        string candidate = name;
        for (int count = 2; others.Any(other => string.Equals(other.File, candidate, StringComparison.OrdinalIgnoreCase)); count++)
            candidate = $"{Path.GetFileNameWithoutExtension(name)} ({count}){Path.GetExtension(name)}";

        return candidate;
    }

    // After a change of the sounds: every UI gets them again, and the playback of the one that
    // changed. The sounds are part of the project.
    private async Task TellSoundsAsync(int number)
    {
        await Clients.All.SoundsChanged(_desk.GetSounds());
        await Clients.All.SoundPlaybackChanged(_desk.GetSoundPlayback(number));
        await TellProjectAsync();
    }
}
