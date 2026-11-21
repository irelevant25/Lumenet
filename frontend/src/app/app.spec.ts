import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';
import { ACTION_GROUPS, DESK_ACTIONS, actionWithId } from './desk/desk-actions';
import { DeskConnection } from './desk/desk-connection';
import { DeskSettings } from './desk/desk-settings';
import { DeskStore } from './desk/desk-store';
import {
  AutosaveOptions,
  CHANNEL_COUNT,
  EMPTY_STAGE,
  ImportAnswer,
  LanAccess,
  MEMORY_COUNT,
  Playback,
  Sequence,
  StackPlayback,
  StackState,
  StageLight,
  StagePlan,
  TimedStep,
  Welcome,
} from './desk/desk-types';
import { IDLE } from './desk/function-mode';
import { NOTE_LINE_SEPARATOR } from './desk/notes';

// The test browser (jsdom) has no modal windows. A window of the page only gets its "open" mark here.
if (typeof HTMLDialogElement.prototype.showModal !== 'function') {
  HTMLDialogElement.prototype.showModal = function (this: HTMLDialogElement) {
    this.open = true;
  };
  HTMLDialogElement.prototype.close = function (this: HTMLDialogElement) {
    this.open = false;
  };
}

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

  /** What the backend answers to "ImportProject", or the message it refuses it with. */
  importAnswer: ImportAnswer = {
    project: { name: 'Hamlet', path: null, unsaved: true },
    report: ['Hamlet.asc: 1 cue.', 'Memory 1 holds the cue.', 'Nothing is saved yet: Save keeps the project.'],
  };
  importRefusal: string | null = null;

  /** The project's settings, as the backend keeps them for the page on the PC: none at first. */
  projectSettings: unknown = null;
  /** What the backend answers to "GetRecovered": when unsaved work was brought back, or null. */
  recovered: string | null = null;
  /** The autosave of the desk program, as the backend answers "GetAutosave" (on the PC). */
  autosave: AutosaveOptions = { on: true, seconds: 5 };
  /** The stage plan, as the backend answers "GetStage": no lights at first. */
  stagePlan: StagePlan = { ...EMPTY_STAGE };
  /** The picture under the plan, as the backend answers "GetStagePicture". */
  stagePicture: string | null = null;

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
    if (method === 'ImportProject') {
      if (this.importRefusal) throw new Error(this.importRefusal);
      return this.importAnswer;
    }
    if (method === 'GetSettings') return this.projectSettings;
    if (method === 'AdoptSettings') {
      if (this.projectSettings !== null) return false;
      this.projectSettings = args[0];
      return true;
    }
    if (method === 'GetHistory') return { undo: null, redo: null };
    if (method === 'GetRecovered') return this.recovered;
    if (method === 'Undo' || method === 'Redo') return 'storing memory 4';
    if (method === 'GetAutosave') return this.autosave;
    if (method === 'GetStage') return this.stagePlan;
    if (method === 'SetStage') {
      this.stagePlan = args[0] as StagePlan;
      return undefined;
    }
    if (method === 'GetStagePicture') return this.stagePicture;
    if (method === 'SetStagePicture') {
      this.stagePicture = args[0] as string | null;
      return undefined;
    }
    if (method === 'SetAutosave') {
      this.autosave = { on: args[0] as boolean, seconds: args[1] as number };
      return this.autosave;
    }
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
const QUESTIONS = [
  'Hello',
  'GetMemories',
  'GetSequences',
  'GetStack',
  'GetPlayback',
  'GetLanAccess',
  'GetHistory',
  'GetRecovered',
  'GetSettings',
  'AdoptSettings',
  'GetAutosave',
  'GetStage',
  'GetStagePicture',
];

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
    // Settings belong to the browser: each test starts with none, and with nothing set on <html>.
    localStorage.clear();
    document.documentElement.removeAttribute('style');

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

  /** The key under a channel fader, if its channel is on the page that is shown. */
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

  /** Chooses a file in the import window, as the system's window for choosing files does. */
  async function pick(file: File): Promise<void> {
    const picker = page.querySelector<HTMLInputElement>('lm-import-dialog input[type="file"]');
    if (!picker) throw new Error('no window for choosing a file');
    Object.defineProperty(picker, 'files', { value: [file], configurable: true });
    picker.dispatchEvent(new Event('change'));
    await fixture.whenStable();
  }

  /** Waits for what the page does on its own, such as reading a file and waiting for the desk. */
  async function until(happened: () => boolean): Promise<void> {
    for (let round = 0; round < 50 && !happened(); round++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
      await fixture.whenStable();
    }
    if (!happened()) throw new Error('it did not happen');
  }

  function textOf(selector: string): string | undefined {
    return page.querySelector(selector)?.textContent?.trim();
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
    store.windowStripsPerRow.set(16);
    await fixture.whenStable();
    expect(page.querySelectorAll('lm-channel-section lm-fader-strip').length).toBe(32);
    expect(page.querySelectorAll('lm-memory-section lm-fader-strip').length).toBe(16);
    expect(page.querySelectorAll('nav[aria-label="Channel pages"] button').length).toBe(16);
    expect(page.querySelectorAll('nav[aria-label="Memory pages"] button').length).toBe(8);

    await press(button('Channels 33 to 64'));
    expect(page.querySelector('lm-fader[aria-label="Channel 33"]')).toBeTruthy();
    expect(page.querySelector('lm-fader[aria-label="Channel 1"]')).toBeNull();

    // wide again: the page that holds channel 33 is the one from 1 to 64
    store.windowStripsPerRow.set(32);
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
    // the record keys stand under the master section, the stack under the general section
    const sides = [...page.querySelectorAll('lm-main-view .side')].map((side) => [...side.children].map((part) => part.tagName.toLowerCase()));
    expect(sides).toEqual([
      ['lm-master-section', 'lm-record-section'],
      ['lm-general-section', 'lm-stack-section'],
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

  it('LAN, Undo and Redo stand at the top left; New, Save, Load and Import at the right', () => {
    const labels = (selector: string) =>
      [...page.querySelectorAll(`lm-top-bar ${selector} button`)].map((key) => key.getAttribute('aria-label'));
    expect(labels('.left')).toEqual(['LAN', 'Undo', 'Redo']);
    expect(labels('.project')).toEqual(['New project', 'Save project', 'Load project', 'Import a show']);
  });

  // ---------------------------------------------------------------- undo, solo, rate, project settings, autosave, colors

  it('undo and redo say what they would do, and do it', async () => {
    expect(button('Undo').disabled).toBe(true); // nothing to undo yet
    expect(button('Redo').disabled).toBe(true);

    connection.emit('HistoryChanged', { undo: 'storing memory 4', redo: null });
    await fixture.whenStable();
    expect(button('Undo').disabled).toBe(false);
    expect(button('Undo').title).toBe('Undo storing memory 4');

    await press(button('Undo'));
    await until(() => store.notice() !== '');
    expect(connection.methods()).toEqual(['Undo']);
    expect(store.notice()).toBe('Undone: storing memory 4.');

    // Ctrl + Z is the usual shortcut; Ctrl + Y for redo.
    connection.emit('HistoryChanged', { undo: null, redo: 'storing memory 4' });
    await fixture.whenStable();
    document.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyY', key: 'y', ctrlKey: true, bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(connection.methods()).toEqual(['Undo', 'Redo']);

    // A live stack locks what is stored, and undo with it.
    connection.emit('HistoryChanged', { undo: 'storing memory 4', redo: null });
    await stackStands(LIVE);
    expect(button('Undo').disabled).toBe(true);
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

  it('at the right of Main: MASTER over RECORD, GENERAL (CLEAR, SOLO UP, SOLO DOWN, RATE) over STACK, then IND; SOLO and PAUSE are white', async () => {
    const columns = [...page.querySelectorAll('lm-main-view > .side')].map((side) => [...side.children].map((child) => child.localName));
    expect(columns).toEqual([
      ['lm-master-section', 'lm-record-section'],
      ['lm-general-section', 'lm-stack-section'],
    ]);
    expect(page.querySelector('lm-main-view')?.lastElementChild?.localName).toBe('lm-independents');

    const keys = (section: string) => [...page.querySelectorAll(`${section} button`)];
    expect(keys('lm-master-section').map((key) => key.getAttribute('aria-label'))).toEqual(['Blackout']);
    expect(keys('lm-general-section').map((key) => key.getAttribute('aria-label') ?? key.textContent?.trim())).toEqual([
      'Clear',
      'Solo up',
      'Solo down',
      'Rate',
    ]);
    expect(keys('lm-general-section').map((key) => /tone-(\S+)/.exec(key.className)?.[1])).toEqual(['clear', 'solo', 'solo-down', 'rate']);
    // the rate stands on top of its fader, at the bottom of the section
    const parts = [...page.querySelectorAll('lm-general-section > *')].map((part) => (part.classList.contains('readout') ? 'rate' : part.localName));
    expect(parts.slice(-2)).toEqual(['rate', 'lm-fader']);

    // White, as CLEAR is: SOLO, and PAUSE on Main and in the Stack view.
    expect(button('Pause').className).toContain('tone-pause');
    await show('/stack');
    expect(button('Pause').className).toContain('tone-pause');
  });

  it('RATE lit makes its fader a speed master for every sequence; dark, each plays at its own', async () => {
    const fader = page.querySelector<HTMLElement>('lm-general-section lm-fader')!;
    const shown = () => textOf('lm-general-section .readout');
    const key = (name: string) => fader.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));

    expect(button('Rate').getAttribute('aria-pressed')).toBe('false');
    expect(button('Rate').className).toContain('level-1');
    expect(shown()).toBe('×1.00');
    expect(fader.getAttribute('aria-valuenow')).toBe('180'); // x1 in the middle
    expect(fader.classList).toContain('off'); // dim while RATE is dark

    await press(button('Rate'));
    expect(button('Rate').className).toContain('level-3');
    expect(fader.classList).not.toContain('off');

    // Up from x1 in steps of 0.5, as the speed fader of a sequence; from x0.1 at the bottom to x10 at the top.
    key('ArrowUp');
    await fixture.whenStable();
    expect(store.rate()).toBe(1.5);
    expect(shown()).toBe('×1.50');
    key('End');
    await fixture.whenStable();
    expect(store.rate()).toBe(10);
    key('Home');
    await fixture.whenStable();
    expect(store.rate()).toBe(0.1);
    await new Promise((resolve) => setTimeout(resolve, 40)); // the rate is sent a moment later, as fader moves are
    expect(connection.last('SetRate')).toEqual([true, 0.1]);

    // A double click puts it back to x1.
    fader.dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
    await fixture.whenStable();
    expect(shown()).toBe('×1.00');

    // Dark again: the sequences play at their own speeds; the fader can be set for the next time.
    await press(button('Rate'));
    key('ArrowUp');
    await fixture.whenStable();
    await new Promise((resolve) => setTimeout(resolve, 40));
    expect(connection.last('SetRate')).toEqual([false, 1.5]);
    expect(page.querySelector('lm-general-section .readout')?.classList).not.toContain('up');

    // What another page does reaches this one, as a fader does.
    connection.emit('RateChanged', true, 0.5);
    await fixture.whenStable();
    expect(button('Rate').getAttribute('aria-pressed')).toBe('true');
    expect(shown()).toBe('×0.50');
    expect(fader.getAttribute('aria-valuenow')).toBe('80');
  });

  it('Settings leaves views out of the top bar, but never itself; without Main the desk opens on the next view', async () => {
    const links = () => [...page.querySelectorAll('nav[aria-label="Views"] a')].map((link) => link.textContent?.trim().split(/\s/)[0]);
    await show('/settings');
    const box = (label: string) =>
      [...page.querySelectorAll<HTMLLabelElement>('ul.views label')].find((item) => item.textContent?.trim().startsWith(label))!.querySelector('input')!;
    expect([...page.querySelectorAll('ul.views label')].map((label) => label.textContent?.trim().split(/\s/)[0])).toEqual([
      'Main',
      'Memories',
      'Sequences',
      'Stack',
      'Stage',
      'Fixtures',
      'Settings',
      'Keyboard',
    ]);
    expect(box('Settings').checked).toBe(true);
    expect(box('Settings').disabled).toBe(true);

    box('Stage').click();
    box('Main').click();
    await fixture.whenStable();
    expect(links()).toEqual(['Memories', 'Sequences', 'Stack', 'Fixtures', 'Settings', 'Keyboard']);
    expect(connection.last('SetSettings')?.[0]).toMatchObject({ hiddenViews: ['main', 'stage'] }); // with the project, on the PC

    box('Stage').click();
    await fixture.whenStable();
    expect(links()).toContain('Stage');

    // Stage to the front
    for (let place = 0; place < 4; place++) await press(button('Move Stage up'));
    expect(links()).toEqual(['Stage', 'Memories', 'Sequences', 'Stack', 'Fixtures', 'Settings', 'Keyboard']);

    // a desk that starts at Main, while Main is left out, opens on the first view in the top bar
    const router = TestBed.inject(Router);
    TestBed.createComponent(App);
    await router.navigateByUrl('/');
    await fixture.whenStable();
    await until(() => router.url === '/stage');
  });

  it('Settings puts the views in another order with ▲ and ▼, and ↺ brings back the usual ones', async () => {
    const usual = ['Main', 'Memories', 'Sequences', 'Stack', 'Stage', 'Fixtures', 'Settings', 'Keyboard'];
    const links = () => [...page.querySelectorAll('nav[aria-label="Views"] a')].map((link) => link.textContent?.trim().split(/\s/)[0]);
    const listed = () => [...page.querySelectorAll('ul.views label')].map((label) => label.textContent?.trim().split(/\s/)[0]);
    await show('/settings');
    expect(button('The usual views').disabled).toBe(true);
    expect(button('Move Main up').disabled).toBe(true); // the first
    expect(button('Move Keyboard down').disabled).toBe(true); // the last

    // One place earlier, in Settings and in the top bar; the keyboard focus goes with it.
    await press(button('Move Keyboard up'));
    const moved = ['Main', 'Memories', 'Sequences', 'Stack', 'Stage', 'Fixtures', 'Keyboard', 'Settings'];
    expect(listed()).toEqual(moved);
    expect(links()).toEqual(moved);
    expect(document.activeElement).toBe(button('Move Keyboard up'));
    expect(connection.last('SetSettings')?.[0]).toMatchObject({ viewOrder: moved.map((name) => name.toLowerCase()) });
    expect(button('The usual views').disabled).toBe(false);

    // Back at the top, ▲ goes dark and the focus moves to ▼.
    await press(button('Move Main down'));
    expect(links().slice(0, 2)).toEqual(['Memories', 'Main']);
    await press(button('Move Main up'));
    expect(button('Move Main up').disabled).toBe(true);
    expect(document.activeElement).toBe(button('Move Main down'));

    // ↺: every view again, in the usual order.
    [...page.querySelectorAll<HTMLLabelElement>('ul.views label')].find((label) => label.textContent?.includes('Stage'))!.querySelector('input')!.click();
    await fixture.whenStable();
    expect(links()).not.toContain('Stage');
    await press(button('The usual views'));
    expect(links()).toEqual(usual);
    expect(listed()).toEqual(usual);
    expect(button('The usual views').disabled).toBe(true);
  });

  it('on the PC the layout and the shortcuts go with the project', async () => {
    // The desk program had none: the page handed it its own.
    await until(() => connection.calls.some((call) => call.method === 'AdoptSettings'));
    expect(connection.projectSettings).toMatchObject({ channelSize: 'auto', channelRows: 2 });
    expect(connection.projectSettings).not.toHaveProperty('fontScale'); // lettering stays with the device

    // A change goes to the project; the lettering does not.
    await show('/settings');
    const rows = page.querySelector<HTMLSelectElement>('#channel-rows')!;
    rows.value = '1';
    rows.dispatchEvent(new Event('change'));
    await press(button('Bigger lettering'));
    expect(connection.methods().filter((method) => method === 'SetSettings').length).toBe(1);
    expect(connection.last('SetSettings')?.[0]).toMatchObject({ channelRows: 1 });

    // A project that is loaded brings its own.
    connection.emit('SettingsChanged', { channelSize: 8, channelRows: 2, shortcuts: { 'master.clear': 'KeyC' } });
    await fixture.whenStable();
    expect(store.stripsPerRow()).toBe(4);
    expect(TestBed.inject(DeskSettings).shortcuts()['master.clear']).toBe('KeyC');
    expect(TestBed.inject(DeskSettings).fontScale()).toBe(1.1);

    // A phone keeps its own.
    connection.emit('LanAccessChanged', { ...connection.lan, local: false });
    connection.emit('SettingsChanged', { channelSize: 128 });
    await fixture.whenStable();
    expect(store.stripsPerRow()).toBe(4);
  });

  it('says once that the work which was not saved when the desk program stopped is back', async () => {
    connection.recovered = '2026-11-20T18:42:05+01:00';
    connection.start(); // connects again
    await until(() => store.notice() !== '');
    expect(store.notice()).toContain('(18:42) is back');
  });

  it('the autosave is switched and timed in Settings, on the PC only', async () => {
    await show('/settings');
    const on = page.querySelector<HTMLSelectElement>('#autosave-on')!;
    const every = page.querySelector<HTMLSelectElement>('#autosave-every')!;
    expect(on.value).toBe('on');
    expect(every.value).toBe('5');
    expect([...every.options].map((option) => option.textContent?.trim())).toEqual([
      'Every 5 seconds',
      'Every 30 seconds',
      'Every 1 minute',
      'Every 5 minutes',
    ]);

    every.value = '60';
    every.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetAutosave')).toEqual([true, 60]);

    on.value = 'off';
    on.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetAutosave')).toEqual([false, 60]);
    expect(every.disabled).toBe(true);

    // Another page on the PC switched it on again.
    connection.emit('AutosaveChanged', { on: true, seconds: 300 });
    await fixture.whenStable();
    expect(on.value).toBe('on');
    expect(every.value).toBe('300');

    // A phone cannot set it.
    connection.emit('LanAccessChanged', { ...connection.lan, local: false });
    await fixture.whenStable();
    expect(page.querySelector('#autosave-on')).toBeNull();
    expect(page.querySelector('lm-settings-view')?.textContent).toContain('Only the PC the desk program runs on can set the autosave.');
  });

  it('the color of a kind of keys or of one key is set in Settings, and goes with the project on the PC', async () => {
    const shown = (variable: string) => document.documentElement.style.getPropertyValue(variable);
    const sentColors = () => (connection.last('SetSettings')?.[0] as { colors?: object } | undefined)?.colors;
    await until(() => connection.calls.some((call) => call.method === 'AdoptSettings'));
    await show('/settings');

    // A press on a color offers the colors of the desk; one of them is taken, and they close.
    const pause = button('Color of Pause');
    expect(pause.getAttribute('aria-expanded')).toBe('false');
    await press(pause);
    expect(pause.getAttribute('aria-expanded')).toBe('true');
    const offered = [...page.querySelectorAll('.palette button')].map((swatch) => swatch.getAttribute('aria-label'));
    expect(offered).toEqual(['Green', 'Red', 'Yellow', 'Amber', 'White', 'Blue']);
    expect(button('White').getAttribute('aria-current')).toBe('true'); // the one it has
    await press(button('Red'));
    expect(shown('--key-pause')).toBe('#ff5247');
    expect(sentColors()).toEqual({ pause: '#ff5247' });
    expect(page.querySelector('.palette')).toBeNull();
    expect(button('The usual color for Pause').disabled).toBe(false);

    // Any other color: while it is picked, the desk shows it at once; when the picker closes, it is kept.
    await press(button('Color of Pause'));
    const other = page.querySelector<HTMLInputElement>('.palette input[type="color"]')!;
    expect(other.value).toBe('#ff5247');
    other.value = '#ff00ff';
    other.dispatchEvent(new Event('input'));
    expect(shown('--key-pause')).toBe('#ff00ff');
    other.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(sentColors()).toEqual({ pause: '#ff00ff' });

    // A kind: all channel keys, pages and faders. One palette is open at a time.
    await press(button('Color of Channels: their keys, pages, independent keys and faders'));
    expect(page.querySelectorAll('.palette').length).toBe(1);
    await press(button('Blue'));
    expect(shown('--channel')).toBe('#4aa3ff');
    expect(sentColors()).toEqual({ pause: '#ff00ff', channel: '#4aa3ff' });

    // Back to the usual colors: one, then all with ↺ beside the title.
    await press(button('The usual color for Pause'));
    expect(shown('--key-pause')).toBe('');
    expect(sentColors()).toEqual({ channel: '#4aa3ff' });
    await press(button('All the usual colors'));
    expect(shown('--channel')).toBe('');
    expect(sentColors()).toEqual({});
    expect(button('All the usual colors').disabled).toBe(true);

    // A project that is loaded brings its own.
    connection.emit('SettingsChanged', { colors: { play: '#123456', nonsense: '#000000', next: 'red' } });
    await fixture.whenStable();
    expect(shown('--key-play')).toBe('#123456');
    expect(TestBed.inject(DeskSettings).colors()).toEqual({ play: '#123456' });
  });

  // ---------------------------------------------------------------- the stage plan

  /** The color of each lens of a light on the plan, as "r g b". */
  function lenses(id: number): string[] {
    return [...page.querySelectorAll<HTMLElement>(`[data-light="${id}"] .lens`)].map((lens) => lens.style.getPropertyValue('--lens'));
  }

  /** A round light of one square, as the plan keeps it. */
  function stageLight(id: number, name: string, address: number, channels: string, color: string | null, x: number, y: number): StageLight {
    return { id, name, address, channels, color, x, y, angle: 0, shape: 'round', width: 1, depth: 1 };
  }

  /** The desk program sends a plan with these lights, as after another page changed it. */
  async function planWith(...lights: StageLight[]): Promise<void> {
    connection.emit('StageChanged', { ...EMPTY_STAGE, lights } satisfies StagePlan);
    await fixture.whenStable();
  }

  /** A press on a light of the plan. (jsdom knows no PointerEvent; a MouseEvent of that name does the same here.) */
  async function pressLight(id: number): Promise<void> {
    page.querySelector(`[data-light="${id}"]`)!.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, button: 0 }));
    await fixture.whenStable();
  }

  it('the Stage view shows every light in the color its channels give it, after the master, dark in blackout', async () => {
    await planWith(
      stageLight(1, 'Pot red', 3, 'D', '#ff2000', 2, 8),
      stageLight(2, 'LED bar', 11, 'RGBRGB', null, 8, 1),
      stageLight(3, 'Spot', 5, 'D', null, 8, 5),
      stageLight(4, 'Hazer', 20, 'X', null, -1, 1),
    );
    await show('/stage');
    expect(page.querySelectorAll('lm-stage-view .light').length).toBe(4);
    expect(lenses(1)).toEqual(['0 0 0']); // dark until its channel is up

    store.setChannel(3, 255);
    store.setChannel(11, 255); // red of the first cell of the bar
    store.setChannel(16, 255); // blue of the second
    store.setChannel(20, 255);
    await fixture.whenStable();
    expect(lenses(1)).toEqual(['255 32 0']);
    expect(lenses(2)).toEqual(['255 0 0', '0 0 255']);
    expect(page.querySelector<HTMLElement>('[data-light="4"] .level')?.style.height).toBe('100%'); // the hazer gives no light

    // A memory that holds the channel lights it too: memory 2 has channel 5 at 200.
    store.setMemoryLevel(2, 255);
    await fixture.whenStable();
    expect(lenses(3)).not.toEqual(['0 0 0']);

    // The master dims what goes out; blackout darkens it all.
    store.setMaster(128);
    await fixture.whenStable();
    expect(lenses(1)).toEqual(['186 23 0']);
    connection.emit('BlackoutChanged', true);
    await fixture.whenStable();
    expect(lenses(1)).toEqual(['0 0 0']);
    expect(textOf('lm-stage-view .summary')).toContain('Blackout is on');
  });

  it('Edit puts a light on the next free channel, named after its note; it is moved, changed and taken away', async () => {
    await planWith(stageLight(1, 'Par', 1, 'DRGBS', null, 2, 8));
    await show('/stage');
    expect(button('Add light').disabled).toBe(true); // only while editing
    await press(button('Edit the plan'));
    expect(button('Edit the plan').getAttribute('aria-pressed')).toBe('true');

    // channels 1 to 5 are the par's: the new light is on 6, which has the note "Front / left"
    await press(button('Add light'));
    const added = (connection.last('SetStage')?.[0] as StagePlan).lights[1];
    expect(added).toMatchObject({ id: 2, name: 'Front left', address: 6, channels: 'D' });
    expect(page.querySelector<HTMLInputElement>('#light-name')?.value).toBe('Front left'); // chosen, in the panel

    // An RGB bar of four cells, without a dimmer.
    const kind = page.querySelector<HTMLSelectElement>('#light-type')!;
    kind.value = 'rgb';
    kind.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    const cells = page.querySelector<HTMLInputElement>('#light-cells')!;
    cells.value = '4';
    cells.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.stage().lights[1].channels).toBe('RGBRGBRGBRGB');
    expect(page.querySelectorAll('[data-light="2"] .lens').length).toBe(4);

    // The arrow keys move it by half a square; Delete takes it away.
    const element = page.querySelector<HTMLElement>('[data-light="2"]')!;
    const before = store.stage().lights[1];
    element.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(store.stage().lights[1].x).toBe(before.x + 0.5);
    expect(connection.last('SetStage')?.[0]).toEqual(store.stage());

    element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Delete', bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(store.stage().lights.map((light) => light.id)).toEqual([1]);
    expect(page.querySelector('#stage-width')).toBeTruthy(); // nothing chosen: the panel shows the stage
  });

  it('a click on a light shows it at the right with the faders of its channels, which work as those of Main', async () => {
    await planWith(stageLight(1, 'Par', 11, 'DRGB', null, 2, 8), stageLight(2, 'Spot', 5, 'D', null, 8, 5));
    await show('/stage');
    // The panel is always there, so that the plan keeps its size: with nothing chosen, what a click does.
    const nothingChosen = () => textOf('lm-stage-panel h3') === 'Light' && page.querySelector('lm-light-faders') === null;
    expect(nothingChosen()).toBe(true);

    await pressLight(1);
    expect(textOf('lm-stage-panel h3')).toBe('Par');
    expect(textOf('lm-stage-panel .facts')).toBe('RGB, dimmer first, on channels 11 to 14');
    expect(page.querySelector('#light-name')).toBeNull(); // out of Edit nothing about it can be changed...
    const faders = [...page.querySelectorAll<HTMLElement>('lm-light-faders lm-fader')];
    expect(faders.map((fader) => fader.getAttribute('aria-label'))).toEqual([
      'Channel 11, dimmer',
      'Channel 12, red',
      'Channel 13, green',
      'Channel 14, blue',
    ]);

    // ...but its faders work: the dimmer and the red to full, and the light is red.
    for (const fader of faders.slice(0, 2)) fader.dispatchEvent(new KeyboardEvent('keydown', { key: 'End', bubbles: true }));
    await fixture.whenStable();
    expect(store.channels().slice(10, 14)).toEqual([255, 255, 0, 0]);
    expect(lenses(1)).toEqual(['255 0 0']);
    await new Promise((resolve) => setTimeout(resolve, 40)); // fader moves are sent a moment later
    expect(connection.calls).toContainEqual({ method: 'SetChannel', args: [12, 255] });

    // A memory that holds a channel holds its fader up here too: memory 2 has channel 5 at 200.
    await pressLight(2);
    store.setMemoryLevel(2, 255);
    await fixture.whenStable();
    expect(page.querySelector('lm-light-faders lm-fader')?.getAttribute('aria-valuenow')).toBe('200');

    // A press beside the lights, or Close, lets the light go.
    page.querySelector('lm-stage-view .plan')!.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, button: 0 }));
    await fixture.whenStable();
    expect(nothingChosen()).toBe(true);
    await pressLight(2);
    await press(button('Close'));
    expect(nothingChosen()).toBe(true);

    // In Edit the faders stand under what can be changed.
    await press(button('Edit the plan'));
    await pressLight(1);
    expect(page.querySelector('#light-name')).toBeTruthy();
    expect(page.querySelectorAll('lm-light-faders lm-fader').length).toBe(4);
  });

  it('a double click puts the faders of a light to full and back; Clear puts every light of the plan to zero', async () => {
    await planWith(stageLight(1, 'Par', 11, 'DRGBS', null, 2, 8), stageLight(2, 'Spot', 5, 'D', null, 8, 5));
    await show('/stage');
    const twice = (id: number) => page.querySelector(`[data-light="${id}"]`)!.dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));

    twice(1);
    await fixture.whenStable();
    expect(store.channels().slice(10, 15)).toEqual([255, 255, 255, 255, 0]); // the strobe stays
    expect(lenses(1)).toEqual(['255 255 255']);
    twice(1);
    await fixture.whenStable();
    expect(store.channels().slice(10, 15)).toEqual([0, 0, 0, 0, 0]);

    // Clear: every channel of every light on the plan to zero, and nothing else.
    twice(1);
    twice(2);
    store.setChannel(30, 99);
    await fixture.whenStable();
    await press(button('Clear the lights of the plan'));
    expect(store.channels().slice(10, 15)).toEqual([0, 0, 0, 0, 0]);
    expect(store.channels()[4]).toBe(0);
    expect(store.channels()[29]).toBe(99);
  });

  it('the Fixtures view plays the lights of the plan: a fader each, a color, and groups that move all their lights', async () => {
    await planWith(stageLight(1, 'Par', 11, 'RGB', null, 2, 8), stageLight(2, 'Front', 5, 'D', null, 8, 5));
    await show('/fixtures');
    const strip = (name: string) =>
      [...page.querySelectorAll('lm-fixture-strip')].find((item) => item.querySelector('.name')?.textContent?.trim() === name)!;
    expect([...page.querySelectorAll('lm-fixture-strip .name')].map((name) => name.textContent?.trim())).toEqual(['Front', 'Par']);

    // a fixture without a dimmer comes up in white from dark, and its fader keeps its colors' mix
    strip('Par').querySelector('lm-fader')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'End' }));
    await fixture.whenStable();
    expect(store.channels().slice(10, 13)).toEqual([255, 255, 255]);
    await press(strip('Front').querySelector<HTMLButtonElement>('button[lmLamp]'));
    expect(store.channels()[4]).toBe(255);

    // its color on top shows it in the panel, with colors to press
    await press(button('Par: color and settings'));
    expect(page.querySelector('lm-fixture-panel h3')?.textContent?.trim()).toBe('Par');
    await press(button('Red'));
    expect(store.channels().slice(10, 13)).toEqual([255, 0, 0]);
    expect(strip('Par').querySelector<HTMLElement>('.swatch')!.style.getPropertyValue('--swatch')).toBe('#ff0000');

    // a new group takes the fixture chosen; its fader moves all its lights, each in its own color
    await press(button('New group'));
    expect(connection.stagePlan.groups).toEqual([{ id: 1, name: 'Group 1', lights: [1] }]);
    const front = page.querySelector<HTMLInputElement>('lm-fixture-panel .members li:first-child input')!;
    front.click(); // Front, the first by its address, joins the group
    await fixture.whenStable();
    expect(connection.stagePlan.groups[0].lights).toEqual([1, 2]);
    strip('Group 1').querySelector('lm-fader')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Home' }));
    await fixture.whenStable();
    expect(store.channels()[4]).toBe(0);
    expect(store.channels().slice(10, 13)).toEqual([0, 0, 0]);
    strip('Group 1').querySelector('lm-fader')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'End' }));
    await fixture.whenStable();
    expect(store.channels()[4]).toBe(255);
    expect(store.channels().slice(10, 13)).toEqual([255, 0, 0]); // red again, as it was
  });

  it('a moving head takes its place from the memory raised last; the master and blackout leave it where it is', async () => {
    await planWith(stageLight(1, 'Head', 1, 'PTD', null, 2, 2));
    const look = (pan: number, tilt: number) => [pan, tilt, 255, ...new Array<number>(CHANNEL_COUNT - 3).fill(0)];
    store.memoryLooks.set({ 4: look(10, 20), 5: look(200, 220) });

    store.setMemoryLevel(4, 128);
    expect(store.shownChannels().slice(0, 3)).toEqual([10, 20, 128]); // the place as stored, the dimmer at half
    store.setMemoryLevel(5, 255);
    expect(store.shownChannels().slice(0, 3)).toEqual([200, 220, 255]);
    store.setChannel(1, 99); // a fader moved later takes its own channel
    expect(store.shownChannels().slice(0, 3)).toEqual([99, 220, 255]);
    store.setMemoryLevel(5, 0);
    expect(store.shownChannels().slice(0, 3)).toEqual([99, 20, 128]);

    store.setMaster(128);
    expect(store.output().slice(0, 3)).toEqual([99, 20, 64]);
    connection.emit('BlackoutChanged', true);
    expect(store.output().slice(0, 3)).toEqual([99, 20, 0]);
  });

  it('the faders of the main view say what the plan has on a channel where no note was typed', async () => {
    await planWith(stageLight(1, 'Spot', 1, 'PTD', null, 2, 2));
    const fields = [
      ...page.querySelectorAll<HTMLInputElement>('[aria-label="Note for Channel 1"], [aria-label="Note for Channel 1, second line"]'),
    ];
    expect(fields.map((field) => field.placeholder)).toEqual(['Spot', 'Pan']);
    expect(fields.map((field) => field.value)).toEqual(['', '']);
  });

  it('a light can be limited, copied, and made a light of the fixture library', async () => {
    await planWith(stageLight(1, 'Spot 1', 11, 'D', null, 2, 2));
    await show('/stage');
    await press(button('Edit the plan'));
    await pressLight(1);

    const limit = page.querySelector<HTMLInputElement>('#light-limit')!;
    limit.value = '60';
    limit.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.stagePlan.lights[0].limit).toBe(60);

    await press(button('Copy the light'));
    expect(connection.stagePlan.lights[1]).toMatchObject({ id: 2, name: 'Spot 2', address: 12, limit: 60 });
    // the copy is chosen now

    // the library: its list is fetched (here from a pretend library), searched, and a mode chosen
    const files = 'https://raw.githubusercontent.com/OpenLightingProject/open-fixture-library/master/fixtures';
    const answers: Record<string, unknown> = {
      [`${files}/manufacturers.json`]: { maker: { name: 'Maker' } },
      'https://api.github.com/repos/OpenLightingProject/open-fixture-library/git/trees/master:fixtures?recursive=1': {
        tree: [{ path: 'maker/head.json', type: 'blob' }],
      },
      [`${files}/maker/head.json`]: {
        name: 'Head',
        availableChannels: { Pan: { capability: { type: 'Pan' } }, Tilt: { capability: { type: 'Tilt' } }, Dimmer: { capability: { type: 'Intensity' } } },
        modes: [{ name: '3-channel', channels: ['Pan', 'Tilt', 'Dimmer'] }],
      },
    };
    vi.stubGlobal('fetch', async (url: string) => ({ ok: url in answers, status: url in answers ? 200 : 404, json: async () => answers[url] }));
    try {
      const kind = page.querySelector<HTMLSelectElement>('#light-type')!;
      kind.value = 'library-search';
      kind.dispatchEvent(new Event('change'));
      await fixture.whenStable();
      await new Promise((resolve) => setTimeout(resolve, 0));
      await fixture.whenStable();
      const search = page.querySelector<HTMLInputElement>('lm-library-dialog input[type="search"]')!;
      search.value = 'maker head';
      search.dispatchEvent(new Event('input'));
      await fixture.whenStable();
      await press(page.querySelector<HTMLButtonElement>('lm-library-dialog .found button'));
      await new Promise((resolve) => setTimeout(resolve, 0));
      await fixture.whenStable();
      await press(button('Use this mode'));
    } finally {
      vi.unstubAllGlobals();
      localStorage.clear();
    }
    expect(connection.stagePlan.types).toEqual([
      {
        id: 1,
        maker: 'Maker',
        model: 'Head',
        mode: '3-channel',
        source: 'maker/head',
        channels: [
          { name: 'Pan', code: 'P', fine: null, ranges: [] },
          { name: 'Tilt', code: 'T', fine: null, ranges: [] },
          { name: 'Dimmer', code: 'D', fine: null, ranges: [] },
        ],
      },
    ]);
    expect(connection.stagePlan.lights[1]).toMatchObject({ type: 1, channels: 'PTD', name: 'Spot 2', address: 12 });
    expect(connection.stagePlan.lights[0]).toMatchObject({ channels: 'D' });
    expect(page.querySelector<HTMLSelectElement>('#light-type')!.selectedOptions[0].textContent?.trim()).toBe('Head, 3-channel');
  });

  /** The desk program sends a plan with these lights and one group, "Pars", of the first light. */
  async function planWithGroup(...lights: StageLight[]): Promise<void> {
    connection.emit('StageChanged', { ...EMPTY_STAGE, lights, groups: [{ id: 1, name: 'Pars', lights: [lights[0].id] }] } satisfies StagePlan);
    await fixture.whenStable();
  }

  it('a step of a sequence can be a fixture or a group, in a color', async () => {
    await planWithGroup(stageLight(1, 'Par', 11, 'RGB', null, 2, 8), stageLight(2, 'Front', 5, 'D', null, 8, 5));
    await show('/sequences');
    await press(button('Work on memory 3'));
    const kind = page.querySelector<HTMLSelectElement>('select[aria-label="Step 1, channel, memory, fixture or group"]')!;
    expect([...kind.options].map((option) => option.text.trim())).toEqual(['Channel', 'Memory', 'Fixture', 'Group']);

    // a fixture: the first by its address, which the closed field shows; its name is the note
    kind.value = 'fixture';
    kind.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetSequence')?.[1]).toMatchObject({ steps: [{ ...step(5, 200), target: 'fixture', number: 2, color: null }, step(6, 100, { wait: 0 })] });
    const first = page.querySelectorAll('lm-sequences-view table.steps tbody tr')[0];
    expect(first.querySelector<HTMLSelectElement>('td.number select')!.selectedOptions[0].text.trim()).toBe('5');
    expect(first.querySelector('td.note')?.textContent?.trim()).toBe('Front');

    // its color: the colors of the Fixtures page
    await press(button('Step 1, color'));
    await press(page.querySelector<HTMLButtonElement>('lm-step-color-dialog button[aria-label="Red"]'));
    expect(connection.last('SetSequence')?.[1]).toMatchObject({ steps: [{ target: 'fixture', number: 2, color: '#ff0000' }, step(6, 100, { wait: 0 })] });

    // a group: its name is the note, "G1" the closed field
    kind.value = 'group';
    kind.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetSequence')?.[1]).toMatchObject({ steps: [{ target: 'group', number: 1, color: '#ff0000' }, step(6, 100, { wait: 0 })] });
    expect(first.querySelector<HTMLSelectElement>('td.number select')!.selectedOptions[0].text.trim()).toBe('G1');
    expect(first.querySelector('td.note')?.textContent?.trim()).toBe('Pars');
  });

  it('Rec seq takes steps of fixtures and groups from the Fixtures page, in the color they show', async () => {
    await planWithGroup(stageLight(1, 'Par', 11, 'RGB', null, 2, 8), stageLight(2, 'Front', 5, 'D', null, 8, 5));
    store.setChannel(11, 255); // the par is red
    store.pressFunction('record');
    store.pressMemory(9);
    await show('/fixtures');
    const key = (name: string) =>
      [...page.querySelectorAll('lm-fixture-strip')].find((item) => item.querySelector('.name')?.textContent?.trim() === name)!.querySelector<HTMLButtonElement>('button[lmLamp]')!;

    await press(key('Par'));
    expect(connection.last('AddSequenceStep')).toEqual([9, { ...step(1, 255), target: 'fixture', color: '#ff0000' }]);
    await press(key('Pars'));
    expect(connection.last('AddSequenceStep')).toEqual([9, { ...step(1, 255), target: 'group', color: '#ff0000' }]);
    expect(store.channels()[10]).toBe(255); // the key put in a step and did not switch the par
    expect(key('Par').querySelector('.steps')?.textContent?.trim()).toBe('1');
  });

  it('a template runs over the fixtures of a group, in a color', async () => {
    connection.emit('StageChanged', {
      ...EMPTY_STAGE,
      lights: [stageLight(4, 'Right', 30, 'RGB', null, 2, 8), stageLight(3, 'Left', 20, 'RGB', null, 8, 5)],
      groups: [{ id: 1, name: 'Pars', lights: [4, 3] }],
    } satisfies StagePlan);
    await fixture.whenStable();
    await show('/sequences');
    await press(button('Work on memory 3'));
    await press(button('Make the steps from a template'));
    await press(button('Chase'));
    const target = page.querySelector<HTMLSelectElement>('#template-target')!;
    target.value = 'group';
    target.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    await press(page.querySelector<HTMLButtonElement>('lm-template-dialog button[aria-label="Blue"]'));
    await press(button('Make the steps'));

    const steps = (connection.last('SetSequence')?.[1] as { steps: TimedStep[] }).steps;
    // the lights of the group by their address: Left (20), then Right (30)
    expect(steps.map((made) => [made.target, made.number, made.color])).toEqual([
      ['fixture', 3, '#0000ff'],
      ['fixture', 4, '#0000ff'],
    ]);
  });

  it('a light is round or square and as big as some squares; one cell lights all of it, cells divide it', async () => {
    await planWith(stageLight(1, 'Bar', 11, 'RGB', null, 3, 2));
    await show('/stage');
    await press(button('Edit the plan'));
    await pressLight(1);

    const shape = page.querySelector<HTMLSelectElement>('#light-shape')!;
    shape.value = 'square';
    shape.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    const width = page.querySelector<HTMLInputElement>('#light-width')!;
    width.value = '4';
    width.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    // four squares wide and one deep, lying on the lines of the grid (from 2 to 6 across, 2 to 3 deep)
    expect(store.stage().lights[0]).toMatchObject({ shape: 'square', width: 4, depth: 1, x: 4, y: 2.5 });
    const fixtureOf = () => page.querySelector<HTMLElement>('[data-light="1"] .fixture')!;
    expect(fixtureOf().classList).toContain('square');
    expect(fixtureOf().style.getPropertyValue('--w')).toBe('3.92');

    // One cell: red on its channel, and all of the bar is red.
    store.setChannel(11, 255);
    await fixture.whenStable();
    expect(lenses(1)).toEqual(['255 0 0']);

    // Four cells: they divide it, and only the first is red.
    const cells = page.querySelector<HTMLInputElement>('#light-cells')!;
    cells.value = '4';
    cells.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(lenses(1)).toEqual(['255 0 0', '0 0 0', '0 0 0', '0 0 0']);
  });

  it('a picture lies under the stage or under the whole plan, and shows through as brightly as set', async () => {
    await show('/stage');
    await press(button('Edit the plan'));
    expect(page.querySelector('lm-stage-view img.picture')).toBeNull();

    // Another page put a picture under the plan: the plan names it, and the page fetches it.
    connection.stagePicture = 'data:image/png;base64,iVBORw0KGgo=';
    connection.emit('StageChanged', { ...EMPTY_STAGE, pictureId: '0123456789abcdef' } satisfies StagePlan);
    await until(() => page.querySelector('lm-stage-view img.picture') !== null);
    const picture = () => page.querySelector<HTMLImageElement>('lm-stage-view img.picture')!;
    expect(picture().src).toBe('data:image/png;base64,iVBORw0KGgo=');
    expect(picture().style.opacity).toBe('0.6');
    expect(picture().style.left).toBe('10%'); // on the stage: 2 of the 20 squares of the plan beside it

    const over = page.querySelector<HTMLSelectElement>('#picture-over')!;
    over.value = 'plan';
    over.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetStage')?.[0]).toMatchObject({ pictureOver: 'plan' });
    expect(picture().style.left).toBe('0%');

    const opacity = page.querySelector<HTMLInputElement>('#picture-opacity')!;
    opacity.value = '30';
    opacity.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetStage')?.[0]).toMatchObject({ pictureOpacity: 0.3 });
    expect(picture().style.opacity).toBe('0.3');

    await press(button('Remove the picture'));
    expect(connection.last('SetStagePicture')).toEqual([null]);
    expect(page.querySelector('lm-stage-view img.picture')).toBeNull();
  });

  it('the stage plan cannot be changed while the stack is live', async () => {
    await show('/stage');
    await press(button('Edit the plan'));
    expect(page.querySelector('#stage-width')).toBeTruthy();

    await stackStands(LIVE);
    expect(page.querySelector('#stage-width')).toBeNull(); // Edit is over: the panel says what a click does
    expect(button('Edit the plan').disabled).toBe(true);
    expect(textOf('lm-stage-view .summary')).toContain('The stack is live');
    expect(textOf('lm-stage-view .nothing')).toBe('No lights yet. Edit, then Add light.');
  });

  it('import: a show file becomes the project, and the window says what became of it', async () => {
    connection.emit('ProjectChanged', { name: 'Untitled', path: null, unsaved: true });
    await press(button('Import a show'));
    const dialog = page.querySelector<HTMLDialogElement>('lm-import-dialog dialog');
    expect(dialog?.open).toBe(true);
    expect(textOf('lm-import-dialog .file-name')).toBe('No file chosen');
    expect(button('Import the show').disabled).toBe(true);

    await pick(new File(['Cue 1\nChan 1 FL\n'], 'Hamlet.asc'));
    expect(textOf('lm-import-dialog .file-name')).toBe('Hamlet.asc');
    expect(page.querySelector('lm-import-dialog')?.textContent).toContain(
      'The open project has changes that are not saved. Importing drops them.',
    );
    await press(button('Import the show'));
    await until(() => page.querySelector('lm-import-dialog .report') !== null);

    expect(connection.last('ImportProject')).toEqual(['Hamlet.asc', 'Cue 1\nChan 1 FL\n']);
    const report = [...page.querySelectorAll('lm-import-dialog .report p')].map((line) => line.textContent?.trim());
    expect(report).toEqual(connection.importAnswer.report);
    expect(textOf('lm-top-bar .name strong')).toBe('Hamlet');

    // OK closes the window. Import again asks for a file at once, over a new question.
    await press(button('Close'));
    expect(dialog?.open).toBe(false);
    const asked = vi.spyOn(HTMLInputElement.prototype, 'click');
    await press(button('Import a show'));
    expect(asked).toHaveBeenCalledTimes(1);
    expect(textOf('lm-import-dialog .file-name')).toBe('No file chosen');
    asked.mockRestore();
    await press(page.querySelector<HTMLButtonElement>('lm-import-dialog .actions button')); // Cancel
    expect(dialog?.open).toBe(false);

    // saving offers the name of the show file
    await press(button('Save project'));
    expect(page.querySelector<HTMLInputElement>('#project-name')?.value).toBe('Hamlet');
  });

  it('import says why the desk refused a file, and another one can be tried', async () => {
    connection.importRefusal = '"notes.txt" has no cues, submasters or groups.';
    await press(button('Import a show'));
    await pick(new File(['Dear diary,'], 'notes.txt'));
    await press(button('Import the show'));
    await until(() => page.querySelector('lm-import-dialog .error') !== null);

    expect(textOf('lm-import-dialog .error')).toBe('"notes.txt" has no cues, submasters or groups.');
    expect(page.querySelector<HTMLDialogElement>('lm-import-dialog dialog')?.open).toBe(true);
    expect(button('Import the show').disabled).toBe(false);
  });

  it('import takes no file larger than the desk takes, and sends nothing', async () => {
    await press(button('Import a show'));
    const file = new File(['Cue 1'], 'huge.asc');
    Object.defineProperty(file, 'size', { value: 9 * 1024 * 1024 });
    await pick(file);

    expect(textOf('lm-import-dialog .error')).toBe('"huge.asc" has 9.0 MB. A show file can have up to 8.0 MB.');
    expect(button('Import the show').disabled).toBe(true);
    expect(connection.methods()).not.toContain('ImportProject');
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
      'Settings',
      'Keyboard',
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

  it('the cells of the memories view carry the notes of their channels', async () => {
    connection.emit('ChannelNoteChanged', 5, 'Front' + NOTE_LINE_SEPARATOR + 'left');
    connection.emit('ChannelNoteChanged', 6, 'Wash');
    await show('/memories');
    const cell = (channel: number) => page.querySelectorAll('lm-memories-view table.matrix td')[channel - 1];

    // every cell keeps room for a note, so that all have the same size
    expect(page.querySelector('lm-memories-view table.matrix')?.classList).toContain('with-notes');
    expect(cell(5).getAttribute('data-note')).toBe('Front\nleft'); // the style sheet shows it, one line under the other
    expect(cell(5).getAttribute('aria-label')).toMatch(/^Channel 5 \(Front left\): \d+$/);
    expect(cell(6).getAttribute('data-note')).toBe('Wash');
    expect(cell(7).hasAttribute('data-note')).toBe(false);
    expect(cell(7).getAttribute('aria-label')).toMatch(/^Channel 7: \d+$/);
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
    expect(connection.last('SetSequence')).toEqual([9, { loop: true, random: false, speed: 1, steps: [step(1, 255)] }]);
    expect(store.memorySequence()[8]).toBe(true);
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
    expect(connection.last('SetSequence')?.[1]).toEqual(expect.objectContaining({ loop: false, random: false }));
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
    const numbers = page.querySelector<HTMLInputElement>('input[aria-label="Numbers of the lights, like 1, 2 or 1-4"]');
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
    expect(page.querySelector('.summary')?.textContent).toContain('16 steps, played in a random order');

    // another template over it: a chase of the two lights, in turn
    await press(button('Make the steps from a template'));
    expect(page.querySelector('lm-template-dialog .hint')?.textContent).toContain('replaces the 16 steps of memory 9');
    await press(button('Chase'));
    await press(button('Make the steps'));
    expect((connection.last('SetSequence')?.[1] as Sequence).steps.map((item) => item.number)).toEqual([1, 2]);
    expect(button('Loop').getAttribute('aria-pressed')).toBe('true');
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
    expect(second.options[4].text).toBe('5'); // a channel without a note: its number alone
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
    const kind = rows()[0].querySelector<HTMLSelectElement>('select[aria-label="Step 1, channel, memory, fixture or group"]');
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
      'Import a show',
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
    expect(store.mode()).toEqual({ kind: 'recording', memory: 9 });
    expect(page.querySelector('input[aria-label="Channel of independent key 2"]')).toBeTruthy();

    await stackStands(LIVE); // switched live on another page

    expect(store.mode()).toEqual(IDLE);
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

  // ---------------------------------------------------------------- the stack: previous, and the main view

  it('the stack view shows the step before the current one, and its keys are bigger, Play the biggest', async () => {
    await show('/stack');
    const rows = () => [...page.querySelectorAll('lm-stack-view table.steps tbody tr')];

    // On the second step the first is the one before it.
    await stackStands({ ...LIVE, currentId: 12, nextId: 11 });
    expect(rows().map((row) => row.classList.contains('previous'))).toEqual([true, false]);
    expect(page.querySelector('.card.previous')?.classList.contains('on')).toBe(true);
    expect(page.querySelector('.card.previous .what')?.textContent).toContain('1.0');

    // On the first step it is the last: Prev goes round, as Play does.
    await stackStands(LIVE);
    expect(page.querySelector('.card.previous .what')?.textContent).toContain('2.0');

    // Not live: no step is on, so none is before it.
    await stackStands(NOT_LIVE);
    expect(page.querySelector('.card.previous')?.classList.contains('on')).toBe(false);
    expect(rows().some((row) => row.classList.contains('previous'))).toBe(false);

    expect(button('Play').classList).toContain('play');
    expect(button('Play').closest('.transport')).toBeTruthy();
    expect(button('Previous step').closest('.transport')).toBeTruthy();
  });

  it('the main view has the stack between the master and the record keys, with a wheel of its steps', async () => {
    const section = page.querySelector('lm-stack-section');
    if (!section) throw new Error('no stack section on the main view');
    // The places in the window of the wheel, from the top: one more on each side only rolls in.
    const wheel = () =>
      [...section.querySelectorAll('.roller li')].slice(1, -1).map((place) => place.textContent?.trim());

    expect(wheel()).toEqual(['', '-', '1.0']); // nothing is on; Play goes to the first step
    expect(section.querySelector('.roller .next')?.getAttribute('aria-label')).toBe('Next step: 1.0');
    expect(section.querySelector('.pointer')).toBeTruthy(); // the arrow beside the middle
    expect(button('Play').disabled).toBe(true); // only while the stack is live

    await press(button('Stack live'));
    expect(connection.last('SetStackLive')).toEqual([true]);

    // Two steps: the other one stands above and below, as Prev and Play both go to it.
    await stackStands(LIVE);
    expect(wheel()).toEqual(['2.0', '1.0', '2.0']);
    expect(section.querySelector('.roller .current')?.classList.contains('empty')).toBe(false);
    expect(section.querySelector('.roller .current')?.getAttribute('aria-label')).toBe('Current step: 1.0');
    expect(section.querySelector('.roller .previous')?.getAttribute('aria-label')).toBe('Previous step: 2.0');

    await press(button('Play'));
    await press(button('Pause'));
    await press(button('Next step'));
    await press(button('Previous step'));
    expect(connection.methods()).toEqual(['SetStackLive', 'StackPlay', 'StackPause', 'StackNext', 'StackPrevious']);
  });

  it('Settings says how many steps the stack wheel shows', async () => {
    const steps = Array.from({ length: 9 }, (_, index) => ({
      id: 21 + index,
      cue: String(index + 1),
      target: 'memory' as const,
      number: index + 1,
      value: 255,
      up: 1,
      wait: 0,
      down: 1,
    }));
    connection.emit('StackChanged', { steps });
    await stackStands({ ...LIVE, currentId: 25, nextId: 26 }); // step 5 is on
    const seen = () =>
      [...page.querySelectorAll('lm-stack-section .roller li')]
        .filter((place) => place.getAttribute('aria-hidden') !== 'true')
        .map((place) => place.textContent?.trim());
    expect(seen()).toEqual(['4', '5', '6']);

    await show('/settings');
    const size = page.querySelector<HTMLSelectElement>('#wheel-size')!;
    size.value = '7';
    size.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    await show('/');
    expect(seen()).toEqual(['2', '3', '4', '5', '6', '7', '8']);
    // the main view sizes its side column with it, the section its window
    expect(page.querySelector('lm-main-view')?.getAttribute('style')).toContain('--wheel-size: 7');
  });

  // ---------------------------------------------------------------- settings

  it('the channel size of Settings sets how many faders a page holds, and the page keys follow', async () => {
    await show('/settings');
    const size = page.querySelector<HTMLSelectElement>('#channel-size');
    if (!size) throw new Error('no channel size');
    expect(size.value).toBe('auto');

    size.value = '8';
    size.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.stripsPerRow()).toBe(4);
    expect(JSON.parse(localStorage.getItem('lumenet-settings') ?? '{}').channelSize).toBe(8);

    await show('/');
    const line = () => page.querySelector<HTMLElement>('lm-main-view')?.style.getPropertyValue('--line');
    expect(line()).toBe('4'); // on the main view only
    expect(document.documentElement.style.getPropertyValue('--line')).toBe('');
    expect(page.querySelectorAll('lm-channel-section lm-fader-strip').length).toBe(8); // two rows of 4
    expect(page.querySelectorAll('lm-memory-section lm-fader-strip').length).toBe(4); // one row
    expect(page.querySelectorAll('nav[aria-label="Channel pages"] button').length).toBe(64);
    expect(page.querySelectorAll('nav[aria-label="Memory pages"] button').length).toBe(32);
    await press(button('Channels 9 to 16'));
    expect(page.querySelector('lm-fader[aria-label="Channel 9"]')).toBeTruthy();

    // The Memories and Sequences views keep the row the window gives them.
    await show('/memories');
    expect(page.querySelectorAll('lm-memories-view .keys button').length).toBe(32);
    expect(page.querySelectorAll('lm-memories-view table.matrix tr')[0].children.length).toBe(32);
    await show('/sequences');
    expect(page.querySelectorAll('lm-sequences-view nav[aria-label="Memory pages"] button').length).toBe(4);

    await show('/settings');
    const again = page.querySelector<HTMLSelectElement>('#channel-size')!;
    again.value = 'auto';
    again.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.stripsPerRow()).toBe(32);
    await show('/');
    expect(line()).toBe('');
  });

  it('Settings says how many rows of channel faders and of memory faders the main view has', async () => {
    await show('/settings');
    const channelRows = page.querySelector<HTMLSelectElement>('#channel-rows')!;
    const memoryRows = page.querySelector<HTMLSelectElement>('#memory-rows')!;
    expect([channelRows.value, memoryRows.value]).toEqual(['2', '1']);

    channelRows.value = '1';
    channelRows.dispatchEvent(new Event('change'));
    memoryRows.value = '2';
    memoryRows.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(page.querySelector('lm-settings-view')?.textContent).toContain(
      '32 channels on a page now: one row of 32, 16 pages; memories: two rows of 32, 2 pages',
    );

    await show('/');
    expect(page.querySelectorAll('lm-channel-section .row').length).toBe(1);
    expect(page.querySelectorAll('lm-channel-section lm-fader-strip').length).toBe(32);
    expect(page.querySelectorAll('lm-memory-section .row').length).toBe(2);
    expect(page.querySelectorAll('lm-memory-section lm-fader-strip').length).toBe(64);
    expect(button('Channels 33 to 64')).toBeTruthy();
    expect(button('Memories 65 to 128')).toBeTruthy();
    const main = page.querySelector<HTMLElement>('lm-main-view')!;
    expect(main.style.getPropertyValue('--channel-share')).toBe('1fr');
    expect(main.style.getPropertyValue('--memory-share')).toBe('2fr');

    // On a narrower window a row is 16 faders.
    store.windowStripsPerRow.set(16);
    await fixture.whenStable();
    expect(page.querySelectorAll('lm-channel-section lm-fader-strip').length).toBe(16);
  });

  it('the rows of the channels and of the memories scroll sideways each on their own, with a big scrollbar', () => {
    const channelRows = page.querySelector('lm-channel-section .rows');
    const memoryRows = page.querySelector('lm-memory-section .rows');
    expect(channelRows?.classList).toContain('big-scrollbar');
    expect(memoryRows?.classList).toContain('big-scrollbar');
    expect(page.querySelector('[lmScrollTogether]')).toBeNull();
  });

  it('Settings says how many keys of each kind the independents show', async () => {
    await show('/settings');
    const channels = page.querySelector<HTMLInputElement>('#channel-keys')!;
    const memories = page.querySelector<HTMLInputElement>('#memory-keys')!;
    expect([channels.value, memories.value]).toEqual(['10', '10']);

    channels.value = '3';
    channels.dispatchEvent(new Event('change'));
    memories.value = '99'; // more than there are: the number stays
    memories.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(memories.value).toBe('10');
    memories.value = '2';
    memories.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    await show('/');
    const keys = [...page.querySelectorAll('lm-independents button.number')];
    expect(keys.map((key) => key.getAttribute('aria-label'))).toEqual([
      'Independent 1, channel 1',
      'Independent 2, channel 2',
      'Independent 3, channel 3',
      'Independent 4, memory 1',
      'Independent 5, memory 2',
    ]);

    // 0 keys of one kind: only the other kind; 0 of both: no independents at all.
    await show('/settings');
    const channelsAgain = page.querySelector<HTMLInputElement>('#channel-keys')!;
    channelsAgain.value = '0';
    channelsAgain.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(channelsAgain.value).toBe('0');
    await show('/');
    expect(page.querySelectorAll('lm-independents button.number').length).toBe(2);

    await show('/settings');
    const memoriesAgain = page.querySelector<HTMLInputElement>('#memory-keys')!;
    memoriesAgain.value = '0';
    memoriesAgain.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    await show('/');
    expect(page.querySelector('lm-independents')).toBeNull();
    expect(page.querySelector('lm-main-view')?.classList).toContain('no-independents');
  });

  it('the lettering of the whole page gets bigger and smaller in steps of 0.1', async () => {
    await show('/settings');
    const scale = () => document.documentElement.style.getPropertyValue('--font-scale');
    expect(page.querySelector('.scale output')?.textContent?.trim()).toBe('×1.0');

    await press(button('Bigger lettering'));
    await press(button('Bigger lettering'));
    expect(scale()).toBe('1.2');
    expect(page.querySelector('.scale output')?.textContent?.trim()).toBe('×1.2');

    await press(button('Reset'));
    await press(button('Smaller lettering'));
    expect(scale()).toBe('0.9');
  });

  /** A tab of the Keyboard view, by the kind of keys it holds. */
  function tab(group: string): HTMLButtonElement {
    const found = [...page.querySelectorAll<HTMLButtonElement>('[role="tab"]')].find(
      (candidate) => candidate.firstChild?.textContent?.trim() === group,
    );
    if (!found) throw new Error(`no tab "${group}"`);
    return found;
  }

  it('Keyboard has a tab for each kind of keys with its number of keys; a search says how many it finds', async () => {
    await show('/keyboard');
    const total = (group: string) => DESK_ACTIONS.filter((action) => action.group === group).length;
    const count = (group: string) => tab(group).querySelector('.count')?.textContent?.trim();
    const rows = () => [...page.querySelectorAll('#keys-list tbody th')].map((cell) => cell.textContent?.trim());

    expect(page.querySelectorAll('[role="tab"]').length).toBe(ACTION_GROUPS.length);
    expect(tab('Views').getAttribute('aria-selected')).toBe('true');
    expect(count('Master')).toBe(String(total('Master')));
    expect(rows().length).toBe(total('Views'));

    await press(tab('Master'));
    expect(tab('Master').getAttribute('aria-selected')).toBe('true');
    expect(rows()).toEqual(['Blackout']);
    await press(tab('General'));
    expect(rows()).toEqual(['Clear', 'Solo up', 'Solo down', 'Rate']);

    // The arrow keys go from tab to tab.
    tab('General').dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(tab('Stack').getAttribute('aria-selected')).toBe('true');

    // A search: each tab says how many of its keys it finds; one that finds none in the tab
    // shown shows the first tab where it finds some.
    const search = page.querySelector<HTMLInputElement>('input[type="search"]')!;
    search.value = 'black';
    search.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(count('Master')).toBe(`1/${total('Master')}`);
    expect(count('Stack')).toBe(`0/${total('Stack')}`);
    expect(tab('Stack').classList).toContain('none-found');
    expect(tab('Master').getAttribute('aria-selected')).toBe('true');
    expect(rows()).toEqual(['Blackout']);

    search.value = '';
    search.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(count('Master')).toBe(String(total('Master')));
    expect(tab('Stack').getAttribute('aria-selected')).toBe('true'); // the tab chosen before
  });

  it('a shortcut is recorded for a key, and then presses that key', async () => {
    await show('/keyboard');
    await press(tab('Master'));
    await press(button('Set the shortcut of Blackout'));
    expect(page.querySelector('tr[data-row="master.blackout"] .waiting')).toBeTruthy();

    // Ctrl alone is held, not a shortcut; Ctrl with B is.
    document.dispatchEvent(new KeyboardEvent('keydown', { code: 'ControlLeft', key: 'Control', ctrlKey: true, bubbles: true }));
    document.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyB', key: 'b', ctrlKey: true, bubbles: true }));
    await fixture.whenStable();
    expect(page.querySelector('tr[data-row="master.blackout"] td.keys')?.textContent?.trim()).toBe('Ctrl + B');

    await show('/');
    document.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyB', key: 'b', ctrlKey: true, bubbles: true, cancelable: true }));
    await fixture.whenStable();
    expect(connection.last('SetBlackout')).toEqual([true]);

    // While a note is typed, the keys are the note's.
    const note = page.querySelector<HTMLInputElement>('input[aria-label="Note for Channel 1"]')!;
    note.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyB', key: 'b', ctrlKey: true, bubbles: true }));
    await fixture.whenStable();
    expect(connection.methods().filter((method) => method === 'SetBlackout').length).toBe(1);

    // The same shortcut for another key moves over to it.
    await show('/keyboard');
    await press(tab('General'));
    await press(button('Set the shortcut of Clear'));
    document.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyB', key: 'b', ctrlKey: true, bubbles: true }));
    await fixture.whenStable();
    expect(page.querySelector('tr[data-row="master.clear"] td.keys')?.textContent?.trim()).toBe('Ctrl + B');
    await press(tab('Master'));
    expect(page.querySelector('tr[data-row="master.blackout"] td.keys')?.textContent?.trim()).toBe('');
    expect(page.textContent).toContain('That shortcut was the one of Blackout before');
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
    await show('/keyboard');
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
    expect(router.url).toBe('/keyboard');
    expect(page.querySelector('tr[data-row="stage.edit"]')?.classList.contains('picked')).toBe(true);
    // Only the list scrolls to the row; the page itself stays where it is, the top bar in sight.
    expect(document.scrollingElement?.scrollTop ?? 0).toBe(0);
    await later();
    expect(router.url).toBe('/keyboard'); // the picked key was not pressed after all

    // A locked key is picked as well: Play, while the stack is not live. It gets no click.
    await press(button('Listen'));
    const play = page.querySelector('[data-action="stack.play"]')!;
    for (let press = 0; press < 2; press++) {
      play.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true }));
      play.dispatchEvent(new MouseEvent('pointerup', { bubbles: true }));
    }
    await fixture.whenStable();
    expect(page.querySelector('tr[data-row="stack.play"]')?.classList.contains('picked')).toBe(true);
    await new Promise((resolve) => setTimeout(resolve, 0));

    // Enter on a key (a click without a pointer) picks it at once.
    await press(button('Listen'));
    page.querySelector<HTMLButtonElement>('[data-action="master.blackout"]')!.click();
    await fixture.whenStable();
    expect(page.querySelector('tr[data-row="master.blackout"]')?.classList.contains('picked')).toBe(true);

    // Esc stops listening.
    await press(button('Listen'));
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', code: 'Escape', bubbles: true }));
    await fixture.whenStable();
    expect(page.querySelector('.listening')).toBeNull();
  });

  it('every key on the page that is marked for shortcuts is one the list of the Keyboard view knows', async () => {
    for (const view of ['/', '/stack', '/sequences', '/memories', '/stage', '/settings', '/keyboard']) {
      await show(view);
      for (const element of page.querySelectorAll('[data-action]')) {
        expect(actionWithId(element.getAttribute('data-action') ?? ''), element.getAttribute('data-action') ?? '').toBeTruthy();
      }
    }
  });

  it('the LAN address and password are changed in Settings while LAN is on', async () => {
    await show('/settings');
    expect(page.querySelector('lm-settings-view')?.textContent).toContain('LAN is off');

    connection.emit('LanAccessChanged', {
      local: true,
      on: true,
      addresses: ['192.168.1.20:5080', '10.0.0.5:5080'],
      password: 'K7M2Q',
      chosen: null,
    } satisfies LanAccess);
    await fixture.whenStable();

    const address = page.querySelector<HTMLSelectElement>('#lan-address')!;
    expect([...address.options].map((option) => option.textContent?.trim())).toEqual([
      'Automatic (192.168.1.20:5080)',
      '192.168.1.20:5080',
      '10.0.0.5:5080',
    ]);
    address.value = '10.0.0.5';
    address.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetLanAddress')).toEqual(['10.0.0.5']);

    const password = page.querySelector<HTMLInputElement>('#lan-password')!;
    password.value = 'show night 7';
    await press(button('Set'));
    expect(connection.last('SetLanPassword')).toEqual(['show night 7']);
    expect(page.querySelector('lm-settings-view .done')?.textContent).toContain('The new password is set');

    await press(button('New random'));
    expect(connection.methods()).toContain('NewLanPassword');
  });
});
