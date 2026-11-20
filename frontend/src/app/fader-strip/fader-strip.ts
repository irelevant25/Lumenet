import { Component, computed, input, model, output } from '@angular/core';
import { LampLevel } from '../desk/lamps';
import { NOTE_LINE_LENGTH, joinNote, splitNote } from '../desk/notes';
import { Fader } from '../fader/fader';
import { LampButton, LampTone } from '../lamp-button/lamp-button';

/** A key that is used in no step. One shared list, so that the keys have nothing to redraw. */
const NO_STEPS: readonly number[] = [];

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
  /** True for a memory that holds a sequence: its key is red running into yellow. */
  readonly sequence = input(false);
  protected readonly keyTone = computed<LampTone>(() => (this.sequence() ? 'sequence' : this.kind()));
  /**
   * The key under the fader was pressed. What that does is the section's business: the fader to
   * full and back to zero, or what the lit record key wants.
   */
  readonly keyPress = output<void>();
  /**
   * The key goes down (true) and up again (false), by a pointer or by Space or Enter while it
   * has the focus: while SOLO is lit the fader is at full while it is held.
   */
  readonly keyHold = output<boolean>();
  /**
   * While a sequence is recorded: the numbers of its steps that use this channel or memory.
   * They stand in yellow in the corner of the key. Nothing given means none.
   */
  readonly steps = input(NO_STEPS, {
    transform: (steps: readonly number[] | undefined) => steps ?? NO_STEPS,
  });
  /** A short text of the owner's, for example which light this is. Empty: nothing is shown. */
  readonly note = model('');
  /** True while the note cannot be changed: the stack is live. */
  readonly locked = input(false);

  protected readonly name = computed(
    () => `${this.kind() === 'channel' ? 'Channel' : 'Memory'} ${this.number()}`,
  );

  /** What a screen reader calls the key: "Channel 5 key", and the steps it is used in. */
  protected readonly keyName = computed(() => {
    const steps = this.steps();
    if (steps.length === 0) return `${this.name()} key`;
    return `${this.name()} key, in ${steps.length === 1 ? 'step' : 'steps'} ${steps.join(', ')}`;
  });

  // The main button of a mouse, or a finger or a pen. The key keeps the pointer until it is
  // let go, so that a SOLO hold ends also when the pointer has left the key by then.
  protected onPointerDown(event: PointerEvent): void {
    if (event.button !== 0) return;
    (event.currentTarget as Element).setPointerCapture?.(event.pointerId);
    this.keyHold.emit(true);
  }

  protected onKey(event: KeyboardEvent, down: boolean): void {
    if ((event.key === ' ' || event.key === 'Enter') && !event.repeat) this.keyHold.emit(down);
  }

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
