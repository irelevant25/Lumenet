/**
 * Export and Import: the parts of the desk that go into a file on this device and come back from
 * one. One file holds the parts that were chosen: the project (as its project file says it), the
 * stage on its own, the settings of this device, and its keyboard shortcuts.
 *
 * ```json
 * {
 *   "format": "lumenet-export",
 *   "version": 1,
 *   "name": "Concert",
 *   "project": { "format": "lumenet-project", "version": 4, ... },
 *   "stage": { "width": 16, "depth": 10, "lights": [ ... ] },
 *   "settings": { "channelSize": "auto", "fontScale": 1, "colors": {}, ... },
 *   "keyboard": { "shortcuts": { "stack.play": "Space" } }
 * }
 * ```
 *
 * Import also takes a project file as the projects folder has it: its project, and its stage. One
 * that an earlier version wrote can also hold "settings" with the shortcuts in them, which were the
 * project's then: they can be taken as the settings and the keyboard of this device.
 */

export type TransferPart = 'project' | 'stage' | 'settings' | 'keyboard';

/** The parts, in the order the windows of Export and Import list them. */
export const TRANSFER_PARTS: readonly { readonly part: TransferPart; readonly label: string; readonly hint: string }[] = [
  { part: 'project', label: 'Project', hint: 'everything the project holds: memories, sequences, the stack, the stage and the notes' },
  { part: 'stage', label: 'Stage', hint: 'the stage alone: its lights, their kinds, the groups and the picture' },
  { part: 'settings', label: 'Settings', hint: 'the settings of this device: layout, lettering, colors of the keys, views' },
  { part: 'keyboard', label: 'Keyboard', hint: 'the keyboard shortcuts of this device' },
];

const FORMAT = 'lumenet-export';
const VERSION = 1;
const PROJECT_FORMAT = 'lumenet-project';

/** What a file of Export is made of: each part as the desk has it. */
export interface ExportParts {
  /** The name of the project. */
  readonly name: string;
  /** The project file, as the desk program writes it; needed for the project and for the stage. */
  readonly projectFile?: string;
  readonly settings?: unknown;
  readonly shortcuts?: Readonly<Record<string, string>>;
}

/** The text of a file of Export with the chosen parts. */
export function exportText(chosen: ReadonlySet<TransferPart>, parts: ExportParts): string {
  const project = parts.projectFile ? (JSON.parse(parts.projectFile) as Record<string, unknown>) : null;
  const file: Record<string, unknown> = { format: FORMAT, version: VERSION, name: parts.name };
  if (chosen.has('project') && project) file['project'] = project;
  if (chosen.has('stage') && project?.['stage']) file['stage'] = project['stage'];
  if (chosen.has('settings') && parts.settings) file['settings'] = parts.settings;
  if (chosen.has('keyboard') && parts.shortcuts) file['keyboard'] = { shortcuts: parts.shortcuts };
  return JSON.stringify(file, null, 2);
}

/** The name a file of Export gets: after the project, and what it holds. "Concert - project, stage.json". */
export function exportFileName(chosen: ReadonlySet<TransferPart>, projectName: string): string {
  const parts = TRANSFER_PARTS.filter((part) => chosen.has(part.part)).map((part) => part.part);
  const ofProject = chosen.has('project') || chosen.has('stage');
  const base = (ofProject ? projectName : 'Lumenet').replace(/[\\/:*?"<>|]+/g, '-').trim() || 'Lumenet';
  return `${base} - ${parts.join(', ')}.json`;
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
  const named = fileName.replace(/\.json$/i, '');
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
  };
}

/** The chosen parts the file does not have, as "the stage and the keyboard"; empty when it has all. */
export function missingParts(chosen: ReadonlySet<TransferPart>, source: ImportSource): string {
  const has: Record<TransferPart, boolean> = {
    project: source.project !== null,
    stage: source.stage !== null,
    settings: source.settings !== null,
    keyboard: source.shortcuts !== null,
  };
  const missing = TRANSFER_PARTS.filter((part) => chosen.has(part.part) && !has[part.part]).map((part) => `the ${part.part}`);
  return missing.length <= 1 ? (missing[0] ?? '') : `${missing.slice(0, -1).join(', ')} and ${missing.at(-1)}`;
}

function objectOrNull(value: unknown): Record<string, unknown> | null {
  return typeof value === 'object' && value !== null && !Array.isArray(value) ? (value as Record<string, unknown>) : null;
}

function notOurs(fileName: string): Error {
  return new Error(`"${fileName}" is no file of Lumenet: choose one that Export made, or a project file.`);
}
