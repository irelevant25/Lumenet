import { Component, computed, inject, linkedSignal } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { CHANNEL_COUNT } from '../desk/desk-types';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * The independents: sixteen keys that each switch one channel between zero and full.
 * The channel a key switches is printed on it. Edit lets the owner change those numbers.
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
   * True while the channel numbers on the keys can be changed. A stack that goes live ends
   * the editing: then nothing that is stored can be changed, and Edit is locked.
   */
  protected readonly editing = linkedSignal({
    source: this.store.live,
    computation: () => false,
  });

  /** The sixteen keys: which channel each one switches, and whether that channel is up. */
  protected readonly keys = computed(() => {
    const levels = this.store.channels();
    return this.store.independents().map((channel, index) => ({
      number: index + 1,
      channel,
      on: levels[channel - 1] > 0,
    }));
  });

  protected toggleEditing(): void {
    this.editing.update((on) => !on);
  }

  /** A new channel number was typed onto a key. */
  protected onChannelTyped(key: number, event: Event): void {
    const field = event.target as HTMLInputElement;
    const channel = Number(field.value.trim());
    if (Number.isInteger(channel) && channel >= 1 && channel <= CHANNEL_COUNT) {
      this.store.setIndependent(key, channel);
    }
    // Whatever was typed: show the channel the key switches now.
    field.value = String(this.store.independents()[key - 1]);
  }

  /** Enter finishes typing: leaving the field takes the number over. */
  protected leaveField(event: Event): void {
    (event.target as HTMLInputElement).blur();
  }
}
