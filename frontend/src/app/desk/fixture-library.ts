import { FixtureChannel, FixtureRange, StagePlan } from './desk-types';
import { fittingAddress } from './stage-lights';

/**
 * The Open Fixture Library (open-fixture-library.org): thousands of lights, each with the
 * channels of its modes. The page reads it from its copy on GitHub, which every browser may
 * fetch from: the list of makers and the names of the files once a week (kept in the browser),
 * and a light's file when it is chosen. What is chosen goes into the project, so the library is
 * needed only while lights are set up.
 */

const REPOSITORY = 'OpenLightingProject/open-fixture-library';
const FILES = `https://raw.githubusercontent.com/${REPOSITORY}/master/fixtures`;
const TREE = `https://api.github.com/repos/${REPOSITORY}/git/trees/master:fixtures?recursive=1`;
const CACHE_KEY = 'lumenet-fixture-library';
const CACHE_MS = 7 * 24 * 60 * 60 * 1000;

/** The most channels, ranges and characters the desk program keeps (FixtureType there). */
const MAX_RANGES = 128;
const MAX_NAME = 40;
const MAX_TEXT = 80;

/** A light of the library, as the list names it. */
export interface LibraryEntry {
  /** Its file, like "american-dj/inno-pocket-spot". */
  readonly source: string;
  readonly maker: string;
  /** Its name as the file name says it, like "Inno Pocket Spot"; its file has the exact one. */
  readonly model: string;
}

/** A mode of a light of the library, with its channels as the desk keeps them. */
export interface LibraryMode {
  readonly name: string;
  readonly channels: FixtureChannel[];
}

/** A light of the library, read from its file. */
export interface LibraryFixture {
  readonly source: string;
  readonly maker: string;
  readonly model: string;
  readonly categories: readonly string[];
  readonly modes: LibraryMode[];
}

interface CachedIndex {
  readonly fetched: number;
  readonly entries: LibraryEntry[];
}

/**
 * Every light of the library: from the browser's copy when it is younger than a week, or
 * fetched (two requests). Fails with a message when the library cannot be reached.
 */
export async function libraryIndex(fetcher: typeof fetch = fetch, now = Date.now(), refresh = false): Promise<LibraryEntry[]> {
  if (!refresh) {
    const cached = readCache();
    if (cached && now - cached.fetched < CACHE_MS) return cached.entries;
  }

  const [makers, tree] = await Promise.all([
    getJson<Record<string, { name?: string }>>(fetcher, `${FILES}/manufacturers.json`),
    getJson<{ tree?: { path: string; type: string }[] }>(fetcher, TREE),
  ]);
  const entries = (tree.tree ?? [])
    .filter((item) => item.type === 'blob' && /^[a-z0-9-]+\/[a-z0-9-]+\.json$/.test(item.path))
    .map((item) => {
      const source = item.path.slice(0, -'.json'.length);
      const [maker, model] = source.split('/');
      return { source, maker: makers[maker]?.name ?? wordsOf(maker), model: wordsOf(model) };
    })
    .sort((a, b) => a.maker.localeCompare(b.maker) || a.model.localeCompare(b.model));
  writeCache({ fetched: now, entries });
  return entries;
}

/** The lights whose maker and model hold every word of the text, the first 60. */
export function searchLibrary(entries: readonly LibraryEntry[], text: string): LibraryEntry[] {
  const words = text.toLowerCase().split(/\s+/).filter((word) => word !== '');
  if (words.length === 0) return [];
  return entries.filter((entry) => {
    const name = `${entry.maker} ${entry.model} ${entry.source}`.toLowerCase();
    return words.every((word) => name.includes(word));
  }).slice(0, 60);
}

/** Reads a light's file from the library and makes its modes into channel lists. */
export async function libraryFixture(source: string, maker: string, fetcher: typeof fetch = fetch): Promise<LibraryFixture> {
  if (!/^[a-z0-9-]+\/[a-z0-9-]+$/.test(source)) throw new Error(`"${source}" is no light of the library.`);
  let file = await getJson<OflFixture>(fetcher, `${FILES}/${source}.json`);
  let found = source;
  // A light that was renamed, or is sold under another name, points to its file.
  if (file.redirectTo && /^[a-z0-9-]+\/[a-z0-9-]+$/.test(file.redirectTo)) {
    found = file.redirectTo;
    file = await getJson<OflFixture>(fetcher, `${FILES}/${found}.json`);
  }
  return fixtureOf(file, found, maker);
}

/**
 * The plan with a light made a light of the library in one of its modes: its channels are the
 * mode's, it names the kind (which the plan gets once for all lights of that model and mode),
 * its address moves down if the channels do not fit after it, and a light with a name like
 * "Light 5" takes the model's name.
 */
export function withLibraryType(plan: StagePlan, lightId: number, fixture: LibraryFixture, mode: LibraryMode): StagePlan {
  const existing = plan.types.find((type) => type.source === fixture.source && type.mode === mode.name);
  const id = existing?.id ?? Math.max(0, ...plan.types.map((type) => type.id)) + 1;
  const types = existing
    ? plan.types
    : [...plan.types, { id, maker: fixture.maker, model: fixture.model, mode: mode.name, source: fixture.source, channels: mode.channels }];
  const channels = (existing?.channels ?? mode.channels).map((channel) => channel.code).join('');
  return {
    ...plan,
    types,
    lights: plan.lights.map((light) =>
      light.id !== lightId
        ? light
        : {
            ...light,
            type: id,
            channels,
            color: null,
            address: fittingAddress(light.address, channels.length),
            name: /^(Light \d+)?$/.test(light.name) ? fixture.model.slice(0, 24) : light.name,
          },
    ),
  };
}

// ---------------------------------------------------------------- the file of a light

/** What the page reads of a light's file; see the library's fixture format. */
export interface OflFixture {
  name?: string;
  categories?: string[];
  redirectTo?: string;
  availableChannels?: Record<string, OflChannel>;
  templateChannels?: Record<string, OflChannel>;
  matrix?: { pixelCount?: number[]; pixelKeys?: (string | null)[][][]; pixelGroups?: Record<string, unknown> };
  wheels?: Record<string, { slots?: OflSlot[] }>;
  modes?: { name?: string; shortName?: string; channels?: (string | null | OflInsert)[] }[];
}

interface OflChannel {
  name?: string;
  fineChannelAliases?: string[];
  dmxValueResolution?: string;
  capability?: OflCapability;
  capabilities?: OflCapability[];
}

interface OflCapability {
  type: string;
  dmxRange?: [number, number];
  comment?: string;
  color?: string;
  colors?: string[];
  wheel?: string;
  slotNumber?: number;
  shutterEffect?: string;
  effectName?: string;
  effectPreset?: string;
  switchChannels?: Record<string, string | null>;
}

interface OflSlot {
  type: string;
  name?: string;
  colors?: string[];
  resource?: string;
}

interface OflInsert {
  insert: string;
  repeatFor: string | string[];
  channelOrder: 'perPixel' | 'perChannel';
  templateChannels: string[];
}

/** What a channel of a mode is: its definition, and for a fine channel the key of its coarse one. */
interface Resolved {
  readonly name: string;
  readonly channel: OflChannel | null;
  readonly coarse: string | null;
}

/** A light's file made into the desk's kinds of channels, one list per mode. */
export function fixtureOf(file: OflFixture, source: string, maker: string): LibraryFixture {
  const resolve = resolver(file);
  const modes = (file.modes ?? []).map((mode, index) => {
    const keys = (mode.channels ?? []).flatMap((entry) => (entry !== null && typeof entry === 'object' ? expandInsert(file, entry) : [entry]));
    const resolved = keys.map((key) => (key === null ? { name: 'Unused', channel: null, coarse: null } : resolve(key)));
    const channels = resolved.map((channel) => channelOf(channel, resolved, file));
    return { name: clip(mode.name || mode.shortName || `Mode ${index + 1}`, MAX_TEXT), channels };
  });
  return {
    source,
    maker: clip(maker, MAX_TEXT),
    model: clip(file.name || source.split('/')[1], MAX_TEXT),
    categories: file.categories ?? [],
    modes: modes.filter((mode) => mode.channels.length > 0),
  };
}

// Finds the definition of a channel key of a mode: an available channel, a fine channel of one, a
// channel a switching channel stands for, or a template channel made for a pixel.
function resolver(file: OflFixture): (key: string) => Resolved {
  const found = new Map<string, Resolved>();
  const add = (key: string, channel: OflChannel) => {
    found.set(key, { name: channel.name ?? key, channel, coarse: null });
    for (const alias of channel.fineChannelAliases ?? []) found.set(alias, { name: alias, channel, coarse: key });
    for (const capability of capabilitiesOf(channel)) {
      for (const [alias, target] of Object.entries(capability.switchChannels ?? {})) {
        if (!found.has(alias) && target) found.set(alias, { name: alias, channel: file.availableChannels?.[target] ?? null, coarse: null });
      }
    }
  };
  for (const [key, channel] of Object.entries(file.availableChannels ?? {})) add(key, channel);
  const pixels = [...pixelKeys(file), ...Object.keys(file.matrix?.pixelGroups ?? {})];
  for (const [template, channel] of Object.entries(file.templateChannels ?? {})) {
    for (const pixel of pixels) {
      const key = template.replaceAll('$pixelKey', pixel);
      if (found.has(key)) continue; // an available channel overrides the template
      const made: OflChannel = { ...channel, fineChannelAliases: channel.fineChannelAliases?.map((alias) => alias.replaceAll('$pixelKey', pixel)) };
      add(key, made);
    }
  }
  return (key) => found.get(key) ?? { name: key, channel: null, coarse: null };
}

// The channel keys a matrix insert block of a mode stands for.
function expandInsert(file: OflFixture, insert: OflInsert): string[] {
  if (insert.insert !== 'matrixChannels') return [];
  let pixels: string[];
  if (Array.isArray(insert.repeatFor)) pixels = insert.repeatFor;
  else if (insert.repeatFor === 'eachPixelGroup') pixels = Object.keys(file.matrix?.pixelGroups ?? {});
  else if (insert.repeatFor === 'eachPixelABC') pixels = [...pixelKeys(file)].sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
  else pixels = pixelKeys(file, insert.repeatFor.replace('eachPixel', ''));

  const resolved = (template: string, pixel: string) => template.replaceAll('$pixelKey', pixel);
  return insert.channelOrder === 'perChannel'
    ? insert.templateChannels.flatMap((template) => pixels.map((pixel) => resolved(template, pixel)))
    : pixels.flatMap((pixel) => insert.templateChannels.map((template) => resolved(template, pixel)));
}

/**
 * The pixel keys of a matrix, in the order of `axes` ("XYZ": along x first, then y, then z, as a
 * book is read). From `pixelCount` they are numbers along the one axis that has more than one
 * pixel ("1", "2"), else "(x, y)" or "(x, y, z)".
 */
function pixelKeys(file: OflFixture, axes = 'XYZ'): string[] {
  const matrix = file.matrix;
  if (!matrix) return [];
  const cells: { key: string; x: number; y: number; z: number }[] = [];
  if (matrix.pixelKeys) {
    matrix.pixelKeys.forEach((plane, z) => plane.forEach((row, y) => row.forEach((key, x) => key && cells.push({ key, x, y, z }))));
  } else if (matrix.pixelCount) {
    const [nx = 1, ny = 1, nz = 1] = matrix.pixelCount;
    const dimensions = [nx, ny, nz].filter((count) => count > 1).length;
    for (let z = 1; z <= nz; z++) {
      for (let y = 1; y <= ny; y++) {
        for (let x = 1; x <= nx; x++) {
          const key =
            dimensions <= 1 ? `${nx > 1 ? x : ny > 1 ? y : z}`
            : dimensions === 3 ? `(${x}, ${y}, ${z})`
            : nz === 1 ? `(${x}, ${y})`
            : ny === 1 ? `(${x}, ${z})`
            : `(${y}, ${z})`;
          cells.push({ key, x, y, z });
        }
      }
    }
  }
  // The last letter of the axes changes slowest: "XYZ" sorts by z, then y, then x.
  const order = [...axes.toLowerCase()].reverse() as ('x' | 'y' | 'z')[];
  return cells.sort((a, b) => order.reduce((result, axis) => result || a[axis] - b[axis], 0)).map((cell) => cell.key);
}

// A channel of a mode as the desk keeps it: its name, its letter, the channel it is the fine part
// of, and what it does at its values.
function channelOf(resolved: Resolved, mode: readonly Resolved[], file: OflFixture): FixtureChannel {
  const name = clip(resolved.name, MAX_NAME);
  if (resolved.channel === null) return { name, code: 'X', fine: null, ranges: [] };
  if (resolved.coarse !== null) {
    const coarse = mode.findIndex((other) => other.coarse === null && other.channel === resolved.channel);
    return { name, code: coarse >= 0 ? 'F' : 'X', fine: coarse >= 0 ? coarse : null, ranges: [] };
  }
  const capabilities = capabilitiesOf(resolved.channel);
  return { name, code: codeOf(capabilities), fine: null, ranges: rangesOf(resolved.channel, capabilities, file, resolved.name) };
}

function capabilitiesOf(channel: OflChannel): OflCapability[] {
  return channel.capabilities ?? (channel.capability ? [channel.capability] : []);
}

const COLOR_CODES: Readonly<Record<string, string>> = {
  Red: 'R',
  Green: 'G',
  Blue: 'B',
  White: 'W',
  'Warm White': 'W',
  'Cold White': 'W',
  Amber: 'A',
  UV: 'U',
};

// The letter of a channel by what it does: a dimmer (also a shutter that dims), a color, a pan,
// a tilt, a shutter or strobe; X for anything else.
function codeOf(capabilities: readonly OflCapability[]): string {
  const types = new Set(capabilities.map((capability) => capability.type));
  if (types.has('Intensity')) return 'D';
  const color = capabilities.find((capability) => capability.type === 'ColorIntensity')?.color;
  if (color) return COLOR_CODES[color] ?? 'X';
  if (types.has('Pan') || types.has('PanContinuous')) return 'P';
  if (types.has('Tilt') || types.has('TiltContinuous')) return 'T';
  if (types.has('ShutterStrobe') || types.has('StrobeSpeed') || types.has('StrobeDuration')) return 'S';
  return 'X';
}

// What a channel that does several things does at its values; none for one that simply goes
// from 0 to full. Values of a 16-bit channel are given in 16 bits: the coarse channel's are their top part.
function rangesOf(channel: OflChannel, capabilities: readonly OflCapability[], file: OflFixture, key: string): FixtureRange[] {
  if (capabilities.length < 2) return [];
  const bytes =
    channel.dmxValueResolution === '8bit' ? 1
    : channel.dmxValueResolution === '16bit' ? 2
    : channel.dmxValueResolution === '24bit' ? 3
    : 1 + (channel.fineChannelAliases?.length ?? 0);
  const shift = 256 ** (bytes - 1);
  return capabilities
    .filter((capability) => capability.dmxRange)
    .slice(0, MAX_RANGES)
    .map((capability) => {
      const [from, to] = capability.dmxRange as [number, number];
      const slot = slotOf(capability, file, key);
      return {
        from: Math.min(255, Math.max(0, Math.floor(from / shift))),
        to: Math.min(255, Math.max(0, Math.floor(to / shift))),
        label: clip(labelOf(capability, slot), MAX_NAME),
        color: colorOf(capability, slot),
      };
    })
    .map((range) => ({ ...range, to: Math.max(range.from, range.to) }));
}

// The slot of a wheel a capability chooses; for one between two slots, both.
function slotOf(capability: OflCapability, file: OflFixture, key: string): OflSlot[] {
  if (capability.slotNumber === undefined) return [];
  const slots = file.wheels?.[capability.wheel ?? key]?.slots ?? [];
  const number = capability.slotNumber;
  const first = slots[Math.floor(number) - 1];
  const second = Number.isInteger(number) ? undefined : slots[Math.ceil(number) - 1];
  return [first, second].filter((slot): slot is OflSlot => slot !== undefined);
}

function slotName(slot: OflSlot): string {
  if (slot.name) return slot.name;
  if (slot.type === 'Gobo' && slot.resource) return wordsOf(slot.resource.split('/').pop() ?? 'Gobo');
  return slot.type;
}

// What a capability does, in a few words: a slot of a wheel, a shutter effect, an effect, or
// what its type and comment say.
function labelOf(capability: OflCapability, slots: readonly OflSlot[]): string {
  const comment = capability.comment ? ` (${capability.comment})` : '';
  switch (capability.type) {
    case 'WheelSlot':
      return slots.length > 0 ? slots.map(slotName).join(' / ') : `Slot ${capability.slotNumber}`;
    case 'WheelShake':
      return `Shake ${slots.map(slotName).join(' / ')}`.trim();
    case 'WheelRotation':
    case 'WheelSlotRotation':
      return `Rotation${comment}`;
    case 'ShutterStrobe':
      return `${capability.shutterEffect ?? 'Shutter'}${comment}`;
    case 'NoFunction':
      return capability.comment ?? 'No function';
    case 'Effect':
      return capability.effectName ?? capability.effectPreset ?? `Effect${comment}`;
    case 'Maintenance':
    case 'Generic':
    case 'ColorPreset':
      return capability.comment ?? wordsOf(capability.type);
    default:
      return `${wordsOf(capability.type)}${comment}`;
  }
}

// The color a capability gives: a color of a wheel slot, or a preset color.
function colorOf(capability: OflCapability, slots: readonly OflSlot[]): string | null {
  const color =
    capability.type === 'WheelSlot' && slots.length === 1 ? slots[0].colors?.[0]
    : capability.type === 'ColorPreset' ? capability.colors?.[0]
    : undefined;
  return color && /^#[0-9a-f]{6}$/i.test(color) ? color.toLowerCase() : null;
}

/** "inno-pocket-spot" as "Inno Pocket Spot"; "PanTiltSpeed" as "Pan tilt speed". */
function wordsOf(text: string): string {
  if (/[a-z][A-Z]/.test(text)) {
    const words = text.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
    return words.charAt(0).toUpperCase() + words.slice(1);
  }
  return text
    .split(/[-_ ]+/)
    .filter((word) => word !== '')
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(' ');
}

function clip(text: string, length: number): string {
  return text.trim().slice(0, length);
}

async function getJson<T>(fetcher: typeof fetch, url: string): Promise<T> {
  let answer: Response;
  try {
    answer = await fetcher(url);
  } catch {
    throw new Error('The fixture library could not be reached. Is this computer on the internet?');
  }
  if (answer.status === 403 || answer.status === 429) throw new Error('The fixture library asks to wait: try again in an hour.');
  if (!answer.ok) throw new Error(`The fixture library answered ${answer.status}.`);
  return (await answer.json()) as T;
}

function readCache(): CachedIndex | null {
  try {
    const text = localStorage.getItem(CACHE_KEY);
    const cached = text ? (JSON.parse(text) as CachedIndex) : null;
    return cached && Array.isArray(cached.entries) && typeof cached.fetched === 'number' ? cached : null;
  } catch {
    return null;
  }
}

function writeCache(index: CachedIndex): void {
  try {
    localStorage.setItem(CACHE_KEY, JSON.stringify(index));
  } catch {
    // A browser without storage asks the library again next time.
  }
}
