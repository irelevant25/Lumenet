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
