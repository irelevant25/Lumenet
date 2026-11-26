import { EMPTY_STAGE, StageLight } from '../desk/desk-types';
import { LibraryEntry, OflFixture, fixtureOf, libraryFixture, libraryIndex, searchLibrary, withLibraryType } from './fixture-library';
import { qlcFixtureOf } from './qlc-library';

const FILES = 'https://raw.githubusercontent.com/OpenLightingProject/open-fixture-library/master/fixtures';
const TREE = 'https://api.github.com/repos/OpenLightingProject/open-fixture-library/git/trees/master:fixtures?recursive=1';
const QLC = 'https://raw.githubusercontent.com/mcallegari/qlcplus/master/resources/fixtures';

/** The list of the QLC+ library, as FixturesMap.xml has it: makers as their folders. A file name with a space is none of the library's. */
const QLC_MAP = `<?xml version='1.0' encoding='UTF-8'?>
<!DOCTYPE FixturesMap>
<FixturesMap xmlns="http://www.qlcplus.org/FixturesMap">
  <M n="American_DJ">
    <F n="American-DJ-Dotz-Bar-1.4" m="Dotz Bar 1.4"/>
  </M>
  <M n="Cameo">
    <F n="Cameo-CLPFLATPRO-Series" m="CLPFLATPRO Series"/>
    <F n="Cameo Bad" m="Bad"/>
  </M>
</FixturesMap>`;

/** A bar in the newer format of QLC+: every channel says what it is (Preset). */
const BAR_QXF = `<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE FixtureDefinition>
<FixtureDefinition xmlns="http://www.qlcplus.org/FixtureDefinition">
 <Creator><Name>Q Light Controller Plus</Name></Creator>
 <Manufacturer>American DJ</Manufacturer>
 <Model>Dotz Bar 1.4</Model>
 <Type>LED Bar (Beams)</Type>
 <Channel Name="Red 1" Preset="IntensityRed"/>
 <Channel Name="Green 1" Preset="IntensityGreen"/>
 <Channel Name="Blue 1" Preset="IntensityBlue"/>
 <Channel Name="Red 2" Preset="IntensityRed"/>
 <Channel Name="Green 2" Preset="IntensityGreen"/>
 <Channel Name="Blue 2" Preset="IntensityBlue"/>
 <Channel Name="Dimmer" Preset="IntensityMasterDimmer"/>
 <Channel Name="Dimmer fine" Preset="IntensityMasterDimmerFine"/>
 <Channel Name="Strobe">
  <Group Byte="0">Shutter</Group>
  <Capability Min="0" Max="15" Preset="ShutterOpen">LED on</Capability>
  <Capability Min="16" Max="255" Preset="StrobeSlowToFast">Strobe slow to fast</Capability>
 </Channel>
 <Channel Name="Macros">
  <Group Byte="0">Colour</Group>
  <Capability Min="0" Max="127" Preset="ColorMacro" Res1="#FF0000">Red</Capability>
  <Capability Min="128" Max="255" Preset="ColorDoubleMacro" Res1="#ff0000" Res2="#0000ff">Red and blue</Capability>
 </Channel>
 <Mode Name="3 Channel">
  <Channel Number="0">Red 1</Channel>
  <Channel Number="1">Green 1</Channel>
  <Channel Number="2">Blue 1</Channel>
 </Mode>
 <Mode Name="10 Channel">
  <Channel Number="0">Dimmer</Channel>
  <Channel Number="1">Dimmer fine</Channel>
  <Channel Number="3">Red 1</Channel>
  <Channel Number="2">Strobe</Channel>
  <Channel Number="4">Green 1</Channel>
  <Channel Number="5">Blue 1</Channel>
  <Channel Number="6">Red 2</Channel>
  <Channel Number="7">Green 2</Channel>
  <Channel Number="8">Blue 2</Channel>
  <Channel Number="9">Macros</Channel>
  <Head><Channel>3</Channel><Channel>4</Channel><Channel>5</Channel></Head>
 </Mode>
 <Physical><Bulb Type="LED"/></Physical>
</FixtureDefinition>`;

/** A moving head in the older format of QLC+: groups, a byte for the fine part, Colour and Color. */
const HEAD_QXF = `<?xml version="1.0" encoding="UTF-8"?>
<FixtureDefinition xmlns="http://qlcplus.sourceforge.net/FixtureDefinition">
 <Manufacturer>Maker</Manufacturer>
 <Model>Old Head</Model>
 <Type>Moving Head</Type>
 <Channel Name="Pan"><Group Byte="0">Pan</Group></Channel>
 <Channel Name="Pan fine"><Group Byte="1">Pan</Group></Channel>
 <Channel Name="Tilt"><Group Byte="0">Tilt</Group></Channel>
 <Channel Name="Colour wheel">
  <Group Byte="0">Colour</Group>
  <Capability Min="0" Max="9" Color="#ffffff">White</Capability>
  <Capability Min="10" Max="19" Color="#FF0000">Red</Capability>
  <Capability Min="20" Max="29" Color="#ff0000" Color2="#0000ff">Red / blue</Capability>
  <Capability Min="30" Max="255">Rotation</Capability>
 </Channel>
 <Channel Name="Shutter">
  <Group Byte="0">Shutter</Group>
  <Capability Min="0" Max="7">Closed</Capability>
  <Capability Min="8" Max="15">Shutter open</Capability>
  <Capability Min="16" Max="255">Strobe</Capability>
 </Channel>
 <Channel Name="Dimmer"><Group Byte="0">Intensity</Group></Channel>
 <Channel Name="Red"><Group Byte="0">Intensity</Group><Colour>Red</Colour></Channel>
 <Channel Name="Cyan"><Group Byte="0">Intensity</Group><Colour>Cyan</Colour></Channel>
 <Mode Name="Standard">
  <Channel Number="0">Pan</Channel>
  <Channel Number="1">Pan fine</Channel>
  <Channel Number="2">Tilt</Channel>
  <Channel Number="3">Colour wheel</Channel>
  <Channel Number="4">Shutter</Channel>
  <Channel Number="5">Dimmer</Channel>
  <Channel Number="6">Red</Channel>
  <Channel Number="7">Cyan</Channel>
  <Channel Number="8">Not defined</Channel>
 </Mode>
</FixtureDefinition>`;

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

/** A pretend fetch: answers each address from the table (JSON, or a text as it is), and remembers what was asked. */
function fetcherOf(answers: Record<string, unknown>, asked: string[] = []): typeof fetch {
  return (async (url: string) => {
    asked.push(url);
    const body = answers[url];
    return {
      ok: body !== undefined,
      status: body === undefined ? 404 : 200,
      json: async () => body,
      text: async () => (typeof body === 'string' ? body : JSON.stringify(body)),
    } as Response;
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

  it('names a strobe with how fast it goes and whether at random, as a bar of ADJ has them', () => {
    const strobe = (from: number, to: number, shutterEffect: string, more: Partial<{ randomTiming: boolean; speedStart: string; speedEnd: string }> = {}) => ({
      dmxRange: [from, to] as [number, number],
      type: 'ShutterStrobe',
      shutterEffect,
      ...more,
    });
    const bar = fixtureOf(
      {
        name: 'Hex Bar',
        availableChannels: {
          Strobe: {
            capabilities: [
              strobe(0, 31, 'Closed'),
              strobe(32, 63, 'Open'),
              strobe(64, 95, 'Strobe', { speedStart: 'slow', speedEnd: 'fast' }),
              strobe(96, 127, 'Open'),
              strobe(128, 159, 'Pulse', { speedStart: 'slow', speedEnd: 'fast' }),
              strobe(160, 191, 'RampUp'),
              strobe(192, 223, 'Strobe', { randomTiming: true }),
              strobe(224, 255, 'Open'),
            ],
          },
        },
        modes: [{ name: '1-channel', channels: ['Strobe'] }],
      },
      'maker/hex-bar',
      'Maker',
    );
    expect(bar.modes[0].channels[0].ranges?.map((range) => range.label)).toEqual([
      'Closed',
      'Open',
      'Strobe, slow to fast',
      'Open',
      'Pulse, slow to fast',
      'Ramp up',
      'Random strobe',
      'Open',
    ]);
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

  it('finds lights of both libraries by every word of the search, and the browser keeps the lists for a week', async () => {
    localStorage.clear();
    localStorage.setItem('lumenet-fixture-library', '{"fetched":1,"entries":[]}'); // what an earlier page kept
    const asked: string[] = [];
    const fetcher = fetcherOf(
      {
        [`${FILES}/manufacturers.json`]: { 'american-dj': { name: 'American DJ' }, cameo: { name: 'Cameo' }, etc: { name: 'ETC' } },
        [TREE]: {
          tree: [
            { path: 'cameo/flat-pro-18.json', type: 'blob' },
            { path: 'american-dj/inno-pocket-spot.json', type: 'blob' },
            { path: 'etc/fos4PD16.json', type: 'blob' }, // a few files have capitals
            { path: 'manufacturers.json', type: 'blob' },
            { path: 'cameo', type: 'tree' },
          ],
        },
        [`${QLC}/FixturesMap.xml`]: QLC_MAP,
      },
      asked,
    );

    const { entries, problems } = await libraryIndex(fetcher, 1000);
    expect(problems).toEqual([]);
    expect(entries).toEqual([
      { library: 'qlcplus', source: 'qlcplus:American_DJ/American-DJ-Dotz-Bar-1.4.qxf', maker: 'American DJ', model: 'Dotz Bar 1.4' },
      { library: 'ofl', source: 'american-dj/inno-pocket-spot', maker: 'American DJ', model: 'Inno Pocket Spot' },
      { library: 'qlcplus', source: 'qlcplus:Cameo/Cameo-CLPFLATPRO-Series.qxf', maker: 'Cameo', model: 'CLPFLATPRO Series' },
      { library: 'ofl', source: 'cameo/flat-pro-18', maker: 'Cameo', model: 'Flat Pro 18' },
      { library: 'ofl', source: 'etc/fos4PD16', maker: 'ETC', model: 'Fos4PD16' },
    ]);
    const models = (found: LibraryEntry[]) => found.map((entry) => entry.model);
    expect(models(searchLibrary(entries, 'american SPOT'))).toEqual(['Inno Pocket Spot']);
    expect(models(searchLibrary(entries, 'flat 18'))).toEqual(['Flat Pro 18']);
    expect(searchLibrary(entries, 'cameo spot')).toEqual([]);

    // ADJ is American DJ; a word may leave out the spaces and dashes of a name
    expect(models(searchLibrary(entries, 'ADJ dotz bar 1.4'))).toEqual(['Dotz Bar 1.4']);
    expect(models(searchLibrary(entries, 'cameo flatpro'))).toEqual(['CLPFLATPRO Series', 'Flat Pro 18']);
    expect(models(searchLibrary(entries, 'flat-pro 18'))).toEqual(['Flat Pro 18']);

    const day = 24 * 60 * 60 * 1000;
    await libraryIndex(fetcher, 1000 + 6 * day);
    expect(asked).toHaveLength(3);
    await libraryIndex(fetcher, 1000 + 8 * day);
    expect(asked).toHaveLength(6);
    expect(localStorage.getItem('lumenet-fixture-library')).toBeNull();
  });

  it('shows the lights of one library when the other cannot be reached, and says so', async () => {
    localStorage.clear();
    const { entries, problems } = await libraryIndex(fetcherOf({ [`${QLC}/FixturesMap.xml`]: QLC_MAP }), 1000);
    expect(entries.map((entry) => entry.library)).toEqual(['qlcplus', 'qlcplus']);
    expect(problems).toEqual(['The Open Fixture Library answered 404.']);

    localStorage.clear();
    await expect(libraryIndex(fetcherOf({}), 1000)).rejects.toThrow('The Open Fixture Library answered 404. The library of QLC+ answered 404.');
    localStorage.clear();
  });

  it('makes a light of QLC+ into channels: letters, fine channels, the order of the mode, colors and an open shutter', () => {
    const bar = qlcFixtureOf(BAR_QXF, 'qlcplus:American_DJ/American-DJ-Dotz-Bar-1.4.qxf');
    expect(bar).toMatchObject({ library: 'qlcplus', maker: 'American DJ', model: 'Dotz Bar 1.4', categories: ['LED Bar (Beams)'] });
    expect(bar.modes.map((mode) => [mode.name, mode.channels.map((channel) => channel.code).join('')])).toEqual([
      ['3 Channel', 'RGB'],
      ['10 Channel', 'DFSRGBRGBX'],
    ]);
    const full = bar.modes[1].channels;
    expect(full[1]).toEqual({ name: 'Dimmer fine', code: 'F', fine: 0, ranges: [] });
    expect(full[2].ranges).toEqual([
      { from: 0, to: 15, label: 'Open (LED on)', color: null },
      { from: 16, to: 255, label: 'Strobe slow to fast', color: null },
    ]);
    expect(full[9].ranges).toEqual([
      { from: 0, to: 127, label: 'Red', color: '#ff0000' },
      { from: 128, to: 255, label: 'Red and blue', color: null },
    ]);

    const head = qlcFixtureOf(HEAD_QXF, 'qlcplus:Maker/Maker-Old-Head.qxf');
    const channels = head.modes[0].channels;
    expect(channels.map((channel) => channel.code).join('')).toBe('PFTXSDRXX');
    expect(channels[1].fine).toBe(0);
    expect(channels[3].ranges).toEqual([
      { from: 0, to: 9, label: 'White', color: '#ffffff' },
      { from: 10, to: 19, label: 'Red', color: '#ff0000' },
      { from: 20, to: 29, label: 'Red / blue', color: null },
      { from: 30, to: 255, label: 'Rotation', color: null },
    ]);
    expect(channels[4].ranges?.map((range) => range.label)).toEqual(['Closed', 'Open', 'Strobe']);
    expect(channels[8]).toEqual({ name: 'Not defined', code: 'X', fine: null, ranges: [] });

    expect(() => qlcFixtureOf('<FixtureDefinition><Model>', 'qlcplus:Maker/Broken.qxf')).toThrow('The file of the light cannot be read.');
  });

  it('reads a light of QLC+ from its file, and nothing else', async () => {
    const asked: string[] = [];
    const fetcher = fetcherOf({ [`${QLC}/American_DJ/American-DJ-Dotz-Bar-1.4.qxf`]: BAR_QXF, [`${QLC}/Cameo/Cameo-Studio-PAR-64-RGBWA%2BUV.qxf`]: BAR_QXF }, asked);
    const bar = await libraryFixture('qlcplus:American_DJ/American-DJ-Dotz-Bar-1.4.qxf', 'American DJ', fetcher);
    expect(bar.source).toBe('qlcplus:American_DJ/American-DJ-Dotz-Bar-1.4.qxf');
    expect(bar.modes).toHaveLength(2);
    await libraryFixture('qlcplus:Cameo/Cameo-Studio-PAR-64-RGBWA+UV.qxf', 'Cameo', fetcher); // a sign of the name, written for the address
    expect(asked.at(-1)).toBe(`${QLC}/Cameo/Cameo-Studio-PAR-64-RGBWA%2BUV.qxf`);
    await expect(libraryFixture('qlcplus:../secret.qxf', 'Maker', fetcher)).rejects.toThrow('"qlcplus:../secret.qxf" is no light of the library.');
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
      'The Open Fixture Library could not be reached. Is this computer on the internet?',
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
