import { DEFAULT_SETTINGS, fontScale, keyCount, parseSettings } from './desk-settings';

describe('the settings this browser keeps', () => {
  it('are the defaults when there are none, or when they cannot be read', () => {
    expect(parseSettings(null)).toEqual(DEFAULT_SETTINGS);
    expect(parseSettings('not json')).toEqual(DEFAULT_SETTINGS);
    expect(parseSettings('[1, 2]').channelSize).toBe('auto');
    expect(DEFAULT_SETTINGS).toEqual({
      channelSize: 'auto',
      channelRows: 2,
      memoryRows: 1,
      wheelSize: 3,
      channelKeys: 10,
      memoryKeys: 10,
      fontScale: 1,
      shortcuts: {},
    });
  });

  it('keep what makes sense, and only that', () => {
    const kept = parseSettings(
      JSON.stringify({
        channelSize: 8,
        channelRows: 1,
        memoryRows: 3, // 1 or 2
        wheelSize: 4, // an odd number from 1 to 9
        channelKeys: 32,
        memoryKeys: 33, // there are 32
        fontScale: 1.24,
        shortcuts: { 'master.clear': 'Ctrl+KeyC', 'stack.play': 5, 'stack.next': '' },
      }),
    );
    expect(kept).toEqual({
      channelSize: 8,
      channelRows: 1,
      memoryRows: 1,
      wheelSize: 3,
      channelKeys: 32,
      memoryKeys: 10,
      fontScale: 1.2,
      shortcuts: { 'master.clear': 'Ctrl+KeyC' },
    });
    expect(parseSettings(JSON.stringify({ channelSize: 12 })).channelSize).toBe('auto');
    expect(parseSettings(JSON.stringify({ wheelSize: 9 })).wheelSize).toBe(9);
  });

  it('keep the lettering between 0.7 and 1.5 times, in steps of 0.1', () => {
    expect(fontScale(1.1)).toBe(1.1);
    expect(fontScale(1.0 + 0.1 + 0.1)).toBe(1.2); // not 1.2000000000000002
    expect(fontScale(3)).toBe(1.5);
    expect(fontScale(0.2)).toBe(0.7);
    expect(fontScale('big')).toBe(1);
  });

  it('show 0 to 32 keys of each kind of the independents', () => {
    expect(keyCount(1, 32)).toBe(1);
    expect(keyCount(0, 32)).toBe(0);
    expect(keyCount(-1, 32)).toBe(10);
    expect(keyCount(2.5, 32)).toBe(10);
  });
});
