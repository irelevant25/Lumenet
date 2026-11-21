import { EMPTY_STAGE, StageLight } from './desk-types';
import { OflFixture, fixtureOf, libraryFixture, libraryIndex, searchLibrary, withLibraryType } from './fixture-library';

const FILES = 'https://raw.githubusercontent.com/OpenLightingProject/open-fixture-library/master/fixtures';
const TREE = 'https://api.github.com/repos/OpenLightingProject/open-fixture-library/git/trees/master:fixtures?recursive=1';

/** A moving head as the library describes one, made up for the tests. */
const SPOT_FILE: OflFixture = {
  name: 'Pocket Spot',
  categories: ['Moving Head'],
  wheels: {
    'Color Wheel': {
      slots: [{ type: 'Open' }, { type: 'Color', name: 'Red', colors: ['#FF0000'] }, { type: 'Color', name: 'Blue', colors: ['#0000ff'] }],
    },
  },
  availableChannels: {
    Pan: { fineChannelAliases: ['Pan fine'], capability: { type: 'Pan' } },
    Tilt: { capability: { type: 'Tilt' } },
    'Color Wheel': {
      capabilities: [
        { dmxRange: [0, 9], type: 'WheelSlot', slotNumber: 1 },
        { dmxRange: [10, 19], type: 'WheelSlot', slotNumber: 2 },
        { dmxRange: [20, 29], type: 'WheelSlot', slotNumber: 2.5 },
        { dmxRange: [30, 255], type: 'WheelRotation' },
      ],
    },
    Shutter: {
      capabilities: [
        { dmxRange: [0, 7], type: 'ShutterStrobe', shutterEffect: 'Closed' },
        { dmxRange: [8, 255], type: 'ShutterStrobe', shutterEffect: 'Open' },
      ],
    },
    Dimmer: { fineChannelAliases: ['Dimmer fine'], capability: { type: 'Intensity' } },
    Zoom: {
      fineChannelAliases: ['Zoom fine'],
      capabilities: [
        { dmxRange: [0, 32767], type: 'Zoom' },
        { dmxRange: [32768, 65535], type: 'Zoom', comment: 'wide' },
      ],
    },
  },
  modes: [
    { name: '5-channel', channels: ['Pan', 'Tilt', 'Color Wheel', 'Shutter', 'Dimmer'] },
    { name: '8-channel', channels: ['Pan', 'Pan fine', 'Tilt', 'Color Wheel', 'Shutter', 'Dimmer', 'Dimmer fine', null] },
    { name: 'Zoom', channels: ['Zoom', 'Zoom fine'] },
  ],
};

/** A bar of three pixels as the library describes one. */
const BAR_FILE: OflFixture = {
  name: 'Pixel Bar',
  matrix: { pixelCount: [3, 1, 1] },
  availableChannels: { Dimmer: { capability: { type: 'Intensity' } } },
  templateChannels: {
    'Red $pixelKey': { capability: { type: 'ColorIntensity', color: 'Red' } },
    'Green $pixelKey': { capability: { type: 'ColorIntensity', color: 'Green' } },
    'Blue $pixelKey': { capability: { type: 'ColorIntensity', color: 'Blue' } },
  },
  modes: [
    {
      name: '10-channel',
      channels: [
        'Dimmer',
        { insert: 'matrixChannels', repeatFor: 'eachPixelXYZ', channelOrder: 'perPixel', templateChannels: ['Red $pixelKey', 'Green $pixelKey', 'Blue $pixelKey'] },
      ],
    },
    {
      name: 'Two pixels',
      channels: [{ insert: 'matrixChannels', repeatFor: ['3', '1'], channelOrder: 'perChannel', templateChannels: ['Red $pixelKey', 'Green $pixelKey'] }],
    },
  ],
};

/** A pretend fetch: answers each address from the table, and remembers what was asked. */
function fetcherOf(answers: Record<string, unknown>, asked: string[] = []): typeof fetch {
  return (async (url: string) => {
    asked.push(url);
    const body = answers[url];
    return { ok: body !== undefined, status: body === undefined ? 404 : 200, json: async () => body } as Response;
  }) as typeof fetch;
}

describe('the fixture library', () => {
  it('makes the modes of a moving head into channels: letters, fine channels, wheel slots and a shutter', () => {
    const spot = fixtureOf(SPOT_FILE, 'maker/pocket-spot', 'Maker');
    expect(spot.model).toBe('Pocket Spot');
    expect(spot.modes.map((mode) => mode.channels.map((channel) => channel.code).join(''))).toEqual(['PTXSD', 'PFTXSDFX', 'XF']);

    const full = spot.modes[1].channels;
    expect(full[1]).toEqual({ name: 'Pan fine', code: 'F', fine: 0, ranges: [] });
    expect(full[6].fine).toBe(5); // the fine part of the dimmer
    expect(full[7].name).toBe('Unused');
    expect(full[3].ranges).toEqual([
      { from: 0, to: 9, label: 'Open', color: null },
      { from: 10, to: 19, label: 'Red', color: '#ff0000' },
      { from: 20, to: 29, label: 'Red / Blue', color: null },
      { from: 30, to: 255, label: 'Rotation', color: null },
    ]);
    expect(full[4].ranges?.map((range) => range.label)).toEqual(['Closed', 'Open']);
  });

  it('reads the values of a 16-bit channel as those of its coarse channel', () => {
    const zoom = fixtureOf(SPOT_FILE, 'maker/pocket-spot', 'Maker').modes[2].channels[0];
    expect(zoom.ranges?.map((range) => [range.from, range.to, range.label])).toEqual([
      [0, 127, 'Zoom'],
      [128, 255, 'Zoom (wide)'],
    ]);
  });

  it('makes the pixels of a bar into cells, in the order the mode gives them', () => {
    const bar = fixtureOf(BAR_FILE, 'maker/pixel-bar', 'Maker');
    expect(bar.modes[0].channels.map((channel) => channel.code).join('')).toBe('DRGBRGBRGB');
    expect(bar.modes[0].channels.slice(1, 5).map((channel) => channel.name)).toEqual(['Red 1', 'Green 1', 'Blue 1', 'Red 2']);
    expect(bar.modes[1].channels.map((channel) => channel.name)).toEqual(['Red 3', 'Red 1', 'Green 3', 'Green 1']);
  });

  it('finds lights by every word of the search, and the browser keeps the list for a week', async () => {
    localStorage.clear();
    const asked: string[] = [];
    const fetcher = fetcherOf(
      {
        [`${FILES}/manufacturers.json`]: { 'american-dj': { name: 'American DJ' }, cameo: { name: 'Cameo' } },
        [TREE]: {
          tree: [
            { path: 'cameo/flat-pro-18.json', type: 'blob' },
            { path: 'american-dj/inno-pocket-spot.json', type: 'blob' },
            { path: 'manufacturers.json', type: 'blob' },
            { path: 'cameo', type: 'tree' },
          ],
        },
      },
      asked,
    );

    const entries = await libraryIndex(fetcher, 1000);
    expect(entries).toEqual([
      { source: 'american-dj/inno-pocket-spot', maker: 'American DJ', model: 'Inno Pocket Spot' },
      { source: 'cameo/flat-pro-18', maker: 'Cameo', model: 'Flat Pro 18' },
    ]);
    expect(searchLibrary(entries, 'american SPOT').map((entry) => entry.source)).toEqual(['american-dj/inno-pocket-spot']);
    expect(searchLibrary(entries, 'flat 18').map((entry) => entry.model)).toEqual(['Flat Pro 18']);
    expect(searchLibrary(entries, 'cameo spot')).toEqual([]);

    const day = 24 * 60 * 60 * 1000;
    await libraryIndex(fetcher, 1000 + 6 * day);
    expect(asked).toHaveLength(2);
    await libraryIndex(fetcher, 1000 + 8 * day);
    expect(asked).toHaveLength(4);
  });

  it('follows a light that moved, and says when the library cannot be reached', async () => {
    const fetcher = fetcherOf({
      [`${FILES}/maker/old-spot.json`]: { name: 'Old Spot', redirectTo: 'maker/pocket-spot' },
      [`${FILES}/maker/pocket-spot.json`]: SPOT_FILE,
    });
    const found = await libraryFixture('maker/old-spot', 'Maker', fetcher);
    expect(found.source).toBe('maker/pocket-spot');
    expect(found.model).toBe('Pocket Spot');

    const offline = (async () => {
      throw new TypeError('Failed to fetch');
    }) as typeof fetch;
    await expect(libraryFixture('maker/pocket-spot', 'Maker', offline)).rejects.toThrow(
      'The fixture library could not be reached. Is this computer on the internet?',
    );
    await expect(libraryFixture('../secret', 'Maker', fetcher)).rejects.toThrow('"../secret" is no light of the library.');
  });

  it('makes a light of the plan a light of the library, the kind once for all lights of that mode', () => {
    const fixture = fixtureOf(SPOT_FILE, 'maker/pocket-spot', 'Maker');
    const light: StageLight = { id: 1, name: 'Light 9', address: 510, channels: 'D', color: '#ff0000', x: 1, y: 1, angle: 0, shape: 'round', width: 1, depth: 1 };
    const plan = withLibraryType({ ...EMPTY_STAGE, lights: [light, { ...light, id: 2, name: 'Spot left', address: 20 }] }, 1, fixture, fixture.modes[0]);
    expect(plan.types).toHaveLength(1);
    expect(plan.lights[0]).toMatchObject({ type: 1, channels: 'PTXSD', address: 508, name: 'Pocket Spot', color: null });

    const again = withLibraryType(plan, 2, fixture, fixture.modes[0]);
    expect(again.types).toHaveLength(1);
    expect(again.lights[1]).toMatchObject({ type: 1, channels: 'PTXSD', name: 'Spot left' });
  });
});
