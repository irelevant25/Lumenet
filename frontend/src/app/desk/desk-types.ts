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
  /** Whether each memory holds a look. */
  memoryStored: boolean[];
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