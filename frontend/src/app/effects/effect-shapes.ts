import type { StageLight, StagePlan } from '../desk/desk-types';
import { Effect, EffectKind, EffectOrder, MAX_EFFECT_SPEED, MIN_EFFECT_SPEED } from './effects';

/**
 * The shapes of the effects and what the editor and the views say about them: the kinds and shapes
 * on offer, a new effect, which lights fit a kind, the scale of the live speed fader, and how often
 * an effect flashes. Apart from effects.ts so that only the views that need it load it.
 */

/** The kinds, in the order the editor offers them. */
export const EFFECT_KINDS: readonly { readonly kind: EffectKind; readonly label: string }[] = [
  { kind: 'movement', label: 'Movement' },
  { kind: 'intensity', label: 'Intensity' },
  { kind: 'color', label: 'Color' },
  { kind: 'pixel', label: 'Pixel' },
];

/** The shapes of each kind, in the order the editor offers them. */
export const EFFECT_SHAPES: Readonly<
  Record<EffectKind, readonly { readonly shape: string; readonly label: string }[]>
> = {
  movement: [
    { shape: 'circle', label: 'Circle' },
    { shape: 'eight', label: 'Figure 8' },
    { shape: 'pan', label: 'Pan swing' },
    { shape: 'tilt', label: 'Tilt swing' },
    { shape: 'line', label: 'Line' },
    { shape: 'random', label: 'Random' },
  ],
  intensity: [
    { shape: 'sine', label: 'Sine' },
    { shape: 'rampup', label: 'Ramp up' },
    { shape: 'rampdown', label: 'Ramp down' },
    { shape: 'pulse', label: 'Pulse' },
    { shape: 'random', label: 'Flicker' },
  ],
  color: [
    { shape: 'rainbow', label: 'Rainbow' },
    { shape: 'chase', label: 'Chase' },
    { shape: 'fade', label: 'Fade' },
  ],
  pixel: [
    { shape: 'gradient', label: 'Gradient' },
    { shape: 'ripple', label: 'Ripple' },
    { shape: 'plasma', label: 'Plasma' },
    { shape: 'rainbow', label: 'Rainbow' },
    { shape: 'bars', label: 'Bars' },
    { shape: 'sparkle', label: 'Sparkle' },
  ],
};

export const EFFECT_ORDERS: readonly { readonly order: EffectOrder; readonly label: string }[] = [
  { order: 'address', label: 'By address' },
  { order: 'left', label: 'From the left' },
  { order: 'center', label: 'From the middle out' },
  { order: 'random', label: 'At random' },
];

/** What the editor calls a kind: "Movement". */
export function kindLabel(kind: EffectKind): string {
  return EFFECT_KINDS.find((item) => item.kind === kind)?.label ?? kind;
}

/** What the editor calls a shape: "Figure 8". */
export function shapeLabel(kind: EffectKind, shape: string): string {
  return EFFECT_SHAPES[kind]?.find((item) => item.shape === shape)?.label ?? shape;
}

/** The colors of a new chase, fade or pattern of each kind (empty: the shape's own). */
const NEW_COLORS: Readonly<Record<EffectKind, string[]>> = {
  movement: [],
  intensity: [],
  color: [],
  pixel: ['#ff2000'],
};

/**
 * A new effect of a kind, as the editor makes it: the kind's first shape, at a speed that suits it
 * (a circle every four seconds, a sine every second, a rainbow every six), on every light of the
 * plan that can do what the kind does. A movement goes around a center of its own, the middle of
 * the heads' range, which the preview moves.
 */
export function newEffect(
  number: number,
  kind: EffectKind,
  plan: Pick<StagePlan, 'lights' | 'types'>,
): Effect {
  const shape = EFFECT_SHAPES[kind][0].shape;
  const speeds: Record<EffectKind, number> = { movement: 15, intensity: 60, color: 10, pixel: 30 };
  return {
    number,
    name: shapeLabel(kind, shape),
    kind,
    shape,
    group: null,
    lights: plan.lights.filter((light) => fitsKind(light, kind, plan)).map((light) => light.id),
    speed: speeds[kind],
    size: kind === 'movement' ? 30 : 100,
    spread: kind === 'movement' ? 0 : 100,
    direction: 'forward',
    order: 'address',
    blocks: 1,
    wings: 1,
    fadeIn: kind === 'movement' ? 1 : 0,
    fadeOut: kind === 'movement' ? 1 : 0,
    duty: 50,
    colors: NEW_COLORS[kind],
    centerPan: kind === 'movement' ? 50 : null,
    centerTilt: kind === 'movement' ? 50 : null,
    angle: 0,
    pointX: null,
    pointY: null,
    seed: 0,
  };
}

/**
 * True for a light that can do what an effect of the kind does, as the desk program sees it: a head
 * (a pan or a tilt) for a movement, a light with colors or a color wheel for a color, any light
 * that gives light for an intensity or a pattern. A device (a hazer) takes no part.
 */
export function fitsKind(
  light: StageLight,
  kind: EffectKind,
  plan: Pick<StagePlan, 'types'>,
): boolean {
  const letters = light.channels;
  const has = (pattern: RegExp) => pattern.test(letters);
  if (!has(/[DRGBWAUPT]/)) return false; // a device
  switch (kind) {
    case 'movement':
      return has(/[PT]/);
    case 'color': {
      if (has(/[RGBWAU]/)) return true;
      const type =
        light.type == null ? undefined : plan.types.find((item) => item.id === light.type);
      return !!type?.channels.some((channel) => channel.ranges?.some((range) => range.color));
    }
    default:
      return has(/[DRGBWAU]/);
  }
}

/** The lights of the plan an effect works on: those of its group, or those it names, that are still there. */
export function effectLights(
  effect: Effect,
  plan: Pick<StagePlan, 'lights' | 'groups'>,
): StageLight[] {
  const ids =
    effect.group != null
      ? (plan.groups.find((group) => group.id === effect.group)?.lights ?? [])
      : effect.lights;
  return plan.lights.filter((light) => ids.includes(light.id));
}

/** The live speed fader: 0 to this many places from the slowest to the fastest speed, evenly in octaves. */
export const SPEED_PLACES = 1000;

/** Where a speed (BPM) stands on the live speed fader, 0 to SPEED_PLACES: twice as fast is always the same way further. */
export function speedPlace(bpm: number): number {
  const kept = Math.min(MAX_EFFECT_SPEED, Math.max(MIN_EFFECT_SPEED, bpm));
  return Math.round(
    (Math.log(kept / MIN_EFFECT_SPEED) / Math.log(MAX_EFFECT_SPEED / MIN_EFFECT_SPEED)) *
      SPEED_PLACES,
  );
}

/** The speed (BPM) at a place of the live speed fader, to one decimal; whole numbers from 10 BPM on. */
export function speedAt(place: number): number {
  const share = Math.min(SPEED_PLACES, Math.max(0, place)) / SPEED_PLACES;
  const bpm = MIN_EFFECT_SPEED * Math.pow(MAX_EFFECT_SPEED / MIN_EFFECT_SPEED, share);
  return bpm >= 10 ? Math.round(bpm) : Math.round(bpm * 10) / 10;
}

/** A speed as people read it: "120 BPM", "2.5 BPM". */
export function speedText(bpm: number): string {
  return `${Number.isInteger(bpm) ? bpm : bpm.toFixed(1)} BPM`;
}

/**
 * How many times a second an effect can flash its lights at its speed: an intensity once a cycle,
 * a chase once for each of its colors, a pattern of bars or rings once for each that passes a
 * pixel. The Stage view never shows more than 3 flashes a second (WCAG 2.3.1): it uses this to mark
 * a faster effect and to slow its lights down on the screen. 0 for a movement, which flashes nothing.
 */
export function flashesPerSecond(effect: Effect, bpm: number): number {
  const cycles = bpm / 60;
  switch (effect.kind) {
    case 'intensity':
      return cycles;
    case 'color':
      return effect.shape === 'chase'
        ? cycles * Math.max(2, effect.colors.length)
        : effect.shape === 'fade'
          ? cycles
          : 0;
    case 'pixel': {
      if (effect.shape === 'plasma' || effect.shape === 'rainbow') return 0; // colors that run, without flashes
      const passing = effect.shape === 'sparkle' ? 1 : 100 / Math.max(1, effect.size);
      return cycles * passing;
    }
    default:
      return 0;
  }
}

/** A short sentence about an effect for its header and screen readers: "A movement (circle) on 4 lights at 15 BPM". */
export function effectSummary(effect: Effect, lights: number, bpm = effect.speed): string {
  const kind = kindLabel(effect.kind).toLowerCase();
  const on = lights === 1 ? '1 light' : `${lights} lights`;
  return `${kind === 'intensity' ? 'An' : 'A'} ${kind} (${shapeLabel(effect.kind, effect.shape).toLowerCase()}) on ${on} at ${speedText(bpm)}`;
}
