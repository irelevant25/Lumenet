import { Component, booleanAttribute, computed, input, model, output } from '@angular/core';
import { LampLevel } from '../desk/lamps';
import { NOTE_LINE_LENGTH, joinNote, splitNote } from '../desk/notes';
import { Fader } from '../fader/fader';
import { LampButton, LampTone } from '../lamp-button/lamp-button';

/** One fader with its level number above it, its numbered key below it, and a note under the key. */
@Component({
  host: {
    '[class.up]': 'value() > 0',
  },
  imports: [Fader, LampButton],
  selector: 'lm-fader-strip',
  styleUrl: './fader-strip.css',
  templateUrl: './fader-strip.html',
})
export class FaderStrip {
  readonly kind = input.required<'channel' | 'memory'>();
  /** The channel or memory number printed on the key. */
  readonly number = input.required<number>();
  /** The level the fader shows, 0 to 255. */
  readonly value = input(0);
  /** The level the owner asks for by moving the fader. */
  readonly valueChange = output<number>();
  /**
   * The fader's own level, where a memory holds the channel higher than that. The part
   * between the two is drawn in the colour of the memories.
   */
  readonly ownLevel = input<number | null>(null);
  readonly lampLevel = input<LampLevel>(1);
  readonly blink = input(false);
  /** Memory keys can be pressed. Channel keys only show a state. */
  readonly pressable = input(false, { transform: booleanAttribute });
  /** True for a memory that holds a sequence: its key is red running into yellow. */
  readonly sequence = input(false);
  protected readonly keyTone = computed<LampTone>(() => (this.sequence() ? 'sequence' : this.kind()));
  readonly keyPress = output<void>();
  /** A short text of the owner's, for example which light this is. Empty: nothing is shown. */
  readonly note = model('');

  protected readonly name = computed(
    () => `${this.kind() === 'channel' ? 'Channel' : 'Memory'} ${this.number()}`,
  );

  /** The note's two lines, each in a field of its own. */
  protected readonly lines = computed(() => splitNote(this.note()));
  protected readonly lineLength = NOTE_LINE_LENGTH;

  /** The browser reports a changed line when the field is left or Enter is pressed. */
  protected onLineChange(line: 0 | 1, event: Event): void {
    const field = event.target as HTMLInputElement;
    const lines: [string, string] = [...this.lines()]; // a copy: the other line stays as it is
    lines[line] = field.value.trim();
    field.value = lines[line];
    this.note.set(joinNote(lines[0], lines[1]));
  }

  /** Enter finishes typing: leaving the field stores the note. */
  protected finishNote(event: Event): void {
    (event.target as HTMLInputElement).blur();
  }

  /** Escape puts the old text back. */
  protected cancelNote(line: 0 | 1, event: Event): void {
    const field = event.target as HTMLInputElement;
    field.value = this.lines()[line];
    field.blur();
  }
}
