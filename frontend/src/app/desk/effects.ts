import { LampLevel } from './lamps';

/**
 * Effects: shapes the desk program works out anew for every frame, on lights of the stage plan, in
 * place of stored steps (a circle of the heads, a sine through a row of lights, a rainbow, bars
 * over the cells of LED bars). The desk program keeps them with the project and plays them; the
 * page shows and changes them, and plays their playbacks. See Desk.Effects.cs for how an effect
 * mixes with the faders, memories, sequences and the stack.
 */

/** The most effects a project can have. */
export const MAX_EFFECTS = 64;
/** The effect keys of the independents the desk keeps (Settings says how many the page shows). */
export const EFFECT_INDEPENDENT_COUNT = 32;
/** The speed of an effect, in cycles a minute (BPM). */
export const MIN_EFFECT_SPEED = 0.5;
export const MAX_EFFECT_SPEED = 600;
/** The longest fade in or out, in seconds. */
export const MAX_EFFECT_FADE = 999.9;
/** The most colors a chase, a fade or a pattern can have. */
export const MAX_EFFECT_COLORS = 8;
/** The longest name. */
export const EFFECT_NAME_LENGTH = 24;

export type EffectKind = 'movement' | 'intensity' | 'color' | 'pixel';
export type EffectDirection = 'forward' | 'back';
export type EffectOrder = 'address' | 'left' | 'center' | 'random';

/** An effect as the desk program keeps it (Effect.cs). */
export interface Effect {
  /** 1 to 64: keys, steps of the stack and the outside name it by this. */
  number: number;
  name: string;
  kind: EffectKind;
  /** One of the shapes of its kind, see EFFECT_SHAPES. */
  shape: string;
  /** The group of the plan it works on; null (or missing) when it names its lights. */
  group?: number | null;
  /** The lights of the plan it works on, by their numbers, when it has no group. */
  lights: number[];
  /** Cycles a minute (BPM). */
  speed: number;
  /** 0 to 100: how big a movement, how deep a wave, how deep the colors, how big a pattern. */
  size: number;
  /** 0 to 100: 0 all lights together, 100 evenly spread over them. */
  spread: number;
  direction: EffectDirection;
  order: EffectOrder;
  /** Lights next to each other that move as one. */
  blocks: number;
  /** Parts of the run, every second one the other way round. */
  wings: number;
  fadeIn: number;
  fadeOut: number;
  /** A pulse: on for this share of a cycle, in per cent; bars: how wide they are. */
  duty: number;
  /** "#ff2000" each; empty for the usual ones. */
  colors: string[];
  /** A movement around a center of its own: pan and tilt in per cent; null to move around where a head points. */
  centerPan?: number | null;
  centerTilt?: number | null;
  /** Degrees: the way of a line, of a gradient, of bars or a rainbow over the plan. 0 to the right, 90 to the front. */
  angle: number;
  /** Where a ripple starts, squares from the left and from the back of the stage; null for the middle of its lights. */
  pointX?: number | null;
  pointY?: number | null;
  /** Chooses the random order and the random shapes. */
  seed: number;
}

/** The playback of an effect: whether it runs, its fader, its live values. None of it is stored. */
export interface EffectPlayback {
  number: number;
  /** From its start until its fader is down at zero, and while a sequence plays it. */
  running: boolean;
  /** Its fader, 0 to 255. */
  level: number;
  /**
   * How strongly the sequences play it, 0 to 255; it leaves the fader alone, and the higher of the
   * two counts. Missing from a desk program from before sequences could play effects.
   */
  played?: number;
  /** Its live speed (BPM), size and spread (per cent). */
  speed: number;
  size: number;
  spread: number;
  /** True while its key moves its fader up or down, in the effect's fade times. */
  fading: boolean;
  /** Cycles since it started. */
  phase: number;
  /** The speed it really runs at: its own, times the tempo over 120 while RATE is lit. */
  bpm: number;
  /** True while its live values differ from the effect's own. */
  changed: boolean;
}

/** What the running effects give the channels they touch, as the desk program mixes it (before a solo, the limits, the master and blackout). */
export interface EffectLevels {
  /** Channels, counted from 1 through the universes. */
  channels: number[];
  levels: number[];
}

/** One cycle of an effect, sampled for the preview. */
export interface EffectPreview {
  kind: EffectKind;
  frames: number;
  items: EffectPreviewItem[];
}

/** A light, or a pixel (a cell of a light), of a preview. */
export interface EffectPreviewItem {
  light: number;
  /** For a pixel its cell, from 0; -1 for a light. */
  cell: number;
  /** Where it is on the plan, in squares. */
  x: number;
  y: number;
  /** A movement: pan and tilt at each moment, 0 to 1. */
  pan: number[];
  tilt: number[];
  /** An intensity or a pixel: how bright, 0 to 1. */
  levels: number[];
  /** A color or a pixel: "#ff2000" at each moment. */
  colors: string[];
}

/** A control of a playback, as SetEffectControl takes it: the keys, the fader, the live values. */
export type EffectControl = 'go' | 'toggle' | 'level' | 'speed' | 'size' | 'spread' | 'reset';

/**
 * The key of an effect: 1 where there is no effect, 2 for an effect that does not run, 3 while it
 * runs. It blinks while the key moves its fader up or down.
 */
export function effectLamp(
  effect: Effect | undefined,
  playback: EffectPlayback | undefined,
): LampLevel {
  if (!effect) return 1;
  return playback?.running ? 3 : 2;
}

/**
 * What the faders show, with what the running effects give in place of the channels they touch: the
 * desk program mixes those with the rest, so their level is taken as it comes.
 */
export function withEffectLevels(
  shown: number[],
  effects: EffectLevels | null | undefined,
): number[] {
  if (!effects) return shown;
  for (let index = 0; index < effects.channels.length; index++) {
    const channel = effects.channels[index] - 1;
    if (channel >= 0 && channel < shown.length) shown[channel] = effects.levels[index];
  }
  return shown;
}
