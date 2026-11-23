import { Component, computed, inject, linkedSignal } from '@angular/core';
import { DeskSettings } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import { MEMORY_COUNT, StepTarget } from '../desk/desk-types';
import { KeyColumns } from '../desk/key-columns';
import { memoryLamp } from '../desk/lamps';
import { channelField, channelFromText, channelLabel, channelNumber, universeOf } from '../desk/universes';
import { LampButton, LampTone } from '../lamp-button/lamp-button';

/**
 * The independents: a column at the right edge. The green keys each switch a channel between
 * zero and full, the red ones each switch the fader of a memory; Settings says how many of each
 * are shown (0 to 32, 10 at first; with none of either the main view leaves the section out).
 * The channel or memory a key switches is printed on it; Edit lets the owner change those
 * numbers. Keys that do not fit into one column go on in another.
 */
@Component({
  imports: [KeyColumns, LampButton],
  selector: 'lm-independents',
  styleUrl: './independents.css',
  templateUrl: './independents.html',
})
export class Independents {
  protected readonly store = inject(DeskStore);
  private readonly settings = inject(DeskSettings);

  /** Edit, and the keys of both kinds that are shown. */
  protected readonly keyCount = computed(() => 1 + this.settings.channelKeys() + this.settings.memoryKeys());

  /**
   * True while the numbers on the keys can be changed. A stack that goes live ends the
   * editing: then nothing that is stored can be changed, and Edit is locked.
   */
  protected readonly editing = linkedSignal({
    source: this.store.live,
    computation: () => false,
  });

  /**
   * The channel keys that are shown: which channel each one switches, and whether that channel is
   * up. A key prints the channel's number in its universe, and in a project of several universes
   * the universe under it.
   */
  protected readonly channelKeys = computed(() => {
    const levels = this.store.channels();
    const universes = this.store.universes();
    return this.store
      .independents()
      .slice(0, this.settings.channelKeys())
      .map((channel, index) => ({
        number: index + 1,
        channel,
        caption: channelNumber(channel),
        universe: universes > 1 ? `U${universeOf(channel)}` : null,
        label: channelLabel(channel),
        field: channelField(channel, universes),
        on: levels[channel - 1] > 0,
      }));
  });

  /**
   * The memory keys that are shown: which memory each one switches, and how lit it is, like the
   * key under that memory's fader. Their numbers for screen readers follow those of the channel
   * keys that are shown: with 10 of those, 11 and on.
   */
  protected readonly memoryKeys = computed(() => {
    const levels = this.store.memoryLevels();
    const stored = this.store.memoryStored();
    const sequence = this.store.memorySequence();
    const channelKeys = this.settings.channelKeys();
    return this.store
      .memoryIndependents()
      .slice(0, this.settings.memoryKeys())
      .map((memory, index) => ({
        number: index + 1,
        name: channelKeys + index + 1,
        memory,
        on: levels[memory - 1] > 0,
        lamp: memoryLamp(levels[memory - 1], stored[memory - 1]),
        tone: (sequence[memory - 1] ? 'sequence' : 'memory') as LampTone,
      }));
  });

  protected toggleEditing(): void {
    this.editing.update((on) => !on);
  }

  /**
   * A key goes down: while a solo is lit it holds its channel or memory until it is let go. The
   * key keeps the pointer until then, so that the hold ends also when the pointer has left it.
   */
  protected hold(kind: StepTarget, key: number, event: PointerEvent): void {
    if (event.button !== 0) return;
    (event.currentTarget as Element).setPointerCapture?.(event.pointerId);
    this.store.holdIndependent(kind, key, true);
  }

  /** The same with Space or Enter on a key that has the focus. */
  protected holdByKey(kind: StepTarget, key: number, event: KeyboardEvent, down: boolean): void {
    if ((event.key === ' ' || event.key === 'Enter') && !event.repeat) this.store.holdIndependent(kind, key, down);
  }

  /** A new channel was typed onto a channel key: "113 U2", or "113" in the universe of its channel (see universes.ts). */
  protected onChannelTyped(key: number, event: Event): void {
    const field = event.target as HTMLInputElement;
    const channel = channelFromText(field.value, this.store.channelCount(), universeOf(this.store.independents()[key - 1]));
    if (channel !== null) this.store.setIndependent(key, channel);
    // Whatever was typed: show the channel the key switches now.
    field.value = channelField(this.store.independents()[key - 1], this.store.universes());
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

/** A whole number from 1 to `highest` (a memory), or null for any other text. */
function wholeNumber(text: string, highest: number): number | null {
  const number = Number(text.trim());
  return Number.isInteger(number) && number >= 1 && number <= highest ? number : null;
}
