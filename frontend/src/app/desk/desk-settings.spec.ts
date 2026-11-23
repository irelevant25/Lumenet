import { DEFAULT_SETTINGS, fontScale, keyCount, parseSettings, settingsPart } from './desk-settings';

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
      shortcuts: { 'project.undo': 'Ctrl+KeyZ', 'project.redo': 'Ctrl+KeyY' },
      colors: {},
      hiddenViews: [],
      viewOrder: ['main', 'memories', 'sequences', 'stack', 'stage', 'fixtures', 'notes', 'settings', 'keyboard'],
    });
  });

  it('have undo and redo on Ctrl + Z and Ctrl + Y, unless their owner said otherwise', () => {
    // taken away: stays away; given to another key: that key keeps it
    expect(parseSettings(JSON.stringify({ shortcuts: { 'project.undo': '' } })).shortcuts).toEqual({
      'project.undo': '',
      'project.redo': 'Ctrl+KeyY',
    });
    expect(parseSettings(JSON.stringify({ shortcuts: { 'master.clear': 'Ctrl+KeyZ' } })).shortcuts).toEqual({
      'master.clear': 'Ctrl+KeyZ',
      'project.redo': 'Ctrl+KeyY',
    });
  });

  it('go into a file of Export as the settings of the Settings view, the shortcuts apart', () => {
    expect(settingsPart(DEFAULT_SETTINGS)).not.toHaveProperty('shortcuts');
    expect(settingsPart({ ...DEFAULT_SETTINGS, fontScale: 1.2 }).fontScale).toBe(1.2);
    expect(settingsPart({ ...DEFAULT_SETTINGS, channelSize: 8 }).channelSize).toBe(8);
    expect(settingsPart({ ...DEFAULT_SETTINGS, colors: { pause: '#ffffff' } }).colors).toEqual({ pause: '#ffffff' });
    expect(settingsPart({ ...DEFAULT_SETTINGS, hiddenViews: ['stage'] }).hiddenViews).toEqual(['stage']);
    expect(settingsPart({ ...DEFAULT_SETTINGS, viewOrder: ['stage'] }).viewOrder).toEqual(['stage']);
  });

  it('leave known views out of the top bar, each once, never Settings', () => {
    const hidden = parseSettings(JSON.stringify({ hiddenViews: ['stage', 'settings', 'nothing', 'main', 'stage', 4] })).hiddenViews;
    expect(hidden).toEqual(['main', 'stage']); // in the usual order
    expect(parseSettings(JSON.stringify({ hiddenViews: 'stage' })).hiddenViews).toEqual([]);
  });

  it('put every view in the top bar once: the known ones in their order, the others after the view they follow', () => {
    const order = parseSettings(JSON.stringify({ viewOrder: ['keyboard', 'stage', 'nothing', 'keyboard', 3, 'main'] })).viewOrder;
    expect(order).toEqual(['keyboard', 'stage', 'fixtures', 'notes', 'settings', 'main', 'memories', 'sequences', 'stack']);
    expect(parseSettings(JSON.stringify({ viewOrder: 'stage' })).viewOrder).toEqual(DEFAULT_SETTINGS.viewOrder);

    // An order kept before there were notes: Notes comes after Fixtures, where it usually is.
    const before = ['main', 'memories', 'sequences', 'stack', 'stage', 'fixtures', 'keyboard', 'settings'];
    expect(parseSettings(JSON.stringify({ viewOrder: before })).viewOrder).toEqual([
      'main',
      'memories',
      'sequences',
      'stack',
      'stage',
      'fixtures',
      'notes',
      'keyboard',
      'settings',
    ]);
  });

  it('read the colors that were kept before they were called so', () => {
    // settings kept before 2026-11-21 name them in the British spelling
    expect(parseSettings(JSON.stringify({ colours: { pause: '#FFAA00' } })).colors).toEqual({ pause: '#ffaa00' });
    expect(parseSettings(JSON.stringify({ colors: { play: '#000000' }, colours: { pause: '#ffaa00' } })).colors).toEqual({
      play: '#000000',
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
        // a known key and a color as #rrggbb, kept in small letters
        colors: { pause: '#FFAA00', play: 'green', nothing: '#000000', channel: '#00ff00' },
        // a setting there was for a day: the channels are always numbered 1 to 512 in their universe
        addresses: 'continuous',
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
      shortcuts: { 'master.clear': 'Ctrl+KeyC', 'project.undo': 'Ctrl+KeyZ', 'project.redo': 'Ctrl+KeyY' },
      colors: { pause: '#ffaa00', channel: '#00ff00' },
      hiddenViews: [],
      viewOrder: DEFAULT_SETTINGS.viewOrder,
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
