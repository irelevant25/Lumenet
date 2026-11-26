using System.Globalization;
using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Server.Api;

// The hub's part for the stack: its steps, going live, and playing it.
internal partial class DeskHub
{
    /// <summary>The stack: its steps in the order of their numbers.</summary>
    public StackState GetStack()
    {
        return _desk.GetStack();
    }

    /// <summary>What changes with time: what the running sequences give, and where the stack stands.</summary>
    public Playback GetPlayback()
    {
        return _desk.GetPlayback();
    }

    /// <summary>Puts a new step at the end of the stack.</summary>
    /// <param name="step">The step.</param>
    /// <returns>The name of the new step.</returns>
    public async Task<int> AddStackStep(Step step)
    {
        int id;
        using (_history.Change("a new cue of the stack"))
            id = _desk.AddStackStep(step);

        await TellStackAsync();
        return id;
    }

    /// <summary>
    /// Puts a new step right after another step of the stack; it gets a number that fits there.
    /// One change in the history, however many steps give way.
    /// </summary>
    /// <param name="afterId">The step it goes behind.</param>
    /// <param name="step">The step.</param>
    /// <returns>The name of the new step.</returns>
    public async Task<int> AddStackStepAfter(int afterId, Step step)
    {
        int id;
        using (_history.Change($"a new cue after cue {CueOf(afterId)} of the stack"))
            id = _desk.AddStackStep(step, afterId);

        await TellStackAsync();
        return id;
    }

    /// <summary>Changes what a step of the stack stands for, its value or its times.</summary>
    /// <param name="id">The name of the step.</param>
    /// <param name="step">The step as it is to be.</param>
    public Task UpdateStackStep(int id, Step step)
    {
        using (_history.Change($"cue {CueOf(id)} of the stack"))
            _desk.UpdateStackStep(id, step);

        return TellStackAsync();
    }

    /// <summary>
    /// Skips a step of the stack or takes it back (Disabled), also while the stack is live, see
    /// <see cref="Desk.SetStackStepDisabled"/>.
    /// </summary>
    /// <param name="id">The name of the step.</param>
    /// <param name="disabled">True to skip it.</param>
    public Task SetStackStepDisabled(int id, bool disabled)
    {
        using (_history.Change($"{(disabled ? "skipping" : "playing")} cue {CueOf(id)} of the stack"))
        {
            if (!_desk.SetStackStepDisabled(id, disabled))
                return Task.CompletedTask;
        }

        return TellStackAsync();
    }

    /// <summary>
    /// The moment of a sound a cue comes at instead of GO, or none (null), also while the stack is
    /// live, see <see cref="Desk.SetStackStepAt"/>.
    /// </summary>
    /// <param name="id">The name of the cue.</param>
    /// <param name="at">The sound and the seconds in it; null for none.</param>
    public Task SetStackStepAt(int id, SoundTime? at)
    {
        using (_history.Change($"the moment of cue {CueOf(id)} of the stack"))
        {
            if (!_desk.SetStackStepAt(id, at))
                return Task.CompletedTask;
        }

        return TellStackAsync();
    }

    /// <summary>
    /// Learn: while on, GO stores the moment of the sound that plays into the cue it goes to, see
    /// <see cref="Desk.SetStackLearning"/>. Every page sees it lit.
    /// </summary>
    /// <param name="on">True to learn.</param>
    public Task SetStackLearning(bool on)
    {
        return _desk.SetStackLearning(on) ? TellPlaybackAsync() : Task.CompletedTask;
    }

    /// <summary>Takes a step out of the stack.</summary>
    /// <param name="id">The name of the step.</param>
    public Task DeleteStackStep(int id)
    {
        using (_history.Change($"taking cue {CueOf(id)} out of the stack"))
            _desk.DeleteStackStep(id);

        return TellStackAsync();
    }

    /// <summary>Puts a step of the stack right after another one; it gets a number that fits there.</summary>
    /// <param name="id">The name of the step to move.</param>
    /// <param name="afterId">The step it goes behind; null for the very top.</param>
    public Task MoveStackStep(int id, int? afterId)
    {
        using (_history.Change($"moving cue {CueOf(id)} of the stack"))
            _desk.MoveStackStep(id, afterId);

        return TellStackAsync();
    }

    /// <summary>Gives a step of the stack the number its owner typed.</summary>
    /// <param name="id">The name of the step.</param>
    /// <param name="cue">The number as text: "3", "3.0", "2.1".</param>
    public Task RenumberStackStep(int id, string cue)
    {
        using (_history.Change($"renumbering cue {CueOf(id)} of the stack"))
            _desk.RenumberStackStep(id, cue);

        return TellStackAsync();
    }

    /// <summary>
    /// Switches the stack live or off. Going live brings every fader down to 0, goes to the
    /// first step and locks everything that is stored; switching it off stops the stack and
    /// leaves the faders where they are. The rate stays as it is either way.
    /// </summary>
    /// <param name="on">True for live.</param>
    public async Task SetStackLive(bool on)
    {
        _desk.SetStackLive(on);

        // Live: every fader went to 0. Off: the sequences play at their own speeds again. Either
        // way the UIs take the whole desk and its sequences again.
        await Clients.All.DeskChanged(_desk.GetState());
        await TellPlaybackAsync();
    }

    /// <summary>
    /// GO: goes on after a pause, or goes to the next cue. While Learn is on, the cue it goes to takes
    /// the moment of the sound that plays: a step of the history, and the stack changes for every page.
    /// </summary>
    public async Task StackPlay()
    {
        if (!_desk.StackLearning)
        {
            _desk.StackPlay();
            await TellPlaybackAsync();
            return;
        }

        using (_history.Change("the moment GO learned"))
            _desk.StackPlay();
        await TellStackAsync();
        await TellPlaybackAsync();
    }

    /// <summary>Pause: holds a running fade or wait.</summary>
    public Task StackPause()
    {
        _desk.StackPause();
        return TellPlaybackAsync();
    }

    /// <summary>Next: goes to the next step now.</summary>
    public Task StackNext()
    {
        _desk.StackNext();
        return TellPlaybackAsync();
    }

    /// <summary>Previous: goes to the step before the current one now.</summary>
    public Task StackPrevious()
    {
        _desk.StackPrevious();
        return TellPlaybackAsync();
    }

    // The number of a step of the stack, like "2.1", for what the history says.
    private string CueOf(int id)
    {
        return _desk.GetStack().Steps.FirstOrDefault(step => step.Id == id)?.Cue ?? "?";
    }

    // After a change to the steps of the stack: every UI gets the stack again, and where it
    // stands (a step that was taken out may have been the current or the next one). The stack
    // is part of the project.
    private async Task TellStackAsync()
    {
        await Clients.All.StackChanged(_desk.GetStack());
        await TellPlaybackAsync();
        await TellProjectAsync();
    }

    // After Play, Pause, Next, Previous or the live switch: every UI learns at once where the
    // stack stands. The faders it moves follow from the desk clock.
    private Task TellPlaybackAsync()
    {
        return Clients.All.PlaybackChanged(_desk.GetPlayback());
    }
}
