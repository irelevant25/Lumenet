import { Component, inject } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { Fader } from '../fader/fader';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * What acts on the whole desk: Clear, Blackout and the master fader. A column at the right
 * edge of the main view; in the mobile layout one line across its top.
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
