import { keyColumns, lowestKey } from './key-columns';

describe('how a group of keys stands', () => {
  it('stays in one column as long as the keys fit, even flatter ones', () => {
    // 8 page keys in a column 600 pixels high: room for 25 keys of 20 pixels and their gaps
    expect(keyColumns(8, 600)).toEqual({ columns: 1, rows: 8 });
    expect(keyColumns(25, 600)).toEqual({ columns: 1, rows: 25 });
  });

  it('goes on in as few further columns as needed instead of scrolling', () => {
    expect(keyColumns(26, 600)).toEqual({ columns: 2, rows: 13 });
    // 64 channel pages (8 channels on a page) in 600 pixels: three columns of 22
    expect(keyColumns(64, 600)).toEqual({ columns: 3, rows: 22 });
    // a column too low for even one key still shows them all, one key high
    expect(keyColumns(5, 10)).toEqual({ columns: 5, rows: 1 });
  });

  it('stands in one column before the height is known', () => {
    expect(keyColumns(16, 0)).toEqual({ columns: 1, rows: 16 });
    expect(keyColumns(0, 600)).toEqual({ columns: 1, rows: 1 });
  });

  it('keeps keys with bigger lettering higher, and goes on in another column sooner', () => {
    expect(lowestKey(10, 2, 1)).toBe(20); // usual lettering: 20 pixels, a range stays on one line
    expect(lowestKey(15, 2, 1.5)).toBe(34); // x1.5: room for a range broken in two lines
    expect(lowestKey(15, 1, 1.5)).toBe(20); // one line of it fits 20 pixels anyway
    expect(lowestKey(21, 1, 1.5)).toBe(25);
    // the 64 channel pages of before, in 600 pixels: five columns of 13 instead of three of 22
    expect(keyColumns(64, 600, 34)).toEqual({ columns: 5, rows: 13 });
  });
});
