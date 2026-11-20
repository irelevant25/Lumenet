import { Service, computed, effect, signal } from '@angular/core';
import { CHANNEL_INDEPENDENT_COUNT, MEMORY_INDEPENDENT_COUNT } from './desk-types';
import { KEY_COLOURS, isColour, keyColourWithId } from './key-colours';

/**
 * How many channels one page of the channel faders on the main view holds: as the size of the
 * window has it ("auto": a row is 32 faders on a wide window, 16 on a narrower one), or a fixed
 * number, spread over the rows of channel faders. A row of memory faders is as long as a row of
 * channel faders: 8 channels in two rows are rows of 4, and a page of 4 memories in one row.
 * The Memories and Sequences views always go by the window.
 */
export type ChannelSize = 'auto' | 8 | 16 | 32 | 64 | 128;

/** The choices of the channel size, in the order the Settings view offers them. */
export const CHANNEL_SIZES: readonly ChannelSize[] = ['auto', 8, 16, 32, 64, 128];

/** Rows of faders on the main view: of channels (2 at first), of memories (1 at first). */
export type RowCount = 1 | 2;
export const ROW_COUNTS: readonly RowCount[] = [1, 2];

/** How many steps the stack wheel on the main view shows: the current one in the middle. */
export type WheelSize = 1 | 3 | 5 | 7 | 9;
export const WHEEL_SIZES: readonly WheelSize[] = [1, 3, 5, 7, 9];

/** The independent keys a desk shows of each kind, unless its owner chose another number. */
export const DEFAULT_SHOWN_KEYS = 10;

/** The size of all lettering: from 0.7 to 1.5 times, in steps of 0.1. */
export const FONT_SCALE_MIN = 0.7;
export const FONT_SCALE_MAX = 1.5;
export const FONT_SCALE_STEP = 0.1;

/**
 * Shortcuts every desk has until its owner changes them: undo and redo, as everywhere. A key
 * whose shortcut was taken away keeps none (an empty text stands for that).
 */
export const DEFAULT_SHORTCUTS: Readonly<Record<string, string>> = {
  'project.undo': 'Ctrl+KeyZ',
  'project.redo': 'Ctrl+KeyY',
};

/**
 * What the owner set in the Settings view and the Keyboard view.
 *
 * On the PC the desk program runs on, the layout of the main view and the shortcuts belong to
 * the project: they are saved with it, and a project that is loaded brings its own (see
 * DeskSettings.takeProject). The lettering belongs to this browser on this device. A phone or a
 * tablet keeps all of its settings itself, for its screen differs. Every browser also keeps the
 * settings it uses, to start with them. (The LAN address and password belong to the desk
 * program; the Settings view changes them there.)
 */
export interface DeskSettingsData {
  readonly channelSize: ChannelSize;
  readonly channelRows: RowCount;
  readonly memoryRows: RowCount;
  /** How many steps the stack wheel on the main view shows. */
  readonly wheelSize: WheelSize;
  /** How many of the 32 channel keys of the independents are shown, 0 to 32. */
  readonly channelKeys: number;
  /** How many of the 32 memory keys of the independents are shown, 0 to 32. With both 0 there are no independents. */
  readonly memoryKeys: number;
  /** Every font size is multiplied by this. */
  readonly fontScale: number;
  /** The keyboard shortcuts: for each key of the desk that has one, by its name (see desk-actions.ts), the keys that press it. */
  readonly shortcuts: Readonly<Record<string, string>>;
  /** The colours that differ from the usual ones, by their name (see key-colours.ts): "#3ddc84". */
  readonly colours: Readonly<Record<string, string>>;
}

export const DEFAULT_SETTINGS: DeskSettingsData = {
  channelSize: 'auto',
  channelRows: 2,
  memoryRows: 1,
  wheelSize: 3,
  channelKeys: DEFAULT_SHOWN_KEYS,
  memoryKeys: DEFAULT_SHOWN_KEYS,
  fontScale: 1,
  shortcuts: DEFAULT_SHORTCUTS,
  colours: {},
};

/** The part of the settings that belongs to the project on the PC: all but the lettering. */
export type ProjectSettingsData = Omit<DeskSettingsData, 'fontScale'>;

/** The project's part of the settings, as the desk program keeps it. */
export function projectPart(settings: DeskSettingsData): ProjectSettingsData {
  return {
    channelSize: settings.channelSize,
    channelRows: settings.channelRows,
    memoryRows: settings.memoryRows,
    wheelSize: settings.wheelSize,
    channelKeys: settings.channelKeys,
    memoryKeys: settings.memoryKeys,
    shortcuts: settings.shortcuts,
    colours: settings.colours,
  };
}

/** Where the settings are kept in the browser. */
export const SETTINGS_STORAGE_KEY = 'lumenet-settings';

/**
 * The settings from what the browser kept, as text. Whatever is missing or makes no sense is
 * the default: a broken entry must not lose the others, and must never break the page.
 */
export function parseSettings(text: string | null): DeskSettingsData {
  let raw: unknown;
  try {
    raw = text ? JSON.parse(text) : null;
  } catch {
    raw = null;
  }
  return settingsOf(raw);
}

/** The settings from an object, a project's for example: as parseSettings, without the text. */
export function settingsOf(raw: unknown): DeskSettingsData {
  if (typeof raw !== 'object' || raw === null) return DEFAULT_SETTINGS;

  const kept = raw as Record<string, unknown>;
  return {
    channelSize: oneOf(kept['channelSize'], CHANNEL_SIZES, DEFAULT_SETTINGS.channelSize),
    channelRows: oneOf(kept['channelRows'], ROW_COUNTS, DEFAULT_SETTINGS.channelRows),
    memoryRows: oneOf(kept['memoryRows'], ROW_COUNTS, DEFAULT_SETTINGS.memoryRows),
    wheelSize: oneOf(kept['wheelSize'], WHEEL_SIZES, DEFAULT_SETTINGS.wheelSize),
    channelKeys: keyCount(kept['channelKeys'], CHANNEL_INDEPENDENT_COUNT),
    memoryKeys: keyCount(kept['memoryKeys'], MEMORY_INDEPENDENT_COUNT),
    fontScale: fontScale(kept['fontScale']),
    shortcuts: shortcuts(kept['shortcuts']),
    colours: colours(kept['colours']),
  };
}

/** The colours that make sense: a known name with a colour like "#3ddc84". */
function colours(value: unknown): Record<string, string> {
  const kept: Record<string, string> = {};
  if (typeof value !== 'object' || value === null) return kept;
  for (const [id, colour] of Object.entries(value)) {
    if (keyColourWithId(id) && isColour(colour)) kept[id] = colour.toLowerCase();
  }
  return kept;
}

/** A font scale as the Settings view offers it: within the range, on a step of 0.1. */
export function fontScale(value: unknown): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) return DEFAULT_SETTINGS.fontScale;
  const stepped = Math.round(value / FONT_SCALE_STEP) * FONT_SCALE_STEP;
  return Math.round(Math.min(FONT_SCALE_MAX, Math.max(FONT_SCALE_MIN, stepped)) * 10) / 10;
}

/** A number of independent keys: a whole number from 0 to `highest`, or the default. */
export function keyCount(value: unknown, highest: number): number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= highest
    ? value
    : DEFAULT_SHOWN_KEYS;
}

/** The value if it is one of the choices, otherwise the default. */
function oneOf<T>(value: unknown, choices: readonly T[], otherwise: T): T {
  return choices.includes(value as T) ? (value as T) : otherwise;
}

/**
 * The shortcuts that make sense. The usual ones (DEFAULT_SHORTCUTS) come with them, unless their
 * key was given a shortcut of its own or none, or their keys belong to another key of the desk.
 */
function shortcuts(value: unknown): Record<string, string> {
  const kept: Record<string, string> = {};
  if (typeof value === 'object' && value !== null) {
    for (const [action, keys] of Object.entries(value)) {
      if (typeof keys !== 'string') continue;
      // An empty text says "none": kept only where it keeps a usual shortcut away.
      if (keys.length > 0 || action in DEFAULT_SHORTCUTS) kept[action] = keys;
    }
  }

  const taken = new Set(Object.values(kept));
  for (const [action, keys] of Object.entries(DEFAULT_SHORTCUTS)) {
    if (!(action in kept) && !taken.has(keys)) kept[action] = keys;
  }
  return kept;
}

/**
 * The settings of this page, kept in the browser's local storage. A change is kept at once. The
 * font scale goes onto the page straight away, as the variable --font-scale on <html>.
 *
 * On the PC the desk program runs on, the desk store connects the settings with the project:
 * followProject sends every change of the project's part there, and takeProject puts the
 * project's in place of this browser's.
 */
@Service()
export class DeskSettings {
  private readonly data = signal<DeskSettingsData>(DeskSettings.read());

  // Where a change of the project's part goes besides this browser: to the desk program, on the
  // PC. Null on a phone, and before the page knows.
  private sendProject: ((settings: ProjectSettingsData) => void) | null = null;

  readonly channelSize = computed(() => this.data().channelSize);
  readonly channelRows = computed(() => this.data().channelRows);
  readonly memoryRows = computed(() => this.data().memoryRows);
  readonly wheelSize = computed(() => this.data().wheelSize);
  readonly channelKeys = computed(() => this.data().channelKeys);
  readonly memoryKeys = computed(() => this.data().memoryKeys);
  readonly fontScale = computed(() => this.data().fontScale);
  readonly shortcuts = computed(() => this.data().shortcuts);
  readonly colours = computed(() => this.data().colours);
  /** The part of the settings that belongs to the project. */
  readonly projectSettings = computed(() => projectPart(this.data()));

  constructor() {
    effect(() => {
      const scale = this.data().fontScale;
      if (typeof document !== 'undefined') document.documentElement.style.setProperty('--font-scale', String(scale));
    });

    // The colours that differ from the usual ones go onto <html>, in place of those of styles.css.
    effect(() => {
      const colours = this.data().colours;
      if (typeof document === 'undefined') return;
      const style = document.documentElement.style;
      for (const entry of KEY_COLOURS) {
        const colour = colours[entry.id];
        if (colour) style.setProperty(entry.variable, colour);
        else style.removeProperty(entry.variable);
      }
    });
  }

  /** Gives a key, or a kind of key, a colour of its own: "#3ddc84". */
  setColour(id: string, colour: string): void {
    const entry = keyColourWithId(id);
    if (!entry || !isColour(colour)) return;
    const rest = { ...this.data().colours };
    if (colour.toLowerCase() === entry.standard) delete rest[id];
    else rest[id] = colour.toLowerCase();
    this.change({ colours: rest });
  }

  /** The usual colour again, for one (by its name) or, without a name, for all. */
  resetColours(id?: string): void {
    if (id === undefined) {
      this.change({ colours: {} });
      return;
    }
    const rest = { ...this.data().colours };
    delete rest[id];
    this.change({ colours: rest });
  }

  setChannelSize(size: ChannelSize): void {
    this.change({ channelSize: size });
  }

  setChannelRows(rows: RowCount): void {
    this.change({ channelRows: oneOf(rows, ROW_COUNTS, this.data().channelRows) });
  }

  setMemoryRows(rows: RowCount): void {
    this.change({ memoryRows: oneOf(rows, ROW_COUNTS, this.data().memoryRows) });
  }

  setWheelSize(size: WheelSize): void {
    this.change({ wheelSize: oneOf(size, WHEEL_SIZES, this.data().wheelSize) });
  }

  setChannelKeys(count: number): void {
    this.change({ channelKeys: keyCount(count, CHANNEL_INDEPENDENT_COUNT) });
  }

  setMemoryKeys(count: number): void {
    this.change({ memoryKeys: keyCount(count, MEMORY_INDEPENDENT_COUNT) });
  }

  /** One step bigger (+1) or smaller (-1), within the range. */
  stepFontScale(direction: 1 | -1): void {
    this.change({ fontScale: fontScale(this.data().fontScale + direction * FONT_SCALE_STEP) });
  }

  resetFontScale(): void {
    this.change({ fontScale: DEFAULT_SETTINGS.fontScale });
  }

  /** All keyboard shortcuts at once: for each key of the desk that has one, the keys that press it. */
  setShortcuts(shortcuts: Readonly<Record<string, string>>): void {
    this.change({ shortcuts });
  }

  /**
   * From now on, every change of the project's part of the settings also goes to `send` (the
   * desk program, on the PC); null for none (a phone keeps its settings to itself).
   */
  followProject(send: ((settings: ProjectSettingsData) => void) | null): void {
    this.sendProject = send;
  }

  /**
   * The project's settings, as the desk program has them: they take the place of this page's,
   * all but the lettering. Whatever they lack or that makes no sense is the default.
   */
  takeProject(settings: unknown): void {
    const taken = settingsOf(settings);
    this.data.update((old) => ({ ...taken, fontScale: old.fontScale }));
    this.keep();
  }

  private change(part: Partial<DeskSettingsData>): void {
    this.data.update((old) => ({ ...old, ...part }));
    this.keep();
    if (this.sendProject && Object.keys(part).some((key) => key !== 'fontScale')) {
      this.sendProject(projectPart(this.data()));
    }
  }

  private keep(): void {
    try {
      localStorage.setItem(SETTINGS_STORAGE_KEY, JSON.stringify(this.data()));
    } catch {
      // A browser that keeps nothing (a private window, storage switched off): the settings
      // last until the page is closed.
    }
  }

  private static read(): DeskSettingsData {
    try {
      return parseSettings(localStorage.getItem(SETTINGS_STORAGE_KEY));
    } catch {
      return DEFAULT_SETTINGS;
    }
  }
}
