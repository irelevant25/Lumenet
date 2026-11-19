import {
  FunctionMode,
  IDLE,
  activeFunction,
  channelKeysArmed,
  functionBlinks,
  heldMemory,
  memoryBlinks,
  pressChannel,
  pressFunction,
  pressMemory,
} from './function-mode';

describe('the record keys', () => {
  it('outside a function the memory keys and the channel keys do nothing', () => {
    expect(pressMemory(IDLE, 5, 'look')).toEqual({ mode: IDLE });
    expect(pressChannel(IDLE, 5)).toEqual({ mode: IDLE });
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
    expect(pressChannel(picking, 5)).toEqual({ mode: picking }); // the memory comes first

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
});
