import { DestroyRef, Service, inject, signal } from '@angular/core';

/** How long a blinking lamp stays bright, and how long dim. About 1.4 blinks a second. */
const HALF_PERIOD_MS = 350;

/**
 * One clock for every blinking lamp, so that they all blink together.
 */
@Service()
export class BlinkClock {
  /** True during the bright half of a blink. */
  readonly on = signal(true);

  /**
   * People who asked their system for less motion get no blinking.
   * A lamp that would blink shows a steady marker instead.
   */
  readonly reducedMotion =
    typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;

  constructor() {
    if (this.reducedMotion) return;

    const timer = setInterval(() => this.on.update((bright) => !bright), HALF_PERIOD_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }
}
