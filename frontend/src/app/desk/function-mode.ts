import { StepTarget } from './desk-types';

/**
 * The record keys (save a memory, edit a memory, record a sequence, copy, delete) as a small
 * state machine. It only decides what a press means. The desk store carries the commands out.
 */

export type FunctionName = 'save' | 'edit' | 'record' | 'copy' | 'delete';

/** What a memory holds. */
export type MemoryContent = 'nothing' | 'look' | 'sequence';

/** What Copy and Del work on besides memories: effects and sounds, each onto its own kind. */
export type ItemKind = 'effect' | 'sound';

/**
 * What Copy and Del of the Memories view and of the Sequences view keep to: looks only, or sequences
 * only (each onto one of its kind or an empty memory). On the main view they take any memory.
 */
export type MemoryScope = 'look' | 'sequence';

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
  /** Copy was pressed: waiting for the memory (or effect, or sound) to copy from; with `only`, a look or a sequence. */
  | { readonly kind: 'copy-pick'; readonly only?: MemoryScope }
  /** The source is chosen: waiting for the memory to copy to; with `of`, the effect or sound. */
  | { readonly kind: 'copy-target'; readonly from: number; readonly of?: ItemKind; readonly only?: MemoryScope }
  /** Del was pressed: waiting for the memory (or effect, or sound) to delete; with `only`, a look or a sequence. */
  | { readonly kind: 'delete-pick'; readonly only?: MemoryScope };

/** Something the backend has to do as the result of a press. */
export type DeskCommand =
  | { readonly kind: 'saveMemory'; readonly memory: number }
  | { readonly kind: 'loadMemory'; readonly memory: number }
  | { readonly kind: 'copyMemory'; readonly from: number; readonly to: number }
  | { readonly kind: 'deleteMemory'; readonly memory: number }
  | { readonly kind: 'copyItem'; readonly of: ItemKind; readonly from: number; readonly to: number }
  | { readonly kind: 'deleteItem'; readonly of: ItemKind; readonly number: number }
  /** The fader of a channel or a memory to full, or to zero when it is at full already. */
  | { readonly kind: 'toggleFader'; readonly target: StepTarget; readonly number: number }
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

/**
 * A function key was pressed. `only`: Copy or Del of the Memories view (looks) or of the Sequences
 * view (sequences), which keep to one kind of memory.
 */
export function pressFunction(mode: FunctionMode, pressed: FunctionName, only?: MemoryScope): Step {
  // Another function than the lit one: start that one. Whatever was unfinished is dropped.
  if (activeFunction(mode) !== pressed) {
    const first = FIRST_STATE[pressed];
    const scoped = only && (first.kind === 'copy-pick' || first.kind === 'delete-pick');
    return { mode: scoped ? { ...first, only } : first };
  }

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
      if (holds === 'nothing') return { mode };
      if (mode.only && holds !== mode.only) return { mode, notice: otherKindNotice('Copy', memory, mode.only) };
      return {
        mode: mode.only
          ? { kind: 'copy-target', from: memory, only: mode.only }
          : { kind: 'copy-target', from: memory },
      };

    case 'copy-target':
      if (mode.of) return { mode, notice: copyNotice(mode.of, mode.from) };
      if (memory === mode.from) return { mode };
      if (mode.only && holds !== 'nothing' && holds !== mode.only) {
        return {
          mode,
          notice: `Memory ${memory} holds ${articled(holds)}: ${articled(mode.only)} is copied onto ${articled(mode.only)} or an empty memory.`,
        };
      }
      return { mode: IDLE, command: { kind: 'copyMemory', from: mode.from, to: memory } };

    case 'delete-pick':
      if (holds === 'nothing') return { mode };
      if (mode.only && holds !== mode.only) return { mode, notice: otherKindNotice('Del', memory, mode.only) };
      return { mode: IDLE, command: { kind: 'deleteMemory', memory } };

    case 'editing':
      return { mode }; // the other memory keys wait until the edit is stored with Edit mem

    case 'idle':
      // Outside a function the key works its own fader: to full, and back to zero.
      return { mode, command: { kind: 'toggleFader', target: 'memory', number: memory } };
  }
}

/**
 * The key of an effect or a sound was pressed; `exists` says whether the project has one under that
 * number. Copy and Del work on it as on a memory: Copy onto another number of the same kind (in
 * place of what is there), Del takes it out. Null: no function wants it, and the key starts or
 * stops its effect or sound as always.
 */
export function pressItem(mode: FunctionMode, of: ItemKind, number: number, exists: boolean): Step | null {
  switch (mode.kind) {
    case 'copy-pick':
      return exists ? { mode: { kind: 'copy-target', from: number, of } } : { mode };
    case 'copy-target':
      if (mode.of !== of) return { mode, notice: copyNotice(mode.of, mode.from) };
      return number === mode.from
        ? { mode }
        : { mode: IDLE, command: { kind: 'copyItem', of, from: mode.from, to: number } };
    case 'delete-pick':
      return exists ? { mode: IDLE, command: { kind: 'deleteItem', of, number } } : { mode };
    default:
      return null;
  }
}

/** True while Copy or Del waits for a key: the key of an empty effect or sound is then no choice of the editor. */
export function itemWaits(mode: FunctionMode): boolean {
  return mode.kind === 'copy-pick' || mode.kind === 'copy-target' || mode.kind === 'delete-pick';
}

/** Whether the key of an effect or a sound blinks: Copy or Del waits for it. */
export function itemBlinks(mode: FunctionMode, of: ItemKind, number: number, exists: boolean): boolean {
  switch (mode.kind) {
    case 'copy-pick':
    case 'delete-pick':
      return exists;
    case 'copy-target':
      return mode.of === of && number !== mode.from;
    default:
      return false;
  }
}

/** The effect or sound Copy copies from, while it waits for the number to copy to; else null. */
export function heldItem(mode: FunctionMode, of: ItemKind): number | null {
  return mode.kind === 'copy-target' && mode.of === of ? mode.from : null;
}

// Copy or Del of the Memories view or the Sequences view pressed for a memory of the other kind:
// "Memory 5 holds a sequence: here Copy copies looks. Sequences are on the Sequences view."
function otherKindNotice(key: 'Copy' | 'Del', memory: number, only: MemoryScope): string {
  const other: MemoryScope = only === 'look' ? 'sequence' : 'look';
  const verb = key === 'Copy' ? 'copies' : 'deletes';
  const where = other === 'look' ? 'Looks are on the Memories view' : 'Sequences are on the Sequences view';
  return `Memory ${memory} holds ${articled(other)}: here ${key} ${verb} ${only}s. ${where}.`;
}

// "a look", "a sequence"
function articled(kind: MemoryContent): string {
  return kind === 'nothing' ? 'nothing' : `a ${kind}`;
}

// Copy puts a memory onto a memory, an effect onto an effect, a sound onto a sound.
function copyNotice(of: ItemKind | undefined, from: number): string {
  const kind = of ?? 'memory';
  return `Copy puts ${kind} ${from} onto another ${kind}: press the key of that one.`;
}

/**
 * The key under a channel fader was pressed. While a sequence is recorded it adds the channel as
 * a step; otherwise it works its own fader: to full, and back to zero.
 */
export function pressChannel(mode: FunctionMode, channel: number): Step {
  return mode.kind === 'recording'
    ? { mode, command: { kind: 'addStep', memory: mode.memory, target: 'channel', number: channel } }
    : { mode, command: { kind: 'toggleFader', target: 'channel', number: channel } };
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
      return holds !== 'nothing' && (!mode.only || holds === mode.only);
    case 'copy-target':
      return !mode.of && memory !== mode.from && (!mode.only || holds === 'nothing' || holds === mode.only);
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
  if (mode.kind === 'copy-target' && !mode.of) return mode.from;
  return null;
}
