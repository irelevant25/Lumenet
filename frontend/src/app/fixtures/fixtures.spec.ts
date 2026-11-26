import { UNIVERSE_SIZE, EMPTY_STAGE, FixtureType, StageLight, StagePlan, recordedStep, stepAfter } from '../desk/desk-types';
import {
  applyLimits,
  channelName,
  colorChanges,
  fixtureLabels,
  hexOf,
  hsvOf,
  intensityChanges,
  intensityOf,
  panTiltOf,
  positionChanges,
  positionOf,
  rgbOfHex,
  rgbOfHsv,
  rolesOf,
  shownFlashes,
  shutterClosed,
  strobeOf,
  swatchOf,
} from './fixtures';
import { NOTE_LINE_SEPARATOR } from '../desk/notes';

/** A light at address 1 with these channels. */
function light(channels: string, more: Partial<StageLight> = {}): StageLight {
  return { id: 1, name: 'Spot', address: 1, channels, color: null, x: 0, y: 0, angle: 0, shape: 'round', width: 1, depth: 1, ...more };
}

/** The levels the faders show: these from channel 1 on, the rest at 0. */
function levels(...values: number[]): number[] {
  return [...values, ...new Array<number>(UNIVERSE_SIZE - values.length).fill(0)];
}

/** A moving head of the library: pan with its fine channel, tilt, a color wheel, a shutter, a dimmer. */
const SPOT: FixtureType = {
  id: 1,
  maker: 'Maker',
  model: 'Spot',
  mode: '6-channel',
  source: 'maker/spot',
  channels: [
    { name: 'Pan', code: 'P' },
    { name: 'Pan fine', code: 'F', fine: 0 },
    { name: 'Tilt', code: 'T' },
    { name: 'Color Wheel', code: 'X', ranges: [{ from: 0, to: 7, label: 'Open' }, { from: 8, to: 15, label: 'Red', color: '#ff0000' }] },
    {
      name: 'Shutter',
      code: 'S',
      ranges: [
        { from: 0, to: 7, label: 'Closed' },
        { from: 8, to: 15, label: 'Open' },
        { from: 16, to: 255, label: 'Strobe' },
      ],
    },
    { name: 'Dimmer', code: 'D' },
  ],
};

describe('the strobe of a light', () => {
  /** A bar of the library with an RGB cell and a strobe channel like those of ADJ, and one with Hz. */
  const bar = (strobe: { from: number; to: number; label: string }[]): FixtureType => ({
    ...SPOT,
    channels: [{ name: 'Red', code: 'R' }, { name: 'Green', code: 'G' }, { name: 'Blue', code: 'B' }, { name: 'Strobe', code: 'S', ranges: strobe }],
  });
  const ADJ = bar([
    { from: 0, to: 31, label: 'Closed' },
    { from: 32, to: 63, label: 'Open' },
    { from: 64, to: 95, label: 'Strobe, slow to fast' },
    { from: 96, to: 127, label: 'Pulse, slow to fast' },
    { from: 128, to: 159, label: 'Strobe, fast to slow' },
    { from: 160, to: 191, label: 'Ramp up' },
    { from: 192, to: 223, label: 'Random strobe' },
    { from: 224, to: 255, label: 'Strobe, 0.3Hz to 19Hz' },
  ]);
  const at = (value: number, type: FixtureType | undefined = ADJ, letters = 'RGBS') => strobeOf(light(letters), type, levels(255, 0, 0, value));

  it('is none while the shutter is open or closed, or the range does something else', () => {
    expect(at(0)).toBeNull();
    expect(at(40)).toBeNull();
    expect(strobeOf(light('RGBS'), bar([{ from: 0, to: 127, label: 'No function' }, { from: 128, to: 255, label: 'Sound active' }]), levels(0, 0, 0, 200))).toBeNull();
  });

  it('knows its kind and how fast it goes within its range, in Hz where the library says them', () => {
    expect(at(64)).toEqual({ kind: 'strobe', random: false, hertz: null, pace: 0, label: 'Strobe' });
    expect(at(95)?.pace).toBe(1);
    expect(at(128)?.pace).toBe(1); // fast to slow: its first value is the fastest
    expect(at(100)).toMatchObject({ kind: 'pulse', label: 'Pulse' });
    expect(at(170)).toMatchObject({ kind: 'ramp-up', label: 'Ramp up' });
    expect(at(200)).toMatchObject({ kind: 'strobe', random: true, label: 'Random strobe' });
    expect(at(224)?.hertz).toBe(0.3);
    expect(at(255)?.hertz).toBe(19);
    expect(at(240)?.hertz).toBe(10); // 0.3 + 16/31 of the way to 19
  });

  it('of a light that says nothing of its strobe channel: steady at first, then slow to fast', () => {
    const plain = (value: number) => strobeOf(light('RGBS'), undefined, levels(255, 0, 0, value));
    expect(plain(5)).toBeNull();
    expect(plain(10)).toMatchObject({ kind: 'strobe', pace: 0 });
    expect(plain(255)?.pace).toBe(1);

    // a light whose shutter has ranges goes by those: its strobe speed channel (no ranges) says no more
    const shutterAndSpeed: FixtureType = { ...ADJ, channels: [...ADJ.channels, { name: 'Strobe speed', code: 'S' }] };
    expect(strobeOf(light('RGBSS'), shutterAndSpeed, levels(255, 0, 0, 40, 200))).toBeNull();
  });

  it('shows on the screen as fast as the light up to 2 flashes a second, and never more than 3', () => {
    const strobe = (hertz: number | null, pace = 0) => ({ kind: 'strobe' as const, random: false, hertz, pace, label: 'Strobe' });
    expect(shownFlashes(strobe(1.5))).toBe(1.5);
    expect(shownFlashes(strobe(0.1))).toBe(0.25);
    expect(shownFlashes(strobe(11))).toBe(2.5);
    expect(shownFlashes(strobe(20))).toBe(3);
    expect(shownFlashes(strobe(50))).toBe(3);
    expect(shownFlashes(strobe(null, 0))).toBe(1); // without Hz: 1 to 20 a second as it goes
    expect(shownFlashes(strobe(null, 1))).toBe(3);
  });
});

describe('the lights of the plan as fixtures', () => {
  it('know which of their channels are attributes and which give light', () => {
    const plan: StagePlan = {
      ...EMPTY_STAGE,
      lights: [light('PFTXSD'), light('X', { id: 2, address: 10 }), light('DRGBS', { id: 3, address: 20 })],
    };
    const roles = rolesOf(plan);
    expect(roles.attribute.slice(0, 6)).toEqual([true, true, true, true, true, false]);
    expect(roles.attribute[9]).toBe(false); // a hazer gives "light", as every channel did before
    expect(roles.attribute.slice(19, 24)).toEqual([false, false, false, false, true]); // the strobe of a par
    expect(roles.attributeLights.map((item) => item.attributes)).toEqual([[0, 1, 2, 3, 4], [23]]);
  });

  it('are held to their limits as the desk program holds them', () => {
    const plan: StagePlan = {
      ...EMPTY_STAGE,
      lights: [
        light('X', { address: 10, limit: 60 }),
        light('RGB', { id: 2, address: 20, limit: 50 }),
        light('D', { id: 3, address: 30, limit: 50 }),
      ],
    };
    const out = levels();
    out[9] = 255;
    out[19] = 255;
    out[20] = 128;
    out[29] = 200;
    applyLimits(out, rolesOf(plan).limits);
    expect(out[9]).toBe(153);
    expect(out.slice(19, 22)).toEqual([128, 64, 0]); // orange stays orange
    expect(out[29]).toBe(128);
  });

  it('have an intensity: their dimmer, or their colors, which keep their mix', () => {
    const par = light('RGB');
    expect(intensityOf(par, levels(200, 100, 0))).toBe(200);
    expect([...intensityChanges(par, undefined, 100, levels(200, 100, 0), null)]).toEqual([[1, 100], [2, 50], [3, 0]]);
    // from dark: in the color it had last
    expect([...intensityChanges(par, undefined, 255, levels(), [255, 0, 0])]).toEqual([[1, 255], [2, 0], [3, 0]]);
    expect([...intensityChanges(light('DRGB'), undefined, 128, levels(0, 255), null)]).toEqual([[1, 128]]);
  });

  it('open the shutter of a moving head that is brought up', () => {
    const spot = light('PFTXSD', { type: 1 });
    const changes = intensityChanges(spot, SPOT, 255, levels(), null);
    expect(changes.get(6)).toBe(255);
    expect(changes.get(5)).toBe(8);
    // a shutter that strobes stays as it is
    expect(intensityChanges(spot, SPOT, 255, levels(0, 0, 0, 0, 100), null).has(5)).toBe(false);
    expect(shutterClosed(spot, SPOT, levels())).toBe(true);
  });

  it('mix a color, a white channel taking the white of it', () => {
    expect([...colorChanges(light('RGBW'), [255, 128, 128], 255)]).toEqual([[1, 253], [2, 0], [3, 0], [4, 255]]);
    expect([...colorChanges(light('RGBRGB'), [0, 0, 255], 128)]).toEqual([[1, 0], [2, 0], [3, 128], [4, 0], [5, 0], [6, 128]]);
  });

  it('point a moving head in 16 bits with its fine channel', () => {
    const axes = panTiltOf(light('PFTXSD', { type: 1 }), SPOT)!;
    expect(axes).toEqual({ pan: 1, panFine: 2, tilt: 3, tiltFine: null });
    expect([...positionChanges(axes, 0.5, 1)]).toEqual([[1, 128], [2, 0], [3, 255]]);
    expect(positionOf(axes, levels(128, 0, 255))).toEqual({ x: 32768 / 65535, y: 1 });
    expect(panTiltOf(light('DRGB'), undefined)).toBeNull();
  });

  it('name each channel by the library, or by its letter and its cell', () => {
    expect(channelName(light('RGBRGB'), 4, undefined)).toBe('Green 2');
    expect(channelName(light('PFTXSD', { type: 1 }), 3, SPOT)).toBe('Color Wheel');
    const labels = fixtureLabels({ ...EMPTY_STAGE, lights: [light('DS', { name: 'Front', address: 5 })] });
    expect(labels[4]).toBe('Front' + NOTE_LINE_SEPARATOR + 'Dimmer');
    expect(labels[5]).toBe('Front' + NOTE_LINE_SEPARATOR + 'Strobe');
    expect(labels[6]).toBe('');
  });

  it('show their color: the one they mix, that of their wheel, or their own', () => {
    expect(swatchOf(light('RGB'), undefined, levels(255, 128, 0))).toBe('#ff8000');
    expect(swatchOf(light('RGB'), undefined, levels())).toBeNull();
    expect(swatchOf(light('PFTXSD', { type: 1 }), SPOT, levels(0, 0, 0, 10))).toBe('#ff0000');
    expect(swatchOf(light('D', { color: '#2060ff' }), undefined, levels())).toBe('#2060ff');
    expect(swatchOf(light('X'), undefined, levels(255))).toBeNull();
  });

  it('are steps: Add step goes on to the next fixture by address, and the next group', () => {
    const plan: StagePlan = {
      ...EMPTY_STAGE,
      lights: [light('D', { id: 7, address: 30 }), light('D', { id: 3, address: 10 }), light('D', { id: 5, address: 20 })],
      groups: [{ id: 2, name: 'A', lights: [] }, { id: 9, name: 'B', lights: [] }],
    };
    const fixture = recordedStep('fixture', 3, '#ff0000');
    expect(stepAfter(fixture, plan)).toEqual({ ...fixture, number: 5 });
    expect(stepAfter({ ...fixture, number: 7 }, plan).number).toBe(7); // the last stays the last
    expect(stepAfter(recordedStep('group', 2), plan)).toEqual({ ...recordedStep('group', 2), number: 9 });
    expect(stepAfter(recordedStep('channel', 4))).toEqual(recordedStep('channel', 5)); // no color for a channel
    expect('color' in recordedStep('channel', 4)).toBe(false);
  });

  it('turn colors into hue and depth and back', () => {
    expect(hsvOf(rgbOfHex('#ff8000'))[0]).toBeCloseTo(30.1, 0);
    expect(rgbOfHsv(120, 1)).toEqual([0, 255, 0]);
    expect(rgbOfHsv(0, 0)).toEqual([255, 255, 255]);
    expect(hexOf([255, 32, 0])).toBe('#ff2000');
  });
});
