/**
 * Export and Import: the parts of the desk that go into a file on this device and come back from
 * one. One ZIP holds the parts that were chosen, each a JSON file of its own, and the sound files of
 * the project: the project (as its project file says it), the stage on its own, the settings of this
 * device, its keyboard shortcuts, and the MIDI controllers of the PC with what their controls do
 * (only the PC takes those out and back). A backup is such a ZIP with every part.
 *
 * The page hands the parts to the desk program as one text, which it packs into the ZIP, and gets
 * that text back from a ZIP (SoundPack):
 *
 * ```json
 * {
 *   "format": "lumenet-export",
 *   "version": 1,
 *   "name": "Concert",
 *   "project": { "format": "lumenet-project", "version": 4, ... },
 *   "stage": { "width": 16, "depth": 10, "lights": [ ... ] },
 *   "settings": { "channelSize": "auto", "zoom": 1, "colors": {}, ... },
 *   "keyboard": { "shortcuts": { "stack.play": "Space" } },
 *   "midi": { "devices": ["APC mini mk2"], "mappings": [ ... ] }
 * }
 * ```
 *
 * Import also takes such a text as a file of its own (Export wrote one before 2026-11-26), and a
 * project file as the projects folder has it: its project, and its stage. One that an earlier version
 * wrote can also hold "settings" with the shortcuts in them, which were the project's then: they can
 * be taken as the settings and the keyboard of this device.
 */

export type TransferPart = 'project' | 'stage' | 'settings' | 'keyboard' | 'midi';

/** A part as the windows of Export and Import list it. */
export interface TransferPartView {
  readonly part: TransferPart;
  readonly label: string;
  readonly hint: string;
  /** True for a part that only the PC takes out and back: the MIDI controllers belong to it. */
  readonly pcOnly?: boolean;
}

/** The parts, in the order the windows of Export and Import list them. */
export const TRANSFER_PARTS: readonly TransferPartView[] = [
  {
    part: 'project',
    label: 'Project',
    hint: 'everything the project holds: memories, sequences, the stack, the stage, the notes and the sounds',
  },
  { part: 'stage', label: 'Stage', hint: 'the stage alone: its lights, their kinds, the groups and the picture' },
  { part: 'settings', label: 'Settings', hint: 'the settings of this device: layout, zoom, colors of the keys, views' },
  { part: 'keyboard', label: 'Keyboard', hint: 'the keyboard shortcuts of this device' },
  { part: 'midi', label: 'MIDI', hint: 'the MIDI controllers of this PC and what their controls do', pcOnly: true },
];

const FORMAT = 'lumenet-export';
const VERSION = 1;
const PROJECT_FORMAT = 'lumenet-project';

/**
 * Why a part cannot be checked on this page: a part of the PC on a phone or a tablet, or MIDI with a
 * desk program that knows none. Empty when it can.
 */
export function pcPartNote(part: TransferPartView, onPc: boolean, hasMidi: boolean): string {
  if (!part.pcOnly) return '';
  if (!onPc) return 'the MIDI controllers belong to the PC: only the PC takes them out and back';
  return hasMidi ? '' : 'the desk program is older than this page and knows no MIDI';
}

/** What a file of Export is made of: each part as the desk has it. */
export interface ExportParts {
  /** The name of the project. */
  readonly name: string;
  /** The project file, as the desk program writes it; needed for the project and for the stage. */
  readonly projectFile?: string;
  readonly settings?: unknown;
  readonly shortcuts?: Readonly<Record<string, string>>;
  /** The MIDI setup of the PC, as the desk program gives it: its devices and mappings. */
  readonly midi?: unknown;
}

/** The text of a file of Export with the chosen parts. */
export function exportText(chosen: ReadonlySet<TransferPart>, parts: ExportParts): string {
  const project = parts.projectFile ? (JSON.parse(parts.projectFile) as Record<string, unknown>) : null;
  const file: Record<string, unknown> = { format: FORMAT, version: VERSION, name: parts.name };
  if (chosen.has('project') && project) file['project'] = project;
  if (chosen.has('stage') && project?.['stage']) file['stage'] = project['stage'];
  if (chosen.has('settings') && parts.settings) file['settings'] = parts.settings;
  if (chosen.has('keyboard') && parts.shortcuts) file['keyboard'] = { shortcuts: parts.shortcuts };
  if (chosen.has('midi') && parts.midi) file['midi'] = parts.midi;
  return JSON.stringify(file, null, 2);
}

/** The name a file of Export gets: after the project, and what it holds. "Concert - project, stage.zip". */
export function exportFileName(chosen: ReadonlySet<TransferPart>, projectName: string): string {
  const parts = TRANSFER_PARTS.filter((part) => chosen.has(part.part)).map((part) => part.part);
  const ofProject = chosen.has('project') || chosen.has('stage');
  const base = (ofProject ? projectName : 'Lumenet').replace(/[\\/:*?"<>|]+/g, '-').trim() || 'Lumenet';
  return `${base} - ${parts.join(', ')}.zip`;
}

/** What a file brought for Import holds, by kind: a ZIP of Export, a JSON file, or the show of a theatre console. */
export type ImportKind = 'zip' | 'json' | 'asc';

/** What a file is, by its name: a ZIP, the show of a theatre console (.asc, .txt), else JSON. */
export function importKind(fileName: string): ImportKind {
  if (/\.zip$/i.test(fileName)) return 'zip';
  if (/\.(asc|txt)$/i.test(fileName)) return 'asc';
  return 'json';
}

/** What a file brings for Import: each part that it has, ready to be taken. */
export interface ImportSource {
  /** What the project is to be called: the name the file gives, else the name of the file. */
  readonly name: string;
  /** The project file to open, as text. */
  readonly project: string | null;
  /** The stage on its own, as text. */
  readonly stage: string | null;
  readonly settings: unknown;
  readonly shortcuts: unknown;
  /** The MIDI setup: an object with the lists "devices" and "mappings", or null. */
  readonly midi: Record<string, unknown> | null;
}

/**
 * What a file chosen for Import has to offer. Throws an error with a message for the user when it
 * is no file of Export and no project file.
 */
export function readImportFile(fileName: string, text: string): ImportSource {
  let file: unknown;
  try {
    file = JSON.parse(text);
  } catch {
    throw new Error(`"${fileName}" is no file of Lumenet: choose one that Export made, or a project file.`);
  }
  const named = fileName.replace(/\.(json|zip)$/i, '');
  if (typeof file !== 'object' || file === null || Array.isArray(file)) throw notOurs(fileName);
  const content = file as Record<string, unknown>;

  if (content['format'] === PROJECT_FORMAT) {
    // A project file: its project and its stage; settings only from an earlier version.
    const earlier = objectOrNull(content['settings']);
    return {
      name: named,
      project: text,
      stage: content['stage'] ? JSON.stringify(content['stage']) : null,
      settings: earlier,
      shortcuts: objectOrNull(earlier?.['shortcuts']),
      midi: null,
    };
  }

  if (content['format'] !== FORMAT) throw notOurs(fileName);
  if (typeof content['version'] !== 'number' || content['version'] > VERSION) {
    throw new Error(`"${fileName}" was made by a newer Lumenet: this one cannot read it.`);
  }
  const project = objectOrNull(content['project']);
  const stage = content['stage'] ?? project?.['stage'];
  return {
    name: typeof content['name'] === 'string' && content['name'].trim() ? content['name'].trim() : named,
    project: project ? JSON.stringify(project) : null,
    stage: stage ? JSON.stringify(stage) : null,
    settings: objectOrNull(content['settings']),
    shortcuts: objectOrNull(objectOrNull(content['keyboard'])?.['shortcuts']),
    midi: midiOrNull(content['midi']),
  };
}

/** The parts a file has, in the order the windows list them. */
export function partsIn(source: ImportSource): ReadonlySet<TransferPart> {
  const has: Record<TransferPart, boolean> = {
    project: source.project !== null,
    stage: source.stage !== null,
    settings: source.settings !== null,
    keyboard: source.shortcuts !== null,
    midi: source.midi !== null,
  };
  return new Set(TRANSFER_PARTS.filter((part) => has[part.part]).map((part) => part.part));
}

function objectOrNull(value: unknown): Record<string, unknown> | null {
  return typeof value === 'object' && value !== null && !Array.isArray(value) ? (value as Record<string, unknown>) : null;
}

// A MIDI setup has its devices and its mappings, each a list.
function midiOrNull(value: unknown): Record<string, unknown> | null {
  const midi = objectOrNull(value);
  return midi && Array.isArray(midi['devices']) && Array.isArray(midi['mappings']) ? midi : null;
}

function notOurs(fileName: string): Error {
  return new Error(`"${fileName}" is no file of Lumenet: choose one that Export made, or a project file.`);
}
