import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { FunctionName, activeFunction, functionBlinks } from '../desk/function-mode';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * `label` is what the key shows, one entry per line; `name` is the full wording for screen readers.
 * Copy and Del work on whatever a memory holds, a look or a sequence.
 */
const FUNCTIONS: readonly { function: FunctionName; label: readonly string[]; name: string }[] = [
  { function: 'save', label: ['Save', 'mem'], name: 'Save memory' },
  { function: 'edit', label: ['Edit', 'mem'], name: 'Edit memory' },
  { function: 'record', label: ['Rec', 'seq'], name: 'Record sequence' },
  { function: 'copy', label: ['Copy'], name: 'Copy memory' },
  { function: 'delete', label: ['Del'], name: 'Delete memory' },
];

/**
 * The record keys: what can be done with a memory. Press one, then the memory key that blinks.
 *
 * Rec seq goes further: after the memory, every channel key and every key of a memory that
 * holds a look puts one more step into that memory's sequence, until Rec seq is pressed again.
 *
 * While the stack is live the keys are locked.
 */
@Component({
  imports: [LampButton],
  selector: 'lm-record-section',
  styleUrl: './record-section.css',
  templateUrl: './record-section.html',
})
export class RecordSection {
  protected readonly store = inject(DeskStore);

  protected readonly functions = computed(() => {
    const mode = this.store.mode();
    const active = activeFunction(mode);
    return FUNCTIONS.map((item) => ({
      ...item,
      active: item.function === active,
      // A lit key that blinks ends what is going on when it is pressed again: Edit mem stores
      // the faders back, Rec seq ends the recording.
      blink: item.function === active && functionBlinks(mode),
    }));
  });
}
