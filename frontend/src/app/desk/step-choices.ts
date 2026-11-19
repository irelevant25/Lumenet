import { noteInOneLine } from './notes';

/**
 * One line of the list from which the number of a step is chosen: the number of a channel or
 * memory at the left, and behind it the note that stands under that fader.
 */
export interface StepChoice {
  readonly number: number;
  /** What the line shows, for example "5   Front left PAR". */
  readonly text: string;
}

/** The lists for the two kinds of step. */
export interface StepChoices {
  readonly channel: readonly StepChoice[];
  readonly memory: readonly StepChoice[];
}

/*
 * A list in a browser's own dropdown is plain text, one line per choice. So that the notes
 * still stand in a column of their own, a number is followed by "figure spaces", which are as
 * wide as a digit and which the browser does not swallow: as many as bring every number to the
 * same width, and then a gap. The gap is wide on purpose. The closed field shows the text of
 * the chosen line too, and it is only wide enough for the number: the note falls outside it,
 * and the field shows the number alone, as it should. The character is made from its number
 * because it cannot be told from an ordinary space in this file.
 */
const DIGIT_WIDE_SPACE = String.fromCharCode(0x2007);
const GAP_IN_DIGITS = 5;

/**
 * The choice for one channel or memory. `digits` is how many digits the highest number of
 * the list has. Without a note the line is the number alone.
 */
export function stepChoice(number: number, note: string, digits: number): StepChoice {
  const words = noteInOneLine(note);
  if (words === '') return { number, text: String(number) };

  const fill = DIGIT_WIDE_SPACE.repeat(Math.max(0, digits - String(number).length) + GAP_IN_DIGITS);
  return { number, text: `${number}${fill}${words}` };
}

/**
 * The choices for the numbers that `offered` lets through, with the note of each. `notes`
 * holds the note of number 1 at index 0, and so on.
 */
export function stepChoices(
  notes: readonly string[],
  offered: (number: number) => boolean = () => true,
): StepChoice[] {
  const digits = String(notes.length).length;
  const choices: StepChoice[] = [];
  notes.forEach((note, index) => {
    if (offered(index + 1)) choices.push(stepChoice(index + 1, note, digits));
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
