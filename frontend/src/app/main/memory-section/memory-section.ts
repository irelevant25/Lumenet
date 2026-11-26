import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../../desk/desk-store';
import { MEMORY_COUNT, stripNumbers } from '../../desk/desk-types';
import { heldMemory, memoryBlinks } from '../../desk/function-mode';
import { LampLevel, memoryLamp, memoryPageLamp } from '../../desk/lamps';
import { DragScroll } from '../../shared/drag-scroll';
import { PressedInSight } from '../../shared/pressed-in-sight';
import { FaderStrip } from '../../shared/fader-strip/fader-strip';
import { LampButton } from '../../shared/lamp-button/lamp-button';

/**
 * The memories: one row of faders (or two, as Settings says), and the keys that choose which
 * memories the rows show. Like the channels, the page keys stand in one column that scrolls when
 * needed, and the rows scroll sideways only when Settings makes them wider than the window (on
 * their own, not with the channels). The key under a fader puts it to full and back to zero, or does what the lit
 * record key wants.
 */
@Component({
  imports: [DragScroll, FaderStrip, LampButton, PressedInSight],
  selector: 'lm-memory-section',
  styleUrl: './memory-section.css',
  templateUrl: './memory-section.html',
})
export class MemorySection {
  protected readonly store = inject(DeskStore);

  /** The memory numbers of the rows on the page that is shown. */
  protected readonly rows = computed(() => {
    const first = this.store.firstMemory();
    const perRow = this.store.stripsPerRow();
    const count = this.store.memoriesPerPage() / perRow;
    return Array.from({ length: count }, (_, row) => stripNumbers(first + row * perRow, perRow));
  });

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
