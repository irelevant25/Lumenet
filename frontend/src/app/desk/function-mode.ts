import { StepTarget } from './desk-types';

/**
 * The record keys (save a memory, edit a memory, record a sequence, copy, delete) as a small
 * state machine. It only decides what a press means. The desk store carries the commands out.
 */

export type FunctionName = 'save' | 'edit' | 'record' | 'copy' | 'delete';

/** What a memory holds. */
export type MemoryContent = 'nothing' | 'look' | 'sequence';

export type FunctionMode =
  | { readonly kind: 'idle' }
  /** Save mem was pressed: the next memory key stores the channel faders there. */
  | { readonly kind: 'save' }
  /** Edit mem was pressed: waiting for the memory to edit. Only a look can be edited this way. */
  | { readonly kind: 'edit-pick' }
  /** A memory is loaded into the channel faders. Pressing Edit mem again stores them back. */
  | { readonly kind: 'editing'; readonly memory: number }
  /** Rec seq was pressed: waiting for the memory the sequence goes into. */
  | { readonly kind: 'record-pick' }
  /**
   * The memory is chosen. Every channel key, and every key of a memory that holds a look,
   * adds a step to its sequence. Pressing Rec seq again ends the recording.
   */
  | { readonly kind: 'recording'; readonly memory: number }
  /** Copy was pressed: waiting for the memory to copy from. */
  | { readonly kind: 'copy-pick' }
  /** The source is chosen: waiting for the memory to copy to. */
  | { readonly kind: 'copy-target'; readonly from: number }
  /** Del was pressed: waiting for the memory to delete. */
  | { readonly kind: 'delete-pick' };

/** Something the backend has to do as the result of a press. */
export type DeskCommand =
  | { readonly kind: 'saveMemory'; readonly memory: number }
  | { readonly kind: 'loadMemory'; readonly memory: number }
  | { readonly kind: 'copyMemory'; readonly from: number; readonly to: number }
  | { readonly kind: 'deleteMemory'; readonly memory: number }
  /** One more step at the end of the sequence in `memory`: the channel or memory `number`. */
  | {
      readonly kind: 'addStep';
      readonly memory: number;
      readonly target: StepTarget;
      readonly number: number;
    };

/**
 * The result of a press: the mode afterwards, a command if the press completed something, and
 * a few words for the owner if the press could not do what it looks like it should.
 */
export interface Step {
  readonly mode: FunctionMode;
  readonly command?: DeskCommand;
  readonly notice?: string;
}

export const IDLE: FunctionMode = { kind: 'idle' };

const FIRST_STATE: Record<FunctionName, FunctionMode> = {
  save: { kind: 'save' },
  edit: { kind: 'edit-pick' },
  record: { kind: 'record-pick' },
  copy: { kind: 'copy-pick' },
  delete: { kind: 'delete-pick' },
};

/** The function key that is lit in this mode. */
export function activeFunction(mode: FunctionMode): FunctionName | null {
  switch (mode.kind) {
    case 'idle':
      return null;
    case 'save':
      return 'save';
    case 'edit-pick':
    case 'editing':
      return 'edit';
    case 'record-pick':
    case 'recording':
      return 'record';
    case 'copy-pick':
    case 'copy-target':
      return 'copy';
    case 'delete-pick':
      return 'delete';
  }
}

/**
 * Whether the lit function key blinks: pressing it again ends something that is going on
 * (storing the edited faders back, or the recording), instead of cancelling.
 */
export function functionBlinks(mode: FunctionMode): boolean {
  return mode.kind === 'editing' || mode.kind === 'recording';
}

/** A function key was pressed. */
export function pressFunction(mode: FunctionMode, pressed: FunctionName): Step {
  // Another function than the lit one: start that one. Whatever was unfinished is dropped.
  if (activeFunction(mode) !== pressed) return { mode: FIRST_STATE[pressed] };

  // Edit mem pressed a second time finishes the edit: the channel faders go back into the memory.
  if (mode.kind === 'editing') {
    return { mode: IDLE, command: { kind: 'saveMemory', memory: mode.memory } };
  }

  // Any other lit function pressed again is over. For a recording that is its regular end:
  // the steps went into the sequence one by one, so nothing is left to do.
  return { mode: IDLE };
}

/** The key under a memory fader was pressed. `holds` says what that memory holds. */
export function pressMemory(mode: FunctionMode, memory: number, holds: MemoryContent): Step {
  switch (mode.kind) {
    case 'save':
      return { mode: IDLE, command: { kind: 'saveMemory', memory } };

    case 'edit-pick':
      if (holds === 'look') {
        return { mode: { kind: 'editing', memory }, command: { kind: 'loadMemory', memory } };
      }
      return holds === 'sequence'
        ? { mode, notice: `Memory ${memory} holds a sequence. Change it on the Sequences page.` }
        : { mode };

    case 'record-pick':
      return holds === 'look'
        ? { mode, notice: `Memory ${memory} holds a look. Empty it with Del first, or choose another memory.` }
        : { mode: { kind: 'recording', memory } };

    case 'recording':
      if (memory === mode.memory) {
        return { mode, notice: `Memory ${memory} is the one being recorded into. Rec seq ends the recording.` };
      }
      if (holds === 'look') {
        return { mode, command: { kind: 'addStep', memory: mode.memory, target: 'memory', number: memory } };
      }
      return holds === 'sequence'
        ? { mode, notice: `Memory ${memory} holds a sequence, and a sequence cannot be a step of another one.` }
        : { mode, notice: `Memory ${memory} is empty: there is nothing a step could play.` };

    case 'copy-pick':
      return holds !== 'nothing' ? { mode: { kind: 'copy-target', from: memory } } : { mode };

    case 'copy-target':
      return memory === mode.from
        ? { mode }
        : { mode: IDLE, command: { kind: 'copyMemory', from: mode.from, to: memory } };

    case 'delete-pick':
      return holds !== 'nothing' ? { mode: IDLE, command: { kind: 'deleteMemory', memory } } : { mode };

    case 'idle':
    case 'editing':
      return { mode }; // outside a function the memory keys do nothing
  }
}

/** The key under a channel fader was pressed. It does something only while a sequence is recorded. */
export function pressChannel(mode: FunctionMode, channel: number): Step {
  return mode.kind === 'recording'
    ? { mode, command: { kind: 'addStep', memory: mode.memory, target: 'channel', number: channel } }
    : { mode };
}

/** Whether the key of a memory blinks, which means "you can press this one now". */
export function memoryBlinks(mode: FunctionMode, memory: number, holds: MemoryContent): boolean {
  switch (mode.kind) {
    case 'save':
      return true;
    case 'edit-pick':
      return holds === 'look';
    case 'record-pick':
      return holds !== 'look';
    case 'recording':
      return holds === 'look' && memory !== mode.memory;
    case 'copy-pick':
    case 'delete-pick':
      return holds !== 'nothing';
    case 'copy-target':
      return memory !== mode.from;
    case 'idle':
    case 'editing':
      return false;
  }
}

/** Whether the channel keys blink and can be pressed: while a sequence is recorded. */
export function channelKeysArmed(mode: FunctionMode): boolean {
  return mode.kind === 'recording';
}

/**
 * The memory a function is holding on to: the one being edited, the one being copied from, or
 * the one a sequence is recorded into.
 */
export function heldMemory(mode: FunctionMode): number | null {
  if (mode.kind === 'editing' || mode.kind === 'recording') return mode.memory;
  if (mode.kind === 'copy-target') return mode.from;
  return null;
}
