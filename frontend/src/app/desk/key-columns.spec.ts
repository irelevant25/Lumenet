import { keyColumns } from './key-columns';

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

  it('can keep the first key alone in its row over the columns (Edit of the independents)', () => {
    // 26 keys in 600 pixels: two columns; Edit in a row of its own, the 25 others in 13 rows under it
    expect(keyColumns(26, 600, 20, true)).toEqual({ columns: 2, rows: 14 });
    // in one column it is alone in its row anyway
    expect(keyColumns(8, 600, 20, true)).toEqual({ columns: 1, rows: 8 });
  });

  it('can keep keys higher, and then goes on in another column sooner', () => {
    // the 64 channel pages of before, keys at least 34 pixels high: five columns of 13
    expect(keyColumns(64, 600, 34)).toEqual({ columns: 5, rows: 13 });
  });
});
