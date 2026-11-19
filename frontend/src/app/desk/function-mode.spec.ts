import {
  FunctionMode,
  IDLE,
  activeFunction,
  heldMemory,
  memoryBlinks,
  pressFunction,
  pressMemory,
} from './function-mode';

describe('memory functions', () => {
  it('outside a function the memory keys do nothing', () => {
    expect(pressMemory(IDLE, 5, true)).toEqual({ mode: IDLE });
    expect(memoryBlinks(IDLE, 5, true)).toBe(false);
    expect(activeFunction(IDLE)).toBeNull();
  });

  it('save: every key blinks, and the pressed one stores the channel faders', () => {
    const { mode } = pressFunction(IDLE, 'save');
    expect(activeFunction(mode)).toBe('save');
    expect(memoryBlinks(mode, 1, false)).toBe(true);
    expect(memoryBlinks(mode, 2, true)).toBe(true);

    expect(pressMemory(mode, 7, false)).toEqual({
      mode: IDLE,
      command: { kind: 'saveMemory', memory: 7 },
    });
  });

  it('edit: stored keys blink, the pressed one is loaded, and Edit again stores it back', () => {
    const picking = pressFunction(IDLE, 'edit').mode;
    expect(memoryBlinks(picking, 1, true)).toBe(true);
    expect(memoryBlinks(picking, 2, false)).toBe(false);
    expect(pressMemory(picking, 2, false)).toEqual({ mode: picking }); // an empty memory cannot be edited

    const loaded = pressMemory(picking, 1, true);
    expect(loaded).toEqual({
      mode: { kind: 'editing', memory: 1 },
      command: { kind: 'loadMemory', memory: 1 },
    });
    expect(activeFunction(loaded.mode)).toBe('edit');
    expect(heldMemory(loaded.mode)).toBe(1);
    expect(memoryBlinks(loaded.mode, 1, true)).toBe(false);
    expect(pressMemory(loaded.mode, 3, true)).toEqual({ mode: loaded.mode }); // other keys wait

    expect(pressFunction(loaded.mode, 'edit')).toEqual({
      mode: IDLE,
      command: { kind: 'saveMemory', memory: 1 },
    });
  });

  it('copy: stored keys blink, then every key except the source, then the copy is made', () => {
    const picking = pressFunction(IDLE, 'copy').mode;
    expect(memoryBlinks(picking, 4, true)).toBe(true);
    expect(memoryBlinks(picking, 5, false)).toBe(false);
    expect(pressMemory(picking, 5, false)).toEqual({ mode: picking });

    const target = pressMemory(picking, 4, true).mode;
    expect(target).toEqual({ kind: 'copy-target', from: 4 });
    expect(heldMemory(target)).toBe(4);
    expect(memoryBlinks(target, 4, true)).toBe(false);
    expect(memoryBlinks(target, 5, false)).toBe(true);
    expect(pressMemory(target, 4, true)).toEqual({ mode: target }); // not onto itself

    expect(pressMemory(target, 9, false)).toEqual({
      mode: IDLE,
      command: { kind: 'copyMemory', from: 4, to: 9 },
    });
  });

  it('delete: stored keys blink, and the pressed one is emptied', () => {
    const picking = pressFunction(IDLE, 'delete').mode;
    expect(memoryBlinks(picking, 1, true)).toBe(true);
    expect(memoryBlinks(picking, 2, false)).toBe(false);
    expect(pressMemory(picking, 2, false)).toEqual({ mode: picking });
    expect(pressMemory(picking, 1, true)).toEqual({
      mode: IDLE,
      command: { kind: 'deleteMemory', memory: 1 },
    });
  });

  it('pressing the lit function again cancels it without doing anything', () => {
    for (const name of ['save', 'edit', 'copy', 'delete'] as const) {
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
  });
});
