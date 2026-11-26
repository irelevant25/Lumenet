namespace DmxBox.Server.Midi;

/// <summary>
/// The desk's faders for MIDI (<see cref="IMidiDesk"/>): read from the desk, and moved on the desk
/// as a page moves them, with the same refusals (the speed of a sequence is part of the project
/// and waits while the stack is live). Every page hears of a move: it is no page's own.
/// </summary>
/// <remarks>Not thread-safe: <see cref="MidiControl"/> holds its lock around it.</remarks>
internal sealed class MidiDeskFaders : IMidiDesk
{
    private readonly Desk _desk;
    private readonly ProjectStore _projects;
    private readonly UndoHistory _history;
    private readonly IDeskClient _pages;
    private readonly ILogger _log;

    // The desk as read last: once for each message and each round of feedback (see Refresh).
    private Snapshot? _snapshot;

    /// <summary>Puts the parts together.</summary>
    /// <param name="desk">The desk.</param>
    /// <param name="projects">The open project: a sequence's speed changes it.</param>
    /// <param name="history">Undo and redo: a sequence's speed is a step of it.</param>
    /// <param name="pages">Every page.</param>
    /// <param name="log">Where a page that could not be told is written.</param>
    public MidiDeskFaders(Desk desk, ProjectStore projects, UndoHistory history, IDeskClient pages, ILogger log)
    {
        _desk = desk;
        _projects = projects;
        _history = history;
        _pages = pages;
        _log = log;
    }

    /// <inheritdoc />
    public MidiPages Pages { get; set; } = MidiPages.First;

    /// <inheritdoc />
    public int ChannelCount => _desk.ChannelCount;

    /// <inheritdoc />
    public void Refresh()
    {
        _snapshot = null;
    }

    /// <inheritdoc />
    public int? PositionOf(DeskFader fader)
    {
        Snapshot now = Read();
        int number = fader.Number;
        switch (fader.Kind)
        {
            case FaderKind.Channel:
                return number >= 1 && number <= now.State.Channels.Length ? now.State.Channels[number - 1] : null;
            case FaderKind.Memory:
                return number >= 1 && number <= now.State.MemoryLevels.Length ? now.State.MemoryLevels[number - 1] : null;
            case FaderKind.Master:
                return now.State.Master;
            case FaderKind.Rate:
                return FaderScale.PositionOf(FaderKind.Rate, now.State.Bpm);
            case FaderKind.Sequence:
                return now.Sequences.TryGetValue(number, out Sequence? sequence) ? FaderScale.PositionOf(FaderKind.Sequence, sequence.Speed) : null;
            case FaderKind.SoundVolume:
                return FaderScale.PositionOf(FaderKind.SoundVolume, now.State.SoundDecibels);
            case FaderKind.Sound:
                // no sound under that number: its fader moves nothing
                return now.Sounds.Contains(number) ? _desk.GetSoundPlayback(number).Level : null;
        }

        if (!now.Effects.Contains(number))
            return null;   // no effect under that number: its fader moves nothing

        EffectPlayback playback = now.Playback(number, _desk);
        return fader.Kind switch
        {
            FaderKind.Effect => playback.Level,
            FaderKind.EffectSpeed => FaderScale.PositionOf(FaderKind.EffectSpeed, playback.Speed),
            FaderKind.EffectSize => FaderScale.PositionOf(FaderKind.EffectSize, playback.Size),
            _ => FaderScale.PositionOf(FaderKind.EffectSpread, playback.Spread),
        };
    }

    /// <inheritdoc />
    public FaderMove Move(DeskFader fader, int position)
    {
        int number = fader.Number;
        double value = FaderScale.ValueAt(fader.Kind, position);
        try
        {
            switch (fader.Kind)
            {
                case FaderKind.Channel:
                    _desk.SetChannel(number, position);
                    Tell(_pages.ChannelChanged(number, position));
                    break;
                case FaderKind.Memory:
                    _desk.SetMemoryLevel(number, position);
                    Tell(_pages.MemoryLevelChanged(number, position));
                    break;
                case FaderKind.Master:
                    _desk.SetMaster(position);
                    Tell(_pages.MasterChanged(position));
                    break;
                case FaderKind.Rate:
                    bool on = Read().State.RateOn;
                    _desk.SetRate(on, value);
                    Tell(_pages.RateChanged(on, value));
                    break;
                case FaderKind.Sequence:
                    double kept;
                    using (_history.Change($"the speed of the sequence in memory {number}", $"speed {number}"))
                        kept = _desk.SetSequenceSpeed(number, value);
                    Tell(_pages.SequenceSpeedChanged(number, kept));
                    TellProject();
                    break;
                case FaderKind.Sound:
                    if (!Read().Sounds.Contains(number))
                        throw new DeskException($"There is no sound {number}.");
                    Tell(_pages.SoundPlaybackChanged(_desk.SetSoundControl(number, "level", value)));
                    break;
                case FaderKind.SoundVolume:
                    _desk.SetSoundVolume(value);
                    (double decibels, bool mute) = _desk.SoundMaster;
                    Tell(_pages.SoundMasterChanged(decibels, mute));
                    break;
                default:
                    string control = fader.Kind switch
                    {
                        FaderKind.Effect => "level",
                        FaderKind.EffectSpeed => "speed",
                        FaderKind.EffectSize => "size",
                        _ => "spread",
                    };
                    Tell(_pages.EffectPlaybackChanged(_desk.SetEffectControl(number, control, value)));
                    break;
            }
        }
        catch (DeskException e)
        {
            return new FaderMove(null, e.Message);
        }

        _snapshot = null;   // read again: the move changed it
        return new FaderMove(PositionOf(fader) ?? position, null);
    }

    // The project and undo changed with a sequence's speed: the pages hear of it as after a page's change.
    private void TellProject()
    {
        if (_projects.TakeChange() is { } project)
            Tell(_pages.ProjectChanged(project));
        if (_history.TakeChange() is { } history)
            Tell(_pages.HistoryChanged(history));
    }

    // The pages are told on their own time; a page that cannot be reached is written down.
    private void Tell(Task telling)
    {
        telling.ContinueWith(
            done => _log.LogWarning(done.Exception?.GetBaseException(), "A page could not hear of a fader a MIDI controller moved."),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private Snapshot Read()
    {
        return _snapshot ??= new Snapshot(
            _desk.GetState(),
            _desk.GetSequences(),
            [.. _desk.GetEffects().Select(effect => effect.Number)],
            [.. _desk.GetSounds().Select(sound => sound.Number)]);
    }

    // The desk as read once: the playbacks of the effects are asked for when needed.
    private sealed class Snapshot(DeskState state, Dictionary<int, Sequence> sequences, HashSet<int> effects, HashSet<int> sounds)
    {
        private readonly Dictionary<int, EffectPlayback> _playbacks = [];

        public DeskState State => state;

        public Dictionary<int, Sequence> Sequences => sequences;

        public HashSet<int> Effects => effects;

        public HashSet<int> Sounds => sounds;

        public EffectPlayback Playback(int number, Desk desk)
        {
            if (!_playbacks.TryGetValue(number, out EffectPlayback? playback))
            {
                playback = desk.GetEffectPlayback(number);
                _playbacks[number] = playback;
            }

            return playback;
        }
    }
}
