namespace DmxBox.Tests.Sounds;

/// <summary>A pretend engine: it notes what the desk asks, and a sound plays until it is stopped or <see cref="End"/> says it is over.</summary>
internal sealed class PretendSoundPlayer : ISoundPlayer
{
    private readonly bool[] _playing = new bool[Sound.MaxSounds];

    public List<string> Calls { get; } = [];

    public double[] Levels { get; } = new double[Sound.MaxSounds];

    /// <summary>Where each sound is in its file, as the test sets it.</summary>
    public double[] Position { get; } = new double[Sound.MaxSounds];

    public void Start(Sound sound)
    {
        Calls.Add($"start {sound.Number}");
        _playing[sound.Number - 1] = true;
    }

    public void Change(Sound sound) => Calls.Add($"change {sound.Number}");

    public void SetLevel(int number, double level) => Levels[number - 1] = level;

    public void Stop(int number)
    {
        Calls.Add($"stop {number}");
        _playing[number - 1] = false;
    }

    public void Seek(int number, double seconds) => Calls.Add(FormattableString.Invariant($"seek {number} {seconds}"));

    public void Pause(int number, bool pause) => Calls.Add($"{(pause ? "pause" : "resume")} {number}");

    public void SetMaster(double volume, bool mute) => Calls.Add(FormattableString.Invariant($"master {volume} {mute}"));

    public SoundVoiceState StateOf(int number) => new(_playing[number - 1], Position[number - 1], false);

    // The sound played to its end.
    public void End(int number) => _playing[number - 1] = false;

    public event Action<double>? VolumeHeard;

    public event Action<bool>? MuteHeard;

    // A hand set the volume of all sounds, or MUTE, on the mixer.
    public void Hear(double? volume = null, bool? mute = null)
    {
        if (volume is { } decibels)
            VolumeHeard?.Invoke(decibels);
        if (mute is { } muted)
            MuteHeard?.Invoke(muted);
    }
}
