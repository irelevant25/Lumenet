import { momentText, parseMoment } from './moments';

describe('the moments of a sound', () => {
  it('are written in minutes and seconds to the hundredth', () => {
    expect(momentText(0)).toBe('0:00.00');
    expect(momentText(31.25)).toBe('0:31.25');
    expect(momentText(75.5)).toBe('1:15.50');
    expect(momentText(59.999)).toBe('1:00.00'); // rounded first, then cut into minutes
  });

  it('are read as typed: minutes and seconds, or seconds, with a dot or a comma', () => {
    expect(parseMoment('1:15.5')).toBe(75.5);
    expect(parseMoment(' 75.5 ')).toBe(75.5);
    expect(parseMoment('1:15,25')).toBe(75.25);
    expect(parseMoment('0:31.256')).toBe(31.26);
    expect(parseMoment('2:59.99')).toBe(179.99);
    expect(parseMoment('9:99')).toBeNull(); // after minutes, seconds under 60
    expect(parseMoment('')).toBeNull();
    expect(parseMoment('soon')).toBeNull();
    expect(parseMoment('-3')).toBeNull();
  });
});
