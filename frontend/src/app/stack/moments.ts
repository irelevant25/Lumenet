import type { SoundTime } from '../desk/desk-types';
import type { Sound } from '../sounds/sounds';

/**
 * Moments of a sound, as the stack's cues come at them (TimedStep.at) and as the page writes and reads
 * them: minutes, then seconds to the hundredth, "0:31.25" and "1:15.50".
 */

/** The latest moment there is, as the desk program allows it (SoundTime.MaxSeconds). */
export const MAX_MOMENT = 99_999.99;

/** A moment with its sound: "0:31.25 of Thunder"; a sound that has no name or is gone by its number. */
export function momentWords(at: SoundTime, sounds: ReadonlyMap<number, Sound>): string {
  return `${momentText(at.seconds)} of ${sounds.get(at.sound)?.name || `sound ${at.sound}`}`;
}

/** A moment in words: 31.25 seconds is "0:31.25", 75.5 seconds "1:15.50". */
export function momentText(seconds: number): string {
  const hundredths = Math.max(0, Math.round(seconds * 100));
  const minutes = Math.floor(hundredths / 6000);
  const rest = (hundredths - minutes * 6000) / 100;
  return `${minutes}:${rest.toFixed(2).padStart(5, '0')}`;
}

/**
 * A moment as typed: "1:15.5", "75.5", "1:15,5" (a comma for the dot); null when it is none. After
 * minutes the seconds are under 60: "9:99" is a slip of the finger, not 10:39.
 */
export function parseMoment(text: string): number | null {
  const typed = text.trim().replace(',', '.');
  const match = /^(?:(\d+):)?(\d+(?:\.\d*)?)$/.exec(typed);
  if (!match || (match[1] !== undefined && Number(match[2]) >= 60)) return null;
  const seconds = Number(match[1] ?? 0) * 60 + Number(match[2]);
  return Number.isFinite(seconds) ? Math.round(seconds * 100) / 100 : null;
}
