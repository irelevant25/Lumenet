import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { CHANNEL_COUNT, STRIPS_PER_LINE_PHONE, stripNumbers } from '../desk/desk-types';
import { LampLevel } from '../desk/lamps';
import { storedMemoryPages } from '../desk/memory-pages';
import { splitNote } from '../desk/notes';
import { LampButton, LampTone } from '../lamp-button/lamp-button';
import { NoteLines } from '../note-lines/note-lines';

/** One cell of the table: a channel and the level the memory holds for it. */
interface Cell {
  channel: number;
  level: number;
}

/**
 * The Memories view: what one memory holds, as a table of all 512 channels. It only shows;
 * nothing here changes the desk. A memory key chooses the memory to look at; under each key
 * stands the note of its memory, to read.
 */
@Component({
  imports: [LampButton, NoteLines],
  selector: 'lm-memories-view',
  styleUrl: './memories-view.css',
  templateUrl: './memories-view.html',
})
export class MemoriesView {
  protected readonly store = inject(DeskStore);

  /** The memory numbers on the page that is shown: as many as memory faders stand in a row. */
  protected readonly numbers = computed(() =>
    stripNumbers(this.store.firstMemory(), this.store.memoriesPerPage()),
  );

  /** The page keys: full for the page that is shown, half for a page with a stored memory. */
  protected readonly pages = computed(() =>
    storedMemoryPages(
      this.store.memoryStored(),
      this.store.memoriesPerPage(),
      this.store.firstMemory(),
    ),
  );

  /** The look of the memory that is looked at, or null when it holds none. */
  private readonly look = computed(
    () => this.store.memoryLooks()[this.store.inspectedMemory()] ?? null,
  );

  /**
   * The rows of the table: all 512 channels, as many in a row as memory keys stand in a line
   * (32, 16, or 8 on a phone).
   */
  protected readonly rows = computed(() => {
    const look = this.look();
    const perRow = this.store.phone() ? STRIPS_PER_LINE_PHONE : this.store.stripsPerRow();
    const rows: Cell[][] = [];
    for (let first = 1; first <= CHANNEL_COUNT; first += perRow) {
      rows.push(
        stripNumbers(first, perRow).map((channel) => ({ channel, level: look?.[channel - 1] ?? 0 })),
      );
    }
    return rows;
  });

  /** One sentence about the memory that is looked at. */
  protected readonly summary = computed(() => {
    const memory = this.store.inspectedMemory();
    const look = this.look();
    if (!look) {
      if (!this.store.memoryLooksKnown()) {
        return 'The desk program is older than this page and cannot say what the memories hold. Start run.cmd again.';
      }
      if (this.store.memorySequence()[memory - 1]) {
        return `Memory ${memory} holds a sequence, not a look. Its steps are in the Sequences view.`;
      }
      return `Memory ${memory} is empty.`;
    }

    const note = splitNote(this.store.memoryNotes()[memory - 1]).join(' ').trim();
    const name = note ? `Memory ${memory} (${note})` : `Memory ${memory}`;
    const up = look.filter((level) => level > 0).length;
    if (up === 0) return `${name} is stored, with every channel at zero.`;
    return `${name}: ${up} of ${CHANNEL_COUNT} ${up === 1 ? 'channel is' : 'channels are'} above zero.`;
  });

  /** Full for the memory that is looked at, half for any other that holds something. */
  protected keyLamp(memory: number): LampLevel {
    if (memory === this.store.inspectedMemory()) return 3;
    return this.store.memoryStored()[memory - 1] ? 2 : 1;
  }

  /** Red, or red running into yellow for a memory that holds a sequence. */
  protected keyTone(memory: number): LampTone {
    return this.store.memorySequence()[memory - 1] ? 'sequence' : 'memory';
  }
}
