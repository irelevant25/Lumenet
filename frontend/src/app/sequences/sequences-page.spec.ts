import { ComponentFixture } from '@angular/core/testing';
import { App } from '../app';
import { DeskStore } from '../desk/desk-store';
import { Sequence, UNIVERSE_SIZE } from '../desk/desk-types';
import { deskPageHelpers, FakeConnection, startDeskPage, step } from '../testing/desk-page.testing';

describe('the desk page: sequences', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;
  const { show, button, memoryKey, press, twoUniverses, choose } = deskPageHelpers(() => ({
    connection,
    fixture,
    page,
    store,
  }));

  beforeEach(async () => {
    ({ connection, fixture, page, store } = await startDeskPage());
  });

  it('the kind of a step says the universe of its channel, and the number list holds that universe only', async () => {
    connection.emit('DeskChanged', twoUniverses());
    await fixture.whenStable();
    await show('/sequences');
    await press(button('Work on memory 3'));
    const row = () => page.querySelectorAll('lm-sequences-view table.steps tbody tr')[0];
    const kind = () =>
      row().querySelector<HTMLSelectElement>(
        'select[aria-label="Step 1, channel, memory, fixture, group, effect, sound or release"]',
      )!;
    const number = () =>
      row().querySelector<HTMLSelectElement>('select[aria-label="Step 1, number of the channel"]')!;
    expect([...kind().options].map((option) => option.text.trim())).toEqual([
      'Channel (U1)',
      'Channel (U2)',
      'Memory',
      'Fixture',
      'Group',
      'Effect',
      'Sound',
      'Release',
    ]);
    expect(kind().value).toBe('channel:1');

    // to U2: channel 5 stays channel 5, of the second universe
    kind().value = 'channel:2';
    kind().dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect((connection.last('SetSequence')?.[1] as Sequence).steps[0]).toEqual(step(517, 200));
    expect(kind().value).toBe('channel:2');
    expect(number().options[0].text.trim()).toBe('5');
    number().dispatchEvent(new Event('focus'));
    expect(number().options.length).toBe(512);
    expect([number().options[0].value, number().options[511].value]).toEqual(['513', '1024']);
    expect(number().options[0].text.trim()).toBe('1');
  });

  it('a memory that holds a sequence has a key that runs from red to yellow', async () => {
    expect(memoryKey(3).className).toContain('tone-sequence');
    expect(memoryKey(2).className).toContain('tone-memory'); // a look stays red

    await show('/memories');
    expect(button('Look at memory 3').className).toContain('tone-sequence');
    await press(button('Look at memory 3'));
    expect(page.querySelector('.summary')?.textContent).toContain('Memory 3 holds a sequence');
  });

  it('a running sequence moves the channel faders it uses', async () => {
    const channel5 = page.querySelector<HTMLElement>('lm-fader[aria-label="Channel 5 (U1)"]');
    const levels = new Array<number>(UNIVERSE_SIZE).fill(0);
    levels[4] = 77;
    connection.emit('PlaybackChanged', { ...connection.playback, levels, sequenceSteps: { 3: 1 } });
    await fixture.whenStable();

    expect(channel5?.getAttribute('aria-valuenow')).toBe('77');
    expect(store.channels()[4]).toBe(0); // shown, not set: the fader's own level stays
  });

  it('the sequences view shows the steps of a memory and sends every change to the desk', async () => {
    await show('/sequences');
    const rows = () => [...page.querySelectorAll('lm-sequences-view table.steps tbody tr')];
    const field = (row: number, label: string) =>
      rows()[row].querySelector<HTMLInputElement | HTMLSelectElement>(
        `[aria-label^="Step ${row + 1}, ${label}"]`,
      );

    // memory 1 is worked on first, and it is empty; memory 2 holds a look
    expect(page.querySelector('.summary')?.textContent).toContain('Memory 1 is empty');
    await press(button('Work on memory 2'));
    expect(page.querySelector('lm-sequences-view .hint')?.textContent).toContain('Empty memory 2');
    expect(page.querySelector('lm-sequences-view table')).toBeNull();

    await press(button('Work on memory 3'));
    expect(page.querySelector('.summary')?.textContent).toContain(
      'a sequence of 2 steps, played once',
    );
    expect(rows().length).toBe(2);
    expect(field(0, 'number')?.value).toBe('5');
    expect(field(0, 'value')?.value).toBe('200');
    expect(field(1, 'wait')?.value).toBe('0.0');

    // a value typed into a field
    const value = field(1, 'value');
    if (!value) throw new Error('no value field in step 2');
    value.value = '150';
    value.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetSequence')).toEqual([
      3,
      { loop: false, random: false, speed: 1, steps: [step(5, 200), step(6, 150, { wait: 0 })] },
    ]);

    // a time: a comma is taken, the text is shown with one digit, nonsense is put back
    const up = field(0, 'up');
    if (!up) throw new Error('no up field in step 1');
    up.value = '2,5';
    up.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.sequences()[3].steps[0].up).toBe(2.5);
    up.value = 'fast';
    up.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(up.value).toBe('2.5');
    expect(store.sequences()[3].steps[0].up).toBe(2.5);

    // Add step: one like the last, on the next channel
    await press(button('Add a step'));
    expect(store.sequences()[3].steps.length).toBe(3);
    expect(store.sequences()[3].steps[2]).toEqual(step(7, 150, { wait: 0 }));

    await press(button('Loop'));
    expect(store.sequences()[3].loop).toBe(true);
    expect((connection.last('SetSequence')?.[1] as Sequence).loop).toBe(true);

    await press(button('Move step 3 up'));
    expect(store.sequences()[3].steps.map((item) => item.number)).toEqual([5, 7, 6]);
    await press(button('Take step 1 out'));
    expect(store.sequences()[3].steps.map((item) => item.number)).toEqual([7, 6]);
  });

  it('the sequences view marks the step a running sequence is on, and starts one in an empty memory', async () => {
    await show('/sequences');
    await press(button('Work on memory 3'));
    connection.emit('PlaybackChanged', { ...connection.playback, sequenceSteps: { 3: 2 } });
    await fixture.whenStable();

    const rows = [...page.querySelectorAll('lm-sequences-view table.steps tbody tr')];
    expect(rows.map((row) => row.classList.contains('current'))).toEqual([false, true]);
    expect(page.querySelector('.summary')?.textContent).toContain('It is running: step 2');

    await press(button('Work on memory 9'));
    expect(button('Loop').getAttribute('aria-pressed')).toBe('true'); // what a new sequence will be
    await press(button('Add a step'));
    expect(connection.last('SetSequence')).toEqual([
      9,
      { loop: true, random: false, speed: 1, steps: [step(1, 255)] },
    ]);
    expect(store.memorySequence()[8]).toBe(true);
  });

  it('the sequences view marks the steps that are on, with a step that adds', async () => {
    await show('/sequences');
    await press(button('Work on memory 3'));
    connection.emit('PlaybackChanged', {
      ...connection.playback,
      sequenceSteps: { 3: 2 },
      sequenceOn: { 3: [1, 2] },
    });
    await fixture.whenStable();

    const rows = [...page.querySelectorAll('lm-sequences-view table.steps tbody tr')];
    expect(rows.map((row) => row.classList.contains('on'))).toEqual([true, true]);
    expect(rows.map((row) => row.classList.contains('current'))).toEqual([false, true]);
  });

  it('a step of a sequence adds, releases another step or plays an effect, and a release follows its step', async () => {
    await show('/sequences');
    await press(button('Work on memory 3'));
    const rows = () => [...page.querySelectorAll('lm-sequences-view table.steps tbody tr')];
    const sent = () => (connection.last('SetSequence')?.[1] as Sequence).steps;
    const adds = (row: number) => rows()[row].querySelector<HTMLInputElement>('td.adds input');
    const kind = (row: number) =>
      `select[aria-label="Step ${row}, channel, memory, fixture, group, effect, sound or release"]`;

    // Add: the box of step 2; a step without it has no "add" at all
    expect(adds(1)?.checked).toBe(false);
    await press(adds(1));
    expect(sent()).toEqual([step(5, 200), step(6, 100, { wait: 0, add: true })]);
    expect(adds(1)?.checked).toBe(true);

    // a release: of the step above it at first, chosen by its number; it has no Add, no value and no up time
    await press(button('Add a step'));
    expect(sent()[2]).toEqual(step(7, 100, { wait: 0 })); // like step 2, without its Add
    await choose(kind(3), 'release');
    expect(sent()[2]).toEqual({
      target: 'release',
      number: 2,
      value: 100,
      up: 1,
      wait: 0,
      down: 1,
    });
    expect(adds(2)).toBeNull();
    expect(rows()[2].querySelectorAll('td.unused').length).toBe(2);
    expect(rows()[2].querySelector('td.note')?.textContent?.trim()).toBe(
      'Channel 6 (U1) (Front left)',
    );
    const released = rows()[2].querySelector<HTMLSelectElement>(
      'select[aria-label="Step 3, the one it takes down"]',
    )!;
    released.dispatchEvent(new Event('focus'));
    expect([...released.options].map((option) => option.text.trim())).toEqual([
      '1: Channel 5 (U1)',
      '2: Channel 6 (U1) (Front left)',
    ]);

    // moved, a release goes on taking down its step; taken out, the steps after it are numbered anew
    await press(button('Move step 2 down'));
    expect(sent().map((item) => [item.target, item.number])).toEqual([
      ['channel', 5],
      ['release', 3],
      ['channel', 6],
    ]);
    await press(button('Take step 1 out'));
    expect(sent().map((item) => [item.target, item.number])).toEqual([
      ['release', 2],
      ['channel', 6],
    ]);
    await press(button('Take step 2 out')); // the step it took down: it takes down none now
    expect(sent()).toEqual([{ target: 'release', number: 0, value: 100, up: 1, wait: 0, down: 1 }]);
    expect(rows()[0].querySelector('td.note')?.textContent?.trim()).toBe(
      'No step to take down: choose one',
    );

    // an effect, by its number
    await choose(kind(1), 'effect');
    expect(sent()).toEqual([{ target: 'effect', number: 1, value: 100, up: 1, wait: 0, down: 1 }]);
    expect(rows()[0].querySelector('td.note')?.textContent?.trim()).toBe('No such effect yet');
  });

  it('random plays a sequence in a new order every round', async () => {
    await show('/sequences');
    await press(button('Work on memory 3'));
    expect(button('Once').getAttribute('aria-pressed')).toBe('true');

    await press(button('Random'));
    expect(button('Random').getAttribute('aria-pressed')).toBe('true');
    expect(button('Loop').getAttribute('aria-pressed')).toBe('false');
    expect(connection.last('SetSequence')?.[1]).toEqual(
      expect.objectContaining({ loop: true, random: true }),
    );
    expect(page.querySelector('.summary')?.textContent).toContain('played in a random order');

    await press(button('Once'));
    expect(connection.last('SetSequence')?.[1]).toEqual(
      expect.objectContaining({ loop: false, random: false }),
    );
  });

  it('the speed fader makes a sequence faster or slower without starting it again', async () => {
    await show('/sequences');
    await press(button('Work on memory 3'));
    const fader = page.querySelector<HTMLElement>('lm-fader[aria-label="Speed of the sequence"]');
    if (!fader) throw new Error('no speed fader');
    expect(fader.getAttribute('aria-valuetext')).toBe('1.00 times');
    expect(page.querySelector('.speed-value')?.textContent?.trim()).toBe('1.00×');
    expect(button('Normal speed').disabled).toBe(true);

    // right of the middle: faster, in steps of 0.5 with the keys
    fader.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight' }));
    await fixture.whenStable();
    expect(store.sequences()[3].speed).toBe(1.5);
    expect(page.querySelector('.speed-value')?.textContent?.trim()).toBe('1.50×');
    await new Promise((resolve) => setTimeout(resolve, 40)); // sent like a fader move, a moment later
    expect(connection.last('SetSequenceSpeed')).toEqual([3, 1.5]);
    expect(connection.methods()).not.toContain('SetSequence'); // the steps stay as they are

    // left of the middle: slower, in steps of 0.05
    await press(button('Normal speed'));
    fader.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft' }));
    await fixture.whenStable();
    expect(store.sequences()[3].speed).toBe(0.95);
    fader.dispatchEvent(new KeyboardEvent('keydown', { key: 'Home' }));
    await fixture.whenStable();
    expect(store.sequences()[3].speed).toBe(0.1);
    fader.dispatchEvent(new KeyboardEvent('keydown', { key: 'End' }));
    await fixture.whenStable();
    expect(store.sequences()[3].speed).toBe(10);

    // another page changes it: this one follows
    connection.emit('SequenceSpeedChanged', 3, 2.25);
    await fixture.whenStable();
    expect(page.querySelector('.speed-value')?.textContent?.trim()).toBe('2.25×');
    expect(page.querySelector('.summary')?.textContent).toContain('at 2.25× speed');
  });

  it('a template fills the sequence of the memory with steps that can be changed afterwards', async () => {
    await show('/sequences');
    await press(button('Work on memory 9'));
    await press(button('Make the steps from a template'));
    const dialog = page.querySelector('lm-template-dialog dialog');
    expect(dialog?.querySelector('h2')?.textContent).toContain('memory 9');
    expect(button('Fire').getAttribute('aria-pressed')).toBe('true');

    // lights that do not exist are refused with a word why
    const numbers = page.querySelector<HTMLInputElement>(
      'input[aria-label="Numbers of the lights, like 1, 2 or 1-4"]',
    );
    if (!numbers) throw new Error('no field for the lights');
    numbers.value = '1, 600';
    numbers.dispatchEvent(new Event('input'));
    await press(button('Make the steps'));
    expect(page.querySelector('lm-template-dialog .error')?.textContent).toContain('from 1 to 512');
    expect(connection.methods()).not.toContain('SetSequence');

    numbers.value = '1, 2';
    numbers.dispatchEvent(new Event('input'));
    await press(button('Make the steps'));
    const made = connection.last('SetSequence') as [number, Sequence];
    expect(made[0]).toBe(9);
    expect(made[1].random).toBe(true);
    expect(made[1].steps.length).toBe(16);
    expect(page.querySelectorAll('lm-sequences-view table.steps tbody tr').length).toBe(16);
    expect(page.querySelector('.summary')?.textContent).toContain(
      '16 steps, played in a random order',
    );

    // another template over it: a chase of the two lights, in turn
    await press(button('Make the steps from a template'));
    expect(page.querySelector('lm-template-dialog .hint')?.textContent).toContain(
      'replaces the 16 steps of memory 9',
    );
    await press(button('Chase'));
    await press(button('Make the steps'));
    expect(
      (connection.last('SetSequence')?.[1] as Sequence).steps.map((item) => item.number),
    ).toEqual([1, 2]);
    expect(button('Loop').getAttribute('aria-pressed')).toBe('true');
  });

  it('the number of a step is chosen from a list that shows the notes under the faders', async () => {
    await show('/sequences');
    await press(button('Work on memory 3'));
    const rows = () => [...page.querySelectorAll('lm-sequences-view table.steps tbody tr')];
    const numberOf = (row: number) =>
      rows()[row].querySelector<HTMLSelectElement>(
        `select[aria-label="Step ${row + 1}, number of the channel"]`,
      );
    const second = numberOf(1);
    if (!second) throw new Error('no number field in step 2');

    // the note of the step's channel stands beside its number: step 2 is on channel 6
    const headers = [...page.querySelectorAll('lm-sequences-view table.steps thead th')];
    expect(headers.map((header) => header.textContent?.trim()).slice(0, 7)).toEqual([
      'Step',
      'Add',
      'Continue',
      'Kind',
      'Number',
      'Notes',
      'Value',
    ]);
    expect(rows()[1].querySelector('td.note')?.textContent?.trim()).toBe('Front left');
    expect(rows()[0].querySelector('td.note')?.textContent?.trim()).toBe('');

    // a row holds the long list only while its field is in use
    expect(second.value).toBe('6');
    expect(second.options.length).toBe(1);
    second.dispatchEvent(new Event('focus'));
    expect(second.options.length).toBe(512); // at once, before the browser opens the list
    expect(second.value).toBe('6');
    expect(second.options[4].text).toBe('5'); // a channel without a note: its number alone (the kind says the universe)
    expect(second.options[5].text).toBe('6: Front left'); // the note's two lines, one space between

    second.value = '70';
    second.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetSequence')).toEqual([
      3,
      { loop: false, random: false, speed: 1, steps: [step(5, 200), step(70, 100, { wait: 0 })] },
    ]);

    // another row takes the list over
    numberOf(0)?.dispatchEvent(new Event('focus'));
    await fixture.whenStable();
    expect(numberOf(0)?.options.length).toBe(512);
    expect(second.options.length).toBe(1);
    expect(second.value).toBe('70');

    // a field that is left holds its number alone again
    numberOf(0)?.dispatchEvent(new Event('blur'));
    await fixture.whenStable();
    expect(numberOf(0)?.options.length).toBe(1);
    expect(numberOf(0)?.options[0].text.trim()).toBe('5');

    // a step switched to "memory": only the memories that hold a look are on offer, and it lands on one
    const kind = rows()[0].querySelector<HTMLSelectElement>(
      'select[aria-label="Step 1, channel, memory, fixture, group, effect, sound or release"]',
    );
    if (!kind) throw new Error('no kind field in step 1');
    kind.value = 'memory';
    kind.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.sequences()[3].steps[0]).toEqual(step(2, 200, { target: 'memory' }));
    const memories = rows()[0].querySelector<HTMLSelectElement>(
      'select[aria-label="Step 1, number of the memory"]',
    );
    memories?.dispatchEvent(new Event('focus'));
    expect([...(memories?.options ?? [])].map((option) => option.value)).toEqual(['2']);
    expect(memories?.options[0].text.trim()).toBe('2: Wash');
    expect(rows()[0].querySelector('td.note')?.textContent?.trim()).toBe('Wash');
  });

  it('Disabled skips a step of a sequence, and the dropdowns of a step have the color of its kind', async () => {
    await show('/sequences');
    await press(button('Work on memory 3'));
    const rows = () => [
      ...page.querySelectorAll<HTMLElement>('lm-sequences-view table.steps tbody tr'),
    ];
    await press(page.querySelector<HTMLInputElement>('input[aria-label="Step 2 is skipped"]'));
    expect(connection.last('SetSequenceStepDisabled')).toEqual([3, 2, true]);
    expect(rows()[1].classList).toContain('skipped');

    expect(rows()[0].style.getPropertyValue('--kind')).toBe('var(--key-channel)');
    const kind = rows()[0].querySelector<HTMLSelectElement>('td.kind select')!;
    kind.value = 'memory';
    kind.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(rows()[0].style.getPropertyValue('--kind')).toBe('var(--key-memory)');

    // each line of the list of the kinds has the color of its own kind, not the one chosen
    const colors = [...rows()[0].querySelectorAll<HTMLOptionElement>('td.kind option')].map(
      (option) => [option.value, option.style.getPropertyValue('--kind')],
    );
    expect(colors).toEqual(
      expect.arrayContaining([
        ['channel:1', 'var(--key-channel)'],
        ['memory', 'var(--key-memory)'],
        ['fixture', 'var(--key-channel)'],
        ['group', 'var(--key-system)'],
      ]),
    );
  });
});
