namespace DmxBox.Server.Sounds;

/// <summary>
/// The tracks of the USB stick of a Soundcraft Ui mixer (sounds with a <see cref="Sound.Playlist"/>): they
/// play on the mixer's own player, through <see cref="UiMixer"/>, one at a time, while the files play on the
/// PC beside them; and while the mixer answers, its PLAYER L/R channel is the desk's sound section: its
/// fader the volume of all sounds, its mute MUTE, both ways.
/// </summary>
/// <remarks>
/// <para>
/// While no track plays, PLAYER L/R stand at the volume of all sounds. The track that plays sets them to
/// that volume plus its own level (<see cref="Sound.Decibels"/>), with its fader on top as a gain: with
/// the volume at -20 dB, a track at -6 dB with its fader at half plays at -32 dB. So its key's fades, the
/// stack's up and down times and the fade at its end move PLAYER L/R, and after the track they stand at
/// the volume again. MUTE mutes PLAYER L/R. The fader of PLAYER L/R is sent when it moves, at most 20
/// times a second (the last place a moment later), the bottom at once.
/// </para>
/// <para>
/// The mixer's player sounds on for a moment after it was told to stop (the owner heard up to half a
/// second at the volume, from where the fade had left the track, when PLAYER L/R went up at once). So a
/// track the desk stops takes PLAYER L/R to the bottom first, and they stay there for
/// <see cref="QuietTime"/>, and longer while the mixer still says its player plays (at most
/// <see cref="QuietLongest"/>); a track that starts meanwhile gets its level as soon as the mixer says
/// it plays that one. A track that ends by itself needs no such moment.
/// </para>
/// <para>
/// The other way: a hand that moves PLAYER L or R on the mixer (its own page, another program) sets the
/// volume of all sounds, so that PLAYER L/R stand where the hand put them; while a track plays, the volume
/// is what is left after its level and its fader. A hand that mutes or unmutes them sets MUTE. When the
/// desk finds the mixer for the first time, PLAYER L/R as they stand on it become the volume and MUTE; when
/// it finds it again after it was lost, what the desk wants goes to it instead (what the mixer tells at
/// once then is no hand, see <see cref="UiMixer"/>). The desk hears of a hand through
/// <see cref="VolumeHeard"/> and <see cref="MuteHeard"/>.
/// </para>
/// <para>
/// A track that is started counts as playing for <see cref="StartTime"/> seconds whatever the mixer says,
/// as its player needs a moment to load it. Then it plays while the player has it, playing or paused; when
/// the player stops (at the track's end, as the desk sets it to play one track and stop, or by a hand on
/// the mixer) or takes another track, it is over. A track the player never took (gone from the stick) is
/// over and missing. While the mixer cannot be reached its player plays on by itself, so a track counts as
/// playing until the mixer says otherwise. Another track that starts ends the one before: the player has one.
/// </para>
/// <para>Thread-safe. The events come without the lock held.</para>
/// </remarks>
internal sealed class UiTrackPlayer : IDisposable
{
    /// <summary>How long a track that was started counts as playing before the mixer must have it, in seconds.</summary>
    public const double StartTime = 3;

    /// <summary>How long PLAYER L/R stay at the bottom after the desk stopped a track: the player sounds on a moment.</summary>
    public static readonly TimeSpan QuietTime = TimeSpan.FromSeconds(1.5);

    /// <summary>The longest they stay there while the mixer still says its player plays the track that was stopped.</summary>
    public static readonly TimeSpan QuietLongest = TimeSpan.FromSeconds(4);

    // The fader of PLAYER L/R is sent at most this often while it moves, in seconds.
    private const double FaderPause = 0.05;

    // While the player still plays after the quiet time, it is asked again this often.
    private static readonly TimeSpan QuietCheck = TimeSpan.FromMilliseconds(250);

    private readonly UiMixer _mixer;
    private readonly Func<double> _now;
    private readonly Lock _lock = new();
    private readonly bool[] _missing = new bool[Sound.MaxSounds];
    private readonly Timer _later;
    private readonly Timer _wake;
    private Sound? _track;
    private double _startedAt;
    private bool _heard;
    private double _position;
    private double _length;
    private double _decibels;     // the own level of the track that plays
    private double _level;        // its fader, 0 to 1
    private double _master = 1;   // the volume of all sounds as a gain, 0 to 1
    private bool _mute;           // MUTE
    private bool? _muted;         // the mute of PLAYER L/R as the mixer has it, as far as the desk knows
    private double _sentFader = double.NaN;   // the place of PLAYER L/R the mixer has, as far as the desk knows
    private double _sentAt = double.NegativeInfinity;
    private bool _waiting;        // a place waits for its turn: it goes a moment later
    private bool _held;           // the desk paused the track that plays (Pause of the stack)
    private bool _quiet;          // PLAYER L/R held at the bottom after the desk stopped a track (see the remarks)
    private long _quietSince;     // since when, Environment.TickCount64
    private string _link = UiMixer.None;   // the state of the link as the player saw it last

    /// <summary>The tracks over this link to a mixer.</summary>
    /// <param name="mixer">The link.</param>
    /// <param name="now">The time in seconds, from any start: a clock, or the test's own.</param>
    public UiTrackPlayer(UiMixer mixer, Func<double> now)
    {
        _mixer = mixer;
        _now = now;
        _later = new Timer(_ => SendLater());
        _wake = new Timer(_ => Wake());
        _link = mixer.State;
        mixer.PlayerMoved += OnPlayerMoved;
        mixer.PlayerMuted += OnPlayerMuted;
        mixer.Changed += OnLinkChanged;
    }

    /// <summary>Tells when a hand set PLAYER L/R on the mixer: the volume of all sounds that puts them there, in decibels.</summary>
    public event Action<double>? VolumeHeard;

    /// <summary>Tells when a hand muted or unmuted PLAYER L/R on the mixer: MUTE.</summary>
    public event Action<bool>? MuteHeard;

    public void Dispose()
    {
        _mixer.PlayerMoved -= OnPlayerMoved;
        _mixer.PlayerMuted -= OnPlayerMuted;
        _mixer.Changed -= OnLinkChanged;
        _later.Dispose();
        _wake.Dispose();
    }

    // The link to the mixer was made, or lost (not: the mixer said what it is). A mixer found again gets
    // what the desk wants of PLAYER L/R; one found for the first time tells its own (see the remarks of
    // the class).
    private void OnLinkChanged()
    {
        string state = _mixer.State;
        lock (_lock)
        {
            if (state == _link)
                return;

            _link = state;
            _sentAt = double.NegativeInfinity;
            _sentFader = double.NaN;
            _muted = null;
            if (!_mixer.IsConnected || !_mixer.HasSetPlayer)
                return;

            SendMute();
            SendFader(now: true);
        }
    }

    /// <summary>Plays a track from its start, in place of the track that plays. Without a mixer it is missing at once.</summary>
    public void Start(Sound track)
    {
        int index = track.Number - 1;
        lock (_lock)
        {
            _missing[index] = false;
            if (!_mixer.IsConnected || track.Playlist is null)
            {
                _missing[index] = true;
                if (_track?.Number == track.Number)
                    _track = null;
                return;
            }

            _track = track;
            _startedAt = _now();
            _heard = false;
            _held = false;
            _position = 0;
            _length = 0;
            _decibels = track.Decibels;
            _level = 0;

            // from silence: a fade-in starts at the bottom, and nothing of the track before is heard
            _mixer.SetPlayerFader(0);
            _sentFader = 0;
            _sentAt = double.NegativeInfinity;
            _mixer.PlayTrack(track.Playlist, track.File);
        }
    }

    /// <summary>The level of a track, 0 to 1: its fader. Only the track that plays moves PLAYER L/R.</summary>
    public void SetLevel(int number, double level)
    {
        lock (_lock)
        {
            if (_track?.Number != number)
                return;

            _level = Math.Clamp(level, 0, 1);
            SendFader();
        }
    }

    /// <summary>A sound was changed: the own level of the track that plays goes to PLAYER L/R at once.</summary>
    public void Change(Sound sound)
    {
        lock (_lock)
        {
            if (_track?.Number != sound.Number || !sound.IsTrack)
                return;

            _decibels = sound.Decibels;
            SendFader();
        }
    }

    /// <summary>The volume of all sounds as a gain, 0 to 1 (0 dB), and MUTE.</summary>
    public void SetMaster(double volume, bool mute)
    {
        lock (_lock)
        {
            _master = Math.Clamp(volume, 0, 1);
            _mute = mute;
            SendMute();
            SendFader();
        }
    }

    /// <summary>
    /// A track stops: if it is the track that plays, PLAYER L/R go to the bottom and the player stops; once
    /// it is quiet PLAYER L/R go back to the volume of all sounds (see the remarks of the class).
    /// </summary>
    public void Stop(int number)
    {
        lock (_lock)
        {
            _missing[number - 1] = false;
            if (_track?.Number != number)
                return;

            _track = null;
            _held = false;
            StopQuietly();
        }
    }

    /// <summary>
    /// The track that plays holds a pause where it is, or goes on from there (the mixer's player): once
    /// each way, so that a pause the desk did not make is never undone by it.
    /// </summary>
    public void Pause(int number, bool pause)
    {
        lock (_lock)
        {
            if (_track?.Number != number || !_mixer.IsConnected || _held == pause)
                return;

            _held = pause;
            _mixer.PausePlayer(pause);
        }
    }

    /// <summary>The track that plays goes on from this place, in seconds; only once the mixer told how long it is.</summary>
    public void Seek(int number, double seconds)
    {
        lock (_lock)
        {
            if (_track?.Number != number || _length <= 0)
                return;

            double part = Math.Clamp(seconds / _length, 0, 1);
            _mixer.JumpTo(part);
            _position = part * _length;
        }
    }

    /// <summary>Whether a track plays, and where it is in it, as the mixer tells; see the remarks of the class.</summary>
    public SoundVoiceState StateOf(int number)
    {
        lock (_lock)
        {
            if (_track?.Number != number)
                return new SoundVoiceState(Playing: false, Position: 0, Missing: _missing[number - 1]);

            Sound track = _track;
            UiPlayer? player = _mixer.IsConnected ? _mixer.Player : null;
            if (player is not { } said)
                return new SoundVoiceState(Playing: true, _position, Missing: false, _length);   // the mixer plays on by itself

            if (said.Plays && SameTrack(said.Track, track.File))
            {
                _heard = true;
                _position = said.Position;
                _length = Math.Max(0, said.Length);
                if (_quiet)
                {
                    // the player plays this track now, not the one stopped before it: its level may come
                    _quiet = false;
                    SendFader(now: true);
                }

                return new SoundVoiceState(Playing: true, _position, Missing: false, _length);
            }

            if (_now() - _startedAt < StartTime)
                return new SoundVoiceState(Playing: true, Position: 0, Missing: false);   // the player loads it

            // over: at its end, stopped or changed on the mixer; missing when the player never took it
            bool never = !_heard;
            _track = null;
            _missing[number - 1] = never;
            SendFader(now: true);
            return new SoundVoiceState(Playing: false, Position: 0, Missing: never);
        }
    }

    /// <summary>
    /// True when the player's track is this one: the same name, also in another Unicode form (a stick
    /// may keep "é" as two characters), or without its ending, or with a folder before it.
    /// </summary>
    internal static bool SameTrack(string said, string file)
    {
        string a = said.Normalize();
        string b = file.Normalize();
        return a == b
            || a.EndsWith('/' + b, StringComparison.Ordinal)
            || a.EndsWith('\\' + b, StringComparison.Ordinal)
            || (a.Length > 0 && Path.GetFileNameWithoutExtension(a) == Path.GetFileNameWithoutExtension(b) && (Path.GetExtension(a).Length == 0 || Path.GetExtension(b).Length == 0));
    }

    /// <summary>
    /// Where PLAYER L/R are to stand, in decibels: the volume of all sounds (a gain, 0 to 1) with the own
    /// level of the track that plays (0 dB while none does) and its fader (0 to 1) on it. Minus infinity
    /// for nothing.
    /// </summary>
    internal static double TargetDecibels(double decibels, double master, double level) =>
        decibels <= Sound.MinDecibels || master <= 0 || level <= 0
            ? double.NegativeInfinity
            : decibels + 20 * Math.Log10(master) + 20 * Math.Log10(level);

    // A hand moved PLAYER L or R on the mixer (or the mixer told it when it was found).
    private void OnPlayerMoved(double place)
    {
        double? volume;
        lock (_lock)
            volume = HeardFader(place);
        Tell(volume, null);
    }

    // A hand muted or unmuted PLAYER L or R on the mixer (or the mixer told it when it was found).
    private void OnPlayerMuted(bool muted)
    {
        bool? mute;
        lock (_lock)
            mute = HeardMute(muted);
        Tell(null, mute);
    }

    private void Tell(double? volume, bool? mute)
    {
        if (volume is { } decibels)
            VolumeHeard?.Invoke(decibels);
        if (mute is { } muted)
            MuteHeard?.Invoke(muted);
    }

    // Call with the lock held. A place of PLAYER L/R the mixer told: the volume of all sounds that puts
    // them there (with the track that plays, its level and its fader), so that the desk sends nothing
    // back. Null when it is the volume the desk has, or when the desk has the track at nothing: then the
    // place says nothing of the volume.
    private double? HeardFader(double place)
    {
        double heard = UiFader.DecibelsAt(place);
        double volume;
        if (_track is null)
            volume = heard;
        else if (_level <= 0 || _decibels <= Sound.MinDecibels)
            return null;
        else
            volume = heard - _decibels - 20 * Math.Log10(_level);

        volume = Sound.Tenth(Math.Clamp(volume, Sound.MinDecibels, Sound.MaxDecibels));
        bool other = Math.Abs(volume - Sound.DecibelsOf(_master)) >= 0.05;
        if (other)
            _master = Sound.GainOf(volume);
        _sentFader = Fader();   // the mixer has it there, as near as the desk can say
        return other ? volume : null;
    }

    // Call with the lock held. The mute of PLAYER L/R the mixer told: MUTE, when it is another one.
    private bool? HeardMute(bool muted)
    {
        _muted = muted;
        if (muted == _mute)
            return null;

        _mute = muted;
        return muted;
    }

    // Call with the lock held. Where the desk wants PLAYER L/R now, 0 to 1 as the mixer's fader goes: the
    // bottom while the player may still sound after a stop.
    private double Fader()
    {
        if (_quiet)
            return 0;

        double decibels = _track is null ? TargetDecibels(0, _master, 1) : TargetDecibels(_decibels, _master, _level);
        return Math.Round(UiFader.ValueForDecibels(decibels), 3);
    }

    // Call with the lock held. The desk stops the track that played: PLAYER L/R to the bottom first, then
    // the player stops, and PLAYER L/R stay at the bottom until the player is quiet (Wake).
    private void StopQuietly()
    {
        _quiet = true;
        _quietSince = Environment.TickCount64;
        if (_mixer.IsConnected)
        {
            if (_sentFader != 0)
            {
                _mixer.SetPlayerFader(0);
                _sentFader = 0;
                _sentAt = _now();
            }

            _mixer.StopPlayer();
        }

        _wake.Change(QuietTime, Timeout.InfiniteTimeSpan);
    }

    // The quiet after a stop is over once the mixer's player no longer plays the track that was stopped
    // (or at the longest): PLAYER L/R go where the desk wants them.
    private void Wake()
    {
        lock (_lock)
        {
            if (!_quiet)
                return;

            UiPlayer? said = _mixer.IsConnected ? _mixer.Player : null;
            bool sounds = said is { Plays: true } player && (_track is null || !SameTrack(player.Track, _track.File));
            if (sounds && Environment.TickCount64 - _quietSince < (long)QuietLongest.TotalMilliseconds)
            {
                _wake.Change(QuietCheck, Timeout.InfiniteTimeSpan);
                return;
            }

            _quiet = false;
            SendFader(now: true);
        }
    }

    // Call with the lock held. MUTE to PLAYER L/R, when the mixer has another.
    private void SendMute()
    {
        if (!_mixer.IsConnected || _muted == _mute)
            return;

        _mixer.SetPlayerMute(_mute);
        _muted = _mute;
    }

    // Call with the lock held. The place of PLAYER L/R the desk wants, when the mixer has another: at
    // most every FaderPause seconds while it moves, the last one a moment later; the bottom, and with
    // "now" any place, at once.
    private void SendFader(bool now = false)
    {
        if (!_mixer.IsConnected)
            return;

        double fader = Fader();
        if (fader == _sentFader)
            return;

        double time = _now();
        if (!now && fader > 0 && time - _sentAt < FaderPause)
        {
            if (!_waiting)
            {
                _waiting = true;
                _later.Change(TimeSpan.FromSeconds(FaderPause), Timeout.InfiniteTimeSpan);
            }

            return;
        }

        _mixer.SetPlayerFader(fader);
        _sentFader = fader;
        _sentAt = time;
    }

    // The place that waited for its turn.
    private void SendLater()
    {
        lock (_lock)
        {
            _waiting = false;
            SendFader(now: true);
        }
    }
}
