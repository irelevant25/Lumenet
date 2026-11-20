/**
 * A colour of the desk that Settings can change: of a kind of key (channels, memories,
 * sequences), or of one key. On the PC it is saved with the project, like the layout.
 */
export interface KeyColour {
  /** Its name in the settings, and the tone of its keys (see LampButton). */
  readonly id: string;
  /** Where Settings lists it. */
  readonly group: KeyColourGroup;
  /** What Settings calls it. */
  readonly label: string;
  /** The variable of styles.css that holds it. */
  readonly variable: string;
  /** The colour it has until it is changed: the same as in styles.css. */
  readonly standard: string;
}

export const KEY_COLOUR_GROUPS = ['Kinds', 'Master', 'Stack', 'Record', 'Top bar', 'Independents', 'Sequences view'] as const;
export type KeyColourGroup = (typeof KEY_COLOUR_GROUPS)[number];

const AMBER = '#ffb020';
const WHITE = '#f1f3f6';

/** Every colour Settings can change, group by group. */
export const KEY_COLOURS: readonly KeyColour[] = [
  colour('channel', 'Kinds', 'Channels: their keys, pages, independent keys and faders', '--channel', '#3ddc84'),
  colour('memory', 'Kinds', 'Memories: their keys, pages, independent keys and faders', '--memory', '#ff5247'),
  colour('sequence', 'Kinds', 'Sequences: the colour a memory that holds one runs into', '--sequence-end', '#ffd426'),

  key('clear', 'Master', 'Clear', WHITE),
  key('solo', 'Master', 'Solo', WHITE),
  key('blackout', 'Master', 'Blackout', '#4aa3ff'),
  key('rate', 'Master', 'Rate', AMBER),

  colour('stack', 'Stack', 'Stack (live), and the Live mark in the top bar', '--live', '#ff5247'),
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

  key('ind-edit', 'Independents', 'Edit', AMBER),

  key('once', 'Sequences view', 'Once', AMBER),
  key('loop', 'Sequences view', 'Loop', AMBER),
  key('random', 'Sequences view', 'Random', AMBER),
  key('sequence-add', 'Sequences view', 'Add step', AMBER),
  key('template', 'Sequences view', 'Template', AMBER),
  key('normal-speed', 'Sequences view', '1x (normal speed)', AMBER),
];

const BY_ID = new Map(KEY_COLOURS.map((entry) => [entry.id, entry]));

/** The colour with this name, or undefined. */
export function keyColourWithId(id: string): KeyColour | undefined {
  return BY_ID.get(id);
}

/** A colour as Settings keeps it: "#3ddc84". */
export function isColour(value: unknown): value is string {
  return typeof value === 'string' && /^#[0-9a-f]{6}$/i.test(value);
}

// The colour of one key: its variable is --key-<name>.
function key(id: string, group: KeyColourGroup, label: string, standard: string): KeyColour {
  return { id, group, label, variable: `--key-${id}`, standard };
}

function colour(id: string, group: KeyColourGroup, label: string, variable: string, standard: string): KeyColour {
  return { id, group, label, variable, standard };
}
