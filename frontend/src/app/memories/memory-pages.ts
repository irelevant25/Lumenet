import { MEMORY_COUNT } from '../desk/desk-types';
import { LampLevel, memoryPageLamp } from '../desk/lamps';

/** One page key of the memories, for example "33-64". */
export interface MemoryPageKey {
  first: number;
  last: number;
  /** True for the page that is shown. */
  shown: boolean;
  lamp: LampLevel;
}

/** No fader counts in these views: a page key is half lit when its page holds a stored memory. */
const NO_LEVELS: readonly number[] = [];

/**
 * The page keys of the views that only look at what the memories hold (Memories, Sequences):
 * full for the page that is shown, half for a page with a stored memory.
 */
export function storedMemoryPages(
  stored: readonly boolean[],
  perPage: number,
  firstShown: number,
): MemoryPageKey[] {
  return Array.from({ length: MEMORY_COUNT / perPage }, (_, page) => {
    const first = page * perPage + 1;
    const shown = first === firstShown;
    return {
      first,
      last: first + perPage - 1,
      shown,
      lamp: memoryPageLamp(NO_LEVELS, stored, first, perPage, shown),
    };
  });
}
