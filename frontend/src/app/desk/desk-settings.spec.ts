import {
  DEFAULT_SETTINGS,
  DEFAULT_SHORTCUTS,
  keyCount,
  parseSettings,
  settingsPart,
  zoomOf,
} from './desk-settings';

describe('the settings this browser keeps', () => {
  it('are the defaults when there are none, or when they cannot be read', () => {
    expect(parseSettings(null)).toEqual(DEFAULT_SETTINGS);
    expect(parseSettings('not json')).toEqual(DEFAULT_SETTINGS);
    expect(parseSettings('[1, 2]').channelSize).toBe('auto');
    expect(DEFAULT_SETTINGS).toEqual({
      channelSize: 'auto',
      channelRows: 2,
      memoryRows: 1,
      wheelSize: 7,
      channelKeys: 10,
      memoryKeys: 10,
      effectKeys: 0,
      soundKeys: 0,
      effectRows: 0,
      soundRows: 0,
      zoom: 1,
      shortcuts: DEFAULT_SHORTCUTS,
      colors: {},
      hiddenViews: ['notes'], // Notes is shown with a tick in Settings, for a show whose notes are written there
      viewOrder: ['main', 'memories', 'sequences', 'stack', 'stage', 'fixtures', 'effects', 'sounds', 'notes', 'midi', 'settings'],
      stagePanel: true,
    });
    // a device that chose its views keeps them, Notes among them; one that never did has those of the start
    expect(parseSettings(JSON.stringify({ hiddenViews: [] })).hiddenViews).toEqual([]);
    expect(parseSettings(JSON.stringify({ zoom: 1.2 })).hiddenViews).toEqual(['notes']);
  });

  it('have the usual shortcuts of other programs, unless their owner said otherwise', () => {
    // the space bar plays the stack, as in QLab; Ctrl + Z and Ctrl + Y undo and redo, as everywhere
    expect(DEFAULT_SHORTCUTS).toEqual({
      'stack.play': 'Space',
      'stack.pause': 'KeyP',
      'stack.next': 'KeyN',
      'stack.previous': 'Backspace',
      'stack.live': 'KeyS',
      'master.blackout': 'KeyB',
      'master.tempo': 'KeyT',
      'sound.mute': 'KeyM',
      'sound.listen': 'KeyL',
      'project.save': 'Ctrl+KeyS',
      'project.load': 'Ctrl+KeyO',
      'project.undo': 'Ctrl+KeyZ',
      'project.redo': 'Ctrl+KeyY',
    });
    // taken away: stays away; given to another key: that key keeps it
    expect(parseSettings(JSON.stringify({ shortcuts: { 'project.undo': '' } })).shortcuts).toEqual({
      ...DEFAULT_SHORTCUTS,
      'project.undo': '',
    });
    const { 'project.undo': undo, ...others } = DEFAULT_SHORTCUTS;
    expect(undo).toBe('Ctrl+KeyZ');
    expect(
      parseSettings(JSON.stringify({ shortcuts: { 'master.clear': 'Ctrl+KeyZ' } })).shortcuts,
    ).toEqual({
      ...others,
      'master.clear': 'Ctrl+KeyZ',
    });
  });

  it('go into a file of Export as the settings of the Settings view, the shortcuts apart', () => {
    expect(settingsPart(DEFAULT_SETTINGS)).not.toHaveProperty('shortcuts');
    expect(settingsPart({ ...DEFAULT_SETTINGS, zoom: 1.2 }).zoom).toBe(1.2);
    expect(settingsPart({ ...DEFAULT_SETTINGS, channelSize: 8 }).channelSize).toBe(8);
    expect(settingsPart({ ...DEFAULT_SETTINGS, colors: { pause: '#ffffff' } }).colors).toEqual({
      pause: '#ffffff',
    });
    expect(settingsPart({ ...DEFAULT_SETTINGS, hiddenViews: ['stage'] }).hiddenViews).toEqual([
      'stage',
    ]);
    expect(settingsPart({ ...DEFAULT_SETTINGS, viewOrder: ['stage'] }).viewOrder).toEqual([
      'stage',
    ]);
  });

  it('leave known views out of the top bar, each once, never Settings', () => {
    const hidden = parseSettings(
      JSON.stringify({ hiddenViews: ['stage', 'settings', 'nothing', 'main', 'stage', 4] }),
    ).hiddenViews;
    expect(hidden).toEqual(['main', 'stage']); // in the usual order
    expect(parseSettings(JSON.stringify({ hiddenViews: 'stage' })).hiddenViews).toEqual([]);
  });

  it('put every view in the top bar once: the known ones in their order, the others after the view they follow', () => {
    const order = parseSettings(
      JSON.stringify({ viewOrder: ['notes', 'stage', 'nothing', 'notes', 3, 'main'] }),
    ).viewOrder;
    expect(order).toEqual([
      'notes',
      'midi',
      'settings',
      'stage',
      'fixtures',
      'effects',
      'sounds',
      'main',
      'memories',
      'sequences',
      'stack',
    ]);
    expect(parseSettings(JSON.stringify({ viewOrder: 'stage' })).viewOrder).toEqual(
      DEFAULT_SETTINGS.viewOrder,
    );

    // An order kept before there were notes, effects, sounds and MIDI: Effects comes after Fixtures,
    // Sounds after Effects, Notes after Sounds and MIDI after Notes, where they usually are. One kept while Keyboard was a view
    // (until 2026-11-24, now a panel of Settings) is read without it.
    const usual = [
      'main',
      'memories',
      'sequences',
      'stack',
      'stage',
      'fixtures',
      'effects',
      'sounds',
      'notes',
      'midi',
      'settings',
    ];
    const before = [
      'main',
      'memories',
      'sequences',
      'stack',
      'stage',
      'fixtures',
      'keyboard',
      'settings',
    ];
    expect(parseSettings(JSON.stringify({ viewOrder: before })).viewOrder).toEqual(usual);

    // The usual order of the day Effects stood after Sequences was never chosen: it is the usual order of now.
    const thatDay = [
      'main',
      'memories',
      'sequences',
      'effects',
      'stack',
      'stage',
      'fixtures',
      'notes',
      'settings',
    ];
    expect(parseSettings(JSON.stringify({ viewOrder: thatDay })).viewOrder).toEqual(usual);
    expect(
      parseSettings(JSON.stringify({ hiddenViews: ['keyboard', 'stage'] })).hiddenViews,
    ).toEqual(['stage']);
  });

  it('read the colors that were kept before they were called so', () => {
    // settings kept before 2026-11-21 name them in the British spelling
    expect(parseSettings(JSON.stringify({ colours: { pause: '#FFAA00' } })).colors).toEqual({
      pause: '#ffaa00',
    });
    expect(
      parseSettings(JSON.stringify({ colors: { play: '#000000' }, colours: { pause: '#ffaa00' } }))
        .colors,
    ).toEqual({
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
        effectKeys: 8,
        soundKeys: 40, // there are 32
        effectRows: 2, // none or one
        soundRows: 3, // none or one
        zoom: 1.24,
        // the size of the lettering, before there was the zoom: passed over
        fontScale: 1.5,
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
      wheelSize: 7,
      channelKeys: 32,
      memoryKeys: 10,
      effectKeys: 8,
      soundKeys: 0,
      effectRows: 0,
      soundRows: 0,
      zoom: 1.2,
      // Play's 5 is no shortcut: Play keeps its usual one; Next was given none, and keeps none
      shortcuts: { ...DEFAULT_SHORTCUTS, 'master.clear': 'Ctrl+KeyC', 'stack.next': '' },
      colors: { pause: '#ffaa00', channel: '#00ff00' },
      hiddenViews: ['notes'], // never chosen: those of the start
      viewOrder: DEFAULT_SETTINGS.viewOrder,
      stagePanel: true,
    });
    expect(parseSettings(JSON.stringify({ channelSize: 12 })).channelSize).toBe('auto');
    expect(parseSettings(JSON.stringify({ stagePanel: false })).stagePanel).toBe(false);
    expect(parseSettings(JSON.stringify({ stagePanel: 'no' })).stagePanel).toBe(true);
    expect(parseSettings(JSON.stringify({ wheelSize: 9 })).wheelSize).toBe(9);
  });

  it('keep the zoom between 50 and 200 per cent, in steps of 10', () => {
    expect(zoomOf(1.1)).toBe(1.1);
    expect(zoomOf(1.0 + 0.1 + 0.1)).toBe(1.2); // not 1.2000000000000002
    expect(zoomOf(3)).toBe(2);
    expect(zoomOf(0.2)).toBe(0.5);
    expect(zoomOf('big')).toBe(1);
  });

  it('show 0 to 32 keys of each kind of the independents', () => {
    expect(keyCount(1, 32)).toBe(1);
    expect(keyCount(0, 32)).toBe(0);
    expect(keyCount(-1, 32)).toBe(10);
    expect(keyCount(2.5, 32)).toBe(10);
  });
});
