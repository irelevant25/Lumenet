using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// The playback of an effect: its key with the fade times, its fader that starts and stops it, Clear,
/// the rate, the live speed, size and spread with reset and Keep, the lock while the stack is live,
/// and the steps of the stack that play an effect, with Add and Release.
/// </summary>
public sealed class EffectPlaybackTests
{
    private readonly ManualTime _time = new();
    private readonly Desk _desk;

    public EffectPlaybackTests()
    {
        _desk = new Desk(_time);
        _desk.SetStage(EffectRuleTests.Plan(EffectRuleTests.Par()));
    }

    // Full for the first half of every cycle: the PAR's dimmer at the effect's level then.
    private static Effect Pulse(int number = 1) =>
        new(number, "Pulse", Effect.IntensityKind, "pulse") { Lights = [1], Speed = 60, Spread = 0 };

    private int Dimmer() => _desk.GetOutput()[0];

    private EffectPlayback Playback(int number = 1) => _desk.GetEffectPlayback(number);

    [Fact]
    public void TheKeyMovesTheFaderUpInTheFadeInTimeAndDownInTheFadeOutTime()
    {
        _desk.SetEffect(Pulse() with { FadeIn = 2, FadeOut = 4 });
        Assert.Equal(0, Playback().Level);

        EffectPlayback started = _desk.SetEffectControl(1, "go", 1);
        Assert.True(started.Running);
        Assert.True(started.Fading);
        Assert.Equal(0, started.Level);   // the fader leaves zero, on its way up
        Assert.Equal(0, Dimmer());

        _time.Pass(1);
        Assert.Equal(128, Playback().Level);   // half way up
        Assert.Equal(128, Dimmer());
        _time.Pass(1);
        Assert.Contains(_desk.Tick(), moved => moved.Effect && moved.Number == 1 && moved.Level == 255);   // the UIs hear of it
        Assert.Equal(255, Dimmer());
        Assert.False(Playback().Fading);

        _time.Pass(0.1);
        _desk.SetEffectControl(1, "go", 0);
        _time.Pass(1);
        Assert.Equal(191, Dimmer());   // a quarter of the way down

        // The key on the way down: up again from there, at the pace of the fade in (a quarter of 2 s).
        Assert.True(_desk.SetEffectControl(1, "toggle", 0).Fading);
        _time.Pass(0.5);
        Assert.Equal(255, Playback().Level);

        _desk.SetEffectControl(1, "toggle", 0);
        _time.Pass(4);
        _desk.Tick();
        Assert.False(Playback().Running);
        Assert.False(Playback().Fading);
        Assert.Equal(0, Playback().Level);   // the fader is down, as the effect is out
        Assert.Equal(0, Dimmer());
    }

    [Fact]
    public void TheFaderStartsTheEffectWhenItLeavesZeroAndStopsItAtZero()
    {
        _desk.SetEffect(Pulse() with { FadeIn = 5 });

        _desk.SetEffectControl(1, "level", 40);
        Assert.True(Playback().Running);
        Assert.Equal(102, Dimmer());   // at once: the fader is its own fade

        _desk.SetEffectControl(1, "level", 60);
        Assert.Equal(153, Dimmer());

        _desk.SetEffectControl(1, "level", 0);
        Assert.False(Playback().Running);
        Assert.Equal(0, Dimmer());

        // The key takes the fader down with the effect (at once, without a fade-out time), and a
        // fader raised again starts it again.
        _desk.SetEffectControl(1, "level", 50);
        _desk.SetEffectControl(1, "go", 0);
        Assert.False(Playback().Running);
        Assert.Equal(0, Playback().Level);
        _desk.SetEffectControl(1, "level", 70);
        Assert.True(Playback().Running);
    }

    [Fact]
    public void ToggleStartsAndStopsAndAnEffectThatIsNotThereCannotStart()
    {
        _desk.SetEffect(Pulse());
        Assert.True(_desk.SetEffectControl(1, "toggle", 0).Running);
        Assert.False(_desk.SetEffectControl(1, "toggle", 0).Running);

        var error = Assert.Throws<DeskException>(() => _desk.SetEffectControl(9, "go", 1));
        Assert.Equal("There is no effect 9 to start.", error.Message);
        Assert.Throws<DeskException>(() => _desk.SetEffectControl(1, "jump", 1));
        Assert.Throws<DeskException>(() => _desk.SetEffectControl(1, "level", 101));
        Assert.Throws<DeskException>(() => _desk.SetEffectControl(1, "speed", 0.1));
    }

    [Fact]
    public void ClearStopsEveryEffectAndBringsItsFaderToZero()
    {
        _desk.SetEffect(Pulse());
        _desk.SetEffectControl(1, "go", 1);

        _desk.Clear();

        Assert.False(Playback().Running);
        Assert.Equal(0, Playback().Level);
        Assert.Equal(0, Dimmer());
    }

    [Fact]
    public void WhileRateIsLitAnEffectRunsAtItsSpeedTimesTheTempoOver120AndDoesNotJump()
    {
        _desk.SetEffect(Pulse() with { Speed = 120 });
        _desk.SetEffectControl(1, "go", 1);
        _time.Pass(0.2);
        Assert.Equal(0.4, Playback().Phase, 3);
        Assert.Equal(120, Playback().Bpm);

        _desk.SetRate(true, 60);   // half the usual tempo: one cycle every two beats of 60
        Assert.Equal(0.4, Playback().Phase, 3);   // the same place as a moment ago
        Assert.Equal(60, Playback().Bpm);
        _time.Pass(1);
        Assert.Equal(1.4, Playback().Phase, 3);

        _desk.SetRate(true, 120);   // the usual tempo: its own speed again, one cycle per beat
        _time.Pass(0.5);
        Assert.Equal(2.4, Playback().Phase, 3);
    }

    [Fact]
    public void LiveValuesChangeTheRunOnlyUntilKeepStoresThemOrResetForgetsThem()
    {
        _desk.SetEffect(Pulse() with { Speed = 60, Size = 100, Spread = 0 });
        _desk.SetEffectControl(1, "go", 1);
        _time.Pass(0.25);

        EffectPlayback faster = _desk.SetEffectControl(1, "speed", 120);
        Assert.Equal(0.25, faster.Phase, 3);   // no jump
        Assert.True(faster.Changed);
        Assert.Equal(60, _desk.GetEffects()[0].Speed);   // the effect itself is as it was

        _desk.SetEffectControl(1, "size", 40);
        _desk.SetEffectControl(1, "spread", 75);
        EffectPlayback reset = _desk.SetEffectControl(1, "reset", 0);
        Assert.Equal((60.0, 100, 0, false), (reset.Speed, reset.Size, reset.Spread, reset.Changed));

        _desk.SetEffectControl(1, "speed", 90);
        Effect kept = _desk.KeepEffect(1);
        Assert.Equal(90, kept.Speed);
        Assert.False(Playback().Changed);
    }

    [Fact]
    public void WhileTheStackIsLiveEffectsCannotBeChangedButTheirPlaybacksWork()
    {
        _desk.SetEffect(Pulse());
        _desk.AddStackStep(new Step(Step.Channel, 5, 255, 0, 0, 0));
        _desk.SetStackLive(true);

        Assert.Throws<DeskException>(() => _desk.SetEffect(Pulse() with { Speed = 30 }));
        Assert.Throws<DeskException>(() => _desk.DeleteEffect(1));
        Assert.Throws<DeskException>(() => _desk.KeepEffect(1));
        Assert.Throws<DeskException>(() => _desk.SetEffectIndependent(1, 2));

        _desk.SetEffectControl(1, "go", 1);
        _desk.SetEffectControl(1, "speed", 90);
        Assert.Equal(255, Dimmer());
    }

    [Fact]
    public void TheStackBringsTheFaderOfAnEffectUpInItsUpTimeAndDownInItsDownTime()
    {
        _desk.SetEffect(Pulse());
        _desk.AddStackStep(new Step(Step.EffectTarget, 1, 255, 2, 0, 2));
        _desk.AddStackStep(new Step(Step.Channel, 5, 255, 0, 0, 0));
        _desk.SetStackLive(true);

        _time.Pass(1);
        _desk.Tick();
        Assert.True(Playback().Running);
        Assert.Equal(128, Playback().Level);   // half way up
        Assert.Equal(128, Dimmer());

        _time.Pass(1);
        _desk.Tick();
        Assert.Equal(255, Playback().Level);

        _desk.StackNext();   // the step of the effect goes down in its down time
        _time.Pass(2);
        IReadOnlyList<FaderMovedView> moved = Moved();
        Assert.Contains(new FaderMovedView(Effect: true, 1, 0), moved);
        Assert.False(Playback().Running);
    }

    [Fact]
    public void AReleaseTakesTheEffectDownInItsOwnDownTimeAndAnAddedEffectComesOnTop()
    {
        _desk.SetEffect(Pulse());
        _desk.AddStackStep(new Step(Step.Channel, 5, 255, 0, 0, 0));
        int effect = _desk.AddStackStep(new Step(Step.EffectTarget, 1, 255, 0, 0, 3) { Add = true });
        _desk.AddStackStep(new Step(Step.Release, effect, 0, 0, 0, 1));
        _desk.SetStackLive(true);

        _desk.StackNext();
        _desk.Tick();
        Assert.True(Playback().Running);
        Assert.Equal(255, _desk.GetOutput()[4]);   // the channel of step 1 stays: the effect came on top

        _desk.StackNext();   // the release, in one second, not in the effect's three
        _time.Pass(1);
        _desk.Tick();
        Assert.False(Playback().Running);
        Assert.Equal(0, Playback().Level);
        Assert.Equal(255, _desk.GetOutput()[4]);
    }

    [Fact]
    public void AStepOfASequenceCannotPlayAnEffect()
    {
        var error = Assert.Throws<DeskException>(() => _desk.SetSequence(3, true, [new Step(Step.EffectTarget, 1, 255, 0, 0, 0)]));
        Assert.Equal("A step of a sequence cannot play an effect; a step of the stack can.", error.Message);
        Assert.Throws<DeskException>(() => _desk.AddStackStep(new Step(Step.EffectTarget, 65, 255, 0, 0, 0)));
    }

    [Fact]
    public void TakingAnEffectOutStopsItsPlaybackAndAKeyBoundToItIsKept()
    {
        _desk.SetEffect(Pulse());
        _desk.SetEffectIndependent(3, 1);
        _desk.SetEffectControl(1, "go", 1);

        Assert.True(_desk.DeleteEffect(1));
        Assert.False(Playback().Running);
        Assert.Equal(0, Dimmer());
        Assert.Equal(1, _desk.GetState().EffectIndependents[2]);
        Assert.Empty(_desk.GetState().Effects);   // no effect, its fader down: nothing to show
    }

    // The faders the desk clock would tell the UIs about, as plain values.
    private List<FaderMovedView> Moved() => [.. _desk.Tick().Select(moved => new FaderMovedView(moved.Effect, moved.Number, moved.Level))];

    private readonly record struct FaderMovedView(bool Effect, int Number, int Level);
}
