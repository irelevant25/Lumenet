import { FixtureRange, FixtureType, MAX_CHANNELS, MAX_LEVEL, NO_LIMIT, StageLight, StagePlan } from './desk-types';
import { NOTE_LINE_SEPARATOR } from './notes';
import { partsOf } from './stage-lights';

/**
 * The lights of the stage plan as fixtures: what each of their channels does, and how a fixture
 * is played as a whole (its intensity, its color, where a moving head points).
 *
 * A channel gives light when it is a dimmer (D) or a color (R G B W A U) of a light, a channel of
 * a device that gives no light (a hazer), or no light's channel. Every other channel of a light
 * that gives light is an attribute of it: a pan (P), a tilt (T), a fine channel (F), a shutter (S),
 * a wheel or a mode. The master, blackout, a solo and a light's limit act on the light only; an
 * attribute comes from the fader that took it last (latest takes precedence), as the desk program
 * does it (Desk.Attributes.cs and PlanRoles.cs there).
 */

/** The letters of channels that give light. */
const LIGHT_LETTERS = 'DRGBWAU';

/** A light with attributes: all its channels, and those that are attributes (indexes from 0, channel 1 at 0). */
export interface AttributeLight {
  readonly channels: readonly number[];
  readonly attributes: readonly number[];
}

/**
 * What a light's limit does: its dimmers and a device's channels stop at `cap`; the colors of
 * a cell without a dimmer are dimmed together, so that the color stays.
 */
export interface LimitRule {
  readonly cap: number;
  readonly stopped: readonly number[];
  readonly cells: readonly (readonly number[])[];
}

/** What the plan says about every channel. */
export interface PlanRoles {
  /** For each channel (channel 1 at 0): true for an attribute of a light. */
  readonly attribute: readonly boolean[];
  readonly attributeLights: readonly AttributeLight[];
  readonly limits: readonly LimitRule[];
}

/** True for a light that gives no light: no dimmer, no color, no pan or tilt (a hazer). */
export function isDevice(letters: string): boolean {
  return ![...letters].some((letter) => LIGHT_LETTERS.includes(letter) || letter === 'P' || letter === 'T');
}

/** Works out the roles of the channels of a plan's lights. */
export function rolesOf(plan: StagePlan): PlanRoles {
  const attribute = new Array<boolean>(MAX_CHANNELS).fill(false);
  const attributeLights: AttributeLight[] = [];
  const limits: LimitRule[] = [];
  for (const light of plan.lights) {
    const first = light.address - 1;
    const letters = light.channels;
    const channels = [...letters].map((_, offset) => first + offset).filter((channel) => channel < MAX_CHANNELS);
    const device = isDevice(letters);
    const attributes = device ? [] : channels.filter((channel) => !LIGHT_LETTERS.includes(letters[channel - first]));
    if (attributes.length > 0) {
      for (const channel of attributes) attribute[channel] = true;
      attributeLights.push({ channels, attributes });
    }
    if ((light.limit ?? NO_LIMIT) < NO_LIMIT) limits.push(limitRule(light, device));
  }
  return { attribute, attributeLights, limits };
}

function limitRule(light: StageLight, device: boolean): LimitRule {
  const first = light.address - 1;
  const cap = Math.round(((light.limit ?? NO_LIMIT) * MAX_LEVEL) / 100);
  if (device) return { cap, stopped: [...light.channels].map((_, offset) => first + offset), cells: [] };

  const parts = partsOf(light.channels);
  const stopped = [...parts.masters, ...parts.cells.flatMap((cell) => cell.dimmers)].map((offset) => first + offset);
  const cells =
    parts.masters.length > 0
      ? []
      : parts.cells.filter((cell) => cell.dimmers.length === 0).map((cell) => cell.colors.map((color) => first + color.offset));
  return { cap, stopped, cells };
}

/** Holds the lights in `levels` (changed in place) to their limits. */
export function applyLimits(levels: number[], limits: readonly LimitRule[]): void {
  for (const rule of limits) {
    for (const channel of rule.stopped) levels[channel] = Math.min(levels[channel], rule.cap);
    for (const cell of rule.cells) {
      const strongest = Math.max(...cell.map((channel) => levels[channel]));
      if (strongest <= rule.cap) continue;
      for (const channel of cell) levels[channel] = Math.round((levels[channel] * rule.cap) / strongest);
    }
  }
}

/** A look holds a light when it gives any of the light's channels a level. */
export function lookHolds(look: readonly number[], light: AttributeLight): boolean {
  return light.channels.some((channel) => (look[channel] ?? 0) > 0);
}

// ---------------------------------------------------------------- names

/** What the letters of a light's channels are called, for a channel without a name of its own. */
const FUNCTION_NAMES: Readonly<Record<string, string>> = {
  D: 'Dimmer',
  R: 'Red',
  G: 'Green',
  B: 'Blue',
  W: 'White',
  A: 'Amber',
  U: 'UV',
  P: 'Pan',
  T: 'Tilt',
  S: 'Strobe',
};

/** The lights a step of a fixture or a group stands for, as the plan has them now; none for any other step. */
export function stepLights(plan: Pick<StagePlan, 'lights' | 'groups'>, step: { target: string; number: number }): StageLight[] {
  if (step.target === 'fixture') return plan.lights.filter((light) => light.id === step.number);
  if (step.target !== 'group') return [];
  const group = plan.groups.find((item) => item.id === step.number);
  return (group?.lights ?? []).flatMap((id) => plan.lights.filter((light) => light.id === id));
}

/** The color a step of a fixture gets when its key records it: the one it mixes or its wheel shows now; null for none. */
export function recordedColorOf(light: StageLight, type: FixtureType | undefined, levels: readonly number[]): string | null {
  const mixed = mixedColor(light, levels);
  return mixed ? hexOf(mixed) : wheelColorOf(light, type, levels);
}

/** The kind of light from the library a light is; undefined for a light of letters. */
export function typeOf(plan: StagePlan, light: StageLight): FixtureType | undefined {
  return light.type == null ? undefined : plan.types.find((type) => type.id === light.type);
}

/**
 * What a channel of a light does, as a word or two: the library's name, or the name of its
 * letter, with the number of its cell for a light of several cells ("Red 2").
 */
export function channelName(light: StageLight, offset: number, type: FixtureType | undefined): string {
  const named = type?.channels[offset]?.name;
  if (named) return named;

  const letter = light.channels[offset];
  const name = FUNCTION_NAMES[letter];
  if (!name) return `Channel ${offset + 1}`;
  const parts = partsOf(light.channels);
  if (parts.cells.length < 2) return name;
  const cell = parts.cells.findIndex((part) => part.colors.some((color) => color.offset === offset) || part.dimmers.includes(offset));
  return cell >= 0 ? `${name} ${cell + 1}` : name;
}

/**
 * For each channel (channel 1 at 0): the light it belongs to and what it does there, as a note of
 * two lines ("Spot 1" and "Pan"); empty for a channel of no light. The faders show it where the
 * owner wrote no note of their own.
 */
export function fixtureLabels(plan: StagePlan): string[] {
  const labels = new Array<string>(MAX_CHANNELS).fill('');
  for (const light of plan.lights) {
    const type = typeOf(plan, light);
    for (let offset = 0; offset < light.channels.length; offset++) {
      const index = light.address - 1 + offset;
      if (index < MAX_CHANNELS && labels[index] === '') {
        labels[index] = `${light.name || 'Light'}${NOTE_LINE_SEPARATOR}${channelName(light, offset, type)}`;
      }
    }
  }
  return labels;
}

// ---------------------------------------------------------------- playing a fixture

/** Channel levels to set: channel number (from 1) and level. */
export type LevelChanges = ReadonlyMap<number, number>;

/** What a fixture is made of, for playing it. */
interface FixtureParts {
  /** Channels (numbers from 1) of its dimmers. */
  readonly dimmers: readonly number[];
  /** For each cell: its color channels with their letters. */
  readonly cells: readonly { readonly letter: string; readonly channel: number }[][];
  readonly device: boolean;
}

function fixtureParts(light: StageLight): FixtureParts {
  const parts = partsOf(light.channels);
  const at = (offset: number) => light.address + offset;
  return {
    dimmers: [...parts.masters, ...parts.cells.flatMap((cell) => cell.dimmers)].map(at),
    cells: parts.cells.map((cell) => cell.colors.map((color) => ({ letter: color.letter, channel: at(color.offset) }))),
    device: isDevice(light.channels),
  };
}

/**
 * The intensity of a fixture, 0 to 255: its dimmer; without one, its strongest color; for a
 * device, its first channel. `levels` are the levels the faders show (channel 1 at 0).
 */
export function intensityOf(light: StageLight, levels: readonly number[]): number {
  const parts = fixtureParts(light);
  const level = (channel: number) => levels[channel - 1] ?? 0;
  if (parts.device) return level(light.address);
  if (parts.dimmers.length > 0) return Math.max(...parts.dimmers.map(level));
  const colors = parts.cells.flat();
  return colors.length > 0 ? Math.max(...colors.map((color) => level(color.channel))) : 0;
}

/**
 * The channels to set for a fixture at another intensity: its dimmers (with its shutter opened
 * when it was closed: a moving head that is brought up must also give light); without a dimmer,
 * its colors, which keep their mix (from dark: the color it had last, or white); for a device,
 * its first channel.
 */
export function intensityChanges(
  light: StageLight,
  type: FixtureType | undefined,
  level: number,
  levels: readonly number[],
  lastColor: readonly [number, number, number] | null,
): LevelChanges {
  const changes = new Map<number, number>();
  const parts = fixtureParts(light);
  const current = (channel: number) => levels[channel - 1] ?? 0;
  if (parts.device) {
    changes.set(light.address, level);
    return changes;
  }

  if (parts.dimmers.length > 0) {
    for (const channel of parts.dimmers) changes.set(channel, level);
    if (level > 0) openShutter(light, type, levels, changes);
    return changes;
  }

  const colors = parts.cells.flat();
  const strongest = Math.max(0, ...colors.map((color) => current(color.channel)));
  if (strongest > 0) {
    for (const color of colors) changes.set(color.channel, Math.round((current(color.channel) * level) / strongest));
  } else {
    for (const [channel, value] of colorChanges(light, lastColor ?? [255, 255, 255], level)) changes.set(channel, value);
  }
  if (level > 0) openShutter(light, type, levels, changes);
  return changes;
}

/**
 * The color channels of a fixture for a color (red, green and blue, 0 to 255) at an
 * intensity: every cell the same color, its strongest channel at `intensity`. A white
 * channel takes the white part of the color, an amber one the warm part that is left.
 */
export function colorChanges(light: StageLight, color: readonly [number, number, number], intensity: number): LevelChanges {
  const changes = new Map<number, number>();
  for (const cell of fixtureParts(light).cells) {
    const has = (letter: string) => cell.some((part) => part.letter === letter);
    let [red, green, blue] = color;
    let white = 0;
    let amber = 0;
    if (has('W')) {
      white = Math.min(red, green, blue);
      red -= white;
      green -= white;
      blue -= white;
    }
    if (has('A') && red > 0 && green > 0 && blue === 0) {
      // amber is red with about a third of green: the part of the color that is amber goes to it
      amber = Math.min(red, green / 0.35);
      red -= amber;
      green -= amber * 0.35;
    }
    const wanted: Record<string, number> = { R: red, G: green, B: blue, W: white, A: amber, U: 0 };
    const strongest = Math.max(...cell.map((part) => wanted[part.letter] ?? 0));
    const scale = strongest > 0 ? intensity / strongest : 0;
    for (const part of cell) changes.set(part.channel, Math.round(Math.max(0, wanted[part.letter] ?? 0) * scale));
  }
  return changes;
}

/** The color a fixture mixes now, as red, green and blue at full; null for one without color channels or one that is dark. */
export function mixedColor(light: StageLight, levels: readonly number[]): [number, number, number] | null {
  const cell = fixtureParts(light).cells[0];
  if (!cell) return null;
  let red = 0;
  let green = 0;
  let blue = 0;
  const add = (part: readonly [number, number, number], level: number) => {
    red += part[0] * level;
    green += part[1] * level;
    blue += part[2] * level;
  };
  const LETTER_RGB: Record<string, readonly [number, number, number]> = {
    R: [1, 0, 0],
    G: [0, 1, 0],
    B: [0, 0, 1],
    W: [1, 1, 1],
    A: [1, 0.35, 0],
    U: [0.35, 0, 1],
  };
  for (const part of cell) add(LETTER_RGB[part.letter] ?? [0, 0, 0], levels[part.channel - 1] ?? 0);
  const strongest = Math.max(red, green, blue);
  if (strongest <= 0) return null;
  return [Math.round((red / strongest) * 255), Math.round((green / strongest) * 255), Math.round((blue / strongest) * 255)];
}

/** True for a fixture that mixes its color from color channels. */
export function mixesColor(light: StageLight): boolean {
  return fixtureParts(light).cells.length > 0;
}

/** True for a fixture with a dimmer channel. */
export function hasDimmer(light: StageLight): boolean {
  return fixtureParts(light).dimmers.length > 0;
}

/** The shutter of a fixture opened, if it is closed now; nothing for one that is open or has none. */
export function shutterOpening(light: StageLight, type: FixtureType | undefined, levels: readonly number[]): LevelChanges {
  const changes = new Map<number, number>();
  openShutter(light, type, levels, changes);
  return changes;
}

// A shutter that is closed opens: at the first value of its first range called "Open".
function openShutter(light: StageLight, type: FixtureType | undefined, levels: readonly number[], changes: Map<number, number>): void {
  type?.channels.forEach((channel, offset) => {
    if (channel.code !== 'S' || !channel.ranges?.length) return;
    const number = light.address + offset;
    const range = rangeAt(channel.ranges, levels[number - 1] ?? 0);
    if (range && !/^closed/i.test(range.label)) return;
    const open = channel.ranges.find((candidate) => /^open/i.test(candidate.label));
    if (open) changes.set(number, open.from);
  });
}

/** The range a value lies in; undefined if none. */
export function rangeAt(ranges: readonly FixtureRange[], value: number): FixtureRange | undefined {
  return ranges.find((range) => value >= range.from && value <= range.to);
}

/** The values of a range as people say them: "32 to 63", or "32" for a range of one value. */
export function valuesOf(range: FixtureRange): string {
  return range.from === range.to ? `${range.from}` : `${range.from} to ${range.to}`;
}

/** A range with its values, as the lists of the panels show it: "Open (32 to 63)". */
export function rangeText(range: FixtureRange): string {
  return `${range.label} (${valuesOf(range)})`;
}

// ---------------------------------------------------------------- moving heads

/** Where a moving head's pan and tilt are: their channels (numbers from 1), with the fine ones. */
export interface PanTilt {
  readonly pan: number;
  readonly panFine: number | null;
  readonly tilt: number;
  readonly tiltFine: number | null;
}

/** The pan and tilt of a fixture; null for one that has neither. */
export function panTiltOf(light: StageLight, type: FixtureType | undefined): PanTilt | null {
  const pan = light.channels.indexOf('P');
  const tilt = light.channels.indexOf('T');
  if (pan < 0 || tilt < 0) return null;
  const fineOf = (coarse: number) => {
    const fine = type?.channels.findIndex((channel) => channel.code === 'F' && channel.fine === coarse) ?? -1;
    return fine >= 0 ? light.address + fine : null;
  };
  return { pan: light.address + pan, panFine: fineOf(pan), tilt: light.address + tilt, tiltFine: fineOf(tilt) };
}

/** Where a head points, as pan and tilt from 0 to 1, with the fine channels. */
export function positionOf(axes: PanTilt, levels: readonly number[]): { x: number; y: number } {
  const value = (coarse: number, fine: number | null) =>
    fine === null ? (levels[coarse - 1] ?? 0) / 255 : ((levels[coarse - 1] ?? 0) * 256 + (levels[fine - 1] ?? 0)) / 65535;
  return { x: value(axes.pan, axes.panFine), y: value(axes.tilt, axes.tiltFine) };
}

/** The channels to set for a head to point at pan `x` and tilt `y`, each from 0 to 1. */
export function positionChanges(axes: PanTilt, x: number, y: number): LevelChanges {
  const changes = new Map<number, number>();
  const put = (coarse: number, fine: number | null, value: number) => {
    const kept = Math.min(1, Math.max(0, value));
    if (fine === null) {
      changes.set(coarse, Math.round(kept * 255));
      return;
    }
    const whole = Math.round(kept * 65535);
    changes.set(coarse, Math.floor(whole / 256));
    changes.set(fine, whole % 256);
  };
  put(axes.pan, axes.panFine, x);
  put(axes.tilt, axes.tiltFine, y);
  return changes;
}

/** A channel of a fixture that does several things, with what it does now. */
export interface ChoiceChannel {
  readonly channel: number;
  readonly name: string;
  readonly ranges: readonly FixtureRange[];
}

/** The channels of a fixture from the library that do several things (wheels, a shutter, modes). */
export function choiceChannels(light: StageLight, type: FixtureType | undefined): ChoiceChannel[] {
  return (type?.channels ?? []).flatMap((channel, offset) =>
    (channel.ranges?.length ?? 0) > 1 ? [{ channel: light.address + offset, name: channel.name, ranges: channel.ranges ?? [] }] : [],
  );
}

/** The colors of a fixture's color wheel: each slot that has one, with the channel and the value that bring it. */
export function wheelColors(light: StageLight, type: FixtureType | undefined): { channel: number; value: number; color: string; label: string }[] {
  return (type?.channels ?? []).flatMap((channel, offset) =>
    (channel.ranges ?? []).flatMap((range) =>
      range.color ? [{ channel: light.address + offset, value: range.from, color: range.color, label: range.label }] : [],
    ),
  );
}

/** The color a fixture's color wheel shows now; null without one, or on a slot without a color. */
export function wheelColorOf(light: StageLight, type: FixtureType | undefined, levels: readonly number[]): string | null {
  for (const [offset, channel] of (type?.channels ?? []).entries()) {
    if (!channel.ranges?.some((range) => range.color)) continue;
    const range = rangeAt(channel.ranges, levels[light.address - 1 + offset] ?? 0);
    if (range?.color) return range.color;
  }
  return null;
}

// ---------------------------------------------------------------- colors

/** "#ff2000" as red, green and blue from 0 to 255. */
export function rgbOfHex(hex: string): [number, number, number] {
  const part = (start: number) => parseInt(hex.slice(start, start + 2), 16) || 0;
  return [part(1), part(3), part(5)];
}

/** Red, green and blue from 0 to 255 as "#ff2000". */
export function hexOf(rgb: readonly [number, number, number]): string {
  return `#${rgb.map((part) => Math.round(Math.min(255, Math.max(0, part))).toString(16).padStart(2, '0')).join('')}`;
}

/** The hue (0 to 360) and the saturation (0 to 1) of a color. */
export function hsvOf(rgb: readonly [number, number, number]): [number, number] {
  const [red, green, blue] = rgb.map((part) => part / 255);
  const strongest = Math.max(red, green, blue);
  const weakest = Math.min(red, green, blue);
  const range = strongest - weakest;
  let hue = 0;
  if (range > 0) {
    if (strongest === red) hue = 60 * (((green - blue) / range + 6) % 6);
    else if (strongest === green) hue = 60 * ((blue - red) / range + 2);
    else hue = 60 * ((red - green) / range + 4);
  }
  return [hue, strongest > 0 ? range / strongest : 0];
}

/** The color of a hue (0 to 360) at a saturation (0 to 1), at full, as red, green and blue from 0 to 255. */
export function rgbOfHsv(hue: number, saturation: number): [number, number, number] {
  const part = (shift: number) => {
    const k = (shift + hue / 60) % 6;
    return Math.round(255 * (1 - saturation * Math.max(0, Math.min(k, 4 - k, 1))));
  };
  return [part(5), part(3), part(1)];
}

/**
 * The color a fixture's strip shows: the color it mixes, the slot of its color wheel, or its
 * own color (a gel; warm white without one); null for a device, and for a fixture that mixes its
 * color and is dark.
 */
export function swatchOf(light: StageLight, type: FixtureType | undefined, levels: readonly number[]): string | null {
  if (isDevice(light.channels)) return null;
  const mixed = mixedColor(light, levels);
  if (mixed) return hexOf(mixed);
  const wheel = wheelColorOf(light, type, levels);
  if (wheel) return wheel;
  return mixesColor(light) ? null : (light.color ?? '#ffd9a6');
}

/** True when a fixture's shutter is closed now: it gives no light whatever its dimmer says. */
export function shutterClosed(light: StageLight, type: FixtureType | undefined, levels: readonly number[]): boolean {
  return (type?.channels ?? []).some((channel, offset) => {
    if (channel.code !== 'S' || !channel.ranges?.length) return false;
    const range = rangeAt(channel.ranges, levels[light.address - 1 + offset] ?? 0);
    return range !== undefined && /^closed/i.test(range.label);
  });
}

// ---------------------------------------------------------------- strobe

/** How a light's strobe runs now, from the value of its strobe channel (S). */
export interface Strobe {
  /** A strobe flashes; a pulse swells and fades; a ramp rises (or falls) and starts again. */
  readonly kind: 'strobe' | 'pulse' | 'ramp-up' | 'ramp-down';
  /** True for a strobe at random moments. */
  readonly random: boolean;
  /** Flashes a second, when its light says them (in Hz); null when it says only "slow to fast". */
  readonly hertz: number | null;
  /** How fast it is within its range: 0 the slowest, 1 the fastest. */
  readonly pace: number;
  /** What it is, in words: "Strobe", "Random strobe", "Pulse". */
  readonly label: string;
}

/**
 * A strobe channel that says nothing of its values (a light of letters, or one whose library gives
 * it a single range): as most simple lights have it, steady light at its first values, then a
 * strobe from slow to fast.
 */
export const PLAIN_STROBE_FROM = 10;

/**
 * How the light's strobe runs now; null while it gives steady light (or none). A light of a library
 * says it in the range its strobe channel stands in: its kind ("Pulse", "Ramp up", "Random strobe"),
 * and how fast, in Hz ("Strobe, 0.3Hz to 19Hz", at the place of the value in the range) or only as
 * "slow to fast".
 */
export function strobeOf(light: StageLight, type: FixtureType | undefined, levels: readonly number[]): Strobe | null {
  const channels = [...light.channels].flatMap((letter, offset) => (letter === 'S' ? [{ offset, ranges: type?.channels[offset]?.ranges ?? [] }] : []));
  // A light whose shutter says its ranges goes by those: another S channel of it (a strobe speed) says no more.
  const ranged = channels.filter((channel) => channel.ranges.length > 1);
  for (const channel of ranged.length > 0 ? ranged : channels) {
    const value = levels[light.address - 1 + channel.offset] ?? 0;
    const strobe = channel.ranges.length > 1 ? strobeInRange(rangeAt(channel.ranges, value), value) : plainStrobe(value);
    if (strobe) return strobe;
  }
  return null;
}

function plainStrobe(value: number): Strobe | null {
  if (value < PLAIN_STROBE_FROM) return null;
  return { kind: 'strobe', random: false, hertz: null, pace: (value - PLAIN_STROBE_FROM) / (255 - PLAIN_STROBE_FROM), label: 'Strobe' };
}

const STROBE_NAMES: Readonly<Record<Strobe['kind'], string>> = { strobe: 'Strobe', pulse: 'Pulse', 'ramp-up': 'Ramp up', 'ramp-down': 'Ramp down' };

function strobeInRange(range: FixtureRange | undefined, value: number): Strobe | null {
  if (!range || /^(open|closed)\b/i.test(range.label)) return null;
  const label = range.label;
  const kind: Strobe['kind'] | null =
    /ramp\s*-?up/i.test(label) ? 'ramp-up'
    : /ramp\s*-?down/i.test(label) ? 'ramp-down'
    : /pulse/i.test(label) ? 'pulse'
    : /strob|flash|lightning|burst|spike/i.test(label) ? 'strobe'
    : null;
  if (kind === null) return null;

  const place = range.to > range.from ? (value - range.from) / (range.to - range.from) : 0.5;
  const pace = /fast\s*(to|-|->|→)\s*slow/i.test(label) ? 1 - place : place;
  const number = '(\\d+(?:[.,]\\d+)?)\\s*hz';
  const between = new RegExp(`${number}\\s*(?:to|-|->|→|–)\\s*${number}`, 'i').exec(label);
  const one = new RegExp(number, 'i').exec(label);
  const hz = (text: string) => Number(text.replace(',', '.'));
  const hertz = between ? hz(between[1]) + (hz(between[2]) - hz(between[1])) * place : one ? hz(one[1]) : null;
  const random = /random/i.test(label);
  return {
    kind,
    random,
    hertz: hertz === null || !Number.isFinite(hertz) ? null : Math.round(hertz * 10) / 10,
    pace,
    label: random ? `Random ${STROBE_NAMES[kind].toLowerCase()}` : STROBE_NAMES[kind],
  };
}

/** The most flashes a second a screen may show: more can set off a seizure (WCAG 2.3.1). */
export const MAX_SHOWN_FLASHES = 3;

/**
 * How many flashes a second the screen shows for a strobe: as many as the light gives up to 2 a
 * second, and above that fewer and fewer of them, so that a faster strobe still looks faster, but
 * never more than three. Without Hz, the place in its range stands for 1 to 20 a second, as most
 * strobes go.
 */
export function shownFlashes(strobe: Strobe): number {
  const real = strobe.hertz ?? 1 + strobe.pace * 19;
  if (real <= 2) return Math.max(0.25, real);
  return Math.min(MAX_SHOWN_FLASHES, 2 + (Math.min(real, 20) - 2) / 18);
}
