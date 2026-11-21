import { Component, computed, inject, input } from '@angular/core';
import { BlinkClock } from '../desk/blink-clock';
import { LampLevel } from '../desk/lamps';

/**
 * The color of a lamp. Each key has a color of its own, which Settings can change (see
 * desk/key-colors.ts): the tone names it, and the key takes the variable --key-<tone> of
 * styles.css. Besides those of the keys, "channel" (green), "memory" (red), "system" (amber),
 * "white", "blue" and "live" stand for the colors of their kind; "sequence" is a memory that
 * holds a sequence, red running into yellow.
 */
export type LampTone = string;

/**
 * A square key that is lit from behind, like the keys of a lighting desk.
 * Every key on the desk has the same size. Put it on a native element:
 * `<button lmLamp>` for something to press, `<span lmLamp>` for a lamp that only shows a state.
 */
@Component({
  host: {
    '[class]': 'hostClass()',
    '[style.--lamp]': 'lampColor()',
  },
  selector: '[lmLamp]',
  styleUrl: './lamp-button.css',
  templateUrl: './lamp-button.html',
})
export class LampButton {
  readonly tone = input<LampTone>('system');
  /** Brightness while the lamp is steady. */
  readonly level = input<LampLevel>(1);
  /** A blinking lamp says "press me now". */
  readonly blink = input(false);

  private readonly clock = inject(BlinkClock);

  private readonly shownLevel = computed<LampLevel>(() => {
    if (!this.blink()) return this.level();
    if (this.clock.reducedMotion || this.clock.on()) return 3;
    // A blinking lamp goes between full and its normal level, so a half-lit key stays half lit
    // in the dark half. A lamp that is normally full dips to faint, or it would not blink at all.
    return this.level() === 3 ? 1 : this.level();
  });

  protected readonly hostClass = computed(() => {
    const classes = ['lamp', `tone-${this.tone()}`, `level-${this.shownLevel()}`];
    if (this.blink() && this.clock.reducedMotion) classes.push('steady-attention');
    return classes.join(' ');
  });

  /** The lamp's color: the variable of its tone. A sequence's comes from its style sheet. */
  protected readonly lampColor = computed(() =>
    this.tone() === 'sequence' ? null : `var(--key-${this.tone()})`,
  );
}
