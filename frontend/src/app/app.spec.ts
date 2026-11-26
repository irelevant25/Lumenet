import { ComponentFixture, TestBed } from '@angular/core/testing';
import { App } from './app';
import { DeskSettings } from './desk/desk-settings';
import { DeskStore } from './desk/desk-store';
import { IDLE } from './desk/function-mode';
import { NOTE_LINE_SEPARATOR } from './desk/notes';
import { deskPageHelpers, FakeConnection, startDeskPage, step } from './testing/desk-page.testing';

describe('the desk page', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;
  const { show, button, buttonIn, memoryKey, press, textOf, twoUniverses } = deskPageHelpers(() => ({ connection, fixture, page, store }));

  beforeEach(async () => {
    ({ connection, fixture, page, store } = await startDeskPage());
  });

  /** The key under a channel fader, if its channel is on the page that is shown. */
  function channelKey(channel: number): HTMLButtonElement | null {
    return page.querySelector<HTMLButtonElement>(`button[aria-label^="Channel ${channel} (U1) key"]`);
  }

  it('shows the name, 64 channel faders, 32 memory faders and the page buttons', () => {
    expect(page.querySelector('h1')?.textContent).toContain('Lumenet');
    expect(page.querySelectorAll('lm-channel-section lm-fader-strip').length).toBe(64);
    expect(page.querySelectorAll('lm-memory-section lm-fader-strip').length).toBe(32);
    expect(page.querySelectorAll('nav[aria-label="Channel pages"] button').length).toBe(8);
    expect(page.querySelectorAll('nav[aria-label="Memory pages"] button').length).toBe(4);
    expect(button('Channels 1 to 64 (U1)')).toBeTruthy();
    expect(button('Memories 97 to 128')).toBeTruthy();
  });

  it('moving a fader changes the level at once and tells the backend', async () => {
    const fader = page.querySelector<HTMLElement>('lm-fader[aria-label="Channel 3 (U1)"]');
    fader?.dispatchEvent(new KeyboardEvent('keydown', { key: 'End' }));
    await fixture.whenStable();

    expect(store.channels()[2]).toBe(255);
    expect(fader?.getAttribute('aria-valuenow')).toBe('255');

    await new Promise((resolve) => setTimeout(resolve, 40)); // fader moves are sent a moment later
    expect(connection.calls).toContainEqual({ method: 'SetChannel', args: [3, 255] });
  });

  it('quick steps on a fader all count, also before the page has drawn the first one', async () => {
    const fader = page.querySelector<HTMLElement>('lm-fader[aria-label="Channel 4 (U1)"]');
    for (let step = 0; step < 3; step++) {
      fader?.dispatchEvent(new KeyboardEvent('keydown', { key: 'PageUp' })); // 16 each, no waiting between
    }
    fader?.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    await fixture.whenStable();

    expect(store.channels()[3]).toBe(47);
    expect(fader?.getAttribute('aria-valuenow')).toBe('47');
  });

  it('switching the channel page shows the next 64 channels', async () => {
    await press(button('Channels 65 to 128 (U1)'));
    expect(page.querySelector('lm-fader[aria-label="Channel 65 (U1)"]')).toBeTruthy();
    expect(page.querySelector('lm-fader[aria-label="Channel 1 (U1)"]')).toBeNull();
  });

  it('a key under a fader puts the fader to full, and back to zero when it is at full', async () => {
    const channel = (number: number) => store.channels()[number - 1];
    const memory = (number: number) => store.memoryLevels()[number - 1];

    await press(channelKey(5));
    expect(channel(5)).toBe(255);
    await press(channelKey(5));
    expect(channel(5)).toBe(0);

    // A fader somewhere between goes to full first.
    store.setChannel(6, 100);
    await press(channelKey(6));
    expect(channel(6)).toBe(255);

    await press(memoryKey(3));
    expect(memory(3)).toBe(255);
    await press(memoryKey(3));
    expect(memory(3)).toBe(0);
    await new Promise((resolve) => setTimeout(resolve, 40)); // fader moves are sent a moment later
    expect(connection.calls).toContainEqual({ method: 'SetChannel', args: [6, 255] });
    expect(connection.calls).toContainEqual({ method: 'SetMemoryLevel', args: [3, 255] });
    expect(connection.last('SetMemoryLevel')).toEqual([3, 0]);

    // While a record key waits for a memory, the memory keys are for that, not for their faders.
    await press(button('Save memory'));
    await press(memoryKey(3));
    expect(memory(3)).toBe(0);
    expect(connection.calls).toContainEqual({ method: 'SaveMemory', args: [3] });
    // A channel key still works its fader: a look can be built with the keys before it is saved.
    await press(button('Save memory'));
    await press(channelKey(7));
    expect(channel(7)).toBe(255);
    expect(store.mode()).toEqual({ kind: 'save' });
  });

  it('save: a press on a memory key stores the channel faders there', async () => {
    await press(button('Save memory'));
    expect(button('Save memory').getAttribute('aria-pressed')).toBe('true');

    await press(memoryKey(5));
    expect(connection.calls).toContainEqual({ method: 'SaveMemory', args: [5] });
    expect(button('Save memory').getAttribute('aria-pressed')).toBe('false');
    expect(store.memoryStored()[4]).toBe(true);
  });

  it('edit: loads the memory, and Edit again stores it back', async () => {
    await press(button('Edit memory'));
    await press(memoryKey(7)); // empty: nothing to edit
    expect(connection.methods()).toEqual([]);

    await press(memoryKey(2));
    expect(connection.calls).toContainEqual({ method: 'LoadMemory', args: [2] });

    await press(button('Edit memory'));
    expect(connection.calls).toContainEqual({ method: 'SaveMemory', args: [2] });
  });

  it('copy and delete act on stored memories', async () => {
    await press(button('Copy memory'));
    await press(memoryKey(2));
    await press(memoryKey(9));
    expect(connection.calls).toContainEqual({ method: 'CopyMemory', args: [2, 9] });

    await press(button('Delete memory'));
    await press(memoryKey(2));
    expect(connection.calls).toContainEqual({ method: 'DeleteMemory', args: [2] });
    expect(store.memoryStored()[1]).toBe(false);
  });

  it('copy and delete work on a memory that holds a sequence too, and edit does not', async () => {
    await press(button('Copy memory'));
    await press(memoryKey(3));
    await press(memoryKey(10));
    expect(connection.calls).toContainEqual({ method: 'CopyMemory', args: [3, 10] });
    expect(memoryKey(10).className).toContain('tone-sequence'); // at once, before the desk answers

    await press(button('Edit memory'));
    await press(memoryKey(3)); // a sequence is not edited with the faders
    expect(connection.methods()).not.toContain('LoadMemory');
    expect(store.mode()).toEqual({ kind: 'edit-pick' });
    expect(store.notice()).toContain('Memory 3 holds a sequence');

    await press(button('Delete memory'));
    await press(memoryKey(3));
    expect(connection.calls).toContainEqual({ method: 'DeleteMemory', args: [3] });
    expect(memoryKey(3).className).toContain('tone-memory');
    expect(memoryKey(3).className).toContain('level-1');
  });

  it('the record section has five keys: save mem, edit mem, rec seq, copy, del', () => {
    const keys = [...page.querySelectorAll('lm-record-section button')];
    expect(keys.map((key) => key.textContent?.replace(/\s+/g, ' ').trim())).toEqual([
      'Save mem',
      'Edit mem',
      'Rec seq',
      'Copy',
      'Del',
    ]);
    expect(keys.map((key) => key.getAttribute('aria-label'))).toEqual([
      'Save memory',
      'Edit memory',
      'Record sequence',
      'Copy memory',
      'Delete memory',
    ]);
  });

  it('rec seq: after the memory, every pressed key becomes a step of its sequence', async () => {
    await press(button('Record sequence'));
    await press(memoryKey(2)); // holds a look: no place for a sequence
    expect(store.mode()).toEqual({ kind: 'record-pick' });
    expect(store.notice()).toContain('Memory 2 holds a look');

    await press(memoryKey(9));
    expect(store.mode()).toEqual({ kind: 'recording', memory: 9 });
    expect(memoryKey(9).className).toContain('tone-sequence');
    expect(memoryKey(9).className).toContain('level-3');
    expect(button('Record sequence').getAttribute('aria-pressed')).toBe('true');

    await press(channelKey(7));
    await press(memoryKey(2));
    await press(channelKey(7));
    const added = connection.calls.filter((call) => call.method === 'AddSequenceStep');
    expect(added.map((call) => call.args)).toEqual([
      [9, step(7, 255)],
      [9, step(2, 255, { target: 'memory' })],
      [9, step(7, 255)],
    ]);
    // shown at once: a new sequence loops, and its steps have the usual times
    expect(store.sequences()[9]).toEqual({
      loop: true,
      steps: [step(7, 255), step(2, 255, { target: 'memory' }), step(7, 255)],
    });
    expect(store.memorySequence()[8]).toBe(true);

    // the keys carry the numbers of the steps they are in
    expect(channelKey(7)?.querySelector('.steps')?.textContent).toBe('1,3');
    expect(channelKey(7)?.getAttribute('aria-label')).toBe('Channel 7 (U1) key, in steps 1, 3');
    expect(memoryKey(2).querySelector('.steps')?.textContent).toBe('2');
    expect(memoryKey(2).getAttribute('aria-label')).toBe('Memory 2 key, in step 2');
    expect(channelKey(8)?.querySelector('.steps')).toBeNull();

    // what cannot be a step says why
    await press(memoryKey(3));
    expect(store.notice()).toContain('cannot be a step of another one');
    await press(memoryKey(20));
    expect(store.notice()).toContain('Memory 20 is empty');
    expect(connection.calls.filter((call) => call.method === 'AddSequenceStep').length).toBe(3);

    await press(button('Record sequence')); // done
    expect(store.mode()).toEqual(IDLE);
    expect(page.querySelector('.steps')).toBeNull();
    expect(connection.methods()).not.toContain('SetSequence');
  });

  it('rec seq on a memory that holds a sequence goes on behind its last step', async () => {
    await press(button('Record sequence'));
    await press(memoryKey(3)); // two steps already: channel 5, channel 6

    expect(channelKey(5)?.querySelector('.steps')?.textContent).toBe('1');
    expect(channelKey(6)?.querySelector('.steps')?.textContent).toBe('2');

    await press(channelKey(5));
    expect(connection.last('AddSequenceStep')).toEqual([3, step(5, 255)]);
    expect(store.sequences()[3].steps.length).toBe(3);
    expect(store.sequences()[3].loop).toBe(false); // it stays what it was
    expect(channelKey(5)?.querySelector('.steps')?.textContent).toBe('1,3');
  });

  it('clear cancels an unfinished function and brings every fader to zero', async () => {
    store.setChannel(1, 200);
    store.setMemoryLevel(2, 100);
    await press(button('Copy memory'));

    await press(button('Clear'));
    expect(store.channels()[0]).toBe(0);
    expect(store.memoryLevels()[1]).toBe(0);
    expect(button('Copy memory').getAttribute('aria-pressed')).toBe('false');
    expect(connection.methods()).toEqual(['Clear']); // the fader moves that were waiting are dropped
  });

  it('a note typed under a key is kept and sent to the backend', async () => {
    const field = page.querySelector<HTMLInputElement>('input[aria-label="Note for Channel 5 (U1)"]');
    if (!field) throw new Error('no note field for channel 5');
    expect(field.value).toBe('');

    field.value = '  Front left ';
    field.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(store.channelNotes()[4]).toBe('Front left');
    expect(field.value).toBe('Front left');
    expect(connection.calls).toContainEqual({ method: 'SetChannelNote', args: [5, 'Front left'] });

    const memoryField = page.querySelector<HTMLInputElement>('input[aria-label="Note for Memory 2"]');
    if (!memoryField) throw new Error('no note field for memory 2');
    memoryField.value = 'Blue wash';
    memoryField.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.calls).toContainEqual({ method: 'SetMemoryNote', args: [2, 'Blue wash'] });

    // the second line is a field of its own; the two lines travel as one text
    const second = page.querySelector<HTMLInputElement>(
      'input[aria-label="Note for Channel 5 (U1), second line"]',
    );
    if (!second) throw new Error('no second note line for channel 5');
    second.value = 'PAR';
    second.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.channelNotes()[4]).toBe('Front left\u2028PAR');
    expect(field.value).toBe('Front left');
    expect(second.value).toBe('PAR');
  });

  it('a medium window shows 16 faders in a row and twice as many page keys', async () => {
    store.windowStripsPerRow.set(16);
    await fixture.whenStable();
    expect(page.querySelectorAll('lm-channel-section lm-fader-strip').length).toBe(32);
    expect(page.querySelectorAll('lm-memory-section lm-fader-strip').length).toBe(16);
    expect(page.querySelectorAll('nav[aria-label="Channel pages"] button').length).toBe(16);
    expect(page.querySelectorAll('nav[aria-label="Memory pages"] button').length).toBe(8);

    await press(button('Channels 33 to 64 (U1)'));
    expect(page.querySelector('lm-fader[aria-label="Channel 33 (U1)"]')).toBeTruthy();
    expect(page.querySelector('lm-fader[aria-label="Channel 1 (U1)"]')).toBeNull();

    // wide again: the page that holds channel 33 is the one from 1 to 64
    store.windowStripsPerRow.set(32);
    await fixture.whenStable();
    expect(page.querySelector('lm-fader[aria-label="Channel 1 (U1)"]')).toBeTruthy();
    expect(page.querySelector('lm-fader[aria-label="Channel 64 (U1)"]')).toBeTruthy();
  });

  it('an independent key switches its channel between zero and full', async () => {
    await press(button('Independent 3, channel 3 (U1)'));
    expect(store.channels()[2]).toBe(255);
    expect(button('Independent 3, channel 3 (U1)').getAttribute('aria-pressed')).toBe('true');

    await press(button('Independent 3, channel 3 (U1)'));
    expect(store.channels()[2]).toBe(0);
  });

  it('the independents stand at the right edge: ten keys for channels and ten for memories', () => {
    // at the right of Main: the stack across the top, under it the master section over the
    // record keys, the general section, the sound section, and the independents last
    const side = page.querySelector('lm-main-view > .side');
    expect([...(side?.children ?? [])].map((part) => part.tagName.toLowerCase())).toEqual([
      'lm-stack-section',
      'div',
      'lm-general-section',
      'lm-sound-master',
      'lm-independents',
    ]);
    expect([...page.querySelectorAll('lm-main-view .first > *')].map((part) => part.tagName.toLowerCase())).toEqual([
      'lm-master-section',
      'lm-record-section',
    ]);

    const keys = [...page.querySelectorAll<HTMLButtonElement>('lm-independents button.number')];
    expect(keys.length).toBe(20);
    expect(keys.slice(0, 10).every((key) => key.className.includes('tone-channel'))).toBe(true);
    expect(keys.slice(10).every((key) => key.className.includes('tone-memory') || key.className.includes('tone-sequence'))).toBe(true);
    expect(keys[10].getAttribute('aria-label')).toBe('Independent 11, memory 1');
    expect(keys[12].className).toContain('tone-sequence'); // memory 3 holds a sequence
  });

  it('a memory key of the independents switches the fader of its memory between zero and full', async () => {
    await press(button('Independent 12, memory 2'));
    expect(store.memoryLevels()[1]).toBe(255);
    expect(button('Independent 12, memory 2').getAttribute('aria-pressed')).toBe('true');
    expect(button('Independent 12, memory 2').className).toContain('level-3');
    // the memory's look shows on the channel faders, as with its own fader
    expect(page.querySelector('lm-fader[aria-label="Channel 5 (U1)"]')?.getAttribute('aria-valuenow')).toBe('200');

    await press(button('Independent 12, memory 2'));
    expect(store.memoryLevels()[1]).toBe(0);
    expect(button('Independent 12, memory 2').className).toContain('level-2'); // stored, fader down
  });

  it('edit lets the memory of a memory key be changed', async () => {
    await press(button('Edit the channels, memories, effects and sounds of the independent keys'));
    const field = page.querySelector<HTMLInputElement>('input[aria-label="Memory of independent key 12"]');
    if (!field) throw new Error('no field for independent key 12');

    field.value = '129'; // there is no memory 129: the key keeps its memory
    field.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.memoryIndependents()[1]).toBe(2);
    expect(field.value).toBe('2');

    field.value = '77';
    field.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.memoryIndependents()[1]).toBe(77);
    expect(connection.calls).toContainEqual({ method: 'SetMemoryIndependent', args: [2, 77] });

    await press(button('Edit the channels, memories, effects and sounds of the independent keys'));
    await press(button('Independent 12, memory 77'));
    expect(store.memoryLevels()[76]).toBe(255);
  });

  it('edit lets the channel of an independent key be changed', async () => {
    await press(button('Edit the channels, memories, effects and sounds of the independent keys'));
    const field = page.querySelector<HTMLInputElement>(
      'input[aria-label="Channel of independent key 2"]',
    );
    if (!field) throw new Error('no field for independent key 2');

    field.value = '600'; // there is no channel 600: the key keeps its channel
    field.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.independents()[1]).toBe(2);
    expect(field.value).toBe('2');

    field.value = '40';
    field.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.independents()[1]).toBe(40);
    expect(connection.calls).toContainEqual({ method: 'SetIndependent', args: [2, 40] });

    await press(button('Edit the channels, memories, effects and sounds of the independent keys'));
    await press(button('Independent 2, channel 40 (U1)'));
    expect(store.channels()[39]).toBe(255);
  });

  it('a project of two universes has a U key for each above the channel faders, and numbers the channels in each', async () => {
    expect(page.querySelector('nav[aria-label="Universes"]')).toBeNull(); // one universe: no U keys

    connection.emit('DeskChanged', twoUniverses());
    await fixture.whenStable();
    const keys = [...page.querySelectorAll('lm-channel-section nav[aria-label="Universes"] button')];
    expect(keys.map((key) => key.textContent?.trim())).toEqual(['U1', 'U2']);
    expect(button('Universe 1').getAttribute('aria-pressed')).toBe('true');

    // U2: its channels, numbered 1 to 512 in it, and its pages
    await press(button('Universe 2'));
    expect(page.querySelector('lm-fader[aria-label="Channel 1 (U2)"]')).toBeTruthy();
    expect(page.querySelector('button[data-action="channel.513"]')?.textContent?.trim()).toBe('1');
    expect(button('Channels 1 to 64 (U2)').getAttribute('aria-pressed')).toBe('true');
    expect(page.querySelectorAll('nav[aria-label="Channel pages"] button').length).toBe(8);
    await press(button('Channels 65 to 128 (U2)'));
    expect(page.querySelector('lm-fader[aria-label="Channel 65 (U2)"]')).toBeTruthy();

    // a key of the independents takes a channel of U2 as "113 U2"
    await press(button('Edit the channels, memories, effects and sounds of the independent keys'));
    const field = page.querySelector<HTMLInputElement>('input[aria-label="Channel of independent key 2"]')!;
    expect(field.value).toBe('2 U1');
    field.value = '113 U2';
    field.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetIndependent')).toEqual([2, 625]);
    expect(field.value).toBe('113 U2');
    // a number alone is in the universe the key's channel is in: now U2
    field.value = '7';
    field.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetIndependent')).toEqual([2, 519]);
    expect(field.value).toBe('7 U2');
    // a number past 512 names no channel: the key keeps its own
    field.value = '625';
    field.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.calls.filter((call) => call.method === 'SetIndependent').length).toBe(2);
    expect(field.value).toBe('7 U2');
    await press(button('Edit the channels, memories, effects and sounds of the independent keys'));

    // back to one universe: the U keys go, and the faders show U1 again
    connection.emit('DeskChanged', connection.welcome.desk);
    await fixture.whenStable();
    expect(page.querySelector('nav[aria-label="Universes"]')).toBeNull();
    expect(page.querySelector('lm-fader[aria-label="Channel 449 (U1)"]')).toBeTruthy();
  });

  it('SOLO UP: a key held puts its fader to full and every other light out; let go, everything is back', async () => {
    // jsdom knows no PointerEvent; a MouseEvent of that name does the same here.
    const hold = (key: Element, down: boolean) =>
      key.dispatchEvent(new MouseEvent(down ? 'pointerdown' : 'pointerup', { bubbles: true, button: 0 }));
    store.setChannel(5, 40);
    store.setChannel(6, 100);

    await press(button('Solo up'));
    expect(button('Solo up').getAttribute('aria-pressed')).toBe('true');
    expect(button('Solo up').className).toContain('level-3');

    hold(channelKey(5)!, true);
    expect(store.channels()[4]).toBe(255);
    // The desk program lets only this one out; the stage plan shows it the same way.
    expect(connection.last('HoldSolo')).toEqual([true, 'channel', 5, true]);
    expect(store.output()[4]).toBe(255);
    expect(store.output()[5]).toBe(0); // channel 6 is dark, though its fader stays at 100
    expect(store.channels()[5]).toBe(100);
    hold(channelKey(5)!, false);
    channelKey(5)!.click(); // the click after a press does not switch the fader as well
    await fixture.whenStable();
    expect(store.channels()[4]).toBe(40); // back where it was
    expect(connection.last('HoldSolo')).toEqual([true, 'channel', 5, false]);
    expect(store.output()[5]).toBe(100);

    hold(memoryKey(2), true);
    expect(store.memoryLevels()[1]).toBe(255);
    hold(memoryKey(2), false);
    expect(store.memoryLevels()[1]).toBe(0);

    const independent = page.querySelector('[data-action="ind.channel.3"]')!;
    hold(independent, true);
    expect(store.channels()[2]).toBe(255);
    hold(independent, false);
    expect(store.channels()[2]).toBe(0);

    // Its shortcut puts it to full while the key of the keyboard is held.
    store.setChannel(7, 0);
    TestBed.inject(DeskSettings).setShortcuts({ ...TestBed.inject(DeskSettings).shortcuts(), 'channel.7': 'KeyJ' });
    document.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyJ', key: 'j', bubbles: true, cancelable: true }));
    expect(store.channels()[6]).toBe(255);
    document.dispatchEvent(new KeyboardEvent('keyup', { code: 'KeyJ', key: 'j', bubbles: true }));
    expect(store.channels()[6]).toBe(0);

    // A key held while SOLO UP goes dark comes back down; then the keys switch on and off again.
    hold(channelKey(5)!, true);
    await press(button('Solo up'));
    expect(store.channels()[4]).toBe(40);
    expect(button('Solo up').className).toContain('level-1');
    await press(channelKey(5));
    expect(store.channels()[4]).toBe(255);
  });

  it('SOLO DOWN: a key held takes only its own fader down; let go, it is back; the two solos exclude each other', async () => {
    const hold = (key: Element, down: boolean) =>
      key.dispatchEvent(new MouseEvent(down ? 'pointerdown' : 'pointerup', { bubbles: true, button: 0 }));
    store.setChannel(5, 40);
    store.setChannel(6, 100);
    store.setMemoryLevel(2, 255); // memory 2 holds channel 5 at 200

    await press(button('Solo up'));
    await press(button('Solo down'));
    expect(button('Solo down').getAttribute('aria-pressed')).toBe('true');
    expect(button('Solo up').getAttribute('aria-pressed')).toBe('false');

    // A channel: its fader to zero, and its light dark, also where a memory holds it; the rest stays.
    hold(channelKey(5)!, true);
    expect(store.channels()[4]).toBe(0);
    expect(connection.last('HoldSolo')).toEqual([false, 'channel', 5, true]);
    expect(store.output()[4]).toBe(0);
    expect(store.output()[5]).toBe(100);
    hold(channelKey(5)!, false);
    expect(store.channels()[4]).toBe(40);
    expect(store.output()[4]).toBe(200);

    // A memory: its fader to zero while held, and back.
    hold(memoryKey(2), true);
    expect(store.memoryLevels()[1]).toBe(0);
    hold(memoryKey(2), false);
    expect(store.memoryLevels()[1]).toBe(255);

    // What another page holds counts here too.
    connection.emit('SoloChanged', { up: [{ target: 'channel', number: 6 }], down: [] });
    expect(store.output()[5]).toBe(255);
    expect(store.output()[4]).toBe(0);
  });

  it('at the right of Main: STACK across the top, under it MASTER over RECORD, GENERAL (CLEAR, SOLO UP, SOLO DOWN, RATE), SOUND (MUTE), then IND; SOLO and PAUSE are white', async () => {
    const side = [...(page.querySelector('lm-main-view > .side')?.children ?? [])].map((child) => child.localName);
    expect(side).toEqual(['lm-stack-section', 'div', 'lm-general-section', 'lm-sound-master', 'lm-independents']);
    expect([...page.querySelectorAll('lm-main-view .first > *')].map((child) => child.localName)).toEqual([
      'lm-master-section',
      'lm-record-section',
    ]);
    // the stack's keys in its first column, the wheel over the other two (a subgrid of the side's columns)
    expect([...page.querySelectorAll('lm-stack-section > *')].map((child) => child.localName + '.' + child.className)).toEqual([
      'h2.',
      'div.keys',
      'div.wheel',
    ]);

    const keys = (section: string) => [...page.querySelectorAll(`${section} button`)];
    expect(keys('lm-master-section').map((key) => key.getAttribute('aria-label'))).toEqual(['Blackout']);
    expect(keys('lm-general-section').map((key) => key.getAttribute('aria-label') ?? key.textContent?.trim())).toEqual([
      'Clear',
      'Solo up',
      'Solo down',
      'Rate',
      'Tempo',
    ]);
    expect(keys('lm-general-section').map((key) => /tone-(\S+)/.exec(key.className)?.[1])).toEqual(['clear', 'solo', 'solo-down', 'rate', 'tempo']);
    // the tempo stands on top of its fader, and Tempo under it, at the bottom of the section
    const parts = [...page.querySelectorAll('lm-general-section > *')].map((part) =>
      part.classList.contains('readout') ? 'tempo shown' : part.classList.contains('tempo') ? 'Tempo' : part.localName,
    );
    expect(parts.slice(-3)).toEqual(['tempo shown', 'lm-fader', 'Tempo']);

    // White, as CLEAR is: SOLO, and PAUSE on Main and in the Stack view.
    expect(button('Pause').className).toContain('tone-pause');
    await show('/stack');
    expect(button('Pause').className).toContain('tone-pause');
  });

  it('RATE lit makes its fader a tempo for every sequence, in beats per minute, and TEMPO taps it; dark, each plays its own times', async () => {
    const fader = page.querySelector<HTMLElement>('lm-general-section lm-fader')!;
    const shown = () => textOf('lm-general-section .readout');
    const key = (name: string) => fader.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));
    const sent = async () => {
      await new Promise((resolve) => setTimeout(resolve, 40)); // the rate is sent a moment later, as fader moves are
      return connection.last('SetRate');
    };

    expect(button('Rate').getAttribute('aria-pressed')).toBe('false');
    expect(button('Rate').className).toContain('level-1');
    expect(shown()).toBe('120 BPM');
    expect(fader.getAttribute('aria-valuenow')).toBe('90'); // 120 is 90 beats above the bottom, 30
    expect(fader.getAttribute('aria-valuetext')).toBe('120 beats per minute');
    expect(fader.classList).toContain('off'); // dim while RATE is dark
    expect(button('Tempo').disabled).toBe(true); // Tempo only while RATE is lit

    await press(button('Rate'));
    expect(button('Rate').className).toContain('level-3');
    expect(fader.classList).not.toContain('off');
    expect(button('Tempo').disabled).toBe(false);

    // In whole beats per minute, from 30 at the bottom to 300 at the top.
    key('ArrowUp');
    await fixture.whenStable();
    expect(store.bpm()).toBe(121);
    expect(shown()).toBe('121 BPM');
    key('End');
    await fixture.whenStable();
    expect(store.bpm()).toBe(300);
    key('Home');
    await fixture.whenStable();
    expect(store.bpm()).toBe(30);
    expect(await sent()).toEqual([true, 30]);

    // A double click puts it back to 120.
    fader.dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
    await fixture.whenStable();
    expect(shown()).toBe('120 BPM');

    // Tempo, tapped in time: from the second tap on, the beat of the taps (0.4 s: 150 beats a minute).
    store.tapTempo(1000);
    expect(store.bpm()).toBe(120);
    store.tapTempo(1400);
    store.tapTempo(1800);
    await fixture.whenStable();
    expect(shown()).toBe('150 BPM');
    expect(await sent()).toEqual([true, 150]);
    store.tapTempo(9000); // after a pause it counts afresh: one tap says nothing yet
    expect(store.bpm()).toBe(150);
    store.tapTempo(9500);
    expect(store.bpm()).toBe(120);
    // the key on the page taps (and its shortcut does)
    const taps = vi.spyOn(store, 'tapTempo');
    await press(button('Tempo'));
    expect(taps).toHaveBeenCalledTimes(1);
    taps.mockRestore();

    // Dark again: the sequences play their own times, Tempo is dark; the fader can be set for the next time.
    await press(button('Rate'));
    expect(button('Tempo').disabled).toBe(true);
    store.tapTempo(20000);
    store.tapTempo(20250);
    expect(store.bpm()).not.toBe(240);
    key('ArrowUp');
    await fixture.whenStable();
    expect(await sent()).toEqual([false, store.bpm()]);
    expect(page.querySelector('lm-general-section .readout')?.classList).not.toContain('up');

    // What another page does reaches this one, as a fader does.
    connection.emit('RateChanged', true, 90);
    await fixture.whenStable();
    expect(button('Rate').getAttribute('aria-pressed')).toBe('true');
    expect(shown()).toBe('90 BPM');
    expect(fader.getAttribute('aria-valuenow')).toBe('60');
  });

  it('a window closes with its ✕, and with a press outside it, but not with a drag that ends outside', async () => {
    const dialog = () => page.querySelector<HTMLDialogElement>('lm-project-dialog dialog')!;
    const at = (x: number) => ({ bubbles: true, clientX: x, clientY: x });

    await press(button('Load project'));
    expect(dialog().open).toBe(true);
    await press(buttonIn('lm-project-dialog', 'Close the window'));
    expect(dialog().open).toBe(false);

    // a press on the backdrop: the dialog itself is its target, outside the window's box
    await press(button('Load project'));
    dialog().dispatchEvent(new MouseEvent('pointerdown', at(900)));
    dialog().dispatchEvent(new MouseEvent('click', at(900)));
    await fixture.whenStable();
    expect(dialog().open).toBe(false);

    // a drag that began inside the window and ends outside it
    await press(button('Load project'));
    dialog().querySelector('h2')!.dispatchEvent(new MouseEvent('pointerdown', at(0)));
    dialog().dispatchEvent(new MouseEvent('click', at(900)));
    await fixture.whenStable();
    expect(dialog().open).toBe(true);
    await press(buttonIn('lm-project-dialog', 'Close the window'));
  });

  it('blackout is switched on and off', async () => {
    await press(button('Blackout'));
    expect(connection.calls).toContainEqual({ method: 'SetBlackout', args: [true] });
    expect(button('Blackout').getAttribute('aria-pressed')).toBe('true');

    await press(button('Blackout'));
    expect(connection.calls).toContainEqual({ method: 'SetBlackout', args: [false] });
  });

  it('a note typed only into the second line stays there and leaves the first line empty', async () => {
    const first = page.querySelector<HTMLInputElement>('input[aria-label="Note for Channel 7 (U1)"]');
    const second = page.querySelector<HTMLInputElement>(
      'input[aria-label="Note for Channel 7 (U1), second line"]',
    );
    if (!first || !second) throw new Error('no note fields for channel 7');

    second.value = ' Dimmer ';
    second.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(store.channelNotes()[6]).toBe(NOTE_LINE_SEPARATOR + 'Dimmer');
    expect(connection.calls).toContainEqual({
      method: 'SetChannelNote',
      args: [7, NOTE_LINE_SEPARATOR + 'Dimmer'],
    });
    expect(first.value).toBe('');
    expect(second.value).toBe('Dimmer');

    // the first line can be filled in afterwards without touching the second
    first.value = 'Front';
    first.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.channelNotes()[6]).toBe('Front' + NOTE_LINE_SEPARATOR + 'Dimmer');
    expect(second.value).toBe('Dimmer');
  });

  it('a memory fader moves the faders of the channels the memory holds', async () => {
    const channel5 = page.querySelector<HTMLElement>('lm-fader[aria-label="Channel 5 (U1)"]');
    const memory2 = page.querySelector<HTMLElement>('lm-fader[aria-label="Memory 2"]');
    if (!channel5 || !memory2) throw new Error('faders missing');
    expect(channel5.getAttribute('aria-valuenow')).toBe('0');

    memory2.dispatchEvent(new KeyboardEvent('keydown', { key: 'End' }));
    await fixture.whenStable();
    expect(channel5.getAttribute('aria-valuenow')).toBe('200'); // what the memory holds
    expect(store.channels()[4]).toBe(0); // the channel's own level is untouched
    expect(page.querySelector('lm-fader[aria-label="Channel 6 (U1)"]')?.getAttribute('aria-valuenow')).toBe('0');
    // channel 70 is on another page: its page key is half lit now
    expect(button('Channels 65 to 128 (U1)').className).toContain('level-2');

    store.setMemoryLevel(2, 128); // half way: 200 x 128 / 255
    await fixture.whenStable();
    expect(channel5.getAttribute('aria-valuenow')).toBe('100');

    // the owner's own fader wins as soon as it is higher
    store.setChannel(5, 150);
    await fixture.whenStable();
    expect(channel5.getAttribute('aria-valuenow')).toBe('150');

    store.setMemoryLevel(2, 0);
    store.setChannel(5, 0);
    await fixture.whenStable();
    expect(channel5.getAttribute('aria-valuenow')).toBe('0');
  });

  it('a fader held up by a memory still takes the level its owner asks for', async () => {
    const channel5 = page.querySelector<HTMLElement>('lm-fader[aria-label="Channel 5 (U1)"]');
    if (!channel5) throw new Error('no fader for channel 5');
    store.setChannel(5, 40);
    store.setMemoryLevel(2, 255);
    await fixture.whenStable();
    expect(channel5.getAttribute('aria-valuenow')).toBe('200');

    // one step down goes from the owner's 40, not from the 200 the cap is held at
    channel5.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    await fixture.whenStable();
    expect(store.channels()[4]).toBe(39);
    expect(channel5.getAttribute('aria-valuenow')).toBe('200');

    channel5.dispatchEvent(new KeyboardEvent('keydown', { key: 'End' }));
    await fixture.whenStable();
    expect(store.channels()[4]).toBe(255);
    expect(channel5.getAttribute('aria-valuenow')).toBe('255');
  });

  it('blackout and the master fader stand in the master section of the main view, clear in the general section', async () => {
    const inMaster = (selector: string) => page.querySelector(`lm-main-view lm-master-section ${selector}`);
    expect(inMaster('lm-fader[aria-label="Master"]')).toBeTruthy();
    expect(inMaster('button[aria-label="Blackout"]')).toBeTruthy();
    expect(button('Clear').closest('lm-general-section')).toBeTruthy();
    expect(page.querySelector('lm-top-bar lm-fader')).toBeNull();

    // from the top: Blackout, the level, the fader (the level above its fader, as on every strip)
    const order = [...(page.querySelector('lm-master-section')?.children ?? [])].map(
      (part) => part.getAttribute('aria-label') ?? part.className.split(' ')[0] ?? part.tagName,
    );
    expect(order.indexOf('readout')).toBeGreaterThan(order.indexOf('Blackout'));
    expect(order.indexOf('readout')).toBeLessThan(order.indexOf('Master'));
    expect(page.querySelector('lm-master-section .readout')?.textContent?.trim()).toBe('255');

    // the memories view is for looking only: it has no master section
    await show('/memories');
    expect(page.querySelector('lm-master-section')).toBeNull();
    expect(page.querySelector('lm-fader[aria-label="Master"]')).toBeNull();
  });

  it('the top bar links to the views and marks the one that is shown', async () => {
    const links = [...page.querySelectorAll<HTMLAnchorElement>('nav[aria-label="Views"] a')];
    expect(links.map((link) => link.textContent?.trim())).toEqual([
      'Main',
      'Memories',
      'Sequences',
      'Stack',
      'Stage',
      'Fixtures',
      'Effects',
      'Sounds',
      'MIDI',
      'Settings',
    ]); // Notes only when Settings shows it: it is left out at first
    expect(links[0].getAttribute('aria-current')).toBe('page');
    expect(links[1].getAttribute('aria-current')).toBeNull();
    expect(page.querySelector('lm-main-view')).toBeTruthy();

    await show('/memories');
    expect(links[0].getAttribute('aria-current')).toBeNull();
    expect(links[1].getAttribute('aria-current')).toBe('page');
    expect(page.querySelector('lm-main-view')).toBeNull();
    expect(page.querySelector('lm-memories-view')).toBeTruthy();
  });
});
