using System.Globalization;
using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Server.Api;

// The hub's part for the effects: the effects themselves, their keys and their live controls.
internal partial class DeskHub
{
    /// <summary>The effects of the project, in the order of their numbers.</summary>
    public Effect[] GetEffects()
    {
        return _desk.GetEffects();
    }

    /// <summary>
    /// Stores an effect under its number, new or in place of the one there, as the editor of the
    /// Effects view changes it. Changes of one effect in quick succession are one step of the history.
    /// </summary>
    /// <param name="effect">The effect as it is to be.</param>
    /// <returns>The effect as the desk keeps it.</returns>
    public async Task<Effect> SetEffect(Effect effect)
    {
        Effect kept;
        using (_history.Change($"effect {effect?.Number}", $"effect {effect?.Number}"))
            kept = _desk.SetEffect(effect);

        await TellEffectsAsync(kept.Number);
        return kept;
    }

    /// <summary>Takes an effect out of the project; its playback stops.</summary>
    /// <param name="number">The effect, 1-64.</param>
    public async Task DeleteEffect(int number)
    {
        using (_history.Change($"taking effect {number} out"))
        {
            if (!_desk.DeleteEffect(number))
                return;
        }

        await TellEffectsAsync(number);
    }

    /// <summary>Stores a copy of an effect under another number.</summary>
    /// <param name="from">The effect to copy, 1-64.</param>
    /// <param name="to">The number for the copy, 1-64.</param>
    public async Task CopyEffect(int from, int to)
    {
        using (_history.Change($"copying effect {from} to {to}"))
            _desk.CopyEffect(from, to);

        await TellEffectsAsync(to);
    }

    /// <summary>Stores the live speed, size and spread of an effect's playback in the effect (Keep).</summary>
    /// <param name="number">The effect, 1-64.</param>
    public async Task KeepEffect(int number)
    {
        using (_history.Change($"keeping the live values of effect {number}"))
            _desk.KeepEffect(number);

        await TellEffectsAsync(number);
    }

    /// <summary>Binds an effect key of the independents to an effect.</summary>
    /// <param name="index">Number of the key, 1-32.</param>
    /// <param name="effect">The effect the key starts and stops from now on, 1-64.</param>
    public async Task SetEffectIndependent(int index, int effect)
    {
        using (_history.Change($"effect key {index} of the independents"))
            _desk.SetEffectIndependent(index, effect);

        await Clients.Others.EffectIndependentChanged(index, effect);
        await TellProjectAsync();
    }

    /// <summary>
    /// Works the playback of an effect: its key, its fader, its live speed, size and spread. This
    /// is also the way in for anything outside the desk, a MIDI controller later: "effect 3 speed =
    /// 120 BPM" is SetEffectControl(3, "speed", 120). See <see cref="Desk.SetEffectControl"/> for
    /// the controls. A fader or a live control moved goes to the other UIs, like a fader; the keys
    /// and "reset" go to every UI.
    /// </summary>
    /// <param name="number">The effect, 1-64.</param>
    /// <param name="control">"go", "toggle", "level", "speed", "size", "spread" or "reset".</param>
    /// <param name="value">The value: 1 or 0 for "go", per cent for "level", "size" and "spread", BPM for "speed".</param>
    /// <returns>The playback after the change.</returns>
    public async Task<EffectPlayback> SetEffectControl(int number, string control, double value)
    {
        EffectPlayback playback = _desk.SetEffectControl(number, control, value);
        bool moved = control is "level" or "speed" or "size" or "spread";
        await (moved ? Clients.Others : Clients.All).EffectPlaybackChanged(playback);
        return playback;
    }

    /// <summary>One cycle of an effect sampled for the preview of the Effects view, see <see cref="Desk.PreviewEffect"/>.</summary>
    /// <param name="effect">The effect, as the editor has it; it need not be stored.</param>
    public EffectPreview PreviewEffect(Effect effect)
    {
        return _desk.PreviewEffect(effect);
    }

    // After a change of the effects: every UI gets them again, and the playback of the one that
    // changed (it takes the effect's speed, size and spread). The effects are part of the project.
    private async Task TellEffectsAsync(int number)
    {
        await Clients.All.EffectsChanged(_desk.GetEffects());
        await Clients.All.EffectPlaybackChanged(_desk.GetEffectPlayback(number));
        await TellProjectAsync();
    }
}
