import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';
import { ACTION_GROUPS, DESK_ACTIONS, actionWithId } from './desk/desk-actions';
import { DeskConnection } from './desk/desk-connection';
import { DeskSettings, SETTINGS_STORAGE_KEY } from './desk/desk-settings';
import { DeskStore } from './desk/desk-store';
import {
  AutosaveOptions,
  BoxView,
  DeskState,
  UNIVERSE_SIZE,
  EMPTY_STAGE,
  FoundNode,
  ImportAnswer,
  LanAccess,
  MEMORY_COUNT,
  NetworkChoice,
  OutputView,
  Playback,
  ProjectList,
  Sequence,
  StackPlayback,
  StackState,
  StageLight,
  StagePlan,
  TimedStep,
  Welcome,
} from './desk/desk-types';
import { Effect } from './desk/effects';
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
      channels: new Array<number>(UNIVERSE_SIZE).fill(0),
      memoryLevels: new Array<number>(MEMORY_COUNT).fill(0),
      memoryStored: new Array<boolean>(MEMORY_COUNT).fill(false),
      channelNotes: new Array<string>(UNIVERSE_SIZE).fill(''),
      memoryNotes: new Array<string>(MEMORY_COUNT).fill(''),
      independents: Array.from({ length: 10 }, (_, index) => index + 1),
      memoryIndependents: Array.from({ length: 10 }, (_, index) => index + 1),
      master: 255,
      blackout: false,
    },
    boxes: [],
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

  /** The notes of the project, as the backend answers "GetNotes": none at first. */
  notes = '';
  /** The open project as its file says it, as the backend answers "ExportProject". */
  projectFile = JSON.stringify({
    format: 'lumenet-project',
    version: 4,
    notes: '<b>Doors at 7</b>',
    memories: [],
    stage: { width: 12, depth: 8, lights: [{ name: 'Wash', address: 1, channels: 'RGB', x: 2, y: 2 }] },
  });
  /** The projects and the examples, as the backend answers "GetProjects". */
  projectList: ProjectList = { folder: 'C:\\Lumenet', projects: [], examples: ['1 Memories and the stack', '6 Concert'] };
  /** What the backend answers to "GetRecovered": when unsaved work was brought back, or null. */
  recovered: string | null = null;
  /** The autosave of the desk program, as the backend answers "GetAutosave" (on the PC). */
  autosave: AutosaveOptions = { on: true, seconds: 5 };
  /** The stage plan, as the backend answers "GetStage": no lights at first. */
  stagePlan: StagePlan = { ...EMPTY_STAGE };
  /** The picture under the plan, as the backend answers "GetStagePicture". */
  stagePicture: string | null = null;
  /** The Art-Net nodes that answer the search of the Outputs window ("FindArtNetNodes"). */
  foundNodes: FoundNode[] = [];
  /** The serial ports and the networks of the PC, as the backend answers "GetSerialPorts" and "GetNetworks". */
  serialPorts = ['COM3', 'COM7'];
  networks: NetworkChoice[] = [{ network: '192.168.1.0/24', address: '192.168.1.20', adapter: 'Wi-Fi', router: true }];
  /** The effects of the project, as the backend answers "GetEffects": none at first. */
  effects: Effect[] = [];

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
    if (method === 'GetNotes') return this.notes;
    if (method === 'SetNotes') {
      this.notes = args[0] as string;
      return undefined;
    }
    if (method === 'ExportProject') return this.projectFile;
    if (method === 'ImportProjectFile') return { name: args[0], path: null, unsaved: true };
    if (method === 'GetProjects') return this.projectList;
    if (method === 'LoadExample') return { name: args[0], path: null, unsaved: false };
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
    if (method === 'FindArtNetNodes') return this.foundNodes;
    if (method === 'GetSerialPorts') return this.serialPorts;
    if (method === 'GetNetworks') return this.networks;
    if (method === 'GetEffects') return this.effects;
    if (method === 'PreviewEffect') return null;
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
  'GetNotes',
  'GetAutosave',
  'GetStage',
  'GetStagePicture',
  'GetSerialPorts',
  'GetNetworks',
  'GetEffects',
  'PreviewEffect',
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
    const look = new Array<number>(UNIVERSE_SIZE).fill(0);
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

  /** A button inside one part of the page, a window for example, by its name or what it says. */
  function buttonIn(container: string, label: string): HTMLButtonElement {
    const found = [...page.querySelectorAll<HTMLButtonElement>(`${container} button`)].find(
      (candidate) =>
        (candidate.getAttribute('aria-label') ?? candidate.textContent?.trim()) === label,
    );
    if (!found) throw new Error(`no button "${label}" in ${container}`);
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
    return page.querySelector<HTMLButtonElement>(`button[aria-label^="Channel ${channel} (U1) key"]`);
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

  /** Checks (or unchecks) a part in the window of Import or Export, by what its label starts with. */
  async function check(dialog: 'lm-import-dialog' | 'lm-export-dialog', label: string): Promise<void> {
    const box = [...page.querySelectorAll<HTMLLabelElement>(`${dialog} .parts label`)]
      .find((item) => item.textContent?.trim().startsWith(label))
      ?.querySelector('input');
    if (!box) throw new Error(`no checkbox "${label}"`);
    box.click();
    await fixture.whenStable();
  }

  /** Whether a part in the window of Import or Export is checked. */
  function checked(dialog: 'lm-import-dialog' | 'lm-export-dialog', label: string): boolean {
    return [...page.querySelectorAll<HTMLLabelElement>(`${dialog} .parts label`)]
      .find((item) => item.textContent?.trim().startsWith(label))!
      .querySelector('input')!.checked;
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
    // record keys, the general section, and the independents last
    const side = page.querySelector('lm-main-view > .side');
    expect([...(side?.children ?? [])].map((part) => part.tagName.toLowerCase())).toEqual([
      'lm-stack-section',
      'div',
      'lm-general-section',
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
    await press(button('Edit the channels, memories and effects of the independent keys'));
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

    await press(button('Edit the channels, memories and effects of the independent keys'));
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
      expect(page.querySelectorAll('lm-note-lines')[3].getAttribute('title')).toBe('Chase blue (double click to change it)');
      expect(page.querySelectorAll('.keys input').length).toBe(0); // lettering, until a double click
    }
  });

  it('a double click on the note of a memory key in Memories and Sequences changes it there', async () => {
    for (const view of ['/memories', '/sequences']) {
      await show(view);
      const note = () => page.querySelectorAll<HTMLElement>('.keys lm-note-lines')[3];
      const doubleClick = async (target: Element) => {
        target.dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
        await fixture.whenStable();
      };

      // the second line was double-clicked: two fields, the second one to type in
      await doubleClick(note().querySelectorAll('.line')[1]);
      const fields = [...note().querySelectorAll<HTMLInputElement>('input')];
      expect(fields.map((field) => field.getAttribute('aria-label'))).toEqual(['Note for memory 4', 'Note for memory 4, second line']);
      expect(document.activeElement).toBe(fields[1]);
      fields[0].value = 'Chase';
      fields[1].value = view === '/memories' ? 'red' : 'green';
      fields[1].dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
      await fixture.whenStable();
      expect(note().querySelectorAll('input').length).toBe(0);
      expect(store.memoryNotes()[3]).toBe('Chase' + NOTE_LINE_SEPARATOR + fields[1].value);
      expect(connection.calls).toContainEqual({ method: 'SetMemoryNote', args: [4, 'Chase' + NOTE_LINE_SEPARATOR + fields[1].value] });

      // Escape leaves the note as it was
      await doubleClick(note());
      note().querySelector('input')!.value = 'Gone';
      note().querySelector('input')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
      await fixture.whenStable();
      expect(store.memoryNotes()[3]).toBe('Chase' + NOTE_LINE_SEPARATOR + fields[1].value);
    }

    // While the stack is live nothing that is stored changes: no fields
    await stackStands({ ...connection.playback.stack, live: true });
    await show('/memories');
    page.querySelectorAll('.keys lm-note-lines')[3].dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
    await fixture.whenStable();
    expect(page.querySelectorAll('.keys input').length).toBe(0);
  });

  it('edit lets the channel of an independent key be changed', async () => {
    await press(button('Edit the channels, memories and effects of the independent keys'));
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

    await press(button('Edit the channels, memories and effects of the independent keys'));
    await press(button('Independent 2, channel 40 (U1)'));
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

  it('the outputs, LAN, Undo and Redo stand at the top left; New, Save, Load, Import and Export at the right', () => {
    const labels = (selector: string) =>
      [...page.querySelectorAll(`lm-top-bar ${selector} button`)].map((key) => key.getAttribute('aria-label') ?? key.textContent?.trim());
    expect(labels('.left')).toEqual(['LAN', 'Undo', 'Redo']);
    expect(textOf('lm-top-bar .left lm-outputs-status')).toBe('Outputs connected: 0'); // a line of text: Settings has the outputs
    expect(labels('.project')).toEqual(['New project', 'Save project', 'Load project', 'Import', 'Export']);
  });

  // ---------------------------------------------------------------- universes and boxes

  /** The desk as the backend sends it for a project of two universes: 1024 channels. */
  function twoUniverses(): DeskState {
    return {
      ...connection.welcome.desk,
      universes: 2,
      channels: new Array<number>(2 * UNIVERSE_SIZE).fill(0),
      channelNotes: new Array<string>(2 * UNIVERSE_SIZE).fill(''),
    };
  }

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
    await press(button('Edit the channels, memories and effects of the independent keys'));
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
    await press(button('Edit the channels, memories and effects of the independent keys'));

    // back to one universe: the U keys go, and the faders show U1 again
    connection.emit('DeskChanged', connection.welcome.desk);
    await fixture.whenStable();
    expect(page.querySelector('nav[aria-label="Universes"]')).toBeNull();
    expect(page.querySelector('lm-fader[aria-label="Channel 449 (U1)"]')).toBeTruthy();
  });

  it('Settings sets the universes of the project', async () => {
    await show('/settings');
    const universes = page.querySelector<HTMLSelectElement>('#universes')!;
    expect(universes.value).toBe('1');
    universes.value = '3';
    universes.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetUniverses')).toEqual([3]);
    expect(universes.value).toBe('1'); // until the desk program has taken it: it may refuse
    connection.emit('DeskChanged', { ...twoUniverses(), universes: 3, channels: new Array<number>(1536).fill(0) });
    await fixture.whenStable();
    expect(universes.value).toBe('3');
    expect(page.querySelector('#addresses')).toBeNull(); // the channels are always 1 to 512 in each
  });

  it('the kind of a step says the universe of its channel, and the number list holds that universe only', async () => {
    connection.emit('DeskChanged', twoUniverses());
    await fixture.whenStable();
    await show('/sequences');
    await press(button('Work on memory 3'));
    const row = () => page.querySelectorAll('lm-sequences-view table.steps tbody tr')[0];
    const kind = () => row().querySelector<HTMLSelectElement>('select[aria-label="Step 1, channel, memory, fixture or group"]')!;
    const number = () => row().querySelector<HTMLSelectElement>('select[aria-label="Step 1, number of the channel"]')!;
    expect([...kind().options].map((option) => option.text.trim())).toEqual([
      'Channel (U1)',
      'Channel (U2)',
      'Memory',
      'Fixture',
      'Group',
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

  /** Chooses a value of a list in the page and says so, as a person does. */
  async function choose(selector: string, value: string): Promise<void> {
    const list = page.querySelector<HTMLSelectElement>(selector);
    if (!list) throw new Error(`no list ${selector}`);
    list.value = value;
    list.dispatchEvent(new Event('change'));
    await fixture.whenStable();
  }

  it('the top left counts the boxes, and the Outputs panel of Settings says which universe each output of a box plays', async () => {
    const boxes: BoxView[] = [
      { address: '192.168.7.1', connected: true, firmware: '1.0', outputs: 1, universes: [1], kind: 'automatic' },
      { address: '10.1.3.1', connected: true, firmware: '1.0', outputs: 2, universes: [0, 0], kind: 'automatic' },
    ];
    connection.emit('BoxesChanged', boxes);
    await fixture.whenStable();
    expect(textOf('lm-outputs-status')).toBe('Outputs connected: 2');
    await show('/settings');
    const rows = () => [...page.querySelectorAll('lm-outputs-panel tbody tr')];
    expect(rows().map((row) => row.querySelector('.name')?.textContent?.trim())).toEqual(['dmxbox', 'dmxbox 2', 'dmxbox 2, output 2']);
    expect(rows()[1].querySelector('th')?.textContent).toContain('10.1.3.1, switch 3, firmware 1.0');
    expect(rows()[2].textContent).toContain('Output 2');
    expect(textOf('lm-outputs-panel .remark')).toBeUndefined(); // U1, the only universe, is played

    // The second output of the second box plays U1 as well: the boxes become the project's list.
    await choose('select[aria-label="Universe of output 2 of dmxbox 10.1.3.1"]', '1');
    expect(connection.last('SetBoxes')).toEqual([
      [
        { address: '192.168.7.1', universes: [1] },
        { address: '10.1.3.1', universes: [0, 1] },
      ],
    ]);

    // A box added by hand, before it is there.
    connection.emit('BoxesChanged', [
      { ...boxes[0], kind: 'listed' },
      { ...boxes[1], universes: [0, 1], kind: 'listed' },
    ]);
    await fixture.whenStable();
    await choose('#output-kind', 'dmxbox');
    const address = page.querySelector<HTMLInputElement>('lm-outputs-panel form.add input[aria-label="Address"]')!;
    address.value = '10.1.4.1';
    address.dispatchEvent(new Event('input'));
    page.querySelector('lm-outputs-panel form.add')!.dispatchEvent(new Event('submit'));
    await until(() => connection.methods().filter((method) => method === 'SetBoxes').length === 2);
    expect(connection.last('SetBoxes')).toEqual([
      [
        { address: '192.168.7.1', universes: [1] },
        { address: '10.1.3.1', universes: [0, 1] },
        { address: '10.1.4.1', universes: [0] },
      ],
    ]);

    // It does not answer: the top left counts it, and its light is amber. Auto empties the list.
    connection.emit('BoxesChanged', [
      { ...boxes[0], kind: 'listed' },
      { ...boxes[1], universes: [0, 1], kind: 'listed' },
      { address: '10.1.4.1', connected: false, firmware: null, outputs: 1, universes: [0], kind: 'listed' },
    ]);
    await fixture.whenStable();
    expect(textOf('lm-outputs-status')).toBe('Outputs connected: 2 of 3');
    expect(page.querySelector('lm-outputs-status p')?.className).toContain('missing');
    expect(rows()[3].querySelector('.state')?.textContent?.trim()).toBe('Not answering');
    expect(textOf('lm-outputs-panel .remark')).toBe('⚠ U1 goes out twice, on dmxbox 192.168.7.1 and dmxbox 10.1.3.1, as a splitter would give it.');
    await press(button('Automatic: the boxes play by themselves'));
    expect(connection.last('SetBoxes')).toEqual([[]]);
  });

  it('a page loaded at Settings asks for the networks and the nodes once it is connected, not before', async () => {
    connection.online.set(false);
    await show('/settings');
    const asked = (method: string) => connection.calls.filter((call) => call.method === method).length;
    expect(asked('FindArtNetNodes')).toBe(0);
    expect(asked('GetNetworks')).toBe(0);

    connection.online.set(true);
    await fixture.whenStable();
    expect(asked('FindArtNetNodes')).toBe(1);
    expect(asked('GetNetworks')).toBe(1);
    expect(page.querySelector('lm-outputs-panel .error')).toBeNull();
  });

  it('the Outputs panel adds an Art-Net node the search found, sACN and a USB interface, and the top left counts them', async () => {
    connection.foundNodes = [
      { ip: '10.0.0.7', shortName: 'Node 2', longName: 'A node', firmware: '1.2', ports: [0, 1], dmxbox: false },
      { ip: '192.168.7.1', shortName: 'dmxbox', longName: 'dmxbox', firmware: '1.0', ports: [0], dmxbox: true },
    ];
    await show('/settings');
    // the panel asks for the networks, the serial ports and the nodes at once
    await until(() => page.querySelectorAll('lm-outputs-panel .found li').length === 2);
    expect(connection.calls.map((call) => call.method)).toEqual(expect.arrayContaining(['GetNetworks', 'GetSerialPorts', 'FindArtNetNodes']));

    // A dmxbox cannot be added as a node, the other one can.
    expect(page.querySelectorAll('lm-outputs-panel .found li')[1].textContent).toContain('Already played as a dmxbox');
    await press(button('Add Node 2 at 10.0.0.7'));
    const node = {
      kind: 'artnet',
      target: '10.0.0.7',
      ports: [
        { universe: 1, address: 0 },
        { universe: 0, address: 1 },
      ],
      network: '',
      priority: 100,
    };
    expect(connection.last('SetOutputs')).toEqual([[node]]);

    // The desk program answers with the output and its state; the Net of the first port changes.
    const outputs: OutputView[] = [{ plan: node as OutputView['plan'], state: 'connected', detail: 'Node 2, firmware 1.2' }];
    connection.emit('OutputsChanged', outputs);
    await fixture.whenStable();
    // a line for each port, the node by its name, the kind and the state on its first line
    const rows = [...page.querySelectorAll('lm-outputs-panel tbody tr')];
    expect(rows.map((row) => row.querySelector('.name')?.textContent?.trim())).toEqual(['Node 2', 'Node 2, port 2']);
    expect(rows[0].querySelector('th small')?.textContent?.trim()).toBe('10.0.0.7, Art-Net');
    expect(rows[0].querySelector('.kind')?.textContent?.trim()).toBe('Art-Net');
    expect(rows[0].querySelector('.state')?.textContent?.trim()).toBe('Connected');
    const net = page.querySelector<HTMLInputElement>('input[aria-label="Net of port 1 of Node 2"]')!;
    net.value = '1';
    net.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect((connection.last('SetOutputs')?.[0] as { ports: unknown }[])[0].ports).toEqual([
      { universe: 1, address: 256 },
      { universe: 0, address: 1 },
    ]);

    // sACN by multicast, and a USB interface at the first free port.
    const add = async (kind: string) => {
      await choose('#output-kind', kind);
      page.querySelector('lm-outputs-panel form.add')!.dispatchEvent(new Event('submit'));
      await fixture.whenStable();
      await wait(0);
    };
    await add('multicast');
    expect((connection.last('SetOutputs')?.[0] as unknown[])[1]).toEqual({
      kind: 'sacn',
      target: 'multicast',
      ports: [{ universe: 1, address: 1 }],
      network: '',
      priority: 100,
    });
    await add('usb');
    expect((connection.last('SetOutputs')?.[0] as { target: string }[])[1].target).toBe('COM3');

    // The top left counts the outputs that play, and a node that does not answer turns it amber.
    connection.emit('OutputsChanged', [
      outputs[0],
      { plan: { kind: 'sacn', target: 'multicast', ports: [{ universe: 1, address: 1 }], network: '', priority: 100 }, state: 'sending', detail: '' },
      { plan: { kind: 'artnet', target: '10.0.0.9', ports: [{ universe: 1, address: 0 }], network: '', priority: 100 }, state: 'missing', detail: '' },
    ]);
    await fixture.whenStable();
    expect(textOf('lm-outputs-status')).toBe('Outputs connected: 2 of 3');
    expect(page.querySelector('lm-outputs-status p')?.className).toContain('missing');
    expect(page.querySelector('lm-outputs-status p')?.getAttribute('title')).toContain('Art-Net 10.0.0.9: not answering (U1 at 0:0:0)');
    expect(textOf('lm-outputs-panel .remark')).toBe(
      '⚠ U1 goes out 3 times, on Art-Net 10.0.0.7, sACN multicast and Art-Net 10.0.0.9, as a splitter would give it.',
    );
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

  it('at the right of Main: STACK across the top, under it MASTER over RECORD, GENERAL (CLEAR, SOLO UP, SOLO DOWN, RATE), then IND; SOLO and PAUSE are white', async () => {
    const side = [...(page.querySelector('lm-main-view > .side')?.children ?? [])].map((child) => child.localName);
    expect(side).toEqual(['lm-stack-section', 'div', 'lm-general-section', 'lm-independents']);
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
      'Effects',
      'Notes',
      'Settings',
    ]); // Keyboard is a panel of Settings
    expect(box('Settings').checked).toBe(true);
    expect(box('Settings').disabled).toBe(true);

    box('Stage').click();
    box('Main').click();
    await fixture.whenStable();
    expect(links()).toEqual(['Memories', 'Sequences', 'Stack', 'Fixtures', 'Effects', 'Notes', 'Settings']);
    expect(JSON.parse(localStorage.getItem(SETTINGS_STORAGE_KEY)!)).toMatchObject({ hiddenViews: ['main', 'stage'] }); // kept on this device

    box('Stage').click();
    await fixture.whenStable();
    expect(links()).toContain('Stage');

    // Stage to the front
    for (let place = 0; place < 4; place++) await press(button('Move Stage up'));
    expect(links()).toEqual(['Stage', 'Memories', 'Sequences', 'Stack', 'Fixtures', 'Effects', 'Notes', 'Settings']);

    // a desk that starts at Main, while Main is left out, opens on the first view in the top bar
    const router = TestBed.inject(Router);
    TestBed.createComponent(App);
    await router.navigateByUrl('/');
    await fixture.whenStable();
    await until(() => router.url === '/stage');
  });

  it('Settings puts the views in another order with ▲ and ▼, and ↺ brings back the usual ones', async () => {
    const usual = ['Main', 'Memories', 'Sequences', 'Stack', 'Stage', 'Fixtures', 'Effects', 'Notes', 'Settings'];
    const links = () => [...page.querySelectorAll('nav[aria-label="Views"] a')].map((link) => link.textContent?.trim().split(/\s/)[0]);
    const listed = () => [...page.querySelectorAll('ul.views label')].map((label) => label.textContent?.trim().split(/\s/)[0]);
    await show('/settings');
    expect(button('The usual views').disabled).toBe(true);
    expect(button('Move Main up').disabled).toBe(true); // the first
    expect(button('Move Settings down').disabled).toBe(true); // the last

    // One place earlier, in Settings and in the top bar; the keyboard focus goes with it.
    await press(button('Move Settings up'));
    const moved = ['Main', 'Memories', 'Sequences', 'Stack', 'Stage', 'Fixtures', 'Effects', 'Settings', 'Notes'];
    expect(listed()).toEqual(moved);
    expect(links()).toEqual(moved);
    expect(document.activeElement).toBe(button('Move Settings up'));
    expect(JSON.parse(localStorage.getItem(SETTINGS_STORAGE_KEY)!)).toMatchObject({ viewOrder: moved.map((name) => name.toLowerCase()) });
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

  it('the settings and the shortcuts belong to this device, on the PC as on a phone', async () => {
    await show('/settings');
    expect(textOf('lm-settings-view .summary')).toContain('The settings and the keyboard shortcuts belong to this device');
    const rows = page.querySelector<HTMLSelectElement>('#channel-rows')!;
    rows.value = '1';
    rows.dispatchEvent(new Event('change'));
    await press(button('Zoom in'));
    expect(JSON.parse(localStorage.getItem(SETTINGS_STORAGE_KEY)!)).toMatchObject({ channelRows: 1, zoom: 1.1 });

    // Nothing of them goes to the desk program, and a project that is loaded leaves them as they are.
    expect(connection.methods().filter((method) => method.includes('Settings'))).toEqual([]);
    connection.emit('DeskChanged', connection.welcome.desk);
    await fixture.whenStable();
    expect(TestBed.inject(DeskSettings).channelRows()).toBe(1);
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

  it('the color of a kind of keys or of one key is set in Settings, and kept on this device', async () => {
    const shown = (variable: string) => document.documentElement.style.getPropertyValue(variable);
    const sentColors = () => (JSON.parse(localStorage.getItem(SETTINGS_STORAGE_KEY) ?? '{}') as { colors?: object }).colors;
    await show('/settings');

    // A press on a color offers the colors of the desk; one of them is taken, and they close.
    const pause = button('Color of Pause');
    expect(pause.getAttribute('aria-expanded')).toBe('false');
    await press(pause);
    expect(pause.getAttribute('aria-expanded')).toBe('true');
    const offered = [...page.querySelectorAll('.palette button')].map((swatch) => swatch.getAttribute('aria-label'));
    expect(offered).toEqual(['Green', 'Red', 'Yellow', 'Amber', 'White', 'Blue', 'Violet']);
    expect(button('White').getAttribute('aria-current')).toBe('true'); // the one it has
    await press(button('Red'));
    expect(shown('--key-pause')).toBe('#ff5247');
    expect(sentColors()).toEqual({ pause: '#ff5247' });
    expect(page.querySelector('.palette')).toBeNull();
    expect(button('The usual color for Pause').disabled).toBe(false);

    // Any other color: while it is picked, the desk shows it at once; when the picker closes, it is kept.
    await press(button('Color of Pause'));
    await press(page.querySelector<HTMLElement>('.palette .other')); // a press in there leaves it open
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

    // A press anywhere else closes the palette, and so does Esc, with the keyboard back on the color.
    const channels = button('Color of Channels: their keys, pages, independent keys and faders');
    await press(channels);
    await press(document.body);
    expect(page.querySelector('.palette')).toBeNull();
    expect(channels.getAttribute('aria-expanded')).toBe('false');
    await press(channels);
    button('Green').focus();
    button('Green').dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    await fixture.whenStable();
    expect(page.querySelector('.palette')).toBeNull();
    expect(document.activeElement).toBe(channels);
    expect(sentColors()).toEqual({ pause: '#ff00ff', channel: '#4aa3ff' }); // nothing taken

    // Back to the usual colors: one, then all with ↺ beside the title.
    await press(button('The usual color for Pause'));
    expect(shown('--key-pause')).toBe('');
    expect(sentColors()).toEqual({ channel: '#4aa3ff' });
    await press(button('All the usual colors'));
    expect(shown('--channel')).toBe('');
    expect(sentColors()).toEqual({});
    expect(button('All the usual colors').disabled).toBe(true);

    // Settings from a file (Import) bring theirs; what makes no sense is left out.
    TestBed.inject(DeskSettings).takeSettings({ colors: { play: '#123456', nonsense: '#000000', next: 'red' } });
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
    expect(textOf('lm-stage-panel .facts')).toBe('RGB, dimmer first, on channels 11 to 14 (U1)');
    expect(page.querySelector('#light-name')).toBeNull(); // out of Edit nothing about it can be changed...
    const faders = [...page.querySelectorAll<HTMLElement>('lm-light-faders lm-fader')];
    expect(faders.map((fader) => fader.getAttribute('aria-label'))).toEqual([
      'Channel 11 (U1), dimmer',
      'Channel 12 (U1), red',
      'Channel 13 (U1), green',
      'Channel 14 (U1), blue',
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

  it('a double click puts the faders of a light to full and back; Clear does what Clear on Main does', async () => {
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

    // Clear: the key of the main view, the same action: every channel fader and memory fader to zero.
    twice(1);
    twice(2);
    store.setChannel(30, 99);
    store.setMemoryLevel(2, 120);
    await fixture.whenStable();
    const clear = page.querySelector<HTMLButtonElement>('lm-stage-view [data-action="master.clear"]')!;
    expect(clear.textContent?.trim()).toBe('Clear');
    await press(clear);
    expect(store.channels().slice(10, 15)).toEqual([0, 0, 0, 0, 0]);
    expect(store.channels()[4]).toBe(0);
    expect(store.channels()[29]).toBe(0); // a channel that no light uses goes down too, as on Main
    expect(store.memoryLevels()[1]).toBe(0);
    expect(connection.methods()).toContain('Clear');
  });

  it('the Fixtures view plays the lights of the plan: a fader each, a color, and groups that move all their lights', async () => {
    await planWith(stageLight(1, 'Par', 11, 'RGB', null, 2, 8), stageLight(2, 'Front', 5, 'D', null, 8, 5));
    await show('/fixtures');
    const strip = (name: string) =>
      [...page.querySelectorAll('lm-fixture-strip')].find((item) => item.querySelector('.swatch')?.getAttribute('aria-label') === `${name}: color and settings`)!;
    // under each key the note, as under a channel fader: where nothing was typed, the name from the stage, dimmed
    const notes = () => [...page.querySelectorAll<HTMLInputElement>('lm-fixture-strip .note input:first-child')];
    expect(notes().map((field) => [field.placeholder, field.value])).toEqual([
      ['Front', ''],
      ['Par', ''],
    ]);

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

    // A note typed under a key belongs to that fixture or group, with the stage plan; the name stays.
    const write = async (name: string, line: 0 | 1, text: string) => {
      const field = strip(name).querySelectorAll<HTMLInputElement>('.note input')[line];
      field.value = text;
      field.dispatchEvent(new Event('change'));
      await fixture.whenStable();
    };
    await write('Par', 1, 'stage left');
    await write('Group 1', 0, 'All');
    expect(connection.stagePlan.lights.find((light) => light.id === 1)).toMatchObject({ name: 'Par', note: NOTE_LINE_SEPARATOR + 'stage left' });
    expect(connection.stagePlan.groups[0]).toMatchObject({ name: 'Group 1', note: 'All' });
    expect([...strip('Par').querySelectorAll<HTMLInputElement>('.note input')].map((field) => field.value)).toEqual(['', 'stage left']);
    expect(strip('Par').querySelector('.note')?.getAttribute('title')).toBe('stage left');

    // While the stack is live a note is lettering only.
    await stackStands({ ...connection.playback.stack, live: true });
    expect(notes().every((field) => field.readOnly)).toBe(true);
  });

  it('a light that strobes flashes on the Stage view, never more than three times a second, with a mark of how fast', async () => {
    const color = (name: string, code: string) => ({ name, code, fine: null, ranges: [] });
    const strobe = {
      name: 'Strobe',
      code: 'S',
      fine: null,
      ranges: [
        { from: 0, to: 31, label: 'Closed', color: null },
        { from: 32, to: 63, label: 'Open', color: null },
        { from: 64, to: 255, label: 'Strobe, 1Hz to 20Hz', color: null },
      ],
    };
    connection.emit('StageChanged', {
      ...EMPTY_STAGE,
      lights: [{ ...stageLight(1, 'Bar', 1, 'RGBS', null, 2, 2), type: 1 }, stageLight(2, 'Par', 11, 'RGBS', null, 6, 2)],
      types: [{ id: 1, maker: 'Maker', model: 'Bar', mode: '4-channel', source: 'maker/bar', channels: [color('Red', 'R'), color('Green', 'G'), color('Blue', 'B'), strobe] }],
    } satisfies StagePlan);
    await show('/stage');
    const body = (id: number) => page.querySelector<HTMLElement>(`[data-light="${id}"] .fixture`)!;
    const mark = (id: number) => page.querySelector(`[data-light="${id}"] .strobe-mark`)?.textContent?.trim();

    // red with its shutter open: steady light
    store.setChannel(1, 255);
    store.setChannel(4, 40);
    await fixture.whenStable();
    expect(body(1).classList).not.toContain('strobe');
    expect(mark(1)).toBeUndefined();

    // a strobe of 20 a second: the screen flashes 3 times a second, and the mark says the 20
    store.setChannel(4, 255);
    await fixture.whenStable();
    expect(body(1).classList).toContain('strobe');
    expect(body(1).dataset['strobe']).toBe('strobe');
    expect(body(1).style.getPropertyValue('--strobe-period')).toBe('0.333s');
    expect(mark(1)).toBe('⚡ 20 Hz');
    expect(page.querySelector('[data-light="1"]')?.getAttribute('title')).toBe('Bar: channels 1 to 4 (U1); strobe, 20 Hz');

    // a light of letters says nothing of its strobe: above its first values it strobes, at a speed it does not say
    store.setChannel(11, 255);
    store.setChannel(14, 128);
    await fixture.whenStable();
    expect(mark(2)).toBe('⚡');

    // a light that gives no light shows no strobe
    store.setChannel(1, 0);
    await fixture.whenStable();
    expect(body(1).classList).not.toContain('strobe');
  });

  it('the panel of a fixture says what its channels do at their values: in its lists, under its faders and on its letters', async () => {
    const strobe = { name: 'Strobe', code: 'S', fine: null, ranges: [
      { from: 0, to: 31, label: 'Closed', color: null },
      { from: 32, to: 63, label: 'Open', color: null },
      { from: 64, to: 95, label: 'Strobe, slow to fast', color: null },
      { from: 96, to: 255, label: 'Open', color: null },
    ] };
    connection.emit('StageChanged', {
      ...EMPTY_STAGE,
      lights: [{ ...stageLight(1, 'Hex bar', 1, 'RGBS', null, 2, 8), type: 1 }],
      types: [
        {
          id: 1,
          maker: 'American DJ',
          model: 'Ultra Hex Bar 12',
          mode: '4-channel',
          source: 'american-dj/ultra-hex-bar-12',
          channels: [
            { name: 'Red', code: 'R', fine: null, ranges: [] },
            { name: 'Green', code: 'G', fine: null, ranges: [] },
            { name: 'Blue', code: 'B', fine: null, ranges: [] },
            strobe,
          ],
        },
      ],
    } satisfies StagePlan);
    await fixture.whenStable();
    await show('/fixtures');
    await press(button('Hex bar: color and settings'));

    // the list of the strobe: every choice with its values, so the two Opens are told apart
    const list = page.querySelector<HTMLSelectElement>('lm-fixture-panel .form select')!;
    expect([...list.options].map((option) => option.text.trim())).toEqual([
      'Closed (0 to 31)',
      'Open (32 to 63)',
      'Strobe, slow to fast (64 to 95)',
      'Open (96 to 255)',
    ]);

    // under the faders: what the fader moved last does at its value
    const said = () => page.querySelector('lm-fixture-panel lm-light-faders .said')?.textContent?.trim();
    expect(said()).toBe('Move a fader to see here what its value does; the pointer on its letter shows all its values.');
    const fader = page.querySelectorAll<HTMLElement>('lm-fixture-panel lm-light-faders lm-fader')[3];
    for (const press of [1, 2]) fader.dispatchEvent(new KeyboardEvent('keydown', { key: 'PageUp', bubbles: true, detail: press }));
    await fixture.whenStable();
    expect(store.channels()[3]).toBe(32);
    expect(said()).toBe('S (4) at 32: Open (32 to 63)');
    expect(fader.getAttribute('aria-valuetext')).toBe('32, Open');
    expect(list.selectedOptions[0].text.trim()).toBe('Open (32 to 63)');

    // the pointer on the letter: all its values
    const letters = page.querySelectorAll<HTMLElement>('lm-fixture-panel lm-light-faders .letter');
    expect(letters[3].title).toBe('Strobe\n0 to 31: Closed\n32 to 63: Open\n64 to 95: Strobe, slow to fast\n96 to 255: Open');
    expect(letters[0].title).toBe('Red');
  });

  it('a moving head takes its place from the memory raised last; the master and blackout leave it where it is', async () => {
    await planWith(stageLight(1, 'Head', 1, 'PTD', null, 2, 2));
    const look = (pan: number, tilt: number) => [pan, tilt, 255, ...new Array<number>(UNIVERSE_SIZE - 3).fill(0)];
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
      ...page.querySelectorAll<HTMLInputElement>('[aria-label="Note for Channel 1 (U1)"], [aria-label="Note for Channel 1 (U1), second line"]'),
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
    expect([...kind.options].map((option) => option.text.trim())).toEqual(['Channel (U1)', 'Memory', 'Fixture', 'Group']);

    // a fixture: the first by its address, which the closed field shows; its name is the note
    kind.value = 'fixture';
    kind.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetSequence')?.[1]).toMatchObject({ steps: [{ ...step(5, 200), target: 'fixture', number: 2, color: null }, step(6, 100, { wait: 0 })] });
    const first = page.querySelectorAll('lm-sequences-view table.steps tbody tr')[0];
    expect(first.querySelector<HTMLSelectElement>('td.number select')!.selectedOptions[0].text.trim()).toBe('5 (U1)');
    expect(first.querySelector('td.note')?.textContent?.trim()).toBe('Front');

    // its color: the colors of the Fixtures page. A press only chooses, the window shows it; Use gives it to the step.
    const colorWindow = page.querySelector<HTMLDialogElement>('lm-step-color-dialog dialog')!;
    const sent = connection.calls.length;
    await press(button('Step 1, color'));
    await press(page.querySelector<HTMLButtonElement>('lm-step-color-dialog button[aria-label="Blue"]'));
    await press(page.querySelector<HTMLButtonElement>('lm-step-color-dialog button[aria-label="Red"]'));
    expect(colorWindow.open).toBe(true);
    expect(textOf('lm-step-color-dialog .chosen')).toBe('Chosen: #ff0000');
    expect(connection.calls.slice(sent).map((call) => call.method)).not.toContain('SetSequence');
    await press(button('Use this color'));
    expect(colorWindow.open).toBe(false);
    expect(connection.last('SetSequence')?.[1]).toMatchObject({ steps: [{ target: 'fixture', number: 2, color: '#ff0000' }, step(6, 100, { wait: 0 })] });

    // Cancel leaves the step as it was
    const before = connection.calls.length;
    await press(button('Step 1, color'));
    expect(textOf('lm-step-color-dialog .chosen')).toBe('Chosen: #ff0000'); // it opens on the step's color
    await press(button('White, no color'));
    expect(textOf('lm-step-color-dialog .chosen')).toBe('Chosen: white, no color');
    await press(page.querySelector<HTMLButtonElement>('lm-step-color-dialog .actions button:nth-child(2)')); // Cancel
    expect(colorWindow.open).toBe(false);
    expect(connection.calls.slice(before).map((call) => call.method)).not.toContain('SetSequence');

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
      [...page.querySelectorAll('lm-fixture-strip')]
        .find((item) => item.querySelector('.swatch')?.getAttribute('aria-label') === `${name}: color and settings`)!
        .querySelector<HTMLButtonElement>('button[lmLamp]')!;

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

  it('import: a show file (.ASC) becomes the project, and the window says what became of it', async () => {
    connection.emit('ProjectChanged', { name: 'Untitled', path: null, unsaved: true });
    await press(button('Import'));
    const dialog = page.querySelector<HTMLDialogElement>('lm-import-dialog dialog');
    expect(dialog?.open).toBe(true);
    expect(['Project', 'Stage', 'Settings', 'Keyboard', '.ASC'].map((part) => checked('lm-import-dialog', part))).toEqual([
      false,
      false,
      false,
      false,
      false,
    ]);
    expect(textOf('lm-import-dialog .file-name')).toBe('No file chosen');
    expect(button('Import the file').disabled).toBe(true);

    // .ASC is a file of its own: taken alone, the others go off.
    await check('lm-import-dialog', 'Stage');
    await check('lm-import-dialog', '.ASC');
    expect(checked('lm-import-dialog', 'Stage')).toBe(false);
    expect(page.querySelector<HTMLInputElement>('lm-import-dialog input[type="file"]')?.accept).toBe('.asc,.txt');
    expect(page.querySelector('lm-import-dialog')?.textContent).toContain('The open project has changes that are not saved.');

    await pick(new File(['Cue 1\nChan 1 FL\n'], 'Hamlet.asc'));
    expect(textOf('lm-import-dialog .file-name')).toBe('Hamlet.asc');
    await press(button('Import the file'));
    await until(() => page.querySelector('lm-import-dialog .report') !== null);

    expect(connection.last('ImportProject')).toEqual(['Hamlet.asc', 'Cue 1\nChan 1 FL\n']);
    const report = [...page.querySelectorAll('lm-import-dialog .report p')].map((line) => line.textContent?.trim());
    expect(report).toEqual(connection.importAnswer.report);
    expect(textOf('lm-top-bar .name strong')).toBe('Hamlet');

    // OK closes the window; Import again starts with nothing checked and no file.
    await press(buttonIn('lm-import-dialog', 'OK'));
    expect(dialog?.open).toBe(false);
    await press(button('Import'));
    expect(checked('lm-import-dialog', '.ASC')).toBe(false);
    expect(textOf('lm-import-dialog .file-name')).toBe('No file chosen');
    await press(page.querySelector<HTMLButtonElement>('lm-import-dialog .actions button')); // Cancel
    expect(dialog?.open).toBe(false);

    // saving offers the name of the show file
    await press(button('Save project'));
    expect(page.querySelector<HTMLInputElement>('#project-name')?.value).toBe('Hamlet');
  });

  it('import says why the desk refused a file, and another one can be tried', async () => {
    connection.importRefusal = '"notes.txt" has no cues, submasters or groups.';
    await press(button('Import'));
    await check('lm-import-dialog', '.ASC');
    await pick(new File(['Dear diary,'], 'notes.txt'));
    await press(button('Import the file'));
    await until(() => page.querySelector('lm-import-dialog .error') !== null);

    expect(textOf('lm-import-dialog .error')).toBe('"notes.txt" has no cues, submasters or groups.');
    expect(page.querySelector<HTMLDialogElement>('lm-import-dialog dialog')?.open).toBe(true);
    expect(button('Import the file').disabled).toBe(false);
  });

  it('import takes no file larger than the desk takes, and sends nothing', async () => {
    await press(button('Import'));
    await check('lm-import-dialog', '.ASC');
    const file = new File(['Cue 1'], 'huge.asc');
    Object.defineProperty(file, 'size', { value: 9 * 1024 * 1024 });
    await pick(file);

    expect(textOf('lm-import-dialog .error')).toBe('"huge.asc" has 9.0 MB. A file can have up to 8.0 MB.');
    expect(button('Import the file').disabled).toBe(true);
    expect(connection.methods()).not.toContain('ImportProject');
  });

  it('import takes the checked parts of a file that Export made, and none when one of them is missing', async () => {
    const file = JSON.stringify({
      format: 'lumenet-export',
      version: 1,
      name: 'Concert',
      project: JSON.parse(connection.projectFile),
      settings: { channelRows: 1, colors: { play: '#123456' } },
      keyboard: { shortcuts: { 'stack.play': 'Space' } },
    });
    await press(button('Import'));
    await check('lm-import-dialog', 'Project');
    await check('lm-import-dialog', 'Settings');
    await check('lm-import-dialog', 'Keyboard');
    expect(page.querySelector<HTMLInputElement>('lm-import-dialog input[type="file"]')?.accept).toBe('.json');
    await pick(new File([file], 'Concert - project, settings, keyboard.json'));
    await press(button('Import the file'));
    await until(() => page.querySelector('lm-import-dialog .report') !== null);

    expect(connection.last('ImportProjectFile')).toEqual(['Concert', JSON.stringify(JSON.parse(connection.projectFile))]);
    expect(textOf('lm-top-bar .name strong')).toBe('Concert');
    const settings = TestBed.inject(DeskSettings);
    expect(settings.channelRows()).toBe(1);
    expect(settings.colors()).toEqual({ play: '#123456' });
    expect(settings.shortcuts()['stack.play']).toBe('Space');
    expect([...page.querySelectorAll('lm-import-dialog .report p')].map((line) => line.textContent?.trim())).toEqual([
      'The project "Concert" is open. Save keeps it in the projects folder.',
      'The settings of this device are those of the file.',
      'The keyboard shortcuts of this device are those of the file.',
    ]);

    // The stage of a project file: on its own, in place of the project's.
    await press(buttonIn('lm-import-dialog', 'OK'));
    await press(button('Import'));
    await check('lm-import-dialog', 'Stage');
    await pick(new File([connection.projectFile], 'Evening.json'));
    await press(button('Import the file'));
    await until(() => page.querySelector('lm-import-dialog .report') !== null);
    expect(JSON.parse(connection.last('ImportStage')![0] as string).lights.length).toBe(1);
    expect(textOf('lm-import-dialog .report')).toContain('The stage is that of the file, with 1 light.');

    // A project file has no keyboard of this device: nothing is imported.
    await press(buttonIn('lm-import-dialog', 'OK'));
    await press(button('Import'));
    await check('lm-import-dialog', 'Stage');
    await check('lm-import-dialog', 'Keyboard');
    const imported = connection.calls.length;
    await pick(new File([connection.projectFile], 'Evening.json'));
    await press(button('Import the file'));
    await until(() => page.querySelector('lm-import-dialog .error') !== null);
    expect(textOf('lm-import-dialog .error')).toBe('"Evening.json" does not have the keyboard. Nothing was imported.');
    expect(connection.calls.slice(imported).map((call) => call.method)).not.toContain('ImportStage');
  });

  it('export puts the checked parts into one file on this device', async () => {
    const written: string[] = [];
    let suggested = '';
    const picker = vi.fn(async (options: { suggestedName: string }) => {
      suggested = options.suggestedName;
      return {
        name: options.suggestedName,
        createWritable: async () => ({ write: async (text: string) => void written.push(text), close: async () => undefined }),
      };
    });
    (globalThis as { showSaveFilePicker?: unknown }).showSaveFilePicker = picker;
    try {
      TestBed.inject(DeskSettings).setShortcuts({ 'stack.play': 'Space' });
      await press(button('Export'));
      const dialog = page.querySelector<HTMLDialogElement>('lm-export-dialog dialog');
      expect(dialog?.open).toBe(true);
      expect(['Project', 'Stage', 'Settings', 'Keyboard'].map((part) => checked('lm-export-dialog', part))).toEqual([false, false, false, false]);
      expect(button('Export the file').disabled).toBe(true);

      await check('lm-export-dialog', 'Stage');
      await check('lm-export-dialog', 'Keyboard');
      await press(button('Export the file'));
      await until(() => written.length > 0);
      await fixture.whenStable();

      expect(suggested).toBe('Untitled - stage, keyboard.json');
      const file = JSON.parse(written[0]);
      expect(Object.keys(file)).toEqual(['format', 'version', 'name', 'stage', 'keyboard']);
      expect(file.stage.lights[0].name).toBe('Wash');
      expect(file.keyboard.shortcuts['stack.play']).toBe('Space');
      expect(textOf('lm-export-dialog .report')).toContain('Saved as "Untitled - stage, keyboard.json"');
      await press(buttonIn('lm-export-dialog', 'OK'));
      expect(dialog?.open).toBe(false);
    } finally {
      delete (globalThis as { showSaveFilePicker?: unknown }).showSaveFilePicker;
    }
  });

  it('Notes: the notes of the project are read, written while Edit is lit, and kept by the desk program', async () => {
    connection.notes = '<div><b>Cue 1</b> at the bell</div><script>alert(1)</script><img src="x" onerror="alert(1)">';
    connection.emit('DeskChanged', connection.welcome.desk);
    await show('/notes');
    await until(() => page.querySelector('lm-notes-view .text b') !== null);
    const text = page.querySelector<HTMLElement>('lm-notes-view .text')!;
    const edit = button('Edit the notes');

    // Read: only the marks of the notes are shown, and nothing can be changed.
    expect(text.innerHTML).toBe('<div><b>Cue 1</b> at the bell</div>');
    expect(text.getAttribute('contenteditable')).toBe('false');
    expect(edit.className).toContain('level-1');
    expect(edit.getAttribute('aria-pressed')).toBe('false');
    for (const tool of ['Bold', 'Underline', 'Bulleted list', 'Numbered list', 'Usual color', 'Red']) expect(button(tool).disabled).toBe(true);

    // Edit: full, the text can be written in, and the tool keys work on it with the browser's editor.
    const commands: unknown[][] = [];
    const editor = document as unknown as { execCommand: (...args: unknown[]) => boolean };
    editor.execCommand = (...args: unknown[]) => commands.push(args) > 0;
    try {
      await press(edit);
      expect(edit.className).toContain('level-3');
      expect(text.getAttribute('contenteditable')).toBe('true');
      expect(button('Bold').disabled).toBe(false);
      await press(button('Bold'));
      await press(button('Red'));
      expect(commands).toContainEqual(['bold', false, undefined]);
      expect(commands).toContainEqual(['foreColor', false, '#ff5247']);
    } finally {
      delete (editor as { execCommand?: unknown }).execCommand;
    }

    // What is written goes to the desk program, made into the marks of the notes; Edit goes faint again.
    text.innerHTML = '<div><b>Cue 1</b> at the bell</div><div>Cue 2 <u>after the song</u><span>!</span></div>';
    text.dispatchEvent(new Event('input'));
    await press(edit);
    expect(connection.last('SetNotes')).toEqual(['<div><b>Cue 1</b> at the bell</div><div>Cue 2 <u>after the song</u>!</div>']);
    expect(text.getAttribute('contenteditable')).toBe('false');
    expect(edit.className).toContain('level-1');

    // Notes from another page come in; while the stack is live, Edit is locked, and a writer is stopped.
    connection.emit('NotesChanged', '<ul><li>Doors at 7</li></ul>');
    await fixture.whenStable();
    expect(text.innerHTML).toBe('<ul><li>Doors at 7</li></ul>');
    await press(edit);
    await stackStands(LIVE);
    expect(text.getAttribute('contenteditable')).toBe('false');
    expect(edit.disabled).toBe(true);
  });

  it('Load offers the examples under the projects; one opens as a project without a file', async () => {
    await press(button('Load project'));
    await until(() => page.querySelector('lm-project-dialog #examples-title') !== null);
    const examples = [...page.querySelectorAll('lm-project-dialog [aria-labelledby="examples-title"] button')];
    expect(examples.map((entry) => entry.textContent?.trim())).toEqual(['1 Memories and the stack', '6 Concert']);

    await press(examples[1] as HTMLButtonElement);
    await press(page.querySelector<HTMLButtonElement>('lm-project-dialog button[type="submit"]'));
    await until(() => connection.methods().includes('LoadExample'));
    expect(connection.last('LoadExample')).toEqual(['6 Concert']);
    expect(connection.methods()).not.toContain('LoadProject');
    expect(textOf('lm-top-bar .name strong')).toBe('6 Concert');
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
      'Notes',
      'Settings',
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

  it('the memories view shows what a memory holds, and looking changes nothing', async () => {
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
    expect(cells()[4].getAttribute('aria-label')).toBe('Channel 5 (U1): 200');

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
    expect(cell(5).getAttribute('aria-label')).toMatch(/^Channel 5 \(U1\) \(Front left\): \d+$/);
    expect(cell(6).getAttribute('data-note')).toBe('Wash');
    expect(cell(7).hasAttribute('data-note')).toBe(false);
    expect(cell(7).getAttribute('aria-label')).toMatch(/^Channel 7 \(U1\): \d+$/);
  });

  it('a double click on a cell of the memories view changes the level the memory gives the channel, and its note', async () => {
    connection.emit('ChannelNoteChanged', 6, 'Wash');
    await show('/memories');
    await press(button('Look at memory 2'));
    const cell = (channel: number) => page.querySelectorAll<HTMLElement>('lm-memories-view table.matrix td')[channel - 1];
    const editor = () => page.querySelector('lm-cell-editor');
    const fields = () => [...(editor()?.querySelectorAll<HTMLInputElement>('input') ?? [])];
    const doubleClick = async (channel: number) => {
      cell(channel).dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
      await fixture.whenStable();
    };
    const key = async (field: HTMLInputElement, name: string) => {
      field.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true }));
      await fixture.whenStable();
    };

    // fields over the cell: the level, and the two lines of the channel's note
    await doubleClick(5);
    expect(editor()?.getAttribute('aria-label')).toBe('Channel 5 (U1) in memory 2');
    expect(fields().map((field) => field.value)).toEqual(['200', '', '']);
    expect(document.activeElement).toBe(fields()[0]);
    fields()[0].value = '90';
    fields()[1].value = 'Front';
    await key(fields()[0], 'Enter');
    expect(editor()).toBeNull();
    expect(connection.last('SetMemoryChannel')).toEqual([2, 5, 90]);
    expect(connection.last('SetChannelNote')).toEqual([5, 'Front']);
    expect(cell(5).textContent?.trim()).toBe('90'); // shown at once
    expect(store.memoryLooks()[2][4]).toBe(90);

    // Escape changes nothing
    await doubleClick(6);
    expect(fields().map((field) => field.value)).toEqual(['0', 'Wash', '']);
    fields()[0].value = '17';
    await key(fields()[0], 'Escape');
    expect(editor()).toBeNull();
    expect(connection.calls.filter((call) => call.method === 'SetMemoryChannel').length).toBe(1);

    // A memory that holds a sequence: only the note can change here.
    await press(button('Look at memory 3'));
    await doubleClick(8);
    expect(fields()[0].disabled).toBe(true);
    expect(document.activeElement).toBe(fields()[1]);
    await key(fields()[1], 'Escape');

    // An empty memory gets a look with that channel; a level beyond full is full.
    await press(button('Look at memory 9'));
    await doubleClick(1);
    fields()[0].value = '300';
    await key(fields()[0], 'Enter');
    expect(connection.last('SetMemoryChannel')).toEqual([9, 1, 255]);
    expect(store.memoryStored()[8]).toBe(true);

    // While the stack is live nothing that is stored changes: no fields.
    await stackStands({ ...connection.playback.stack, live: true });
    await doubleClick(1);
    expect(editor()).toBeNull();
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
    // the number of the step big at the right of its card, the words at the left
    expect(page.querySelector('.card.current')?.lastElementChild?.className).toBe('cue');
    expect(page.querySelector('.card.current .cue')?.textContent?.trim()).toBe('Step 1.0');
    expect(page.querySelector('.card.current .what')?.textContent?.trim()).toBe('Channel 7 (U1) at 255');
    expect(page.querySelector('.card.next .cue')?.textContent?.trim()).toBe('Step 2.0');
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
    expect(page.querySelector('.card.next .cue')?.textContent?.trim()).toBe('Step 1.0');
    expect(rows().map((row) => row.classList.contains('next'))).toEqual([true, false]);

    await stackStands({ ...LIVE, currentId: 12, nextId: 11, state: 'fading', paused: true, remaining: 0.6 });
    expect(button('Pause').getAttribute('aria-pressed')).toBe('true');
    expect(page.querySelector('.card.current .how')?.textContent).toContain('Paused');
  });

  it('the Stage view has the keys and the cards of the stack at its left, and its steps without fields', async () => {
    await show('/stage');
    await stackStands(LIVE);
    const panel = page.querySelector('lm-stage-stack')!;
    expect([...panel.querySelectorAll('lm-stack-keys button')].map((key) => key.getAttribute('aria-label'))).toEqual([
      'Stack live',
      'Previous step',
      'Play',
      'Pause',
      'Next step',
    ]);
    expect([...panel.querySelectorAll('lm-stack-cards.column .card h3')].map((title) => title.textContent?.trim())).toEqual([
      'Previous',
      'Current',
      'Next',
    ]);
    expect(panel.querySelector('.card.current .what')?.textContent).toContain('Channel 7 (U1) at 255');

    // the steps: number, whether it adds, notes and times only, nothing to change, the one that is on marked
    const headers = [...panel.querySelectorAll('table.steps thead th')].map((header) => header.textContent?.trim());
    expect(headers).toEqual(['Step', 'Add', 'Notes', 'Up (s)', 'Wait (s)', 'Down (s)']);
    const rows = [...panel.querySelectorAll('table.steps tbody tr')];
    expect(rows.map((row) => row.querySelector('th')?.textContent?.trim())).toEqual(['1.0', '2.0']);
    expect(rows.map((row) => row.classList.contains('current'))).toEqual([true, false]);
    expect(rows.map((row) => row.classList.contains('next'))).toEqual([false, true]);
    expect(rows[1].querySelector('td.note')?.textContent?.trim()).toBe('Wash');
    expect([...rows[1].querySelectorAll('td.time')].map((cell) => cell.textContent?.trim())).toEqual(['1.0', '3.0', '1.0']);
    expect(panel.querySelectorAll('table.steps input, table.steps select, table.steps button').length).toBe(0);

    await press(button('Play'));
    await press(button('Next step'));
    expect(connection.methods()).toEqual(['StackPlay', 'StackNext']);
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
    expect(page.querySelector('.card.next .cue')?.textContent?.trim()).toBe('Step 1.0'); // where live starts

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
    // per row: the number of the step, its Add box, the two lists, four typed fields and four keys (▲ ▼ + ✕)
    expect(fields('lm-stack-view').length).toBe(2 * 12);
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
      'Import',
      'Edit the channels, memories and effects of the independent keys',
    ]) {
      expect(button(key).disabled).toBe(true);
    }
    expect(page.querySelector<HTMLInputElement>('input[aria-label="Note for Channel 5 (U1)"]')?.readOnly).toBe(true);
    expect(page.querySelector<HTMLInputElement>('input[aria-label="Note for Memory 2"]')?.readOnly).toBe(true);
    store.pressFunction('save'); // a press that gets through anyway starts nothing
    expect(store.mode()).toEqual(IDLE);
    // what a show needs is not locked
    for (const key of ['Save project', 'Export', 'Clear', 'Blackout', 'Independent 3, channel 3 (U1)']) {
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
      expect(connection.calls.filter((call) => call.method === 'SetStackLive').map((call) => call.args)).toEqual([[true], [false]]);
    } finally {
      delete (root as { requestFullscreen?: unknown }).requestFullscreen;
      delete (document as { exitFullscreen?: unknown }).exitFullscreen;
      delete (document as { fullscreenElement?: unknown }).fullscreenElement;
    }
  });

  it('a stack that goes live ends an unfinished recording and the editing of the independent keys', async () => {
    await press(button('Record sequence'));
    await press(memoryKey(9));
    await press(button('Edit the channels, memories and effects of the independent keys'));
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

  it('the + key of a step puts a new one right after it, and rests while the stack is live', async () => {
    await show('/stack');

    // the step Add step would make after that one: the next channel or memory, with its times
    await press(button('Add a step after step 1.0'));
    expect(connection.last('AddStackStepAfter')).toEqual([11, step(8, 255, { wait: 0 })]);
    await press(button('Add a step after step 2.0'));
    expect(connection.last('AddStackStepAfter')).toEqual([12, step(3, 128, { target: 'memory', wait: 3 })]);

    await stackStands(LIVE);
    expect(button('Add a step after step 1.0').disabled).toBe(true);
  });

  it('the Add box makes a step come up on top of what is on, and the steps that are on are marked', async () => {
    await show('/stack');
    const box = () => page.querySelector<HTMLInputElement>('input[aria-label="Step 2.0 adds on top of what is on"]');
    expect(box()?.checked).toBe(false);

    box()?.click();
    await fixture.whenStable();
    expect(connection.last('UpdateStackStep')).toEqual([12, { ...step(2, 128, { target: 'memory', wait: 3 }), add: true }]);

    // the desk takes it, and goes live on that step with the first one still on under it
    const adding = { steps: [connection.stack.steps[0], { ...connection.stack.steps[1], add: true }] };
    connection.emit('StackChanged', adding);
    await stackStands({ ...LIVE, currentId: 12, nextId: 11, on: [11, 12] });
    const rows = () => [...page.querySelectorAll('lm-stack-view table.steps tbody tr')];
    expect(rows().map((row) => row.classList.contains('on'))).toEqual([true, true]);
    expect(rows().map((row) => row.classList.contains('current'))).toEqual([false, true]);
    expect(box()?.checked).toBe(true);
    expect(box()?.disabled).toBe(true); // a live stack keeps its steps
    expect(page.querySelector('.card.current .what')?.textContent).toContain('Memory 2 (Wash) at 128, adds');

    // unchecked again: the desk is told without "add"
    await stackStands(NOT_LIVE);
    box()?.click();
    await fixture.whenStable();
    expect(connection.last('UpdateStackStep')).toEqual([12, step(2, 128, { target: 'memory', wait: 3 })]);
    expect(box()?.checked).toBe(false);

    // the Stage view ticks the steps that add, and marks those that are on
    connection.emit('StackChanged', adding);
    await show('/stage');
    await stackStands({ ...LIVE, currentId: 12, nextId: 11, on: [11, 12] });
    const stageRows = [...page.querySelectorAll('lm-stage-stack table.steps tbody tr')];
    expect(stageRows.map((row) => row.querySelector('td.adds')?.textContent?.trim())).toEqual(['', '✓adds']);
    expect(stageRows.map((row) => row.classList.contains('on'))).toEqual([true, true]);
  });

  it('a release takes one step of the stack down, chosen from the other steps by their numbers', async () => {
    await show('/stack');
    const kind = page.querySelector<HTMLSelectElement>('select[aria-label="Step 2.0, channel, memory, fixture, group, effect or release"]');
    if (!kind) throw new Error('no field for the kind of step 2.0');
    expect([...kind.options].map((option) => option.text)).toContain('Release');

    kind.value = 'release';
    kind.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    // at first it takes down the step right above it; it never adds
    expect(connection.last('UpdateStackStep')).toEqual([12, { target: 'release', number: 11, value: 128, up: 1, wait: 3, down: 1 }]);

    // the row: the step it takes down by its number and what that step is; no value, no up time, no Add box
    const row = page.querySelectorAll('lm-stack-view table.steps tbody tr')[1];
    const number = row.querySelector<HTMLSelectElement>('select[aria-label="Step 2.0, the step it takes down"]');
    if (!number) throw new Error('no field for the step that step 2.0 takes down');
    expect(number.options[0].text.trim()).toBe('1.0');
    expect(row.querySelector('td.note')?.textContent?.trim()).toBe('Channel 7 (U1)');
    expect(row.querySelectorAll('td.unused').length).toBe(2);
    expect(row.querySelector('input[aria-label="Step 2.0, value, 0 to 255"]')).toBeNull();
    expect(row.querySelector('input[aria-label="Step 2.0 adds on top of what is on"]')).toBeNull();
    number.dispatchEvent(new Event('focus'));
    await fixture.whenStable();
    expect([...number.options].map((option) => option.text.trim())).toEqual(['1.0: Channel 7 (U1)']); // never itself

    // the cards say what it does
    await stackStands({ ...LIVE, currentId: 12, nextId: 11, state: 'fading', remaining: 0.4, on: [] });
    expect(page.querySelector('.card.current .what')?.textContent).toContain('Release of step 1.0: Channel 7 (U1)');
    expect(page.querySelector('.card.current .how')?.textContent).toContain('Taking its step down, 0.4 s left.');

    // a sequence has no releases
    await stackStands(NOT_LIVE);
    await show('/sequences');
    await press(button('Work on memory 3'));
    const sequenceKind = page.querySelector<HTMLSelectElement>('select[aria-label="Step 1, channel, memory, fixture or group"]');
    expect([...(sequenceKind?.options ?? [])].map((option) => option.value)).not.toContain('release');
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
    connection.emit('PlaybackChanged', { ...connection.playback, crossfade: { channels: [], memories: [2, 5], levels } });
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
    expect(page.querySelector('.card.previous .cue')?.textContent?.trim()).toBe('Step 1.0');

    // On the first step it is the last: Prev goes round, as Play does.
    await stackStands(LIVE);
    expect(page.querySelector('.card.previous .cue')?.textContent?.trim()).toBe('Step 2.0');

    // Not live: no step is on, so none is before it.
    await stackStands(NOT_LIVE);
    expect(page.querySelector('.card.previous')?.classList.contains('on')).toBe(false);
    expect(rows().some((row) => row.classList.contains('previous'))).toBe(false);

    expect(button('Play').classList).toContain('play');
    expect(button('Play').closest('.transport')).toBeTruthy();
    expect(button('Previous step').closest('.transport')).toBeTruthy();
  });

  it('the main view has the stack across the top at the right, with a wheel of its steps and their notes', async () => {
    const section = page.querySelector('lm-stack-section');
    if (!section) throw new Error('no stack section on the main view');
    // The places in the window of the wheel, from the top, as [number, note]: one more on each
    // side only rolls in. A step without a note says what it is.
    const wheel = () =>
      [...section.querySelectorAll('.roller li')]
        .slice(1, -1)
        .map((place) => [place.querySelector('.cue')?.textContent?.trim(), place.querySelector('.note')?.textContent?.trim()]);

    expect(wheel()).toEqual([
      ['', ''],
      ['', ''],
      ['', ''],
      ['-', ''],
      ['1.0', 'Channel 7 (U1)'],
      ['2.0', 'Wash'],
      ['', ''],
    ]); // nothing is on; Play goes to the first step, then the second; 7 places at first
    expect(section.querySelector('.roller .next')?.getAttribute('aria-label')).toBe('Next step: 1.0, Channel 7 (U1)');
    expect(section.querySelector('.pointer')).toBeTruthy(); // the arrow beside the middle
    expect(button('Play').disabled).toBe(true); // only while the stack is live

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
    expect(section.querySelector('.roller .current')?.getAttribute('aria-label')).toBe('Current step: 1.0, Channel 7 (U1)');
    expect(section.querySelector('.roller .previous')?.getAttribute('aria-label')).toBe('Previous step: 2.0, Wash');

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
        .map((place) => place.querySelector('.cue')?.textContent?.trim());
    expect(seen()).toEqual(['2', '3', '4', '5', '6', '7', '8']); // 7 at first

    await show('/settings');
    const size = page.querySelector<HTMLSelectElement>('#wheel-size')!;
    expect(size.value).toBe('7');
    size.value = '3';
    size.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    await show('/');
    expect(seen()).toEqual(['4', '5', '6']);
    // the main view sizes its side column with it, the section its window
    expect(page.querySelector('lm-main-view')?.getAttribute('style')).toContain('--wheel-size: 3');
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
    await press(button('Channels 9 to 16 (U1)'));
    expect(page.querySelector('lm-fader[aria-label="Channel 9 (U1)"]')).toBeTruthy();

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
    expect([channelRows.value, memoryRows.value]).toEqual(['1', '2']);

    await show('/');
    expect(page.querySelectorAll('lm-channel-section .row').length).toBe(1);
    expect(page.querySelectorAll('lm-channel-section lm-fader-strip').length).toBe(32);
    expect(page.querySelectorAll('lm-memory-section .row').length).toBe(2);
    expect(page.querySelectorAll('lm-memory-section lm-fader-strip').length).toBe(64);
    expect(button('Channels 33 to 64 (U1)')).toBeTruthy();
    expect(button('Memories 65 to 128')).toBeTruthy();
    const main = page.querySelector<HTMLElement>('lm-main-view')!;
    expect(main.style.getPropertyValue('--channel-share')).toBe('1fr');
    expect(main.style.getPropertyValue('--memory-share')).toBe('2fr');

    // On a narrower window a row is 16 faders.
    store.windowStripsPerRow.set(16);
    await fixture.whenStable();
    expect(page.querySelectorAll('lm-channel-section lm-fader-strip').length).toBe(16);
  });

  it('Settings has four panels: the main view with the universes, the zoom, the autosave and LAN; the views; the colors; the keyboard; the outputs under them', async () => {
    await show('/settings');
    const panels = [...page.querySelectorAll('lm-settings-view .panels > *')];
    const titles = (panel: Element, level: string) => [...panel.querySelectorAll(level)].map((title) => title.textContent?.trim());
    expect(panels.map((panel) => titles(panel, 'h3')[0])).toEqual(['Main view', 'Views in the top bar', 'Colors of the keys', 'Keyboard', 'Outputs']);
    expect(titles(panels[0], 'h4')).toEqual(['Universes', 'Zoom', 'Autosave', 'Network (LAN)']);
    expect(page.querySelector('#universes')?.closest('.panel')).toBe(panels[0]);
    // fields only, no sentences about them, until the autosave and LAN, which keep theirs
    const autosave = panels[0].querySelector('#autosave-title')!;
    const hints = [...panels[0].querySelectorAll('.hint')];
    expect(hints.length).toBeGreaterThan(0);
    expect(hints.every((hint) => autosave.compareDocumentPosition(hint) & Node.DOCUMENT_POSITION_FOLLOWING)).toBe(true);
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
      'Independent 1, channel 1 (U1)',
      'Independent 2, channel 2 (U1)',
      'Independent 3, channel 3 (U1)',
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

  it('the zoom makes the whole page bigger and smaller in steps of 10 per cent', async () => {
    await show('/settings');
    const zoom = () => document.documentElement.style.getPropertyValue('--zoom');
    expect(page.querySelector('.scale output')?.textContent?.trim()).toBe('100 %');

    await press(button('Zoom in'));
    await press(button('Zoom in'));
    expect(zoom()).toBe('1.2');
    expect(page.querySelector('.scale output')?.textContent?.trim()).toBe('120 %');

    await press(button('Reset'));
    await press(button('Zoom out'));
    expect(zoom()).toBe('0.9');
    await press(button('Reset'));
  });

  /** A kind of keys in the Keyboard panel of Settings: the head that opens it. */
  function kind(group: string): HTMLButtonElement {
    const found = [...page.querySelectorAll<HTMLButtonElement>('lm-keyboard-panel .head')].find(
      (candidate) => candidate.querySelector('.name')?.textContent?.trim() === group,
    );
    if (!found) throw new Error(`no kind of keys "${group}"`);
    return found;
  }

  it('the Keyboard panel of Settings lists the kinds of keys one under the other: one open at a time, none at first', async () => {
    await show('/settings');
    const total = (group: string) => DESK_ACTIONS.filter((action) => action.group === group).length;
    // "Stack 6 (4) 2": the keys, in brackets those with a shortcut, and while a search runs, those it finds
    const count = (group: string) => kind(group).querySelector('.count')?.textContent?.trim();
    const bound = (group: string) => kind(group).querySelector('.bound');
    const found = (group: string) => kind(group).querySelector('.found');
    const rows = () => [...page.querySelectorAll('lm-keyboard-panel .keys tbody th')].map((cell) => cell.textContent?.trim());
    const open = () =>
      [...page.querySelectorAll('lm-keyboard-panel .head[aria-expanded="true"]')].map((head) => head.querySelector('.name')?.textContent?.trim());

    expect(page.querySelectorAll('lm-keyboard-panel .head').length).toBe(ACTION_GROUPS.length);
    expect(open()).toEqual([]); // all closed at first
    expect(rows()).toEqual([]);
    expect(count('Master')).toBe(String(total('Master')));
    expect(bound('Master')?.textContent?.trim()).toBe('(0)');
    expect(bound('Master')?.classList).toContain('none'); // dim; green only with a shortcut
    expect(bound('Project and LAN')?.textContent?.trim()).toBe('(2)'); // Undo and Redo have theirs from the start
    expect(bound('Project and LAN')?.classList).not.toContain('none');
    expect(found('Master')).toBeNull(); // no search, no third number
    expect(kind('Master').getAttribute('aria-label')).toBe('Master: 1 key, 0 with a shortcut');

    await press(kind('Master'));
    expect(open()).toEqual(['Master']);
    expect(kind('Master').getAttribute('aria-controls')).toBe(page.querySelector('lm-keyboard-panel .keys')?.id);
    expect(rows()).toEqual(['Blackout']);
    await press(kind('General'));
    expect(open()).toEqual(['General']); // one at a time
    expect(rows()).toEqual(['Clear', 'Solo up', 'Solo down', 'Rate', 'Tempo (tap)']);
    await press(kind('General'));
    expect(open()).toEqual([]); // pressed again, it closes

    // The arrow keys go from kind to kind.
    kind('General').focus();
    kind('General').dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }));
    expect(document.activeElement).toBe(kind('Stack'));

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
    expect(kind('Master').getAttribute('aria-label')).toBe('Master: 1 key, 0 with a shortcut, 1 found');
    expect(kind('Stack').classList).toContain('none-found');
    expect(open()).toEqual(['Master']);
    expect(rows()).toEqual(['Blackout']);

    search.value = '';
    search.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(open()).toEqual(['Stack']);

    // The old address of the Keyboard view opens Settings.
    await show('/keyboard');
    expect(TestBed.inject(Router).url).toBe('/settings');
  });

  it('a shortcut is recorded for a key, and then presses that key', async () => {
    await show('/settings');
    await press(kind('Master'));
    await press(button('Set the shortcut of Blackout'));
    expect(page.querySelector('tr[data-row="master.blackout"] .waiting')).toBeTruthy();

    // Ctrl alone is held, not a shortcut; Ctrl with B is, with the side of that Ctrl.
    const key = (type: 'keydown' | 'keyup', code: string, ctrlKey: boolean) =>
      document.dispatchEvent(new KeyboardEvent(type, { code, key: code === 'KeyB' ? 'b' : code === 'KeyZ' ? 'z' : 'Control', ctrlKey, bubbles: true, cancelable: true }));
    key('keydown', 'ControlLeft', true);
    key('keydown', 'KeyB', true);
    await fixture.whenStable();
    expect(page.querySelector('tr[data-row="master.blackout"] td.shortcut')?.textContent?.trim()).toBe('Left Ctrl + B');

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
    const note = page.querySelector<HTMLInputElement>('input[aria-label="Note for Channel 1 (U1)"]')!;
    note.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyB', key: 'b', ctrlKey: true, bubbles: true }));
    await fixture.whenStable();
    expect(connection.methods().filter((method) => method === 'SetBlackout').length).toBe(1);

    // The same shortcut for another key moves over to it.
    await show('/settings');
    await press(kind('General'));
    await press(button('Set the shortcut of Clear'));
    document.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyB', key: 'b', ctrlKey: true, bubbles: true }));
    await fixture.whenStable();
    expect(page.querySelector('tr[data-row="master.clear"] td.shortcut')?.textContent?.trim()).toBe('Left Ctrl + B');
    await press(kind('Master'));
    expect(page.querySelector('tr[data-row="master.blackout"] td.shortcut')?.textContent?.trim()).toBe('');
    expect(page.textContent).toContain('That shortcut was the one of Blackout before');
  });

  it('AltGr is the right Alt, and still presses a shortcut recorded with it as Ctrl + Alt', async () => {
    // AltGr on Windows: a left Ctrl that is not there goes down first, and every press says Ctrl and Alt.
    const altGr = (type: 'keydown' | 'keyup', code: string, down: boolean) =>
      document.dispatchEvent(
        new KeyboardEvent(type, { code, key: code === 'KeyB' ? 'b' : 'AltGraph', ctrlKey: down, altKey: down, modifierAltGraph: down, bubbles: true, cancelable: true }),
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

    await show('/settings');
    await press(kind('Master'));
    await press(button('Set the shortcut of Blackout'));
    pressAltGrB();
    await fixture.whenStable();
    expect(page.querySelector('tr[data-row="master.blackout"] td.shortcut')?.textContent?.trim()).toBe('Right Alt + B');
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
    await show('/settings');
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
    expect(router.url).toBe('/settings');
    expect(page.querySelector('tr[data-row="stage.edit"]')?.classList.contains('picked')).toBe(true);
    // Only the list scrolls to the row; the page itself stays where it is, the top bar in sight.
    expect(document.scrollingElement?.scrollTop ?? 0).toBe(0);
    await later();
    expect(router.url).toBe('/settings'); // the picked key was not pressed after all

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

  it('every key on the page that is marked for shortcuts is one the list of the Keyboard panel knows', async () => {
    for (const view of ['/', '/stack', '/sequences', '/memories', '/stage', '/fixtures', '/notes', '/settings']) {
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

  // ---------------------------------------------------------------- effects

  /** An effect of the project, as the desk program keeps it. */
  function anEffect(number: number, more: Partial<Effect> = {}): Effect {
    return {
      number,
      name: `Effect ${number}`,
      kind: 'intensity',
      shape: 'sine',
      group: null,
      lights: [],
      speed: 60,
      size: 100,
      spread: 100,
      direction: 'forward',
      order: 'address',
      blocks: 1,
      wings: 1,
      fadeIn: 0,
      fadeOut: 0,
      duty: 50,
      colors: [],
      angle: 0,
      seed: 0,
      ...more,
    };
  }

  /** Lets the page's own timers run: the store sends changes of an effect together, a moment later. */
  async function wait(ms: number): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, ms));
    await fixture.whenStable();
  }

  it('the Effects view makes an effect of a kind, and its editor changes it', async () => {
    await show('/effects');
    expect(page.querySelector('lm-effects-view .summary')?.textContent).toContain('Effect 1 is empty');

    // the key of an empty number chooses it, and the kinds to make it are offered
    await press(page.querySelector<HTMLButtonElement>('button[aria-label="Effect 3 key"]'));
    expect(page.querySelector('lm-effects-view .summary')?.textContent).toContain('Effect 3 is empty');
    expect(connection.methods()).not.toContain('SetEffectControl');
    await press(page.querySelector<HTMLButtonElement>('button[aria-label="Effect 1 key"]'));

    await press(button('Intensity'));
    expect(store.effects().map((effect) => effect.number)).toEqual([1]);
    await wait(200);
    const made = connection.last('SetEffect')?.[0] as Effect;
    expect(made).toMatchObject({ number: 1, kind: 'intensity', shape: 'sine', name: 'Sine' });

    const speed = page.querySelector<HTMLInputElement>('#effect-speed')!;
    speed.value = '128';
    speed.dispatchEvent(new Event('change'));
    await wait(200);
    expect((connection.last('SetEffect')?.[0] as Effect).speed).toBe(128);

    // a number out of range is put back as it was
    speed.value = '9000';
    speed.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(speed.value).toBe('128');
  });

  it('a new movement goes around a center of its own, which a press on the preview moves', async () => {
    await show('/effects');
    await press(button('Movement'));
    await wait(200);
    expect(connection.last('SetEffect')?.[0]).toMatchObject({ kind: 'movement', centerPan: 50, centerTilt: 50 });

    // the square of the heads' range: 200 pixels, 8 from the edges of the preview
    const canvas = page.querySelector<HTMLCanvasElement>('lm-effect-preview canvas')!;
    expect(canvas.classList).toContain('movable');
    const real = canvas.getBoundingClientRect.bind(canvas);
    canvas.getBoundingClientRect = () => ({ left: 0, top: 0, right: 216, bottom: 216, width: 216, height: 216, x: 0, y: 0, toJSON: () => ({}) }) as DOMRect;
    canvas.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, button: 0, clientX: 58, clientY: 158 }));
    canvas.dispatchEvent(new MouseEvent('pointerup', { bubbles: true }));
    canvas.getBoundingClientRect = real;
    await wait(200);
    expect(connection.last('SetEffect')?.[0]).toMatchObject({ kind: 'movement', centerPan: 25, centerTilt: 75 });
  });

  it('the key of an effect starts and stops it, its fader moves like a fader, and the desk tells how it runs', async () => {
    connection.emit('EffectsChanged', [anEffect(2, { name: 'Pulse' })]);
    await show('/effects');

    await press(page.querySelector<HTMLButtonElement>('button[aria-label="Effect 2 key"]'));
    expect(connection.last('SetEffectControl')).toEqual([2, 'toggle', 0]);

    store.setEffectLevel(2, 128);
    await wait(40);
    const [number, control, value] = connection.last('SetEffectControl') as [number, string, number];
    expect([number, control]).toEqual([2, 'level']);
    expect(value).toBeCloseTo(50.2, 1);

    connection.emit('EffectPlaybackChanged', {
      number: 2, running: true, level: 255, speed: 60, size: 100, spread: 100, fading: false, phase: 0.5, bpm: 60, changed: false,
    });
    await fixture.whenStable();
    expect(page.querySelector('button[aria-label="Effect 2 key"]')?.classList).toContain('level-3');
    // the fader this page moved a moment ago keeps the page's level
    expect(store.effectPlaybacks().get(2)?.level).toBe(128);
  });

  it('on the Stage view a light in a running effect shows a change at once and keeps it a sixth of a second', async () => {
    await planWith(stageLight(1, 'Par', 1, 'D', '#ffffff', 2, 2));
    connection.emit('EffectsChanged', [anEffect(1, { name: 'Pulse', shape: 'pulse', lights: [1], speed: 120 })]);
    connection.emit('EffectPlaybackChanged', {
      number: 1, running: true, level: 255, speed: 120, size: 100, spread: 100, fading: false, phase: 0, bpm: 120, changed: false,
    });
    await show('/stage');
    const start = performance.now() + 1000; // after the looks the plan has shown so far
    let now = start;
    const clock = vi.spyOn(performance, 'now').mockImplementation(() => now);
    // the desk program's playback at a moment, and the lens the plan shows then
    const at = async (ms: number, level: number): Promise<string> => {
      now = start + ms;
      connection.emit('PlaybackChanged', { ...connection.playback, effectLevels: { channels: [1], levels: [level] } });
      await fixture.whenStable();
      return lenses(1)[0];
    };
    try {
      const dark = await at(0, 0);
      expect(await at(200, 0)).toBe(dark);
      // a short flash comes: it shows at once, whenever it comes ...
      const lit = await at(300, 255);
      expect(lit).not.toBe(dark);
      // ... and stays a sixth of a second on the screen, though it is over sooner
      expect(await at(390, 0)).toBe(lit);
      expect(await at(470, 0)).toBe(dark);
      expect(await at(800, 255)).toBe(lit);
    } finally {
      clock.mockRestore();
    }
  });

  it('a key of an effect that is not there says so and sends nothing', async () => {
    store.toggleEffect(7);
    await fixture.whenStable();
    expect(store.notice()).toContain('There is no effect 7 yet');
    expect(connection.methods()).not.toContain('SetEffectControl');
  });

  it('the channels show what the running effects give, as the desk program mixes them', async () => {
    connection.emit('PlaybackChanged', { ...connection.playback, effectLevels: { channels: [1, 6], levels: [200, 90] } });
    await fixture.whenStable();
    expect(store.shownChannels()[0]).toBe(200);
    expect(store.shownChannels()[5]).toBe(90);
    expect(store.output()[0]).toBe(200);

    connection.emit('PlaybackChanged', { ...connection.playback, effectLevels: null });
    await fixture.whenStable();
    expect(store.shownChannels()[0]).toBe(0);
  });

  it('Settings shows effect keys in the independents and a row of effect faders on the main view', async () => {
    connection.emit('EffectsChanged', [anEffect(1, { name: 'Circle' })]);
    const settings = TestBed.inject(DeskSettings);
    settings.setEffectKeys(2);
    settings.setEffectRows(1);
    await show('/memories');
    await show('/');
    await until(() => !!page.querySelector('lm-main-view lm-effect-section'));
    await fixture.whenStable();

    const key = page.querySelector<HTMLButtonElement>('button[aria-label="Independent 21, effect 1, Circle"]');
    expect(key).toBeTruthy();
    await press(key);
    expect(connection.last('SetEffectControl')).toEqual([1, 'toggle', 0]);
    expect(page.querySelectorAll('lm-main-view lm-effect-section lm-fader-strip').length).toBeGreaterThan(0);

    // Edit binds an effect key to another effect
    await press(button('Edit the channels, memories and effects of the independent keys'));
    const field = page.querySelector<HTMLInputElement>('input[aria-label="Effect of independent key 21"]')!;
    field.value = '5';
    field.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetEffectIndependent')).toEqual([1, 5]);
  });

  it('every effect key, effect key of the independents and the Effects view has a shortcut', () => {
    for (const id of ['view.effects', 'effect.1', 'effect.64', 'ind.effect.32', 'page.effect.2', 'effects.keep', 'effects.reset', 'effects.copy', 'effects.delete']) {
      expect(actionWithId(id)).toBeTruthy();
    }
    expect(ACTION_GROUPS).toContain('Effect keys');
  });
});