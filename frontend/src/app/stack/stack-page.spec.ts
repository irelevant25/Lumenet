import { ComponentFixture } from '@angular/core/testing';
import { App } from '../app';
import { actionLamp } from '../desk/action-lamps';
import { actionWithId } from '../desk/desk-actions';
import { DeskStore } from '../desk/desk-store';
import { UNIVERSE_SIZE } from '../desk/desk-types';
import { IDLE } from '../desk/function-mode';
import { quietPlayback } from '../sounds/sounds';
import {
  deskPageHelpers,
  FakeConnection,
  LIVE,
  NOT_LIVE,
  startDeskPage,
  step,
} from '../testing/desk-page.testing';

describe('the desk page: the stack', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;
  const { show, button, buttonIn, memoryKey, press, stackStands, aSound, choose } = deskPageHelpers(() => ({
    connection,
    fixture,
    page,
    store,
  }));

  beforeEach(async () => {
    ({ connection, fixture, page, store } = await startDeskPage());
  });

  it('the stack view shows the steps with the current and the next one, and goes through them', async () => {
    await show('/stack');
    await stackStands(LIVE);
    const rows = () => [...page.querySelectorAll('lm-stack-view table.steps tbody tr')];
    expect(rows().length).toBe(2);
    expect(rows().map((row) => row.classList.contains('current'))).toEqual([true, false]);
    expect(rows().map((row) => row.classList.contains('next'))).toEqual([false, true]);
    // the number of the step big at the right of its card, the words at the left
    expect(page.querySelector('.card.current')?.lastElementChild?.className).toBe('cue');
    expect(page.querySelector('.card.current .cue')?.textContent?.trim()).toBe('Cue 1.0');
    expect(page.querySelector('.card.current .what')?.textContent?.trim()).toBe('Channel 7 (U1)'); // its value is in the table
    expect(page.querySelector('.card.next .cue')?.textContent?.trim()).toBe('Cue 2.0');
    expect(page.querySelector('.card.current .how')?.textContent).toContain('waits for GO');
    expect(page.querySelector('.card.next .what')?.textContent?.trim()).toBe('Memory 2 (Wash)');
    expect(rows()[1].querySelector('td.note')?.textContent?.trim()).toBe('Wash'); // the note under that fader

    await press(button('GO'));
    await press(button('Pause'));
    await press(button('Next cue'));
    await press(button('Back to the cue before'));
    expect(connection.methods()).toEqual(['StackPlay', 'StackPause', 'StackNext', 'StackPrevious']);

    // the desk reports a fade on the last step, and then a pause; after the last step comes the first
    await stackStands({ ...LIVE, currentId: 12, nextId: 11, state: 'fading', remaining: 0.6 });
    expect(page.querySelector('.card.current .how')?.textContent).toContain('0.6 s left');
    expect(button('GO').className).toContain('level-3');
    expect(page.querySelector('.card.next .cue')?.textContent?.trim()).toBe('Cue 1.0');
    expect(rows().map((row) => row.classList.contains('next'))).toEqual([true, false]);

    await stackStands({
      ...LIVE,
      currentId: 12,
      nextId: 11,
      state: 'fading',
      paused: true,
      remaining: 0.6,
    });
    expect(button('Pause').getAttribute('aria-pressed')).toBe('true');
    expect(page.querySelector('.card.current .how')?.textContent).toContain('Paused');
  });

  it('the Stage view has the keys and the cards of the stack at its left, and its steps without fields', async () => {
    await show('/stage');
    await stackStands(LIVE);
    const panel = page.querySelector('lm-stage-stack')!;
    expect(
      [...panel.querySelectorAll('lm-stack-keys button')].map((key) =>
        key.getAttribute('aria-label'),
      ),
    ).toEqual(['Stack live', 'Back to the cue before', 'GO', 'Pause', 'Next cue']);
    // the master at the left of the cards, the sound at their right
    // the cards, then (between the wave of a sound and the steps) the master and the sound section, each in a line
    expect(
      panel.querySelector(
        ':scope > lm-stack-cards + lm-master-section.lying + lm-sound-master.lying + .table-box',
      ),
    ).toBeTruthy();
    expect(panel.querySelector('lm-sound-master button[aria-label="Mute"]')).toBeTruthy();
    expect(
      [...panel.querySelectorAll('lm-stack-cards.column .card h3')].map((title) =>
        title.textContent?.trim(),
      ),
    ).toEqual(['Previous', 'Current', 'Next']);
    expect(panel.querySelector('.card.current .what')?.textContent?.trim()).toBe('Channel 7 (U1)');

    // the steps: number, whether it adds, notes and times only, nothing to change, the one that is on marked
    const headers = [...panel.querySelectorAll('table.steps thead th')].map((header) =>
      header.textContent?.trim(),
    );
    expect(headers).toEqual(['Cue', 'Add', 'Notes', 'Up (s)', 'Wait (s)', 'Down (s)']);
    const rows = [...panel.querySelectorAll('table.steps tbody tr')];
    expect(rows.map((row) => row.querySelector('th')?.textContent?.trim())).toEqual(['1.0', '2.0']);
    expect(rows.map((row) => row.classList.contains('current'))).toEqual([true, false]);
    expect(rows.map((row) => row.classList.contains('next'))).toEqual([false, true]);
    expect(rows[1].querySelector('td.note')?.textContent?.trim()).toBe('Wash');
    expect(
      [...rows[1].querySelectorAll('td.time')].map((cell) => cell.textContent?.trim()),
    ).toEqual(['1.0', '3.0', '1.0']);
    expect(
      panel.querySelectorAll('table.steps input, table.steps select, table.steps button').length,
    ).toBe(0);

    await press(button('GO'));
    await press(button('Next cue'));
    expect(connection.methods()).toEqual(['StackPlay', 'StackNext']);
  });

  it('the Stack key takes the stack live, and a live stack locks everything that is stored', async () => {
    await show('/stack');
    const liveTag = () => page.querySelector('nav[aria-label="Views"] .live');

    // not live: the stack does not run, and its steps can be changed
    expect(button('Stack live').getAttribute('aria-pressed')).toBe('false');
    expect(button('Stack live').className).toContain('level-1');
    for (const key of ['GO', 'Pause', 'Next cue', 'Back to the cue before']) {
      expect(button(key).disabled).toBe(true);
    }
    expect(button('Add a cue').disabled).toBe(false);
    expect(liveTag()).toBeNull();
    expect(page.querySelector('.card.next .cue')?.textContent?.trim()).toBe('Cue 1.0'); // where live starts

    store.setChannel(4, 90); // a fader move that is still waiting to be sent
    await press(button('Stack live'));
    expect(connection.methods()).toEqual(['SetStackLive']); // the waiting move is dropped: every fader goes down
    expect(connection.last('SetStackLive')).toEqual([true]);

    await stackStands(LIVE);
    expect(button('Stack live').getAttribute('aria-pressed')).toBe('true');
    expect(button('Stack live').className).toContain('level-3');
    expect(liveTag()?.textContent?.trim()).toBe('Live');
    expect(page.querySelector('.summary')?.textContent).toContain('It is live');
    expect(button('GO').disabled).toBe(false);
    expect(button('Add a cue').disabled).toBe(true);
    const fields = (view: string) => [
      ...page.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLButtonElement>(
        `${view} table.steps input, ${view} table.steps select, ${view} table.steps button`,
      ),
    ];
    // per row: the number of the step, its Add box, the two lists, four typed fields, four keys (▲ ▼ + ✕),
    // and the two that still work: At (the moments are set in a rehearsal) and its box Disabled (a step
    // can be skipped during a show, not the one that is on)
    expect(fields('lm-stack-view').length).toBe(2 * 14);
    expect(
      fields('lm-stack-view')
        .filter((field) => !field.closest('td.skips, td.moment'))
        .every((field) => field.disabled),
    ).toBe(true);
    expect(
      [...page.querySelectorAll<HTMLButtonElement>('lm-stack-view td.moment button')].map(
        (key) => key.disabled,
      ),
    ).toEqual([false, false]);
    expect(
      [...page.querySelectorAll<HTMLInputElement>('lm-stack-view td.skips input')].map(
        (box) => box.disabled,
      ),
    ).toEqual([true, false]);
    expect(page.querySelector('lm-stack-view .grip')?.getAttribute('draggable')).toBeNull();

    // the main view: the record keys, the notes, the independent keys and the project keys
    await show('/');
    expect(liveTag()).toBeTruthy();
    for (const key of [
      'Save memory',
      'Edit memory',
      'Record sequence',
      'Copy memory',
      'Delete memory',
      'New project',
      'Load project',
      'Import',
      'Edit the channels, memories, effects and sounds of the independent keys',
    ]) {
      expect(button(key).disabled).toBe(true);
    }
    expect(
      page.querySelector<HTMLInputElement>('input[aria-label="Note for Channel 5 (U1)"]')?.readOnly,
    ).toBe(true);
    expect(
      page.querySelector<HTMLInputElement>('input[aria-label="Note for Memory 2"]')?.readOnly,
    ).toBe(true);
    store.pressFunction('save'); // a press that gets through anyway starts nothing
    expect(store.mode()).toEqual(IDLE);
    // what a show needs is not locked
    for (const key of [
      'Save project',
      'Export',
      'Clear',
      'Blackout',
      'Independent 3, channel 3 (U1)',
    ]) {
      expect(button(key).disabled).toBe(false);
    }

    // the sequences view only shows
    await show('/sequences');
    await press(button('Work on memory 3'));
    expect(page.querySelector('.summary')?.textContent).toContain('The stack is live');
    expect(button('Add a step').disabled).toBe(true);
    expect(button('Once').disabled).toBe(true);
    expect(fields('lm-sequences-view').length).toBeGreaterThan(0);
    // all but the boxes Disabled: a step of a sequence can be skipped during a show
    expect(
      fields('lm-sequences-view')
        .filter((field) => !field.closest('td.skips'))
        .every((field) => field.disabled),
    ).toBe(true);
    expect(page.querySelectorAll('lm-sequences-view td.skips input:not(:disabled)').length).toBe(2);

    // Stack again switches it off: the lock is gone
    await show('/stack');
    await press(button('Stack live'));
    expect(connection.last('SetStackLive')).toEqual([false]);
    await stackStands(NOT_LIVE);
    expect(liveTag()).toBeNull();
    expect(button('Add a cue').disabled).toBe(false);
    expect(button('GO').disabled).toBe(true);
    expect(fields('lm-stack-view').some((field) => !field.disabled)).toBe(true);
  });

  it('the Stack key puts the page on the whole screen when it takes the stack live, and takes it back when it switches the stack off', async () => {
    const root = document.documentElement;
    let full: Element | null = null;
    const enter = vi.fn(async () => void (full = root));
    const leave = vi.fn(async () => void (full = null));
    Object.defineProperty(root, 'requestFullscreen', { value: enter, configurable: true });
    Object.defineProperty(document, 'exitFullscreen', { value: leave, configurable: true });
    Object.defineProperty(document, 'fullscreenElement', { get: () => full, configurable: true });
    try {
      await press(button('Stack live')); // the key on Main
      expect(enter).toHaveBeenCalledTimes(1);
      await stackStands(LIVE);

      await show('/stack');
      await press(button('Stack live')); // the key of the Stack view, as the one of the Stage view
      expect(leave).toHaveBeenCalledTimes(1);
      expect(full).toBeNull();
      expect(
        connection.calls.filter((call) => call.method === 'SetStackLive').map((call) => call.args),
      ).toEqual([[true], [false]]);
    } finally {
      delete (root as { requestFullscreen?: unknown }).requestFullscreen;
      delete (document as { exitFullscreen?: unknown }).exitFullscreen;
      delete (document as { fullscreenElement?: unknown }).fullscreenElement;
    }
  });

  it('a stack that goes live ends an unfinished recording and the editing of the independent keys', async () => {
    await press(button('Record sequence'));
    await press(memoryKey(9));
    await press(button('Edit the channels, memories, effects and sounds of the independent keys'));
    expect(store.mode()).toEqual({ kind: 'recording', memory: 9 });
    expect(page.querySelector('input[aria-label="Channel of independent key 2"]')).toBeTruthy();

    await stackStands(LIVE); // switched live on another page

    expect(store.mode()).toEqual(IDLE);
    expect(page.querySelector('input[aria-label="Channel of independent key 2"]')).toBeNull();
  });

  it('the stack view adds, changes, renumbers, moves and takes out steps', async () => {
    await show('/stack');

    await press(button('Add a cue'));
    expect(connection.last('AddStackStep')).toEqual([step(3, 128, { target: 'memory', wait: 3 })]);

    const value = page.querySelector<HTMLInputElement>(
      'input[aria-label="Cue 1.0, value, 0 to 255"]',
    );
    if (!value) throw new Error('no value field in step 1.0');
    value.value = '90';
    value.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('UpdateStackStep')).toEqual([11, step(7, 90, { wait: 0 })]);

    // a number typed for a step: the desk decides, and the field shows the old one until it answers
    const cue = page.querySelector<HTMLInputElement>('input[aria-label="Number of cue 2.0"]');
    if (!cue) throw new Error('no number field for step 2.0');
    cue.value = ' 0.5 ';
    cue.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('RenumberStackStep')).toEqual([12, '0.5']);
    expect(cue.value).toBe('2.0');

    await press(button('Move cue 2.0 up'));
    expect(connection.last('MoveStackStep')).toEqual([12, null]); // to the very top
    await press(button('Move cue 1.0 down'));
    expect(connection.last('MoveStackStep')).toEqual([11, 12]); // behind the step that follows
    expect(button('Move cue 1.0 up').disabled).toBe(true);

    await press(button('Take cue 2.0 out'));
    expect(connection.last('DeleteStackStep')).toEqual([12]);

    // the desk sends the stack after a change: the table follows
    connection.emit('StackChanged', { steps: [connection.stack.steps[0]] });
    await fixture.whenStable();
    expect(page.querySelectorAll('lm-stack-view table.steps tbody tr').length).toBe(1);
  });

  it('the + key of a step puts a new one right after it, and rests while the stack is live', async () => {
    await show('/stack');

    // the step Add step would make after that one: the next channel or memory, with its times
    await press(button('Add a cue after cue 1.0'));
    expect(connection.last('AddStackStepAfter')).toEqual([11, step(8, 255, { wait: 0 })]);
    await press(button('Add a cue after cue 2.0'));
    expect(connection.last('AddStackStepAfter')).toEqual([
      12,
      step(3, 128, { target: 'memory', wait: 3 }),
    ]);

    await stackStands(LIVE);
    expect(button('Add a cue after cue 1.0').disabled).toBe(true);
  });

  it('the Add box makes a step come up on top of what is on, and the steps that are on are marked', async () => {
    await show('/stack');
    const box = () =>
      page.querySelector<HTMLInputElement>('input[aria-label="Cue 2.0 adds on top of what is on"]');
    expect(box()?.checked).toBe(false);

    box()?.click();
    await fixture.whenStable();
    expect(connection.last('UpdateStackStep')).toEqual([
      12,
      { ...step(2, 128, { target: 'memory', wait: 3 }), add: true },
    ]);

    // the desk takes it, and goes live on that step with the first one still on under it
    const adding = {
      steps: [connection.stack.steps[0], { ...connection.stack.steps[1], add: true }],
    };
    connection.emit('StackChanged', adding);
    await stackStands({ ...LIVE, currentId: 12, nextId: 11, on: [11, 12] });
    const rows = () => [...page.querySelectorAll('lm-stack-view table.steps tbody tr')];
    expect(rows().map((row) => row.classList.contains('on'))).toEqual([true, true]);
    expect(rows().map((row) => row.classList.contains('current'))).toEqual([false, true]);
    expect(box()?.checked).toBe(true);
    expect(box()?.disabled).toBe(true); // a live stack keeps its steps
    expect(page.querySelector('.card.current .what')?.textContent).toContain(
      'Memory 2 (Wash), adds',
    );

    // unchecked again: the desk is told without "add"
    await stackStands(NOT_LIVE);
    box()?.click();
    await fixture.whenStable();
    expect(connection.last('UpdateStackStep')).toEqual([
      12,
      step(2, 128, { target: 'memory', wait: 3 }),
    ]);
    expect(box()?.checked).toBe(false);

    // the Stage view ticks the steps that add, and marks those that are on
    connection.emit('StackChanged', adding);
    await show('/stage');
    await stackStands({ ...LIVE, currentId: 12, nextId: 11, on: [11, 12] });
    const stageRows = [...page.querySelectorAll('lm-stage-stack table.steps tbody tr')];
    expect(stageRows.map((row) => row.querySelector('td.adds')?.textContent?.trim())).toEqual([
      '',
      '✓adds',
    ]);
    expect(stageRows.map((row) => row.classList.contains('on'))).toEqual([true, true]);
  });

  it('a release takes one step of the stack down, chosen from the other steps by their numbers', async () => {
    await show('/stack');
    const kind = page.querySelector<HTMLSelectElement>(
      'select[aria-label="Cue 2.0, channel, memory, fixture, group, effect, sound or release"]',
    );
    if (!kind) throw new Error('no field for the kind of step 2.0');
    expect([...kind.options].map((option) => option.text)).toContain('Release');

    kind.value = 'release';
    kind.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    // at first it takes down the step right above it; it never adds
    expect(connection.last('UpdateStackStep')).toEqual([
      12,
      { target: 'release', number: 11, value: 128, up: 1, wait: 3, down: 1 },
    ]);

    // the row: the one it takes down by its number and what that step is; no value, no up time, no Add box
    const row = page.querySelectorAll('lm-stack-view table.steps tbody tr')[1];
    const number = row.querySelector<HTMLSelectElement>(
      'select[aria-label="Cue 2.0, the one it takes down"]',
    );
    if (!number) throw new Error('no field for the step that step 2.0 takes down');
    expect(number.options[0].text.trim()).toBe('1.0');
    expect(row.querySelector('td.note')?.textContent?.trim()).toBe('Channel 7 (U1)');
    expect(row.querySelectorAll('td.unused').length).toBe(2);
    expect(row.querySelector('input[aria-label="Cue 2.0, value, 0 to 255"]')).toBeNull();
    expect(row.querySelector('input[aria-label="Cue 2.0 adds on top of what is on"]')).toBeNull();
    number.dispatchEvent(new Event('focus'));
    await fixture.whenStable();
    expect([...number.options].map((option) => option.text.trim())).toEqual([
      '1.0: Channel 7 (U1)',
    ]); // never itself

    // the cards say what it does
    await stackStands({
      ...LIVE,
      currentId: 12,
      nextId: 11,
      state: 'fading',
      remaining: 0.4,
      on: [],
    });
    expect(page.querySelector('.card.current .what')?.textContent).toContain(
      'Release of cue 1.0: Channel 7 (U1)',
    );
    expect(page.querySelector('.card.current .how')?.textContent).toContain(
      'Taking its cue down, 0.4 s left.',
    );

    // a sequence has releases too, of its own steps
    await stackStands(NOT_LIVE);
    await show('/sequences');
    await press(button('Work on memory 3'));
    const sequenceKind = page.querySelector<HTMLSelectElement>(
      'select[aria-label="Step 1, channel, memory, fixture, group, effect, sound or release"]',
    );
    expect([...(sequenceKind?.options ?? [])].map((option) => option.value)).toContain('release');
  });

  it('while the stack crossfades, the page shows what the desk sends: a channel both looks hold does not dip', async () => {
    // memory 2 holds channel 5 at 200 (see above); memory 5 holds it at 150
    const look = new Array<number>(UNIVERSE_SIZE).fill(0);
    look[4] = 150;
    store.memoryLooks.update((looks) => ({ ...looks, 5: look }));
    // half way through the fade: memory 2 on its way down, memory 5 on its way up
    connection.emit('MemoryLevelChanged', 2, 128);
    connection.emit('MemoryLevelChanged', 5, 128);
    await fixture.whenStable();
    expect(store.output()[4]).toBe(100); // the higher of the two alone: the dip the desk does not send

    const levels = new Array<number>(UNIVERSE_SIZE).fill(0);
    levels[4] = 175; // half way from 200 to 150
    levels[69] = 128;
    connection.emit('PlaybackChanged', {
      ...connection.playback,
      crossfade: { channels: [], memories: [2, 5], levels },
    });
    await fixture.whenStable();
    expect(store.output()[4]).toBe(175);
    expect(store.shownChannels()[4]).toBe(175);
    expect(store.output()[69]).toBe(128);

    // a key held with SOLO DOWN on one of its faders: every fader counts on its own again, as in the desk program
    store.solo.set({ up: [], down: [{ target: 'memory', number: 5 }] });
    await fixture.whenStable();
    expect(store.output()[4]).toBe(100);

    // the fade is over: the faders give the light on their own
    store.solo.set({ up: [], down: [] });
    connection.emit('MemoryLevelChanged', 2, 0);
    connection.emit('MemoryLevelChanged', 5, 255);
    connection.emit('PlaybackChanged', { ...connection.playback });
    await fixture.whenStable();
    expect(store.output()[4]).toBe(150);
  });

  it('a step of the stack is moved by dragging its grip onto another row', async () => {
    await show('/stack');
    const rows = [...page.querySelectorAll<HTMLElement>('lm-stack-view table.steps tbody tr')];
    const grip = rows[1].querySelector<HTMLElement>('.grip');
    if (!grip) throw new Error('no grip on the second row');

    // jsdom knows no layout: every row is at height 0, so a pointer at -1 is "above the middle"
    const over = Object.assign(new Event('dragover', { bubbles: true, cancelable: true }), {
      clientY: -1,
    });
    grip.dispatchEvent(new Event('dragstart', { bubbles: true }));
    rows[0].dispatchEvent(over);
    await fixture.whenStable();
    expect(over.defaultPrevented).toBe(true); // or the browser would allow no drop
    expect(rows[0].classList.contains('drop-before')).toBe(true);

    rows[0].dispatchEvent(new Event('drop', { bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(connection.last('MoveStackStep')).toEqual([12, null]); // before the first step: the very top
    expect(rows[0].classList.contains('drop-before')).toBe(false);
  });

  // ---------------------------------------------------------------- the stack: previous, and the main view

  it('the stack view shows the step before the current one, and its keys are bigger, Play the biggest', async () => {
    await show('/stack');
    const rows = () => [...page.querySelectorAll('lm-stack-view table.steps tbody tr')];

    // On the second step the first is the one before it.
    await stackStands({ ...LIVE, currentId: 12, nextId: 11 });
    expect(rows().map((row) => row.classList.contains('previous'))).toEqual([true, false]);
    expect(page.querySelector('.card.previous')?.classList.contains('on')).toBe(true);
    expect(page.querySelector('.card.previous .cue')?.textContent?.trim()).toBe('Cue 1.0');

    // On the first step it is the last: Prev goes round, as Play does.
    await stackStands(LIVE);
    expect(page.querySelector('.card.previous .cue')?.textContent?.trim()).toBe('Cue 2.0');

    // Not live: no step is on, so none is before it.
    await stackStands(NOT_LIVE);
    expect(page.querySelector('.card.previous')?.classList.contains('on')).toBe(false);
    expect(rows().some((row) => row.classList.contains('previous'))).toBe(false);

    expect(button('GO').classList).toContain('play');
    expect(button('GO').closest('.transport')).toBeTruthy();
    expect(button('Back to the cue before').closest('.transport')).toBeTruthy();
  });

  it('the main view has the stack across the top at the right, with a wheel of its steps and their notes', async () => {
    const section = page.querySelector('lm-stack-section');
    if (!section) throw new Error('no stack section on the main view');
    // The places in the window of the wheel, from the top, as [number, note]: one more on each
    // side only rolls in. A step without a note says what it is.
    const wheel = () =>
      [...section.querySelectorAll('.roller li')]
        .slice(1, -1)
        .map((place) => [
          place.querySelector('.cue')?.textContent?.trim(),
          place.querySelector('.note')?.textContent?.trim(),
        ]);

    expect(wheel()).toEqual([
      ['', ''],
      ['', ''],
      ['', ''],
      ['-', ''],
      ['1.0', 'Channel 7 (U1)'],
      ['2.0', 'Wash'],
      ['', ''],
    ]); // nothing is on; Play goes to the first step, then the second; 7 places at first
    expect(section.querySelector('.roller .next')?.getAttribute('aria-label')).toBe(
      'Next cue: 1.0, Channel 7 (U1)',
    );
    expect(section.querySelector('.pointer')).toBeTruthy(); // the arrow beside the middle
    expect(button('GO').disabled).toBe(true); // only while the stack is live

    await press(button('Stack live'));
    expect(connection.last('SetStackLive')).toEqual([true]);

    // Two steps: the other one stands above and below, as Prev and Play both go to it.
    await stackStands(LIVE);
    expect(wheel()).toEqual([
      ['', ''],
      ['', ''],
      ['2.0', 'Wash'],
      ['1.0', 'Channel 7 (U1)'],
      ['2.0', 'Wash'],
      ['', ''],
      ['', ''],
    ]);
    expect(section.querySelector('.roller .current')?.classList.contains('empty')).toBe(false);
    expect(section.querySelector('.roller .current')?.getAttribute('aria-label')).toBe(
      'Current cue: 1.0, Channel 7 (U1)',
    );
    expect(section.querySelector('.roller .previous')?.getAttribute('aria-label')).toBe(
      'Previous cue: 2.0, Wash',
    );

    await press(button('GO'));
    await press(button('Pause'));
    await press(button('Next cue'));
    await press(button('Back to the cue before'));
    expect(connection.methods()).toEqual([
      'SetStackLive',
      'StackPlay',
      'StackPause',
      'StackNext',
      'StackPrevious',
    ]);
  });

  it('Disabled skips a step of the stack, also while the stack is live, but not the step that is on', async () => {
    await show('/stack');
    const box = (cue: string) =>
      page.querySelector<HTMLInputElement>(`input[aria-label="Cue ${cue} is skipped"]`)!;
    const row = (index: number) =>
      page.querySelectorAll('lm-stack-view table.steps tbody tr')[index];
    expect(
      [...page.querySelectorAll('lm-stack-view table.steps thead th')].at(-1)?.textContent?.trim(),
    ).toBe('Disabled');

    await press(box('2.0'));
    expect(connection.last('SetStackStepDisabled')).toEqual([12, true]);
    expect(row(1).classList).toContain('skipped');
    expect(store.stack().steps[1].disabled).toBe(true);

    // an edit of the step keeps it skipped
    const value = row(1).querySelector<HTMLInputElement>(
      'input[aria-label="Cue 2.0, value, 0 to 255"]',
    )!;
    value.value = '100';
    value.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('UpdateStackStep')?.[1]).toMatchObject({ value: 100, disabled: true });

    // live: the box works, but not for the step that is on
    await stackStands(LIVE);
    expect(box('1.0').disabled).toBe(true);
    expect(box('2.0').disabled).toBe(false);
    await press(box('2.0'));
    expect(connection.last('SetStackStepDisabled')).toEqual([12, false]);
    expect(row(1).classList).not.toContain('skipped');
  });

  it('At gives a cue the moment of a sound it comes at, in a window, also while the stack is live', async () => {
    connection.emit('SoundsChanged', [aSound(2, { name: 'Rain' }), aSound(4, { name: 'Bell' })]);
    await show('/stack');
    const heads = [...page.querySelectorAll('lm-stack-view table.steps thead th')];
    expect(heads.map((head) => head.textContent?.trim()).indexOf('At')).toBe(11); // right after Down (s)
    const key = (cue: string) =>
      page.querySelector<HTMLButtonElement>(`lm-stack-view td.moment button[aria-label^="Cue ${cue} comes"]`)!;
    expect(key('2.0').textContent?.trim()).toBe('—');
    expect(key('2.0').getAttribute('aria-label')).toBe(
      'Cue 2.0 comes with GO: give it a moment of a sound',
    );

    // nothing plays yet: the field is empty, nothing is said against it, and Use waits for a moment
    await press(key('1.0'));
    expect(page.querySelector<HTMLInputElement>('#cue-moment-time')?.value).toBe('');
    expect(page.querySelector('lm-cue-moment-dialog .hint')?.classList).not.toContain('problem');
    expect(buttonIn('lm-cue-moment-dialog', 'Use this moment').disabled).toBe(true);
    await press(buttonIn('lm-cue-moment-dialog', 'Cancel'));

    // live, with sound 4 playing at 7.25 s: the window opens on that sound, at that moment
    await stackStands(LIVE);
    connection.emit('SoundPlaybackChanged', { ...quietPlayback(4), playing: true, level: 255, position: 7.25 });
    await fixture.whenStable();
    expect(key('2.0').disabled).toBe(false);
    await press(key('2.0'));
    const dialog = page.querySelector<HTMLDialogElement>('lm-cue-moment-dialog dialog')!;
    expect(dialog.open).toBe(true);
    expect(dialog.querySelector('h2')?.textContent?.trim()).toBe('Moment of cue 2.0');
    const sound = dialog.querySelector<HTMLSelectElement>('#cue-moment-sound')!;
    const time = dialog.querySelector<HTMLInputElement>('#cue-moment-time')!;
    expect(sound.value).toBe('4');
    expect(time.value).toBe('0:07.25');
    expect(dialog.querySelector('.where')?.textContent?.trim()).toBe('It plays at 0:07.25.');
    expect(() => buttonIn('lm-cue-moment-dialog', 'None: the cue waits for GO again')).toThrow(); // it has none

    // a moment the sound never comes to cannot be used; one typed as seconds, with a comma, can
    time.value = '0:13';
    time.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(dialog.querySelector('.hint')?.textContent).toContain('The sound is 0:12.50 long');
    expect(buttonIn('lm-cue-moment-dialog', 'Use this moment').disabled).toBe(true);
    time.value = '3,5';
    time.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    await choose('#cue-moment-sound', '2');
    expect(dialog.querySelector('.where')?.textContent?.trim()).toBe('It is quiet.'); // sound 2 does not play
    await press(buttonIn('lm-cue-moment-dialog', 'Use this moment'));
    expect(dialog.open).toBe(false);
    expect(connection.last('SetStackStepAt')).toEqual([12, { sound: 2, seconds: 3.5 }]);

    // the desk sends the stack with the moment: the row, the next card and the wave show it
    connection.emit('StackChanged', {
      steps: [connection.stack.steps[0], { ...connection.stack.steps[1], at: { sound: 4, seconds: 75.5 } }],
    });
    await fixture.whenStable();
    expect(key('2.0').textContent?.trim()).toBe('1:15.50 of Bell');
    expect(key('2.0').getAttribute('aria-label')).toBe('Cue 2.0 comes at 1:15.50 of Bell: change the moment');
    expect(page.querySelector('.card.next .how')?.textContent?.trim()).toBe(
      'Comes at 1:15.50 of Bell. Up in 1.0 s, then goes on after 3.0 s.',
    );
    expect(page.querySelector('.card.current .how')?.textContent?.trim()).toBe(
      'Up. The stack waits for 1:15.50 of Bell, or GO.',
    );
    expect(page.querySelector('lm-sound-now lm-sound-wave')?.getAttribute('aria-label')).toContain(
      '1 cue comes at its moments',
    );

    // the window opens on the cue's own moment; None takes it away
    await press(key('2.0'));
    expect(sound.value).toBe('4');
    expect(time.value).toBe('1:15.50');
    await press(buttonIn('lm-cue-moment-dialog', 'None: the cue waits for GO again'));
    expect(connection.last('SetStackStepAt')).toEqual([12, null]);

    // a change of the cue's fields keeps its moment: the desk takes the cue as it is sent
    await stackStands(NOT_LIVE);
    const value = page.querySelector<HTMLInputElement>('input[aria-label="Cue 2.0, value, 0 to 255"]')!;
    value.value = '100';
    value.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('UpdateStackStep')?.[1]).toMatchObject({ value: 100, at: { sound: 4, seconds: 75.5 } });
  });

  it('Learn: while it is lit, each GO stores the moment of the sound that plays into the cue it goes to', async () => {
    await show('/stack');
    const learn = () => button('Learn: GO stores the moment of the sound that plays');
    expect(learn().getAttribute('aria-pressed')).toBe('false');
    await press(learn());
    expect(connection.last('SetStackLearning')).toEqual([true]);

    // the desk says it learns: the key blinks, the summary says so
    await stackStands({ ...LIVE, learning: true });
    expect(learn().getAttribute('aria-pressed')).toBe('true');
    expect(learn().className).toContain('level-3');
    expect(page.querySelector('lm-stack-view .summary')?.textContent).toContain('Learn is on');
    await press(learn());
    expect(connection.last('SetStackLearning')).toEqual([false]);

    // a shortcut or a controller can switch it as well
    expect(actionWithId('stack.learn')?.label).toBe('Learn (Stack view)');
    expect(actionLamp('stack.learn', store, '/').blink).toBe(true);
  });

  it('the cue sheet shows every cue with its times and room to write, and Print prints it', async () => {
    connection.emit('SoundsChanged', [aSound(4, { name: 'Bell' })]);
    connection.emit('StackChanged', {
      steps: [connection.stack.steps[0], { ...connection.stack.steps[1], at: { sound: 4, seconds: 75.5 } }],
    });
    await show('/stack');
    expect(page.querySelector('lm-stack-view a.sheet')?.getAttribute('href')).toBe('/cue-sheet');

    await show('/cue-sheet');
    expect(page.querySelector('lm-cue-sheet header')?.textContent).toContain('2 cues');
    const cells = (index: number) =>
      [...page.querySelectorAll('lm-cue-sheet tbody tr')[index].querySelectorAll('th, td')].map(
        (cell) => cell.textContent?.trim(),
      );
    // cue, what, note, value, with, up, wait, down, at, and the column to write in
    expect(cells(0)[0]).toBe('1.0');
    expect(cells(0)[1]).toBe('Channel 7 (U1)');
    expect(cells(0).slice(3)).toEqual(['255', '', '1 s', 'GO', '1 s', '', '']); // a wait of 0 waits for GO
    expect(cells(1).slice(0, 4)).toEqual(['2.0', 'Memory 2', 'Wash', '128']);
    expect(cells(1)[6]).toBe('3 s');
    expect(cells(1)[8]).toBe('1:15.50 of Bell'); // the moment of a sound it comes at

    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    await press(button('Print'));
    expect(print).toHaveBeenCalledOnce();
    print.mockRestore();
  });
});
