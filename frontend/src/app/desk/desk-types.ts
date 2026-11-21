/** Sizes of the desk. */
export const CHANNEL_COUNT = 512;
export const MEMORY_COUNT = 128;
export const MAX_LEVEL = 255;
/**
 * The independent keys the desk keeps: 32 that each switch a channel, 32 that each switch a
 * memory's fader, between zero and full. Settings says how many of each the page shows.
 */
export const CHANNEL_INDEPENDENT_COUNT = 32;
export const MEMORY_INDEPENDENT_COUNT = 32;

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
 * Narrower than this is "narrow": the top bar takes two lines, the links to the views under the
 * name, the keys and the project, which no longer fit beside them.
 */
export const NARROW_BELOW_PX = 1300;
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
  /** The channel each channel key of the independents switches. */
  independents: number[];
  /** The memory each memory key of the independents switches. Missing from a desk program from before those keys. */
  memoryIndependents?: number[];
  master: number;
  blackout: boolean;
  /** The rate, a speed master for all sequences, 0.1 to 10. Missing from an older desk program. */
  rate?: number;
  /** True while the rate counts (RATE lit). */
  rateOn?: boolean;
  /** The keys held for a solo now, on any page. An older desk program leaves it out. */
  solo?: SoloState;
  /**
   * For each channel fader, when it last took its channel, on a count that grows (0: never): an
   * attribute of a light, such as a pan, comes from the fader that took it last. An older desk
   * program leaves it out.
   */
  channelClaims?: number[];
  /** For each memory fader, when it last left zero and took the lights its memory holds, on the same count. */
  memoryClaims?: number[];
}

/** A key of a channel or a memory held for a solo. */
export interface SoloKey {
  target: StepTarget;
  number: number;
}

/**
 * The keys held for a solo now, on any page: with SOLO UP only their faders shine, at full, and
 * every other light is dark; with SOLO DOWN their faders give nothing, and the rest stays.
 */
export interface SoloState {
  up: SoloKey[];
  down: SoloKey[];
}

export const NO_SOLO: SoloState = { up: [], down: [] };

/** The autosave of the desk program: whether it keeps the work that is not saved, and how often. */
export interface AutosaveOptions {
  on: boolean;
  /** 5, 30, 60 or 300 seconds. */
  seconds: number;
}

/**
 * A light of the rig on the stage plan: a fixture. Its channels are one letter each, from its
 * address on: D dimmer, R red, G green, B blue, W white, A amber, U ultraviolet; any other letter
 * is a channel that gives no light (P pan, T tilt, S strobe, a mode). See stage-lights.ts.
 */
export interface StageLight {
  /** Its number while the plan is edited; no other light of the plan has it. */
  id: number;
  name: string;
  /** Its first channel, 1-512. */
  address: number;
  /** One letter per channel, like "DRGB" or "RGBRGBRGBRGB" (four cells). */
  channels: string;
  /** The color of a light without color channels (a gel), like "#ff2000"; null: warm white. */
  color: string | null;
  /** Where it hangs, in squares: across from the left edge of the stage, and from its back edge. */
  x: number;
  y: number;
  /** How it is turned, in degrees clockwise, 0 to 359. */
  angle: number;
  /** A circle (an ellipse when wider than deep) or a square (an oblong over several squares). */
  shape: LightShape;
  /** How big it is before it is turned, in whole squares, 1 to 32: across and from back to front. */
  width: number;
  depth: number;
  /** The kind of light from the fixture library it is, by its id in the plan's types; null (or missing) for one of letters. */
  type?: number | null;
  /** The most it gives, in per cent, 1 to 100 (missing: 100), like a hazer at most 60. */
  limit?: number;
}

export type LightShape = 'round' | 'square';

/** The biggest a light can be, across and from back to front, in squares. */
export const LIGHT_MAX_SIZE = 32;

/** A light without a limit goes to 100 per cent. */
export const NO_LIMIT = 100;

/**
 * A kind of light from the fixture library (the Open Fixture Library): a maker's model in one of
 * its modes, with what each channel does. The lights of this kind have its channels' codes as
 * their letters.
 */
export interface FixtureType {
  id: number;
  maker: string;
  model: string;
  mode: string;
  /** Where it is in the library, like "american-dj/inno-pocket-spot". */
  source: string;
  channels: FixtureChannel[];
}

/** A channel of a kind of light from the library. */
export interface FixtureChannel {
  /** What the library calls it, like "Pan" or "Color Wheel". */
  name: string;
  /** Its letter: D dimmer, R G B W A U colors, P pan, T tilt, F the fine part of another channel, S shutter or strobe, X anything else. */
  code: string;
  /** For the fine part of another channel (16 bits): that channel's index, 0 for the first; null otherwise. */
  fine?: number | null;
  /** What it does at its values, for a channel that does several things: wheel slots, a shutter, modes. */
  ranges?: FixtureRange[];
}

/** What a channel does from one value to another, like "Red" on a color wheel. */
export interface FixtureRange {
  from: number;
  to: number;
  label: string;
  /** For a color of a wheel: its color, like "#ff0000". */
  color?: string | null;
}

/** A group of lights, with one fader on the Fixtures page that moves all of them. */
export interface LightGroup {
  id: number;
  name: string;
  /** The ids of its lights. */
  lights: number[];
}

/** The most groups and the most kinds of lights from the library a plan can have, and the longest group name. */
export const MAX_GROUPS = 64;
export const MAX_TYPES = 64;
export const GROUP_NAME_LENGTH = 24;

/** The stage plan of the project: the stage in squares of its grid, and the lights on it. */
export interface StagePlan {
  width: number;
  depth: number;
  lights: StageLight[];
  /** The picture under the plan, by a short name of its content; null without one. GetStagePicture gives it. */
  pictureId: string | null;
  /** How brightly the picture shows through, 0.1 to 1. */
  pictureOpacity: number;
  /** What the picture lies under: the stage, or the whole plan with the room around the stage. */
  pictureOver: 'stage' | 'plan';
  /** The kinds of lights from the library that lights of the plan are. Missing from an older desk program. */
  types: FixtureType[];
  /** The groups of lights. Missing from an older desk program. */
  groups: LightGroup[];
}

/** The plan of a desk without one: a stage of 16 by 10 squares, no lights, no picture. */
export const EMPTY_STAGE: StagePlan = {
  width: 16,
  depth: 10,
  lights: [],
  pictureId: null,
  pictureOpacity: 0.6,
  pictureOver: 'stage',
  types: [],
  groups: [],
};

/** The smallest and biggest stage, across and from back to front, in squares. */
export const STAGE_MIN_SIZE = 4;
export const STAGE_MAX_SIZE = 64;

/** Squares around the stage where lights can hang too: beside it, behind it, and in front of it over the audience. */
export const STAGE_SIDE_ROOM = 2;
export const STAGE_BACK_ROOM = 2;
export const STAGE_FRONT_ROOM = 4;

/** What the stored memories hold: for each stored memory, by its number, the level of every channel. */
export type MemoryLooks = Readonly<Record<number, readonly number[]>>;

/** A fader of the desk: of a channel or of a memory (solo keys, the independents). */
export type StepTarget = 'channel' | 'memory';

/** What a step of a sequence or of the stack stands for: a fader, or a fixture or a group of fixtures of the stage plan. */
export type StepKind = StepTarget | 'fixture' | 'group';

/** The longest time a step can have, in seconds. */
export const MAX_STEP_SECONDS = 999.9;
/** The most steps a sequence can have. */
export const MAX_SEQUENCE_STEPS = 99;

/**
 * One step of a sequence or of the stack: a channel, a memory, a fixture or a group, the value
 * it is brought to, and three times in seconds. `up` is its way from zero to the value, `wait`
 * how long it then stays, `down` its way back to zero when the next step takes over.
 */
export interface TimedStep {
  target: StepKind;
  /** The channel (1 to 512) or the memory (1 to 128); a fixture or a group by its id in the stage plan. */
  number: number;
  /** 0 to 255: for a fixture or a group, the intensity of its lights. */
  value: number;
  up: number;
  wait: number;
  down: number;
  /** For a fixture or a group: the color its lights shine in, like "#ff2000"; missing or null for white. */
  color?: string | null;
}

/** A sequence, stored in a memory: its steps run one after the other while the memory's fader is up. */
export interface Sequence {
  /** True: after the last step it starts again. False: it runs once and stays on the last step. */
  loop: boolean;
  steps: TimedStep[];
  /** True: in a loop, but every round plays the steps in a new random order. Missing means false. */
  random?: boolean;
  /** How fast it is played, 0.1 to 10: every time is divided by it. Missing means 1. */
  speed?: number;
}

/** How a new sequence is played until its owner says otherwise: in a loop. The backend starts one the same way. */
export const DEFAULT_LOOP = true;

/** The speed of a sequence: from a tenth to ten times, in steps of 0.05. */
export const MIN_SPEED = 0.1;
export const MAX_SPEED = 10;
export const SPEED_STEP = 0.05;

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
 * The step that recording a sequence puts in for a pressed key: that channel, memory, fixture or
 * group at full, a fixture or a group in `color`, with the times a new sequence starts with.
 */
export function recordedStep(target: StepKind, number: number, color: string | null = null): TimedStep {
  const step: TimedStep = { ...firstStep(1), target, number };
  return target === 'fixture' || target === 'group' ? { ...step, color } : step;
}

/**
 * The step that "Add step" puts in after `last`: the same kind, value, color and times, on the
 * next channel or memory, or the next fixture (by address) or group of `plan`. That is what
 * building a run of lights needs most often.
 */
export function stepAfter(last: TimedStep, plan: Pick<StagePlan, 'lights' | 'groups'> = EMPTY_STAGE): TimedStep {
  const nextOf = (ids: readonly number[]) => {
    const at = ids.indexOf(last.number);
    return at >= 0 && at + 1 < ids.length ? ids[at + 1] : last.number;
  };
  const number =
    last.target === 'fixture' ? nextOf([...plan.lights].sort((a, b) => a.address - b.address).map((light) => light.id))
    : last.target === 'group' ? nextOf(plan.groups.map((group) => group.id))
    : Math.min(last.number + 1, last.target === 'channel' ? CHANNEL_COUNT : MEMORY_COUNT);
  const { target, value, up, wait, down, color } = last;
  return color === undefined ? { target, number, value, up, wait, down } : { target, number, value, up, wait, down, color };
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
  /**
   * The address of this PC chosen for LAN, like "192.168.1.20": devices come in there only.
   * Null (or missing, from an older desk program) while the desk chooses by itself: then the
   * first of `addresses` is the one to give them. Not among `addresses` while the network has
   * given the PC another address: then the desk chooses by itself until it is back.
   */
  chosen?: string | null;
}

/**
 * Where devices reach the desk now, like "192.168.1.20:5080": the chosen address while this PC
 * has it, otherwise the first, the best one. Null without a network.
 */
export function lanAddressInUse(lan: LanAccess): string | null {
  const chosen = lan.chosen ? lan.addresses.find((address) => address.split(':')[0] === lan.chosen) : undefined;
  return chosen ?? lan.addresses[0] ?? null;
}

/**
 * What undo and redo would do now, as the desk program says: "storing memory 5", or null when
 * there is nothing to undo or redo. Undo covers what the project holds: memories, sequences,
 * the stack, notes and the independent keys.
 */
export interface HistoryState {
  undo: string | null;
  redo: string | null;
}

/** Nothing to undo, nothing to redo. */
export const NO_HISTORY: HistoryState = { undo: null, redo: null };

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

/** The name the backend gives a project that has none yet: a new one. */
export const NEW_PROJECT_NAME = 'Untitled';

/** What the backend answers to an import: the project, now open, and what became of the show file, sentence by sentence. */
export interface ImportAnswer {
  project: ProjectInfo;
  report: string[];
}

/**
 * The largest show file the page sends to import. It goes to the backend in one message, and
 * the backend takes messages of up to 10 MB (Program.MaxMessageBytes): room for the file in JSON.
 */
export const IMPORT_LIMIT_BYTES = 8 * 1024 * 1024;

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