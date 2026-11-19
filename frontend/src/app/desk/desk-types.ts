/** Sizes of the desk. */
export const CHANNEL_COUNT = 512;
export const MEMORY_COUNT = 128;
export const MAX_LEVEL = 255;
/** The independent keys: each switches one channel between zero and full. */
export const INDEPENDENT_COUNT = 16;

/** How many faders stand in one row: 32 on a wide window, 16 on a narrower one. */
export const STRIPS_PER_ROW_WIDE = 32;
export const STRIPS_PER_ROW_MEDIUM = 16;
/** On a phone a row is shown as lines of this many. The same number is "--line" in styles.css. */
export const STRIPS_PER_LINE_PHONE = 8;

/*
 * The limits of the window sizes, in pixels. They stand only here: the page marks <html> with
 * the classes "medium", "mobile" and "phone" (see desk-store.ts), and the style sheets go by
 * those classes.
 */
/** Narrower than this is "medium": 16 faders in a row instead of 32, and twice as many page keys. */
export const MEDIUM_BELOW_PX = 1850;
/**
 * Narrower or lower than this is "mobile": the sections stand under each other, the page keys
 * lie under the section titles, and the page scrolls.
 */
export const MOBILE_BELOW_WIDTH_PX = 900;
export const MOBILE_BELOW_HEIGHT_PX = 600;
/** Narrower than this is "phone": a row of 16 faders breaks into two lines of 8, and the top bar into three lines. */
export const PHONE_BELOW_PX = 720;

/** The numbers of one row of faders, for example 33, 34, ... 64. */
export function stripNumbers(first: number, count: number): number[] {
  return Array.from({ length: count }, (_, index) => first + index);
}

/** The first number of the page that holds `number`, when a page holds `perPage` of them. */
export function pageStart(number: number, perPage: number): number {
  return Math.floor((number - 1) / perPage) * perPage + 1;
}

/** Everything the desk holds, as the backend sends it. Lists start at 0: index = number - 1. */
export interface DeskState {
  /** Level of each channel fader, 0 to 255. */
  channels: number[];
  /** Level of each memory fader, 0 to 255. */
  memoryLevels: number[];
  /** Whether each memory holds something: a look or a sequence. */
  memoryStored: boolean[];
  /** Whether what a memory holds is a sequence. Missing from a desk program that knows no sequences. */
  memorySequence?: boolean[];
  /** The note under each channel fader; an empty text means none. */
  channelNotes: string[];
  /** The note under each memory fader. */
  memoryNotes: string[];
  /** The channel each independent key switches. */
  independents: number[];
  master: number;
  blackout: boolean;
}

/** What the stored memories hold: for each stored memory, by its number, the level of every channel. */
export type MemoryLooks = Readonly<Record<number, readonly number[]>>;

/** What a step of a sequence or of the stack stands for. */
export type StepTarget = 'channel' | 'memory';

/** The longest time a step can have, in seconds. */
export const MAX_STEP_SECONDS = 999.9;
/** The most steps a sequence can have. */
export const MAX_SEQUENCE_STEPS = 99;

/**
 * One step of a sequence or of the stack: a channel or a memory, the value it is brought to,
 * and three times in seconds. `up` is its way from zero to the value, `wait` how long it then
 * stays, `down` its way back to zero when the next step takes over.
 */
export interface TimedStep {
  target: StepTarget;
  /** The channel (1 to 512) or the memory (1 to 128). */
  number: number;
  /** 0 to 255. */
  value: number;
  up: number;
  wait: number;
  down: number;
}

/** A sequence, stored in a memory: its steps run one after the other while the memory's fader is up. */
export interface Sequence {
  /** True: after the last step it starts again. False: it runs once and stays on the last step. */
  loop: boolean;
  steps: TimedStep[];
}

/** How a new sequence is played until its owner says otherwise: in a loop. The backend starts one the same way. */
export const DEFAULT_LOOP = true;

/** The sequences, by the number of the memory that holds each. */
export type Sequences = Readonly<Record<number, Sequence>>;

/** A step of the stack: it also has a name of its own and its number in the order, like "2.1". */
export interface StackStep extends TimedStep {
  id: number;
  cue: string;
}

/** The one stack of the project: its steps, in the order of their numbers. */
export interface StackState {
  steps: StackStep[];
}

/**
 * Where the stack stands. idle: no step is on. fading: the current step is on its way up.
 * waiting: it is up and the stack goes on by itself when its wait is over. holding: it is up
 * and the stack waits for Play.
 */
export type StackPhase = 'idle' | 'fading' | 'waiting' | 'holding';

export interface StackPlayback {
  currentId: number | null;
  /** The step Play goes to. After the last step that is the first one; null if there is no other step. */
  nextId: number | null;
  state: StackPhase;
  paused: boolean;
  /** Seconds left of the fade or of the wait; 0 otherwise. */
  remaining: number;
  /**
   * True while the stack is live: it runs, and nothing that is stored can be changed. Missing
   * from a desk program that knows no live switch.
   */
  live?: boolean;
}

/** What changes with time, as the backend reports it several times a second while something runs. */
export interface Playback {
  /** What the running sequences give each channel right now (already scaled by their memory faders). */
  levels: number[];
  /** For each memory whose sequence is running: the number of the step it is on, counted from 1. */
  sequenceSteps: Record<number, number>;
  stack: StackPlayback;
}

/** The first step of a new list, and the pattern for the next ones. */
export function firstStep(wait: number): TimedStep {
  return { target: 'channel', number: 1, value: MAX_LEVEL, up: 1, wait, down: 1 };
}

/**
 * The step that recording a sequence puts in for a pressed key: that channel or memory at
 * full, with the times a new sequence starts with.
 */
export function recordedStep(target: StepTarget, number: number): TimedStep {
  return { ...firstStep(1), target, number };
}

/**
 * The step that "Add step" puts in after `last`: the same kind, value and times, on the next
 * channel or memory. That is what building a run of lights needs most often.
 */
export function stepAfter(last: TimedStep): TimedStep {
  const highest = last.target === 'channel' ? CHANNEL_COUNT : MEMORY_COUNT;
  return {
    target: last.target,
    number: Math.min(last.number + 1, highest),
    value: last.value,
    up: last.up,
    wait: last.wait,
    down: last.down,
  };
}

/**
 * LAN: whether other devices in the same network (a phone, a tablet) may use the desk. Off
 * whenever the desk program starts; switched on and off at the PC only.
 */
export interface LanAccess {
  /** True for a page on the PC itself. Only such a page can switch LAN and learns the password. */
  local: boolean;
  /** True while other devices may connect, with the password. */
  on: boolean;
  /** Where another device reaches the desk, for example "192.168.1.20:5080", the best one first. Empty on another device. */
  addresses: string[];
  /** The password other devices have to give; null while LAN is off, and on another device. */
  password: string | null;
}

/** Whether the backend reaches the dmxbox. */
export interface BoxStatus {
  connected: boolean;
  host: string;
  firmware: string | null;
}

/** The project that is open. `path` is null until it has been saved to a file. */
export interface ProjectInfo {
  name: string;
  path: string | null;
  unsaved: boolean;
}

/** What the backend answers when the page connects. */
export interface Welcome {
  version: string;
  desk: DeskState;
  box: BoxStatus;
  project: ProjectInfo;
}

/** One project file in the projects folder. */
export interface ProjectFile {
  name: string;
  path: string;
  /** When the file was last changed, as an ISO 8601 text. */
  modified: string;
}

export interface ProjectList {
  folder: string;
  projects: ProjectFile[];
}