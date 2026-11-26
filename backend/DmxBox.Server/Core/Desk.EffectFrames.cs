namespace DmxBox.Server.Core;

// The effects worked out frame by frame: a run of an effect, and the state of each light and each cell of
// a light while it runs.
internal partial class Desk
{
    // The playback of an effect: none of it is stored in the project.
    private sealed class EffectRun
    {
        public bool Running;        // by its fader: from its start until its fader is down at zero
        public double Played;       // how strongly the sequences play it, 0 to 1, as the clock last found it
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

        // True while it plays: by its fader, or by a sequence.
        public bool Plays => Running || Played > 0;
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
