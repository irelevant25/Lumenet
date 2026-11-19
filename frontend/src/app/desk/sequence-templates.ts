import { MAX_LEVEL, MAX_SEQUENCE_STEPS, MAX_STEP_SECONDS, StepTarget, TimedStep } from './desk-types';

/**
 * Templates for sequences: a few clicks instead of dozens of fields. A template writes ordinary
 * steps, which can be changed in the table afterwards like any others. Each one is made for the
 * channels (or memories) it is given, in their order.
 */
export type TemplateName = 'fire' | 'chase' | 'wave' | 'strobe' | 'lightning';

/** What the owner chooses for a template. */
export interface TemplateSettings {
  readonly name: TemplateName;
  readonly target: StepTarget;
  /** The channels or memories, in the order the template goes through them. */
  readonly numbers: readonly number[];
  /** The lowest value the template uses, where it uses one (see TemplateInfo.usesLow). */
  readonly low: number;
  /** The highest value: how bright the lights get. */
  readonly high: number;
  /** The template's own time in seconds; TemplateInfo.timeLabel says what it means. */
  readonly time: number;
}

/** A template as the window that offers them shows it. */
export interface TemplateInfo {
  readonly name: TemplateName;
  /** The word on its key. */
  readonly label: string;
  /** One sentence: what it does. */
  readonly about: string;
  readonly usesLow: boolean;
  /** What its time is. */
  readonly timeLabel: string;
  readonly low: number;
  readonly high: number;
  readonly time: number;
}

export const TEMPLATES: readonly TemplateInfo[] = [
  {
    name: 'fire',
    label: 'Fire',
    about: 'Flickers like flames: every light on its own, between Low and High, in a new random order on every round.',
    usesLow: true,
    timeLabel: 'Seconds between flickers',
    low: 90,
    high: 255,
    time: 0.2,
  },
  {
    name: 'chase',
    label: 'Chase',
    about: 'The lights come on one after the other, each for a moment, round and round.',
    usesLow: false,
    timeLabel: 'Seconds each light is on',
    low: 0,
    high: 255,
    time: 0.5,
  },
  {
    name: 'wave',
    label: 'Wave',
    about: 'Each light swells and fades in turn; a single light breathes. For several lights at once, pick a memory that holds them.',
    usesLow: false,
    timeLabel: 'Seconds up, and as many down',
    low: 0,
    high: 255,
    time: 1,
  },
  {
    name: 'strobe',
    label: 'Strobe',
    about: 'Short flashes, light after light. For several lights at once, pick a memory that holds them.',
    usesLow: false,
    timeLabel: 'Seconds between flashes',
    low: 0,
    high: 255,
    time: 0.3,
  },
  {
    name: 'lightning',
    label: 'Storm',
    about: 'Lightning: strikes and faint flickers out of the dark, at random, with pauses between.',
    usesLow: true,
    timeLabel: 'Seconds of a pause, about',
    low: 60,
    high: 255,
    time: 3,
  },
];

/** What a template makes: the steps, and how the sequence is played. */
export interface MadeSequence {
  readonly steps: TimedStep[];
  readonly loop: boolean;
  readonly random: boolean;
}

/** The most steps per light the fire template makes; fewer when many lights share the 99 steps. */
const FIRE_STEPS_PER_LIGHT = 8;

/** How many steps a template needs for this many lights. More than 99 does not fit into a sequence. */
export function stepsNeeded(name: TemplateName, lights: number): number {
  switch (name) {
    case 'fire':
      return lights * Math.max(2, Math.min(FIRE_STEPS_PER_LIGHT, Math.floor(MAX_SEQUENCE_STEPS / Math.max(1, lights))));
    case 'chase':
      return lights;
    case 'wave':
      return lights === 1 ? 2 : lights;
    case 'strobe':
      return lights * 2;
    case 'lightning':
      return 3 + lights * 3;
  }
}

/**
 * Makes the steps of a template. `random` gives numbers from 0 to below 1, like Math.random;
 * the tests pass one that gives the same numbers every time.
 */
export function makeSequence(settings: TemplateSettings, random: () => number = Math.random): MadeSequence {
  const { target, numbers, low, high, time } = settings;
  const step = (number: number, value: number, up: number, wait: number, down: number): TimedStep => ({
    target,
    number,
    value: Math.round(Math.min(MAX_LEVEL, Math.max(0, value))),
    up: tenths(up),
    wait: tenths(wait),
    down: tenths(down),
  });

  switch (settings.name) {
    case 'fire': {
      // Every light gets values between Low and High, now and then a dip below Low, with short
      // and uneven times. Down is longer than Up, so that each value grows out of the one
      // before instead of falling to dark in between. Played at random, the lights flicker
      // each at its own pace.
      const perLight = stepsNeeded('fire', numbers.length) / numbers.length;
      const steps: TimedStep[] = [];
      for (let round = 0; round < perLight; round++) {
        for (const number of numbers) {
          const dip = random() < 0.15;
          const value = dip ? low * (0.4 + random() * 0.3) : low + random() * (high - low);
          steps.push(step(number, value, random() * time * 0.5, random() * time, time * (1.5 + random() * 1.5)));
        }
      }
      return { steps, loop: true, random: true };
    }

    case 'chase':
      // One light after the other, each for the time, with a short tail when it goes.
      return {
        steps: numbers.map((number) => step(number, high, 0, time, Math.min(0.3, time / 2))),
        loop: true,
        random: false,
      };

    case 'wave': {
      // Each light swells over the time and fades over the same time while the next swells.
      // A single light gets a dark step after it, so that it fades all the way and breathes.
      const steps = numbers.map((number) => step(number, high, time, 0, time));
      if (numbers.length === 1) steps.push(step(numbers[0], 0, 0, time, 0));
      return { steps, loop: true, random: false };
    }

    case 'strobe': {
      // A flash that is as short as a step can be, then dark until the next one.
      const steps: TimedStep[] = [];
      for (const number of numbers) {
        steps.push(step(number, high, 0, 0, 0));
        steps.push(step(number, 0, 0, Math.max(0, time - 0.05), 0));
      }
      return { steps, loop: true, random: false };
    }

    case 'lightning': {
      // Dark pauses of different lengths, and for every light two strikes and a faint flicker.
      // Played at random, they come in a new order every round: now a double strike, now a
      // long dark wait.
      const steps: TimedStep[] = [];
      for (let pause = 0; pause < 3; pause++) {
        steps.push(step(numbers[0], 0, 0, time * (0.5 + random()), 0));
      }
      for (const number of numbers) {
        steps.push(step(number, high, 0, 0, 0.1 + random() * 0.2));
        steps.push(step(number, high * (0.8 + random() * 0.2), 0, 0, 0.1 + random() * 0.3));
        steps.push(step(number, low + random() * Math.max(0, (high - low) / 2), 0, 0.1, 0.3));
      }
      return { steps, loop: true, random: true };
    }
  }
}

/**
 * The numbers typed for a template: "1, 2", "1-4" or "1-4, 9", each from 1 to `highest`.
 * Gives the numbers in the order typed, each once, or a sentence that says what is wrong.
 */
export function readNumbers(text: string, highest: number): number[] | string {
  const numbers: number[] = [];
  const parts = text.split(',').map((part) => part.trim()).filter((part) => part !== '');
  if (parts.length === 0) return 'Type the numbers of the lights, for example 1, 2 or 1-4.';

  for (const part of parts) {
    const range = /^(\d+)\s*-\s*(\d+)$/.exec(part);
    const single = /^\d+$/.test(part) ? Number(part) : null;
    if (!range && single === null) return `"${part}" is no number. Write them like 1, 2 or 1-4.`;

    const from = range ? Number(range[1]) : single!;
    const to = range ? Number(range[2]) : single!;
    if (from < 1 || to < 1 || from > highest || to > highest) return `The numbers go from 1 to ${highest}.`;

    const direction = from <= to ? 1 : -1;
    for (let number = from; number !== to + direction; number += direction) {
      if (!numbers.includes(number)) numbers.push(number);
    }
  }
  return numbers;
}

/** A time on tenths of a second, within what a step allows. */
function tenths(seconds: number): number {
  return Math.min(MAX_STEP_SECONDS, Math.max(0, Math.round(seconds * 10) / 10));
}
