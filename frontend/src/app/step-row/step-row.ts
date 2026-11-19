import { Component, input, output } from '@angular/core';
import {
  CHANNEL_COUNT,
  MAX_LEVEL,
  MAX_STEP_SECONDS,
  MEMORY_COUNT,
  StepTarget,
  TimedStep,
} from '../desk/desk-types';

/** The three times of a step. */
type TimeField = 'up' | 'wait' | 'down';

/**
 * One row of a table of steps: what the step stands for (a channel or a memory and its
 * number), its value, and its three times. Put it on a table row: `<tr lmStepRow>`. Cells that
 * belong before or after these six are given by whoever uses the row, marked `before` and `after`.
 *
 * A field is taken over when it is left or Enter is pressed. A text that is no number in the
 * allowed range is put back as it was.
 */
@Component({
  selector: 'tr[lmStepRow]',
  styleUrl: './step-row.css',
  templateUrl: './step-row.html',
})
export class StepRow {
  readonly step = input.required<TimedStep>();
  /** What a screen reader calls this row, for example "Step 2". */
  readonly label = input.required<string>();
  /** The step with one field changed. */
  readonly stepChange = output<TimedStep>();

  protected readonly maxSeconds = MAX_STEP_SECONDS;

  /** The highest channel or memory number the step can have. */
  protected highest(): number {
    return this.step().target === 'channel' ? CHANNEL_COUNT : MEMORY_COUNT;
  }

  /** Seconds are shown with one digit after the point. */
  protected seconds(value: number): string {
    return value.toFixed(1);
  }

  protected onTarget(event: Event): void {
    const target = (event.target as HTMLSelectElement).value as StepTarget;
    const highest = target === 'channel' ? CHANNEL_COUNT : MEMORY_COUNT;
    // a memory has no number above 128: a step that pointed at channel 300 points at memory 128
    this.stepChange.emit({ ...this.step(), target, number: Math.min(this.step().number, highest) });
  }

  protected onNumber(event: Event): void {
    const field = event.target as HTMLInputElement;
    const number = whole(field.value, 1, this.highest());
    if (number === null) field.value = String(this.step().number);
    else if (number !== this.step().number) this.stepChange.emit({ ...this.step(), number });
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
