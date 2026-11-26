import { Service, computed, effect, signal } from '@angular/core';
import { CHANNEL_INDEPENDENT_COUNT, MEMORY_INDEPENDENT_COUNT } from './desk-types';
import { EFFECT_INDEPENDENT_COUNT } from '../effects/effects';
import { SOUND_INDEPENDENT_COUNT } from '../sounds/sounds';
import { KEY_COLORS, isColor, keyColorWithId } from './key-colors';
import { ALWAYS_SHOWN, DEFAULT_VIEW_ORDER, ViewName, hiddenViewsOf, viewOrderOf } from './views';

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

/** The row of effect faders on the main view: none at first, or one, under the memories; the row of sound faders likewise. */
export type EffectRowCount = 0 | 1;
export const EFFECT_ROW_COUNTS: readonly EffectRowCount[] = [0, 1];

/** How many steps the stack wheel on the main view shows: the current one in the middle. */
export type WheelSize = 1 | 3 | 5 | 7 | 9;
export const WHEEL_SIZES: readonly WheelSize[] = [1, 3, 5, 7, 9];

/** The independent keys a desk shows of each kind, unless its owner chose another number. */
export const DEFAULT_SHOWN_KEYS = 10;

/** The zoom of the whole page: from 50 to 200 per cent, in steps of 10. */
export const ZOOM_MIN = 0.5;
export const ZOOM_MAX = 2;
export const ZOOM_STEP = 0.1;

/**
 * Shortcuts every desk has until its owner changes them: undo and redo, as everywhere. A key
 * whose shortcut was taken away keeps none (an empty text stands for that).
 */
export const DEFAULT_SHORTCUTS: Readonly<Record<string, string>> = {
  'project.undo': 'Ctrl+KeyZ',
  'project.redo': 'Ctrl+KeyY',
};

/**
 * What the owner set in the Settings view, the keyboard shortcuts too. They belong to this device, its
 * browser keeps them, and every project shows them; Export takes them to another device, and
 * Import brings them from one (see DeskSettings.takeSettings and takeShortcuts). (The LAN
 * address and password and the autosave belong to the desk program; the Settings view changes
 * them there.)
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
  /** How many of the 32 effect keys of the independents are shown, 0 to 32; none at first. */
  readonly effectKeys: number;
  /** How many of the 32 sound keys of the independents are shown, 0 to 32; none at first. */
  readonly soundKeys: number;
  /** The row of effect faders under the memories on the main view: 0 (none, at first) or 1. */
  readonly effectRows: EffectRowCount;
  /** The row of sound faders under the memories (and the effects) on the main view: 0 (none, at first) or 1. */
  readonly soundRows: EffectRowCount;
  /** The zoom of the whole page, 1 as it is: as the browser's own zoom does, on this page only. */
  readonly zoom: number;
  /** The keyboard shortcuts: for each key of the desk that has one, by its name (see desk-actions.ts), the keys that press it. */
  readonly shortcuts: Readonly<Record<string, string>>;
  /** The colors that differ from the usual ones, by their name (see key-colors.ts): "#3ddc84". */
  readonly colors: Readonly<Record<string, string>>;
  /** The views left out of the top bar (see views.ts); never Settings. */
  readonly hiddenViews: readonly ViewName[];
  /** The order of the views in the top bar: every view, once. */
  readonly viewOrder: readonly ViewName[];
  /** The panel of the light at the right of the Stage view: shown (at first), or hidden for a bigger plan. */
  readonly stagePanel: boolean;
}

export const DEFAULT_SETTINGS: DeskSettingsData = {
  channelSize: 'auto',
  channelRows: 2,
  memoryRows: 1,
  wheelSize: 7,
  channelKeys: DEFAULT_SHOWN_KEYS,
  memoryKeys: DEFAULT_SHOWN_KEYS,
  effectKeys: 0,
  soundKeys: 0,
  effectRows: 0,
  soundRows: 0,
  zoom: 1,
  shortcuts: DEFAULT_SHORTCUTS,
  colors: {},
  hiddenViews: [],
  viewOrder: DEFAULT_VIEW_ORDER,
  stagePanel: true,
};

/** The settings of the Settings view, as Export writes them: all but the shortcuts, which Keyboard has. */
export type SettingsPart = Omit<DeskSettingsData, 'shortcuts'>;

/** The settings without the shortcuts. */
export function settingsPart(settings: DeskSettingsData): SettingsPart {
  return {
    channelSize: settings.channelSize,
    channelRows: settings.channelRows,
    memoryRows: settings.memoryRows,
    wheelSize: settings.wheelSize,
    channelKeys: settings.channelKeys,
    memoryKeys: settings.memoryKeys,
    effectKeys: settings.effectKeys,
    soundKeys: settings.soundKeys,
    effectRows: settings.effectRows,
    soundRows: settings.soundRows,
    zoom: settings.zoom,
    colors: settings.colors,
    hiddenViews: settings.hiddenViews,
    viewOrder: settings.viewOrder,
    stagePanel: settings.stagePanel,
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
    effectKeys: keyCount(kept['effectKeys'], EFFECT_INDEPENDENT_COUNT, DEFAULT_SETTINGS.effectKeys),
    soundKeys: keyCount(kept['soundKeys'], SOUND_INDEPENDENT_COUNT, DEFAULT_SETTINGS.soundKeys),
    effectRows: oneOf(kept['effectRows'], EFFECT_ROW_COUNTS, DEFAULT_SETTINGS.effectRows),
    soundRows: oneOf(kept['soundRows'], EFFECT_ROW_COUNTS, DEFAULT_SETTINGS.soundRows),
    // settings kept before the zoom (2026-11-24) have a size of the lettering, "fontScale": passed over
    zoom: zoomOf(kept['zoom']),
    shortcuts: shortcuts(kept['shortcuts']),
    // settings kept before 2026-11-21 name the colors in the British spelling: read, never written
    colors: colors(kept['colors'] ?? kept['colours']),
    hiddenViews: hiddenViewsOf(kept['hiddenViews']),
    viewOrder: viewOrderOf(kept['viewOrder']),
    stagePanel:
      typeof kept['stagePanel'] === 'boolean' ? kept['stagePanel'] : DEFAULT_SETTINGS.stagePanel,
  };
}

/** The colors that make sense: a known name with a color like "#3ddc84". */
function colors(value: unknown): Record<string, string> {
  const kept: Record<string, string> = {};
  if (typeof value !== 'object' || value === null) return kept;
  for (const [id, color] of Object.entries(value)) {
    if (keyColorWithId(id) && isColor(color)) kept[id] = color.toLowerCase();
  }
  return kept;
}

/** A zoom as the Settings view offers it: within the range, on a step of 0.1. */
export function zoomOf(value: unknown): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) return DEFAULT_SETTINGS.zoom;
  const stepped = Math.round(value / ZOOM_STEP) * ZOOM_STEP;
  return Math.round(Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, stepped)) * 10) / 10;
}

/** A number of independent keys: a whole number from 0 to `highest`, or the default (10, or `otherwise`). */
export function keyCount(value: unknown, highest: number, otherwise = DEFAULT_SHOWN_KEYS): number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= highest
    ? value
    : otherwise;
}

/** The value if it is one of the choices, otherwise the default. */
function oneOf<T>(value: unknown, choices: readonly T[], otherwise: T): T {
  return choices.includes(value as T) ? (value as T) : otherwise;
}

/**
 * The shortcuts that make sense. The usual ones (DEFAULT_SHORTCUTS) come with them, unless their
 * key was given a shortcut of its own or none, or their keys belong to another key of the desk.
 */
export function shortcuts(value: unknown): Record<string, string> {
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
 * The settings of this device, kept in the browser's local storage. A change is kept at once. The
 * zoom goes onto the page straight away: as CSS zoom on <html>, and as the variable --zoom, by
 * which the style sheets turn the sizes of the window (vw, dvh) into the zoomed page's own.
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
  readonly effectKeys = computed(() => this.data().effectKeys);
  readonly soundKeys = computed(() => this.data().soundKeys);
  readonly effectRows = computed(() => this.data().effectRows);
  readonly soundRows = computed(() => this.data().soundRows);
  readonly zoom = computed(() => this.data().zoom);
  readonly shortcuts = computed(() => this.data().shortcuts);
  readonly colors = computed(() => this.data().colors);
  readonly hiddenViews = computed(() => this.data().hiddenViews);
  readonly viewOrder = computed(() => this.data().viewOrder);
  readonly stagePanel = computed(() => this.data().stagePanel);
  /** The settings of the Settings view, without the shortcuts: what Export writes as "settings". */
  readonly settingsPart = computed(() => settingsPart(this.data()));

  constructor() {
    effect(() => {
      const zoom = this.data().zoom;
      if (typeof document === 'undefined') return;
      const style = document.documentElement.style;
      style.setProperty('zoom', zoom === 1 ? '' : String(zoom));
      style.setProperty('--zoom', String(zoom));
    });

    // The colors that differ from the usual ones go onto <html>, in place of those of styles.css.
    effect(() => {
      const colors = this.data().colors;
      if (typeof document === 'undefined') return;
      const style = document.documentElement.style;
      for (const entry of KEY_COLORS) {
        const color = colors[entry.id];
        if (color) style.setProperty(entry.variable, color);
        else style.removeProperty(entry.variable);
      }
    });
  }

  /** Gives a key, or a kind of key, a color of its own: "#3ddc84". */
  setColor(id: string, color: string): void {
    const entry = keyColorWithId(id);
    if (!entry || !isColor(color)) return;
    const rest = { ...this.data().colors };
    if (color.toLowerCase() === entry.standard) delete rest[id];
    else rest[id] = color.toLowerCase();
    this.change({ colors: rest });
  }

  /** The usual color again, for one (by its name) or, without a name, for all. */
  resetColors(id?: string): void {
    if (id === undefined) {
      this.change({ colors: {} });
      return;
    }
    const rest = { ...this.data().colors };
    delete rest[id];
    this.change({ colors: rest });
  }

  setChannelSize(size: ChannelSize): void {
    this.change({ channelSize: size });
  }

  /** A view in the top bar (true), or left out of it (false). Settings stays in it. */
  setViewShown(name: ViewName, shown: boolean): void {
    if (name === ALWAYS_SHOWN) return;
    const others = this.data().hiddenViews.filter((hidden) => hidden !== name);
    this.change({ hiddenViews: hiddenViewsOf(shown ? others : [...others, name]) });
  }

  /** A view one place earlier (-1) or later (+1) in the top bar. */
  moveView(name: ViewName, by: -1 | 1): void {
    const order = [...this.data().viewOrder];
    const at = order.indexOf(name);
    const other = at + by;
    if (at < 0 || other < 0 || other >= order.length) return;
    [order[at], order[other]] = [order[other], order[at]];
    this.change({ viewOrder: order });
  }

  /** Every view in the top bar again, in the usual order. */
  resetViews(): void {
    this.change({ hiddenViews: [], viewOrder: DEFAULT_VIEW_ORDER });
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

  setEffectKeys(count: number): void {
    this.change({ effectKeys: keyCount(count, EFFECT_INDEPENDENT_COUNT, this.data().effectKeys) });
  }

  setSoundKeys(count: number): void {
    this.change({ soundKeys: keyCount(count, SOUND_INDEPENDENT_COUNT, this.data().soundKeys) });
  }

  setEffectRows(rows: EffectRowCount): void {
    this.change({ effectRows: oneOf(rows, EFFECT_ROW_COUNTS, this.data().effectRows) });
  }

  /** The panel of the light at the right of the Stage view: shown, or hidden for a bigger plan. */
  setStagePanel(shown: boolean): void {
    this.change({ stagePanel: shown });
  }

  setSoundRows(rows: EffectRowCount): void {
    this.change({ soundRows: oneOf(rows, EFFECT_ROW_COUNTS, this.data().soundRows) });
  }

  /** The zoom one step bigger (+1) or smaller (-1), within the range. */
  stepZoom(direction: 1 | -1): void {
    this.change({ zoom: zoomOf(this.data().zoom + direction * ZOOM_STEP) });
  }

  resetZoom(): void {
    this.change({ zoom: DEFAULT_SETTINGS.zoom });
  }

  /** All keyboard shortcuts at once: for each key of the desk that has one, the keys that press it. */
  setShortcuts(shortcuts: Readonly<Record<string, string>>): void {
    this.change({ shortcuts });
  }

  /**
   * Settings from a file (Import): they take the place of this device's, all but the shortcuts.
   * Whatever they lack or that makes no sense is the default.
   */
  takeSettings(settings: unknown): void {
    const taken = settingsOf(settings);
    this.data.update((old) => ({ ...taken, shortcuts: old.shortcuts }));
    this.keep();
  }

  /** Shortcuts from a file (Import): they take the place of this device's, with the usual ones they leave free. */
  takeShortcuts(value: unknown): void {
    this.change({ shortcuts: shortcuts(value) });
  }

  private change(part: Partial<DeskSettingsData>): void {
    this.data.update((old) => ({ ...old, ...part }));
    this.keep();
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
