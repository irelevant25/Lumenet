import { Component, computed, inject, input } from '@angular/core';
import { BlinkClock } from '../desk/blink-clock';
import { LampLevel } from '../desk/lamps';

/**
 * The colour of a lamp: green for channels, red for memories, red running into yellow for a
 * memory that holds a sequence, amber for the memory functions and the project buttons,
 * white for Clear, blue for Blackout.
 */
export type LampTone = 'channel' | 'memory' | 'sequence' | 'system' | 'white' | 'blue';

/**
 * A square key that is lit from behind, like the keys of a lighting desk.
 * Every key on the desk has the same size. Put it on a native element:
 * `<button lmLamp>` for something to press, `<span lmLamp>` for a lamp that only shows a state.
 */
@Component({
  host: {
    '[class]': 'hostClass()',
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
}
