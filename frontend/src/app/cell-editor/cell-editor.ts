import {
  Component,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  input,
  output,
} from '@angular/core';
import { MAX_LEVEL } from '../desk/desk-types';
import { NOTE_LINE_LENGTH, joinNote, splitNote } from '../desk/notes';

/** What was typed into a cell of the Memories view's table. */
export interface CellEdit {
  readonly level: number;
  readonly note: string;
}

/**
 * A cell of the Memories view's table while it is changed (a double click on it): the level the
 * memory gives the channel, and under it the two lines of the channel's note, each a field. The
 * field of the part that was double-clicked has the focus. Enter, or a click anywhere else, keeps
 * what was typed; Escape changes nothing. The view lays it over the cell.
 */
@Component({
  host: {
    role: 'group',
    '[attr.aria-label]': 'channelName() + " in memory " + memory()',
    '(keydown)': 'onKey($event)',
    '(focusout)': 'onFocusOut($event)',
  },
  selector: 'lm-cell-editor',
  styleUrl: './cell-editor.css',
  templateUrl: './cell-editor.html',
})
export class CellEditor {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  /** The channel, as the desk writes it: "Channel 113 (U2)". */
  readonly channelName = input.required<string>();
  readonly memory = input.required<number>();
  /** The level the memory gives the channel, 0 to 255. */
  readonly level = input.required<number>();
  /** The channel's note: two lines in one text. */
  readonly note = input('');
  /** What the stage plan says the channel is, as a note: dimmed in a line the note leaves empty, as on the main view. */
  readonly fixture = input('');
  /** True when the level cannot be changed here: the memory holds a sequence. */
  readonly levelLocked = input(false);
  /** The field that gets the focus: the level, or the first line of the note. */
  readonly start = input<'level' | 'note'>('level');
  /** What was typed; null after Escape. */
  readonly done = output<CellEdit | null>();

  protected readonly lines = computed(() => splitNote(this.note()));
  protected readonly fixtureLines = computed(() => splitNote(this.fixture()));
  protected readonly lineLength = NOTE_LINE_LENGTH;
  protected readonly maxLevel = MAX_LEVEL;
  private finished = false;

  constructor() {
    afterNextRender(() => {
      const fields = this.host.querySelectorAll('input');
      const field = this.start() === 'note' || this.levelLocked() ? fields[1] : fields[0];
      field?.focus();
      field?.select();
    });
  }

  protected onKey(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.finish(true);
    } else if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      this.finish(false);
    }
  }

  /** Leaving the cell keeps what was typed; going from field to field does not. */
  protected onFocusOut(event: FocusEvent): void {
    const next = event.relatedTarget as Node | null;
    if (next && this.host.contains(next)) return;
    this.finish(true);
  }

  private finish(keep: boolean): void {
    if (this.finished) return;
    this.finished = true;
    if (!keep) {
      this.done.emit(null);
      return;
    }

    const [level, first, second] = Array.from(this.host.querySelectorAll('input'));
    const typed = Math.round(Number(level?.value));
    const kept =
      level?.value.trim() !== '' && Number.isFinite(typed)
        ? Math.min(MAX_LEVEL, Math.max(0, typed))
        : this.level();
    this.done.emit({ level: kept, note: joinNote(first?.value ?? '', second?.value ?? '') });
  }
}
