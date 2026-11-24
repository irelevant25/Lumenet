import {
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { NOTE_LINE_LENGTH, joinNote, noteInOneLine, splitNote } from '../desk/notes';

/**
 * A note under a key: its two lines, in the lettering of the note fields under the faders of
 * the main view. A line that is too long for the key is cut off; the pointer on the note shows
 * all of it.
 *
 * Where the owner may change the note (editable), a double click turns the two lines into two
 * fields, the one clicked with the focus: Enter, or a click anywhere else, keeps what was typed;
 * Escape leaves the note as it was. Not while it is locked (the stack is live).
 */
@Component({
  host: {
    '[attr.title]': 'title()',
    '[class.editable]': 'editable() && !locked()',
    '(dblclick)': 'startEditing($event)',
  },
  selector: 'lm-note-lines',
  styleUrl: './note-lines.css',
  templateUrl: './note-lines.html',
})
export class NoteLines {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly injector = inject(Injector);

  /** The note as the desk keeps it: two lines in one text. Empty: two empty lines. */
  readonly note = input('');
  /** True where a double click may change the note. */
  readonly editable = input(false);
  /** True while the note cannot be changed: the stack is live. */
  readonly locked = input(false);
  /** What a screen reader calls the first field, like "Note for memory 5". */
  readonly label = input('Note');
  /** The note as it was typed, when typing ends with something else than before. */
  readonly noteChange = output<string>();

  protected readonly lines = computed(() => splitNote(this.note()));
  protected readonly lineLength = NOTE_LINE_LENGTH;
  /** True while the two lines are fields. */
  protected readonly editing = signal(false);

  /** The whole note in one line, for the pointer, and what a double click does where it can. */
  protected readonly title = computed(() => {
    if (this.editing()) return null;
    const whole = noteInOneLine(this.note());
    if (!this.editable() || this.locked()) return whole || null;
    return whole ? `${whole} (double click to change it)` : 'Double click to write a note';
  });

  protected startEditing(event: MouseEvent): void {
    if (!this.editable() || this.locked() || this.editing()) return;
    const clicked = (event.target as Element).closest('.line');
    const line = clicked && clicked === this.host.lastElementChild ? 1 : 0;
    this.editing.set(true);
    afterNextRender(
      () => {
        const field = this.host.querySelectorAll('input')[line];
        field?.focus();
        field?.select();
      },
      { injector: this.injector },
    );
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

  /** Leaving the fields keeps what was typed; going from one line to the other does not. */
  protected onFocusOut(event: FocusEvent): void {
    const next = event.relatedTarget as Node | null;
    if (next && this.host.contains(next)) return;
    this.finish(true);
  }

  private finish(keep: boolean): void {
    if (!this.editing()) return;
    const [first, second] = Array.from(this.host.querySelectorAll('input'));
    const note = joinNote(first?.value ?? '', second?.value ?? '');
    this.editing.set(false);
    if (keep && !this.locked() && note !== joinNote(...this.lines())) this.noteChange.emit(note);
  }
}
