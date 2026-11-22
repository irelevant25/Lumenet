import type { FixtureChannel, FixtureRange } from './desk-types';
import type { LibraryEntry, LibraryFixture } from './fixture-library';

/**
 * The fixture library of QLC+ (qlcplus.org), the open lighting desk, which the page reads besides
 * the Open Fixture Library: about 1,800 lights, each in a file of its own (.qxf, XML) in its
 * repository on GitHub, under the Apache 2.0 license. Its list of makers and models is one small
 * file (FixturesMap.xml); a light's file is read when it is chosen. The desk makes of it what it
 * makes of a light of the other library: its modes, each a list of channels with the desk's
 * letters and what they do at their values.
 */

const FILES = 'https://raw.githubusercontent.com/mcallegari/qlcplus/master/resources/fixtures';

/** The list of the library: every maker, with the file and the model of each of its lights. */
export const QLC_MAP = `${FILES}/FixturesMap.xml`;

/** How a light of this library names its file, in front of it: "qlcplus:American_DJ/American-DJ-Dotz-Bar-1.4.qxf". */
export const QLC_PREFIX = 'qlcplus:';

/** A file of this library: a folder (the maker) and a name, with only the signs the library uses, each starting with a letter or digit. */
const SOURCE = /^qlcplus:([A-Za-z0-9][A-Za-z0-9_.()+&,-]*)\/([A-Za-z0-9][A-Za-z0-9_.()+&,-]*\.qxf)$/;

/** The most channels, ranges and characters the desk program keeps (FixtureType there). */
const MAX_RANGES = 128;
const MAX_NAME = 40;
const MAX_TEXT = 80;

/**
 * The lights of the list (FixturesMap.xml). It names each maker as its folder ("American_DJ"): the
 * page shows the name with spaces.
 */
export function qlcEntries(mapXml: string): LibraryEntry[] {
  const map = parseXml(mapXml, 'The list of the QLC+ library');
  const entries: LibraryEntry[] = [];
  for (const maker of elements(map.documentElement, 'M')) {
    const folder = (maker.getAttribute('n') ?? '').trim();
    for (const light of elements(maker, 'F')) {
      const file = light.getAttribute('n');
      const model = light.getAttribute('m');
      const source = `${QLC_PREFIX}${folder}/${file}.qxf`;
      if (!file || !model || !SOURCE.test(source)) continue;
      entries.push({ library: 'qlcplus', source, maker: folder.replaceAll('_', ' '), model: model.trim() });
    }
  }
  return entries;
}

/** Where the file of a light of this library is; throws for anything that is no such light. */
export function qlcFileAddress(source: string): string {
  const parts = SOURCE.exec(source);
  if (!parts) throw new Error(`"${source}" is no light of the library.`);
  return `${FILES}/${encodeURIComponent(parts[1])}/${encodeURIComponent(parts[2])}`;
}

// ---------------------------------------------------------------- the file of a light

/** What the desk reads of a channel of the file. */
interface QlcChannel {
  readonly name: string;
  /** What the channel is, as QLC+ names it ("IntensityRed", "PositionPanFine"); empty in older files. */
  readonly preset: string;
  /** Older files: the group of the channel ("Intensity", "Pan") and its byte (1 for the fine part). */
  readonly group: string;
  readonly byte: number;
  /** Older files: the color of an intensity channel ("Red"). */
  readonly colour: string;
  readonly capabilities: readonly QlcCapability[];
}

interface QlcCapability {
  readonly min: number;
  readonly max: number;
  readonly preset: string;
  readonly text: string;
  readonly color: string | null;
}

// The desk's letter for a channel by what QLC+ says it is; a fine channel by the one it is the fine part of.
const PRESET_CODES: Readonly<Record<string, string>> = {
  IntensityMasterDimmer: 'D',
  IntensityDimmer: 'D',
  IntensityRed: 'R',
  IntensityGreen: 'G',
  IntensityBlue: 'B',
  IntensityWhite: 'W',
  IntensityAmber: 'A',
  IntensityUV: 'U',
  PositionPan: 'P',
  PositionTilt: 'T',
  ShutterStrobeSlowFast: 'S',
  ShutterStrobeFastSlow: 'S',
};

const COLOUR_CODES: Readonly<Record<string, string>> = { Red: 'R', Green: 'G', Blue: 'B', White: 'W', Amber: 'A', UV: 'U' };

/** A light's file made into the desk's kinds of channels, one list per mode. Throws when it cannot be read. */
export function qlcFixtureOf(xml: string, source: string): LibraryFixture {
  const root = parseXml(xml, 'The file of the light').documentElement;
  const definitions = new Map<string, QlcChannel>();
  for (const channel of elements(root, 'Channel')) {
    const read = channelOf(channel);
    if (read.name && !definitions.has(read.name)) definitions.set(read.name, read);
  }

  const modes = elements(root, 'Mode').map((mode, index) => {
    const names = elements(mode, 'Channel')
      .map((entry) => ({ number: Number(entry.getAttribute('Number') ?? 0), name: (entry.textContent ?? '').trim() }))
      .sort((a, b) => a.number - b.number)
      .map((entry) => entry.name);
    const resolved = names.map((name) => definitions.get(name) ?? null);
    const channels = resolved.map((channel, at) => deskChannel(channel, names[at], resolved));
    return { name: clip(mode.getAttribute('Name') || `Mode ${index + 1}`, MAX_TEXT), channels };
  });

  const type = textOf(root, 'Type');
  return {
    library: 'qlcplus',
    source,
    maker: clip(textOf(root, 'Manufacturer') || source.slice(QLC_PREFIX.length).split('/')[0].replaceAll('_', ' '), MAX_TEXT),
    model: clip(textOf(root, 'Model') || 'Light', MAX_TEXT),
    categories: type ? [type] : [],
    modes: modes.filter((mode) => mode.channels.length > 0),
  };
}

function channelOf(element: Element): QlcChannel {
  const group = elements(element, 'Group')[0];
  return {
    name: (element.getAttribute('Name') ?? '').trim(),
    preset: element.getAttribute('Preset') ?? '',
    group: (group?.textContent ?? '').trim(),
    byte: Number(group?.getAttribute('Byte') ?? 0),
    colour: textOf(element, 'Colour'),
    capabilities: elements(element, 'Capability').map((capability) => {
      // A color for a slot of a wheel or a macro: Res1 in newer files, Color in older ones; none for two colors.
      const second = capability.getAttribute('Res2') ?? capability.getAttribute('Color2');
      const first = capability.getAttribute('Res1') ?? capability.getAttribute('Color');
      return {
        min: Number(capability.getAttribute('Min') ?? 0),
        max: Number(capability.getAttribute('Max') ?? 0),
        preset: capability.getAttribute('Preset') ?? '',
        text: (capability.textContent ?? '').trim(),
        color: !second && first && /^#[0-9a-f]{6}$/i.test(first) ? first.toLowerCase() : null,
      };
    }),
  };
}

// True for the fine part of another channel (the second byte of 16 bits).
function isFine(channel: QlcChannel): boolean {
  return channel.preset ? /.Fine$/.test(channel.preset) : channel.byte === 1;
}

// What a channel and the one it is the fine part of have alike.
function kindOf(channel: QlcChannel): string {
  return channel.preset ? `preset ${channel.preset.replace(/Fine$/, '')}` : `group ${channel.group} ${channel.colour}`;
}

function codeOf(channel: QlcChannel): string {
  if (channel.preset) return PRESET_CODES[channel.preset] ?? 'X';
  switch (channel.group) {
    case 'Intensity':
      return COLOUR_CODES[channel.colour] ?? (channel.colour === '' || channel.colour === 'Generic' ? 'D' : 'X');
    case 'Pan':
      return 'P';
    case 'Tilt':
      return 'T';
    case 'Shutter':
      return 'S';
    default:
      return 'X';
  }
}

// A channel of a mode as the desk keeps it. A name the file does not define is a channel of no kind.
function deskChannel(channel: QlcChannel | null, name: string, mode: readonly (QlcChannel | null)[]): FixtureChannel {
  if (channel === null) return { name: clip(name || 'Unknown', MAX_NAME), code: 'X', fine: null, ranges: [] };
  if (isFine(channel)) {
    const coarse = mode.findIndex((other) => other !== null && !isFine(other) && kindOf(other) === kindOf(channel));
    return { name: clip(channel.name, MAX_NAME), code: coarse >= 0 ? 'F' : 'X', fine: coarse >= 0 ? coarse : null, ranges: [] };
  }
  return { name: clip(channel.name, MAX_NAME), code: codeOf(channel), fine: null, ranges: rangesOf(channel) };
}

// What a channel that does several things does at its values; none for one that goes from 0 to full.
function rangesOf(channel: QlcChannel): FixtureRange[] {
  if (channel.capabilities.length < 2) return [];
  return channel.capabilities
    .slice(0, MAX_RANGES)
    .map((capability) => {
      const from = Math.min(255, Math.max(0, Math.floor(capability.min)));
      const to = Math.min(255, Math.max(0, Math.floor(capability.max)));
      return { from, to: Math.max(from, to), label: clip(labelOf(capability), MAX_NAME), color: capability.color };
    })
    .sort((a, b) => a.from - b.from);
}

// What a capability does. An open shutter is said with "Open" first, a closed one with "Closed":
// the desk opens the shutter of a light that a step lights up by those words.
function labelOf(capability: QlcCapability): string {
  const text = capability.text || wordsOf(capability.preset) || 'Range';
  if (capability.preset === 'ShutterOpen' || /^(shutter\s+)?open\b/i.test(text)) return said(text, 'Open', /^(shutter\s+)?open$/i);
  if (capability.preset === 'ShutterClose' || /^(shutter\s+)?closed?\b/i.test(text)) return said(text, 'Closed', /^(shutter\s+)?closed?$/i);
  return text;
}

// A text that says what `word` says: the word alone, the text when it starts with the word, else the word with the text.
function said(text: string, word: string, only: RegExp): string {
  if (only.test(text)) return word;
  return text.toLowerCase().startsWith(word.toLowerCase()) ? text : `${word} (${text})`;
}

/** "PositionPan" as "Position pan". */
function wordsOf(text: string): string {
  const words = text.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}

function parseXml(xml: string, what: string): Document {
  const document = new DOMParser().parseFromString(xml, 'application/xml');
  if (document.getElementsByTagName('parsererror').length > 0 || !document.documentElement) throw new Error(`${what} cannot be read.`);
  return document;
}

// The elements of this name right under an element, whatever namespace the file gives them.
function elements(parent: Element, name: string): Element[] {
  return Array.from(parent.children).filter((child) => child.localName === name);
}

function textOf(parent: Element, name: string): string {
  return (elements(parent, name)[0]?.textContent ?? '').trim();
}

function clip(text: string, length: number): string {
  return text.trim().slice(0, length);
}
