import { noteInOneLine } from './notes';

/**
 * One line of the list from which the number of a step is chosen: the number of a channel or
 * memory, and after it the note that stands under that fader.
 */
export interface StepChoice {
  readonly number: number;
  /** What the line shows, for example "5: Front left PAR", or "6" for a fader without a note. */
  readonly text: string;
}

/** The lists for the two kinds of step. */
export interface StepChoices {
  readonly channel: readonly StepChoice[];
  readonly memory: readonly StepChoice[];
}

/**
 * The choice for one channel or memory: "5: Front left PAR", the note's two lines with a space
 * between them. Without a note the line is the number alone.
 */
export function stepChoice(number: number, note: string): StepChoice {
  const words = noteInOneLine(note);
  return { number, text: words === '' ? String(number) : `${number}: ${words}` };
}

/**
 * The choices for the numbers that `offered` lets through, with the note of each. `notes`
 * holds the note of number 1 at index 0, and so on.
 */
export function stepChoices(
  notes: readonly string[],
  offered: (number: number) => boolean = () => true,
): StepChoice[] {
  const choices: StepChoice[] = [];
  notes.forEach((note, index) => {
    if (offered(index + 1)) choices.push(stepChoice(index + 1, note));
  });
  return choices;
}

/**
 * The list a step shows: `choices`, and the number the step has if the list does not offer it
 * (a memory that has been emptied since), so that the step can still show where it points.
 */
export function withCurrent(choices: readonly StepChoice[], current: number): readonly StepChoice[] {
  if (choices.some((choice) => choice.number === current)) return choices;
  return [...choices, { number: current, text: String(current) }].sort(
    (left, right) => left.number - right.number,
  );
}
