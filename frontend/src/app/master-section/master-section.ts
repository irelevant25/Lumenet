import { Component, inject } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { Fader } from '../fader/fader';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * What acts on the whole output: Blackout and the master fader. A column at the right of the
 * main view, over the record keys; in the mobile layout a line across its top. Clear, Solo and
 * Rate are in the general section beside it.
 */
@Component({
  imports: [Fader, LampButton],
  selector: 'lm-master-section',
  styleUrl: './master-section.css',
  templateUrl: './master-section.html',
})
export class MasterSection {
  protected readonly store = inject(DeskStore);
}
