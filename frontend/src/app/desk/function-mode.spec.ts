import {
  FunctionMode,
  IDLE,
  activeFunction,
  channelKeysArmed,
  functionBlinks,
  heldItem,
  heldMemory,
  itemBlinks,
  itemWaits,
  memoryBlinks,
  pressChannel,
  pressFunction,
  pressItem,
  pressMemory,
} from './function-mode';

describe('the record keys', () => {
  it('outside a function the memory keys and the channel keys work their own faders', () => {
    expect(pressMemory(IDLE, 5, 'look')).toEqual({
      mode: IDLE,
      command: { kind: 'toggleFader', target: 'memory', number: 5 },
    });
    expect(pressMemory(IDLE, 6, 'nothing')).toEqual({
      mode: IDLE,
      command: { kind: 'toggleFader', target: 'memory', number: 6 },
    });
    expect(pressChannel(IDLE, 5)).toEqual({
      mode: IDLE,
      command: { kind: 'toggleFader', target: 'channel', number: 5 },
    });
    expect(memoryBlinks(IDLE, 5, 'look')).toBe(false);
    expect(channelKeysArmed(IDLE)).toBe(false);
    expect(activeFunction(IDLE)).toBeNull();
  });

  it('save: every key blinks, and the pressed one stores the channel faders', () => {
    const { mode } = pressFunction(IDLE, 'save');
    expect(activeFunction(mode)).toBe('save');
    expect(memoryBlinks(mode, 1, 'nothing')).toBe(true);
    expect(memoryBlinks(mode, 2, 'look')).toBe(true);
    expect(memoryBlinks(mode, 3, 'sequence')).toBe(true);

    expect(pressMemory(mode, 7, 'nothing')).toEqual({
      mode: IDLE,
      command: { kind: 'saveMemory', memory: 7 },
    });
  });

  it('edit: only keys of looks blink, the pressed one is loaded, and Edit again stores it back', () => {
    const picking = pressFunction(IDLE, 'edit').mode;
    expect(memoryBlinks(picking, 1, 'look')).toBe(true);
    expect(memoryBlinks(picking, 2, 'nothing')).toBe(false);
    expect(memoryBlinks(picking, 3, 'sequence')).toBe(false);
    expect(pressMemory(picking, 2, 'nothing')).toEqual({ mode: picking }); // an empty memory cannot be edited

    // a sequence is not edited with the faders: the press says where it is changed
    const onSequence = pressMemory(picking, 3, 'sequence');
    expect(onSequence.mode).toBe(picking);
    expect(onSequence.command).toBeUndefined();
    expect(onSequence.notice).toContain('Memory 3 holds a sequence');

    const loaded = pressMemory(picking, 1, 'look');
    expect(loaded).toEqual({
      mode: { kind: 'editing', memory: 1 },
      command: { kind: 'loadMemory', memory: 1 },
    });
    expect(activeFunction(loaded.mode)).toBe('edit');
    expect(functionBlinks(loaded.mode)).toBe(true);
    expect(heldMemory(loaded.mode)).toBe(1);
    expect(memoryBlinks(loaded.mode, 1, 'look')).toBe(false);
    expect(pressMemory(loaded.mode, 3, 'look')).toEqual({ mode: loaded.mode }); // other keys wait

    expect(pressFunction(loaded.mode, 'edit')).toEqual({
      mode: IDLE,
      command: { kind: 'saveMemory', memory: 1 },
    });
  });

  it('copy: keys that hold something blink, then every key except the source, then the copy is made', () => {
    const picking = pressFunction(IDLE, 'copy').mode;
    expect(memoryBlinks(picking, 4, 'look')).toBe(true);
    expect(memoryBlinks(picking, 6, 'sequence')).toBe(true); // a sequence is copied like a look
    expect(memoryBlinks(picking, 5, 'nothing')).toBe(false);
    expect(pressMemory(picking, 5, 'nothing')).toEqual({ mode: picking });

    const target = pressMemory(picking, 6, 'sequence').mode;
    expect(target).toEqual({ kind: 'copy-target', from: 6 });
    expect(heldMemory(target)).toBe(6);
    expect(memoryBlinks(target, 6, 'sequence')).toBe(false);
    expect(memoryBlinks(target, 5, 'nothing')).toBe(true);
    expect(pressMemory(target, 6, 'sequence')).toEqual({ mode: target }); // not onto itself

    expect(pressMemory(target, 9, 'nothing')).toEqual({
      mode: IDLE,
      command: { kind: 'copyMemory', from: 6, to: 9 },
    });
  });

  it('copy and del work on effects and sounds as on memories, each onto its own kind', () => {
    // outside Copy and Del the keys start and stop their effects and sounds
    expect(pressItem(IDLE, 'sound', 3, true)).toBeNull();
    expect(pressItem(pressFunction(IDLE, 'save').mode, 'effect', 3, true)).toBeNull();
    expect(itemWaits(IDLE)).toBe(false);

    const picking = pressFunction(IDLE, 'copy').mode;
    expect(itemWaits(picking)).toBe(true);
    expect(itemBlinks(picking, 'sound', 3, true)).toBe(true);
    expect(itemBlinks(picking, 'sound', 4, false)).toBe(false);
    expect(pressItem(picking, 'sound', 4, false)).toEqual({ mode: picking }); // nothing to copy
    const target = pressItem(picking, 'sound', 3, true)!.mode;
    expect(target).toEqual({ kind: 'copy-target', from: 3, of: 'sound' });
    expect(heldItem(target, 'sound')).toBe(3);
    expect(heldItem(target, 'effect')).toBeNull();
    expect(heldMemory(target)).toBeNull();
    expect(itemBlinks(target, 'sound', 9, false)).toBe(true);
    expect(itemBlinks(target, 'sound', 3, true)).toBe(false); // not onto itself
    expect(itemBlinks(target, 'effect', 9, true)).toBe(false);
    expect(memoryBlinks(target, 9, 'nothing')).toBe(false);

    // a sound goes onto a sound only: an effect or a memory says so
    expect(pressItem(target, 'effect', 9, false)?.notice).toBe('Copy puts sound 3 onto another sound: press the key of that one.');
    expect(pressMemory(target, 9, 'nothing').notice).toBe('Copy puts sound 3 onto another sound: press the key of that one.');
    expect(pressItem(target, 'sound', 3, true)).toEqual({ mode: target });
    expect(pressItem(target, 'sound', 9, false)).toEqual({
      mode: IDLE,
      command: { kind: 'copyItem', of: 'sound', from: 3, to: 9 },
    });

    // a memory goes onto a memory only
    const memory = pressMemory(picking, 6, 'look').mode;
    expect(pressItem(memory, 'effect', 2, true)?.notice).toBe('Copy puts memory 6 onto another memory: press the key of that one.');

    const deleting = pressFunction(IDLE, 'delete').mode;
    expect(itemBlinks(deleting, 'effect', 2, true)).toBe(true);
    expect(pressItem(deleting, 'effect', 5, false)).toEqual({ mode: deleting });
    expect(pressItem(deleting, 'effect', 2, true)).toEqual({
      mode: IDLE,
      command: { kind: 'deleteItem', of: 'effect', number: 2 },
    });
  });

  it('delete: keys that hold something blink, and the pressed one is emptied', () => {
    const picking = pressFunction(IDLE, 'delete').mode;
    expect(memoryBlinks(picking, 1, 'look')).toBe(true);
    expect(memoryBlinks(picking, 3, 'sequence')).toBe(true);
    expect(memoryBlinks(picking, 2, 'nothing')).toBe(false);
    expect(pressMemory(picking, 2, 'nothing')).toEqual({ mode: picking });
    expect(pressMemory(picking, 3, 'sequence')).toEqual({
      mode: IDLE,
      command: { kind: 'deleteMemory', memory: 3 },
    });
  });

  it('record: first the memory for the sequence, an empty one or one that holds a sequence', () => {
    const picking = pressFunction(IDLE, 'record').mode;
    expect(picking).toEqual({ kind: 'record-pick' });
    expect(activeFunction(picking)).toBe('record');
    expect(functionBlinks(picking)).toBe(false);
    expect(memoryBlinks(picking, 1, 'nothing')).toBe(true);
    expect(memoryBlinks(picking, 2, 'sequence')).toBe(true);
    expect(memoryBlinks(picking, 3, 'look')).toBe(false);
    expect(channelKeysArmed(picking)).toBe(false);
    // The memory comes first: a channel key still only works its fader.
    expect(pressChannel(picking, 5)).toEqual({
      mode: picking,
      command: { kind: 'toggleFader', target: 'channel', number: 5 },
    });

    const onLook = pressMemory(picking, 3, 'look');
    expect(onLook.mode).toBe(picking);
    expect(onLook.notice).toContain('Memory 3 holds a look');

    expect(pressMemory(picking, 1, 'nothing')).toEqual({ mode: { kind: 'recording', memory: 1 } });
    expect(pressMemory(picking, 2, 'sequence')).toEqual({ mode: { kind: 'recording', memory: 2 } });
  });

  it('record: then every channel key and every key of a look adds a step, until Rec seq again', () => {
    const recording: FunctionMode = { kind: 'recording', memory: 4 };
    expect(activeFunction(recording)).toBe('record');
    expect(functionBlinks(recording)).toBe(true); // press me again to end the recording
    expect(heldMemory(recording)).toBe(4);
    expect(channelKeysArmed(recording)).toBe(true);
    expect(memoryBlinks(recording, 2, 'look')).toBe(true);
    expect(memoryBlinks(recording, 3, 'sequence')).toBe(false);
    expect(memoryBlinks(recording, 5, 'nothing')).toBe(false);
    expect(memoryBlinks(recording, 4, 'sequence')).toBe(false);

    // the mode stays: the next press adds the next step
    expect(pressChannel(recording, 7)).toEqual({
      mode: recording,
      command: { kind: 'addStep', memory: 4, target: 'channel', number: 7 },
    });
    expect(pressMemory(recording, 2, 'look')).toEqual({
      mode: recording,
      command: { kind: 'addStep', memory: 4, target: 'memory', number: 2 },
    });

    // what cannot be a step says why, and adds nothing
    for (const [memory, holds, words] of [
      [3, 'sequence', 'cannot be a step of another one'],
      [5, 'nothing', 'Memory 5 is empty'],
      [4, 'sequence', 'the one being recorded into'],
      [4, 'nothing', 'the one being recorded into'],
    ] as const) {
      const refused = pressMemory(recording, memory, holds);
      expect(refused.mode).toBe(recording);
      expect(refused.command).toBeUndefined();
      expect(refused.notice).toContain(words);
    }

    expect(pressFunction(recording, 'record')).toEqual({ mode: IDLE });
  });

  it('pressing the lit function again cancels it without doing anything', () => {
    for (const name of ['save', 'edit', 'record', 'copy', 'delete'] as const) {
      const started = pressFunction(IDLE, name).mode;
      expect(pressFunction(started, name)).toEqual({ mode: IDLE });
    }
    const copyTarget: FunctionMode = { kind: 'copy-target', from: 3 };
    expect(pressFunction(copyTarget, 'copy')).toEqual({ mode: IDLE });
  });

  it('pressing another function switches to it and drops the unfinished one', () => {
    const editing: FunctionMode = { kind: 'editing', memory: 2 };
    expect(pressFunction(editing, 'delete')).toEqual({ mode: { kind: 'delete-pick' } });
    expect(pressFunction({ kind: 'save' }, 'copy')).toEqual({ mode: { kind: 'copy-pick' } });
    expect(pressFunction({ kind: 'recording', memory: 2 }, 'save')).toEqual({ mode: { kind: 'save' } });
  });

  it('Copy and Del of the Memories view keep to looks, those of the Sequences view to sequences', () => {
    const copyLooks = pressFunction(IDLE, 'copy', 'look').mode;
    expect(copyLooks).toEqual({ kind: 'copy-pick', only: 'look' });
    expect(pressMemory(copyLooks, 3, 'sequence').notice).toBe(
      'Memory 3 holds a sequence: here Copy copies looks. Sequences are on the Sequences view.',
    );
    const target = pressMemory(copyLooks, 2, 'look').mode;
    expect(target).toEqual({ kind: 'copy-target', from: 2, only: 'look' });
    expect(memoryBlinks(target, 3, 'sequence')).toBe(false);
    expect(memoryBlinks(target, 4, 'look')).toBe(true);
    expect(memoryBlinks(target, 5, 'nothing')).toBe(true);
    expect(pressMemory(target, 3, 'sequence').notice).toBe(
      'Memory 3 holds a sequence: a look is copied onto a look or an empty memory.',
    );
    expect(pressMemory(target, 5, 'nothing')).toEqual({
      mode: IDLE,
      command: { kind: 'copyMemory', from: 2, to: 5 },
    });

    const deleteSequences = pressFunction(IDLE, 'delete', 'sequence').mode;
    expect(memoryBlinks(deleteSequences, 2, 'look')).toBe(false);
    expect(memoryBlinks(deleteSequences, 3, 'sequence')).toBe(true);
    expect(pressMemory(deleteSequences, 2, 'look').notice).toBe(
      'Memory 2 holds a look: here Del deletes sequences. Looks are on the Memories view.',
    );
    expect(pressMemory(deleteSequences, 3, 'sequence')).toEqual({
      mode: IDLE,
      command: { kind: 'deleteMemory', memory: 3 },
    });

    // the other keys take no scope; the main view's Copy and Del take any memory
    expect(pressFunction(IDLE, 'save', 'look').mode).toEqual({ kind: 'save' });
    expect(pressMemory({ kind: 'delete-pick' }, 3, 'sequence').command).toEqual({ kind: 'deleteMemory', memory: 3 });
  });
});
