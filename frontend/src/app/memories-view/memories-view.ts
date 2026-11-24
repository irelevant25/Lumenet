import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { CellEdit, CellEditor } from '../cell-editor/cell-editor';
import { DeskStore } from '../desk/desk-store';
import { STRIPS_PER_LINE_PHONE, UNIVERSE_SIZE, stripNumbers } from '../desk/desk-types';
import { LampLevel } from '../desk/lamps';
import { storedMemoryPages } from '../desk/memory-pages';
import { splitNote } from '../desk/notes';
import { channelLabel, channelNumber, universeStart } from '../desk/universes';
import { LampButton, LampTone } from '../lamp-button/lamp-button';
import { NoteLines } from '../note-lines/note-lines';

/** One cell of the table: a channel, the level the memory holds for it, and the channel's note. */
interface Cell {
  channel: number;
  /** The number in the corner: the channel's number in its universe, 1 to 512. */
  number: number;
  level: number;
  /** The lines of the channel's note, one under the other; null without a note. */
  note: string | null;
  /** What a screen reader says: "Channel 5 (U1) (Front left): 255". */
  name: string;
}

/** A cell that is being changed: which, what it held, and where its fields lie over the table. */
interface Editing {
  readonly memory: number;
  readonly channel: number;
  readonly channelName: string;
  readonly level: number;
  readonly note: string;
  /** What the stage plan says the channel is, shown dimmed where the note is empty. */
  readonly fixture: string;
  /** True for a memory that holds a sequence: only the note can be changed. */
  readonly sequence: boolean;
  readonly start: 'level' | 'note';
  readonly left: number;
  readonly top: number;
  readonly width: number;
  readonly height: number;
}

/** The fields over a cell are at least this wide, for three digits and a few letters of a note. */
const EDITOR_MIN_WIDTH = 64;

/**
 * The Memories view: what one memory holds, as a table of all 512 channels of a universe (in a
 * project of several, a U key for each chooses which). A memory key chooses the memory to look
 * at; under each key stands the note of its memory.
 *
 * A double click changes what it shows: on the note under a memory key, that note; on a cell,
 * the level the memory gives the channel, or (under the level) the channel's note. Not while the
 * stack is live, which locks everything that is stored.
 */
@Component({
  imports: [CellEditor, LampButton, NoteLines],
  selector: 'lm-memories-view',
  styleUrl: './memories-view.css',
  templateUrl: './memories-view.html',
})
export class MemoriesView {
  protected readonly store = inject(DeskStore);
  private readonly tableBox = viewChild.required<ElementRef<HTMLElement>>('tableBox');

  /** The cell whose fields lie over the table; null while none is changed. */
  protected readonly editing = signal<Editing | null>(null);

  /** The universe chosen with the U keys; the last one there is when the project has fewer. */
  private readonly chosenUniverse = signal(1);
  protected readonly universe = computed(() => Math.min(this.chosenUniverse(), this.store.universes()));

  /** The U keys, in a project of more than one universe: full for the one shown, half for one the memory gives a level. */
  protected readonly universeKeys = computed(() => {
    const universes = this.store.universes();
    if (universes < 2) return [];
    const look = this.look();
    return Array.from({ length: universes }, (_, index) => {
      const start = universeStart(index + 1) - 1;
      const used = !!look && look.slice(start, start + UNIVERSE_SIZE).some((level) => level > 0);
      return { universe: index + 1, shown: index + 1 === this.universe(), lamp: (index + 1 === this.universe() ? 3 : used ? 2 : 1) as LampLevel };
    });
  });

  protected showUniverse(universe: number): void {
    this.chosenUniverse.set(universe);
  }

  /**
   * The memory numbers on the page that is shown: as many as faders stand in a row on this window.
   * What Settings says about the main view does not count here.
   */
  protected readonly numbers = computed(() =>
    stripNumbers(this.store.viewFirstMemory(), this.store.viewMemoriesPerPage()),
  );

  /** The page keys: full for the page that is shown, half for a page with a stored memory. */
  protected readonly pages = computed(() =>
    storedMemoryPages(
      this.store.memoryStored(),
      this.store.viewMemoriesPerPage(),
      this.store.viewFirstMemory(),
    ),
  );

  /** The look of the memory that is looked at, or null when it holds none. */
  private readonly look = computed(
    () => this.store.memoryLooks()[this.store.inspectedMemory()] ?? null,
  );

  /** True when any channel has a note: then every cell of the table keeps room for one. */
  protected readonly withNotes = computed(() =>
    this.store.channelNames().some((note) => note.trim() !== ''),
  );

  /**
   * The rows of the table: the 512 channels of the universe shown, as many in a row as memory
   * keys stand in a line (32, 16, or 8 on a phone), each with the note of its channel.
   */
  protected readonly rows = computed(() => {
    const look = this.look();
    const notes = this.store.channelNames();
    const perRow = this.store.phone() ? STRIPS_PER_LINE_PHONE : this.store.windowStripsPerRow();
    const start = universeStart(this.universe());
    const rows: Cell[][] = [];
    for (let first = start; first < start + UNIVERSE_SIZE; first += perRow) {
      rows.push(
        stripNumbers(first, perRow).map((channel) => {
          const level = look?.[channel - 1] ?? 0;
          const lines = splitNote(notes[channel - 1] ?? '')
            .map((line) => line.trim())
            .filter((line) => line !== '');
          const said = lines.length ? ` (${lines.join(' ')})` : '';
          return {
            channel,
            number: channelNumber(channel),
            level,
            note: lines.length ? lines.join('\n') : null,
            name: `Channel ${channelLabel(channel)}${said}: ${level}`,
          };
        }),
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
    return `${name}: ${up} of ${look.length} ${up === 1 ? 'channel is' : 'channels are'} above zero.`;
  });

  /**
   * A double click on a cell lays the fields over it: the level, and the channel's note. The one
   * double-clicked gets the focus: the note when the click was under the level of a table that
   * shows notes.
   */
  protected onTableDouble(event: MouseEvent): void {
    const cell = (event.target as Element).closest<HTMLElement>('td[data-channel]');
    if (!cell || this.store.live() || this.editing()) return;

    const memory = this.store.inspectedMemory();
    const channel = universeStart(this.universe()) - 1 + Number(cell.dataset['channel']);
    const box = this.tableBox().nativeElement;
    const frame = box.getBoundingClientRect();
    const place = cell.getBoundingClientRect();
    const width = Math.max(place.width, EDITOR_MIN_WIDTH);

    const underLevel = this.withNotes() && event.clientY > levelBottom(cell);

    this.editing.set({
      memory,
      channel,
      channelName: `Channel ${channelLabel(channel)}`,
      level: this.look()?.[channel - 1] ?? 0,
      note: this.store.channelNotes()[channel - 1] ?? '',
      fixture: this.store.fixtureLabels()[channel - 1] ?? '',
      sequence: this.store.memorySequence()[memory - 1],
      start: underLevel ? 'note' : 'level',
      left: Math.max(0, Math.min(place.left - frame.left + box.scrollLeft, box.clientWidth - width)),
      top: place.top - frame.top + box.scrollTop,
      width,
      height: place.height,
    });
  }

  /** The fields are left: what was typed goes to the desk, the level and the note each if it changed. */
  protected finishEdit(typed: CellEdit | null): void {
    const edit = this.editing();
    this.editing.set(null);
    if (!edit || !typed) return;
    if (!edit.sequence && typed.level !== edit.level) this.store.setMemoryChannel(edit.memory, edit.channel, typed.level);
    if (typed.note !== edit.note) this.store.setChannelNote(edit.channel, typed.note);
  }

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

/**
 * Where the level of a cell ends, down the window: the level is the cell's text, the note is drawn
 * under it. A browser that cannot measure text has the whole cell for the level.
 */
function levelBottom(cell: HTMLElement): number {
  const text = document.createRange();
  text.selectNodeContents(cell);
  return typeof text.getBoundingClientRect === 'function' ? text.getBoundingClientRect().bottom + 1 : Infinity;
}
