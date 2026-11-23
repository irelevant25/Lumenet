import { MAX_UNIVERSES, UNIVERSE_SIZE } from './desk-types';

/*
 * Channels are numbered 1 to 512 in every universe, as on a DMX cable, and every channel says its
 * universe: channel 113 of universe 2 is "113 (U2)". Inside, the page and the desk count the
 * channels on through the universes (113 of U2 is 625); that number is never shown.
 */

/** The universe a channel is in: 1 for channels 1 to 512, 2 for 513 to 1024, and on. */
export function universeOf(channel: number): number {
  return Math.floor((channel - 1) / UNIVERSE_SIZE) + 1;
}

/** The first channel of a universe: 1, 513, 1025 ... */
export function universeStart(universe: number): number {
  return (universe - 1) * UNIVERSE_SIZE + 1;
}

/** The number of a channel in its universe, 1 to 512: 113 for channel 625. */
export function channelNumber(channel: number): number {
  return channel - universeStart(universeOf(channel)) + 1;
}

/** A channel as the page writes it: "113 (U2)". */
export function channelLabel(channel: number): string {
  return `${channelNumber(channel)} (U${universeOf(channel)})`;
}

/** Channels one after the other in one universe: "113 to 116 (U2)", or "113 (U2)" for one. */
export function channelSpan(first: number, last: number): string {
  const from = channelNumber(first);
  return last === first ? channelLabel(first) : `${from} to ${from + last - first} (U${universeOf(first)})`;
}

/**
 * A channel as a field shows it for typing: "113 U2" while the project has more than one
 * universe, "113" in a project of one. channelFromText reads it back.
 */
export function channelField(channel: number, universes: number): string {
  const number = channelNumber(channel);
  return universes > 1 ? `${number} U${universeOf(channel)}` : String(number);
}

/**
 * The channel a typed text names, or null for a text that names none of the `count` channels of
 * the project. The number is 1 to 512, in a universe: "113 U2", "113 (U2)", "U2 113", "U2.113"
 * or "U2/113" are all channel 113 of U2. A number alone is in `universe`, the universe the field
 * showed before. Upper and lower case and spaces do not matter.
 */
export function channelFromText(text: string, count: number, universe = 1): number | null {
  const typed = text.trim().toUpperCase();
  let channel: number | null = null;
  const alone = /^(\d+)$/.exec(typed);
  const after = /^(\d+)\s*\(?\s*U\s*(\d+)\s*\)?$/.exec(typed);
  const before = /^U\s*(\d+)\s*[\s.:/-]\s*(\d+)$/.exec(typed);
  if (alone) channel = inUniverse(universe, Number(alone[1]));
  else if (after) channel = inUniverse(Number(after[2]), Number(after[1]));
  else if (before) channel = inUniverse(Number(before[1]), Number(before[2]));
  return channel !== null && channel <= count ? channel : null;
}

// The channel of an address 1 to 512 in a universe; null when either is out of range.
function inUniverse(universe: number, address: number): number | null {
  if (universe < 1 || universe > MAX_UNIVERSES || address < 1 || address > UNIVERSE_SIZE) return null;
  return universeStart(universe) + address - 1;
}
