import { CHANNEL_COUNT, EMPTY_STAGE, StageLight } from './desk-types';
import {
  cursorOf,
  formOf,
  freeSpot,
  keptOn,
  lettersOf,
  lightChannels,
  lookOf,
  nameFor,
  nextAddress,
  partsOf,
  resized,
  resizedLight,
  zoneAt,
} from './stage-lights';
import { NOTE_LINE_SEPARATOR } from './notes';

/** A round light of one square at address 1 with these channels. */
function light(channels: string, color: string | null = null): StageLight {
  return { id: 1, name: 'Test', address: 1, channels, color, x: 0, y: 0, angle: 0, shape: 'round', width: 1, depth: 1 };
}

/** The levels that go out: these from channel 1 on, the rest at 0. */
function output(...levels: number[]): number[] {
  return [...levels, ...new Array<number>(CHANNEL_COUNT - levels.length).fill(0)];
}

describe('the lights of the stage plan', () => {
  it('find their cells and dimmers in the letters of their channels', () => {
    const offsets = (channels: string) => {
      const parts = partsOf(channels);
      return {
        cells: parts.cells.map((cell) => ({ colors: cell.colors.map((color) => color.offset), dimmers: cell.dimmers })),
        masters: parts.masters,
        device: parts.device,
      };
    };
    // one cell, its dimmer before or after its colors; strobe and mode give no light
    expect(offsets('DSRGB')).toEqual({ cells: [{ colors: [2, 3, 4], dimmers: [0] }], masters: [], device: false });
    expect(offsets('RGBD')).toEqual({ cells: [{ colors: [0, 1, 2], dimmers: [3] }], masters: [], device: false });
    // four cells; one dimmer dims them all; a dimmer per cell dims its own
    expect(offsets('RGBRGBRGBRGB').cells.length).toBe(4);
    expect(offsets('DRGBRGB')).toEqual({
      cells: [{ colors: [1, 2, 3], dimmers: [] }, { colors: [4, 5, 6], dimmers: [] }],
      masters: [0],
      device: false,
    });
    expect(offsets('DRGBDRGB').cells.map((cell) => cell.dimmers)).toEqual([[0], [4]]);
    expect(offsets('RGBDRGBD').cells.map((cell) => cell.dimmers)).toEqual([[3], [7]]);
    // no color: a gel on a dimmer; no color and no dimmer: no light at all
    expect(offsets('D')).toEqual({ cells: [], masters: [0], device: false });
    expect(offsets('X')).toEqual({ cells: [], masters: [], device: true });
  });

  it('mix their color from their channels, dimmed by their dimmers', () => {
    expect(lookOf(light('RGB'), output(255, 0, 0)).cells).toEqual([{ lens: '255 0 0', glow: '255 0 0', brightness: 1 }]);
    expect(lookOf(light('RGB'), output(255, 255, 0)).cells[0].lens).toBe('255 255 0');
    expect(lookOf(light('RGBW'), output(0, 0, 0, 255)).cells[0].lens).toBe('255 255 255');

    // half the light: the screen shows it brighter than half, as the eye sees it; the glow keeps the color at full
    const half = lookOf(light('DRGB'), output(128, 255, 0, 0)).cells[0];
    expect(half.lens).toBe('186 0 0');
    expect(half.glow).toBe('255 0 0');
    expect(half.brightness).toBeCloseTo(0.73, 2);

    // dark: black lens, no glow
    expect(lookOf(light('DRGB'), output(0, 255, 255, 255)).cells[0]).toEqual({ lens: '0 0 0', glow: '0 0 0', brightness: 0 });
  });

  it('show each cell of a bar in its own color', () => {
    const bar = lookOf(light('RGBRGB'), output(255, 0, 0, 0, 0, 255));
    expect(bar.cells.map((cell) => cell.lens)).toEqual(['255 0 0', '0 0 255']);
  });

  it('shine in their own color without color channels, warm white without one, and show the level of a device', () => {
    expect(lookOf(light('D', '#ff2000'), output(255)).cells[0].lens).toBe('255 32 0');
    expect(lookOf(light('D'), output(255)).cells[0].lens).toBe('255 217 166');
    expect(lookOf(light('D', '#ff2000'), output(0)).cells[0].brightness).toBe(0);

    const hazer = lookOf(light('X'), output(128));
    expect(hazer).toEqual({ device: true, cells: [], level: 128 });
  });

  it('come from the form as letters, and go back to it', () => {
    expect(lettersOf({ type: 'dimmer', dimmer: 'none', cells: 1 }, '')).toBe('D');
    expect(lettersOf({ type: 'rgb', dimmer: 'none', cells: 4 }, '')).toBe('RGBRGBRGBRGB');
    expect(lettersOf({ type: 'rgbw', dimmer: 'first', cells: 1 }, '')).toBe('DRGBW');
    expect(lettersOf({ type: 'rgbwau', dimmer: 'last', cells: 2 }, '')).toBe('RGBWAURGBWAUD');
    expect(lettersOf({ type: 'device', dimmer: 'none', cells: 1 }, '')).toBe('X');
    expect(lettersOf({ type: 'letters', dimmer: 'none', cells: 1 }, 'd r-g b s')).toBe('DRGBS');

    expect(formOf('D')).toEqual({ type: 'dimmer', dimmer: 'none', cells: 1 });
    expect(formOf('RGBRGBRGBRGB')).toEqual({ type: 'rgb', dimmer: 'none', cells: 4 });
    expect(formOf('DRGBW')).toEqual({ type: 'rgbw', dimmer: 'first', cells: 1 });
    expect(formOf('RGBWAURGBWAUD')).toEqual({ type: 'rgbwau', dimmer: 'last', cells: 2 });
    expect(formOf('X')).toEqual({ type: 'device', dimmer: 'none', cells: 1 });
    expect(formOf('DRGBSM').type).toBe('letters');
  });

  it('go on the plan on the next free channel, in a free place, named after the note of their channel', () => {
    const lights = [{ ...light('RGBW'), address: 11 }];
    expect(nextAddress([])).toBe(1);
    expect(nextAddress(lights)).toBe(15);
    expect(nextAddress([{ ...light('RGB'), address: 510 }])).toBe(1); // nothing free after it

    expect(nameFor(5, 'Front' + NOTE_LINE_SEPARATOR + 'left')).toBe('Front left');
    expect(nameFor(5, '')).toBe('Light 5');

    const plan = { ...EMPTY_STAGE, width: 16, depth: 10 };
    expect(freeSpot(plan)).toEqual({ x: 1.5, y: 8.5 });
    expect(freeSpot({ ...plan, lights: [{ ...light('D'), x: 1.5, y: 8.5 }] })).toEqual({ x: 3.5, y: 8.5 });
  });

  it('stay on the plan, in half squares, also when the stage gets smaller', () => {
    const plan = { ...EMPTY_STAGE, width: 16, depth: 10, lights: [{ ...light('D'), x: 15, y: 9 }] };
    expect(keptOn(plan, 3.3, 4.8)).toEqual({ x: 3.5, y: 5 });
    expect(keptOn(plan, -9, 99)).toEqual({ x: -2, y: 14 }); // two squares beside, four in front

    const smaller = resized(plan, 'width', 8);
    expect(smaller.width).toBe(8);
    expect(smaller.lights[0].x).toBe(10); // the edge of the room beside the smaller stage
    expect(resized(plan, 'depth', 2).depth).toBe(4); // at least 4 squares
  });

  it('are sized by an edge or a corner, and moved by their middle, with the cursor to say so', () => {
    // a light 4 squares wide and 2 deep, pressed near its right edge, near its front left corner, in its middle
    expect(zoneAt(0, 4, 2, 1.9, 0, 0.2)).toEqual({ x: 1, y: 0 });
    expect(zoneAt(0, 4, 2, -1.9, 0.95, 0.2)).toEqual({ x: -1, y: 1 });
    expect(zoneAt(0, 4, 2, 0.5, 0.2, 0.2)).toEqual({ x: 0, y: 0 });
    // turned by 90°, its right edge lies at the front
    expect(zoneAt(90, 4, 2, 0, 1.9, 0.2)).toEqual({ x: 1, y: 0 });

    expect(cursorOf({ x: 1, y: 0 }, 0)).toBe('ew-resize');
    expect(cursorOf({ x: 0, y: -1 }, 0)).toBe('ns-resize');
    expect(cursorOf({ x: 1, y: 1 }, 0)).toBe('nwse-resize');
    expect(cursorOf({ x: 1, y: -1 }, 0)).toBe('nesw-resize');
    expect(cursorOf({ x: 1, y: 0 }, 90)).toBe('ns-resize');
    expect(cursorOf({ x: 0, y: 0 }, 0)).toBe('move');

    // The right edge dragged 2.4 squares out: 2 more squares, the left edge stays where it was.
    const bar = { ...light('RGB'), shape: 'square' as const, x: 8, y: 2.5, width: 4, depth: 1 };
    expect(resizedLight(bar, { x: 1, y: 0 }, 2.4, 0)).toEqual({ x: 9, y: 2.5, width: 6, depth: 1 });
    // The front left corner: one square narrower, two deeper
    expect(resizedLight(bar, { x: -1, y: 1 }, 1, 2)).toEqual({ x: 8.5, y: 3.5, width: 3, depth: 3 });
    // never smaller than one square
    expect(resizedLight(bar, { x: 1, y: 0 }, -9, 0).width).toBe(1);
    // turned by 90°: its right edge is at the front, so dragging to the front makes it wider
    expect(resizedLight({ ...bar, angle: 90 }, { x: 1, y: 0 }, 0, 1)).toEqual({ x: 8, y: 3, width: 5, depth: 1 });
  });

  it('go up and down by a double click with the channels that give light', () => {
    expect(lightChannels({ address: 10, channels: 'DRGBS' })).toEqual([10, 11, 12, 13]); // not the strobe
    expect(lightChannels({ address: 20, channels: 'X' })).toEqual([20]); // a hazer: its channel
  });

  it('lie on the lines of the grid when they are square and not turned askew', () => {
    const plan = { ...EMPTY_STAGE, width: 16, depth: 10 };
    const bar = { ...light('RGB'), shape: 'square' as const, width: 4, depth: 1 };
    // four squares wide: its edges on whole squares, so its middle too; one deep: its middle on a half
    expect(keptOn(plan, 7.3, 2.2, bar)).toEqual({ x: 7, y: 2.5 });
    expect(keptOn(plan, 7.6, 2.9, bar)).toEqual({ x: 8, y: 2.5 });
    // turned by 90°, it is four deep and one wide
    expect(keptOn(plan, 7.3, 2.2, { ...bar, angle: 90 })).toEqual({ x: 7.5, y: 2 });
    // askew, or round: half squares, as every other light
    expect(keptOn(plan, 7.3, 2.2, { ...bar, angle: 45 })).toEqual({ x: 7.5, y: 2 });
    expect(keptOn(plan, 7.3, 2.2, { ...bar, shape: 'round' })).toEqual({ x: 7.5, y: 2 });
  });
});
