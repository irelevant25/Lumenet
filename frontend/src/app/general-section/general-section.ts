import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { SPEED_KEY_STEP, SPEED_POSITIONS, speedAt, speedPosition } from '../desk/speed';
import { Fader } from '../fader/fader';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * General: Clear, Solo, and Rate (a speed master for the sequences) with its fader, the rate
 * above the fader. A column beside the master section on the main view, over the stack; in the
 * mobile layout a line across the top.
 *
 * The rate fader goes from x0.1 at the bottom to x10 at the top, x1 in the middle, as the speed
 * fader of a sequence does (desk/speed.ts). It counts while RATE is lit; dark, it can still be
 * set for the next time. A double click puts it back to x1.
 */
@Component({
  imports: [Fader, LampButton],
  selector: 'lm-general-section',
  styleUrl: './general-section.css',
  templateUrl: './general-section.html',
})
export class GeneralSection {
  protected readonly store = inject(DeskStore);

  protected readonly positions = SPEED_POSITIONS;
  protected readonly keyStep = SPEED_KEY_STEP;
  protected readonly position = computed(() => speedPosition(this.store.rate()));
  protected readonly rateText = computed(() => `×${this.store.rate().toFixed(2)}`);
  protected readonly rateSaid = computed(() => `${this.store.rate().toFixed(2)} times`);

  protected onPosition(position: number): void {
    this.store.setRate(speedAt(position));
  }
}
