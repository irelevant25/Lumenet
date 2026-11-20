import { DESK_ACTIONS, actionWithId, shortcutName, shortcutOf } from './desk-actions';

const press = (code: string, held: Partial<Record<'ctrlKey' | 'altKey' | 'shiftKey' | 'metaKey', boolean>> = {}) => ({
  code,
  ctrlKey: false,
  altKey: false,
  shiftKey: false,
  metaKey: false,
  ...held,
});

describe('the keys of the desk that can have a shortcut', () => {
  it('each have a name of their own', () => {
    const ids = DESK_ACTIONS.map((action) => action.id);
    expect(new Set(ids).size).toBe(ids.length);
    expect(actionWithId('stack.play')?.label).toBe('Play');
    expect(actionWithId('channel.512')?.group).toBe('Channel keys');
    expect(actionWithId('page.channel.64')).toBeTruthy(); // 8 channels on a page: 64 pages
    expect(actionWithId('page.memory.32')).toBeTruthy();
    expect(actionWithId('ind.memory.32')).toBeTruthy();
    expect(actionWithId('nothing')).toBeUndefined();
  });
});

describe('a shortcut', () => {
  it('is the physical key with the keys held with it', () => {
    expect(shortcutOf(press('KeyP'))).toBe('KeyP');
    expect(shortcutOf(press('Digit1', { ctrlKey: true, shiftKey: true }))).toBe('Ctrl+Shift+Digit1');
    expect(shortcutOf(press('Space', { altKey: true, metaKey: true }))).toBe('Alt+Meta+Space');
  });

  it('is not a key that is only held, nor Esc or Tab', () => {
    expect(shortcutOf(press('ShiftLeft', { shiftKey: true }))).toBeNull();
    expect(shortcutOf(press('ControlRight', { ctrlKey: true }))).toBeNull();
    expect(shortcutOf(press('Escape'))).toBeNull();
    expect(shortcutOf(press('Tab'))).toBeNull();
    expect(shortcutOf(press(''))).toBeNull();
  });

  it('is written as people write it', () => {
    expect(shortcutName('Ctrl+Shift+KeyP')).toBe('Ctrl + Shift + P');
    expect(shortcutName('Digit7')).toBe('7');
    expect(shortcutName('Numpad3')).toBe('Num 3');
    expect(shortcutName('Alt+ArrowLeft')).toBe('Alt + Left');
    expect(shortcutName('F5')).toBe('F5');
  });
});
