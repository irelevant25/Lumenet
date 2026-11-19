/**
 * The four memory functions (save, edit, copy, delete) as a small state machine.
 * It only decides what a press means. The desk store carries the commands out.
 */

export type FunctionName = 'save' | 'edit' | 'copy' | 'delete';

export type FunctionMode =
  | { readonly kind: 'idle' }
  /** Save was pressed: the next memory button stores the channel faders there. */
  | { readonly kind: 'save' }
  /** Edit was pressed: waiting for the memory to edit. */
  | { readonly kind: 'edit-pick' }
  /** A memory is loaded into the channel faders. Pressing Edit again stores them back. */
  | { readonly kind: 'editing'; readonly memory: number }
  /** Copy was pressed: waiting for the memory to copy from. */
  | { readonly kind: 'copy-pick' }
  /** The source is chosen: waiting for the memory to copy to. */
  | { readonly kind: 'copy-target'; readonly from: number }
  /** Delete was pressed: waiting for the memory to delete. */
  | { readonly kind: 'delete-pick' };

/** Something the backend has to do as the result of a press. */
export type DeskCommand =
  | { readonly kind: 'saveMemory'; readonly memory: number }
  | { readonly kind: 'loadMemory'; readonly memory: number }
  | { readonly kind: 'copyMemory'; readonly from: number; readonly to: number }
  | { readonly kind: 'deleteMemory'; readonly memory: number };

/** The result of a press: the mode afterwards, and a command if the press completed something. */
export interface Step {
  readonly mode: FunctionMode;
  readonly command?: DeskCommand;
}

export const IDLE: FunctionMode = { kind: 'idle' };

const FIRST_STATE: Record<FunctionName, FunctionMode> = {
  save: { kind: 'save' },
  edit: { kind: 'edit-pick' },
  copy: { kind: 'copy-pick' },
  delete: { kind: 'delete-pick' },
};

/** The function button that is lit in this mode. */
export function activeFunction(mode: FunctionMode): FunctionName | null {
  switch (mode.kind) {
    case 'idle':
      return null;
    case 'save':
      return 'save';
    case 'edit-pick':
    case 'editing':
      return 'edit';
    case 'copy-pick':
    case 'copy-target':
      return 'copy';
    case 'delete-pick':
      return 'delete';
  }
}

/** A function button was pressed. */
export function pressFunction(mode: FunctionMode, pressed: FunctionName): Step {
  // Another function than the lit one: start that one. Whatever was unfinished is dropped.
  if (activeFunction(mode) !== pressed) return { mode: FIRST_STATE[pressed] };

  // Edit pressed a second time finishes the edit: the channel faders go back into the memory.
  if (mode.kind === 'editing') {
    return { mode: IDLE, command: { kind: 'saveMemory', memory: mode.memory } };
  }

  // Any other lit function pressed again is cancelled.
  return { mode: IDLE };
}

/** The button under a memory fader was pressed. `stored` says whether that memory holds a look. */
export function pressMemory(mode: FunctionMode, memory: number, stored: boolean): Step {
  switch (mode.kind) {
    case 'save':
      return { mode: IDLE, command: { kind: 'saveMemory', memory } };
    case 'edit-pick':
      return stored
        ? { mode: { kind: 'editing', memory }, command: { kind: 'loadMemory', memory } }
        : { mode };
    case 'copy-pick':
      return stored ? { mode: { kind: 'copy-target', from: memory } } : { mode };
    case 'copy-target':
      return memory === mode.from
        ? { mode }
        : { mode: IDLE, command: { kind: 'copyMemory', from: mode.from, to: memory } };
    case 'delete-pick':
      return stored ? { mode: IDLE, command: { kind: 'deleteMemory', memory } } : { mode };
    case 'idle':
    case 'editing':
      return { mode }; // outside a function the memory buttons do nothing
  }
}

/** Whether the button of a memory blinks, which means "you can press this one now". */
export function memoryBlinks(mode: FunctionMode, memory: number, stored: boolean): boolean {
  switch (mode.kind) {
    case 'save':
      return true;
    case 'edit-pick':
    case 'copy-pick':
    case 'delete-pick':
      return stored;
    case 'copy-target':
      return memory !== mode.from;
    case 'idle':
    case 'editing':
      return false;
  }
}

/** The memory a function is holding on to: the one being edited, or the one being copied from. */
export function heldMemory(mode: FunctionMode): number | null {
  if (mode.kind === 'editing') return mode.memory;
  if (mode.kind === 'copy-target') return mode.from;
  return null;
}
