import { Component, inject, input } from '@angular/core';
import { DeskStore } from '../../desk/desk-store';
import { Fader } from '../../shared/fader/fader';
import { LampButton } from '../../shared/lamp-button/lamp-button';

/**
 * What acts on the whole output: Blackout and the master fader. A column at the right of the
 * main view, over the record keys, and beside the cards of the stack on the Stage view; a line lying
 * down on the Stack view ("lying") and in the mobile layout. Clear, Solo and Rate are in the general
 * section beside it on the main view.
 */
@Component({
  host: { '[class.lying]': 'lying()' },
  imports: [Fader, LampButton],
  selector: 'lm-master-section',
  styleUrl: './master-section.css',
  templateUrl: './master-section.html',
})
export class MasterSection {
  protected readonly store = inject(DeskStore);
  /** True: a line, the fader lying (the Stack view). */
  readonly lying = input(false);
}
