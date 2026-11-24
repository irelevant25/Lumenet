import type { DeskStore } from './desk-store';
import {
  CHANNEL_INDEPENDENT_COUNT,
  MAX_CHANNELS,
  MAX_UNIVERSES,
  MEMORY_COUNT,
  MEMORY_INDEPENDENT_COUNT,
  UNIVERSE_SIZE,
} from './desk-types';
import type { FunctionName } from './function-mode';
import { universeStart } from './universes';

/** What an action needs when its key is not on the view that is shown. */
export interface ActionContext {
  readonly store: DeskStore;
  /** Goes to a view, like "/stack". */
  readonly navigate: (url: string) => void;
}

/**
 * A key of the desk that a keyboard shortcut can press. Its element on the page carries its
 * name as `data-action`: a shortcut presses that element when it is there, exactly as a click
 * would. When it is not (another view, another page of faders), `run` does what the key does;
 * a key without `run` works only where it is.
 */
export interface DeskAction {
  readonly id: string;
  readonly group: ActionGroup;
  /** What the list of the Keyboard panel calls it, like "Play" or "Channel key 5". */
  readonly label: string;
  readonly run: ((context: ActionContext) => void) | null;
  /**
   * For a key that holds its fader while a solo is lit (under a fader, or of the independents): what holding
   * it down (true) and letting it go (false) does. Its shortcut then does the same while it is held.
   */
  readonly hold?: (context: ActionContext, down: boolean) => void;
}

/** The kinds of keys of the Keyboard panel, in their order. */
export const ACTION_GROUPS = [
  'Views',
  'Project and LAN',
  'Master',
  'General',
  'Stack',
  'Record',
  'Sequences view',
  'Stage view',
  'Notes view',
  'Independents',
  'Channel pages',
  'Memory pages',
  'Channel keys',
  'Memory keys',
] as const;

export type ActionGroup = (typeof ACTION_GROUPS)[number];

/**
 * The most page keys there can be: with 8 channels on a page, 64 pages of channels in a universe
 * (the page keys are those of the universe shown), and 32 of memories.
 */
export const MAX_CHANNEL_PAGES = UNIVERSE_SIZE / 8;
export const MAX_MEMORY_PAGES = MEMORY_COUNT / 4;

const RECORD_KEYS: readonly { function: FunctionName; label: string }[] = [
  { function: 'save', label: 'Save mem' },
  { function: 'edit', label: 'Edit mem' },
  { function: 'record', label: 'Rec seq' },
  { function: 'copy', label: 'Copy' },
  { function: 'delete', label: 'Del' },
];

/** Every key of the desk that can have a shortcut, group by group. */
export const DESK_ACTIONS: readonly DeskAction[] = [
  view('main', 'Main view', '/'),
  view('memories', 'Memories view', '/memories'),
  view('sequences', 'Sequences view', '/sequences'),
  view('stack', 'Stack view', '/stack'),
  view('stage', 'Stage view', '/stage'),
  view('fixtures', 'Fixtures view', '/fixtures'),
  view('notes', 'Notes view', '/notes'),
  view('settings', 'Settings view', '/settings'),

  { id: 'project.undo', group: 'Project and LAN', label: 'Undo', run: ({ store }) => store.undo() },
  { id: 'project.redo', group: 'Project and LAN', label: 'Redo', run: ({ store }) => store.redo() },
  { id: 'project.new', group: 'Project and LAN', label: 'New project', run: null },
  { id: 'project.save', group: 'Project and LAN', label: 'Save project', run: null },
  { id: 'project.load', group: 'Project and LAN', label: 'Load project', run: null },
  { id: 'project.import', group: 'Project and LAN', label: 'Import', run: null },
  { id: 'project.export', group: 'Project and LAN', label: 'Export', run: null },
  {
    id: 'lan',
    group: 'Project and LAN',
    label: 'LAN',
    run: ({ store }) => store.setLan(!store.lan()?.on),
  },
  { id: 'boxes', group: 'Project and LAN', label: 'Boxes (the window of the boxes)', run: null },

  // Clear, Solo and Rate stood in the master section once: their names say so, and the shortcuts
  // that projects keep under these names go on working.
  { id: 'master.blackout', group: 'Master', label: 'Blackout', run: ({ store }) => store.toggleBlackout() },
  { id: 'master.clear', group: 'General', label: 'Clear', run: ({ store }) => store.clear() },
  { id: 'master.solo', group: 'General', label: 'Solo up', run: ({ store }) => store.toggleSoloUp() },
  { id: 'master.solo-down', group: 'General', label: 'Solo down', run: ({ store }) => store.toggleSoloDown() },
  { id: 'master.rate', group: 'General', label: 'Rate', run: ({ store }) => store.toggleRate() },
  // tapped in time with the music; only while RATE is lit (the store sees to that)
  { id: 'master.tempo', group: 'General', label: 'Tempo (tap)', run: ({ store }) => store.tapTempo() },

  {
    id: 'stack.live',
    group: 'Stack',
    label: 'Stack (live)',
    run: ({ store }) => {
      if (store.live() || store.stack().steps.length > 0) store.setStackLive(!store.live());
    },
  },
  liveOnly('stack.previous', 'Prev', (store) => store.stackPrevious()),
  liveOnly('stack.play', 'Play', (store) => store.stackPlay()),
  liveOnly('stack.pause', 'Pause', (store) => store.stackPause()),
  liveOnly('stack.next', 'Next', (store) => store.stackNext()),
  { id: 'stack.add', group: 'Stack', label: 'Add step (Stack view)', run: ({ store }) => store.addStackStepAtEnd() },

  ...RECORD_KEYS.map(
    (key): DeskAction => ({
      id: `record.${key.function}`,
      group: 'Record',
      label: key.label,
      run: ({ store }) => store.pressFunction(key.function),
    }),
  ),

  { id: 'sequence.once', group: 'Sequences view', label: 'Once', run: null },
  { id: 'sequence.loop', group: 'Sequences view', label: 'Loop', run: null },
  { id: 'sequence.random', group: 'Sequences view', label: 'Random', run: null },
  { id: 'sequence.add', group: 'Sequences view', label: 'Add step', run: null },
  { id: 'sequence.template', group: 'Sequences view', label: 'Template', run: null },
  { id: 'sequence.speed', group: 'Sequences view', label: 'Normal speed (1x)', run: null },

  { id: 'stage.edit', group: 'Stage view', label: 'Edit (stage plan)', run: null },
  { id: 'stage.add', group: 'Stage view', label: 'Add light', run: null },
  // (The Stage view's Clear is the Clear of General: master.clear.)
  { id: 'fixtures.group', group: 'Stage view', label: 'New group (Fixtures view)', run: null },

  { id: 'notes.edit', group: 'Notes view', label: 'Edit (notes)', run: null },

  { id: 'ind.edit', group: 'Independents', label: 'Edit (independents)', run: null },
  ...numbered(CHANNEL_INDEPENDENT_COUNT, (key) => ({
    id: `ind.channel.${key}`,
    group: 'Independents',
    label: `Channel key ${key} of the independents`,
    run: ({ store }) => store.toggleIndependent(key),
    hold: ({ store }, down) => store.holdIndependent('channel', key, down),
  })),
  ...numbered(MEMORY_INDEPENDENT_COUNT, (key) => ({
    id: `ind.memory.${key}`,
    group: 'Independents',
    label: `Memory key ${key} of the independents`,
    run: ({ store }) => store.toggleMemoryIndependent(key),
    hold: ({ store }, down) => store.holdIndependent('memory', key, down),
  })),

  // The U keys above the channel faders, of a project with more than one universe.
  ...numbered(MAX_UNIVERSES, (universe) => ({
    id: `universe.${universe}`,
    group: 'Channel pages',
    label: `Universe ${universe} (U${universe})`,
    run: ({ store }) => store.showUniverse(universe),
  })),
  ...numbered(MAX_CHANNEL_PAGES, (page) => ({
    id: `page.channel.${page}`,
    group: 'Channel pages',
    label: `Channel page ${page}`,
    run: ({ store }) => {
      const offset = (page - 1) * store.channelsPerPage();
      if (offset < UNIVERSE_SIZE) store.showChannelPage(universeStart(store.shownUniverse()) + offset);
    },
  })),
  ...numbered(MAX_MEMORY_PAGES, (page) => ({
    id: `page.memory.${page}`,
    group: 'Memory pages',
    label: `Memory page ${page}`,
    run: ({ store }) => {
      const first = (page - 1) * store.memoriesPerPage() + 1;
      if (first <= MEMORY_COUNT) store.showMemoryPage(first);
    },
  })),

  // All 4096; the Keyboard panel lists those of the project's universes, and a key of a universe
  // the project does not have does nothing.
  ...numbered(MAX_CHANNELS, (channel) => ({
    id: `channel.${channel}`,
    group: 'Channel keys',
    label: `Channel key ${channel}`,
    run: ({ store }) => {
      if (channel <= store.channelCount()) store.pressChannel(channel);
    },
    hold: ({ store }, down) => {
      if (channel <= store.channelCount()) store.holdKey('channel', channel, down);
    },
  })),
  ...numbered(MEMORY_COUNT, (memory) => ({
    id: `memory.${memory}`,
    group: 'Memory keys',
    label: `Memory key ${memory}`,
    run: ({ store }) => store.pressMemory(memory),
    hold: ({ store }, down) => store.holdKey('memory', memory, down),
  })),
];

const BY_ID = new Map(DESK_ACTIONS.map((action) => [action.id, action]));

/** The action with this name, or undefined. */
export function actionWithId(id: string): DeskAction | undefined {
  return BY_ID.get(id);
}

function view(name: string, label: string, url: string): DeskAction {
  return { id: `view.${name}`, group: 'Views', label, run: ({ navigate }) => navigate(url) };
}

// Prev, Play, Pause and Next do something only while the stack is live, as their keys.
function liveOnly(id: string, label: string, act: (store: DeskStore) => void): DeskAction {
  return {
    id,
    group: 'Stack',
    label,
    run: ({ store }) => {
      if (store.live()) act(store);
    },
  };
}

function numbered(count: number, make: (number: number) => DeskAction): DeskAction[] {
  return Array.from({ length: count }, (_, index) => make(index + 1));
}

// ---------------------------------------------------------------- the keys of the keyboard

/** What a shortcut is made of: the physical key (KeyboardEvent.code) and the keys held with it. */
export interface KeyPress {
  readonly code: string;
  readonly ctrlKey: boolean;
  readonly altKey: boolean;
  readonly shiftKey: boolean;
  readonly metaKey: boolean;
  /**
   * True while AltGr is held (the right Alt of a keyboard like the Czech or Slovak one): Windows
   * then says that Ctrl is held too, by a Ctrl that is not there.
   */
  readonly altGraph?: boolean;
}

/** A key press as the shortcuts read it, from the browser's event. */
export function pressOf(event: KeyboardEvent): KeyPress {
  return {
    code: event.code,
    ctrlKey: event.ctrlKey,
    altKey: event.altKey,
    shiftKey: event.shiftKey,
    metaKey: event.metaKey,
    altGraph: event.getModifierState?.('AltGraph') ?? false,
  };
}

/** The Ctrl and Alt keys, by their codes: a shortcut can name the left or the right one. */
export const SIDED_KEYS = new Set(['ControlLeft', 'ControlRight', 'AltLeft', 'AltRight']);

/** Keys that are never a shortcut by themselves: those that are held with another one. */
const MODIFIERS = new Set([
  'ControlLeft',
  'ControlRight',
  'AltLeft',
  'AltRight',
  'ShiftLeft',
  'ShiftRight',
  'MetaLeft',
  'MetaRight',
  'OSLeft',
  'OSRight',
]);

/**
 * Keys that cannot be a shortcut: Esc stops recording one, and Tab goes from one key of the page
 * to the next.
 */
export const RESERVED_KEYS = new Set(['Escape', 'Tab']);

/**
 * The shortcut a key press makes, like "LeftCtrl+Shift+KeyP": the keys held with it, then the
 * physical key, whatever letter the keyboard's language puts on it. Null for a key that is
 * only held with others, and for one that cannot be a shortcut (see RESERVED_KEYS).
 *
 * `held` are the Ctrl and Alt keys held down, by their codes ("ControlLeft"), as their own presses
 * said them: the browser says only that a Ctrl is held, not which. A Ctrl or Alt whose side is not
 * known (or both of them) is named without one, "Ctrl", and such a shortcut is pressed by either.
 * AltGr is the right Alt, without the Ctrl that Windows adds to it (a left one; a right Ctrl held
 * with AltGr is named).
 */
export function shortcutOf(press: KeyPress, held: ReadonlySet<string> = new Set()): string | null {
  if (!press.code || MODIFIERS.has(press.code) || RESERVED_KEYS.has(press.code)) return null;

  const parts: string[] = [];
  if (press.altGraph) {
    if (held.has('ControlRight')) parts.push('RightCtrl');
    parts.push('RightAlt');
  } else {
    if (press.ctrlKey) parts.push(sideOf(held, 'Control', 'Ctrl'));
    if (press.altKey) parts.push(sideOf(held, 'Alt', 'Alt'));
  }
  if (press.shiftKey) parts.push('Shift');
  if (press.metaKey) parts.push('Meta');
  parts.push(press.code);
  return parts.join('+');
}

// "LeftCtrl" or "RightCtrl" when the key of one side is held; "Ctrl" for both, or for a side not seen.
function sideOf(held: ReadonlySet<string>, code: string, name: string): string {
  const left = held.has(`${code}Left`);
  const right = held.has(`${code}Right`);
  return left === right ? name : `${left ? 'Left' : 'Right'}${name}`;
}

/** A shortcut without the sides of its Ctrl and Alt: "LeftCtrl+KeyZ" as "Ctrl+KeyZ". One kept so is pressed by either key. */
export function withoutSides(shortcut: string): string {
  return shortcut.replace(/\b(?:Left|Right)(Ctrl|Alt)\b/g, '$1');
}

/**
 * The shortcuts a key press presses, in order: the first one that a key of the desk has wins.
 * First the press with the sides of its Ctrl and Alt ("RightCtrl+KeyZ"), then without them
 * ("Ctrl+KeyZ"). An AltGr press on Windows, which the desk named "Ctrl+Alt+..." before it told the
 * sides apart, comes last in that old name too, so that a shortcut recorded with AltGr then still
 * answers to it. Empty for a key that is never a shortcut.
 */
export function shortcutsOf(press: KeyPress, held: ReadonlySet<string> = new Set()): string[] {
  const shortcut = shortcutOf(press, held);
  if (shortcut === null) return [];

  const before = press.altGraph && press.ctrlKey && press.altKey ? shortcutOf({ ...press, altGraph: false }) : null;
  return [...new Set([shortcut, withoutSides(shortcut), before ?? shortcut])];
}

const KEY_NAMES: Readonly<Record<string, string>> = {
  LeftCtrl: 'Left Ctrl',
  RightCtrl: 'Right Ctrl',
  LeftAlt: 'Left Alt',
  RightAlt: 'Right Alt',
  Space: 'Space',
  Enter: 'Enter',
  Backspace: 'Backspace',
  Delete: 'Delete',
  Insert: 'Insert',
  Home: 'Home',
  End: 'End',
  PageUp: 'Page Up',
  PageDown: 'Page Down',
  ArrowUp: 'Up',
  ArrowDown: 'Down',
  ArrowLeft: 'Left',
  ArrowRight: 'Right',
  Minus: '-',
  Equal: '=',
  BracketLeft: '[',
  BracketRight: ']',
  Backslash: '\\',
  Semicolon: ';',
  Quote: "'",
  Backquote: '`',
  Comma: ',',
  Period: '.',
  Slash: '/',
  IntlBackslash: '\\ (left)',
  NumpadAdd: 'Num +',
  NumpadSubtract: 'Num -',
  NumpadMultiply: 'Num *',
  NumpadDivide: 'Num /',
  NumpadDecimal: 'Num .',
  NumpadEnter: 'Num Enter',
};

/** A shortcut as people write it: "Ctrl + Shift + P", "Num 5", "Space". */
export function shortcutName(shortcut: string): string {
  return shortcut
    .split('+')
    .map((part) => {
      if (/^Key[A-Z]$/.test(part)) return part.slice(3);
      if (/^Digit\d$/.test(part)) return part.slice(5);
      if (/^Numpad\d$/.test(part)) return `Num ${part.slice(6)}`;
      return KEY_NAMES[part] ?? part;
    })
    .join(' + ');
}
