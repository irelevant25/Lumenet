using System.Globalization;
using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Server.Api;

// The hub's part for sequences: their steps, their speed and the rate.
internal partial class DeskHub
{
    /// <summary>The sequences, for each memory that holds one, by its number.</summary>
    public Dictionary<int, Sequence> GetSequences()
    {
        return _desk.GetSequences();
    }

    /// <summary>Stores a sequence in a memory that is empty or holds a sequence already.</summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="sequence">
    /// The sequence: "loop", "random" (a new order on every round), "speed" (0.1 to 10) and the
    /// steps, the first one first.
    /// </param>
    public Task SetSequence(int memory, Sequence sequence)
    {
        using (_history.Change($"the sequence of memory {memory}"))
            _desk.SetSequence(memory, sequence);

        return TellDeskAsync();
    }

    /// <summary>
    /// Changes how fast the sequence of a memory is played, without starting it again. The UI
    /// moves its speed fader this way, like a fader: the other UIs hear of it, this one does not.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="speed">From 0.1 to 10.</param>
    public async Task SetSequenceSpeed(int memory, double speed)
    {
        double kept;
        using (_history.Change($"the speed of the sequence in memory {memory}", $"speed {memory}"))
            kept = _desk.SetSequenceSpeed(memory, speed);

        await Clients.Others.SequenceSpeedChanged(memory, kept);
        await TellProjectAsync();
    }

    /// <summary>
    /// Sets the rate, a tempo for all sequences (see <see cref="Desk.SetRate"/>): RATE and its
    /// fader, or the taps of TEMPO. The UI sends it like a fader: the other UIs hear of it, this
    /// one does not. It is no part of the project, so the history leaves it alone.
    /// </summary>
    /// <param name="on">True: the rate counts (RATE is lit).</param>
    /// <param name="bpm">The tempo, from 30 to 300 beats per minute.</param>
    public Task SetRate(bool on, double bpm)
    {
        _desk.SetRate(on, bpm);
        return Clients.Others.RateChanged(on, bpm);
    }

    /// <summary>
    /// Puts one more step at the end of the sequence of a memory. An empty memory gets a new
    /// sequence, played in a loop.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="step">The step.</param>
    public Task AddSequenceStep(int memory, Step step)
    {
        using (_history.Change($"a step of the sequence in memory {memory}"))
            _desk.AddSequenceStep(memory, step);

        return TellDeskAsync();
    }

    /// <summary>
    /// Skips a step of a sequence or takes it back (Disabled), also while the stack is live, see
    /// <see cref="Desk.SetSequenceStepDisabled"/>.
    /// </summary>
    /// <param name="memory">Memory number, 1-128.</param>
    /// <param name="step">The step, counted from 1.</param>
    /// <param name="disabled">True to skip it.</param>
    public Task SetSequenceStepDisabled(int memory, int step, bool disabled)
    {
        using (_history.Change($"{(disabled ? "skipping" : "playing")} step {step} of the sequence in memory {memory}"))
        {
            if (!_desk.SetSequenceStepDisabled(memory, step, disabled))
                return Task.CompletedTask;
        }

        return TellDeskAsync();
    }
}
