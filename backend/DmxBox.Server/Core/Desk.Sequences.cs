namespace DmxBox.Server.Core;

// The part of the desk that deals with sequences: a memory can hold one in place of a look.
// A sequence runs while the fader of its memory is up, from the moment the fader leaves zero.
// It plays itself the way the stack is played (steps that add, releases, effects, sounds; see
// SequenceTimeline), but it is a layer like a look: scaled by the fader, merged highest-wins, and
// it moves no fader, an effect's or a sound's neither (Desk.Effects.cs, Desk.Sounds.cs).
internal sealed partial class Desk
{
    // The sequence stored in each memory. Null where the memory holds a look or nothing.
    private readonly Sequence?[] _sequences = new Sequence?[MemoryCount];

    // The round and the step each sequence played last as the sounds saw it: a step of a sound that
    // starts plays its sound again when the sound is over (AddSequenceSounds).
    private readonly (long Round, int Step)[] _soundStepsSeen = [.. Enumerable.Repeat((-1L, -1), MemoryCount)];

    // The moment each sequence was last worked out for, with what it was worked out from: the
    // output of one frame needs it several times (the light, the effects, the attributes).
    private readonly KnownMoment?[] _moments = new KnownMoment?[MemoryCount];

    // When the sequence of each memory started, in desk seconds, as if it had always run at the
    // speed it has now: then the time of the sequence is (now - start) x speed. A change of the
    // speed moves this, so that the sequence goes on from where it is.
    private readonly double[] _sequenceStarts = new double[MemoryCount];

    // The seed of each start, which chooses the orders of a sequence played at random: every
    // start plays other orders.
    private readonly int[] _sequenceSeeds = new int[MemoryCount];

    // The rate: a tempo for all sequences, in beats per minute. While RATE is lit every sequence
    // takes one step per beat (its own speed counts the steps of a beat); dark, every sequence
    // plays its own times. Like the master fader it belongs to the evening, not to the project.
    private double _bpm = DefaultBpm;
    private bool _rateOn;

    /// <summary>The slowest tempo of the rate, in beats per minute.</summary>
    public const double MinBpm = 30;

    /// <summary>The fastest tempo of the rate, in beats per minute.</summary>
    public const double MaxBpm = 300;

    /// <summary>The tempo of the rate on a new desk.</summary>
    public const double DefaultBpm = 120;

    /// <summary>
    /// Stores a sequence in a memory that is empty or holds a sequence already, played in turn
    /// and at its own speed. See <see cref="SetSequence(int, Sequence?)"/>.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="loop">True to start again after the last step, false to play once.</param>
    /// <param name="steps">The steps, at most 99. None at all is allowed: such a sequence gives nothing.</param>
    /// <exception cref="DeskException">The memory holds a look, a step is out of range, or the stack is live.</exception>
    public void SetSequence(int memory, bool loop, Step[]? steps)
    {
        SetSequence(memory, new Sequence(loop, steps!));
    }

    /// <summary>
    /// Stores a sequence in a memory that is empty or holds a sequence already: its steps, and
    /// how it is played. If the memory's fader is up, the sequence starts from its first step.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="sequence">The sequence: at most 99 steps (none at all is allowed: such a sequence gives nothing), and a speed from 0.1 to 10.</param>
    /// <exception cref="DeskException">
    /// The memory holds a look, a step or the speed is out of range, a release names itself or a
    /// step the sequence does not have, or the stack is live.
    /// </exception>
    public void SetSequence(int memory, Sequence? sequence)
    {
        RequireMemory(memory);
        if (sequence is null)
            throw new DeskException("The sequence is missing.");

        Step[] steps = sequence.Steps ?? [];
        if (steps.Length > Sequence.MaxSteps)
            throw TooManySteps();

        Sequence checkedSequence = sequence with
        {
            Steps = Array.ConvertAll(steps, step => (step ?? throw new DeskException("A step is missing.")).Checked()),
            Speed = Sequence.CheckedSpeed(sequence.Speed),
        };
        RequireReleasesFit(checkedSequence.Steps);

        lock (_lock)
        {
            RequireUnlocked();
            RequireNoLook(memory);
            Array.ForEach(checkedSequence.Steps, RequireStepFits);
            StoreSequence(memory, checkedSequence);
            CountLookChange();
        }
    }

    /// <summary>
    /// Skips a step of a sequence, or takes it back (Disabled): the sequence passes over a skipped
    /// step, which stays in the list. Unlike every other change of a sequence this works while the
    /// stack is live too, as a way out when something goes wrong; the step a running sequence plays
    /// just then cannot be skipped. A sequence that runs goes on: its rounds are shorter or longer
    /// from now on.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="step">The step, counted from 1.</param>
    /// <param name="disabled">True to skip it.</param>
    /// <returns>True when the step changed.</returns>
    /// <exception cref="DeskException">The memory holds no sequence with that step, or the step plays just now.</exception>
    public bool SetSequenceStepDisabled(int memory, int step, bool disabled)
    {
        RequireMemory(memory);
        lock (_lock)
        {
            if (_sequences[memory - 1] is not { } sequence)
                throw new DeskException($"Memory {memory} holds no sequence.");
            if (step < 1 || step > sequence.Steps.Length)
                throw new DeskException($"The sequence of memory {memory} has no step {step}.");
            if (sequence.Steps[step - 1].Disabled == disabled)
                return false;
            if (disabled && _memoryLevels[memory - 1] > 0 && MomentOf(memory - 1, sequence, Now).Current == step - 1)
                throw new DeskException($"Step {step} of the sequence in memory {memory} plays just now: it cannot be skipped now.");

            Step[] steps = [.. sequence.Steps];
            steps[step - 1] = steps[step - 1] with { Disabled = disabled };
            _sequences[memory - 1] = sequence with { Steps = steps };
            CountLookChange();
            return true;
        }
    }

    /// <summary>
    /// Changes how fast the sequence of a memory is played. A sequence that runs goes on from
    /// where it is, at the new speed: it does not start again.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="speed">From 0.1 (ten times slower) to 10 (ten times faster); 1 plays the times as they are.</param>
    /// <returns>The speed as the desk keeps it, in steps of 0.05.</returns>
    /// <exception cref="DeskException">The memory holds no sequence, the speed is out of range, or the stack is live.</exception>
    public double SetSequenceSpeed(int memory, double speed)
    {
        RequireMemory(memory);
        double checkedSpeed = Sequence.CheckedSpeed(speed);

        lock (_lock)
        {
            RequireUnlocked();
            Sequence sequence = _sequences[memory - 1] ?? throw new DeskException($"Memory {memory} holds no sequence.");
            if (sequence.Speed == checkedSpeed)
                return checkedSpeed;

            // The sequence stands where it stands; from here on its time runs at the new speed.
            double now = Now;
            double elapsed = SequenceTime(memory - 1, now);
            _sequences[memory - 1] = sequence with { Speed = checkedSpeed };
            _sequenceStarts[memory - 1] = now - elapsed / SpeedOf(memory - 1);
            _projectChanges++;
            _outputChanges++;
            return checkedSpeed;
        }
    }

    /// <summary>The rate: whether it is lit, and its tempo in beats per minute.</summary>
    public (bool On, double Bpm) Rate
    {
        get
        {
            lock (_lock)
            {
                return (_rateOn, _bpm);
            }
        }
    }

    /// <summary>
    /// Sets the rate, a tempo for all sequences: while it is lit, every sequence takes one step
    /// per beat, its steps keeping their proportions (on average a step and its wait last one
    /// beat), and a sequence's own speed counts the steps of a beat (2: two steps per beat). Dark,
    /// every sequence plays its own times. Effects run at their speed times the tempo over 120 BPM
    /// while it is lit. Every sequence and effect goes on from where it is. It is no part of the
    /// project, so it can be changed while the stack is live.
    /// </summary>
    /// <param name="on">True: the rate counts.</param>
    /// <param name="bpm">The tempo, from 30 to 300 beats per minute.</param>
    /// <exception cref="DeskException">The tempo is out of range.</exception>
    public void SetRate(bool on, double bpm)
    {
        // "Not a number" fails both comparisons, so it is refused as well. The numbers are written
        // with a point, as on the UI, whatever the language of the PC.
        if (!(bpm >= MinBpm && bpm <= MaxBpm))
            throw new DeskException(FormattableString.Invariant($"The tempo must be from {MinBpm} to {MaxBpm} beats per minute, not {bpm}."));

        lock (_lock)
        {
            if (on == _rateOn && bpm == _bpm)
                return;

            // Every sequence and every effect stands where it stands; from here on its time runs at the new speed.
            double now = Now;
            double[] elapsed = new double[MemoryCount];
            for (int index = 0; index < MemoryCount; index++)
                elapsed[index] = SequenceTime(index, now);

            RebaseEffects(now, () =>
            {
                _rateOn = on;
                _bpm = bpm;
            });
            for (int index = 0; index < MemoryCount; index++)
                _sequenceStarts[index] = now - elapsed[index] / SpeedOf(index);

            _outputChanges++;
        }
    }

    /// <summary>
    /// Puts one more step at the end of the sequence a memory holds. An empty memory gets a new
    /// sequence with this one step, played in a loop. This is what recording a sequence key by
    /// key does. If the memory's fader is up, the sequence starts from its first step.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="step">The step.</param>
    /// <exception cref="DeskException">
    /// The memory holds a look, the sequence is full, the step is out of range (a release: of a
    /// step the sequence does not have), or the stack is live.
    /// </exception>
    public void AddSequenceStep(int memory, Step? step)
    {
        RequireMemory(memory);
        Step checkedStep = (step ?? throw new DeskException("A step is missing.")).Checked();

        lock (_lock)
        {
            RequireUnlocked();
            RequireNoLook(memory);
            RequireStepFits(checkedStep);

            Sequence old = _sequences[memory - 1] ?? new Sequence(Sequence.DefaultLoop, []);
            if (old.Steps.Length >= Sequence.MaxSteps)
                throw TooManySteps();

            Step[] steps = [.. old.Steps, checkedStep];
            RequireReleasesFit(steps);
            StoreSequence(memory, old with { Steps = steps });
            CountLookChange();
        }
    }

    /// <summary>Copies the sequences of all memories that hold one, for the UI.</summary>
    /// <returns>For each such memory, by its number (1-128): its sequence.</returns>
    public Dictionary<int, Sequence> GetSequences()
    {
        lock (_lock)
        {
            var sequences = new Dictionary<int, Sequence>();
            for (int index = 0; index < MemoryCount; index++)
            {
                if (_sequences[index] is { } sequence)
                    sequences[index + 1] = sequence with { Steps = Copy(sequence.Steps) };
            }

            return sequences;
        }
    }

    // Call with the lock held. A sequence that is put into a memory whose fader is up starts now.
    private void StoreSequence(int memory, Sequence sequence)
    {
        _sequences[memory - 1] = sequence;
        StartSequence(memory - 1);
    }

    // Call with the lock held. The sequence of the memory at "index" starts now, with new
    // random orders if it is played at random.
    private void StartSequence(int index)
    {
        _sequenceStarts[index] = Now;
        _sequenceSeeds[index] = System.Random.Shared.Next();
        _soundStepsSeen[index] = (-1, -1);
    }

    // Call with the lock held. The time of the sequence at "index": seconds since its start,
    // counted at the speed it plays at.
    private double SequenceTime(int index, double now)
    {
        return (now - _sequenceStarts[index]) * SpeedOf(index);
    }

    // Call with the lock held. The speed the sequence at "index" plays at: its own; while the rate
    // is lit, so fast that its average step lasts one beat of the tempo, times its own speed (the
    // steps it takes in a beat).
    private double SpeedOf(int index)
    {
        Sequence? sequence = _sequences[index];
        double own = sequence?.Speed ?? 1;
        return _rateOn && sequence is not null ? own * SequenceTimeline.AverageStep(sequence) * _bpm / 60 : own;
    }

    // Call with the lock held. A look is not turned into a sequence unasked: its owner empties
    // the memory first.
    private void RequireNoLook(int memory)
    {
        if (_memories[memory - 1] is not null)
            throw new DeskException($"Memory {memory} holds a look. Empty it first.");
    }

    private static DeskException TooManySteps()
    {
        return new DeskException($"A sequence can have at most {Sequence.MaxSteps} steps.");
    }

    // A release of a sequence names another step of it by its number, from 1, or none (0).
    private static void RequireReleasesFit(Step[] steps)
    {
        for (int index = 0; index < steps.Length; index++)
        {
            Step step = steps[index];
            if (!step.IsRelease || step.Number == 0)
                continue;
            if (step.Number == index + 1)
                throw new DeskException($"Step {index + 1} cannot release itself. Choose another step of the sequence.");
            if (step.Number > steps.Length)
            {
                string has = steps.Length == 1 ? "1 step" : $"{steps.Length} steps";
                throw new DeskException($"Step {index + 1} releases step {step.Number}, but the sequence has {has}.");
            }
        }
    }

    // Call with the lock held. Every move of a memory fader goes through here, by hand or by
    // the stack: a fader that leaves zero starts the sequence of its memory, and takes the
    // attributes of the lights the memory holds (Desk.Attributes.cs).
    private void MoveMemoryFader(int memory, byte level)
    {
        if (_memoryLevels[memory - 1] == 0 && level > 0)
        {
            StartSequence(memory - 1);
            ClaimMemory(memory);
        }

        _memoryLevels[memory - 1] = level;
    }

    // Call with the lock held. Raises "levels" to what the running sequences give at "now".
    // "stepsOn", if given, receives the step each running sequence played last, counted from 1,
    // and "onSteps" the steps that are on in it. The memories in "leftOut" (held with SOLO DOWN)
    // give nothing.
    private void AddSequences(
        byte[] levels, double now, Dictionary<int, int>? stepsOn, HashSet<Fader>? leftOut = null, Dictionary<int, int[]>? onSteps = null)
    {
        for (int index = 0; index < MemoryCount; index++)
        {
            if (_memoryLevels[index] > 0 && leftOut?.Contains(new Fader(true, index + 1)) != true)
                AddSequence(levels, index, now, _memoryLevels[index], stepsOn, onSteps);
        }
    }

    // Call with the lock held. Raises "levels" to what the sequence of one memory gives at "now",
    // under a fader at "fader".
    //
    // A step that takes everything down goes over in a crossfade without a dip: every channel moves
    // from its level in what was on to its level in the step that comes. A channel both hold at the
    // same level stays where it is; one that rises follows the up time of the step that comes, one
    // that falls the down time of the step that held it (to 0 if the new step does not hold it).
    // A step that adds, or a release, changes what is on in its own time: the up time of the step
    // that adds, the down time of the release. A step played earlier that is still on its way down
    // (a down time longer than the steps after it) holds its channels up the same way, from where
    // the newer steps have them.
    private void AddSequence(
        byte[] levels, int index, double now, byte fader, Dictionary<int, int>? stepsOn, Dictionary<int, int[]>? onSteps = null)
    {
        if (_sequences[index] is not { Steps.Length: > 0 } sequence)
            return;

        SequenceTimeline.Moment moment = MomentOf(index, sequence, now);
        stepsOn?.Add(index + 1, moment.Current + 1);
        onSteps?.Add(index + 1, [.. moment.On.Select(step => step + 1)]);
        if (moment.Before.Length == 0 && moment.Going.Length == 0 && moment.On.Length == 1)
        {
            // Nothing was on before it and nothing goes down: it comes up from dark.
            AddStep(levels, sequence.Steps[moment.On[0]], moment.In, fader);
            return;
        }

        var looks = new SequenceLooks(this, sequence.Steps, levels.Length);
        byte[] from = looks.LightOf(moment.Before), to = looks.LightOf(moment.On);
        (byte[] Look, double Fraction)[] going = [.. moment.Going.Select(step => (looks.LightOf(step), step.Fraction))];
        for (int channel = 0; channel < levels.Length; channel++)
        {
            double value = Between(from[channel], to[channel], moment.In, moment.TookAll);
            foreach ((byte[] look, double fraction) in going)
            {
                if (look[channel] > value)
                    value += (look[channel] - value) * fraction;
            }

            if (value > 0)
                levels[channel] = Math.Max(levels[channel], Scale((byte)Math.Round(value), fader));
        }
    }

    // Call with the lock held. Raises "levels" (one per effect, 0 to 1) to how strongly the running
    // sequences play each effect at "now": as far as its step is up, scaled by the fader of the
    // sequence's memory; the highest wins. An effect step comes and goes like the light of a step,
    // without the crossfade, which an effect does not need. The memories in "leftOut" (held with
    // SOLO DOWN) play nothing.
    private void AddSequenceEffects(double[] levels, double now, HashSet<Fader>? leftOut)
    {
        for (int index = 0; index < MemoryCount; index++)
        {
            byte fader = _memoryLevels[index];
            if (fader == 0 || _sequences[index] is not { } sequence || !sequence.Steps.Any(step => step.IsEffect)
                || leftOut?.Contains(new Fader(true, index + 1)) == true)
            {
                continue;
            }

            SequenceTimeline.Moment moment = MomentOf(index, sequence, now);
            var looks = new SequenceLooks(this, sequence.Steps, Channels);
            byte[] from = looks.EffectsOf(moment.Before), to = looks.EffectsOf(moment.On);
            (byte[] Levels, double Fraction)[] going = [.. moment.Going.Select(step => (looks.EffectsOf(step), step.Fraction))];
            for (int effect = 0; effect < levels.Length; effect++)
            {
                double value = Between(from[effect], to[effect], moment.In, moment.TookAll);
                foreach ((byte[] played, double fraction) in going)
                {
                    if (played[effect] > value)
                        value += (played[effect] - value) * fraction;
                }

                levels[effect] = Math.Max(levels[effect], value * fader / (MaxLevel * (double)MaxLevel));
            }
        }
    }

    // Call with the lock held. Raises "levels" (one per sound, 0 to 1) to how loud the running
    // sequences play each sound at "now": as far as its step is up, scaled by the fader of the
    // sequence's memory; the highest wins. A step of a sound comes and goes like that of an effect;
    // one that plays on (Continue) stays on through the steps after it (SequenceTimeline).
    private void AddSequenceSounds(double[] levels, double now)
    {
        for (int index = 0; index < MemoryCount; index++)
        {
            byte fader = _memoryLevels[index];
            if (fader == 0 || _sequences[index] is not { } sequence || !sequence.Steps.Any(step => step.IsSound))
                continue;

            SequenceTimeline.Moment moment = MomentOf(index, sequence, now);
            var looks = new SequenceLooks(this, sequence.Steps, Channels);
            byte[] from = looks.SoundsOf(moment.Before), to = looks.SoundsOf(moment.On);
            (byte[] Levels, double Fraction)[] going = [.. moment.Going.Select(step => (looks.SoundsOf(step), step.Fraction))];
            for (int sound = 0; sound < levels.Length; sound++)
            {
                double value = Between(from[sound], to[sound], moment.In, moment.TookAll);
                foreach ((byte[] played, double fraction) in going)
                {
                    if (played[sound] > value)
                        value += (played[sound] - value) * fraction;
                }

                levels[sound] = Math.Max(levels[sound], value * fader / (MaxLevel * (double)MaxLevel));
            }

            // A step of a sound that has just started plays its sound again when the sound is over,
            // also where the sound stayed up from a step before (one that plays on, a new round).
            if (_soundStepsSeen[index] != (moment.Round, moment.Current) && moment.Current >= 0)
            {
                _soundStepsSeen[index] = (moment.Round, moment.Current);
                Step started = sequence.Steps[moment.Current];
                if (started is { IsSound: true, Value: > 0 } && _soundRuns[started.Number - 1] is { Ended: true, Voice: false } over)
                    over.Ended = false;
            }
        }
    }

    // Where a channel (or an effect) stands while the step played last comes in, "coming" of its
    // way: between its level in what was on ("from") and in what is on now ("to"). One that rises
    // goes up with the step. One that falls goes down with it too where the step added or released;
    // where it took everything down the channel is at its new level at once, and the steps that
    // went down hold it up as they go (AddSequence).
    private static double Between(byte from, byte to, double coming, bool tookAll)
    {
        return to >= from || !tookAll ? from + (to - from) * coming : to;
    }

    // Call with the lock held. The moment the sequence at "index" stands at "now", see SequenceTimeline.
    private SequenceTimeline.Moment MomentOf(int index, Sequence sequence, double now)
    {
        double elapsed = SequenceTime(index, now);
        int seed = _sequenceSeeds[index];
        if (_moments[index] is { } known && ReferenceEquals(known.Sequence, sequence) && known.Elapsed == elapsed && known.Seed == seed)
            return known.Moment;

        SequenceTimeline.Moment moment = SequenceTimeline.At(sequence, elapsed, seed);
        _moments[index] = new KnownMoment(sequence, elapsed, seed, moment);
        return moment;
    }

    // What one step gives when it is all the way up, before the fader of its sequence: a channel
    // its value, a memory its look at the step's value, a fixture or a group the light of its parts.
    // An effect and a release give no light.
    private byte[] FullLook(Step step, int channels)
    {
        byte[] look = new byte[channels];
        if (step.Value == 0 || !step.GivesLight)
            return look;

        if (step.IsLights)
        {
            foreach (FixtureLooks.Part part in LookOfLights(step))
            {
                if (!part.Attribute && part.Channel < channels)
                    look[part.Channel] = Math.Max(look[part.Channel], part.Value);
            }
        }
        else if (!step.IsMemory)
        {
            if (step.Number - 1 < channels)
                look[step.Number - 1] = (byte)step.Value;
        }
        else if (_memories[step.Number - 1] is { } stored)
        {
            for (int channel = 0; channel < channels; channel++)
                look[channel] = Scale(stored[channel], (byte)step.Value);
        }

        return look;
    }

    // What one step gives while it is "fraction" of the way up, under the fader of its sequence.
    // A fixture or a group gives its light here; its attributes (a shutter, a color wheel) come
    // from the step that is on, see Desk.Attributes.cs. An effect and a release give no light.
    private void AddStep(byte[] levels, Step step, double fraction, byte fader)
    {
        byte level = (byte)Math.Round(step.Value * fraction);
        if (level == 0 || !step.GivesLight)
            return;

        if (step.IsLights)
        {
            foreach (FixtureLooks.Part part in LookOfLights(step))
            {
                if (!part.Attribute && part.Channel < levels.Length)
                    levels[part.Channel] = Math.Max(levels[part.Channel], Scale((byte)Math.Round(part.Value * fraction), fader));
            }

            return;
        }

        if (!step.IsMemory)
        {
            levels[step.Number - 1] = Math.Max(levels[step.Number - 1], Scale(level, fader));
            return;
        }

        // A memory step plays that memory's look. An empty memory, or one that holds a
        // sequence itself, gives nothing: sequences are not played inside sequences.
        if (_memories[step.Number - 1] is not { } look)
            return;

        for (int channel = 0; channel < levels.Length; channel++)
        {
            if (look[channel] > 0)
                levels[channel] = Math.Max(levels[channel], Scale(Scale(look[channel], level), fader));
        }
    }

    // Call with the lock held. True while some sequence still changes what it gives.
    private bool SequencesMove(double now)
    {
        for (int index = 0; index < MemoryCount; index++)
        {
            if (_memoryLevels[index] > 0 && _sequences[index] is { } sequence
                && SequenceTimeline.StillMoves(sequence, SequenceTime(index, now)))
            {
                return true;
            }
        }

        return false;
    }

    // A moment of a sequence, with what it was worked out from: it holds while these are the same.
    private sealed record KnownMoment(Sequence Sequence, double Elapsed, int Seed, SequenceTimeline.Moment Moment);

    // What the steps of one sequence give, each worked out once for one moment: their light, and
    // the effects they play.
    private sealed class SequenceLooks(Desk desk, Step[] steps, int channels)
    {
        private readonly Dictionary<int, byte[]> _looks = [];
        private readonly Dictionary<int, Dictionary<Fader, byte>> _faders = [];

        // The light of the steps in "on", all the way up. Of two that use the same fader (the
        // same channel, the same memory) the later one's level counts, as on the stack; between
        // faders the highest wins.
        public byte[] LightOf(int[] on)
        {
            if (on.Length == 1)
                return LookOf(on[0]);

            byte[] light = new byte[channels];
            foreach ((Fader fader, byte level) in Held(on))
                desk.Contribute(light, fader, level);

            return light;
        }

        // The light of a step on its way down, from all the way up: without the faders that the
        // steps which came on after it held, as those were theirs.
        public byte[] LightOf(SequenceTimeline.Going going)
        {
            if (going.Over.Length == 0)
                return LookOf(going.Step);

            byte[] light = new byte[channels];
            foreach ((Fader fader, byte level) in Without(going))
                desk.Contribute(light, fader, level);

            return light;
        }

        // How strongly the steps in "on" play each effect, all the way up, 0-255.
        public byte[] EffectsOf(int[] on)
        {
            return EffectLevels(Held(on));
        }

        // How strongly a step on its way down played each effect, from all the way up.
        public byte[] EffectsOf(SequenceTimeline.Going going)
        {
            return EffectLevels(Without(going));
        }

        // How loud the steps in "on" play each sound, all the way up, 0-255.
        public byte[] SoundsOf(int[] on)
        {
            return SoundLevels(Held(on));
        }

        // How loud a step on its way down played each sound, from all the way up.
        public byte[] SoundsOf(SequenceTimeline.Going going)
        {
            return SoundLevels(Without(going));
        }

        private static byte[] EffectLevels(IEnumerable<KeyValuePair<Fader, byte>> faders)
        {
            byte[] levels = new byte[Effect.MaxEffects];
            foreach ((Fader fader, byte level) in faders)
            {
                if (fader.Effect)
                    levels[fader.Number - 1] = Math.Max(levels[fader.Number - 1], level);
            }

            return levels;
        }

        private static byte[] SoundLevels(IEnumerable<KeyValuePair<Fader, byte>> faders)
        {
            byte[] levels = new byte[Sound.MaxSounds];
            foreach ((Fader fader, byte level) in faders)
            {
                if (fader.Sound)
                    levels[fader.Number - 1] = Math.Max(levels[fader.Number - 1], level);
            }

            return levels;
        }

        // The faders the steps in "on" hold, each at the level of the last of them that uses it.
        private Dictionary<Fader, byte> Held(int[] on)
        {
            var held = new Dictionary<Fader, byte>();
            foreach (int step in on)
            {
                foreach ((Fader fader, byte level) in FadersOf(step))
                    held[fader] = level;
            }

            return held;
        }

        // The faders a step on its way down held when it went: its own but those of the steps over it.
        private IEnumerable<KeyValuePair<Fader, byte>> Without(SequenceTimeline.Going going)
        {
            Dictionary<Fader, byte> over = Held(going.Over);
            return FadersOf(going.Step).Where(pair => !over.ContainsKey(pair.Key));
        }

        // The faders one step holds, as the stack would move them: the fader of its channel, memory
        // or effect, or the channel faders of its lights (the higher, where two of them share one),
        // each at its level; a release holds none. Attributes do not count here (Desk.Attributes.cs).
        private Dictionary<Fader, byte> FadersOf(int index)
        {
            if (_faders.TryGetValue(index, out Dictionary<Fader, byte>? faders))
                return faders;

            faders = [];
            Step step = steps[index];
            if (step.IsLights)
            {
                foreach (FixtureLooks.Part part in desk.LookOfLights(step))
                {
                    var fader = new Fader(Memory: false, part.Channel + 1);
                    if (!part.Attribute)
                        faders[fader] = Math.Max(faders.GetValueOrDefault(fader), part.Value);
                }
            }
            else if (!step.IsRelease)
            {
                faders[new Fader(step.IsMemory, step.Number, step.IsEffect, step.IsSound)] = (byte)step.Value;
            }

            return _faders[index] = faders;
        }

        private byte[] LookOf(int index)
        {
            return _looks.TryGetValue(index, out byte[]? look) ? look : _looks[index] = desk.FullLook(steps[index], channels);
        }
    }
}
