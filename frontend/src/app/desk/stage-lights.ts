import {
  CHANNEL_COUNT,
  LIGHT_MAX_SIZE,
  STAGE_BACK_ROOM,
  STAGE_FRONT_ROOM,
  STAGE_MAX_SIZE,
  STAGE_MIN_SIZE,
  STAGE_SIDE_ROOM,
  StageLight,
  StagePlan,
} from './desk-types';
import { NOTE_LINE_SEPARATOR } from './notes';

/**
 * The lights of the stage plan: what their channels are, and the color they give at a moment.
 *
 * A light's channels are one letter each, from its address on: D dimmer, R red, G green, B blue,
 * W white, A amber, U ultraviolet; any other letter is a channel that gives no light (S strobe,
 * M mode). A light of several cells repeats its color letters: RGBRGB is two cells. A D dims the
 * cell it stands in, before its colors or after them; a light with one D and several cells
 * dims all of them with it. A light without color letters shines in its own color (a gel), and
 * one without color letters and without a D, such as a hazer, gives no light at all.
 */

/** The color each color letter gives at full, as red, green and blue from 0 to 1, in linear light. */
const COLORS: Readonly<Record<string, readonly [number, number, number]>> = {
  R: [1, 0, 0],
  G: [0, 1, 0],
  B: [0, 0, 1],
  W: [1, 1, 1],
  A: [1, 0.35, 0], // amber: about 590 nm
  U: [0.12, 0.02, 0.6], // ultraviolet: the deep violet that shows of it
};

/** The color of a light without color channels that has none of its own: warm white, as a lamp with a filament. */
export const WARM_WHITE = '#ffd9a6';

/** The most cells the form offers, and the most letters a light can have. */
export const MAX_CELLS = 16;
export const MAX_LETTERS = 64;

/** A cell of a light: the channels of its colors and its own dimmers, as places from the address (0 is the address). */
export interface LightCell {
  readonly colors: readonly { readonly letter: string; readonly offset: number }[];
  readonly dimmers: readonly number[];
}

/** What a light is made of. */
export interface LightParts {
  /** Its cells: one for a light of one color, several for a bar of cells; none for a light without color channels. */
  readonly cells: readonly LightCell[];
  /** The dimmers of the whole light. */
  readonly masters: readonly number[];
  /** True for a light that gives no light: no color channel and no dimmer. */
  readonly device: boolean;
}

/** Finds the cells and the dimmers in the letters of a light's channels. */
export function partsOf(channels: string): LightParts {
  const cells: { colors: { letter: string; offset: number }[]; dimmers: number[] }[] = [];
  let current: (typeof cells)[number] | null = null;
  let waiting: number[] = []; // dimmers before the colors of the next cell
  let dimmerCount = 0;

  for (let offset = 0; offset < channels.length; offset++) {
    const letter = channels[offset];
    if (letter in COLORS) {
      if (!current || current.colors.some((color) => color.letter === letter)) {
        current = { colors: [], dimmers: waiting };
        waiting = [];
        cells.push(current);
      }
      current.colors.push({ letter, offset });
    } else if (letter === 'D') {
      dimmerCount++;
      // After the colors of a cell that has no dimmer yet: that cell's. Otherwise the next one's.
      if (current && current.dimmers.length === 0 && waiting.length === 0) current.dimmers.push(offset);
      else waiting.push(offset);
    }
  }

  if (cells.length === 0) return { cells: [], masters: waiting, device: waiting.length === 0 };
  cells[cells.length - 1].dimmers.push(...waiting); // dimmers after the last colors

  // One dimmer for a light of several cells: it dims them all.
  if (dimmerCount === 1 && cells.length > 1) {
    const master = cells.flatMap((cell) => cell.dimmers);
    return { cells: cells.map((cell) => ({ ...cell, dimmers: [] })), masters: master, device: false };
  }
  return { cells, masters: [], device: false };
}

/** How a cell of a light looks at a moment. */
export interface CellLook {
  /** The color of its lens, as "r g b" (0 to 255): dark while it is off. */
  readonly lens: string;
  /** The color of its light at full, as "r g b", for the glow around it. */
  readonly glow: string;
  /** How bright it is, 0 to 1, as the eye sees it. */
  readonly brightness: number;
}

/** How a light looks at a moment. */
export interface LightLook {
  /** True for a light that gives no light, such as a hazer: it shows the level of its first channel. */
  readonly device: boolean;
  /** One for each cell; one for a light without color channels; none for a device. */
  readonly cells: readonly CellLook[];
  /** For a device: the level of its first channel, 0 to 255. */
  readonly level: number;
}

/**
 * How a light looks with the levels that go out: the colors of its cells, mixed from its color
 * channels (or its own color) and dimmed by its dimmers. A moving head from the library may
 * say more (see fixtures.ts): `color`, the color its color wheel shows, and `dark`, a shutter
 * that is closed.
 */
export function lookOf(light: StageLight, output: readonly number[], head: { color?: string | null; dark?: boolean } = {}): LightLook {
  const parts = partsOf(light.channels);
  const level = (offset: number): number => (head.dark ? 0 : (output[light.address - 1 + offset] ?? 0) / 255);
  const dimmed = (dimmers: readonly number[]): number => dimmers.reduce((factor, offset) => factor * level(offset), 1);

  if (parts.device) return { device: true, cells: [], level: Math.round(level(0) * 255) };

  const master = dimmed(parts.masters);
  if (parts.cells.length === 0) {
    const [red, green, blue] = rgbOf(head.color ?? light.color ?? WARM_WHITE);
    return { device: false, cells: [cellLook(red * master, green * master, blue * master)], level: 0 };
  }

  const cells = parts.cells.map((cell) => {
    let red = 0;
    let green = 0;
    let blue = 0;
    for (const { letter, offset } of cell.colors) {
      const [r, g, b] = COLORS[letter];
      const part = level(offset);
      red += r * part;
      green += g * part;
      blue += b * part;
    }
    const factor = master * dimmed(cell.dimmers);
    return cellLook(Math.min(1, red) * factor, Math.min(1, green) * factor, Math.min(1, blue) * factor);
  });
  return { device: false, cells, level: 0 };
}

// Linear light (0 to 1) as the screen shows it: the screen's colors are not linear.
function cellLook(red: number, green: number, blue: number): CellLook {
  const strongest = Math.max(red, green, blue);
  const shown = (part: number) => Math.round(255 * Math.pow(part, 1 / 2.2));
  const full = (part: number) => (strongest > 0 ? shown(part / strongest) : 0);
  return {
    lens: `${shown(red)} ${shown(green)} ${shown(blue)}`,
    glow: `${full(red)} ${full(green)} ${full(blue)}`,
    brightness: Math.pow(strongest, 1 / 2.2),
  };
}

// "#ffd9a6" as linear light from 0 to 1.
function rgbOf(color: string): [number, number, number] {
  const channel = (start: number) => Math.pow(parseInt(color.slice(start, start + 2), 16) / 255, 2.2);
  return [channel(1), channel(3), channel(5)];
}

// ---------------------------------------------------------------- the form of a light

/** The kinds of lights the form offers. "letters" are the channels typed as letters. */
export type LightType = 'dimmer' | 'rgb' | 'rgbw' | 'rgba' | 'rgbwa' | 'rgbwu' | 'rgbwau' | 'device' | 'letters';

/** Where a light of color channels has a dimmer channel for the whole light. */
export type DimmerPlace = 'none' | 'first' | 'last';

/** A light as the form shows it. */
export interface LightForm {
  readonly type: LightType;
  readonly dimmer: DimmerPlace;
  readonly cells: number;
}

/** The kinds of lights, with the letters of one cell. */
export const LIGHT_TYPES: readonly { readonly type: LightType; readonly label: string; readonly cell: string }[] = [
  { type: 'dimmer', label: 'Dimmer, one color', cell: 'D' },
  { type: 'rgb', label: 'RGB', cell: 'RGB' },
  { type: 'rgbw', label: 'RGBW', cell: 'RGBW' },
  { type: 'rgba', label: 'RGBA', cell: 'RGBA' },
  { type: 'rgbwa', label: 'RGBWA', cell: 'RGBWA' },
  { type: 'rgbwu', label: 'RGBW + UV', cell: 'RGBWU' },
  { type: 'rgbwau', label: 'RGBWA + UV', cell: 'RGBWAU' },
  { type: 'device', label: 'No light (hazer, mirror ball)', cell: 'X' },
  { type: 'letters', label: 'Channels as letters', cell: '' },
];

/** True for a kind of light made of color channels, which has cells and may have a dimmer. */
export function hasCells(type: LightType): boolean {
  return type !== 'dimmer' && type !== 'device' && type !== 'letters';
}

/** The letters of a light of the form; "letters" keeps those it is given. */
export function lettersOf(form: LightForm, letters: string): string {
  if (form.type === 'letters') return cleanLetters(letters);
  const cell = LIGHT_TYPES.find((type) => type.type === form.type)?.cell ?? 'D';
  if (!hasCells(form.type)) return cell;

  const cells = Math.min(MAX_CELLS, Math.max(1, Math.round(form.cells) || 1));
  return (form.dimmer === 'first' ? 'D' : '') + cell.repeat(cells) + (form.dimmer === 'last' ? 'D' : '');
}

/** The form of a light's letters: a kind the form knows, or "letters". */
export function formOf(channels: string): LightForm {
  if (channels === 'D') return { type: 'dimmer', dimmer: 'none', cells: 1 };
  if (channels === 'X') return { type: 'device', dimmer: 'none', cells: 1 };

  for (const kind of LIGHT_TYPES.filter((type) => hasCells(type.type))) {
    for (const dimmer of ['none', 'first', 'last'] as const) {
      const body =
        dimmer === 'first' ? (channels.startsWith('D') ? channels.slice(1) : null)
        : dimmer === 'last' ? (channels.endsWith('D') ? channels.slice(0, -1) : null)
        : channels;
      const cells = body ? body.length / kind.cell.length : 0;
      if (Number.isInteger(cells) && cells >= 1 && cells <= MAX_CELLS && body === kind.cell.repeat(cells)) {
        return { type: kind.type, dimmer, cells };
      }
    }
  }
  return { type: 'letters', dimmer: 'none', cells: 1 };
}

/** Letters as a light keeps them: capitals A to Z, at most 64. */
export function cleanLetters(text: string): string {
  return text.toUpperCase().replace(/[^A-Z]/g, '').slice(0, MAX_LETTERS);
}

/** True when the light gets its color from the form's color field: it has no color channel but gives light. */
export function usesOwnColor(channels: string): boolean {
  const parts = partsOf(channels);
  return !parts.device && parts.cells.length === 0;
}

// ---------------------------------------------------------------- lights on the plan

/** The channels of a light, as the page says them: "9", or "30 to 41". */
export function channelRange(light: Pick<StageLight, 'address' | 'channels'>): string {
  const last = light.address + light.channels.length - 1;
  return last === light.address ? `${light.address}` : `${light.address} to ${last}`;
}

/** The address after the last channel of any light: where a new light goes. */
export function nextAddress(lights: readonly StageLight[]): number {
  const after = lights.reduce((end, light) => Math.max(end, light.address + light.channels.length), 1);
  return after > CHANNEL_COUNT ? 1 : after;
}

/** A name for a new light at an address: the note of its channel, both lines, or "Light 5". */
export function nameFor(address: number, note: string): string {
  const words = note.split(NOTE_LINE_SEPARATOR).map((line) => line.trim()).filter((line) => line !== '').join(' ');
  return (words || `Light ${address}`).slice(0, 24);
}

/** A light moves in half squares. */
export const SNAP = 0.5;

/**
 * A place on a plan for a light: a square light that is not turned askew lies with its edges on
 * the lines of the grid, over whole squares; any other light stands on the grid of half
 * squares. Never beyond the room around the stage.
 */
export function keptOn(
  plan: Pick<StagePlan, 'width' | 'depth'>,
  x: number,
  y: number,
  light?: Pick<StageLight, 'shape' | 'width' | 'depth' | 'angle'>,
): { x: number; y: number } {
  let snapX = (value: number) => Math.round(value / SNAP) * SNAP;
  let snapY = snapX;
  if (light?.shape === 'square' && light.angle % 90 === 0) {
    const across = light.angle % 180 === 0 ? light.width : light.depth;
    const deep = light.angle % 180 === 0 ? light.depth : light.width;
    snapX = (value) => Math.round(value - across / 2) + across / 2;
    snapY = (value) => Math.round(value - deep / 2) + deep / 2;
  }
  const kept = (value: number, low: number, high: number) => Math.min(high, Math.max(low, value));
  return {
    x: kept(snapX(x), -STAGE_SIDE_ROOM, plan.width + STAGE_SIDE_ROOM),
    y: kept(snapY(y), -STAGE_BACK_ROOM, plan.depth + STAGE_FRONT_ROOM),
  };
}

/** An address from 1 on at which a light of this many channels fits. */
export function fittingAddress(address: number, channels: number): number {
  return Math.min(CHANNEL_COUNT - channels + 1, Math.max(1, Math.round(address) || 1));
}

/** The plan with one light changed. */
export function withLight(plan: StagePlan, id: number, changes: Partial<StageLight>): StagePlan {
  return { ...plan, lights: plan.lights.map((light) => (light.id === id ? { ...light, ...changes } : light)) };
}

/** The plan without one light; its groups go on without it. */
export function withoutLight(plan: StagePlan, id: number): StagePlan {
  return {
    ...plan,
    lights: plan.lights.filter((light) => light.id !== id),
    groups: (plan.groups ?? []).map((group) => ({ ...group, lights: group.lights.filter((member) => member !== id) })),
  };
}

/**
 * A copy of a light for the next one of the same kind: on the channels after the last light,
 * beside the light on the plan, with the next number in its name ("Spot 1" gives "Spot 2").
 */
export function copiedLight(plan: StagePlan, light: StageLight, id = plan.lights.reduce((highest, other) => Math.max(highest, other.id), 0) + 1): StageLight {
  const numbered = /^(.*?)(\d+)$/.exec(light.name);
  const name = (numbered ? `${numbered[1]}${Number(numbered[2]) + 1}` : `${light.name || 'Light'} 2`).slice(0, 24);
  const place = keptOn(plan, light.x + Math.max(light.width, 1) + 0.5, light.y, light);
  return { ...light, id, name, address: fittingAddress(nextAddress(plan.lights), light.channels.length), ...place };
}

/** The plan with a stage of another width or depth (4 to 64 squares); lights beyond its new edge come to the edge. */
export function resized(plan: StagePlan, side: 'width' | 'depth', size: number): StagePlan {
  const kept = Math.min(STAGE_MAX_SIZE, Math.max(STAGE_MIN_SIZE, Math.round(size) || plan[side]));
  const stage = { ...plan, [side]: kept };
  return { ...stage, lights: plan.lights.map((light) => ({ ...light, ...keptOn(stage, light.x, light.y, light) })) };
}

// ---------------------------------------------------------------- on the plan: resizing, faders

/**
 * Where on a light a point is: on an edge or a corner (x -1 left, 1 right; y -1 back, 1 front,
 * in the light's own directions, before it is turned), or in its middle (0 and 0).
 */
export interface LightZone {
  readonly x: -1 | 0 | 1;
  readonly y: -1 | 0 | 1;
}

/**
 * The zone of a light at a point: `dx` and `dy` are the point's distance from the light's middle
 * in squares, in the plan's directions; the light is `width` by `depth` squares as it is drawn;
 * `edge` is how near to an edge counts as on it, in squares (at most a quarter of the light).
 */
export function zoneAt(angle: number, width: number, depth: number, dx: number, dy: number, edge: number): LightZone {
  const [x, y] = toLocal(angle, dx, dy);
  const side = (value: number, size: number): -1 | 0 | 1 => {
    const near = Math.min(edge, size / 4);
    return value > size / 2 - near ? 1 : value < -size / 2 + near ? -1 : 0;
  };
  return { x: side(x, width), y: side(y, depth) };
}

/** The cursor over a zone of a light: across its edge or corner as the light lies turned; "move" in its middle. */
export function cursorOf(zone: LightZone, angle: number): string {
  if (zone.x === 0 && zone.y === 0) return 'move';
  const [x, y] = toPlan(angle, zone.x, zone.y);
  const direction = (((Math.atan2(y, x) * 180) / Math.PI) % 180 + 180) % 180;
  return ['ew-resize', 'nwse-resize', 'ns-resize', 'nesw-resize'][Math.round(direction / 45) % 4];
}

/**
 * A light resized by dragging an edge or a corner by `dx` and `dy` squares, in the plan's
 * directions: whole squares from 1 to 32, the opposite edge staying where it is.
 */
export function resizedLight(light: StageLight, zone: LightZone, dx: number, dy: number): Pick<StageLight, 'x' | 'y' | 'width' | 'depth'> {
  const [x, y] = toLocal(light.angle, dx, dy);
  const size = (from: number, by: number) => Math.min(LIGHT_MAX_SIZE, Math.max(1, Math.round(from + by)));
  const width = zone.x === 0 ? light.width : size(light.width, zone.x * x);
  const depth = zone.y === 0 ? light.depth : size(light.depth, zone.y * y);
  const [moveX, moveY] = toPlan(light.angle, (zone.x * (width - light.width)) / 2, (zone.y * (depth - light.depth)) / 2);
  const tidy = (value: number) => Math.round(value * 1000) / 1000;
  return { x: tidy(light.x + moveX), y: tidy(light.y + moveY), width, depth };
}

/**
 * The channels a double click on a light puts to full, and back to zero: those that give light
 * (its dimmers and colors); a light that gives none, such as a hazer, all of its channels. A
 * strobe or a mode channel stays where it is.
 */
export function lightChannels(light: Pick<StageLight, 'address' | 'channels'>): number[] {
  const all = [...light.channels].map((letter, offset) => ({ letter, channel: light.address + offset }));
  const giving = all.filter(({ letter }) => letter === 'D' || letter in COLORS);
  return (giving.length > 0 ? giving : all).map(({ channel }) => channel);
}

// From the plan's directions to a light's own (before it is turned), and back.
function toLocal(angle: number, x: number, y: number): [number, number] {
  const turn = (angle * Math.PI) / 180;
  return [x * Math.cos(turn) + y * Math.sin(turn), -x * Math.sin(turn) + y * Math.cos(turn)];
}

function toPlan(angle: number, x: number, y: number): [number, number] {
  const turn = (angle * Math.PI) / 180;
  return [x * Math.cos(turn) - y * Math.sin(turn), x * Math.sin(turn) + y * Math.cos(turn)];
}

/** What each letter of a light's channels stands for, as a fader of the light is named. */
export const LETTER_NAMES: Readonly<Record<string, string>> = {
  D: 'dimmer',
  R: 'red',
  G: 'green',
  B: 'blue',
  W: 'white',
  A: 'amber',
  U: 'UV',
  P: 'pan',
  T: 'tilt',
};

/** The color of a fader of a light: that of its letter; a dimmer and another channel in the color of the channels. */
export const LETTER_COLORS: Readonly<Record<string, string>> = {
  R: '#ff3b30',
  G: '#3ddc84',
  B: '#4a7dff',
  W: '#f1f3f6',
  A: '#ffb020',
  U: '#9b5cff',
};

/** A place for a new light on the plan: the first free one, going along the rows from the front. */
export function freeSpot(plan: StagePlan): { x: number; y: number } {
  const taken = (x: number, y: number) => plan.lights.some((light) => Math.hypot(light.x - x, light.y - y) < 1.4);
  for (let y = plan.depth - 1.5; y >= 1; y -= 2) {
    for (let x = 1.5; x <= plan.width - 1; x += 2) {
      if (!taken(x, y)) return { x, y };
    }
  }
  return { x: plan.width / 2, y: plan.depth / 2 };
}
