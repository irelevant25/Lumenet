import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';
import { DeskConnection } from './desk/desk-connection';
import { DeskStore } from './desk/desk-store';
import {
  CHANNEL_COUNT,
  LanAccess,
  MEMORY_COUNT,
  Playback,
  Sequence,
  StackPlayback,
  StackState,
  TimedStep,
  Welcome,
} from './desk/desk-types';
import { IDLE } from './desk/function-mode';
import { NOTE_LINE_SEPARATOR } from './desk/notes';

/** Stands in for the connection to the backend: answers "Hello" and remembers what the page sent. */
class FakeConnection {
  readonly online = signal(false);
  readonly calls: { method: string; args: unknown[] }[] = [];

  private readonly handlers = new Map<string, (...args: never[]) => void>();
  private readonly connected: (() => void)[] = [];

  readonly welcome: Welcome = {
    version: '0.1.0',
    desk: {
      channels: new Array<number>(CHANNEL_COUNT).fill(0),
      memoryLevels: new Array<number>(MEMORY_COUNT).fill(0),
      memoryStored: new Array<boolean>(MEMORY_COUNT).fill(false),
      channelNotes: new Array<string>(CHANNEL_COUNT).fill(''),
      memoryNotes: new Array<string>(MEMORY_COUNT).fill(''),
      independents: Array.from({ length: 10 }, (_, index) => index + 1),
      memoryIndependents: Array.from({ length: 10 }, (_, index) => index + 1),
      master: 255,
      blackout: false,
    },
    box: { connected: false, host: '192.168.7.1', firmware: null },
    project: { name: 'Untitled', path: null, unsaved: false },
  };

  /** What the stored memories hold, as the backend answers "GetMemories". */
  readonly looks: Record<number, number[]> = {};
  /** The sequences, the stack and where it stands, as the backend answers the three questions about them. */
  readonly sequences: Record<number, Sequence> = {};
  stack: StackState = { steps: [] };
  playback: Playback = {
    levels: [],
    sequenceSteps: {},
    stack: { currentId: null, nextId: null, state: 'idle', paused: false, remaining: 0, live: false },
  };

  /** LAN as the backend answers "GetLanAccess": this page runs on the PC, and LAN is off. */
  lan: LanAccess = { local: true, on: false, addresses: ['192.168.1.20:5080'], password: null };

  start(): void {
    this.online.set(true);
    for (const callback of this.connected) callback();
  }

  whenConnected(callback: () => void): void {
    this.connected.push(callback);
  }

  on(event: string, handler: (...args: never[]) => void): void {
    this.handlers.set(event, handler);
  }

  async invoke(method: string, ...args: unknown[]): Promise<unknown> {
    this.calls.push({ method, args });
    if (method === 'Hello') return this.welcome;
    if (method === 'GetMemories') return this.looks;
    if (method === 'GetSequences') return this.sequences;
    if (method === 'GetStack') return this.stack;
    if (method === 'GetPlayback') return this.playback;
    if (method === 'GetLanAccess') return this.lan;
    if (method === 'NewProject') return { name: 'Untitled', path: null, unsaved: false };
    return undefined;
  }

  send(method: string, ...args: unknown[]): void {
    this.calls.push({ method, args });
  }

  /** A message from the backend, as if it had just arrived. */
  emit(event: string, ...args: unknown[]): void {
    (this.handlers.get(event) as ((...args: unknown[]) => void) | undefined)?.(...args);
  }

  /** The names of the calls made so far, without the questions the page asks by itself. */
  methods(): string[] {
    return this.calls.map((call) => call.method).filter((method) => !QUESTIONS.includes(method));
  }

  /** The arguments of the last call of a method. */
  last(method: string): unknown[] | undefined {
    return [...this.calls].reverse().find((call) => call.method === method)?.args;
  }
}

/** What the page asks the backend by itself, after connecting and after every "DeskChanged". */
const QUESTIONS = ['Hello', 'GetMemories', 'GetSequences', 'GetStack', 'GetPlayback', 'GetLanAccess'];

/** A step on a channel, with all three times at one second unless said otherwise. */
function step(number: number, value: number, more: Partial<TimedStep> = {}): TimedStep {
  return { target: 'channel', number, value, up: 1, wait: 1, down: 1, ...more };
}

/** Where the stack of the tests stands while it is not live, and once it is: on its first step, waiting for Play. */
const NOT_LIVE: StackPlayback = { currentId: null, nextId: 11, state: 'idle', paused: false, remaining: 0, live: false };
const LIVE: StackPlayback = { currentId: 11, nextId: 12, state: 'holding', paused: false, remaining: 0, live: true };

describe('the desk page', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;

  beforeEach(async () => {
    connection = new FakeConnection();
    connection.welcome.desk.memoryStored[1] = true; // memory 2 holds a look: channel 5 at 200, channel 70 full
    const look = new Array<number>(CHANNEL_COUNT).fill(0);
    look[4] = 200;
    look[69] = 255;
    connection.looks[2] = look;

    // memory 3 holds a sequence of two steps, played once
    connection.welcome.desk.memoryStored[2] = true;
    connection.welcome.desk.memorySequence = new Array<boolean>(MEMORY_COUNT).fill(false);
    connection.welcome.desk.memorySequence[2] = true;
    connection.sequences[3] = { loop: false, steps: [step(5, 200), step(6, 100, { wait: 0 })] };

    // two faders carry a note: channel 6 (two lines) and memory 2
    connection.welcome.desk.channelNotes[5] = 'Front' + NOTE_LINE_SEPARATOR + 'left';
    connection.welcome.desk.memoryNotes[1] = 'Wash';

    // the stack has two steps, and is not live: no step is on
    connection.stack = {
      steps: [
        { id: 11, cue: '1.0', ...step(7, 255, { wait: 0 }) },
        { id: 12, cue: '2.0', ...step(2, 128, { target: 'memory', wait: 3 }) },
      ],
    };
    connection.playback.stack = { ...NOT_LIVE };

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes), { provide: DeskConnection, useValue: connection }],
    }).compileComponents();

    fixture = TestBed.createComponent(App);
    page = fixture.nativeElement as HTMLElement;
    store = TestBed.inject(DeskStore);
    await show('/');
  });

  /** Opens a view, as a click on its link in the top bar does. */
  async function show(path: string): Promise<void> {
    await TestBed.inject(Router).navigateByUrl(path);
    await fixture.whenStable();
  }

  function button(label: string): HTMLButtonElement {
    // A key is found by its full name for screen readers, or else by what is printed on it.
    const found = [...page.querySelectorAll('button')].find(
      (candidate) =>
        (candidate.getAttribute('aria-label') ?? candidate.textContent?.trim()) === label,
    );
    if (!found) throw new Error(`no button "${label}"`);
    return found;
  }

  // A key's name begins with "Memory 5 key"; while a sequence is recorded the steps it is in follow.
  function memoryKey(memory: number): HTMLButtonElement {
    const found = page.querySelector<HTMLButtonElement>(`button[aria-label^="Memory ${memory} key"]`);
    if (!found) throw new Error(`no key for memory ${memory}`);
    return found;
  }

  /** The key under a channel fader. It is a button only while a sequence is recorded. */
  function channelKey(channel: number): HTMLButtonElement | null {
    return page.querySelector<HTMLButtonElement>(`button[aria-label^="Channel ${channel} key"]`);
  }

  async function press(element: HTMLElement | null): Promise<void> {
    if (!element) throw new Error('nothing to press');
    element.click();
    await fixture.whenStable();
  }

  /** The desk reports where the stack stands, as it does after every key of the stack. */
  async function stackStands(stack: StackPlayback): Promise<void> {
    connection.emit('PlaybackChanged', { ...connection.playback, stack });
    await fixture.whenStable();
  }

  it('shows the name, 64 channel faders, 32 memory faders and the page buttons', () => {
    expect(page.querySelector('h1')?.textContent).toContain('Lumenet');
    expect(page.querySelectorAll('lm-channel-section lm-fader-strip').length).toBe(64);
    expect(page.querySelectorAll('lm-memory-section lm-fader-strip').length).toBe(32);
    expect(page.querySelectorAll('nav[aria-label="Channel pages"] button').length).toBe(8);
    expect(page.querySelectorAll('nav[aria-label="Memory pages"] button').length).toBe(4);
    expect(button('Channels 1 to 64')).toBeTruthy();
    expect(button('Memories 97 to 128')).toBeTruthy();
  });

  it('moving a fader changes the level at once and tells the backend', async () => {
    const fader = page.querySelector<HTMLElement>('lm-fader[aria-label="Channel 3"]');
    fader?.dispatchEvent(new KeyboardEvent('keydown', { key: 'End' }));
    await fixture.whenStable();

    expect(store.channels()[2]).toBe(255);
    expect(fader?.getAttribute('aria-valuenow')).toBe('255');

    await new Promise((resolve) => setTimeout(resolve, 40)); // fader moves are sent a moment later
    expect(connection.calls).toContainEqual({ method: 'SetChannel', args: [3, 255] });
  });

  it('quick steps on a fader all count, also before the page has drawn the first one', async () => {
    const fader = page.querySelector<HTMLElement>('lm-fader[aria-label="Channel 4"]');
    for (let step = 0; step < 3; step++) {
      fader?.dispatchEvent(new KeyboardEvent('keydown', { key: 'PageUp' })); // 16 each, no waiting between
    }
    fader?.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    await fixture.whenStable();

    expect(store.channels()[3]).toBe(47);
    expect(fader?.getAttribute('aria-valuenow')).toBe('47');
  });

  it('switching the channel page shows the next 64 channels', async () => {
    await press(button('Channels 65 to 128'));
    expect(page.querySelector('lm-fader[aria-label="Channel 65"]')).toBeTruthy();
    expect(page.querySelector('lm-fader[aria-label="Channel 1"]')).toBeNull();
  });

  it('save: a press on a memory key stores the channel faders there', async () => {
    await press(memoryKey(5));
    expect(connection.methods()).toEqual([]); // outside a function the keys do nothing

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
    expect(channelKey(7)).toBeNull(); // a channel key is a lamp, not a key, until a sequence is recorded

    await press(button('Record sequence'));
    await press(memoryKey(2)); // holds a look: no place for a sequence
    expect(store.mode()).toEqual({ kind: 'record-pick' });
    expect(store.notice()).toContain('Memory 2 holds a look');
    expect(channelKey(7)).toBeNull();

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
    expect(channelKey(7)?.getAttribute('aria-label')).toBe('Channel 7 key, in steps 1, 3');
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
    expect(channelKey(7)).toBeNull();
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
    const field = page.querySelector<HTMLInputElement>('input[aria-label="Note for Channel 5"]');
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
      'input[aria-label="Note for Channel 5, second line"]',
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
    store.stripsPerRow.set(16);
    await fixture.whenStable();
    expect(page.querySelectorAll('lm-channel-section lm-fader-strip').length).toBe(32);
    expect(page.querySelectorAll('lm-memory-section lm-fader-strip').length).toBe(16);
    expect(page.querySelectorAll('nav[aria-label="Channel pages"] button').length).toBe(16);
    expect(page.querySelectorAll('nav[aria-label="Memory pages"] button').length).toBe(8);

    await press(button('Channels 33 to 64'));
    expect(page.querySelector('lm-fader[aria-label="Channel 33"]')).toBeTruthy();
    expect(page.querySelector('lm-fader[aria-label="Channel 1"]')).toBeNull();

    // wide again: the page that holds channel 33 is the one from 1 to 64
    store.stripsPerRow.set(32);
    await fixture.whenStable();
    expect(page.querySelector('lm-fader[aria-label="Channel 1"]')).toBeTruthy();
    expect(page.querySelector('lm-fader[aria-label="Channel 64"]')).toBeTruthy();
  });

  it('an independent key switches its channel between zero and full', async () => {
    await press(button('Independent 3, channel 3'));
    expect(store.channels()[2]).toBe(255);
    expect(button('Independent 3, channel 3').getAttribute('aria-pressed')).toBe('true');

    await press(button('Independent 3, channel 3'));
    expect(store.channels()[2]).toBe(0);
  });

  it('the independents stand at the right edge: ten keys for channels and ten for memories', () => {
    const main = page.querySelector('lm-main-view');
    expect(main?.lastElementChild?.tagName.toLowerCase()).toBe('lm-independents');
    // the record keys stand under the master section
    const side = [...(page.querySelector('lm-main-view .side')?.children ?? [])].map((part) => part.tagName.toLowerCase());
    expect(side).toEqual(['lm-master-section', 'lm-record-section']);

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
    expect(page.querySelector('lm-fader[aria-label="Channel 5"]')?.getAttribute('aria-valuenow')).toBe('200');

    await press(button('Independent 12, memory 2'));
    expect(store.memoryLevels()[1]).toBe(0);
    expect(button('Independent 12, memory 2').className).toContain('level-2'); // stored, fader down
  });

  it('edit lets the memory of a memory key be changed', async () => {
    await press(button('Edit the channels and memories of the independent keys'));
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

    await press(button('Edit the channels and memories of the independent keys'));
    await press(button('Independent 12, memory 77'));
    expect(store.memoryLevels()[76]).toBe(255);
  });

  it('the memory keys of the memories view and the sequences view carry the notes of their memories', async () => {
    connection.emit('MemoryNoteChanged', 4, 'Chase' + NOTE_LINE_SEPARATOR + 'blue');
    await fixture.whenStable();

    for (const view of ['/memories', '/sequences']) {
      await show(view);
      const lines = (memory: number) =>
        [...(page.querySelectorAll('lm-note-lines')[memory - 1]?.querySelectorAll('.line') ?? [])].map((line) =>
          line.textContent?.trim(),
        );
      expect(page.querySelectorAll('.keys lm-note-lines').length).toBe(32);
      expect(lines(2)).toEqual(['Wash', '']);
      expect(lines(4)).toEqual(['Chase', 'blue']);
      expect(lines(1)).toEqual(['', '']);
      expect(page.querySelectorAll('lm-note-lines')[3].getAttribute('title')).toBe('Chase blue');
      expect(page.querySelectorAll('.keys input').length).toBe(0); // to read only: notes are written in the main view
    }
  });

  it('edit lets the channel of an independent key be changed', async () => {
    await press(button('Edit the channels and memories of the independent keys'));
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

    await press(button('Edit the channels and memories of the independent keys'));
    await press(button('Independent 2, channel 40'));
    expect(store.channels()[39]).toBe(255);
  });

  it('a new project is asked for from the backend and unfinished functions are dropped', async () => {
    await press(button('Copy memory'));
    await store.newProject();
    await fixture.whenStable();
    expect(connection.methods()).toContain('NewProject');
    expect(button('Copy memory').getAttribute('aria-pressed')).toBe('false');
    expect(button('New project')).toBeTruthy();
  });

  it('the LAN key stands at the top left, and New beside Save and Load at the right', () => {
    const labels = (selector: string) =>
      [...page.querySelectorAll(`lm-top-bar ${selector} button`)].map((key) => key.getAttribute('aria-label'));
    expect(labels('.left')).toEqual(['LAN']);
    expect(labels('.project')).toEqual(['New project', 'Save project', 'Load project']);
  });

  it('LAN lit shows the address and the password under the box light; off shows neither', async () => {
    const line = () => page.querySelector('lm-top-bar .lan');
    expect(button('LAN').getAttribute('aria-pressed')).toBe('false');
    expect(button('LAN').className).toContain('level-1');
    expect(line()).toBeNull();

    await press(button('LAN'));
    expect(connection.last('SetLanAccess')).toEqual([true]);
    expect(button('LAN').className).toContain('level-3'); // lit at once

    // the desk answers with the password
    connection.emit('LanAccessChanged', { ...connection.lan, on: true, password: 'K7M2Q' });
    await fixture.whenStable();
    expect(line()?.textContent?.replace(/\s+/g, ' ').trim()).toBe('LAN 192.168.1.20:5080 Password K7M2Q');
    expect(line()?.getAttribute('title')).toContain('open http://192.168.1.20:5080');

    await press(button('LAN'));
    expect(connection.last('SetLanAccess')).toEqual([false]);
    expect(line()).toBeNull(); // the password goes at once
    expect(button('LAN').getAttribute('aria-pressed')).toBe('false');
  });

  it('a page on a phone has no LAN key and shows no password', async () => {
    connection.emit('LanAccessChanged', { local: false, on: true, addresses: [], password: null });
    await fixture.whenStable();

    expect(page.querySelector('button[aria-label="LAN"]')).toBeNull();
    expect(page.querySelector('lm-top-bar .lan')).toBeNull();
  });

  it('blackout is switched on and off', async () => {
    await press(button('Blackout'));
    expect(connection.calls).toContainEqual({ method: 'SetBlackout', args: [true] });
    expect(button('Blackout').getAttribute('aria-pressed')).toBe('true');

    await press(button('Blackout'));
    expect(connection.calls).toContainEqual({ method: 'SetBlackout', args: [false] });
  });

  it('a note typed only into the second line stays there and leaves the first line empty', async () => {
    const first = page.querySelector<HTMLInputElement>('input[aria-label="Note for Channel 7"]');
    const second = page.querySelector<HTMLInputElement>(
      'input[aria-label="Note for Channel 7, second line"]',
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
    const channel5 = page.querySelector<HTMLElement>('lm-fader[aria-label="Channel 5"]');
    const memory2 = page.querySelector<HTMLElement>('lm-fader[aria-label="Memory 2"]');
    if (!channel5 || !memory2) throw new Error('faders missing');
    expect(channel5.getAttribute('aria-valuenow')).toBe('0');

    memory2.dispatchEvent(new KeyboardEvent('keydown', { key: 'End' }));
    await fixture.whenStable();
    expect(channel5.getAttribute('aria-valuenow')).toBe('200'); // what the memory holds
    expect(store.channels()[4]).toBe(0); // the channel's own level is untouched
    expect(page.querySelector('lm-fader[aria-label="Channel 6"]')?.getAttribute('aria-valuenow')).toBe('0');
    // channel 70 is on another page: its page key is half lit now
    expect(button('Channels 65 to 128').className).toContain('level-2');

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
    const channel5 = page.querySelector<HTMLElement>('lm-fader[aria-label="Channel 5"]');
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

  it('clear, blackout and the master fader stand in the master section of the main view', async () => {
    const inMaster = (selector: string) => page.querySelector(`lm-main-view lm-master-section ${selector}`);
    expect(inMaster('lm-fader[aria-label="Master"]')).toBeTruthy();
    expect(inMaster('button[aria-label="Blackout"]')).toBeTruthy();
    expect(button('Clear').closest('lm-master-section')).toBeTruthy();
    expect(page.querySelector('lm-top-bar lm-fader')).toBeNull();

    // from the top: Clear, Blackout, the level, the fader (the level above its fader, as on every strip)
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

  it('the top bar links to the two views and marks the one that is shown', async () => {
    const links = [...page.querySelectorAll<HTMLAnchorElement>('nav[aria-label="Views"] a')];
    expect(links.map((link) => link.textContent?.trim())).toEqual([
      'Main',
      'Memories',
      'Sequences',
      'Stack',
    ]);
    expect(links[0].getAttribute('aria-current')).toBe('page');
    expect(links[1].getAttribute('aria-current')).toBeNull();
    expect(page.querySelector('lm-main-view')).toBeTruthy();

    await show('/memories');
    expect(links[0].getAttribute('aria-current')).toBeNull();
    expect(links[1].getAttribute('aria-current')).toBe('page');
    expect(page.querySelector('lm-main-view')).toBeNull();
    expect(page.querySelector('lm-memories-view')).toBeTruthy();
  });

  it('the memories view shows what a memory holds and changes nothing', async () => {
    await show('/memories');
    const cells = () => [...page.querySelectorAll('lm-memories-view table.matrix td')];
    const levelOf = (channel: number) => cells()[channel - 1].textContent?.trim();
    expect(cells().length).toBe(512);
    expect(page.querySelectorAll('lm-memories-view table.matrix tr').length).toBe(16); // 16 rows of 32
    expect(cells()[69].getAttribute('data-channel')).toBe('70');
    // a cell is one element: the table is light enough for the browser's developer tools
    expect(page.querySelectorAll('lm-memories-view table.matrix td *').length).toBe(0);
    expect(page.querySelectorAll('lm-memories-view .keys button').length).toBe(32);
    expect(page.querySelectorAll('nav[aria-label="Memory pages"] button').length).toBe(4);

    // memory 1 is looked at first, and it is empty
    expect(page.querySelector('.summary')?.textContent).toContain('Memory 1 is empty');
    expect(levelOf(5)).toBe('0');

    await press(button('Look at memory 2'));
    expect(button('Look at memory 2').getAttribute('aria-pressed')).toBe('true');
    expect(page.querySelector('.summary')?.textContent).toContain('Memory 2 (Wash): 2 of 512 channels are above zero');
    expect(levelOf(5)).toBe('200');
    expect(levelOf(70)).toBe('255');
    expect(levelOf(6)).toBe('0');
    expect(cells()[4].getAttribute('aria-label')).toBe('Channel 5: 200');

    expect(connection.methods()).toEqual([]); // looking sends nothing to the desk
    expect(store.channels()[4]).toBe(0);
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
    const channel5 = page.querySelector<HTMLElement>('lm-fader[aria-label="Channel 5"]');
    const levels = new Array<number>(CHANNEL_COUNT).fill(0);
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
    expect(page.querySelector('.summary')?.textContent).toContain('a sequence of 2 steps, played once');
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
      false,
      [step(5, 200), step(6, 150, { wait: 0 })],
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
    expect(connection.last('SetSequence')?.[1]).toBe(true);

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
    expect(connection.last('SetSequence')).toEqual([9, true, [step(1, 255)]]);
    expect(store.memorySequence()[8]).toBe(true);
  });

  it('the number of a step is chosen from a list that shows the notes under the faders', async () => {
    await show('/sequences');
    await press(button('Work on memory 3'));
    const rows = () => [...page.querySelectorAll('lm-sequences-view table.steps tbody tr')];
    const numberOf = (row: number) =>
      rows()[row].querySelector<HTMLSelectElement>(`select[aria-label="Step ${row + 1}, number of the channel"]`);
    const second = numberOf(1);
    if (!second) throw new Error('no number field in step 2');

    // the note of the step's channel stands beside its number: step 2 is on channel 6
    const headers = [...page.querySelectorAll('lm-sequences-view table.steps thead th')];
    expect(headers.map((header) => header.textContent?.trim()).slice(0, 5)).toEqual([
      'Step',
      'Channel or memory',
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
    expect(second.options[4].text).toBe('5'); // a channel without a note: its number alone
    expect(second.options[5].text).toBe('6: Front left'); // the note's two lines, one space between

    second.value = '70';
    second.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetSequence')).toEqual([3, false, [step(5, 200), step(70, 100, { wait: 0 })]]);

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
    const kind = rows()[0].querySelector<HTMLSelectElement>('select[aria-label="Step 1, channel or memory"]');
    if (!kind) throw new Error('no kind field in step 1');
    kind.value = 'memory';
    kind.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.sequences()[3].steps[0]).toEqual(step(2, 200, { target: 'memory' }));
    const memories = rows()[0].querySelector<HTMLSelectElement>('select[aria-label="Step 1, number of the memory"]');
    memories?.dispatchEvent(new Event('focus'));
    expect([...(memories?.options ?? [])].map((option) => option.value)).toEqual(['2']);
    expect(memories?.options[0].text.trim()).toBe('2: Wash');
    expect(rows()[0].querySelector('td.note')?.textContent?.trim()).toBe('Wash');
  });

  it('the stack view shows the steps with the current and the next one, and goes through them', async () => {
    await show('/stack');
    await stackStands(LIVE);
    const rows = () => [...page.querySelectorAll('lm-stack-view table.steps tbody tr')];
    expect(rows().length).toBe(2);
    expect(rows().map((row) => row.classList.contains('current'))).toEqual([true, false]);
    expect(rows().map((row) => row.classList.contains('next'))).toEqual([false, true]);
    expect(page.querySelector('.card.current .what')?.textContent).toContain('1.0');
    expect(page.querySelector('.card.current .what')?.textContent).toContain('Channel 7 at 255');
    expect(page.querySelector('.card.current .how')?.textContent).toContain('waits for Play');
    expect(page.querySelector('.card.next .what')?.textContent).toContain('Memory 2 (Wash) at 128');
    expect(rows()[1].querySelector('td.note')?.textContent?.trim()).toBe('Wash'); // the note under that fader

    await press(button('Play'));
    await press(button('Pause'));
    await press(button('Next step'));
    await press(button('Previous step'));
    expect(connection.methods()).toEqual(['StackPlay', 'StackPause', 'StackNext', 'StackPrevious']);

    // the desk reports a fade on the last step, and then a pause; after the last step comes the first
    await stackStands({ ...LIVE, currentId: 12, nextId: 11, state: 'fading', remaining: 0.6 });
    expect(page.querySelector('.card.current .how')?.textContent).toContain('0.6 s left');
    expect(button('Play').className).toContain('level-3');
    expect(page.querySelector('.card.next .what')?.textContent).toContain('1.0');
    expect(rows().map((row) => row.classList.contains('next'))).toEqual([true, false]);

    await stackStands({ ...LIVE, currentId: 12, nextId: 11, state: 'fading', paused: true, remaining: 0.6 });
    expect(button('Pause').getAttribute('aria-pressed')).toBe('true');
    expect(page.querySelector('.card.current .how')?.textContent).toContain('Paused');
  });

  it('the Stack key takes the stack live, and a live stack locks everything that is stored', async () => {
    await show('/stack');
    const liveTag = () => page.querySelector('nav[aria-label="Views"] .live');

    // not live: the stack does not run, and its steps can be changed
    expect(button('Stack live').getAttribute('aria-pressed')).toBe('false');
    expect(button('Stack live').className).toContain('level-1');
    for (const key of ['Play', 'Pause', 'Next step', 'Previous step']) {
      expect(button(key).disabled).toBe(true);
    }
    expect(button('Add a step').disabled).toBe(false);
    expect(liveTag()).toBeNull();
    expect(page.querySelector('.card.next .what')?.textContent).toContain('1.0'); // where live starts

    store.setChannel(4, 90); // a fader move that is still waiting to be sent
    await press(button('Stack live'));
    expect(connection.methods()).toEqual(['SetStackLive']); // the waiting move is dropped: every fader goes down
    expect(connection.last('SetStackLive')).toEqual([true]);

    await stackStands(LIVE);
    expect(button('Stack live').getAttribute('aria-pressed')).toBe('true');
    expect(button('Stack live').className).toContain('level-3');
    expect(liveTag()?.textContent?.trim()).toBe('Live');
    expect(page.querySelector('.summary')?.textContent).toContain('It is live');
    expect(button('Play').disabled).toBe(false);
    expect(button('Add a step').disabled).toBe(true);
    const fields = (view: string) => [
      ...page.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLButtonElement>(
        `${view} table.steps input, ${view} table.steps select, ${view} table.steps button`,
      ),
    ];
    // per row: the number of the step, the two lists, four typed fields and three keys
    expect(fields('lm-stack-view').length).toBe(2 * 10);
    expect(fields('lm-stack-view').every((field) => field.disabled)).toBe(true);
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
      'Edit the channels and memories of the independent keys',
    ]) {
      expect(button(key).disabled).toBe(true);
    }
    expect(page.querySelector<HTMLInputElement>('input[aria-label="Note for Channel 5"]')?.readOnly).toBe(true);
    expect(page.querySelector<HTMLInputElement>('input[aria-label="Note for Memory 2"]')?.readOnly).toBe(true);
    store.pressFunction('save'); // a press that gets through anyway starts nothing
    expect(store.mode()).toEqual(IDLE);
    // what a show needs is not locked
    for (const key of ['Save project', 'Clear', 'Blackout', 'Independent 3, channel 3']) {
      expect(button(key).disabled).toBe(false);
    }

    // the sequences view only shows
    await show('/sequences');
    await press(button('Work on memory 3'));
    expect(page.querySelector('.summary')?.textContent).toContain('The stack is live');
    expect(button('Add a step').disabled).toBe(true);
    expect(button('Once').disabled).toBe(true);
    expect(fields('lm-sequences-view').length).toBeGreaterThan(0);
    expect(fields('lm-sequences-view').every((field) => field.disabled)).toBe(true);

    // Stack again switches it off: the lock is gone
    await show('/stack');
    await press(button('Stack live'));
    expect(connection.last('SetStackLive')).toEqual([false]);
    await stackStands(NOT_LIVE);
    expect(liveTag()).toBeNull();
    expect(button('Add a step').disabled).toBe(false);
    expect(button('Play').disabled).toBe(true);
    expect(fields('lm-stack-view').some((field) => !field.disabled)).toBe(true);
  });

  it('a stack that goes live ends an unfinished recording and the editing of the independent keys', async () => {
    await press(button('Record sequence'));
    await press(memoryKey(9));
    await press(button('Edit the channels and memories of the independent keys'));
    expect(channelKey(7)).toBeTruthy();
    expect(page.querySelector('input[aria-label="Channel of independent key 2"]')).toBeTruthy();

    await stackStands(LIVE); // switched live on another page

    expect(store.mode()).toEqual(IDLE);
    expect(channelKey(7)).toBeNull();
    expect(page.querySelector('input[aria-label="Channel of independent key 2"]')).toBeNull();
  });

  it('the stack view adds, changes, renumbers, moves and takes out steps', async () => {
    await show('/stack');

    await press(button('Add a step'));
    expect(connection.last('AddStackStep')).toEqual([step(3, 128, { target: 'memory', wait: 3 })]);

    const value = page.querySelector<HTMLInputElement>('input[aria-label="Step 1.0, value, 0 to 255"]');
    if (!value) throw new Error('no value field in step 1.0');
    value.value = '90';
    value.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('UpdateStackStep')).toEqual([11, step(7, 90, { wait: 0 })]);

    // a number typed for a step: the desk decides, and the field shows the old one until it answers
    const cue = page.querySelector<HTMLInputElement>('input[aria-label="Number of step 2.0"]');
    if (!cue) throw new Error('no number field for step 2.0');
    cue.value = ' 0.5 ';
    cue.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('RenumberStackStep')).toEqual([12, '0.5']);
    expect(cue.value).toBe('2.0');

    await press(button('Move step 2.0 up'));
    expect(connection.last('MoveStackStep')).toEqual([12, null]); // to the very top
    await press(button('Move step 1.0 down'));
    expect(connection.last('MoveStackStep')).toEqual([11, 12]); // behind the step that follows
    expect(button('Move step 1.0 up').disabled).toBe(true);

    await press(button('Take step 2.0 out'));
    expect(connection.last('DeleteStackStep')).toEqual([12]);

    // the desk sends the stack after a change: the table follows
    connection.emit('StackChanged', { steps: [connection.stack.steps[0]] });
    await fixture.whenStable();
    expect(page.querySelectorAll('lm-stack-view table.steps tbody tr').length).toBe(1);
  });

  it('a step of the stack is moved by dragging its grip onto another row', async () => {
    await show('/stack');
    const rows = [...page.querySelectorAll<HTMLElement>('lm-stack-view table.steps tbody tr')];
    const grip = rows[1].querySelector<HTMLElement>('.grip');
    if (!grip) throw new Error('no grip on the second row');

    // jsdom knows no layout: every row is at height 0, so a pointer at -1 is "above the middle"
    const over = Object.assign(new Event('dragover', { bubbles: true, cancelable: true }), { clientY: -1 });
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
});
