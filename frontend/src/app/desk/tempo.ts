/*
 * The tempo of the rate (RATE and its fader in the general section): beats per minute. While RATE
 * is lit every sequence takes one step per beat (the desk program does that); TEMPO, tapped in
 * time with the music, sets it.
 */

/** The slowest, the fastest and the first tempo of the rate, in beats per minute. */
export const MIN_BPM = 30;
export const MAX_BPM = 300;
export const DEFAULT_BPM = 120;

/** A pause longer than this between two taps of TEMPO starts the count afresh: 30 beats a minute. */
export const TAP_PAUSE_MS = 2000;

/** The taps that count: the last five, so the last four beats between them. */
export const TAPS_KEPT = 5;

/** A tempo in whole beats per minute, within its range. */
export function roundedBpm(bpm: number): number {
  return Math.min(MAX_BPM, Math.max(MIN_BPM, Math.round(bpm)));
}

/**
 * The taps of TEMPO that count, with one more at `now` (milliseconds): after a pause longer than
 * TAP_PAUSE_MS it is the first again, and only the last TAPS_KEPT count.
 */
export function withTap(taps: readonly number[], now: number): number[] {
  const last = taps.at(-1);
  const counted =
    last !== undefined && now > last && now - last <= TAP_PAUSE_MS ? [...taps, now] : [now];
  return counted.slice(-TAPS_KEPT);
}

/** The tempo the taps give: their average beat, in whole beats per minute; null before the second tap. */
export function tempoOfTaps(taps: readonly number[]): number | null {
  if (taps.length < 2) return null;
  const beat = (taps[taps.length - 1] - taps[0]) / (taps.length - 1);
  return beat > 0 ? roundedBpm(60000 / beat) : null;
}
