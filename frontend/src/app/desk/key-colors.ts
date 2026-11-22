/**
 * A color of the desk that Settings can change: of a kind of key (channels, memories,
 * sequences), or of one key. It belongs to this device, like the other settings.
 */
export interface KeyColor {
  /** Its name in the settings, and the tone of its keys (see LampButton). */
  readonly id: string;
  /** Where Settings lists it. */
  readonly group: KeyColorGroup;
  /** What Settings calls it. */
  readonly label: string;
  /** The variable of styles.css that holds it. */
  readonly variable: string;
  /** The color it has until it is changed: the same as in styles.css. */
  readonly standard: string;
}

export const KEY_COLOR_GROUPS = [
  'Kinds',
  'Master',
  'General',
  'Stack',
  'Record',
  'Top bar',
  'Independents',
  'Sequences view',
  'Stage view',
  'Notes view',
] as const;
export type KeyColorGroup = (typeof KEY_COLOR_GROUPS)[number];

const AMBER = '#ffb020';
const WHITE = '#f1f3f6';

/** Every color Settings can change, group by group. */
export const KEY_COLORS: readonly KeyColor[] = [
  color('channel', 'Kinds', 'Channels: their keys, pages, independent keys and faders', '--channel', '#3ddc84'),
  color('memory', 'Kinds', 'Memories: their keys, pages, independent keys and faders', '--memory', '#ff5247'),
  color('sequence', 'Kinds', 'Sequences: the color a memory that holds one runs into', '--sequence-end', '#ffd426'),

  key('blackout', 'Master', 'Blackout', '#4aa3ff'),

  key('clear', 'General', 'Clear', WHITE),
  key('solo', 'General', 'Solo up', WHITE),
  key('solo-down', 'General', 'Solo down', WHITE),
  key('rate', 'General', 'Rate (and its fader)', AMBER),

  color('stack', 'Stack', 'Stack (live), and the Live mark in the top bar', '--live', '#ff5247'),
  key('prev', 'Stack', 'Prev', AMBER),
  key('play', 'Stack', 'Play, and the step that is on', '#3ddc84'),
  key('pause', 'Stack', 'Pause', WHITE),
  key('next', 'Stack', 'Next, and the step Play goes to', AMBER),
  key('add-step', 'Stack', 'Add step (Stack view)', AMBER),

  key('save', 'Record', 'Save mem', AMBER),
  key('edit', 'Record', 'Edit mem', AMBER),
  key('record', 'Record', 'Rec seq', AMBER),
  key('copy', 'Record', 'Copy', AMBER),
  key('delete', 'Record', 'Del', AMBER),

  key('lan', 'Top bar', 'LAN', AMBER),
  key('undo', 'Top bar', 'Undo', AMBER),
  key('redo', 'Top bar', 'Redo', AMBER),
  key('new', 'Top bar', 'New', AMBER),
  key('save-project', 'Top bar', 'Save', AMBER),
  key('load', 'Top bar', 'Load', AMBER),
  key('import', 'Top bar', 'Import', AMBER),
  key('export', 'Top bar', 'Export', AMBER),

  key('ind-edit', 'Independents', 'Edit', AMBER),

  key('once', 'Sequences view', 'Once', AMBER),
  key('loop', 'Sequences view', 'Loop', AMBER),
  key('random', 'Sequences view', 'Random', AMBER),
  key('sequence-add', 'Sequences view', 'Add step', AMBER),
  key('template', 'Sequences view', 'Template', AMBER),
  key('normal-speed', 'Sequences view', '1x (normal speed)', AMBER),

  key('stage-clear', 'Stage view', 'Clear', WHITE),
  key('stage-edit', 'Stage view', 'Edit', AMBER),
  key('stage-add', 'Stage view', 'Add light', AMBER),

  key('notes-edit', 'Notes view', 'Edit', AMBER),
  key('notes-tools', 'Notes view', 'The tools (bold, underline, size, lists, color)', AMBER),
];

/** The colors the keys have at first, each once, with a name: the palette Settings offers for any key. */
export const STANDARD_KEY_COLORS: readonly { readonly color: string; readonly name: string }[] = [
  { color: '#3ddc84', name: 'Green' },
  { color: '#ff5247', name: 'Red' },
  { color: '#ffd426', name: 'Yellow' },
  { color: AMBER, name: 'Amber' },
  { color: WHITE, name: 'White' },
  { color: '#4aa3ff', name: 'Blue' },
];

const BY_ID = new Map(KEY_COLORS.map((entry) => [entry.id, entry]));

/** The color with this name, or undefined. */
export function keyColorWithId(id: string): KeyColor | undefined {
  return BY_ID.get(id);
}

/** A color as Settings keeps it: "#3ddc84". */
export function isColor(value: unknown): value is string {
  return typeof value === 'string' && /^#[0-9a-f]{6}$/i.test(value);
}

// The color of one key: its variable is --key-<name>.
function key(id: string, group: KeyColorGroup, label: string, standard: string): KeyColor {
  return { id, group, label, variable: `--key-${id}`, standard };
}

function color(id: string, group: KeyColorGroup, label: string, variable: string, standard: string): KeyColor {
  return { id, group, label, variable, standard };
}
