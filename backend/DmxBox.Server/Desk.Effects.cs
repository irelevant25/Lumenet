namespace DmxBox.Server;

// The part of the desk that plays effects (see Effect): shapes worked out anew for every frame, on
// lights of the stage plan. An effect is stored in the project; its playback is not: whether it
// runs, its fader, and its live speed, size and spread, like the positions of the faders.
//
// How an effect mixes with the rest of the desk, frame by frame (GetOutput):
//  1. The faders, memories, sequences and the stack give their light as always, highest wins.
//  2. The effects work on that light, light by light, cell by cell:
//     - An intensity effect: highest wins. A light gives the higher of what the rest gives it and
//       what the effect gives it, its fader scaling the wave. A light's intensity is its dimmer, or
//       for a light without one its color channels together, keeping their mix; a light the rest
//       gives no color comes up white. Two intensity effects: the higher wins.
//     - A color effect takes over the color: from the color the rest gives (its fader at zero) to
//       the effect's (at full), the two mixed in between. A light the rest lights keeps its
//       intensity; one the rest leaves dark the effect lights itself, in its color, as bright as its
//       fader (near dark the two blend, so that a fade of the rest through dark has no jump). Where
//       an intensity effect plays the light, that one gives the brightness, and the color effect
//       only colors it. Two color effects: the one started last takes it.
//     - A pixel effect gives each cell a color and a brightness: the brightness as an intensity
//       effect (highest wins), the color as a color effect, but only as much as the pattern is
//       bright there. Where the pattern is black, the cell stays as the rest has it.
//  3. Solo, each light's limit, the master and blackout act on the result, on light only.
//  4. The attributes come from the fader that took them last; a movement effect moves a head from
//     there (or around a center of its own), its fader scaling the size. A head never jumps through
//     the master, blackout or a solo. Two movements: the one started last takes each axis it uses.
//     A head an intensity effect lights gets its shutter opened; a color effect turns a color wheel.
//
// A playback: raising its fader from zero starts the effect, bringing it to zero stops it. The fader
// says how much of the effect there is (for a movement around a center of its own, also how far the
// heads have gone there). Its key moves the fader: up to full in the effect's fade-in time, and down
// to zero in its fade-out time. The stack moves the fader in its step times, like a memory fader.
internal sealed partial class Desk
{
    /// <summary>Number of independent keys for effects. Each is bound to one effect, which the key starts and stops.</summary>
    public const int EffectIndependentCount = 32;

    // The effects of the project, by number; null where there is none.
    private readonly Effect?[] _effects = new Effect?[Effect.MaxEffects];

    // The playback of each effect, also of a number without an effect.
    private readonly EffectRun[] _runs = [.. Enumerable.Range(0, Effect.MaxEffects).Select(_ => new EffectRun())];

    // The lights of each effect, made when first needed and anew when the plan or the effect changes.
    private readonly EffectRig?[] _rigs = new EffectRig?[Effect.MaxEffects];

    // The effect each effect key of the independents is bound to.
    private readonly int[] _effectIndependents = DefaultEffectIndependents();

    // Counts the starts of effects: the one started last counts most.
    private long _effectStarts;

    /// <summary>True while an effect runs: then the UIs get the playback on every round of the clock.</summary>
    public bool PlaysEffects
    {
        get
        {
            lock (_lock)
            {
                return EffectsRun();
            }
        }
    }

    /// <summary>The bindings of the effect keys of the independents on a new desk: key 1 on effect 1, and so on.</summary>
    public static int[] DefaultEffectIndependents()
    {
        return [.. Enumerable.Range(1, EffectIndependentCount)];
    }

    /// <summary>The effects of the project, in the order of their numbers.</summary>
    public Effect[] GetEffects()
    {
        lock (_lock)
        {
            return [.. _effects.OfType<Effect>()];
        }
    }

    /// <summary>
    /// Stores an effect under its number, a new one or in place of the one there. Its playback
    /// takes the new speed, size and spread; one that runs goes on.
    /// </summary>
    /// <param name="effect">The effect.</param>
    /// <returns>The effect as the desk keeps it.</returns>
    /// <exception cref="DeskException">Something about it cannot be used, or the stack is live.</exception>
    public Effect SetEffect(Effect? effect)
    {
        Effect checkedEffect = (effect ?? throw new DeskException("The effect is missing.")).Checked();

        lock (_lock)
        {
            RequireUnlocked();
            int index = checkedEffect.Number - 1;
            if (_effects[index] is { } old && old.SameAs(checkedEffect))
                return old;

            double now = Now;
            _effects[index] = checkedEffect;
            _rigs[index] = null;
            TakeLiveValues(_runs[index], checkedEffect, now);
            _projectChanges++;
            _outputChanges++;
            return checkedEffect;
        }
    }

    /// <summary>Takes an effect out of the project. Its playback stops; an effect key bound to it starts nothing.</summary>
    /// <param name="number">The effect, 1-64.</param>
    /// <returns>True when there was one.</returns>
    /// <exception cref="DeskException">The stack is live.</exception>
    public bool DeleteEffect(int number)
    {
        RequireEffectNumber(number);

        lock (_lock)
        {
            RequireUnlocked();
            if (_effects[number - 1] is null)
                return false;

            _effects[number - 1] = null;
            _rigs[number - 1] = null;
            _runs[number - 1].Running = false;
            _runs[number - 1].Level = 0;
            _projectChanges++;
            _outputChanges++;
            return true;
        }
    }

    /// <summary>Stores a copy of an effect under another number, in place of what is there.</summary>
    /// <param name="from">The effect to copy, 1-64.</param>
    /// <param name="to">The number for the copy, 1-64.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="DeskException">There is no effect to copy, both numbers are the same, or the stack is live.</exception>
    public Effect CopyEffect(int from, int to)
    {
        RequireEffectNumber(from);
        RequireEffectNumber(to);
        if (from == to)
            throw new DeskException($"Effect {from} cannot be copied onto itself.");

        Effect source;
        lock (_lock)
        {
            source = _effects[from - 1] ?? throw new DeskException($"There is no effect {from} to copy.");
        }

        return SetEffect(source with { Number = to });
    }

    /// <summary>Stores the live speed, size and spread of an effect's playback in the effect.</summary>
    /// <param name="number">The effect, 1-64.</param>
    /// <returns>The effect as it is now.</returns>
    /// <exception cref="DeskException">There is no such effect, or the stack is live.</exception>
    public Effect KeepEffect(int number)
    {
        RequireEffectNumber(number);

        Effect effect;
        EffectRun run;
        lock (_lock)
        {
            effect = _effects[number - 1] ?? throw new DeskException($"There is no effect {number}.");
            run = _runs[number - 1];
        }

        return SetEffect(effect with { Speed = run.Speed, Size = run.Size, Spread = run.Spread });
    }

    /// <summary>Binds an effect key of the independents to an effect.</summary>
    /// <param name="independent">Number of the key, 1-32.</param>
    /// <param name="effect">The effect the key starts and stops from now on, 1-64.</param>
    /// <exception cref="DeskException">The stack is live.</exception>
    public void SetEffectIndependent(int independent, int effect)
    {
        RequireRange("effect independent", independent, 1, EffectIndependentCount);
        RequireEffectNumber(effect);

        lock (_lock)
        {
            Bind(_effectIndependents, independent, effect);
        }
    }

    /// <summary>
    /// Works an effect's playback, as its key, its fader or its live controls do, and as anything
    /// outside the desk may (a MIDI controller): "effect 3 speed = 120 BPM" is
    /// <c>SetEffectControl(3, "speed", 120)</c>. It is no part of the project, so it works while the
    /// stack is live, and undo leaves it alone.
    /// </summary>
    /// <param name="number">The effect, 1-64.</param>
    /// <param name="control">
    /// "go": 1 brings the fader up to full in the effect's fade-in time, which starts it; 0 brings it
    /// down to zero in the fade-out time, which stops it. "toggle": down when the effect runs and is
    /// not already on its way down, else up. "level": its fader, 0 to 100 per cent, at once; from
    /// zero it starts the effect, at zero it stops it. "speed": in cycles a minute (BPM). "size" and
    /// "spread": 0 to 100 per cent. "reset": the speed, size and spread of the effect again.
    /// </param>
    /// <param name="value">The value; for "toggle" and "reset" any.</param>
    /// <returns>The playback after the change.</returns>
    /// <exception cref="DeskException">No such control, the value is out of range, or there is no such effect to start.</exception>
    public EffectPlayback SetEffectControl(int number, string? control, double value)
    {
        RequireEffectNumber(number);
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new DeskException("The value must be a number.");

        lock (_lock)
        {
            double now = Now;
            EffectRun run = _runs[number - 1];
            Effect? effect = _effects[number - 1];
            switch (control)
            {
                case "go":
                    Go(number, value >= 0.5, now);
                    break;
                case "toggle":
                    Go(number, !run.Running || (run.Moving && run.MoveTo == 0), now);
                    break;
                case "level":
                    if (!(value >= 0 && value <= 100))
                        throw new DeskException("The level of an effect goes from 0 to 100 per cent.");
                    MoveEffectFader(number, (byte)Math.Round(value * MaxLevel / 100, MidpointRounding.AwayFromZero), now);
                    _moves.Remove(new Fader(Memory: false, number, Effect: true));   // the hand wins over the stack
                    break;
                case "speed":
                    if (!(value >= Effect.MinSpeed && value <= Effect.MaxSpeed))
                        throw new DeskException(FormattableString.Invariant($"The speed of an effect goes from {Effect.MinSpeed} to {Effect.MaxSpeed} BPM."));
                    Rebase(run, now, () => run.Speed = Math.Round(value, 1));
                    break;
                case "size":
                    run.Size = Percent(value, "size");
                    break;
                case "spread":
                    run.Spread = Percent(value, "spread");
                    break;
                case "reset":
                    if (effect is not null)
                        TakeLiveValues(run, effect, now);
                    break;
                default:
                    throw new DeskException($"An effect has the controls go, toggle, level, speed, size, spread and reset, not \"{control}\".");
            }

            _outputChanges++;
            return PlaybackOf(number, now);
        }
    }

    /// <summary>The playback of one effect.</summary>
    /// <param name="number">The effect, 1-64.</param>
    public EffectPlayback GetEffectPlayback(int number)
    {
        RequireEffectNumber(number);
        lock (_lock)
        {
            return PlaybackOf(number, Now);
        }
    }

    /// <summary>
    /// One cycle of an effect, sampled for the preview of the Effects view, at its full level: the
    /// effect as given, whether or not it is stored. A movement starts from where each head points now.
    /// </summary>
    /// <param name="effect">The effect.</param>
    /// <exception cref="DeskException">Something about it cannot be used.</exception>
    public EffectPreview PreviewEffect(Effect? effect)
    {
        Effect shown = (effect ?? throw new DeskException("The effect is missing.")).Checked();

        lock (_lock)
        {
            const int frames = 48;
            EffectRig rig = EffectRig.Of(_stage, shown);
            var items = new List<EffectPreviewItem>();
            double spread = shown.Spread / 100.0;
            if (shown.IsPixel)
            {
                foreach (EffectPixel pixel in rig.Pixels)
                {
                    double[] levels = new double[frames];
                    string[] colors = new string[frames];
                    for (int frame = 0; frame < frames; frame++)
                    {
                        (EffectEngine.Rgb color, double bright) = EffectEngine.Pattern(shown, rig, pixel, (double)frame / frames, shown.Size);
                        levels[frame] = Math.Round(bright, 3);
                        colors[frame] = color.Hex;
                    }

                    items.Add(new EffectPreviewItem(pixel.Target.Light.Id, pixel.Cell, pixel.X, pixel.Y) { Levels = levels, Colors = colors });
                }

                return new EffectPreview(shown.Kind, frames, [.. items]);
            }

            byte[] where = shown.IsMovement ? WhereHeadsPoint(Now) : [];
            foreach (EffectTarget target in rig.Targets)
            {
                int light = target.Light.Id;
                var item = new EffectPreviewItem(light, -1, target.Light.X, target.Light.Y);
                double PhaseAt(int frame) => EffectEngine.PhaseOf((double)frame / frames, target.Place, spread, shown.Backwards);
                switch (shown.Kind)
                {
                    case Effect.MovementKind:
                        {
                            double[] pans = new double[frames], tilts = new double[frames];
                            (double basePan, double baseTilt) = (AxisOf(where, target.Pan, target.PanFine), AxisOf(where, target.Tilt, target.TiltFine));
                            for (int frame = 0; frame < frames; frame++)
                            {
                                (double pan, double tilt) = Moved(shown, target, PhaseAt(frame), basePan, baseTilt, size: shown.Size, level: 1);
                                pans[frame] = Math.Round(pan, 4);
                                tilts[frame] = Math.Round(tilt, 4);
                            }

                            item = item with { Pan = pans, Tilt = tilts };
                            break;
                        }

                    case Effect.IntensityKind:
                        item = item with { Levels = [.. Enumerable.Range(0, frames).Select(frame => Math.Round(EffectEngine.Intensity(shown, PhaseAt(frame), light, shown.Size), 3))] };
                        break;
                    default:
                        item = item with { Colors = [.. Enumerable.Range(0, frames).Select(frame => EffectEngine.ColorOf(shown, PhaseAt(frame), shown.Size).Hex)] };
                        break;
                }

                items.Add(item);
            }

            return new EffectPreview(shown.Kind, frames, [.. items]);
        }
    }

    // Call with the lock held. The playbacks for the UIs: of every effect the project has, and of any
    // other number whose fader is up.
    private EffectPlayback[] EffectPlaybacksNow(double now)
    {
        var playbacks = new List<EffectPlayback>();
        for (int index = 0; index < Effect.MaxEffects; index++)
        {
            if (_effects[index] is not null || _runs[index].Level > 0)
                playbacks.Add(PlaybackOf(index + 1, now));
        }

        return [.. playbacks];
    }

    // Call with the lock held. What the running effects give the channels they touch, mixed with the
    // rest as in GetOutput, for the UIs; null while none runs.
    private EffectLevels? EffectLevelsForUi(double now)
    {
        if (EffectFrameAt(now) is not { } effects)
            return null;

        var touched = new HashSet<int>();
        byte[] levels = FadersOutput(now, []);
        effects.PutLight(levels, touched);
        if (_roles.AttributeLights.Length > 0)
            PutAttributes(levels, now, leftOut: null);
        effects.PutAttributes(levels, touched);

        int[] channels = [.. touched.Where(channel => channel < Channels).Order()];
        return new EffectLevels([.. channels.Select(channel => channel + 1)], [.. channels.Select(channel => (int)levels[channel])]);
    }

    // Call with the lock held. Where the heads point without effects: the attributes as the faders give them.
    private byte[] WhereHeadsPoint(double now)
    {
        byte[] levels = FadersOutput(now, []);
        if (_roles.AttributeLights.Length > 0)
            PutAttributes(levels, now, leftOut: null);
        return levels;
    }

    // Call with the lock held. True while an effect runs.
    private bool EffectsRun()
    {
        return _runs.Any(run => run.Running);
    }

    // Call with the lock held. Moves the faders the keys are moving to where they belong at "now";
    // a fader that has come down to zero stops its effect. The UIs hear of every fader moved. True
    // while an effect runs.
    private bool SettleEffects(double now)
    {
        // Times are sums of fractions of seconds, which a computer does not add exactly: a fade of
        // 4 s can seem to be 3.9999999 s old at its end. A millionth of a second settles that.
        now += 0.000001;
        bool any = false;
        for (int index = 0; index < _runs.Length; index++)
        {
            EffectRun run = _runs[index];
            if (run.Moving)
            {
                byte level = (byte)Math.Round(LevelAt(run, now), MidpointRounding.AwayFromZero);
                if (now - run.MoveStart >= run.MoveTime)
                {
                    level = run.MoveTo;
                    run.Moving = false;
                    if (level == 0)
                        run.Running = false;
                    _outputChanges++;
                }

                if (level != run.Level || !run.Moving)
                {
                    run.Level = level;
                    _moved.RemoveAll(moved => moved.Effect && moved.Number == index + 1);
                    _moved.Add(new FaderMoved(Memory: false, index + 1, level) { Effect = true });
                }
            }

            any |= run.Running;
        }

        return any;
    }

    // Call with the lock held. Every effect stops at once and its fader goes to zero: Clear, a
    // project loaded, the stack going live.
    private void StopEffects()
    {
        foreach (EffectRun run in _runs)
        {
            run.Running = false;
            run.Moving = false;
            run.Level = 0;
        }
    }

    // Call with the lock held. The effects and the effect keys of a project take the place of the
    // desk's. A playback whose effect changed takes its speed, size and spread; one whose effect is
    // gone stops, its fader at zero.
    private void PutEffects(Effect?[] effects, int[] independents)
    {
        double now = Now;
        for (int index = 0; index < Effect.MaxEffects; index++)
        {
            Effect? old = _effects[index];
            Effect? effect = index < effects.Length ? effects[index] : null;
            _effects[index] = effect;
            _rigs[index] = null;
            if (effect is null)
            {
                _runs[index].Running = false;
                _runs[index].Moving = false;
                _runs[index].Level = 0;
            }
            else if (old is null || !old.SameAs(effect))
            {
                TakeLiveValues(_runs[index], effect, now);
            }
        }

        independents.CopyTo(_effectIndependents, 0);
    }

    // Call with the lock held. The plan changed: the lights of every effect are found anew.
    private void ForgetEffectRigs()
    {
        Array.Clear(_rigs);
    }

    // Call with the lock held. The key of an effect: moves its fader up to full in the effect's
    // fade-in time, which starts it, or down to zero in its fade-out time, which stops it. A fader
    // already on its way goes on from where it is, at the same pace: a fade of 4 s from zero to
    // full takes 2 s from half way.
    private void Go(int number, bool start, double now)
    {
        EffectRun run = _runs[number - 1];
        if (_effects[number - 1] is not { } effect)
        {
            if (start)
                throw new DeskException($"There is no effect {number} to start.");
            return;
        }

        byte to = start ? (byte)MaxLevel : (byte)0;
        double from = LevelAt(run, now);
        double seconds = (start ? effect.FadeIn : effect.FadeOut) * Math.Abs(to - from) / MaxLevel;
        if (seconds <= 0)
        {
            MoveEffectFader(number, to, now);
            return;
        }

        if (start && !run.Running)
            Start(run, now);

        run.Moving = true;
        run.MoveFrom = from;
        run.MoveTo = to;
        run.MoveStart = now;
        run.MoveTime = seconds;
    }

    // Call with the lock held. Every move of an effect's fader at once goes through here, by hand or
    // by the stack (a move the key began ends): a fader that leaves zero starts the effect, one that
    // comes to zero stops it.
    private void MoveEffectFader(int number, byte level, double now)
    {
        EffectRun run = _runs[number - 1];
        run.Moving = false;
        if (level > 0 && !run.Running && _effects[number - 1] is not null)
            Start(run, now);
        else if (level == 0)
            run.Running = false;

        run.Level = level;
    }

    // Call with the lock held. An effect that starts begins its cycle, and takes what it moves from
    // the effects that started before it.
    private void Start(EffectRun run, double now)
    {
        run.Running = true;
        run.Claim = ++_effectStarts;
        run.PhaseBase = 0;
        run.PhaseAt = now;
    }

    // Call with the lock held. The playback takes the effect's speed, size and spread, going on from where it is.
    private void TakeLiveValues(EffectRun run, Effect effect, double now)
    {
        Rebase(run, now, () => run.Speed = effect.Speed);
        run.Size = effect.Size;
        run.Spread = effect.Spread;
    }

    // Call with the lock held. Something that changes the speed of an effect: the effect stands where
    // it stands, and from here on its time runs at the new speed.
    private void Rebase(EffectRun run, double now, Action change)
    {
        double phase = PhaseOf(run, now);
        change();
        run.PhaseBase = phase;
        run.PhaseAt = now;
    }

    // Call with the lock held, around a change of the rate: every effect goes on from where it is.
    private void RebaseEffects(double now, Action change)
    {
        double[] phases = [.. _runs.Select(run => PhaseOf(run, now))];
        change();
        for (int index = 0; index < _runs.Length; index++)
        {
            _runs[index].PhaseBase = phases[index];
            _runs[index].PhaseAt = now;
        }
    }

    // How many cycles the effect has run: at its live speed, and while RATE is lit at that speed
    // times the tempo over 120 BPM (at 120 nothing changes; an effect at 120 BPM then takes one cycle
    // per beat).
    private double PhaseOf(EffectRun run, double now)
    {
        return run.PhaseBase + ((now - run.PhaseAt) * CyclesPerMinute(run) / 60);
    }

    private double CyclesPerMinute(EffectRun run)
    {
        return _rateOn ? run.Speed * _bpm / DefaultBpm : run.Speed;
    }

    // Where an effect's fader stands, 0 to 255: on its way while its key moves it, between.
    private static double LevelAt(EffectRun run, double now)
    {
        if (!run.Moving)
            return run.Level;

        double done = run.MoveTime <= 0 ? 1 : Math.Clamp((now - run.MoveStart) / run.MoveTime, 0, 1);
        return run.MoveFrom + ((run.MoveTo - run.MoveFrom) * done);
    }

    private EffectPlayback PlaybackOf(int number, double now)
    {
        EffectRun run = _runs[number - 1];
        Effect? effect = _effects[number - 1];
        byte level = (byte)Math.Round(LevelAt(run, now), MidpointRounding.AwayFromZero);
        return new EffectPlayback(number, run.Running, level, run.Speed, run.Size, run.Spread)
        {
            Fading = run.Moving,
            Phase = Math.Round(run.Running ? PhaseOf(run, now) : 0, 4),
            Bpm = Math.Round(CyclesPerMinute(run), 2),
            Changed = effect is not null && (run.Speed != effect.Speed || run.Size != effect.Size || run.Spread != effect.Spread),
        };
    }

    // Call with the lock held. The effects that give something at "now", in the order they started,
    // with what they need for this frame; null when none does.
    private EffectFrame? EffectFrameAt(double now)
    {
        List<EffectNow>? running = null;
        for (int index = 0; index < Effect.MaxEffects; index++)
        {
            EffectRun run = _runs[index];
            if (!run.Running || _effects[index] is not { } effect)
                continue;

            double level = LevelAt(run, now) / MaxLevel;
            if (level <= 0)
                continue;

            EffectRig rig = _rigs[index] ??= EffectRig.Of(_stage, effect);
            (running ??= []).Add(new EffectNow(effect, rig, run.Claim, PhaseOf(run, now), level, run.Size, run.Spread / 100.0));
        }

        if (running is null)
            return null;

        running.Sort((left, right) => left.Claim.CompareTo(right.Claim));
        return new EffectFrame(running);
    }

    private static int Percent(double value, string what)
    {
        if (!(value >= 0 && value <= 100))
            throw new DeskException($"The {what} of an effect goes from 0 to 100 per cent.");

        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static void RequireEffectNumber(int number)
    {
        RequireRange("effect", number, 1, Effect.MaxEffects);
    }

    // Where a head points on one axis, 0 to 1: its coarse channel, with the fine one where it has one.
    private static double AxisOf(byte[] levels, int coarse, int fine)
    {
        if (coarse < 0 || coarse >= levels.Length)
            return 0.5;

        return fine < 0 || fine >= levels.Length
            ? levels[coarse] / 255.0
            : ((levels[coarse] * 256) + levels[fine]) / 65535.0;
    }

    // Where a movement takes a head, each axis 0 to 1: around where it points, or around the effect's
    // center (as far towards it as the fader is up: in the fade-in all the way), the shape scaled by
    // the size and the fader. An axis the shape does not use stays where it points.
    private static (double Pan, double Tilt) Moved(
        Effect effect, EffectTarget target, double phase, double basePan, double baseTilt, int size, double level)
    {
        (double offsetPan, double offsetTilt) = EffectEngine.Movement(effect, phase, target.Light.Id);
        double amount = size / 100.0 * level;
        double pan = basePan, tilt = baseTilt;
        if (effect.CenterPan is double centerPan && effect.CenterTilt is double centerTilt)
        {
            pan = basePan + ((centerPan / 100 - basePan) * level);
            tilt = baseTilt + ((centerTilt / 100 - baseTilt) * level);
        }

        if (EffectEngine.UsesPan(effect))
            pan += offsetPan * amount;
        if (EffectEngine.UsesTilt(effect))
            tilt += offsetTilt * amount;

        return (Math.Clamp(pan, 0, 1), Math.Clamp(tilt, 0, 1));
    }

    // Writes where a head points on one axis, in 16 bits where it has a fine channel.
    private static void PutAxis(byte[] output, int coarse, int fine, double value, HashSet<int>? touched)
    {
        if (coarse < 0 || coarse >= output.Length)
            return;

        if (fine < 0 || fine >= output.Length)
        {
            output[coarse] = (byte)Math.Round(value * 255, MidpointRounding.AwayFromZero);
            touched?.Add(coarse);
            return;
        }

        int whole = (int)Math.Round(value * 65535, MidpointRounding.AwayFromZero);
        output[coarse] = (byte)(whole >> 8);
        output[fine] = (byte)(whole & 0xFF);
        touched?.Add(coarse);
        touched?.Add(fine);
    }

    // The playback of an effect: none of it is stored in the project.
    private sealed class EffectRun
    {
        public bool Running;        // from its start until its fader is down at zero
        public byte Level;          // its fader, as the UIs last heard of it
        public bool Moving;         // its key moves the fader, from MoveFrom to MoveTo:
        public double MoveFrom;     // where the fader stood when the move began, 0 to 255
        public byte MoveTo;         // where it goes
        public double MoveStart;    // when the move began, in desk seconds
        public double MoveTime;     // seconds the move takes
        public double Speed = 30;   // its live speed, cycles a minute
        public int Size = 100;      // its live size, per cent
        public int Spread = 100;    // its live spread, per cent
        public double PhaseBase;    // the cycles it had run at PhaseAt
        public double PhaseAt;      // desk seconds
        public long Claim;          // when it started, on the count of starts
    }

    // An effect that gives something in this frame.
    private sealed record EffectNow(
        Effect Effect, EffectRig Rig, long Claim, double Cycles, double Level, int Size, double Spread);

    // The effects of one frame, and what they do to the lights: PutLight on the light the faders
    // give (step 2), PutAttributes once the attributes are in (step 4). "touched" collects the
    // channels they change, for the UIs.
    private sealed class EffectFrame(List<EffectNow> effects)
    {
        // The lights the light effects worked on in this frame, by their number in the plan.
        private readonly Dictionary<int, LightState> _lights = [];

        public void PutLight(byte[] output, HashSet<int>? touched)
        {
            foreach (EffectNow now in effects.Where(item => item.Effect.Kind == Effect.IntensityKind))
            {
                foreach (EffectTarget target in now.Rig.Targets)
                {
                    double phase = EffectEngine.PhaseOf(now.Cycles, target.Place, now.Spread, now.Effect.Backwards);
                    double lift = now.Level * EffectEngine.Intensity(now.Effect, phase, target.Light.Id, now.Size);
                    LightState light = StateOf(target, output);
                    light.Pulsed = true;   // also at the bottom of its wave: a color effect leaves its brightness alone
                    if (lift > 0)
                        light.Raise(lift);
                }
            }

            // The colors in the order the effects started: the one started last has the last word.
            foreach (EffectNow now in effects.Where(item => item.Effect.Kind is Effect.ColorKind or Effect.PixelKind))
            {
                double amount = now.Level;
                if (now.Effect.IsPixel)
                {
                    foreach (EffectPixel pixel in now.Rig.Pixels)
                    {
                        (EffectEngine.Rgb color, double bright) = EffectEngine.Pattern(now.Effect, now.Rig, pixel, now.Cycles, now.Size);
                        StateOf(pixel.Target, output).Paint(pixel.Cell, color, amount * bright);
                    }

                    continue;
                }

                foreach (EffectTarget target in now.Rig.Targets)
                {
                    double phase = EffectEngine.PhaseOf(now.Cycles, target.Place, now.Spread, now.Effect.Backwards);
                    StateOf(target, output).Tint(EffectEngine.ColorOf(now.Effect, phase, now.Size), amount);
                }
            }

            foreach (LightState light in _lights.Values)
                light.Put(output, touched);
        }

        public void PutAttributes(byte[] output, HashSet<int>? touched)
        {
            foreach (LightState light in _lights.Values)
                light.PutAttributes(output, touched);

            // Every movement from where the head points without effects; the one started last
            // takes each axis it uses.
            Dictionary<int, (double Pan, double Tilt)> where = [];
            foreach (EffectNow now in effects.Where(item => item.Effect.IsMovement))
            {
                foreach (EffectTarget target in now.Rig.Targets)
                {
                    if (!where.TryGetValue(target.Light.Id, out (double Pan, double Tilt) start))
                        where[target.Light.Id] = start = (AxisOf(output, target.Pan, target.PanFine), AxisOf(output, target.Tilt, target.TiltFine));

                    double phase = EffectEngine.PhaseOf(now.Cycles, target.Place, now.Spread, now.Effect.Backwards);
                    (double pan, double tilt) = Moved(now.Effect, target, phase, start.Pan, start.Tilt, now.Size, now.Level);
                    if (EffectEngine.UsesPan(now.Effect))
                        PutAxis(output, target.Pan, target.PanFine, pan, touched);
                    if (EffectEngine.UsesTilt(now.Effect))
                        PutAxis(output, target.Tilt, target.TiltFine, tilt, touched);
                }
            }
        }

        private LightState StateOf(EffectTarget target, byte[] output)
        {
            if (!_lights.TryGetValue(target.Light.Id, out LightState? light))
                _lights[target.Light.Id] = light = new LightState(target, output);

            return light;
        }
    }

    // A light while the effects of a frame work on it: what the rest gives it, read from the output
    // once, and what the effects make of it, written back once.
    private sealed class LightState
    {
        private readonly EffectTarget _target;
        private readonly double _master;        // the dimmer of all its cells, 0-1; -1 for none
        private readonly CellState[] _cells;
        private double _gel;                     // a light without color cells: its dimmers, 0-1
        private readonly double _gelBefore;
        private bool _lit;                       // an effect gave it light: a closed shutter opens
        private EffectEngine.Rgb? _wheel;        // the color its color wheel turns to

        public LightState(EffectTarget target, byte[] output)
        {
            _target = target;
            _master = target.Masters.Length > 0 ? MaxOf(output, target.Masters) : -1;
            _gel = _gelBefore = Math.Max(0, _master);
            _cells = new CellState[target.Cells.Length];
            for (int index = 0; index < _cells.Length; index++)
            {
                EffectCell cell = target.Cells[index];
                double own = cell.Dimmers.Length > 0 ? MaxOf(output, cell.Dimmers) : -1;
                double strength = MaxOf(output, cell.Colors);
                double[]? shares = strength > 0 ? [.. cell.Colors.Select(channel => output[channel] / 255.0 / strength)] : null;

                // A cell with a dimmer (its own, or the light's) is as bright as its dimmer says; its
                // colors are its color, at a strength of their own. Without a dimmer, the colors are both.
                bool dimmed = _master >= 0 || own >= 0;
                double brightness = dimmed ? (_master >= 0 ? _master : 1) * (own >= 0 ? own : 1) : strength;
                _cells[index] = new CellState(own, dimmed, strength, brightness, shares);
            }
        }

        // An intensity: every cell at least this bright, 0-1; a cell without a color comes up white.
        public void Raise(double lift)
        {
            _lit = true;
            if (_cells.Length == 0)
            {
                _gel = Math.Max(_gel, lift);
                return;
            }

            for (int index = 0; index < _cells.Length; index++)
                RaiseCell(index, lift);
        }

        // A color effect lights a light the rest leaves dark, as bright as its fader, and leaves a lit
        // light as bright as it is. Between dark and this brightness the two blend, so that a fade of
        // the rest through dark has no jump: the effect's light gives way as the rest's comes.
        private const double DarkBelow = 16 / 255.0;

        /// <summary>True when an intensity effect plays the light in this frame: it gives the brightness.</summary>
        public bool Pulsed { get; set; }

        // A color: every cell takes over this much of it, 0-1. A cell the rest leaves dark comes up in
        // the color alone, as bright as "amount", unless an intensity effect plays the light (that one
        // gives the brightness); a lit one keeps its brightness. A color wheel turns at half.
        public void Tint(EffectEngine.Rgb color, double amount)
        {
            if (amount <= 0)
                return;

            // the light the effect gives itself opens a closed shutter, as an intensity effect's does
            bool gave = false;
            for (int index = 0; index < _cells.Length; index++)
            {
                double before = _cells[index].Brightness;
                TintCell(index, color, amount);
                if (Pulsed)
                    continue;

                double lifted = LiftOver(before, amount);
                gave |= lifted > before;
                LiftCell(index, lifted);
            }

            if (_cells.Length == 0)
            {
                if (_target.Wheel >= 0 && amount >= 0.5)
                    _wheel = color;
                if (!Pulsed)
                {
                    double lifted = LiftOver(_gel, amount);
                    gave |= lifted > _gel;
                    _gel = lifted;
                }
            }

            _lit |= gave;
        }

        // How bright a color effect makes a light that is "before" bright without it.
        private static double LiftOver(double before, double amount)
        {
            return before >= DarkBelow ? before : Math.Max(before, amount * (1 - (before / DarkBelow)));
        }

        // A pixel: the cell (-1 for the whole of a light without cells) at least this bright, and
        // its color taken over as much.
        public void Paint(int cell, EffectEngine.Rgb color, double amount)
        {
            if (amount <= 0)
                return;

            _lit = true;
            if (cell < 0)
            {
                _gel = Math.Max(_gel, amount);
                return;
            }

            RaiseCell(cell, amount);
            TintCell(cell, color, amount);
        }

        // Writes the light back into the output, the cells that changed.
        public void Put(byte[] output, HashSet<int>? touched)
        {
            if (_cells.Length == 0)
            {
                if (_gel != _gelBefore)
                    PutAll(output, _target.Masters, _gel, touched);
                return;
            }

            if (!_cells.Any(cell => cell.Changed))
                return;

            if (_master >= 0)
            {
                // One dimmer for all cells: it stays where the rest has it unless a cell needs more,
                // and the colors of each cell give the rest of its brightness.
                double master = Math.Max(_master, _cells.Max(cell => cell.Brightness));
                PutAll(output, _target.Masters, master, touched);
                for (int index = 0; index < _cells.Length; index++)
                {
                    CellState state = _cells[index];
                    if (state.Changed || master != _master)
                        PutColors(output, index, state.ColorStrength * (master > 0 ? state.Brightness / master : 0), touched);
                }

                return;
            }

            for (int index = 0; index < _cells.Length; index++)
            {
                CellState state = _cells[index];
                if (!state.Changed)
                    continue;

                if (!state.Dimmed)
                {
                    PutColors(output, index, state.Brightness, touched);
                    continue;
                }

                // Its own dimmer gives the brightness; the colors keep their strength.
                PutAll(output, _target.Cells[index].Dimmers, state.Brightness, touched);
                PutColors(output, index, state.ColorStrength, touched);
            }
        }

        // The attributes the light effects ask for: a closed shutter of a lit head opens, a color wheel turns.
        public void PutAttributes(byte[] output, HashSet<int>? touched)
        {
            if (_lit && _target.Shutter >= 0 && _target.Shutter < output.Length && IsClosed(output[_target.Shutter]))
            {
                output[_target.Shutter] = (byte)_target.OpenValue;
                touched?.Add(_target.Shutter);
            }

            if (_wheel is { } color && _target.Wheel >= 0 && _target.Wheel < output.Length)
            {
                output[_target.Wheel] = (byte)FixtureLooks.Nearest(_target.WheelRanges, color.Scaled).From;
                touched?.Add(_target.Wheel);
            }
        }

        private bool IsClosed(byte value)
        {
            FixtureRange? range = _target.ShutterRanges.FirstOrDefault(candidate => value >= candidate.From && value <= candidate.To);
            return range is not null && range.Label.StartsWith("Closed", StringComparison.OrdinalIgnoreCase);
        }

        private void RaiseCell(int index, double lift)
        {
            CellState state = _cells[index];
            if (lift <= state.Brightness)
                return;

            state.Brightness = lift;
            state.Shares ??= SharesOf(index, EffectEngine.Rgb.White);
            state.Changed = true;
        }

        // A cell at least this bright, in the color it has.
        private void LiftCell(int index, double lift)
        {
            CellState state = _cells[index];
            if (lift <= state.Brightness)
                return;

            state.Brightness = lift;
            state.Changed = true;
        }

        private void TintCell(int index, EffectEngine.Rgb color, double amount)
        {
            CellState state = _cells[index];
            double[] wanted = SharesOf(index, color);
            if (state.Shares is not { } shares)
            {
                state.Shares = wanted;
            }
            else
            {
                double[] mixed = new double[shares.Length];
                for (int channel = 0; channel < mixed.Length; channel++)
                    mixed[channel] = shares[channel] + ((wanted[channel] - shares[channel]) * Math.Min(1, amount));

                double strongest = mixed.Max();
                state.Shares = strongest > 0 ? [.. mixed.Select(share => share / strongest)] : null;
            }

            state.Changed = true;
        }

        // The color channels of a cell: the color mixed as the Fixtures view mixes it, the strongest at "strength", 0-1.
        private void PutColors(byte[] output, int index, double strength, HashSet<int>? touched)
        {
            int[] channels = _target.Cells[index].Colors;
            double[]? shares = _cells[index].Shares;
            for (int at = 0; at < channels.Length; at++)
            {
                double value = (shares?[at] ?? 0) * Math.Clamp(strength, 0, 1) * 255;
                output[channels[at]] = (byte)Math.Round(value, MidpointRounding.AwayFromZero);
                touched?.Add(channels[at]);
            }
        }

        private double[] SharesOf(int index, EffectEngine.Rgb color)
        {
            return FixtureLooks.MixCell(_target.Light.Channels, _target.Cells[index].Offsets, color.Scaled);
        }

        private static void PutAll(byte[] output, int[] channels, double level, HashSet<int>? touched)
        {
            var value = (byte)Math.Round(Math.Clamp(level, 0, 1) * 255, MidpointRounding.AwayFromZero);
            foreach (int channel in channels)
            {
                output[channel] = value;
                touched?.Add(channel);
            }
        }

        private static double MaxOf(byte[] output, int[] channels)
        {
            double highest = 0;
            foreach (int channel in channels)
                highest = Math.Max(highest, output[channel] / 255.0);

            return highest;
        }
    }

    // A cell of a light while the effects of a frame work on it. "Own": its own dimmer as the rest
    // gives it, 0-1, -1 for none; "Dimmed": true when a dimmer (its own or the light's) makes it
    // bright; "Strength": its strongest color channel, 0-1; "Brightness": how bright it is, 0-1 (its
    // dimmers, or without a dimmer its colors); "Shares": its color channels, the strongest at 1,
    // null for no color.
    private sealed class CellState(double own, bool dimmed, double strength, double brightness, double[]? shares)
    {
        public double Own { get; } = own;

        public bool Dimmed { get; } = dimmed;

        public double Strength { get; } = strength;

        public double Brightness { get; set; } = brightness;

        public double[]? Shares { get; set; } = shares;

        public bool Changed { get; set; }

        // How strong the colors of a cell with a dimmer are: as the rest has them, or full for a cell
        // that had no color (it comes up white, or in an effect's color).
        public double ColorStrength => Strength > 0 ? Strength : 1;
    }
}
