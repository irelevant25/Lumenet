import { DESK_ACTIONS, actionWithId, shortcutName, shortcutOf, shortcutsOf, withoutSides } from './desk-actions';

const press = (code: string, held: Partial<Record<'ctrlKey' | 'altKey' | 'shiftKey' | 'metaKey' | 'altGraph', boolean>> = {}) => ({
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

  it('names the left or the right Ctrl and Alt, as their own presses said them', () => {
    const held = (...codes: string[]) => new Set(codes);
    expect(shortcutOf(press('KeyZ', { ctrlKey: true }), held('ControlLeft'))).toBe('LeftCtrl+KeyZ');
    expect(shortcutOf(press('KeyZ', { ctrlKey: true }), held('ControlRight'))).toBe('RightCtrl+KeyZ');
    expect(shortcutOf(press('KeyA', { ctrlKey: true, altKey: true }), held('ControlRight', 'AltLeft'))).toBe('RightCtrl+LeftAlt+KeyA');
    // both, or a side not seen (the key went down before the page had the keyboard): either
    expect(shortcutOf(press('KeyZ', { ctrlKey: true }), held('ControlLeft', 'ControlRight'))).toBe('Ctrl+KeyZ');
    expect(shortcutOf(press('KeyZ', { ctrlKey: true }))).toBe('Ctrl+KeyZ');
    // AltGr of a Czech or Slovak keyboard: the right Alt, without the Ctrl Windows adds to it
    expect(shortcutOf(press('Digit1', { ctrlKey: true, altKey: true, altGraph: true }), held('ControlLeft', 'AltRight'))).toBe('RightAlt+Digit1');
    // ... and with a right Ctrl that is really held with it
    const withRightCtrl = held('ControlLeft', 'ControlRight', 'AltRight');
    expect(shortcutOf(press('Digit1', { ctrlKey: true, altKey: true, altGraph: true }), withRightCtrl)).toBe('RightCtrl+RightAlt+Digit1');
  });

  it('without its sides is pressed by either key', () => {
    expect(withoutSides('LeftCtrl+RightAlt+Shift+KeyP')).toBe('Ctrl+Alt+Shift+KeyP');
    expect(withoutSides('Alt+ArrowLeft')).toBe('Alt+ArrowLeft');
  });

  it('a press tries the shortcut with its sides, then without, then an AltGr one as it was named before', () => {
    const held = (...codes: string[]) => new Set(codes);
    expect(shortcutsOf(press('KeyP'))).toEqual(['KeyP']);
    expect(shortcutsOf(press('KeyZ', { ctrlKey: true }), held('ControlRight'))).toEqual(['RightCtrl+KeyZ', 'Ctrl+KeyZ']);
    expect(shortcutsOf(press('KeyZ', { ctrlKey: true }))).toEqual(['Ctrl+KeyZ']);
    // AltGr on Windows: until the sides were told apart it was "Ctrl+Alt"
    const altGr = press('Digit1', { ctrlKey: true, altKey: true, shiftKey: true, altGraph: true });
    expect(shortcutsOf(altGr, held('ControlLeft', 'AltRight'))).toEqual(['RightAlt+Shift+Digit1', 'Alt+Shift+Digit1', 'Ctrl+Alt+Shift+Digit1']);
    // where the browser says AltGr without Ctrl and Alt, there was no other name
    expect(shortcutsOf(press('Digit1', { altGraph: true }))).toEqual(['RightAlt+Digit1', 'Alt+Digit1']);
    expect(shortcutsOf(press('ControlLeft', { ctrlKey: true }), held('ControlLeft'))).toEqual([]);
  });

  it('is written as people write it', () => {
    expect(shortcutName('Ctrl+Shift+KeyP')).toBe('Ctrl + Shift + P');
    expect(shortcutName('Digit7')).toBe('7');
    expect(shortcutName('Numpad3')).toBe('Num 3');
    expect(shortcutName('Alt+ArrowLeft')).toBe('Alt + Left');
    expect(shortcutName('F5')).toBe('F5');
    expect(shortcutName('LeftCtrl+KeyZ')).toBe('Left Ctrl + Z');
    expect(shortcutName('RightAlt+Digit1')).toBe('Right Alt + 1');
  });
});
