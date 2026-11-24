import { Component, computed, input, output } from '@angular/core';
import { NOTE_LINE_LENGTH, inTwoLines, joinNote, noteInOneLine, splitNote } from '../desk/notes';
import { Fader } from '../fader/fader';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * One fixture, or one group of fixtures, on the Fixtures page: its color on top (a press
 * chooses it, and the panel shows its color, where it points and all its channels), its
 * intensity as a number and a fader, its key (to full, and to zero), and its note.
 *
 * The note is as the one under a channel fader on the main view: two lines, each a field, in
 * white as the owner typed it; where nothing was typed, the name from the stage stands there
 * dimmed.
 */
@Component({
  host: {
    '[class.up]': 'level() > 0',
    '[class.chosen]': 'chosen()',
    '[class.group]': 'group()',
  },
  imports: [Fader, LampButton],
  selector: 'lm-fixture-strip',
  styleUrl: './fixture-strip.css',
  templateUrl: './fixture-strip.html',
})
export class FixtureStrip {
  /** The fixture's or the group's name, from the stage. */
  readonly name = input.required<string>();
  /** What its key says: the fixture's address, or the group's number. */
  readonly keyText = input.required<string>();
  /** Its intensity, 0 to 255. */
  readonly level = input(0);
  /** The intensity the owner asks for with the fader. */
  readonly levelChange = output<number>();
  /** Its color as CSS, like "#ff2000"; null for none to show (a hazer). */
  readonly color = input<string | null>(null);
  /** True while the panel shows it. */
  readonly chosen = input(false);
  /** True for a group: its fader and key are amber, those of a fixture green as the channels. */
  readonly group = input(false);
  /** True while a sequence is recorded: the key blinks, and a press puts in a step. */
  readonly blink = input(false);
  /** While a sequence is recorded: the numbers of its steps that use this fixture or group, shown on the key. */
  readonly steps = input<readonly number[]>([]);
  /** The key was pressed. */
  readonly keyPress = output<void>();
  /** The color on top was pressed: the panel shows this fixture or group. */
  readonly choose = output<void>();
  /** The note under the key: two lines in one text, empty for none. */
  readonly note = input('');
  /** The note as it was typed, when a line was left with something else in it. */
  readonly noteChange = output<string>();
  /** True while the note cannot be changed: the stack is live. */
  readonly locked = input(false);

  protected readonly lines = computed(() => splitNote(this.note()));
  protected readonly lineLength = NOTE_LINE_LENGTH;
  /** The name in the two lines, dimmed where no note was typed. */
  protected readonly nameLines = computed(() => inTwoLines(this.name()));
  /** The note, or else the name, for the pointer. */
  protected readonly noteTitle = computed(() => noteInOneLine(this.note()) || this.name());

  /** The browser reports a changed line when the field is left or Enter is pressed. */
  protected onLineChange(line: 0 | 1, event: Event): void {
    const field = event.target as HTMLInputElement;
    const lines: [string, string] = [...this.lines()]; // a copy: the other line stays as it is
    lines[line] = field.value.trim();
    field.value = lines[line];
    this.noteChange.emit(joinNote(lines[0], lines[1]));
  }

  /** Enter finishes typing: leaving the field keeps the note. */
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
