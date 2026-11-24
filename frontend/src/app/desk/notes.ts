/**
 * A note has two lines. It travels and is stored as one text, with this character
 * between the lines: the Unicode "line separator". (An ordinary line break is not
 * allowed in a note by the backend.)
 */
export const NOTE_LINE_SEPARATOR = '\u2028';

/** Most characters in one line of a note. Two lines and the separator stay within the 24 the backend allows. */
export const NOTE_LINE_LENGTH = 11;

/** The two lines of a note. A note without a second line gives an empty second line. */
export function splitNote(note: string): [string, string] {
  const at = note.indexOf(NOTE_LINE_SEPARATOR);
  if (at < 0) return [note, ''];
  return [note.slice(0, at), note.slice(at + 1)];
}

/**
 * One note from two lines. Each line stays where it was typed: a note with text only in its
 * second line starts with the separator.
 */
export function joinNote(first: string, second: string): string {
  const top = first.trim();
  const bottom = second.trim();
  return bottom === '' ? top : top + NOTE_LINE_SEPARATOR + bottom;
}

/** A note as one line of text: its two lines with a space between them. Empty for no note. */
export function noteInOneLine(note: string): string {
  return splitNote(note).join(' ').trim();
}

/** The most characters a line under a fader of the usual width shows whole. */
const SHORT_LINE = 7;

/**
 * A text of one line, such as the name of a light, in the two lines of a note under a fader: one
 * line when it is short, else broken at the space that leaves the shorter longest line ("Blinder
 * left": "Blinder", "left"). A field cuts off what is still too long.
 */
export function inTwoLines(text: string): [string, string] {
  const words = text.trim().replace(/\s+/g, ' ');
  let best: [string, string] = [words, ''];
  if (words.length <= SHORT_LINE) return best;
  for (let at = words.indexOf(' '); at >= 0; at = words.indexOf(' ', at + 1)) {
    const lines: [string, string] = [words.slice(0, at), words.slice(at + 1)];
    if (Math.max(lines[0].length, lines[1].length) < Math.max(best[0].length, best[1].length))
      best = lines;
  }
  return best;
}
