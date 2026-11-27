import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from '../app';
import { routes } from '../app.routes';
import { DeskConnection } from '../desk/desk-connection';
import { SETTINGS_STORAGE_KEY } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import {
  AutosaveOptions,
  DeskState,
  EMPTY_STAGE,
  FoundNode,
  ImportAnswer,
  LanAccess,
  MEMORY_COUNT,
  NetworkChoice,
  Playback,
  ProjectList,
  Sequence,
  StackPlayback,
  StackState,
  StageLight,
  StagePlan,
  TimedStep,
  UNIVERSE_SIZE,
  Welcome,
} from '../desk/desk-types';
import { Effect } from '../effects/effects';
import { FoundMixer, newSound, Sound, SoundOutput, SoundSetup } from '../sounds/sounds';
import {
  type MidiMapping,
  type MidiMonitorLine,
  type MidiSetup,
  type MidiState,
} from '../midi/midi';
import { NOTE_LINE_SEPARATOR } from '../desk/notes';
import type {
  BackupSetup,
  BackupView,
  RdmBoxView,
  RdmLightChangeSent,
  RdmLightView,
  UpdateInfo,
  UpdateStep,
} from '../desk/hub-contract';

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
export class FakeConnection {
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
    stack: {
      currentId: null,
      nextId: null,
      state: 'idle',
      paused: false,
      remaining: 0,
      live: false,
    },
  };

  /** LAN as the backend answers "GetLanAccess": this page runs on the PC, and LAN is off. */
  lan: LanAccess = { local: true, on: false, addresses: ['192.168.1.20:5080'], password: null };

  /** What the backend answers to "ImportProject", or the message it refuses it with. */
  importAnswer: ImportAnswer = {
    project: { name: 'Hamlet', path: null, unsaved: true },
    report: [
      'Hamlet.asc: 1 cue.',
      'Memory 1 holds the cue.',
      'Nothing is saved yet: Save keeps the project.',
    ],
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
    stage: {
      width: 12,
      depth: 8,
      lights: [{ name: 'Wash', address: 1, channels: 'RGB', x: 2, y: 2 }],
    },
  });
  /** The projects and the examples, as the backend answers "GetProjects". */
  projectList: ProjectList = {
    folder: 'C:\\Lumenet',
    projects: [],
    examples: ['1 Looks and cues', '6 A whole concert'],
  };
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
  networks: NetworkChoice[] = [
    { network: '192.168.1.0/24', address: '192.168.1.20', adapter: 'Wi-Fi', router: true },
  ];
  /** The effects of the project, as the backend answers "GetEffects": none at first. */
  effects: Effect[] = [];
  /** The sounds of the project, as the backend answers "GetSounds": none at first. */
  sounds: Sound[] = [];
  /** The device of the sound, as the backend answers "GetSoundOutput": none (a page not on the PC) unless a test gives it. */
  soundOutput: SoundOutput | undefined = undefined;
  /** The playlists of the mixer's USB stick and their tracks, as "GetMixerPlaylists" and "GetMixerTracks" answer. */
  mixerPlaylists: string[] = [];
  mixerTracks: Record<string, string[]> = {};
  /** The mixers the key Find of Settings finds, as "FindMixers" answers. */
  mixersFound: FoundMixer[] = [];
  /** The MIDI of the PC, as the backend answers "GetMidi": none (a desk program from before MIDI) unless a test gives it. */
  midi: MidiState | undefined = undefined;
  /** The messages that came in from the controllers, as "WatchMidi" answers. */
  midiLines: MidiMonitorLine[] = [];
  /** The backups of the PC, as the backend answers "GetBackup" and "SetBackup": no folder yet, Google Drive found. */
  backup: BackupView = {
    folder: '',
    onSave: true,
    keep: 30,
    last: null,
    lastAt: null,
    count: 0,
    problem: null,
    googleDrive: 'G:\\My Drive\\Lumenet backups',
  };
  /** The versions, as the backend answers "CheckUpdate": this one is the newest. */
  update: UpdateInfo = {
    current: '0.1.0',
    latest: '0.1.0',
    newer: false,
    notes: null,
    published: null,
    size: null,
    canUpdate: false,
    why: null,
    problem: null,
  };
  /** The boxes as "GetRdmBoxes" answers: none unless a test gives them. */
  rdmBoxes: RdmBoxView[] = [];
  /** The lights with RDM as "GetRdmLight" answers them, by "<box> <id>". */
  rdmLights: Record<string, RdmLightView> = {};
  /** What a light refuses "SetRdmLight" with; null: it takes the change. */
  rdmRefusal: string | null = null;
  /** The steps "InstallUpdate" answers with: the download, then the version it starts again as. */
  updateSteps: UpdateStep[] = [
    { percent: 40, version: null },
    { percent: 100, version: null },
    { percent: 100, version: '0.1.45' },
  ];

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
    if (method === 'SaveProject')
      return {
        name: args[0],
        path: `${this.projectList.folder}\\${String(args[0])}.json`,
        unsaved: false,
      };
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
    if (method === 'GetSounds') return this.sounds;
    if (method === 'GetSoundOutput') return this.soundOutput;
    if (method === 'SetSoundOutput')
      return (this.soundOutput = { ...this.soundOutput!, ...(args[0] as SoundSetup) });
    if (method === 'GetMixerPlaylists') return this.mixerPlaylists;
    if (method === 'GetMixerTracks') return this.mixerTracks[args[0] as string] ?? [];
    if (method === 'FindMixers') return this.mixersFound;
    if (method === 'PreviewEffect') return null;
    if (method === 'GetMidi') return this.midi;
    if (method === 'WatchMidi') return args[0] ? this.midiLines : undefined;
    if (method === 'GetBackup') return this.backup;
    if (method === 'SetBackup')
      return (this.backup = { ...this.backup, ...(args[0] as BackupSetup) });
    if (method === 'CheckUpdate') return this.update;
    if (method === 'GetRdmBoxes') return this.rdmBoxes;
    if (method === 'GetRdmLight') return this.rdmLights[`${String(args[0])} ${String(args[1])}`];
    if (method === 'SetRdmLight')
      return this.setRdmLight(args[0] as string, args[1] as string, args[2] as RdmLightChangeSent);
    if (method === 'SearchRdm') return undefined;
    if (this.midi) return this.midiAnswer(this.midi, method, args);
    return undefined;
  }

  // A light with RDM after a change, as the desk program reads it again from the light.
  private setRdmLight(box: string, id: string, change: RdmLightChangeSent): RdmLightView {
    if (this.rdmRefusal) throw new Error(this.rdmRefusal);
    const key = `${box} ${id}`;
    const light = this.rdmLights[key];
    const mode = change.mode ?? light.mode;
    return (this.rdmLights[key] = {
      ...light,
      mode,
      channels: light.modes[mode - 1]?.channels ?? light.channels,
      address: change.address ?? light.address,
      label: change.label ?? light.label,
      identify: change.identify ?? light.identify,
    });
  }

  // The answers of a desk program with MIDI: the MIDI after the change.
  private midiAnswer(midi: MidiState, method: string, args: unknown[]): unknown {
    if (method === 'SetMidiMappings')
      return (this.midi = { ...midi, mappings: args[0] as MidiMapping[] });
    if (method === 'LearnMidi')
      return (this.midi = { ...midi, learning: args[0] as string | null });
    if (method === 'SetMidiDevice') {
      const [name, used] = args as [string, boolean];
      const devices = midi.devices.map((device) =>
        device.name === name
          ? { ...device, used, state: used ? ('open' as const) : ('unused' as const) }
          : device,
      );
      return (this.midi = { ...midi, devices });
    }
    if (method === 'GetMidiSetup') {
      return {
        devices: midi.devices.filter((device) => device.used).map((device) => device.name),
        mappings: midi.mappings,
      };
    }
    if (method === 'SetMidiSetup')
      return (this.midi = { ...midi, mappings: (args[0] as MidiSetup).mappings });
    if (method === 'SetOsc') {
      const [on, port] = args as [boolean, number];
      return (this.midi = {
        ...midi,
        osc: { on, port, state: on ? 'listening' : 'off', problem: null },
      });
    }
    return undefined;
  }

  /** A method that answers as a stream of one item (the search for Art-Net nodes): as invoke here. */
  ask(method: string, ...args: unknown[]): Promise<unknown> {
    return this.invoke(method, ...args);
  }

  /** A method that answers as a stream of several items: the steps of an update, one by one. */
  async follow(
    method: string,
    next: (item: unknown) => void,
    ...args: unknown[]
  ): Promise<unknown> {
    if (method !== 'InstallUpdate') {
      const answer = await this.invoke(method, ...args);
      next(answer);
      return answer;
    }
    this.calls.push({ method, args });
    for (const step of this.updateSteps) next(step);
    return this.updateSteps.at(-1);
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
export const QUESTIONS = [
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
  // the sounds of the project, and the device of the sound (asked for by the Sounds view)
  'GetSounds',
  'GetSoundOutput',
  // the MIDI controllers of the PC: asked for on connecting, the keys claimed when the page is used,
  // the lamps and the pages told, the list of messages watched while the MIDI panel is shown
  'GetMidi',
  'ClaimMidi',
  'ReportMidiLamps',
  'ReportMidiPages',
  'WatchMidi',
  // the backups and the newest version, asked for when their tabs of Settings open
  'GetBackup',
  'CheckUpdate',
  // the boxes and their lights with RDM, asked for when the RDM tab of Settings opens
  'GetRdmBoxes',
  'GetRdmLight',
];

/** The MIDI of the PC in the tests: a controller in use with two controls mapped, and one that is not plugged in. */
export const MIDI: MidiState = {
  works: true,
  devices: [
    {
      name: 'APC mini mk2',
      input: true,
      output: true,
      used: true,
      state: 'open',
      problem: null,
      simulated: false,
    },
    {
      name: 'nanoKONTROL2',
      input: true,
      output: true,
      used: true,
      state: 'missing',
      problem: null,
      simulated: false,
    },
  ],
  mappings: [
    {
      device: 'APC mini mk2',
      channel: 1,
      message: 'note',
      number: 56,
      target: 'stack.play',
      mode: 'absolute',
      pickup: false,
      feedback: false,
    },
    {
      device: 'APC mini mk2',
      channel: 1,
      message: 'cc',
      number: 48,
      target: 'fader.slot.channel.1',
      mode: 'absolute',
      pickup: true,
      feedback: false,
    },
  ],
  learning: null,
  presets: [
    {
      id: 'apc-mini-mk2',
      name: 'Akai APC mini mk2',
      device: 'APC mini mk2',
      about: 'The faders move the channels of the page shown.',
    },
  ],
};

/** A step on a channel, with all three times at one second unless said otherwise. */
export function step(number: number, value: number, more: Partial<TimedStep> = {}): TimedStep {
  return { target: 'channel', number, value, up: 1, wait: 1, down: 1, ...more };
}

/** Where the stack of the tests stands while it is not live, and once it is: on its first step, waiting for Play. */
export const NOT_LIVE: StackPlayback = {
  currentId: null,
  nextId: 11,
  state: 'idle',
  paused: false,
  remaining: 0,
  live: false,
};
export const LIVE: StackPlayback = {
  currentId: 11,
  nextId: 12,
  state: 'holding',
  paused: false,
  remaining: 0,
  live: true,
};

/** The page of a test: the fake backend, the page started on it, and the store the page uses. */
export interface DeskPage {
  readonly connection: FakeConnection;
  readonly fixture: ComponentFixture<App>;
  readonly page: HTMLElement;
  readonly store: DeskStore;
}

/**
 * Starts the desk page on a fake backend, as every test of the page does first: no settings in the
 * browser, memory 2 with a look, memory 3 with a sequence, two notes, a stack of two steps that is not
 * live, and the Main view shown.
 */
export async function startDeskPage(firstStart = false): Promise<DeskPage> {
  // Settings belong to the browser: each test starts with the usual ones, and with nothing set on
  // <html>. A device that keeps none yet starts with the window of the setups: only when the test asks.
  localStorage.clear();
  if (!firstStart) localStorage.setItem(SETTINGS_STORAGE_KEY, '{}');
  document.documentElement.removeAttribute('style');

  const connection = new FakeConnection();
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

  const fixture = TestBed.createComponent(App);
  const page = fixture.nativeElement as HTMLElement;
  const store = TestBed.inject(DeskStore);
  await TestBed.inject(Router).navigateByUrl('/');
  await fixture.whenStable();
  return { connection, fixture, page, store };
}

/** The helpers of the tests of the page, on the page of the test that runs, which `get` gives. */
export function deskPageHelpers(get: () => DeskPage) {
  /** Opens a view, as a click on its link in the top bar does. */
  async function show(path: string): Promise<void> {
    await TestBed.inject(Router).navigateByUrl(path);
    await get().fixture.whenStable();
  }

  function button(label: string): HTMLButtonElement {
    // A key is found by its full name for screen readers, or else by what is printed on it.
    const found = [...get().page.querySelectorAll('button')].find(
      (candidate) =>
        (candidate.getAttribute('aria-label') ?? candidate.textContent?.trim()) === label,
    );
    if (!found) throw new Error(`no button "${label}"`);
    return found;
  }

  /** A button inside one part of the page, a window for example, by its name or what it says. */
  function buttonIn(container: string, label: string): HTMLButtonElement {
    const found = [...get().page.querySelectorAll<HTMLButtonElement>(`${container} button`)].find(
      (candidate) =>
        (candidate.getAttribute('aria-label') ?? candidate.textContent?.trim()) === label,
    );
    if (!found) throw new Error(`no button "${label}" in ${container}`);
    return found;
  }

  // A key's name begins with "Memory 5 key"; while a sequence is recorded the steps it is in follow.
  function memoryKey(memory: number): HTMLButtonElement {
    const found = get().page.querySelector<HTMLButtonElement>(
      `button[aria-label^="Memory ${memory} key"]`,
    );
    if (!found) throw new Error(`no key for memory ${memory}`);
    return found;
  }

  async function press(element: HTMLElement | null): Promise<void> {
    if (!element) throw new Error('nothing to press');
    element.click();
    await get().fixture.whenStable();
  }

  /** The desk reports where the stack stands, as it does after every key of the stack. */
  async function stackStands(stack: StackPlayback): Promise<void> {
    get().connection.emit('PlaybackChanged', { ...get().connection.playback, stack });
    await get().fixture.whenStable();
  }

  /** Chooses a file in the import window, as the system's window for choosing files does. */
  async function pick(file: File): Promise<void> {
    const picker = get().page.querySelector<HTMLInputElement>(
      'lm-import-dialog input[type="file"]',
    );
    if (!picker) throw new Error('no window for choosing a file');
    Object.defineProperty(picker, 'files', { value: [file], configurable: true });
    picker.dispatchEvent(new Event('change'));
    await get().fixture.whenStable();
  }

  /** Checks (or unchecks) a part in the window of Import or Export, by what its label starts with. */
  async function check(
    dialog: 'lm-import-dialog' | 'lm-export-dialog',
    label: string,
  ): Promise<void> {
    const box = [...get().page.querySelectorAll<HTMLLabelElement>(`${dialog} .parts label`)]
      .find((item) => item.textContent?.trim().startsWith(label))
      ?.querySelector('input');
    if (!box) throw new Error(`no checkbox "${label}"`);
    box.click();
    await get().fixture.whenStable();
  }

  /** Waits for what the page does on its own, such as reading a file and waiting for the desk. */
  async function until(happened: () => boolean): Promise<void> {
    for (let round = 0; round < 50 && !happened(); round++) {
      await new Promise((resolve) => setTimeout(resolve, 0));
      await get().fixture.whenStable();
    }
    if (!happened()) throw new Error('it did not happen');
  }

  function textOf(selector: string): string | undefined {
    return get().page.querySelector(selector)?.textContent?.trim();
  }

  /** The desk as the backend sends it for a project of two universes: 1024 channels. */
  function twoUniverses(): DeskState {
    return {
      ...get().connection.welcome.desk,
      universes: 2,
      channels: new Array<number>(2 * UNIVERSE_SIZE).fill(0),
      channelNotes: new Array<string>(2 * UNIVERSE_SIZE).fill(''),
    };
  }

  /** Chooses a value of a list in the page and says so, as a person does. */
  async function choose(selector: string, value: string): Promise<void> {
    const list = get().page.querySelector<HTMLSelectElement>(selector);
    if (!list) throw new Error(`no list ${selector}`);
    list.value = value;
    list.dispatchEvent(new Event('change'));
    await get().fixture.whenStable();
  }

  /** The color of each lens of a light on the plan, as "r g b". */
  function lenses(id: number): string[] {
    return [...get().page.querySelectorAll<HTMLElement>(`[data-light="${id}"] .lens`)].map((lens) =>
      lens.style.getPropertyValue('--lens'),
    );
  }

  /** A round light of one square, as the plan keeps it. */
  function stageLight(
    id: number,
    name: string,
    address: number,
    channels: string,
    color: string | null,
    x: number,
    y: number,
  ): StageLight {
    return {
      id,
      name,
      address,
      channels,
      color,
      x,
      y,
      angle: 0,
      shape: 'round',
      width: 1,
      depth: 1,
    };
  }

  /** The desk program sends a plan with these lights, as after another page changed it. */
  async function planWith(...lights: StageLight[]): Promise<void> {
    get().connection.emit('StageChanged', { ...EMPTY_STAGE, lights } satisfies StagePlan);
    await get().fixture.whenStable();
  }

  /** A press on a light of the plan. (jsdom knows no PointerEvent; a MouseEvent of that name does the same here.) */
  async function pressLight(id: number): Promise<void> {
    get()
      .page.querySelector(`[data-light="${id}"]`)!
      .dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, button: 0 }));
    await get().fixture.whenStable();
  }

  /** A kind of keys in the Keyboard panel of Settings: the head that opens it. */
  function kind(group: string): HTMLButtonElement {
    const found = [
      ...get().page.querySelectorAll<HTMLButtonElement>('lm-keyboard-panel .head'),
    ].find((candidate) => candidate.querySelector('.name')?.textContent?.trim() === group);
    if (!found) throw new Error(`no kind of keys "${group}"`);
    return found;
  }

  /** Lets the page's own timers run: the store sends changes of an effect together, a moment later. */
  async function wait(ms: number): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, ms));
    await get().fixture.whenStable();
  }

  /** A sound of the project, as the desk program keeps it: a file of 12.5 seconds. */
  function aSound(number: number, change: Partial<Sound> = {}): Sound {
    const file = {
      hash: 'a'.repeat(32),
      name: 'rain.wav',
      seconds: 12.5,
      channels: 2,
      rate: 48000,
    };
    return { ...newSound(number, file), ...change };
  }

  return {
    show,
    button,
    buttonIn,
    memoryKey,
    press,
    stackStands,
    pick,
    check,
    until,
    textOf,
    twoUniverses,
    choose,
    lenses,
    stageLight,
    planWith,
    pressLight,
    kind,
    wait,
    aSound,
  };
}
