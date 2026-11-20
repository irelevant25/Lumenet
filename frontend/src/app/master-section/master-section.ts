import { Component, inject } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { Fader } from '../fader/fader';
import { LampButton } from '../lamp-button/lamp-button';
import { RateWheel } from '../rate-wheel/rate-wheel';

/**
 * What acts on the whole desk: Clear, Solo, Blackout, the master fader, and under it the rate
 * (a speed master for the sequences) with its wheel. A column at the right edge of the main
 * view; in the mobile layout two lines across its top.
 */
@Component({
  imports: [Fader, LampButton, RateWheel],
  selector: 'lm-master-section',
  styleUrl: './master-section.css',
  templateUrl: './master-section.html',
})
export class MasterSection {
  protected readonly store = inject(DeskStore);
}
