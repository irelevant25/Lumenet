import { Service, computed, effect, signal } from '@angular/core';
import { CHANNEL_INDEPENDENT_COUNT, MEMORY_INDEPENDENT_COUNT } from './desk-types';

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
 * What the owner set in the Settings view. It belongs to this browser on this device: a phone
 * and the PC each have their own, for they have different screens and keyboards. (The LAN
 * address and password belong to the desk program; the Settings view changes them there.)
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
}

export const DEFAULT_SETTINGS: DeskSettingsData = {
  channelSize: 'auto',
  channelRows: 2,
  memoryRows: 1,
  wheelSize: 3,
  channelKeys: DEFAULT_SHOWN_KEYS,
  memoryKeys: DEFAULT_SHOWN_KEYS,
  fontScale: 1,
  shortcuts: {},
};

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
  };
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

function shortcuts(value: unknown): Record<string, string> {
  if (typeof value !== 'object' || value === null) return {};
  const kept: Record<string, string> = {};
  for (const [action, keys] of Object.entries(value)) {
    if (typeof keys === 'string' && keys.length > 0) kept[action] = keys;
  }
  return kept;
}

/**
 * The settings of this browser, kept in its local storage. A change is kept at once. The font
 * scale goes onto the page straight away, as the variable --font-scale on <html>.
 */
@Service()
export class DeskSettings {
  private readonly data = signal<DeskSettingsData>(DeskSettings.read());

  readonly channelSize = computed(() => this.data().channelSize);
  readonly channelRows = computed(() => this.data().channelRows);
  readonly memoryRows = computed(() => this.data().memoryRows);
  readonly wheelSize = computed(() => this.data().wheelSize);
  readonly channelKeys = computed(() => this.data().channelKeys);
  readonly memoryKeys = computed(() => this.data().memoryKeys);
  readonly fontScale = computed(() => this.data().fontScale);
  readonly shortcuts = computed(() => this.data().shortcuts);

  constructor() {
    effect(() => {
      const scale = this.data().fontScale;
      if (typeof document !== 'undefined') document.documentElement.style.setProperty('--font-scale', String(scale));
    });
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

  private change(part: Partial<DeskSettingsData>): void {
    this.data.update((old) => ({ ...old, ...part }));
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
