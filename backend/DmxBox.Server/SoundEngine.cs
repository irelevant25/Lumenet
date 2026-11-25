using System.Diagnostics;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DmxBox.Server;

/// <summary>The desk's way to the sound: it starts, levels and stops each sound, and sets the master.</summary>
internal interface ISoundPlayer
{
    /// <summary>Plays a sound from its start, in place of the sound playing under its number. Its gain follows <see cref="SetLevel"/>.</summary>
    void Start(Sound sound);

    /// <summary>A sound that plays changes, as far as it can while it plays (its speed, pitch, echo, reverb, end, loop, volume, pan).</summary>
    void Change(Sound sound);

    /// <summary>The level of a sound, 0 to 1: its fader and its steps. Its own volume comes on top.</summary>
    void SetLevel(int number, double level);

    /// <summary>A sound stops: it fades out in a moment and is gone.</summary>
    void Stop(int number);

    /// <summary>A sound that plays goes on from another place, in seconds of its file (of its track).</summary>
    void Seek(int number, double seconds);

    /// <summary>The master volume as a gain, 0 to 1 (0 dB), and MUTE.</summary>
    void SetMaster(double volume, bool mute);

    /// <summary>Whether a sound plays, and where it is in its file.</summary>
    SoundVoiceState StateOf(int number);

    /// <summary>Tells when the volume of all sounds was set elsewhere, in decibels: on a Soundcraft Ui mixer.</summary>
    event Action<double>? VolumeHeard;

    /// <summary>Tells when MUTE was set elsewhere: on a Soundcraft Ui mixer.</summary>
    event Action<bool>? MuteHeard;
}

/// <summary>No sound at all, for a desk without the sound engine (the tests of other parts): a sound plays from its start until it is stopped.</summary>
internal sealed class NoSoundPlayer : ISoundPlayer
{
    private readonly bool[] _playing = new bool[Sound.MaxSounds];

    public void Start(Sound sound) => _playing[sound.Number - 1] = true;

    public void Change(Sound sound)
    {
    }

    public void SetLevel(int number, double level)
    {
    }

    public void Stop(int number) => _playing[number - 1] = false;

    public void Seek(int number, double seconds)
    {
    }

    public void SetMaster(double volume, bool mute)
    {
    }

    public SoundVoiceState StateOf(int number) => new(_playing[number - 1], 0, false);

    // nothing to hear of
    public event Action<double>? VolumeHeard
    {
        add { }
        remove { }
    }

    public event Action<bool>? MuteHeard
    {
        add { }
        remove { }
    }
}

/// <summary>
/// The sound of the desk program: the mixer (<see cref="SoundMixer"/>) played on the device of the
/// PC chosen in Settings, or on Windows' default device; with LISTEN ON once more, in stereo, on a
/// second device, to hear what goes out (<see cref="ListenTap"/>). Beside them the tracks of the USB
/// stick of a Soundcraft Ui mixer play on the mixer's own player (<see cref="UiTrackPlayer"/>), while
/// it answers: each sound is a file or a track, and plays where it belongs. The devices, the pair of
/// outputs of the sounds, the mixer and its playlist belong to the PC, kept in sound.json beside the
/// LAN password. A device that is pulled out, and a mixer that is not there, are looked for again
/// every 2 seconds.
/// </summary>
/// <remarks>
/// Only Windows has devices here (WASAPI). Elsewhere, and with the setting "Sound:Output" at "none"
/// (the tests, a test desk beside the real one), the mixer is read in time all the same with nothing
/// to hear, so sounds still play to their ends.
/// </remarks>
internal sealed class SoundEngine : ISoundPlayer, IHostedService, IDisposable
{
    /// <summary>The name of the PC's setup file.</summary>
    public const string SetupFileName = "sound.json";

    private const int DefaultRate = 48000;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    private readonly SoundStore _store;
    private readonly string _setupFile;
    private readonly bool _devices;
    private readonly ILogger _log;
    private readonly Lock _lock = new();
    private readonly long[] _starts = new long[Sound.MaxSounds];
    private readonly SoundMixer _mixer = new(DefaultRate, 2);
    private readonly Func<IUiSocket> _sockets;
    private readonly UiMixer _ui;
    private readonly UiTrackPlayer _tracks;
    private readonly bool[] _onMixer = new bool[Sound.MaxSounds];   // the sound under this number is a track of the mixer
    private bool _started;
    private SoundSetup _setup = new();
    private ISoundOutput? _output;
    private string _openName = "";
    private string _openId = "";
    private string _state = SoundOutputView.Off;

    // The device to listen on (LISTEN ON): what goes out, once more, in stereo.
    private ISoundOutput? _listenOutput;
    private string _listenName = "";
    private string _listenId = "";
    private string _listenState = "";
    private Timer? _watch;

    /// <summary>The engine of this PC.</summary>
    /// <param name="store">The sound files.</param>
    /// <param name="setupFile">Where the PC's device and outputs are kept.</param>
    /// <param name="devices">False: play to no device at all (the tests, a test desk).</param>
    /// <param name="log">Where the engine writes what happens.</param>
    /// <param name="sockets">Makes the websocket to a Soundcraft Ui mixer: a real one, or a pretend mixer in the tests.</param>
    /// <param name="clock">The time in seconds, for the tracks of a mixer; a clock of its own when none is given.</param>
    public SoundEngine(SoundStore store, string setupFile, bool devices, ILogger log, Func<IUiSocket>? sockets = null, Func<double>? clock = null)
    {
        _store = store;
        _setupFile = setupFile;
        _devices = devices && OperatingSystem.IsWindows();
        _log = log;
        _setup = ReadSetup();
        _mixer.Route(_setup.Outputs - 1);
        _sockets = sockets ?? (() => new UiWebSocket());
        _ui = new UiMixer(_sockets, log);
        var watch = Stopwatch.StartNew();
        _tracks = new UiTrackPlayer(_ui, clock ?? (() => watch.Elapsed.TotalSeconds));
        _tracks.VolumeHeard += decibels => VolumeHeard?.Invoke(decibels);
        _tracks.MuteHeard += mute => MuteHeard?.Invoke(mute);
        _ui.Changed += OnMixerLink;
    }

    /// <summary>Tells when a hand set PLAYER L/R on the mixer: the volume of all sounds, in decibels.</summary>
    public event Action<double>? VolumeHeard;

    /// <summary>Tells when a hand muted or unmuted PLAYER L/R on the mixer: MUTE.</summary>
    public event Action<bool>? MuteHeard;

    /// <summary>Tells when the device or its state changed, for the pages on the PC.</summary>
    public event Action<SoundOutputView>? Changed;

    /// <summary>Tells when the mixer was found or lost, or another playlist was chosen, for every page.</summary>
    public event Action<MixerView>? MixerChanged;

    /// <summary>The mixer, for the tests.</summary>
    internal SoundMixer Mixer => _mixer;

    /// <summary>The link to the Soundcraft Ui mixer, for the tests.</summary>
    internal UiMixer Ui => _ui;

    /// <summary>The player of the tracks of the mixer, for the tests.</summary>
    internal UiTrackPlayer Tracks => _tracks;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Open();
        _watch = new Timer(_ => Watch(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        string mixer;
        lock (_lock)
        {
            _started = true;
            mixer = _setup.Mixer;
        }

        _ui.Connect(mixer);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _watch?.Dispose();
        _watch = null;
        lock (_lock)
        {
            _started = false;
            CloseListen();
            _output?.Dispose();
            _output = null;
        }

        _ui.Connect("");   // the mixer's player plays on by itself: it is the mixer's
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        _ui.Changed -= OnMixerLink;
        _ui.Dispose();
        _tracks.Dispose();
    }

    public void Start(Sound sound)
    {
        int number = sound.Number;
        Volatile.Write(ref _onMixer[number - 1], sound.IsTrack);
        if (sound.IsTrack)
        {
            StopVoice(number);       // a file that played under this number goes
            _tracks.Start(sound);    // missing at once without the mixer
            return;
        }

        _tracks.Stop(number);        // a track that played under this number goes
        long start = Interlocked.Increment(ref _starts[number - 1]);
        if (_store.PathOf(sound.Hash) is not { } path)
        {
            _mixer.MarkMissing(number);
            _mixer.Post(() => _mixer.Detach(number));
            return;
        }

        _mixer.MarkStarting(number);
        int rate = _mixer.WaveFormat.SampleRate;

        // Opening a file takes a moment: not on the desk's thread, nor on the output's.
        _ = Task.Run(() =>
        {
            SoundVoice voice;
            try
            {
                voice = new SoundVoice(sound, path, rate);
            }
            catch (Exception e)
            {
                _log.LogWarning("Sound {Number} ({File}) cannot be played: {Message}", number, sound.File, e.Message);
                if (Interlocked.Read(ref _starts[number - 1]) == start)
                    _mixer.MarkMissing(number);
                return;
            }

            _mixer.Post(() =>
            {
                // a stop or another start since: this one is too late
                if (Interlocked.Read(ref _starts[number - 1]) == start && _mixer.WaveFormat.SampleRate == rate)
                    _mixer.Attach(number, voice);
                else
                    voice.Dispose();
            });
        });
    }

    // A track plays as it is but for its level, which goes to PLAYER L/R.
    public void Change(Sound sound)
    {
        if (sound.IsTrack)
            _tracks.Change(sound);
        else
            _mixer.Post(() => _mixer.Apply(sound.Number, sound));
    }

    public void SetLevel(int number, double level)
    {
        _mixer.SetLevel(number, level);
        _tracks.SetLevel(number, level);
    }

    public void Stop(int number)
    {
        _tracks.Stop(number);
        StopVoice(number);
    }

    public void Seek(int number, double seconds)
    {
        if (Volatile.Read(ref _onMixer[number - 1]))
            _tracks.Seek(number, seconds);
        else
            _mixer.Post(() => _mixer.Seek(number, seconds));
    }

    public void SetMaster(double volume, bool mute)
    {
        _mixer.SetMaster(volume, mute);
        _tracks.SetMaster(volume, mute);
    }

    public SoundVoiceState StateOf(int number)
    {
        return Volatile.Read(ref _onMixer[number - 1]) ? _tracks.StateOf(number) : _mixer.StateOf(number);
    }

    /// <summary>The mixer as every page hears of it.</summary>
    public MixerView MixerNow()
    {
        string playlist;
        lock (_lock)
            playlist = _setup.Playlist;
        return new MixerView(_ui.State, _ui.Name) { Playlist = playlist };
    }

    /// <summary>The Soundcraft Ui mixers in the networks of this PC (see <see cref="UiMixerFinder"/>).</summary>
    public Task<FoundMixer[]> FindMixersAsync(CancellationToken cancel) => UiMixerFinder.FindAsync(_sockets, cancel);

    /// <summary>The playlists of the mixer's USB stick; empty without a mixer.</summary>
    public Task<string[]> MixerPlaylistsAsync(CancellationToken cancel) => _ui.PlaylistsAsync(cancel);

    /// <summary>The tracks of a playlist of the mixer's USB stick; empty without a mixer.</summary>
    public Task<string[]> MixerTracksAsync(string playlist, CancellationToken cancel) => _ui.TracksAsync(playlist, cancel);

    // The file that plays under a number stops: its voice fades out in a moment.
    private void StopVoice(int number)
    {
        Interlocked.Increment(ref _starts[number - 1]);
        _mixer.MarkStopped(number);
        _mixer.Post(() => _mixer.Detach(number));
    }

    // The mixer was found or lost (the player of the tracks sees to PLAYER L/R itself): the pages hear of it.
    private void OnMixerLink()
    {
        Changed?.Invoke(View());
        MixerChanged?.Invoke(MixerNow());
    }

    /// <summary>The device in use, the devices there are, and the pair of outputs of the sounds, for Settings.</summary>
    public SoundOutputView View()
    {
        SoundDevice[] devices = ListDevices();
        lock (_lock)
        {
            return new SoundOutputView(devices, _setup.Device, _setup.Outputs, _state, _openName, _mixer.WaveFormat.Channels)
            {
                Listen = _setup.Listen,
                ListenDevice = _setup.ListenDevice,
                ListenState = _listenState,
                ListenName = _listenName,
                Mixer = _setup.Mixer,
                Playlist = _setup.Playlist,
                MixerState = _ui.State,
                MixerName = _ui.Name,
            };
        }
    }

    /// <summary>
    /// Takes another device, other outputs, another device to listen on, another mixer or another
    /// playlist, keeps them in sound.json and opens what changed: the device of the sound only when it
    /// is another one, so that the sound goes on while only the outputs or the listening change.
    /// </summary>
    /// <param name="setup">The device ("" for Windows' default), the pair of outputs (from 1), the device to listen on, the mixer and its playlist.</param>
    /// <exception cref="DeskException">The pair is out of range, or the mixer's address is no address.</exception>
    public SoundOutputView SetSetup(SoundSetup? setup)
    {
        SoundSetup checkedSetup = (setup ?? throw new DeskException("The device of the sound is missing.")).Checked();
        bool otherDevice;
        bool otherListening;
        bool otherMixer;
        bool otherPlaylist;
        bool started;
        lock (_lock)
        {
            otherDevice = checkedSetup.Device != _setup.Device;
            otherListening = checkedSetup.Listen != _setup.Listen || checkedSetup.ListenDevice != _setup.ListenDevice;
            otherMixer = checkedSetup.Mixer != _setup.Mixer;
            otherPlaylist = checkedSetup.Playlist != _setup.Playlist;
            started = _started;
            _setup = checkedSetup;
            _mixer.Route(checkedSetup.Outputs - 1);
            WriteSetup(checkedSetup);
        }

        if (otherDevice)
        {
            Open();
        }
        else if (otherListening)
        {
            lock (_lock)
                OpenListen();
        }

        if (otherPlaylist)
            MixerChanged?.Invoke(MixerNow());   // the steps and the Sounds view offer its tracks

        if (otherMixer && started)
            _ui.Connect(checkedSetup.Mixer);   // the pages hear of it from the link

        SoundOutputView view = View();
        Changed?.Invoke(view);
        return view;
    }

    /// <summary>LISTEN ON on or off (the key LISTEN of the sound section), on the device to listen on that the PC has chosen.</summary>
    public SoundOutputView SetListen(bool listen)
    {
        SoundSetup setup;
        lock (_lock)
            setup = _setup;
        return SetSetup(setup with { Listen = listen });
    }

    /// <summary>Whether what goes out plays on the device to listen on as well, for every page.</summary>
    public ListenView ListenNow()
    {
        lock (_lock)
            return new ListenView(_setup.Listen, _listenState);
    }

    // The devices of the PC that play: their ids, names and outputs.
    private SoundDevice[] ListDevices()
    {
        if (!_devices || !OperatingSystem.IsWindows())
            return [];

        try
        {
            return WindowsSound.Devices();
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            _log.LogWarning("The sound devices cannot be listed: {Message}", e.Message);
            return [];
        }
    }

    // Opens the device of the setup (or Windows' default one, followed as it changes), or none: the
    // mixer is then read in time all the same.
    private void Open()
    {
        lock (_lock)
        {
            CloseListen();   // the device may give the mixer another rate: the listening follows it
            _output?.Dispose();
            _output = null;
            _openName = "";
            _openId = "";
            string state = SoundOutputView.Off;
            if (_devices && OperatingSystem.IsWindows())
            {
                try
                {
                    if (WindowsSound.Open(_setup.Device, _mixer) is { } opened)
                    {
                        _output = opened.Output;
                        _openName = opened.Name;
                        _openId = opened.Id;
                        state = SoundOutputView.Playing;
                    }
                    else
                    {
                        state = _setup.Device.Length > 0 ? SoundOutputView.Missing : SoundOutputView.NoDevice;
                    }
                }
                catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException)
                {
                    _log.LogWarning("The sound device cannot be opened: {Message}", e.Message);
                    state = SoundOutputView.Missing;
                }
            }

            if (_output is null)
            {
                if (_mixer.WaveFormat.Channels != 2)
                    _mixer.Reshape(DefaultRate, 2);
                _output = new TimedSoundOutput(_mixer);
            }

            _state = state;
            OpenListen();
        }

        Changed?.Invoke(View());
    }

    // Call with the lock held. Opens the device to listen on, when listening is on: a device of its
    // own, not the one of the sound (that one plays it already).
    private void OpenListen()
    {
        CloseListen();
        if (!_setup.Listen)
            return;

        string state = SoundOutputView.Off;
        if (_devices && OperatingSystem.IsWindows())
        {
            try
            {
                string id = _setup.ListenDevice.Length > 0 ? _setup.ListenDevice : WindowsSound.DefaultId();
                if (id.Length == 0)
                {
                    state = SoundOutputView.NoDevice;
                }
                else if (!WindowsSound.Has(_setup.ListenDevice))
                {
                    state = SoundOutputView.Missing;
                }
                else if (id == _openId)
                {
                    state = SoundOutputView.Same;
                }
                else
                {
                    var tap = new ListenTap(_mixer.WaveFormat.SampleRate);
                    if (WindowsSound.OpenListen(id, tap) is { } opened)
                    {
                        _listenOutput = opened.Output;
                        _listenName = opened.Name;
                        _listenId = opened.Id;
                        _mixer.Tap(tap);
                        state = SoundOutputView.Playing;
                    }
                    else
                    {
                        state = SoundOutputView.Missing;
                    }
                }
            }
            catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException)
            {
                _log.LogWarning("The sound device to listen on cannot be opened: {Message}", e.Message);
                state = SoundOutputView.Missing;
            }
        }

        _listenState = state;
    }

    // Call with the lock held. Nothing goes to a device to listen on any more.
    private void CloseListen()
    {
        _mixer.Tap(null);
        _listenOutput?.Dispose();
        _listenOutput = null;
        _listenName = "";
        _listenId = "";
        _listenState = "";
    }

    // Every 2 seconds: a device that failed (pulled out), the chosen one that came back, or Windows'
    // default device that changed (the sound follows it while no device is chosen).
    private void Watch()
    {
        if (!_devices || !OperatingSystem.IsWindows())
            return;

        try
        {
            bool reopen;
            bool listenAgain = false;
            lock (_lock)
            {
                reopen = _output is { Failed: true }
                    || (_state != SoundOutputView.Playing && WindowsSound.Has(_setup.Device))
                    || (_state == SoundOutputView.Playing && _setup.Device.Length == 0 && WindowsSound.DefaultId() != _openId);
                if (!reopen && _setup.Listen)
                {
                    // the device to listen on failed or came back, or Windows' default one is another now
                    string wanted = _setup.ListenDevice.Length > 0 ? _setup.ListenDevice : WindowsSound.DefaultId();
                    listenAgain = _listenOutput is { Failed: true }
                        || (_listenState is SoundOutputView.Missing or SoundOutputView.NoDevice && WindowsSound.Has(_setup.ListenDevice))
                        || (_listenState == SoundOutputView.Playing && wanted != _listenId)
                        || (_listenState == SoundOutputView.Same && wanted != _openId);
                    if (listenAgain)
                        OpenListen();
                }
            }

            if (reopen)
                Open();
            else if (listenAgain)
                Changed?.Invoke(View());
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            _log.LogDebug("The sound devices cannot be looked at now: {Message}", e.Message);
        }
    }

    private SoundSetup ReadSetup()
    {
        try
        {
            if (File.Exists(_setupFile))
                return (JsonSerializer.Deserialize<SoundSetup>(File.ReadAllText(_setupFile), JsonOptions) ?? new SoundSetup()).Checked();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or DeskException)
        {
            _log.LogWarning("{File} cannot be used, the sound plays on Windows' default device: {Message}", _setupFile, e.Message);
        }

        return new SoundSetup();
    }

    private void WriteSetup(SoundSetup setup)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_setupFile) ?? ".");
            File.WriteAllText(_setupFile, JsonSerializer.Serialize(setup, JsonOptions));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new DeskException($"The device of the sound could not be kept in {_setupFile}. {e.Message}");
        }
    }
}

/// <summary>Where the sound goes out.</summary>
internal interface ISoundOutput : IDisposable
{
    /// <summary>True once the device stopped with a fault: pulled out, taken by another program.</summary>
    bool Failed { get; }
}

/// <summary>The devices of Windows (WASAPI, in shared mode, so other programs can still play).</summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal static class WindowsSound
{
    /// <summary>The devices that play now: their ids, names and outputs.</summary>
    public static SoundDevice[] Devices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var devices = new List<SoundDevice>();
        foreach (MMDevice device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using AudioClient client = device.CreateAudioClient();
            devices.Add(new SoundDevice(device.ID, device.FriendlyName, client.MixFormat.Channels));
        }

        return [.. devices];
    }

    /// <summary>True when the device with this id plays now; for "" when Windows has a default device.</summary>
    public static bool Has(string id)
    {
        using var enumerator = new MMDeviceEnumerator();
        if (id.Length == 0)
            return enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        try
        {
            return enumerator.GetDevice(id).State == DeviceState.Active;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    /// <summary>The id of Windows' default device; empty when it has none.</summary>
    public static string DefaultId()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
            ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID
            : "";
    }

    /// <summary>
    /// Opens the device with this id, or for "" Windows' default device; the mixer takes the device's
    /// rate and outputs. Null when the device is not there.
    /// </summary>
    public static (ISoundOutput Output, string Name, string Id)? Open(string id, SoundMixer mixer)
    {
        using var enumerator = new MMDeviceEnumerator();
        MMDevice? device = null;
        if (id.Length == 0)
        {
            if (enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
                device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        else if (Has(id))
        {
            device = enumerator.GetDevice(id);
        }

        if (device is null)
            return null;

        WasapiPlayer player = new WasapiPlayerBuilder().WithDevice(device).WithSharedMode().WithLatency(60).Build();
        WaveFormat mix = player.DeviceMixFormat;
        if (mixer.WaveFormat.SampleRate != mix.SampleRate || mixer.WaveFormat.Channels != Math.Max(2, mix.Channels))
            mixer.Reshape(mix.SampleRate, mix.Channels);

        return (new WasapiSoundOutput(player, mixer), device.FriendlyName, device.ID);
    }

    /// <summary>
    /// Opens the device with this id to listen on: it plays what the tap gives, at its own rate, on
    /// its first two outputs. Null when the device is not there.
    /// </summary>
    public static (ISoundOutput Output, string Name, string Id)? OpenListen(string id, ListenTap tap)
    {
        if (!Has(id))
            return null;

        using var enumerator = new MMDeviceEnumerator();
        MMDevice device = enumerator.GetDevice(id);
        WasapiPlayer player = new WasapiPlayerBuilder().WithDevice(device).WithSharedMode().WithLatency(60).Build();
        WaveFormat mix = player.DeviceMixFormat;
        return (new WasapiSoundOutput(player, new ListenSource(tap, mix.SampleRate, mix.Channels)), device.FriendlyName, device.ID);
    }

    /// <summary>A device playing the mixer, or what it gives to listen to.</summary>
    private sealed class WasapiSoundOutput : ISoundOutput
    {
        private readonly WasapiPlayer _player;

        public WasapiSoundOutput(WasapiPlayer player, ISampleProvider source)
        {
            _player = player;
            _player.PlaybackStopped += (_, stopped) => Failed |= stopped.Exception is not null;
            _player.Init(source.ToWaveProvider());
            _player.Play();
        }

        public bool Failed { get; private set; }

        public void Dispose()
        {
            _player.Stop();
            (_player as IDisposable)?.Dispose();
        }
    }
}

/// <summary>No device: the mixer is read in time all the same, so that sounds play to their ends.</summary>
internal sealed class TimedSoundOutput : ISoundOutput
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread _thread;

    public TimedSoundOutput(ISampleProvider mixer)
    {
        _thread = new Thread(() => Run(mixer, _stop.Token)) { IsBackground = true, Name = "Lumenet sound (no device)" };
        _thread.Start();
    }

    public bool Failed => false;

    public void Dispose()
    {
        // the mixer may be changed right after: its reader has to be gone by then
        _stop.Cancel();
        _thread.Join(500);
    }

    private static void Run(ISampleProvider mixer, CancellationToken stop)
    {
        var clock = Stopwatch.StartNew();
        long framesDone = 0;
        float[] buffer = [];
        while (!stop.IsCancellationRequested)
        {
            WaveFormat format = mixer.WaveFormat;
            long due = (long)(clock.Elapsed.TotalSeconds * format.SampleRate);
            int frames = (int)Math.Min(due - framesDone, format.SampleRate / 10);
            if (frames > 0)
            {
                int samples = frames * format.Channels;
                if (buffer.Length < samples)
                    buffer = new float[samples];
                mixer.Read(buffer.AsSpan(0, samples));
                framesDone += frames;
            }

            Thread.Sleep(10);
        }
    }
}

/// <summary>The device of the sound and its pair of outputs, as the PC keeps them (sound.json).</summary>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record SoundSetup
{
    /// <summary>The device, by Windows' id; empty for Windows' default device.</summary>
    public string Device { get; init; } = "";

    /// <summary>The pair of outputs the sounds play on, from 1 (outputs 1 and 2).</summary>
    public int Outputs { get; init; } = 1;

    /// <summary>
    /// The pair of outputs of music, as sound.json had it before every sound was one kind: read as
    /// <see cref="Outputs"/> where that is missing, never written.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? Music { get; init; }

    /// <summary>True: what goes out plays on <see cref="ListenDevice"/> as well, to listen to it there (LISTEN ON).</summary>
    public bool Listen { get; init; }

    /// <summary>The device to listen on, by Windows' id; empty for Windows' default device.</summary>
    public string ListenDevice { get; init; } = "";

    /// <summary>A Soundcraft Ui mixer on the network, by its IP address or its name (a port after a colon when it is not 80); empty for none.</summary>
    public string Mixer { get; init; } = "";

    /// <summary>
    /// The playlist of the mixer's USB stick whose tracks the steps and the Sounds view offer, and which
    /// can all be given sounds at once. As the mixer names it; empty for all its tracks ("~all~").
    /// </summary>
    public string Playlist { get; init; } = "";

    /// <summary>The longest address of a mixer.</summary>
    public const int MaxMixerLength = 253;

    internal SoundSetup Checked()
    {
        int outputs = Music ?? Outputs;
        if (outputs < 1 || outputs > 16)
            throw new DeskException("A pair of outputs is counted from 1 (outputs 1 and 2) to 16.");

        string mixer = (Mixer ?? "").Trim();
        if (mixer.Length > 0 && !IsAddress(mixer))
            throw new DeskException($"The mixer is found by its IP address, like 10.10.1.1, or by its name; \"{mixer}\" is neither.");

        string playlist = Playlist ?? "";
        if (playlist.Length > Sound.MaxTrackNameLength || playlist.Any(char.IsControl) || playlist.Contains('^'))
            throw new DeskException("The playlist is named as the mixer lists it.");

        return this with
        {
            Device = (Device ?? "").Trim(),
            Outputs = outputs,
            Music = null,
            ListenDevice = (ListenDevice ?? "").Trim(),
            Mixer = mixer,
            Playlist = playlist,
        };
    }

    // An IP address or a name, maybe with a port: nothing else that a URL could hold.
    private static bool IsAddress(string text) =>
        text.Length <= MaxMixerLength
        && !text.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '/' or '\\' or '?' or '#' or '@')
        && Uri.TryCreate($"ws://{text}", UriKind.Absolute, out Uri? uri)
        && uri.Host.Length > 0;
}

/// <summary>LISTEN ON as every page hears of it: the key LISTEN of the sound section (the device belongs to the PC).</summary>
/// <param name="Listen">True while what goes out plays on the device to listen on as well.</param>
/// <param name="State">"" while it is off; else "playing", "missing", "none", "off" or "same", as <see cref="SoundOutputView.ListenState"/>.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record ListenView(bool Listen, string State);

/// <summary>The mixer of the tracks as every page hears of it (the address belongs to the PC).</summary>
/// <param name="State">"" (no mixer asked for), "connecting" (looked for) or "connected" (it answers).</param>
/// <param name="Name">What the mixer is, as it says, like "ui16, firmware 1.0.7548"; empty until it said.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record MixerView(string State, string Name)
{
    /// <summary>The playlist chosen in Settings; empty for all the tracks of the stick.</summary>
    public string Playlist { get; init; } = "";
}

/// <summary>A device of the PC that plays sound.</summary>
/// <param name="Id">Windows' id of it.</param>
/// <param name="Name">Its name, like "Speakers (USB Audio CODEC)".</param>
/// <param name="Channels">Its outputs.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record SoundDevice(string Id, string Name, int Channels);

/// <summary>The sound's device as the Sounds view shows it.</summary>
/// <param name="Devices">The devices of the PC that play.</param>
/// <param name="Device">The one chosen, by its id; empty for Windows' default device.</param>
/// <param name="Outputs">The pair of outputs of the sounds, from 1.</param>
/// <param name="State">"playing" (the device is open), "missing" (the chosen device is not there), "none" (the PC has no device), or "off" (no devices here: another system, or a test desk).</param>
/// <param name="Name">The device in use; empty for none.</param>
/// <param name="Channels">The outputs the sound has now.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record SoundOutputView(SoundDevice[] Devices, string Device, int Outputs, string State, string Name, int Channels)
{
    /// <summary>The device is open and plays.</summary>
    public const string Playing = "playing";

    /// <summary>The chosen device is not there.</summary>
    public const string Missing = "missing";

    /// <summary>The PC has no device that plays.</summary>
    public const string NoDevice = "none";

    /// <summary>No devices here: another system than Windows, or a test desk.</summary>
    public const string Off = "off";

    /// <summary>The device to listen on is the device of the sound: it plays there already.</summary>
    public const string Same = "same";

    /// <summary>True when what goes out plays on the device to listen on as well.</summary>
    public bool Listen { get; init; }

    /// <summary>The device to listen on, by its id; empty for Windows' default device.</summary>
    public string ListenDevice { get; init; } = "";

    /// <summary>The device to listen on: empty while listening is off; else as <see cref="State"/>, or <see cref="Same"/>.</summary>
    public string ListenState { get; init; } = "";

    /// <summary>The device listened on; empty for none.</summary>
    public string ListenName { get; init; } = "";

    /// <summary>The Soundcraft Ui mixer asked for, by its address; empty for none.</summary>
    public string Mixer { get; init; } = "";

    /// <summary>The playlist chosen; empty for all the tracks of the stick.</summary>
    public string Playlist { get; init; } = "";

    /// <summary>The mixer: "" (none asked for), "connecting" (looked for) or "connected" (it answers).</summary>
    public string MixerState { get; init; } = "";

    /// <summary>What the mixer is, as it says, like "ui16, firmware 1.0.7548"; empty until it said.</summary>
    public string MixerName { get; init; } = "";
}
