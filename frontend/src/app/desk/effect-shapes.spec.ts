import { StageLight } from './desk-types';
import {
  SPEED_PLACES,
  effectLights,
  effectSummary,
  fitsKind,
  flashesPerSecond,
  newEffect,
  speedAt,
  speedPlace,
  speedText,
} from './effect-shapes';
import { Effect, EffectPlayback, effectLamp, withEffectLevels } from './effects';

const light = (id: number, channels: string, more: Partial<StageLight> = {}): StageLight => ({
  id,
  name: `Light ${id}`,
  address: id * 10,
  channels,
  color: null,
  x: id,
  y: 2,
  angle: 0,
  shape: 'round',
  width: 1,
  depth: 1,
  ...more,
});

// A PAR, a head, a hazer, a pixel bar and a head with a color wheel from the library.
const plan = {
  lights: [
    light(1, 'DRGB'),
    light(2, 'PTD'),
    light(3, 'XX'),
    light(4, 'RGBRGB'),
    light(5, 'PTDX', { type: 7 }),
  ],
  groups: [{ id: 9, name: 'Heads', lights: [2, 5] }],
  types: [
    {
      id: 7,
      maker: 'Maker',
      model: 'Spot',
      mode: '4-channel',
      source: 'maker/spot',
      channels: [
        { name: 'Pan', code: 'P' },
        { name: 'Tilt', code: 'T' },
        { name: 'Dimmer', code: 'D' },
        {
          name: 'Color wheel',
          code: 'X',
          ranges: [{ from: 0, to: 9, label: 'Red', color: '#ff0000' }],
        },
      ],
    },
  ],
};

describe('effects on the page', () => {
  it('a new effect takes the first shape of its kind and every light that can do it', () => {
    const movement = newEffect(3, 'movement', plan);
    expect(movement).toMatchObject({
      number: 3,
      name: 'Circle',
      shape: 'circle',
      lights: [2, 5],
      speed: 15,
      size: 30,
      spread: 0,
      centerPan: 50, // around a center of its own, the middle of the heads' range
      centerTilt: 50,
    });
    expect(newEffect(4, 'intensity', plan)).toMatchObject({ centerPan: null, centerTilt: null });
    expect(newEffect(4, 'intensity', plan).lights).toEqual([1, 2, 4, 5]); // the hazer gives no light
    expect(newEffect(5, 'color', plan).lights).toEqual([1, 4, 5]); // the head of the library turns its color wheel
    expect(newEffect(6, 'pixel', plan)).toMatchObject({ shape: 'gradient', colors: ['#ff2000'] });
  });

  it('a light fits a kind as the desk program sees it', () => {
    expect(fitsKind(plan.lights[0], 'movement', plan)).toBe(false);
    expect(fitsKind(plan.lights[1], 'color', plan)).toBe(false); // a head of letters without colors
    expect(fitsKind(plan.lights[2], 'intensity', plan)).toBe(false);
    expect(fitsKind(plan.lights[3], 'pixel', plan)).toBe(true);
  });

  it('the lights of an effect are those of its group, or those it names that are still there', () => {
    const effect = { ...newEffect(1, 'movement', plan), lights: [2, 99] };
    expect(effectLights(effect, plan).map((item) => item.id)).toEqual([2]);
    expect(effectLights({ ...effect, group: 9 }, plan).map((item) => item.id)).toEqual([2, 5]);
  });

  it('the live speed fader goes evenly in octaves, and back again', () => {
    expect(speedPlace(0.5)).toBe(0);
    expect(speedPlace(600)).toBe(SPEED_PLACES);
    expect(speedPlace(60) - speedPlace(30)).toBe(speedPlace(120) - speedPlace(60));
    // a place is about 0.7 % of the speed: back within 1 %
    for (const bpm of [1, 7.5, 30, 120, 400])
      expect(Math.abs(speedAt(speedPlace(bpm)) / bpm - 1)).toBeLessThan(0.01);
    expect(speedText(120)).toBe('120 BPM');
    expect(speedText(2.5)).toBe('2.5 BPM');
  });

  it('say how many times a second an effect can flash, for the Stage view', () => {
    const intensity: Effect = newEffect(1, 'intensity', plan);
    expect(flashesPerSecond(intensity, 240)).toBe(4);
    expect(
      flashesPerSecond(
        { ...intensity, kind: 'color', shape: 'chase', colors: ['#ff0000', '#00ff00', '#0000ff'] },
        60,
      ),
    ).toBe(3);
    expect(flashesPerSecond({ ...intensity, kind: 'color', shape: 'rainbow' }, 600)).toBe(0); // the colors run, nothing flashes
    expect(flashesPerSecond({ ...intensity, kind: 'pixel', shape: 'bars', size: 25 }, 60)).toBe(4); // four bars pass a cell each cycle
    expect(flashesPerSecond(newEffect(2, 'movement', plan), 600)).toBe(0);
  });

  it('the running effects give their channels what the desk program reports', () => {
    expect(withEffectLevels([1, 2, 3, 4], { channels: [2, 4, 9], levels: [200, 0, 50] })).toEqual([
      1, 200, 3, 0,
    ]);
    expect(withEffectLevels([1, 2], null)).toEqual([1, 2]);
  });

  it('the key of an effect is faint without an effect, half when it is out, full while it runs', () => {
    const effect = newEffect(1, 'color', plan);
    const playback: EffectPlayback = {
      number: 1,
      running: true,
      level: 255,
      speed: 10,
      size: 100,
      spread: 100,
      fading: false,
      phase: 0,
      bpm: 10,
      changed: false,
    };
    expect(effectLamp(undefined, undefined)).toBe(1);
    expect(effectLamp(effect, undefined)).toBe(2);
    expect(effectLamp(effect, playback)).toBe(3);
    expect(effectSummary(effect, 3)).toBe('A color (rainbow) on 3 lights at 10 BPM');
  });
});
