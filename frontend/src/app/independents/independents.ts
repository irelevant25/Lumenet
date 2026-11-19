import { Component, computed, inject, linkedSignal } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { CHANNEL_COUNT, CHANNEL_INDEPENDENT_COUNT, MEMORY_COUNT } from '../desk/desk-types';
import { memoryLamp } from '../desk/lamps';
import { LampButton, LampTone } from '../lamp-button/lamp-button';

/**
 * The independents: twenty keys in one column at the right edge. The ten green ones each switch
 * a channel between zero and full, the ten red ones each switch the fader of a memory. The
 * channel or memory a key switches is printed on it; Edit lets the owner change those numbers.
 */
@Component({
  imports: [LampButton],
  selector: 'lm-independents',
  styleUrl: './independents.css',
  templateUrl: './independents.html',
})
export class Independents {
  protected readonly store = inject(DeskStore);

  /**
   * True while the numbers on the keys can be changed. A stack that goes live ends the
   * editing: then nothing that is stored can be changed, and Edit is locked.
   */
  protected readonly editing = linkedSignal({
    source: this.store.live,
    computation: () => false,
  });

  /** The ten channel keys: which channel each one switches, and whether that channel is up. */
  protected readonly channelKeys = computed(() => {
    const levels = this.store.channels();
    return this.store.independents().map((channel, index) => ({
      number: index + 1,
      channel,
      on: levels[channel - 1] > 0,
    }));
  });

  /**
   * The ten memory keys: which memory each one switches, and how lit it is, like the key under
   * that memory's fader. Their numbers for screen readers follow those of the channel keys: 11 to 20.
   */
  protected readonly memoryKeys = computed(() => {
    const levels = this.store.memoryLevels();
    const stored = this.store.memoryStored();
    const sequence = this.store.memorySequence();
    return this.store.memoryIndependents().map((memory, index) => ({
      number: index + 1,
      name: CHANNEL_INDEPENDENT_COUNT + index + 1,
      memory,
      on: levels[memory - 1] > 0,
      lamp: memoryLamp(levels[memory - 1], stored[memory - 1]),
      tone: (sequence[memory - 1] ? 'sequence' : 'memory') as LampTone,
    }));
  });

  protected toggleEditing(): void {
    this.editing.update((on) => !on);
  }

  /** A new channel number was typed onto a channel key. */
  protected onChannelTyped(key: number, event: Event): void {
    const field = event.target as HTMLInputElement;
    const channel = wholeNumber(field.value, CHANNEL_COUNT);
    if (channel !== null) this.store.setIndependent(key, channel);
    // Whatever was typed: show the channel the key switches now.
    field.value = String(this.store.independents()[key - 1]);
  }

  /** A new memory number was typed onto a memory key. */
  protected onMemoryTyped(key: number, event: Event): void {
    const field = event.target as HTMLInputElement;
    const memory = wholeNumber(field.value, MEMORY_COUNT);
    if (memory !== null) this.store.setMemoryIndependent(key, memory);
    field.value = String(this.store.memoryIndependents()[key - 1]);
  }

  /** Enter finishes typing: leaving the field takes the number over. */
  protected leaveField(event: Event): void {
    (event.target as HTMLInputElement).blur();
  }
}

/** A whole number from 1 to `highest`, or null for any other text. */
function wholeNumber(text: string, highest: number): number | null {
  const number = Number(text.trim());
  return Number.isInteger(number) && number >= 1 && number <= highest ? number : null;
}
