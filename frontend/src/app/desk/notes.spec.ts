import { NOTE_LINE_LENGTH, NOTE_LINE_SEPARATOR, joinNote, splitNote } from './notes';

describe('notes', () => {
  it('a note with one line has an empty second line', () => {
    expect(splitNote('')).toEqual(['', '']);
    expect(splitNote('Front left')).toEqual(['Front left', '']);
    expect(joinNote('Front left', '')).toBe('Front left');
  });

  it('two lines travel as one text and come back as two lines', () => {
    const note = joinNote(' Moving ', ' head pan ');
    expect(note).toBe('Moving' + NOTE_LINE_SEPARATOR + 'head pan');
    expect(splitNote(note)).toEqual(['Moving', 'head pan']);
  });

  it('text that stands only in the second line stays in the second line', () => {
    const note = joinNote('', ' Dimmer ');
    expect(note).toBe(NOTE_LINE_SEPARATOR + 'Dimmer');
    expect(splitNote(note)).toEqual(['', 'Dimmer']);
    expect(joinNote('  ', '  ')).toBe('');
  });

  it('two full lines stay within the 24 characters the backend allows', () => {
    const full = 'x'.repeat(NOTE_LINE_LENGTH);
    expect(joinNote(full, full).length).toBeLessThanOrEqual(24);
  });
});
