import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { FunctionName, activeFunction } from '../desk/function-mode';
import { LampButton } from '../lamp-button/lamp-button';

/** `label` is what the key shows; `name` is the full wording for screen readers. */
const FUNCTIONS: readonly { function: FunctionName; label: string; name: string }[] = [
  { function: 'save', label: 'Save', name: 'Save memory' },
  { function: 'edit', label: 'Edit', name: 'Edit memory' },
  { function: 'copy', label: 'Copy', name: 'Copy memory' },
  { function: 'delete', label: 'Del', name: 'Delete memory' },
];

/**
 * The record keys: the four things that can be done with a memory.
 * Press one, then the memory key that blinks.
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
      // While a memory is being edited, Edit blinks: pressing it again stores the faders back.
      blink: item.function === 'edit' && mode.kind === 'editing',
    }));
  });
}
