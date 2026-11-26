import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { App } from '../../app';
import { ACTION_GROUPS, actionWithId, DESK_ACTIONS } from '../../desk/desk-actions';
import { DEFAULT_SHORTCUTS, DeskSettings } from '../../desk/desk-settings';
import { DeskStore } from '../../desk/desk-store';
import {
  deskPageHelpers,
  FakeConnection,
  LIVE,
  startDeskPage,
} from '../../testing/desk-page.testing';

describe('the desk page: the Keyboard panel', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;
  const { show, button, press, kind } = deskPageHelpers(() => ({
    connection,
    fixture,
    page,
    store,
  }));

  beforeEach(async () => {
    ({ connection, fixture, page, store } = await startDeskPage());
  });

  it('the Keyboard panel of Settings lists the kinds of keys one under the other: one open at a time, none at first', async () => {
    await show('/settings#keyboard');
    const total = (group: string) => DESK_ACTIONS.filter((action) => action.group === group).length;
    // "Stack 6 (4) 2": the keys, in brackets those with a shortcut, and while a search runs, those it finds
    const count = (group: string) => kind(group).querySelector('.count')?.textContent?.trim();
    const bound = (group: string) => kind(group).querySelector('.bound');
    const found = (group: string) => kind(group).querySelector('.found');
    const rows = () =>
      [...page.querySelectorAll('lm-keyboard-panel .keys tbody th')].map((cell) =>
        cell.textContent?.trim(),
      );
    const open = () =>
      [...page.querySelectorAll('lm-keyboard-panel .head[aria-expanded="true"]')].map((head) =>
        head.querySelector('.name')?.textContent?.trim(),
      );

    expect(page.querySelectorAll('lm-keyboard-panel .head').length).toBe(ACTION_GROUPS.length);
    expect(open()).toEqual([]); // all closed at first
    expect(rows()).toEqual([]);
    expect(count('Master')).toBe(String(total('Master')));
    expect(bound('Master')?.textContent?.trim()).toBe('(1)'); // B for Blackout from the start
    expect(bound('Record')?.textContent?.trim()).toBe('(0)');
    expect(bound('Record')?.classList).toContain('none'); // dim; green only with a shortcut
    expect(bound('Project and LAN')?.textContent?.trim()).toBe('(4)'); // Undo, Redo, Save and Load have theirs from the start
    expect(bound('Project and LAN')?.classList).not.toContain('none');
    expect(found('Master')).toBeNull(); // no search, no third number
    expect(kind('Master').getAttribute('aria-label')).toBe('Master: 1 key, 1 with a shortcut');

    await press(kind('Master'));
    expect(open()).toEqual(['Master']);
    expect(kind('Master').getAttribute('aria-controls')).toBe(
      page.querySelector('lm-keyboard-panel .keys')?.id,
    );
    expect(rows()).toEqual(['Blackout']);
    await press(kind('General'));
    expect(open()).toEqual(['General']); // one at a time
    expect(rows()).toEqual(['Clear', 'Solo up', 'Solo down', 'Rate', 'Tempo (tap)']);
    await press(kind('General'));
    expect(open()).toEqual([]); // pressed again, it closes

    // The arrow keys go from kind to kind.
    kind('General').focus();
    kind('General').dispatchEvent(
      new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }),
    );
    expect(document.activeElement).toBe(kind('Sound'));

    // A search: each kind says how many of its keys it finds; when the kind that is open finds
    // none, the first that finds some opens; emptied, the kind open before opens again.
    await press(kind('Stack'));
    const search = page.querySelector<HTMLInputElement>('lm-keyboard-panel input[type="search"]')!;
    search.value = 'black';
    search.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(count('Master')).toBe(String(total('Master')));
    expect(found('Master')?.textContent?.trim()).toBe('1');
    expect(found('Stack')?.textContent?.trim()).toBe('0');
    expect(found('Stack')?.classList).toContain('none');
    expect(kind('Master').getAttribute('aria-label')).toBe(
      'Master: 1 key, 1 with a shortcut, 1 found',
    );
    expect(kind('Stack').classList).toContain('none-found');
    expect(open()).toEqual(['Master']);
    expect(rows()).toEqual(['Blackout']);

    search.value = '';
    search.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(open()).toEqual(['Stack']);

    // The old address of the Keyboard view opens Settings.
    await show('/keyboard');
    expect(TestBed.inject(Router).url).toBe('/settings#keyboard');
  });

  it('a shortcut is recorded for a key, and then presses that key', async () => {
    await show('/settings#keyboard');
    await press(kind('Master'));
    await press(button('Set the shortcut of Blackout'));
    expect(page.querySelector('tr[data-row="master.blackout"] .waiting')).toBeTruthy();

    // Ctrl alone is held, not a shortcut; Ctrl with B is, with the side of that Ctrl.
    const key = (type: 'keydown' | 'keyup', code: string, ctrlKey: boolean) =>
      document.dispatchEvent(
        new KeyboardEvent(type, {
          code,
          key: code === 'KeyB' ? 'b' : code === 'KeyZ' ? 'z' : 'Control',
          ctrlKey,
          bubbles: true,
          cancelable: true,
        }),
      );
    key('keydown', 'ControlLeft', true);
    key('keydown', 'KeyB', true);
    await fixture.whenStable();
    expect(
      page.querySelector('tr[data-row="master.blackout"] td.shortcut')?.textContent?.trim(),
    ).toBe('Left Ctrl + B');

    await show('/');
    key('keydown', 'KeyB', true);
    await fixture.whenStable();
    expect(connection.last('SetBlackout')).toEqual([true]);

    // The right Ctrl is another key: Right Ctrl + B presses nothing; Ctrl + Z, kept without a side (Undo), works with it too.
    key('keyup', 'ControlLeft', false);
    key('keydown', 'ControlRight', true);
    key('keydown', 'KeyB', true);
    await fixture.whenStable();
    expect(connection.methods().filter((method) => method === 'SetBlackout').length).toBe(1);
    connection.emit('HistoryChanged', { undo: 'storing memory 4', redo: null });
    await fixture.whenStable();
    key('keydown', 'KeyZ', true);
    await fixture.whenStable();
    expect(connection.methods()).toContain('Undo');
    key('keyup', 'ControlRight', false);
    key('keydown', 'ControlLeft', true);

    // While a note is typed, the keys are the note's.
    const note = page.querySelector<HTMLInputElement>(
      'input[aria-label="Note for Channel 1 (U1)"]',
    )!;
    note.dispatchEvent(
      new KeyboardEvent('keydown', { code: 'KeyB', key: 'b', ctrlKey: true, bubbles: true }),
    );
    await fixture.whenStable();
    expect(connection.methods().filter((method) => method === 'SetBlackout').length).toBe(1);

    // The same shortcut for another key moves over to it.
    await show('/settings#keyboard');
    await press(kind('General'));
    await press(button('Set the shortcut of Clear'));
    document.dispatchEvent(
      new KeyboardEvent('keydown', { code: 'KeyB', key: 'b', ctrlKey: true, bubbles: true }),
    );
    await fixture.whenStable();
    expect(page.querySelector('tr[data-row="master.clear"] td.shortcut')?.textContent?.trim()).toBe(
      'Left Ctrl + B',
    );
    await press(kind('Master'));
    expect(
      page.querySelector('tr[data-row="master.blackout"] td.shortcut')?.textContent?.trim(),
    ).toBe('');
    expect(page.textContent).toContain('That shortcut was the one of Blackout before');
  });

  it('AltGr is the right Alt, and still presses a shortcut recorded with it as Ctrl + Alt', async () => {
    // AltGr on Windows: a left Ctrl that is not there goes down first, and every press says Ctrl and Alt.
    const altGr = (type: 'keydown' | 'keyup', code: string, down: boolean) =>
      document.dispatchEvent(
        new KeyboardEvent(type, {
          code,
          key: code === 'KeyB' ? 'b' : 'AltGraph',
          ctrlKey: down,
          altKey: down,
          modifierAltGraph: down,
          bubbles: true,
          cancelable: true,
        }),
      );
    const pressAltGrB = () => {
      altGr('keydown', 'ControlLeft', true);
      altGr('keydown', 'AltRight', true);
      altGr('keydown', 'KeyB', true);
      altGr('keyup', 'AltRight', false);
      altGr('keyup', 'ControlLeft', false);
    };
    const settings = TestBed.inject(DeskSettings);
    settings.setShortcuts({ ...settings.shortcuts(), 'master.blackout': 'Ctrl+Alt+KeyB' });

    await show('/');
    pressAltGrB();
    await fixture.whenStable();
    expect(connection.last('SetBlackout')).toEqual([true]);

    await show('/settings#keyboard');
    await press(kind('Master'));
    await press(button('Set the shortcut of Blackout'));
    pressAltGrB();
    await fixture.whenStable();
    expect(
      page.querySelector('tr[data-row="master.blackout"] td.shortcut')?.textContent?.trim(),
    ).toBe('Right Alt + B');
    expect(settings.shortcuts()['master.blackout']).toBe('RightAlt+KeyB');

    await show('/');
    pressAltGrB();
    await fixture.whenStable();
    expect(connection.methods().filter((method) => method === 'SetBlackout').length).toBe(2);
  });

  it('listening picks the key that is double-clicked on the desk; a single click works as always, a moment later', async () => {
    const router = TestBed.inject(Router);
    const pressOnce = (element: Element) => {
      element.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, cancelable: true }));
      (element as HTMLElement).click();
    };
    const later = async () => {
      await new Promise((resolve) => setTimeout(resolve, 450));
      await fixture.whenStable();
    };
    await show('/settings#keyboard');
    await press(button('Listen'));
    expect(router.url).toBe('/');
    expect(page.querySelector('.listening')).toBeTruthy();

    // a single click on Clear: nothing at once, then Clear as always; still listening
    const clear = page.querySelector<HTMLButtonElement>('[data-action="master.clear"]')!;
    pressOnce(clear);
    await fixture.whenStable();
    expect(connection.methods()).not.toContain('Clear');
    await later();
    expect(connection.methods()).toContain('Clear');
    expect(router.url).toBe('/');

    // a single click on a link opens its view, still listening; a double click there picks a key
    pressOnce(page.querySelector('a[data-action="view.stage"]')!);
    await later();
    expect(router.url).toBe('/stage');
    const edit = page.querySelector('[data-action="stage.edit"]')!;
    pressOnce(edit);
    pressOnce(edit);
    await fixture.whenStable();
    expect(router.url).toBe('/settings#keyboard');
    expect(page.querySelector('tr[data-row="stage.edit"]')?.classList.contains('picked')).toBe(
      true,
    );
    // Only the list scrolls to the row; the page itself stays where it is, the top bar in sight.
    expect(document.scrollingElement?.scrollTop ?? 0).toBe(0);
    await later();
    expect(router.url).toBe('/settings#keyboard'); // the picked key was not pressed after all

    // A locked key is picked as well: Play, while the stack is not live. It gets no click.
    await press(button('Listen'));
    const play = page.querySelector('[data-action="stack.play"]')!;
    for (let press = 0; press < 2; press++) {
      play.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true }));
      play.dispatchEvent(new MouseEvent('pointerup', { bubbles: true }));
    }
    await fixture.whenStable();
    expect(page.querySelector('tr[data-row="stack.play"]')?.classList.contains('picked')).toBe(
      true,
    );
    await new Promise((resolve) => setTimeout(resolve, 0));

    // Enter on a key (a click without a pointer) picks it at once.
    await press(button('Listen'));
    page.querySelector<HTMLButtonElement>('[data-action="master.blackout"]')!.click();
    await fixture.whenStable();
    expect(page.querySelector('tr[data-row="master.blackout"]')?.classList.contains('picked')).toBe(
      true,
    );

    // Esc stops listening.
    await press(button('Listen'));
    document.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Escape', code: 'Escape', bubbles: true }),
    );
    await fixture.whenStable();
    expect(page.querySelector('.listening')).toBeNull();
  });

  it('every key on the page that is marked for shortcuts is one the list of the Keyboard panel knows', async () => {
    for (const view of [
      '/',
      '/stack',
      '/sequences',
      '/memories',
      '/stage',
      '/fixtures',
      '/notes',
      '/midi',
      '/settings',
    ]) {
      await show(view);
      for (const element of page.querySelectorAll('[data-action]')) {
        expect(
          actionWithId(element.getAttribute('data-action') ?? ''),
          element.getAttribute('data-action') ?? '',
        ).toBeTruthy();
      }
    }
  });

  it('the usual shortcuts of other programs work from the start: the space bar is GO, M is MUTE, B is Blackout', async () => {
    const key = (code: string, target: EventTarget = document) =>
      target.dispatchEvent(
        new KeyboardEvent('keydown', { code, key: code, bubbles: true, cancelable: true }),
      );
    const plays = () => connection.calls.filter((call) => call.method === 'StackPlay').length;

    // the stack is live: the space bar plays it, also while the key clicked last has the focus
    connection.emit('PlaybackChanged', { ...connection.playback, stack: { ...LIVE } });
    await fixture.whenStable();
    key('Space');
    expect(plays()).toBe(1);
    const clicked = page.querySelector<HTMLButtonElement>('button[data-action]')!;
    clicked.focus();
    expect(key('Space', clicked)).toBe(false); // the key itself is not pressed again
    expect(plays()).toBe(2);

    // a box that has the focus keeps its space bar: it ticks
    const box = document.createElement('input');
    box.type = 'checkbox';
    page.append(box);
    expect(key('Space', box)).toBe(true);
    expect(plays()).toBe(2);
    box.remove();

    key('KeyM');
    expect(connection.last('SetSoundMute')).toEqual([true]);
    key('KeyB');
    expect(connection.last('SetBlackout')).toEqual([true]);
  });

  it('↺ beside the title gives every key the shortcut it has at first, after a second press', async () => {
    const settings = TestBed.inject(DeskSettings);
    await show('/settings#keyboard');
    const reset = () => page.querySelector<HTMLButtonElement>('lm-keyboard-panel .title-row .reset')!;
    expect(reset().disabled).toBe(true); // every key has its usual shortcut: nothing to do

    settings.setShortcuts({ 'master.blackout': 'KeyX', 'stack.play': '' });
    await fixture.whenStable();
    expect(reset().getAttribute('aria-label')).toBe('The usual shortcuts');
    await press(reset()); // the first press only asks
    expect(reset().getAttribute('aria-label')).toBe('Press again for the usual shortcuts');
    expect(page.querySelector('lm-keyboard-panel .ask')?.textContent).toContain('Press ↺ again');
    expect(settings.shortcuts()).toEqual({ 'master.blackout': 'KeyX', 'stack.play': '' });

    await press(reset());
    expect(settings.shortcuts()).toEqual(DEFAULT_SHORTCUTS);
    expect(reset().disabled).toBe(true);
    expect(page.querySelector('lm-keyboard-panel .done')?.textContent).toContain(
      'usual shortcut again',
    );
  });
});
