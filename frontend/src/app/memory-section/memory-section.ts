import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { MEMORY_COUNT, stripNumbers } from '../desk/desk-types';
import { heldMemory, memoryBlinks } from '../desk/function-mode';
import { LampLevel, memoryLamp, memoryPageLamp } from '../desk/lamps';
import { FaderStrip } from '../fader-strip/fader-strip';
import { LampButton } from '../lamp-button/lamp-button';

/** The memories: one row of faders, and the keys that choose which memories the row shows. */
@Component({
  imports: [FaderStrip, LampButton],
  selector: 'lm-memory-section',
  styleUrl: './memory-section.css',
  templateUrl: './memory-section.html',
})
export class MemorySection {
  protected readonly store = inject(DeskStore);

  /** The memory numbers on the page that is shown. */
  protected readonly numbers = computed(() =>
    stripNumbers(this.store.firstMemory(), this.store.memoriesPerPage()),
  );

  /** The page keys: full for the page that is shown, half for a page that holds or plays something. */
  protected readonly pages = computed(() => {
    const levels = this.store.memoryLevels();
    const stored = this.store.memoryStored();
    const perPage = this.store.memoriesPerPage();
    const shown = this.store.firstMemory();
    return Array.from({ length: MEMORY_COUNT / perPage }, (_, page) => {
      const first = page * perPage + 1;
      return {
        first,
        last: first + perPage - 1,
        shown: first === shown,
        lamp: memoryPageLamp(levels, stored, first, perPage, first === shown),
      };
    });
  });

  protected lamp(memory: number): LampLevel {
    // The memory being edited, copied from or recorded into stays fully lit, so it is clear which one it is.
    if (heldMemory(this.store.mode()) === memory) return 3;
    return memoryLamp(this.store.memoryLevels()[memory - 1], this.store.memoryStored()[memory - 1]);
  }

  protected blinks(memory: number): boolean {
    return memoryBlinks(this.store.mode(), memory, this.store.memoryContent(memory));
  }

  /**
   * True for a memory that holds a sequence, and for the one a sequence is being recorded
   * into, which holds none until its first step: its key runs from red to yellow at once.
   */
  protected isSequence(memory: number): boolean {
    const mode = this.store.mode();
    return this.store.memorySequence()[memory - 1] || (mode.kind === 'recording' && mode.memory === memory);
  }

  /** The steps of the sequence being recorded that use this memory. */
  protected steps(memory: number): readonly number[] | undefined {
    return this.store.recordedSteps()?.memories.get(memory);
  }
}
