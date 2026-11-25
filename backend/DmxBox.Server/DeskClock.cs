namespace DmxBox.Server;

/// <summary>
/// Makes time pass on the desk: about 30 times a second it lets the desk move what moves by
/// itself (the faders the stack is moving, the sequences), and tells the UIs what changed.
/// </summary>
/// <remarks>
/// The box link needs no word from here: it sends whenever the desk says that its output has
/// changed, and <see cref="Desk.Tick"/> says so while something moves.
/// </remarks>
internal sealed class DeskClock : BackgroundService
{
    private static readonly TimeSpan Round = TimeSpan.FromMilliseconds(33);

    // The UIs get the playback state on every third round: about 10 times a second. While an effect
    // runs, on every round: the Stage view shows the effects from it, and 10 a second would scramble a
    // fast chase there (a light of it on and off again between two of them).
    private const int RoundsPerPlayback = 3;

    private readonly Desk _desk;
    private readonly IDeskClient _everyUi;
    private readonly ILogger<DeskClock> _log;
    private int _round;
    private bool _wasMoving;

    /// <summary>Puts the clock together; the host starts it.</summary>
    /// <param name="desk">The desk.</param>
    /// <param name="everyUi">Reaches every UI that is connected.</param>
    /// <param name="log">Gets a line when a round fails.</param>
    public DeskClock(Desk desk, IDeskClient everyUi, ILogger<DeskClock> log)
    {
        _desk = desk;
        _everyUi = everyUi;
        _log = log;
    }

    /// <summary>
    /// One round: the faders the stack moved go out to the UIs, and, while something moves and
    /// once more when it has stopped, the playback state. The tests call this by hand.
    /// </summary>
    public async Task StepAsync()
    {
        foreach (FaderMoved moved in _desk.Tick())
        {
            await (moved.Sound
                ? _everyUi.SoundPlaybackChanged(_desk.GetSoundPlayback(moved.Number))
                : moved.Effect
                ? _everyUi.EffectPlaybackChanged(_desk.GetEffectPlayback(moved.Number))
                : moved.Memory
                    ? _everyUi.MemoryLevelChanged(moved.Number, moved.Level)
                    : _everyUi.ChannelChanged(moved.Number, moved.Level));
        }

        bool moving = _desk.Animating;
        bool itsTurn = ++_round % (_desk.PlaysEffects ? 1 : RoundsPerPlayback) == 0;
        if ((moving && itsTurn) || (_wasMoving && !moving))
            await _everyUi.PlaybackChanged(_desk.GetPlayback());

        _wasMoving = moving;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Round);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await StepAsync();
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // One failed round must not stop time on the desk for good.
                    _log.LogError(e, "A round of the desk clock failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The server is shutting down.
        }
    }
}
