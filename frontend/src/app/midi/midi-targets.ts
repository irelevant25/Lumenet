/*
 * The targets of MIDI controls as the MIDI panel lists them: the keys of desk-actions.ts and the
 * faders below, by kind, with what they are called; and the words for controls and for what became
 * of a message. Only the MIDI panel needs them: they stay out of the first download.
 */

import { ACTION_GROUPS, DESK_ACTIONS, DeskAction } from '../desk/desk-actions';
import { MAX_CHANNELS, MEMORY_COUNT } from '../desk/desk-types';
import { MAX_EFFECTS } from '../effects/effects';
import type { MidiMapping, MidiMode, MidiMonitorLine } from './midi';
import { MAX_SOUNDS } from '../sounds/sounds';
import { channelLabel } from '../desk/universes';

// ---------------------------------------------------------------- the faders

/** The kinds of faders in the MIDI panel, after the kinds of keys. */
export const FADER_GROUPS = [
  'Faders of the pages shown',
  'Channel faders',
  'Memory faders',
  'Master and rate',
  'Sequence speeds',
  'Effect faders',
  'Effect speed, size and spread',
  'Sound faders',
] as const;

export type FaderGroup = (typeof FADER_GROUPS)[number];

/** A fader a control can move. */
export interface FaderTarget {
  readonly id: string;
  readonly group: FaderGroup;
  readonly label: string;
}

/** The slots of the pages shown: as many as a page can show of channels, memories and effects. */
export const CHANNEL_SLOTS = 64;
export const MEMORY_SLOTS = 32;
export const EFFECT_SLOTS = 32;

/** Every fader a control can move, kind by kind. */
export const FADER_TARGETS: readonly FaderTarget[] = [
  ...numbered<FaderTarget>(CHANNEL_SLOTS, (slot) => ({
    id: `fader.slot.channel.${slot}`,
    group: 'Faders of the pages shown',
    label: `Channel fader ${slot} of the page shown`,
  })),
  ...numbered<FaderTarget>(MEMORY_SLOTS, (slot) => ({
    id: `fader.slot.memory.${slot}`,
    group: 'Faders of the pages shown',
    label: `Memory fader ${slot} of the page shown`,
  })),
  ...numbered<FaderTarget>(EFFECT_SLOTS, (slot) => ({
    id: `fader.slot.effect.${slot}`,
    group: 'Faders of the pages shown',
    label: `Effect fader ${slot} of the page shown`,
  })),
  ...numbered<FaderTarget>(MAX_CHANNELS, (channel) => ({
    id: `fader.channel.${channel}`,
    group: 'Channel faders',
    label: `Channel fader ${channel}`,
  })),
  ...numbered<FaderTarget>(MEMORY_COUNT, (memory) => ({
    id: `fader.memory.${memory}`,
    group: 'Memory faders',
    label: `Memory fader ${memory}`,
  })),
  { id: 'fader.master', group: 'Master and rate', label: 'Master' },
  { id: 'fader.rate', group: 'Master and rate', label: 'Rate (the tempo)' },
  ...numbered<FaderTarget>(MEMORY_COUNT, (memory) => ({
    id: `fader.sequence.${memory}`,
    group: 'Sequence speeds',
    label: `Speed of the sequence in memory ${memory}`,
  })),
  ...numbered<FaderTarget>(MAX_EFFECTS, (effect) => ({
    id: `fader.effect.${effect}`,
    group: 'Effect faders',
    label: `Effect fader ${effect}`,
  })),
  ...numbered<FaderTarget[]>(MAX_EFFECTS, (effect) => [
    {
      id: `fader.effect-speed.${effect}`,
      group: 'Effect speed, size and spread',
      label: `Live speed of effect ${effect}`,
    },
    {
      id: `fader.effect-size.${effect}`,
      group: 'Effect speed, size and spread',
      label: `Live size of effect ${effect}`,
    },
    {
      id: `fader.effect-spread.${effect}`,
      group: 'Effect speed, size and spread',
      label: `Live spread of effect ${effect}`,
    },
  ]).flat(),
  { id: 'fader.sound-volume', group: 'Sound faders', label: 'Volume of the sound (-100 to 0 dB)' },
  ...numbered<FaderTarget>(MAX_SOUNDS, (sound) => ({
    id: `fader.sound.${sound}`,
    group: 'Sound faders',
    label: `Sound fader ${sound}`,
  })),
];

/** The kind of the keys of the pages shown, which only controllers have. */
export const SLOT_KEY_GROUP = 'Keys of the pages shown';

/** A key that follows the pages shown, as the faders of the pages shown do. */
export interface SlotKeyTarget {
  readonly id: string;
  readonly group: typeof SLOT_KEY_GROUP;
  readonly label: string;
}

/**
 * The keys under the faders of the pages shown ("slot.channel.3", see slotKey in desk-actions.ts):
 * the keys of a controller under its faders press them.
 */
export const SLOT_KEYS: readonly SlotKeyTarget[] = [
  ...numbered<SlotKeyTarget>(CHANNEL_SLOTS, (slot) => ({
    id: `slot.channel.${slot}`,
    group: SLOT_KEY_GROUP,
    label: `Channel key ${slot} of the page shown`,
  })),
  ...numbered<SlotKeyTarget>(MEMORY_SLOTS, (slot) => ({
    id: `slot.memory.${slot}`,
    group: SLOT_KEY_GROUP,
    label: `Memory key ${slot} of the page shown`,
  })),
  ...numbered<SlotKeyTarget>(EFFECT_SLOTS, (slot) => ({
    id: `slot.effect.${slot}`,
    group: SLOT_KEY_GROUP,
    label: `Effect key ${slot} of the page shown`,
  })),
];

/** Every kind of target in the MIDI panel: the keys, as the Keyboard panel has them, those of the pages shown, then the faders. */
export const TARGET_GROUPS: readonly string[] = [...ACTION_GROUPS, SLOT_KEY_GROUP, ...FADER_GROUPS];

const FADERS_BY_ID = new Map<string, { readonly group: string; readonly label: string }>(
  [...FADER_TARGETS, ...SLOT_KEYS].map((target) => [target.id, target]),
);
const KEYS_BY_ID = new Map(DESK_ACTIONS.map((action) => [action.id, action]));

/** True for the name of a fader; false for a key. */
export function isFader(target: string): boolean {
  return target.startsWith('fader.');
}

/**
 * What a target is called: a key as the Keyboard panel calls it ("Channel key 113 (U2)", "Play"),
 * a key of the pages shown and a fader as SLOT_KEYS and FADER_TARGETS do. A name the page does not
 * know is said as it is.
 */
export function targetLabel(target: string): string {
  const channel = /^(fader\.)?channel\.(\d+)$/.exec(target);
  if (channel) return `Channel ${channel[1] ? 'fader' : 'key'} ${channelLabel(Number(channel[2]))}`;
  return FADERS_BY_ID.get(target)?.label ?? KEYS_BY_ID.get(target)?.label ?? target;
}

/** The kind a target belongs to in the MIDI panel. */
export function targetGroup(target: string): string | undefined {
  return (
    FADERS_BY_ID.get(target)?.group ?? (KEYS_BY_ID.get(target) as DeskAction | undefined)?.group
  );
}

/** The control of a mapping, as the panel says it: "Note 56, channel 1", "CC 7 + 39 (14 bits), channel 2". */
export function controlText(mapping: Pick<MidiMapping, 'message' | 'number' | 'channel'>): string {
  const what = {
    note: `Note ${mapping.number}`,
    cc: `CC ${mapping.number}`,
    cc14: `CC ${mapping.number} + ${mapping.number + 32} (14 bits)`,
    pitchbend: 'Pitch Bend',
    program: `Program ${mapping.number}`,
  }[mapping.message];
  return `${what}, channel ${mapping.channel}`;
}

/** The ways a controller (CC) moves a fader, as the panel names them. */
export const MIDI_MODES: readonly { readonly mode: MidiMode; readonly label: string }[] = [
  { mode: 'absolute', label: 'A fader or knob (0-127)' },
  { mode: 'relative-twos', label: 'An endless encoder: 1 up, 127 down' },
  { mode: 'relative-offset', label: 'An endless encoder: 65 up, 63 down' },
  { mode: 'relative-sign', label: 'An endless encoder: 1 up, 65 down' },
];

/** What became of a message that came in, in words: "Memory key 5 pressed". */
export function resultText(line: MidiMonitorLine): string {
  const what = line.target === null ? '' : targetLabel(line.target);
  switch (line.result) {
    case 'unmapped':
      return 'Not mapped';
    case 'down':
      return `${what} pressed`;
    case 'up':
      return `${what} let go`;
    case 'no-page':
      return `${what}: no page of the desk is open on this PC to press it`;
    case 'moved':
      return `${what} to ${line.value}`;
    case 'pickup':
      return `${what} waits until the control reaches it: the control is at ${line.value}, the desk at ${line.desk}`;
    case 'refused':
      return `${what}: ${line.reason ?? 'refused'}`;
    case 'missing':
      return `${what}: there is no such fader now`;
    case 'learned':
      return `Learned for ${what}`;
    case 'learn-wait':
      return `This control cannot work ${what}: move another one`;
    default:
      return line.result;
  }
}

function numbered<T>(count: number, make: (number: number) => T): T[] {
  return Array.from({ length: count }, (_, index) => make(index + 1));
}
