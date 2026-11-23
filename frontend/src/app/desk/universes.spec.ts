import {
  channelField,
  channelFromText,
  channelLabel,
  channelNumber,
  channelSpan,
  universeOf,
  universeStart,
} from './universes';

describe('universes', () => {
  it('a universe is 512 channels, and inside the numbers go on through them', () => {
    expect([universeOf(1), universeOf(512), universeOf(513), universeOf(4096)]).toEqual([1, 1, 2, 8]);
    expect([universeStart(1), universeStart(2), universeStart(8)]).toEqual([1, 513, 3585]);
  });

  it('a channel is numbered 1 to 512 in its universe and says the universe', () => {
    expect(channelLabel(112)).toBe('112 (U1)');
    expect(channelLabel(625)).toBe('113 (U2)');
    expect(channelLabel(1024)).toBe('512 (U2)');
    expect(channelNumber(1025)).toBe(1);
    expect(channelNumber(4096)).toBe(512);
  });

  it('channels one after the other say their universe once', () => {
    expect(channelSpan(625, 628)).toBe('113 to 116 (U2)');
    expect(channelSpan(7, 7)).toBe('7 (U1)');
  });

  it('a field shows the universe only where a number alone would not say it', () => {
    expect(channelField(625, 2)).toBe('113 U2');
    expect(channelField(113, 1)).toBe('113');
  });

  it('a typed channel is a number in a universe, or a number in the universe of the field', () => {
    const typed = ['113 U2', '113U2', '113 (U2)', '113 (u2)', 'U2 113', 'u2.113', 'U2:113', 'U2/113', 'U2-113'];
    expect(typed.map((text) => channelFromText(text, 1024))).toEqual(typed.map(() => 625));
    expect(channelFromText(' 113 ', 1024, 2)).toBe(625);
    expect(channelFromText('113', 1024)).toBe(113);
    expect(channelFromText('1 U1', 512)).toBe(1);
  });

  it('a text that names no channel of the project is refused', () => {
    const typed = ['', 'x', '0', '513', '625', '513 U1', '0 U2', '1 U9', '1 U3', '1.5', '-3', 'U2'];
    expect(typed.map((text) => channelFromText(text, 1024))).toEqual(typed.map(() => null));
    expect(channelFromText('1', 1024, 3)).toBeNull();
  });
});
