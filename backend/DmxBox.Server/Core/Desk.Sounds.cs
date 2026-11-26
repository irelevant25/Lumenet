namespace DmxBox.Server.Core;

// The part of the desk that plays sounds (see Sound): files, or tracks of a mixer's player, through
// the sound engine (ISoundPlayer; SoundEngine.cs makes the sound). A sound is stored in the project;
// its playback is not: whether it plays, its fader, where it is in its file.
//
// A sound plays like an effect: raising its fader from zero starts it from its start, bringing it
// to zero stops it. Its fader is its level (255: its own volume); its key moves the fader up to full
// in its fade-in time and down to zero in its fade-out time. A sound without a loop that plays to its
// end takes its fader down with it: in its fade-out time before its end, as its key would, or at its
// end without one. That is so where the fader is the hand's (its key, the fader, MIDI); where a step
// of the stack holds it, or a sequence plays the sound, the sound is over with its fader up, and the
// stack or the sequence starts it again from zero, or its key does. Over all sounds stand the master
// volume of the sound (in decibels: -20 dB at first, 0 dB every sound as it is set, -100 dB nothing) and
// MUTE, which belong to the evening, like the master fader of the light. While a Soundcraft Ui mixer
// answers they are its PLAYER L/R, both ways: a hand on the mixer sets them here too (SoundMasterHeard).
internal sealed partial class Desk
{
    /// <summary>The master volume of the sound on a new desk, in decibels: well under full, so that nothing starts loud.</summary>
    public const double DefaultSoundDecibels = -20;

    /// <summary>Number of independent keys for sounds. Each is bound to one sound, which the key starts and stops.</summary>
    public const int SoundIndependentCount = 32;

    // The sound each sound key of the independents is bound to.
    private readonly int[] _soundIndependents = DefaultSoundIndependents();

    // The sounds of the project, by number; null where there is none.
    private readonly Sound?[] _sounds = new Sound?[Sound.MaxSounds];

    // The playback of each sound, also of a number without a sound.
    private readonly SoundRun[] _soundRuns = [.. Enumerable.Range(0, Sound.MaxSounds).Select(_ => new SoundRun())];

    private ISoundPlayer _player = new NoSoundPlayer();
    private double _soundDecibels = DefaultSoundDecibels;
    private bool _soundMute;

    /// <summary>The master volume of the sound (-100 dB to 0 dB) and MUTE.</summary>
    public (double Decibels, bool Mute) SoundMaster
    {
        get
        {
            lock (_lock)
            {
                return (_soundDecibels, _soundMute);
            }
        }
    }

    /// <summary>
    /// Tells when the volume of all sounds or MUTE was set on the Soundcraft Ui mixer (the decibels and
    /// MUTE as they are now), so that the pages and the motors of MIDI faders hear of it. It comes without
    /// the lock held.
    /// </summary>
    public event Action<double, bool>? SoundMasterHeard;

    /// <summary>Hands the desk the engine that makes the sound. A desk without one plays sounds without a sound.</summary>
    /// <param name="player">The engine.</param>
    public void UseSoundPlayer(ISoundPlayer player)
    {
        ISoundPlayer old;
        lock (_lock)
        {
            old = _player;
            _player = player;
            player.SetMaster(Sound.GainOf(_soundDecibels), _soundMute);
        }

        old.VolumeHeard -= OnVolumeHeard;
        old.MuteHeard -= OnMuteHeard;
        player.VolumeHeard += OnVolumeHeard;
        player.MuteHeard += OnMuteHeard;
    }

    /// <summary>The bindings of the sound keys of the independents on a new desk: key 1 on sound 1, and so on.</summary>
    public static int[] DefaultSoundIndependents()
    {
        return [.. Enumerable.Range(1, SoundIndependentCount)];
    }

    /// <summary>Binds a sound key of the independents to a sound.</summary>
    /// <param name="independent">Number of the key, 1-32.</param>
    /// <param name="sound">The sound the key starts and stops from now on, 1-64.</param>
    /// <exception cref="DeskException">The stack is live.</exception>
    public void SetSoundIndependent(int independent, int sound)
    {
        RequireRange("sound independent", independent, 1, SoundIndependentCount);
        RequireSoundNumber(sound);

        lock (_lock)
        {
            Bind(_soundIndependents, independent, sound);
        }
    }

    /// <summary>
    /// Every track of a playlist of the mixer that is no sound yet becomes one, in the free numbers from
    /// the lowest on, named after the track (without its ending, cut to the longest name).
    /// </summary>
    /// <param name="playlist">The playlist, as the mixer names it.</param>
    /// <param name="tracks">Its tracks, as the mixer lists them.</param>
    /// <returns>The numbers of the new sounds: fewer than the tracks where some were sounds already, or the numbers ran out.</returns>
    /// <exception cref="DeskException">The stack is live.</exception>
    public int[] AddTracks(string playlist, IReadOnlyList<string> tracks)
    {
        Sound[] sounds = GetSounds();
        string[] fresh = [.. tracks.Distinct().Where(track => !sounds.Any(sound => sound.Playlist == playlist && sound.File == track))];
        int[] free = [.. Enumerable.Range(1, Sound.MaxSounds).Where(number => sounds.All(sound => sound.Number != number)).Take(fresh.Length)];
        for (int index = 0; index < free.Length; index++)
        {
            string name = Path.GetFileNameWithoutExtension(fresh[index]).Normalize();
            SetSound(new Sound(free[index], name.Length > Sound.MaxNameLength ? name[..Sound.MaxNameLength] : name, fresh[index], "") { Playlist = playlist });
        }

        return free;
    }

    /// <summary>The sounds of the project, in the order of their numbers.</summary>
    public Sound[] GetSounds()
    {
        lock (_lock)
        {
            return [.. _sounds.OfType<Sound>()];
        }
    }

    /// <summary>
    /// Stores a sound under its number, new or in place of the one there. One that plays goes on
    /// with what changed; with another file it starts again, from its start.
    /// </summary>
    /// <param name="sound">The sound.</param>
    /// <returns>The sound as the desk keeps it.</returns>
    /// <exception cref="DeskException">Something about it cannot be used, or the stack is live.</exception>
    public Sound SetSound(Sound? sound)
    {
        Sound checkedSound = (sound ?? throw new DeskException("The sound is missing.")).Checked();

        lock (_lock)
        {
            RequireUnlocked();
            int index = checkedSound.Number - 1;
            Sound? old = _sounds[index];
            if (old == checkedSound)
                return old;

            _sounds[index] = checkedSound;
            SoundRun run = _soundRuns[index];
            if (run.Voice)
            {
                if (!checkedSound.SameSource(old))
                    _player.Start(checkedSound);
                else
                    _player.Change(checkedSound);
            }
            else if (!checkedSound.SameSource(old))
            {
                run.Ended = false;   // a new file or track: the fader that is up plays it
                run.Missing = false;
            }

            _projectChanges++;
            return checkedSound;
        }
    }

    /// <summary>Takes a sound out of the project. It stops, its fader at zero.</summary>
    /// <param name="number">The sound, 1-64.</param>
    /// <returns>True when there was one.</returns>
    /// <exception cref="DeskException">The stack is live.</exception>
    public bool DeleteSound(int number)
    {
        RequireSoundNumber(number);

        lock (_lock)
        {
            RequireUnlocked();
            if (_sounds[number - 1] is null)
                return false;

            _sounds[number - 1] = null;
            ResetSoundRun(number);
            _projectChanges++;
            return true;
        }
    }

    /// <summary>Stores a copy of a sound under another number, in place of what is there.</summary>
    /// <param name="from">The sound to copy, 1-64.</param>
    /// <param name="to">The number for the copy, 1-64.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="DeskException">There is no sound to copy, both numbers are the same, or the stack is live.</exception>
    public Sound CopySound(int from, int to)
    {
        RequireSoundNumber(from);
        RequireSoundNumber(to);
        if (from == to)
            throw new DeskException($"Sound {from} cannot be copied onto itself.");

        Sound source;
        lock (_lock)
        {
            source = _sounds[from - 1] ?? throw new DeskException($"There is no sound {from} to copy.");
        }

        return SetSound(source with { Number = to });
    }

    /// <summary>
    /// Works a sound's playback, as its key and its fader do, and as anything outside the desk may
    /// (a MIDI controller). It is no part of the project, so it works while the stack is live.
    /// </summary>
    /// <param name="number">The sound, 1-64.</param>
    /// <param name="control">
    /// "go": 1 brings the fader up to full in the sound's fade-in time, which starts it (again, from its
    /// start, when it was over); 0 brings it down to zero in the fade-out time, which stops it. "toggle":
    /// down while it plays and is not already on its way down, else up. "level": its fader, 0 to 100 per
    /// cent, at once; from zero it starts the sound, at zero it stops it.
    /// </param>
    /// <param name="value">The value; for "toggle" any.</param>
    /// <returns>The playback after the change.</returns>
    /// <exception cref="DeskException">No such control, the value is out of range, or there is no such sound to start.</exception>
    public SoundPlayback SetSoundControl(int number, string? control, double value)
    {
        RequireSoundNumber(number);
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new DeskException("The value must be a number.");

        lock (_lock)
        {
            double now = Now;
            SoundRun run = _soundRuns[number - 1];
            switch (control)
            {
                case "go":
                    GoSound(number, value >= 0.5, now);
                    break;
                case "toggle":
                    GoSound(number, !run.Voice || (run.Moving && run.MoveTo == 0), now);
                    break;
                case "level":
                    if (!(value >= 0 && value <= 100))
                        throw new DeskException("The level of a sound goes from 0 to 100 per cent.");
                    run.Moving = false;
                    run.EndFading = false;
                    run.Level = (byte)Math.Round(value * MaxLevel / 100, MidpointRounding.AwayFromZero);
                    break;
                default:
                    throw new DeskException($"A sound has the controls go, toggle and level, not \"{control}\".");
            }

            _moves.Remove(new Fader(Memory: false, number, Sound: true));   // the hand wins over the stack

            SettleSound(number - 1, now);
            return SoundPlaybackOf(number);
        }
    }

    /// <summary>
    /// A sound that plays goes on from another place (a double click on its wave). It is no part of the
    /// project, so it works while the stack is live. A fade at its end that had begun is taken back: its
    /// fader is up again where it was.
    /// </summary>
    /// <param name="number">The sound, 1-64.</param>
    /// <param name="seconds">The place in its file (of a track: in the track), from its start to its end.</param>
    /// <returns>The playback after the change.</returns>
    /// <exception cref="DeskException">The sound does not play, or the place is no number.</exception>
    public SoundPlayback SeekSound(int number, double seconds)
    {
        RequireSoundNumber(number);
        if (!double.IsFinite(seconds) || seconds < 0)
            throw new DeskException("A place in a sound is a number of seconds from 0 on.");

        lock (_lock)
        {
            SoundRun run = _soundRuns[number - 1];
            if (_sounds[number - 1] is not { } sound || !run.Voice)
                throw new DeskException($"Sound {number} does not play: only a sound that plays goes on from another place.");

            double start = sound.IsTrack ? 0 : sound.Start;
            double end = sound.IsTrack ? run.Length : (sound.End ?? sound.Seconds);
            double to = end > start ? Math.Clamp(seconds, start, end) : Math.Max(start, seconds);
            if (run.EndFading)
            {
                run.Moving = false;
                run.EndFading = false;
                NoteSoundFader(number - 1, (byte)Math.Round(run.MoveFrom, MidpointRounding.AwayFromZero));
            }

            _player.Seek(number, to);
            run.Position = to;
            return SoundPlaybackOf(number);
        }
    }

    /// <summary>The playback of one sound.</summary>
    /// <param name="number">The sound, 1-64.</param>
    public SoundPlayback GetSoundPlayback(int number)
    {
        RequireSoundNumber(number);
        lock (_lock)
        {
            return SoundPlaybackOf(number);
        }
    }

    /// <summary>Sets the master volume of the sound. Like the master fader it is no part of the project.</summary>
    /// <param name="decibels">-100 dB (nothing) to 0 dB (every sound at its own level).</param>
    /// <exception cref="DeskException">The level is no number from -100 to 0.</exception>
    public void SetSoundVolume(double decibels)
    {
        if (!(decibels >= Sound.MinDecibels && decibels <= Sound.MaxDecibels))
            throw new DeskException($"The volume of the sound goes from {Sound.MinDecibels} dB to {Sound.MaxDecibels} dB.");

        lock (_lock)
        {
            _soundDecibels = Sound.Tenth(decibels);
            _player.SetMaster(Sound.GainOf(_soundDecibels), _soundMute);
        }
    }

    /// <summary>MUTE: no sound goes out while it is on; the sounds play on, unheard.</summary>
    /// <param name="mute">True for on.</param>
    public void SetSoundMute(bool mute)
    {
        lock (_lock)
        {
            _soundMute = mute;
            _player.SetMaster(Sound.GainOf(_soundDecibels), mute);
        }
    }

    // The volume of all sounds, set on the mixer: the desk takes it, and tells.
    private void OnVolumeHeard(double decibels)
    {
        double kept;
        bool mute;
        lock (_lock)
        {
            _soundDecibels = Sound.Tenth(Math.Clamp(decibels, Sound.MinDecibels, Sound.MaxDecibels));
            _player.SetMaster(Sound.GainOf(_soundDecibels), _soundMute);
            (kept, mute) = (_soundDecibels, _soundMute);
        }

        SoundMasterHeard?.Invoke(kept, mute);
    }

    // MUTE, set on the mixer.
    private void OnMuteHeard(bool mute)
    {
        double decibels;
        lock (_lock)
        {
            _soundMute = mute;
            _player.SetMaster(Sound.GainOf(_soundDecibels), mute);
            decibels = _soundDecibels;
        }

        SoundMasterHeard?.Invoke(decibels, mute);
    }

    // Call with the lock held. True while a sound plays or its key moves its fader.
    private bool SoundsPlay()
    {
        return _soundRuns.Any(run => run.Voice || run.Moving);
    }

    // Call with the lock held. The playbacks for the UIs: of every sound the project has, and of any
    // other number whose fader is up.
    private SoundPlayback[] SoundPlaybacksNow()
    {
        var playbacks = new List<SoundPlayback>();
        for (int index = 0; index < Sound.MaxSounds; index++)
        {
            if (_sounds[index] is not null || _soundRuns[index].Level > 0)
                playbacks.Add(SoundPlaybackOf(index + 1));
        }

        return [.. playbacks];
    }

    // Call with the lock held. The stack moves the fader of a sound (a step of the sound): the fader
    // stands where the stack has it, and a move of its key is over.
    private void MoveSoundFader(int number, byte level)
    {
        SoundRun run = _soundRuns[number - 1];
        run.Moving = false;
        run.EndFading = false;
        run.Level = level;
    }

    // Call with the lock held. The fader of a sound stands at this level at once, and the UIs (and the
    // motors of MIDI faders) hear of it.
    private void NoteSoundFader(int index, byte level)
    {
        SoundRun run = _soundRuns[index];
        if (run.Level == level)
            return;

        run.Level = level;
        _moved.RemoveAll(moved => moved.Sound && moved.Number == index + 1);
        _moved.Add(new FaderMoved(Memory: false, index + 1, level) { Sound = true });
    }

    // Call with the lock held. True while the fader of a sound is the hand's (its key, the fader, MIDI):
    // no sequence plays the sound, and the live stack neither moves the fader nor has a step of it on.
    private bool FaderOfHand(int index)
    {
        if (_soundRuns[index].Played > 0)
            return false;
        if (!_live)
            return true;

        int number = index + 1;
        return !_moves.ContainsKey(new Fader(Memory: false, number, Sound: true))
            && !_on.Any(id => _stack.Find(entry => entry.Id == id) is { Step: { IsSound: true } step } && step.Number == number);
    }

    // Call with the lock held. A sound without a loop near its end, its fader the hand's: the fader
    // goes down in the time that is left, when that is no more than the fade-out time, so that it is
    // at zero as the sound ends. Not while its key moves the fader: down already, or up (a sound
    // shorter than its fades comes up first, then goes down in what is left of it).
    private void FadeAtEnd(int index, Sound sound, SoundRun run, double now)
    {
        if (sound.Loop || sound.FadeOut <= 0 || run.EndFading || run.Moving || !FaderOfHand(index))
            return;

        double fader = SoundLevelAt(run, now);
        double end = sound.IsTrack ? run.Length : (sound.End ?? sound.Seconds);
        double left = (end - run.Position) / (sound.IsTrack ? 1 : Math.Max(sound.Speed, 0.01));
        if (fader <= 0 || end <= 0 || left > sound.FadeOut)
            return;

        run.Moving = true;
        run.MoveFrom = fader;
        run.MoveTo = 0;
        run.MoveStart = now;
        run.MoveTime = Math.Max(0.05, left);
        run.EndFading = true;
    }

    // Call with the lock held. Moves the faders the keys are moving, takes how strongly the sequences
    // play each sound, and starts, levels and stops the sounds as their faders and the sequences say.
    // The UIs hear of every fader moved. True while a sound plays.
    private bool SettleSounds(double now)
    {
        double[] played = new double[Sound.MaxSounds];
        AddSequenceSounds(played, now);
        for (int index = 0; index < Sound.MaxSounds; index++)
            _soundRuns[index].Played = _sounds[index] is null ? 0 : played[index];

        for (int index = 0; index < Sound.MaxSounds; index++)
        {
            SoundRun run = _soundRuns[index];
            if (run.Moving)
            {
                byte level = (byte)Math.Round(SoundLevelAt(run, now), MidpointRounding.AwayFromZero);
                if (now + 0.000001 - run.MoveStart >= run.MoveTime)
                {
                    level = run.MoveTo;
                    run.Moving = false;
                    run.EndFading = false;
                }

                if (level != run.Level || !run.Moving)
                {
                    run.Level = level;
                    _moved.RemoveAll(moved => moved.Sound && moved.Number == index + 1);
                    _moved.Add(new FaderMoved(Memory: false, index + 1, level) { Sound = true });
                }
            }

            SettleSound(index, now);
        }

        return SoundsPlay();
    }

    // Call with the lock held. One sound as its level says: started from zero (once, until it is
    // over), levelled while it plays, stopped at zero.
    private void SettleSound(int index, double now)
    {
        SoundRun run = _soundRuns[index];
        double level = Math.Max(SoundLevelAt(run, now) / MaxLevel, run.Played);
        if (_sounds[index] is not { } sound || level <= 0)
        {
            if (run.Voice)
                _player.Stop(index + 1);

            run.Voice = false;
            run.Ended = false;   // from zero it can start again
            return;
        }

        SoundVoiceState state = _player.StateOf(index + 1);
        if (run.Voice && !state.Playing)
        {
            // over: played to its end, or its file could not be played
            run.Voice = false;
            run.Ended = true;
            run.Missing = state.Missing;
            if (!state.Missing && !sound.Loop && FaderOfHand(index))
            {
                // its fader goes down with it, as its key would take it; from zero it starts again
                run.Moving = false;
                run.EndFading = false;
                run.Ended = false;
                NoteSoundFader(index, 0);
                return;
            }
        }
        else if (!run.Voice && !run.Ended)
        {
            _player.Start(sound);
            run.Voice = true;
            run.StartOrder = ++_soundStarts;
            run.Missing = false;
            state = _player.StateOf(index + 1);
            run.Missing = state.Missing;
        }

        if (run.Voice)
        {
            _player.SetLevel(index + 1, level);
            run.Position = state.Position;
            run.Length = state.Length;
            FadeAtEnd(index, sound, run, now);
        }
    }

    // Call with the lock held. The key of a sound: moves its fader up to full in the sound's fade-in
    // time, which starts it (from its start again, when it was over), or down to zero in its fade-out
    // time, which stops it. A fader already on its way goes on from where it is, at the same pace.
    private void GoSound(int number, bool start, double now)
    {
        SoundRun run = _soundRuns[number - 1];
        if (_sounds[number - 1] is not { } sound)
        {
            if (start)
                throw new DeskException($"There is no sound {number} to play.");
            return;
        }

        if (start)
            run.Ended = false;

        run.EndFading = false;
        byte to = start ? (byte)MaxLevel : (byte)0;
        double from = SoundLevelAt(run, now);
        double seconds = (start ? sound.FadeIn : sound.FadeOut) * Math.Abs(to - from) / MaxLevel;
        if (seconds <= 0)
        {
            run.Moving = false;
            run.Level = to;
            return;
        }

        run.Moving = true;
        run.MoveFrom = from;
        run.MoveTo = to;
        run.MoveStart = now;
        run.MoveTime = seconds;
    }

    // Call with the lock held. Every sound stops at once and its fader goes to zero: Clear, a project
    // loaded, the stack going live.
    private void StopSounds()
    {
        for (int number = 1; number <= Sound.MaxSounds; number++)
            ResetSoundRun(number);
    }

    // Call with the lock held. The sounds of a project take the place of the desk's, and the bindings
    // of the sound keys of the independents. One that plays goes on with what changed; with another
    // file, or gone, it stops.
    private void PutSounds(Sound?[] sounds, int[] independents)
    {
        independents.CopyTo(_soundIndependents, 0);
        for (int index = 0; index < Sound.MaxSounds; index++)
        {
            Sound? old = _sounds[index];
            Sound? sound = index < sounds.Length ? sounds[index] : null;
            _sounds[index] = sound;
            if (!_soundRuns[index].Voice || old == sound)
                continue;

            if (sound is null || !sound.SameSource(old))
                ResetSoundRun(index + 1);
            else
                _player.Change(sound);
        }
    }

    // Call with the lock held. A sound's playback back to rest: stopped, its fader at zero.
    private void ResetSoundRun(int number)
    {
        SoundRun run = _soundRuns[number - 1];
        if (run.Voice)
            _player.Stop(number);

        _soundRuns[number - 1] = new SoundRun();
    }

    private SoundPlayback SoundPlaybackOf(int number)
    {
        SoundRun run = _soundRuns[number - 1];
        byte level = (byte)Math.Round(SoundLevelAt(run, Now), MidpointRounding.AwayFromZero);
        return new SoundPlayback(number, run.Voice, level)
        {
            Fading = run.Moving,
            Played = (int)Math.Round(run.Played * MaxLevel, MidpointRounding.AwayFromZero),
            Position = Math.Round(run.Position, 2),
            Missing = run.Missing,
            Length = Math.Round(run.Length, 2),
        };
    }

    // Where a sound's fader stands, 0 to 255: on its way while its key moves it, between.
    private static double SoundLevelAt(SoundRun run, double now)
    {
        if (!run.Moving)
            return run.Level;

        double done = run.MoveTime <= 0 ? 1 : Math.Clamp((now - run.MoveStart) / run.MoveTime, 0, 1);
        return run.MoveFrom + ((run.MoveTo - run.MoveFrom) * done);
    }

    private static void RequireSoundNumber(int number)
    {
        RequireRange("sound", number, 1, Sound.MaxSounds);
    }

    // The playback of a sound: none of it is stored in the project.
    private sealed class SoundRun
    {
        public byte Level;          // its fader, as the UIs last heard of it
        public bool Moving;         // its key moves the fader, from MoveFrom to MoveTo:
        public double MoveFrom;     // where the fader stood when the move began, 0 to 255
        public byte MoveTo;         // where it goes
        public double MoveStart;    // when the move began, in desk seconds
        public double MoveTime;     // seconds the move takes
        public double Played;       // how strongly the sequences play it, 0 to 1, as the clock last found it
        public bool Voice;          // the engine plays it: from its start until it is over or stopped
        public bool Ended;          // it is over while its level stayed up: it starts again from zero or by its key
        public bool Missing;        // its file could not be found or played, or its track (no mixer, or not on its stick)
        public double Position;     // where it is in its file, in seconds
        public long StartOrder;     // which start it was, counted over all sounds: the latest is the highest (Learn)
        public double Length;       // a track of the mixer: how long it is, as the mixer said; 0 when unknown
        public bool EndFading;      // the move of the fader goes down with the end of the sound (FadeAtEnd)
    }
}
