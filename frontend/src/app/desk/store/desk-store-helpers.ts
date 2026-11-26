import { Playback, StackStep } from '../desk-types';
import { EffectPlayback } from '../../effects/effects';

// What the layers of DeskStore share: its constants, two of its types and small helpers.
/**
 * While a sequence is recorded: for every channel, memory, fixture and group it uses, the
 * numbers of the steps that use it, counted from 1. The keys show them.
 */
export interface RecordedSteps {
  readonly channels: ReadonlyMap<number, readonly number[]>;
  readonly memories: ReadonlyMap<number, readonly number[]>;
  readonly fixtures: ReadonlyMap<number, readonly number[]>;
  readonly groups: ReadonlyMap<number, readonly number[]>;
}

/** Where the stack stands, see DeskStore.stackPosition. */
export interface StackPosition {
  readonly previous: StackStep | null;
  readonly current: StackStep | null;
  readonly next: StackStep | null;
}

/** Fader moves are collected for this long and then sent together, the newest value of each fader only. */
export const SEND_DELAY_MS = 15;

/** How long a notice stays in the top bar. */
export const NOTICE_MS = 5000;

/** How long the notice stays that unsaved work was brought back: longer, it matters. */
export const RECOVERED_NOTICE_MS = 15000;

/** Changes of one effect in the editor are collected for this long and then stored together. */
export const EFFECT_SEND_DELAY_MS = 150;

/** A fader or live value of an effect that the page moved keeps the page's value for this long. */
export const EFFECT_TOUCH_MS = 600;

/**
 * A sound that keeps changing (a slider of the editor dragged) goes to the desk program at once and
 * then at most this often, the newest each time, so that what is heard follows the slider.
 */
export const SOUND_SEND_MS = 60;

/** Nothing runs: no sequence gives anything, and no step of the stack is on. */
export const NO_PLAYBACK: Playback = {
  levels: [],
  sequenceSteps: {},
  stack: { currentId: null, nextId: null, state: 'idle', paused: false, remaining: 0 },
};

/** The playback of an effect that has never run: out, its fader at zero. */
export function emptyPlayback(number: number): EffectPlayback {
  return {
    number,
    running: false,
    level: 0,
    speed: 30,
    size: 100,
    spread: 100,
    fading: false,
    phase: 0,
    bpm: 30,
    changed: false,
  };
}

export function zeros(count: number): number[] {
  return new Array<number>(count).fill(0);
}

/** The levels of the channel faders with those of `faders` (numbers from 1) at 0: they give their light through the crossfade. */
export function withoutChannels(levels: readonly number[], faders: readonly number[]): number[] {
  const copy = levels.slice();
  for (const fader of faders) if (fader >= 1 && fader <= copy.length) copy[fader - 1] = 0;
  return copy;
}

/** The channels a look holds up, as indexes from 0; none for a memory without a look. */
export function lookChannels(look: readonly number[] | undefined): number[] {
  return (look ?? []).flatMap((level, index) => (level > 0 ? [index] : []));
}

/** 1, 2, 3 ... count: where the independent keys start, key 1 on channel or memory 1, and so on. */
export function oneToCount(count: number): number[] {
  return Array.from({ length: count }, (_, index) => index + 1);
}

export function noNotes(count: number): string[] {
  return new Array<string>(count).fill('');
}

export function allFalse(count: number): boolean[] {
  return new Array<boolean>(count).fill(false);
}

/** A copy of `list` with one item replaced. Signals need a new list to notice the change. */
export function withItem<T>(list: readonly T[], index: number, value: T): T[] {
  const copy = list.slice();
  copy[index] = value;
  return copy;
}
