import { Component, computed, inject, input } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { SPEED_PLACES, speedAt, speedPlace, speedText } from '../desk/effect-shapes';
import { Effect, EffectPlayback } from '../desk/effects';
import { Fader } from '../fader/fader';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * The live values of an effect's playback: its speed, size and spread, which can be moved while it
 * runs (and while the stack is live). They change this run only, not the effect: ↺ takes the
 * effect's own values again, Keep stores these in the effect. The speed fader goes evenly in
 * octaves: twice as fast is always the same way further.
 */
@Component({
  imports: [Fader, LampButton],
  selector: 'lm-effect-live',
  styleUrl: './effect-live.css',
  templateUrl: './effect-live.html',
})
export class EffectLive {
  protected readonly store = inject(DeskStore);
  readonly effect = input.required<Effect>();
  readonly playback = input<EffectPlayback | undefined>(undefined);

  protected readonly speedPlaces = SPEED_PLACES;
  protected readonly number = computed(() => this.effect().number);
  protected readonly speed = computed(() => this.playback()?.speed ?? this.effect().speed);
  protected readonly size = computed(() => this.playback()?.size ?? this.effect().size);
  protected readonly spread = computed(() => this.playback()?.spread ?? this.effect().spread);
  protected readonly changed = computed(() => this.playback()?.changed ?? false);
  protected readonly speedPlace = computed(() => speedPlace(this.speed()));
  protected readonly speedText = computed(() => speedText(this.speed()));
  /** A pattern lies on the plan: the spread does not move it. */
  protected readonly spreadless = computed(() => this.effect().kind === 'pixel');

  /** While RATE is lit an effect runs at its speed times the tempo over 120: said when that differs. */
  protected readonly runsAt = computed(() => {
    const bpm = this.playback()?.bpm;
    return bpm !== undefined && Math.abs(bpm - this.speed()) > 0.05
      ? `With the tempo of Rate it runs at ${speedText(Math.round(bpm * 10) / 10)}.`
      : '';
  });

  protected onSpeed(place: number): void {
    this.store.effectControl(this.number(), 'speed', speedAt(place));
  }

  protected onSize(size: number): void {
    this.store.effectControl(this.number(), 'size', size);
  }

  protected onSpread(spread: number): void {
    this.store.effectControl(this.number(), 'spread', spread);
  }

  protected reset(): void {
    this.store.effectControl(this.number(), 'reset');
  }

  protected keep(): void {
    this.store.keepEffect(this.number());
  }
}
