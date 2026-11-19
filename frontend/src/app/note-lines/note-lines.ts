import { Component, computed, input } from '@angular/core';
import { noteInOneLine, splitNote } from '../desk/notes';

/**
 * A note under a key, to read only: its two lines, in the lettering of the note fields under
 * the faders of the main view. A line that is too long for the key is cut off; the pointer
 * on the note shows all of it. The notes are written under the faders.
 */
@Component({
  host: {
    '[attr.title]': 'whole()',
  },
  selector: 'lm-note-lines',
  styleUrl: './note-lines.css',
  templateUrl: './note-lines.html',
})
export class NoteLines {
  /** The note as the desk keeps it: two lines in one text. Empty: two empty lines. */
  readonly note = input('');

  protected readonly lines = computed(() => splitNote(this.note()));

  /** The whole note in one line, for the pointer; nothing for an empty note. */
  protected readonly whole = computed(() => noteInOneLine(this.note()) || null);
}
