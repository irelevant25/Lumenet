import { StackStep } from '../desk/desk-types';

/** What a place of the wheel shows: the step that is on, the one before it, the one after it, or another. */
export type WheelKind = 'previous' | 'current' | 'next' | 'other';

/** One place of the stack wheel. */
export interface WheelPlace {
  /** Where it stands from the middle: -1 just above, 1 just below. */
  readonly offset: number;
  readonly kind: WheelKind;
  /** The step standing there; null for an empty place. */
  readonly step: StackStep | null;
}

/**
 * The places of the stack wheel on the main view, from the top: the step that is on in the
 * middle, the steps before it above, the steps after it below, in the order of the stack.
 *
 * - `size` places are seen (an odd number); `extra` more on each side are made for the wheel to
 *   roll in from, see StackSection.
 * - The stack goes round (after the last step comes the first, as Play does), and so does the
 *   wheel, but no step stands in it twice: a short stack leaves the outer places empty. Only the
 *   step before the current one and the one after it are always there, so that with two steps
 *   the other one stands both above and below, as Prev and Play both go to it.
 * - While no step is on, the middle stays empty and the step Play goes to stands under it, with
 *   the ones after it; nothing stands above.
 *
 * `currentIndex` and `nextIndex` are places in `steps`, -1 for none.
 */
export function wheelPlaces(
  steps: readonly StackStep[],
  currentIndex: number,
  nextIndex: number,
  size: number,
  extra = 0,
): WheelPlace[] {
  const reach = (size - 1) / 2 + extra;
  const count = steps.length;
  const standing = new Map<number, StackStep>();

  if (currentIndex >= 0 && currentIndex < count) {
    const shown = new Set([currentIndex]);
    standing.set(0, steps[currentIndex]);
    for (let distance = 1; distance <= reach; distance++) {
      for (const offset of [distance, -distance]) {
        // The steps after the current one come first: what comes next matters more in a show.
        const index = (((currentIndex + offset) % count) + count) % count;
        if ((distance === 1 && count > 1) || !shown.has(index)) {
          standing.set(offset, steps[index]);
          shown.add(index);
        }
      }
    }
  } else if (count > 0) {
    const first = nextIndex >= 0 && nextIndex < count ? nextIndex : 0;
    for (let offset = 1; offset <= Math.min(reach, count); offset++) {
      standing.set(offset, steps[(first + offset - 1) % count]);
    }
  }

  const places: WheelPlace[] = [];
  for (let offset = -reach; offset <= reach; offset++) {
    places.push({ offset, kind: kindAt(offset), step: standing.get(offset) ?? null });
  }
  return places;
}

function kindAt(offset: number): WheelKind {
  if (offset === 0) return 'current';
  if (offset === -1) return 'previous';
  return offset === 1 ? 'next' : 'other';
}

/**
 * How far the wheel rolls when the step that is on changes from `before` to `after` (places in a
 * stack of `count` steps, -1 for none): the shorter way round, positive when it rolls up (on to
 * the steps after). 0 when there is nothing to roll: no step on now, or the same one.
 */
export function wheelRoll(before: number, after: number, count: number): number {
  if (after < 0 || count === 0 || before === after) return 0;
  if (before < 0) return 1; // from "nothing on" to the first step Play started: one place up
  let roll = (((after - before) % count) + count) % count;
  if (roll > count / 2) roll -= count;
  return roll;
}
