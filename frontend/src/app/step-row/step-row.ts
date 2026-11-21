import {
  ChangeDetectorRef,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import {
  CHANNEL_COUNT,
  MAX_LEVEL,
  MAX_STEP_SECONDS,
  MEMORY_COUNT,
  StepKind,
  TimedStep,
} from '../desk/desk-types';
import { StepChoices, withCurrent } from '../desk/step-choices';

/** The three times of a step. */
type TimeField = 'up' | 'wait' | 'down';

/**
 * The one row, of all tables, whose field for the number holds the whole list right now: the
 * row whose field is in use. See the note about the list at StepRow.
 */
const rowWithList = signal<StepRow | null>(null);

/**
 * One row of a table of steps: what the step stands for (a channel, a memory, a fixture or a
 * group, and which), the note of that channel or memory or the name of the fixture or group
 * (with the color of a fixture or group before it, a press on which asks for another), the
 * step's value, and its three times. Put it on a table row: `<tr lmStepRow>`. Cells that belong
 * before or after these seven are given by whoever uses the row, marked `before` and `after`.
 *
 * The number is chosen from a list that shows every number with the note under its fader,
 * like "5: Front left". The list is long (512 channels), so only one row carries it at a time:
 * the one whose field is in use. A table of a hundred steps would otherwise hold fifty thousand
 * entries. Every other row holds just its number, which is also all a closed field shows: the
 * note has a column of its own.
 *
 * A typed field is taken over when it is left or Enter is pressed. A text that is no number in
 * the allowed range is put back as it was.
 */
@Component({
  selector: 'tr[lmStepRow]',
  styleUrl: './step-row.css',
  templateUrl: './step-row.html',
})
export class StepRow {
  private readonly changes = inject(ChangeDetectorRef);

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      if (rowWithList() === this) rowWithList.set(null);
    });
  }

  readonly step = input.required<TimedStep>();
  /** What a screen reader calls this row, for example "Step 2". */
  readonly label = input.required<string>();
  /** The numbers a step can be given, for a channel and for a memory, each with its note. */
  readonly choices = input.required<StepChoices>();
  /** The note under the fader of the step's channel or memory, as one line. It is shown, not changed here. */
  readonly note = input('');
  /** True while the step cannot be changed: the stack is live. */
  readonly locked = input(false);
  /** The step with one field changed. */
  readonly stepChange = output<TimedStep>();
  /** The color of a step of a fixture or a group was pressed: whoever uses the row asks for another. */
  readonly colorPick = output<void>();

  protected readonly maxSeconds = MAX_STEP_SECONDS;

  /** True while this row's field for the number holds the whole list. */
  protected readonly listed = computed(() => rowWithList() === this);

  /** True for a step of a fixture or a group, which has a color. */
  protected readonly lights = computed(() => isLights(this.step().target));

  /** The list for the kind of step this is, with the step's own number in it for sure. */
  protected readonly list = computed(() =>
    withCurrent(this.choices()[this.step().target], this.step().number, this.lights()),
  );

  /** What the closed field shows: the number; a fixture's address, a group's "G2". */
  protected readonly shown = computed(
    () => this.list().find((choice) => choice.number === this.step().number)?.short ?? String(this.step().number),
  );

  /** Seconds are shown with one digit after the point. */
  protected seconds(value: number): string {
    return value.toFixed(1);
  }

  /**
   * The field for the number is about to be used: by the pointer, or by the keyboard. The
   * whole list goes into this row now, before the browser opens it, and the row that had it
   * lets it go.
   */
  protected fillList(): void {
    if (this.listed()) return;
    rowWithList.set(this);
    this.changes.detectChanges(); // at once: the browser opens the list right after this event
  }

  /** The field for the number was left: it goes back to holding its number alone. */
  protected dropList(): void {
    if (this.listed()) rowWithList.set(null);
  }

  protected onTarget(event: Event): void {
    const target = (event.target as HTMLSelectElement).value as StepKind;
    const { color, ...rest } = this.step();
    const step = { ...rest, target, number: this.numberFor(target) };
    // a fixture or a group keeps the color it had; a channel or a memory has none
    this.stepChange.emit(isLights(target) ? { ...step, color: color ?? null } : step);
  }

  protected onNumber(event: Event): void {
    const number = Number((event.target as HTMLSelectElement).value);
    if (number !== this.step().number) this.stepChange.emit({ ...this.step(), number });
  }

  protected onValue(event: Event): void {
    const field = event.target as HTMLInputElement;
    const value = whole(field.value, 0, MAX_LEVEL);
    if (value === null) field.value = String(this.step().value);
    else if (value !== this.step().value) this.stepChange.emit({ ...this.step(), value });
  }

  protected onTime(which: TimeField, event: Event): void {
    const field = event.target as HTMLInputElement;
    const time = secondsFrom(field.value);
    if (time === null) {
      field.value = this.seconds(this.step()[which]);
      return;
    }
    field.value = this.seconds(time); // "2" is shown as "2.0"
    if (time !== this.step()[which]) this.stepChange.emit({ ...this.step(), [which]: time });
  }

  /** Enter finishes typing: leaving the field takes the text over. */
  protected finish(event: Event): void {
    (event.target as HTMLInputElement).blur();
  }

  /**
   * The number a step gets when it is switched to another kind: the number it has if that kind
   * offers it, else the first number on offer, else the nearest number there is (for a fixture
   * or a group, while the plan has none: 0, none).
   */
  private numberFor(target: StepKind): number {
    const old = this.step().number;
    const offered = this.choices()[target];
    if (offered.some((choice) => choice.number === old)) return old;
    if (offered[0]) return offered[0].number;
    return isLights(target) ? 0 : Math.min(Math.max(1, old), target === 'channel' ? CHANNEL_COUNT : MEMORY_COUNT);
  }
}

/** True for a step of a fixture or a group of the stage plan. */
function isLights(target: StepKind): boolean {
  return target === 'fixture' || target === 'group';
}

/** A whole number from `min` to `max`, or null for any other text. */
function whole(text: string, min: number, max: number): number | null {
  const trimmed = text.trim();
  if (!/^\d+$/.test(trimmed)) return null;
  const number = Number(trimmed);
  return number >= min && number <= max ? number : null;
}

/** Seconds from a text like "2", "1.5" or "1,5", rounded to tenths; null for any other text. */
function secondsFrom(text: string): number | null {
  const trimmed = text.trim().replace(',', '.');
  if (!/^\d+(\.\d*)?$|^\.\d+$/.test(trimmed)) return null;
  const tenths = Math.round(Number(trimmed) * 10) / 10;
  return tenths <= MAX_STEP_SECONDS ? tenths : null;
}
