/** How bright a lamp is: 1 = faint, 2 = half, 3 = full. */
export type LampLevel = 1 | 2 | 3;

/** The lamp under a channel fader: full as soon as the channel is above zero. */
export function channelLamp(level: number): LampLevel {
  return level > 0 ? 3 : 1;
}

/** The lamp under a memory fader: full when the fader is up, half when a look is stored, else faint. */
export function memoryLamp(level: number, stored: boolean): LampLevel {
  if (level > 0) return 3;
  return stored ? 2 : 1;
}

/**
 * The lamp of a channel page key: full for the page that is shown,
 * half for another page on which some channel is above zero, else faint.
 * The page holds the channels `first` to `first + count - 1`.
 */
export function channelPageLamp(
  levels: readonly number[],
  first: number,
  count: number,
  selected: boolean,
): LampLevel {
  if (selected) return 3;

  for (let channel = first; channel < first + count; channel++) {
    if (levels[channel - 1] > 0) return 2;
  }
  return 1;
}

/**
 * The lamp of a memory page key: full for the page that is shown,
 * half for another page on which a fader is up or a look is stored, else faint.
 * The page holds the memories `first` to `first + count - 1`.
 */
export function memoryPageLamp(
  levels: readonly number[],
  stored: readonly boolean[],
  first: number,
  count: number,
  selected: boolean,
): LampLevel {
  if (selected) return 3;

  for (let memory = first; memory < first + count; memory++) {
    if (levels[memory - 1] > 0 || stored[memory - 1]) return 2;
  }
  return 1;
}
