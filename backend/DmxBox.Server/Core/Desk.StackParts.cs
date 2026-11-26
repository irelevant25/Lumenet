namespace DmxBox.Server.Core;

// The parts the stack is played with: a fader it holds, a move of one, a crossfade, and a step in the stack.
internal partial class Desk
{
    // A fader on the desk: of a channel, of a memory, of an effect ("Effect"; see Desk.Effects.cs) or
    // of a sound ("Sound"; see Desk.Sounds.cs).
    private readonly record struct Fader(bool Memory, int Number, bool Effect = false, bool Sound = false)
    {
        // True for the fader of a channel.
        public bool Channel => !Memory && !Effect && !Sound;
    }

    // A fader on its way: from one level to another, over some seconds.
    private readonly record struct FaderMove(double Start, double Seconds, int From, int To);

    // A crossfade of the stack, see _crossfade: the faders that take part, each with the level it
    // starts from and the level it goes to, the light they give at the start and at the end, how
    // long a channel takes on its way up and on its way down, and how long the fade has run at
    // the last move of the faders.
    private sealed class Crossfade
    {
        public Crossfade(double start, double up, double down, int channels)
        {
            Start = start;
            Up = up;
            From = new byte[channels];
            To = new byte[channels];
            DownOf = new double[channels];
            Array.Fill(DownOf, down);
        }

        public Dictionary<Fader, (byte From, byte To)> Faders { get; } = [];

        public byte[] From { get; }

        public byte[] To { get; }

        // Seconds a channel that falls takes; that of the fader that held it up highest.
        public double[] DownOf { get; }

        public double Start { get; set; }

        // Seconds a channel that rises takes: the up time of the step that comes.
        public double Up { get; }

        public double Elapsed { get; set; }

        // True once every channel has arrived.
        public bool Over => Elapsed >= Up && DownOf.All(down => Elapsed >= down);

        // A channel that rises goes up with the faders that come, one that falls down with those that go.
        public byte ValueAt(int channel)
        {
            double from = From[channel], to = To[channel];
            if (to >= from)
            {
                double up = Up <= 0 ? 1 : Math.Clamp(Elapsed / Up, 0, 1);
                return (byte)Math.Round(from + (to - from) * up);
            }

            double down = DownOf[channel];
            double left = down <= 0 ? 0 : 1 - Math.Clamp(Elapsed / down, 0, 1);
            return (byte)Math.Round(to + (from - to) * left);
        }
    }

    // What a fader is to have while certain steps are on: its level, and the step it has that from.
    private readonly record struct Holder(StackEntry Entry, int Level, bool Jump);

    // A step of the stack with its name and its number.
    private sealed class StackEntry(int id, CueNumber cue, Step step)
    {
        public int Id { get; } = id;

        public CueNumber Cue { get; set; } = cue;

        public Step Step { get; set; } = step;

        public StackStep ForUi()
        {
            return new StackStep(Id, Cue.ToString(), Step.Target, Step.Number, Step.Value, Step.Up, Step.Wait, Step.Down)
            {
                Color = Step.Color,
                Add = Step.Add,
                Continue = Step.Continue,
                Disabled = Step.Disabled,
                Muted = Step.Muted,
                At = Step.At,
            };
        }
    }
}
