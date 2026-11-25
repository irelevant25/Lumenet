import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { EFFECT_KINDS, effectLights, effectSummary, newEffect } from '../desk/effect-shapes';
import { EffectKind } from '../desk/effects';
import { EffectEditor } from '../effect-editor/effect-editor';
import { EffectLive } from '../effect-live/effect-live';
import { EffectPreviewPanel } from '../effect-preview/effect-preview';
import { EffectSection } from '../effect-section/effect-section';

/**
 * The Effects view: the effects with their faders and keys at the top, as on the main view; under
 * them the effect chosen by its name: its live speed, size and spread, the editor of the effect
 * itself, and a preview of one cycle of it. An empty number offers the four kinds to make one.
 */
@Component({
  imports: [EffectEditor, EffectLive, EffectPreviewPanel, EffectSection],
  selector: 'lm-effects-view',
  styleUrl: './effects-view.css',
  templateUrl: './effects-view.html',
})
export class EffectsView {
  protected readonly store = inject(DeskStore);
  protected readonly kinds = EFFECT_KINDS;

  protected readonly chosen = this.store.chosenEffect;
  protected readonly effect = computed(() => this.store.effectsByNumber().get(this.chosen()));
  protected readonly playback = computed(() => this.store.effectPlaybacks().get(this.chosen()));

  /** One sentence about the effect that is chosen. */
  protected readonly summary = computed(() => {
    if (!this.store.effectsKnown())
      return 'The desk program does not know effects yet: start the newest Lumenet.';
    const effect = this.effect();
    if (!effect)
      return `Effect ${this.chosen()} is empty. Choose a kind to make it, or another effect by its name.`;
    const playback = this.playback();
    const lights = effectLights(effect, this.store.stage()).length;
    const state = !playback?.running
      ? 'out'
      : playback.fading
        ? 'fading'
        : playback.level === 0 && (playback.played ?? 0) > 0
          ? 'played by a sequence'
          : 'running';
    const said = effectSummary(effect, lights, playback?.bpm ?? effect.speed);
    return `Effect ${effect.number}, ${effect.name || 'without a name'}: ${said.charAt(0).toLowerCase()}${said.slice(1)}, ${state}.`;
  });

  /** Makes an effect of a kind under the chosen number, on every light that can do it. */
  protected make(kind: EffectKind): void {
    this.store.setEffect(newEffect(this.chosen(), kind, this.store.stage()));
  }
}
