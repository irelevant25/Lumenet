import type { DeskStore } from './desk-store';
import { CHANNEL_COUNT, CHANNEL_INDEPENDENT_COUNT, MEMORY_COUNT, MEMORY_INDEPENDENT_COUNT } from './desk-types';
import type { FunctionName } from './function-mode';

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
  /** What the list of the Keyboard view calls it, like "Play" or "Channel key 5". */
  readonly label: string;
  readonly run: ((context: ActionContext) => void) | null;
  /**
   * For a key that holds its fader at full in SOLO mode (under a fader, or of the independents): what holding
   * it down (true) and letting it go (false) does. Its shortcut then does the same while it is held.
   */
  readonly hold?: (context: ActionContext, down: boolean) => void;
}

/** The groups of the list of the Keyboard view, in their order. */
export const ACTION_GROUPS = [
  'Views',
  'Project and LAN',
  'Master',
  'Stack',
  'Record',
  'Sequences view',
  'Independents',
  'Channel pages',
  'Memory pages',
  'Channel keys',
  'Memory keys',
] as const;

export type ActionGroup = (typeof ACTION_GROUPS)[number];

/** The most page keys there can be: with 8 channels on a page, 64 pages of channels and 32 of memories. */
export const MAX_CHANNEL_PAGES = CHANNEL_COUNT / 8;
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
  view('settings', 'Settings view', '/settings'),
  view('keyboard', 'Keyboard view', '/keyboard'),

  { id: 'project.undo', group: 'Project and LAN', label: 'Undo', run: ({ store }) => store.undo() },
  { id: 'project.redo', group: 'Project and LAN', label: 'Redo', run: ({ store }) => store.redo() },
  { id: 'project.new', group: 'Project and LAN', label: 'New project', run: null },
  { id: 'project.save', group: 'Project and LAN', label: 'Save project', run: null },
  { id: 'project.load', group: 'Project and LAN', label: 'Load project', run: null },
  { id: 'project.import', group: 'Project and LAN', label: 'Import a show', run: null },
  {
    id: 'lan',
    group: 'Project and LAN',
    label: 'LAN',
    run: ({ store }) => store.setLan(!store.lan()?.on),
  },

  { id: 'master.clear', group: 'Master', label: 'Clear', run: ({ store }) => store.clear() },
  { id: 'master.blackout', group: 'Master', label: 'Blackout', run: ({ store }) => store.toggleBlackout() },
  { id: 'master.solo', group: 'Master', label: 'Solo', run: ({ store }) => store.toggleSolo() },
  { id: 'master.rate', group: 'Master', label: 'Rate', run: ({ store }) => store.toggleRate() },

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

  ...numbered(MAX_CHANNEL_PAGES, (page) => ({
    id: `page.channel.${page}`,
    group: 'Channel pages',
    label: `Channel page ${page}`,
    run: ({ store }) => {
      const first = (page - 1) * store.channelsPerPage() + 1;
      if (first <= CHANNEL_COUNT) store.showChannelPage(first);
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

  ...numbered(CHANNEL_COUNT, (channel) => ({
    id: `channel.${channel}`,
    group: 'Channel keys',
    label: `Channel key ${channel}`,
    run: ({ store }) => store.pressChannel(channel),
    hold: ({ store }, down) => store.holdKey('channel', channel, down),
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
}

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
 * The shortcut a key press makes, like "Ctrl+Shift+KeyP": the keys held with it, then the
 * physical key, whatever letter the keyboard's language puts on it. Null for a key that is
 * only held with others, and for one that cannot be a shortcut (see RESERVED_KEYS).
 */
export function shortcutOf(press: KeyPress): string | null {
  if (!press.code || MODIFIERS.has(press.code) || RESERVED_KEYS.has(press.code)) return null;

  const parts: string[] = [];
  if (press.ctrlKey) parts.push('Ctrl');
  if (press.altKey) parts.push('Alt');
  if (press.shiftKey) parts.push('Shift');
  if (press.metaKey) parts.push('Meta');
  parts.push(press.code);
  return parts.join('+');
}

const KEY_NAMES: Readonly<Record<string, string>> = {
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
