import { Component, computed, inject, input } from '@angular/core';
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
 * What the record keys of a view work on: on the main view memories (all five keys); on the other
 * views only Copy and Del, for the memories that hold a look (Memories), the sequences (Sequences),
 * the effects (Effects) or the sounds (Sounds).
 */
export type RecordSubject = 'memory' | 'look' | 'sequence' | 'effect' | 'sound';

/** What screen readers call Copy and Del on each view. */
const ITEM_NAMES: Record<Exclude<RecordSubject, 'memory'>, string> = {
  look: 'memory',
  sequence: 'sequence',
  effect: 'effect',
  sound: 'sound',
};

/**
 * The record keys: what can be done with a memory. Press one, then the memory key that blinks.
 *
 * Rec seq goes further: after the memory, every channel key and every key of a memory that
 * holds a look puts one more step into that memory's sequence, until Rec seq is pressed again.
 *
 * On the other views there are only Copy and Del, for the keys of that view: Copy, then the one to
 * copy, then the number to copy it to; Del, then the one to take out. On the Memories view they keep
 * to looks, on the Sequences view to sequences (onto one of the kind, or an empty memory).
 *
 * While the stack is live the keys are locked.
 */
@Component({
  host: {
    '[style.--keys]': 'functions().length',
  },
  imports: [LampButton],
  selector: 'lm-record-section',
  styleUrl: './record-section.css',
  templateUrl: './record-section.html',
})
export class RecordSection {
  protected readonly store = inject(DeskStore);

  /** What the keys work on. */
  readonly subject = input<RecordSubject>('memory');

  protected readonly functions = computed(() => {
    const mode = this.store.mode();
    const active = activeFunction(mode);
    const subject = this.subject();
    return FUNCTIONS.filter(
      (item) => subject === 'memory' || item.function === 'copy' || item.function === 'delete',
    ).map((item) => ({
      ...item,
      name:
        subject === 'memory'
          ? item.name
          : `${item.label[0] === 'Del' ? 'Delete' : 'Copy'} ${ITEM_NAMES[subject]}`,
      active: item.function === active,
      // A lit key that blinks ends what is going on when it is pressed again: Edit mem stores
      // the faders back, Rec seq ends the recording.
      blink: item.function === active && functionBlinks(mode),
    }));
  });

  /** Copy and Del of the Memories view keep to looks, those of the Sequences view to sequences. */
  protected press(name: FunctionName): void {
    const subject = this.subject();
    this.store.pressFunction(
      name,
      subject === 'look' || subject === 'sequence' ? subject : undefined,
    );
  }
}
