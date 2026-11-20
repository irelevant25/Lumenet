import { Service, computed, inject, signal } from '@angular/core';
import { DeskConnection } from './desk-connection';
import { DeskSettings, ProjectSettingsData } from './desk-settings';
import {
  AutosaveOptions,
  BoxStatus,
  CHANNEL_COUNT,
  CHANNEL_INDEPENDENT_COUNT,
  DEFAULT_LOOP,
  DeskState,
  HistoryState,
  ImportAnswer,
  LanAccess,
  MAX_LEVEL,
  MAX_SEQUENCE_STEPS,
  MEDIUM_BELOW_PX,
  MEMORY_COUNT,
  NARROW_BELOW_PX,
  MEMORY_INDEPENDENT_COUNT,
  MOBILE_BELOW_HEIGHT_PX,
  MOBILE_BELOW_WIDTH_PX,
  MemoryLooks,
  NEW_PROJECT_NAME,
  NO_HISTORY,
  PHONE_BELOW_PX,
  Playback,
  ProjectInfo,
  ProjectList,
  STRIPS_PER_ROW_MEDIUM,
  STRIPS_PER_ROW_WIDE,
  Sequence,
  Sequences,
  StackState,
  StackStep,
  StepTarget,
  TimedStep,
  Welcome,
  firstStep,
  pageStart,
  recordedStep,
  stepAfter,
} from './desk-types';
import {
  DeskCommand,
  FunctionMode,
  FunctionName,
  IDLE,
  MemoryContent,
  Step,
  pressChannel,
  pressFunction,
  pressMemory,
} from './function-mode';

/**
 * While a sequence is recorded: for every channel and memory it uses, the numbers of the
 * steps that use it, counted from 1. The keys show them.
 */
export interface RecordedSteps {
  readonly channels: ReadonlyMap<number, readonly number[]>;
  readonly memories: ReadonlyMap<number, readonly number[]>;
}

/** Where the stack stands, see DeskStore.stackPosition. */
export interface StackPosition {
  readonly previous: StackStep | null;
  readonly current: StackStep | null;
  readonly next: StackStep | null;
}

/** Fader moves are collected for this long and then sent together, the newest value of each fader only. */
const SEND_DELAY_MS = 15;

/** How long a notice stays in the top bar. */
const NOTICE_MS = 5000;

/** How long the notice stays that unsaved work was brought back: longer, it matters. */
const RECOVERED_NOTICE_MS = 15000;

/** The rate, a speed master for all sequences: from a tenth to ten times their own speed. */
export const MIN_RATE = 0.1;
export const MAX_RATE = 10;

/** Nothing runs: no sequence gives anything, and no step of the stack is on. */
const NO_PLAYBACK: Playback = {
  levels: [],
  sequenceSteps: {},
  stack: { currentId: null, nextId: null, state: 'idle', paused: false, remaining: 0 },
};

/**
 * The state of the desk as this page sees it, and everything the page can do with it.
 *
 * The backend holds the real state. The page changes its own copy at once, so that
 * faders feel direct, and tells the backend. Changes made elsewhere come back as messages.
 */
@Service()
export class DeskStore {
  private readonly connection = inject(DeskConnection);

  // ---- what the backend holds
  readonly channels = signal<readonly number[]>(zeros(CHANNEL_COUNT));
  readonly memoryLevels = signal<readonly number[]>(zeros(MEMORY_COUNT));
  readonly memoryStored = signal<readonly boolean[]>(allFalse(MEMORY_COUNT));
  /** What every stored memory holds. Fetched again whenever the backend sends the whole desk. */
  readonly memoryLooks = signal<MemoryLooks>({});
  /** False while the looks could not be fetched: a desk program from before the page knew them. */
  readonly memoryLooksKnown = signal(false);
  /** Whether what each memory holds is a sequence (and not a look). */
  readonly memorySequence = signal<readonly boolean[]>(allFalse(MEMORY_COUNT));
  /** The sequences, by the number of the memory that holds each. */
  readonly sequences = signal<Sequences>({});
  /** The one stack of the project. */
  readonly stack = signal<StackState>({ steps: [] });
  /** What changes with time: what the running sequences give, and where the stack stands. */
  readonly playback = signal<Playback>(NO_PLAYBACK);
  /** False while the desk program is one from before sequences and the stack. */
  readonly playbackKnown = signal(false);
  /** The notes under the faders, like tape labels on a desk. An empty text means no note. */
  readonly channelNotes = signal<readonly string[]>(noNotes(CHANNEL_COUNT));
  readonly memoryNotes = signal<readonly string[]>(noNotes(MEMORY_COUNT));
  /** The channel each of the 10 channel keys of the independents switches: 1 to 10 until the owner changes them. */
  readonly independents = signal<readonly number[]>(oneToCount(CHANNEL_INDEPENDENT_COUNT));
  /** The memory each of the 10 memory keys of the independents switches: 1 to 10 until the owner changes them. */
  readonly memoryIndependents = signal<readonly number[]>(oneToCount(MEMORY_INDEPENDENT_COUNT));
  readonly master = signal(MAX_LEVEL);
  readonly blackout = signal(false);
  readonly box = signal<BoxStatus>({ connected: false, host: '', firmware: null });
  readonly project = signal<ProjectInfo>({ name: NEW_PROJECT_NAME, path: null, unsaved: false });
  readonly version = signal('');
  /**
   * Whether other devices in the network may use the desk, and the address and password they
   * need. Null while it is not known: before the first answer, or with a desk program from
   * before LAN.
   */
  readonly lan = signal<LanAccess | null>(null);
  /** What undo and redo would do now. */
  readonly history = signal<HistoryState>(NO_HISTORY);

  /**
   * What each channel fader shows: its own level, or what the memories whose faders are up
   * give the channel, whichever is higher. A look gives a fixed level; a running sequence
   * gives what the backend reports from moment to moment. The backend works out the output
   * the same way, before the master.
   */
  readonly shownChannels = computed<readonly number[]>(() => {
    const own = this.channels();
    const levels = this.memoryLevels();
    let shown: number[] | null = null;
    for (const [memory, look] of Object.entries(this.memoryLooks())) {
      const level = levels[Number(memory) - 1];
      if (!level) continue;

      shown ??= own.slice();
      for (let index = 0; index < look.length; index++) {
        const part = Math.floor((look[index] * level + 127) / MAX_LEVEL); // rounded like the backend does
        if (part > shown[index]) shown[index] = part;
      }
    }

    const running = this.playback().levels;
    for (let index = 0; index < running.length; index++) {
      if (running[index] > (shown ?? own)[index]) {
        shown ??= own.slice();
        shown[index] = running[index];
      }
    }
    return shown ?? own;
  });

  /**
   * True while the stack is live. Then the desk plays a show and nothing that is stored can be
   * changed: no memory, sequence, step of the stack, note or independent key. The faders, the
   * master, Clear and Blackout work as always.
   */
  readonly live = computed(() => this.playback().stack.live === true);

  /**
   * Where the stack stands: the step that is on, the one before it in the stack (where Previous
   * goes; before the first comes the last), and the one Play goes to. Null where there is none:
   * no step is on, or the stack has no other step.
   */
  readonly stackPosition = computed<StackPosition>(() => {
    const steps = this.stack().steps;
    const { currentId, nextId } = this.playback().stack;
    const index = currentId === null ? -1 : steps.findIndex((step) => step.id === currentId);
    return {
      previous: index >= 0 && steps.length > 1 ? steps[(index - 1 + steps.length) % steps.length] : null,
      current: index >= 0 ? steps[index] : null,
      next: nextId === null ? null : (steps.find((step) => step.id === nextId) ?? null),
    };
  });

  /** True while the stack moves by itself: a fade or a countdown that is not paused. */
  readonly stackRunning = computed(() => {
    const { state, paused } = this.playback().stack;
    return !paused && (state === 'fading' || state === 'waiting');
  });

  /** True while the stack is paused. */
  readonly stackPaused = computed(() => this.playback().stack.paused);

  // ---- what only this page knows
  /** The settings of this browser: the channel size among them. */
  private readonly settings = inject(DeskSettings);
  /**
   * Faders in one row as the window size has it: 32 on a wide window, 16 on a narrower one (the
   * limit is in desk-types.ts). The store follows the window; the tests set it themselves.
   */
  readonly windowStripsPerRow = signal(STRIPS_PER_ROW_WIDE);
  /**
   * Faders in one row of the main view: as the window size has it, or the channel size chosen in
   * Settings spread over the rows of channels. A row that is then too wide for the window
   * scrolls sideways.
   */
  readonly stripsPerRow = computed(() => {
    const size = this.settings.channelSize();
    return size === 'auto' ? this.windowStripsPerRow() : size / this.settings.channelRows();
  });
  /** True in the mobile layout, where the sections stand under each other and the page scrolls. */
  readonly mobile = signal(false);
  /** True on a phone held upright, where a row of faders is shown as lines of 8. */
  readonly phone = signal(false);
  /** The memory the Memories view shows the content of. */
  readonly inspectedMemory = signal(1);
  /** A page of channels on the main view is its rows of channel faders (two at first), a page of memories its rows of memory faders (one). */
  readonly channelsPerPage = computed(() => this.settings.channelRows() * this.stripsPerRow());
  readonly memoriesPerPage = computed(() => this.settings.memoryRows() * this.stripsPerRow());
  /** A page of memories on the Memories and Sequences views: one row, as the window has it, whatever Settings says for the main view. */
  readonly viewMemoriesPerPage = computed(() => this.windowStripsPerRow());

  /** A channel and a memory that must be on the pages shown. The pages follow the window size. */
  private readonly wantedChannel = signal(1);
  private readonly wantedMemory = signal(1);
  /** The first channel of the page that is shown. */
  readonly firstChannel = computed(() => pageStart(this.wantedChannel(), this.channelsPerPage()));
  /** The first memory of the page that is shown on the main view. */
  readonly firstMemory = computed(() => pageStart(this.wantedMemory(), this.memoriesPerPage()));
  /** The first memory of the page that is shown on the Memories and Sequences views. */
  readonly viewFirstMemory = computed(() => pageStart(this.wantedMemory(), this.viewMemoriesPerPage()));
  /** Where a record key (save or edit a memory, record a sequence, copy, delete) stands. */
  readonly mode = signal<FunctionMode>(IDLE);
  /** The steps of the sequence that is being recorded, by what they use; null while none is recorded. */
  readonly recordedSteps = computed<RecordedSteps | null>(() => {
    const mode = this.mode();
    if (mode.kind !== 'recording') return null;

    const channels = new Map<number, number[]>();
    const memories = new Map<number, number[]>();
    (this.sequences()[mode.memory]?.steps ?? []).forEach((step, index) => {
      const users = step.target === 'channel' ? channels : memories;
      const numbers = users.get(step.number);
      if (numbers) numbers.push(index + 1);
      else users.set(step.number, [index + 1]);
    });
    return { channels, memories };
  });
  /** A short message for the top bar, for example an error from the backend. */
  readonly notice = signal('');
  /**
   * True while SOLO is lit: the keys under the faders and the independent keys put their fader
   * to full only while they are held, instead of switching it on and off.
   */
  readonly soloMode = signal(false);
  // The faders a held key holds at full now, by "channel 5" or "memory 3": the level each had before.
  private readonly held = new Map<string, number>();
  /** The rate, a speed master for all sequences (RATE and its wheel), and whether it counts. */
  readonly rate = signal(1);
  readonly rateOn = signal(false);
  /** The autosave of the desk program: on or off, and how often; null while it is not known. */
  readonly autosave = signal<AutosaveOptions | null>(null);
  // True once this page said that unsaved work was brought back.
  private recoveryTold = false;

  private readonly loaded = signal(false);

  /** True when the page is connected to the desk program and has received its state. */
  readonly online = computed(() => this.connection.online() && this.loaded());

  /** Fader moves waiting to be sent: one entry per fader, so only its newest value goes out. */
  private readonly outbox = new Map<string, { method: string; args: unknown[] }>();
  private outboxTimer: ReturnType<typeof setTimeout> | null = null;
  private noticeTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    const connection = this.connection;

    connection.on<[number, number]>('ChannelChanged', (channel, level) =>
      this.channels.update((levels) => withItem(levels, channel - 1, level)),
    );
    connection.on<[number, number]>('MemoryLevelChanged', (memory, level) =>
      this.memoryLevels.update((levels) => withItem(levels, memory - 1, level)),
    );
    connection.on<[number, string]>('ChannelNoteChanged', (channel, text) =>
      this.channelNotes.update((notes) => withItem(notes, channel - 1, text)),
    );
    connection.on<[number, string]>('MemoryNoteChanged', (memory, text) =>
      this.memoryNotes.update((notes) => withItem(notes, memory - 1, text)),
    );
    connection.on<[number, number]>('IndependentChanged', (key, channel) =>
      this.independents.update((channels) => withItem(channels, key - 1, channel)),
    );
    connection.on<[number, number]>('MemoryIndependentChanged', (key, memory) =>
      this.memoryIndependents.update((memories) => withItem(memories, key - 1, memory)),
    );
    connection.on<[number]>('MasterChanged', (level) => this.master.set(level));
    connection.on<[boolean]>('BlackoutChanged', (on) => this.blackout.set(on));
    connection.on<[DeskState]>('DeskChanged', (desk) => this.takeDesk(desk));
    connection.on<[BoxStatus]>('BoxChanged', (box) => this.box.set(box));
    connection.on<[ProjectInfo]>('ProjectChanged', (project) => this.project.set(project));
    connection.on<[StackState]>('StackChanged', (stack) => this.stack.set(stack));
    connection.on<[Playback]>('PlaybackChanged', (playback) => this.takePlayback(playback));
    connection.on<[LanAccess]>('LanAccessChanged', (lan) => this.lan.set(lan));
    connection.on<[HistoryState]>('HistoryChanged', (history) => this.history.set(history));
    connection.on<[boolean, number]>('RateChanged', (on, rate) => {
      this.rateOn.set(on);
      this.rate.set(rate);
    });
    connection.on<[AutosaveOptions]>('AutosaveChanged', (options) => this.autosave.set(options));
    // The project's settings: another page on the PC changed them, or a project was loaded.
    // A phone keeps its own.
    connection.on<[unknown]>('SettingsChanged', (settings) => {
      if (this.lan()?.local && settings) this.settings.takeProject(settings);
    });
    connection.on<[number, number]>('SequenceSpeedChanged', (memory, speed) =>
      this.sequences.update((all) => (all[memory] ? { ...all, [memory]: { ...all[memory], speed } } : all)),
    );

    connection.whenConnected(() => void this.refresh());
    connection.start();

    this.followWindowSize();
  }

  // ---------------------------------------------------------------- pages

  /** Shows the page of channels that starts at `first`. */
  showChannelPage(first: number): void {
    this.wantedChannel.set(first);
  }

  /** Shows the page of memories that starts at `first`. */
  showMemoryPage(first: number): void {
    this.wantedMemory.set(first);
  }

  // ---------------------------------------------------------------- faders

  setChannel(channel: number, level: number): void {
    if (this.channels()[channel - 1] === level) return;
    this.channels.update((levels) => withItem(levels, channel - 1, level));
    this.sendSoon(`channel ${channel}`, 'SetChannel', channel, level);
  }

  setMemoryLevel(memory: number, level: number): void {
    if (this.memoryLevels()[memory - 1] === level) return;
    this.memoryLevels.update((levels) => withItem(levels, memory - 1, level));
    this.sendSoon(`memory ${memory}`, 'SetMemoryLevel', memory, level);
  }

  setMaster(level: number): void {
    if (this.master() === level) return;
    this.master.set(level);
    this.sendSoon('master', 'SetMaster', level);
  }

  // ---------------------------------------------------------------- notes under the faders

  // A note arrives here as joinNote made it. It is not trimmed again: a note with text only in
  // its second line starts with the line separator, and trimming would take that away.
  setChannelNote(channel: number, note: string): void {
    if (this.channelNotes()[channel - 1] === note) return;
    this.channelNotes.update((notes) => withItem(notes, channel - 1, note));
    this.run('SetChannelNote', channel, note);
  }

  setMemoryNote(memory: number, note: string): void {
    if (this.memoryNotes()[memory - 1] === note) return;
    this.memoryNotes.update((notes) => withItem(notes, memory - 1, note));
    this.run('SetMemoryNote', memory, note);
  }

  // ---------------------------------------------------------------- independent keys

  /**
   * A channel key of the independents switches its channel: to zero when it is up, to full when
   * it is at zero. In SOLO mode the key holds its fader at full instead, while it is held (holdIndependent).
   */
  toggleIndependent(key: number): void {
    if (this.soloMode()) return;
    const channel = this.independents()[key - 1];
    this.setChannel(channel, this.channels()[channel - 1] > 0 ? 0 : MAX_LEVEL);
  }

  /** A memory key of the independents switches the fader of its memory, the same way. */
  toggleMemoryIndependent(key: number): void {
    if (this.soloMode()) return;
    const memory = this.memoryIndependents()[key - 1];
    this.setMemoryLevel(memory, this.memoryLevels()[memory - 1] > 0 ? 0 : MAX_LEVEL);
  }

  // ---------------------------------------------------------------- solo

  /** SOLO: lit, the keys under the faders and the independent keys hold their fader at full while held; dark, they switch it on and off. */
  toggleSolo(): void {
    if (this.soloMode()) this.releaseHeld();
    this.soloMode.update((on) => !on);
  }

  /**
   * The key under the fader of a channel or a memory goes down (true) or up again (false). In
   * SOLO mode the fader is at full while the key is held, and back where it was when it is let
   * go. Not while a record key wants the key: a memory key while one waits for a memory, a
   * channel key while a sequence is recorded.
   */
  holdKey(target: StepTarget, number: number, down: boolean): void {
    if (!down) this.holdOff(target, number);
    else if (this.soloMode() && this.keyIsFree(target)) this.holdOn(target, number);
  }

  /** The same for a key of the independents: it holds the channel or the memory it is bound to at full. */
  holdIndependent(kind: StepTarget, key: number, down: boolean): void {
    const number = kind === 'channel' ? this.independents()[key - 1] : this.memoryIndependents()[key - 1];
    if (number === undefined) return;
    if (!down) this.holdOff(kind, number);
    else if (this.soloMode()) this.holdOn(kind, number);
  }

  // True when a key under a fader is free to hold its fader: no record key wants it.
  private keyIsFree(target: StepTarget): boolean {
    const kind = this.mode().kind;
    return target === 'channel' ? kind !== 'recording' : kind === 'idle';
  }

  private holdOn(target: StepTarget, number: number): void {
    const name = `${target} ${number}`;
    if (this.held.has(name)) return;

    const level = target === 'channel' ? this.channels()[number - 1] : this.memoryLevels()[number - 1];
    this.held.set(name, level);
    this.setLevel(target, number, MAX_LEVEL);
  }

  private holdOff(target: StepTarget, number: number): void {
    const name = `${target} ${number}`;
    const level = this.held.get(name);
    if (level === undefined) return;

    this.held.delete(name);
    this.setLevel(target, number, level);
  }

  // Every fader a key holds at full goes back, as if its key were let go.
  private releaseHeld(): void {
    for (const name of [...this.held.keys()]) {
      const [target, number] = name.split(' ');
      this.holdOff(target as StepTarget, Number(number));
    }
  }

  private setLevel(target: StepTarget, number: number, level: number): void {
    if (target === 'channel') this.setChannel(number, level);
    else this.setMemoryLevel(number, level);
  }

  // ---------------------------------------------------------------- the rate

  /**
   * RATE: lit, every sequence plays at its own speed times the rate (the wheel under the key
   * sets it); dark, at its own speed. A speed master, like the master fader: it is not saved
   * with the project.
   */
  toggleRate(): void {
    this.rateOn.update((on) => !on);
    this.sendSoon('rate', 'SetRate', this.rateOn(), this.rate());
  }

  /** Sets the rate, from 0.1 to 10 (the wheel); it counts while RATE is lit. */
  setRate(rate: number): void {
    const kept = Math.min(MAX_RATE, Math.max(MIN_RATE, rate));
    if (kept === this.rate()) return;
    this.rate.set(kept);
    this.sendSoon('rate', 'SetRate', this.rateOn(), kept);
  }

  // ---------------------------------------------------------------- autosave

  /** Switches the autosave and sets how often it keeps the work (on the PC only). */
  setAutosave(on: boolean, seconds: number): void {
    this.autosave.set({ on, seconds });
    this.connection
      .invoke<AutosaveOptions | undefined>('SetAutosave', on, seconds)
      .then((options) => {
        if (options) this.autosave.set(options);
      })
      .catch((error: Error) => this.say(error.message));
  }

  // ---------------------------------------------------------------- undo and redo

  /**
   * Undo: what the project holds goes back to how it was before its last change. Memories,
   * sequences, the stack, notes and the independent keys count; the faders and Clear do not.
   * Locked while the stack is live, as every change of what is stored is.
   */
  undo(): void {
    this.moveInHistory('Undo', 'Undone');
  }

  /** Redo: a change that was undone is made again. */
  redo(): void {
    this.moveInHistory('Redo', 'Redone');
  }

  private moveInHistory(method: 'Undo' | 'Redo', done: string): void {
    if (this.live()) return;
    // Fader moves of just before reach the desk first: a memory may be stored again by redo.
    this.flushOutbox();
    this.connection
      .invoke<string>(method)
      .then((what) => this.say(`${done}: ${what}.`))
      .catch((error: Error) => this.say(error.message));
  }

  /** Binds a channel key of the independents to another channel. */
  setIndependent(key: number, channel: number): void {
    if (this.independents()[key - 1] === channel) return;
    this.independents.update((channels) => withItem(channels, key - 1, channel));
    this.run('SetIndependent', key, channel);
  }

  /** Binds a memory key of the independents to another memory. */
  setMemoryIndependent(key: number, memory: number): void {
    if (this.memoryIndependents()[key - 1] === memory) return;
    this.memoryIndependents.update((memories) => withItem(memories, key - 1, memory));
    this.run('SetMemoryIndependent', key, memory);
  }

  // ---------------------------------------------------------------- top bar buttons

  /** Blackout sends zero to every address but leaves the desk as it is. */
  toggleBlackout(): void {
    const on = !this.blackout();
    this.blackout.set(on);
    this.connection.send('SetBlackout', on);
  }

  /** Cancels an unfinished function and brings every channel fader and memory fader to zero. */
  clear(): void {
    this.mode.set(IDLE);
    this.dropOutbox(); // fader moves that are still waiting must not arrive after the clear
    this.channels.set(zeros(CHANNEL_COUNT));
    this.memoryLevels.set(zeros(MEMORY_COUNT));
    this.run('Clear');
  }

  // ---------------------------------------------------------------- the record keys

  pressFunction(name: FunctionName): void {
    if (this.live()) return; // a live stack locks what is stored
    this.follow(pressFunction(this.mode(), name));
  }

  pressMemory(memory: number): void {
    if (this.soloMode() && this.keyIsFree('memory')) return; // the key holds its fader at full while it is held, see holdKey
    this.follow(pressMemory(this.mode(), memory, this.memoryContent(memory)));
  }

  /** The key under a channel fader: its fader to full and back to zero, or a step while a sequence is recorded. */
  pressChannel(channel: number): void {
    if (this.soloMode() && this.keyIsFree('channel')) return;
    this.follow(pressChannel(this.mode(), channel));
  }

  /** What a memory holds: nothing, a look, or a sequence. */
  memoryContent(memory: number): MemoryContent {
    if (!this.memoryStored()[memory - 1]) return 'nothing';
    return this.memorySequence()[memory - 1] ? 'sequence' : 'look';
  }

  // ---------------------------------------------------------------- sequences

  /**
   * Stores a sequence in a memory that is empty or holds a sequence already: its steps, and
   * how it is played. A sequence that runs starts again from its first step.
   */
  setSequence(memory: number, sequence: Sequence): void {
    const whole: Sequence = {
      loop: sequence.loop,
      random: sequence.random ?? false,
      speed: sequence.speed ?? 1,
      steps: sequence.steps,
    };
    this.sequences.update((all) => ({ ...all, [memory]: whole }));
    this.markStored(memory, 'sequence');
    this.run('SetSequence', memory, whole);
  }

  /**
   * Changes how fast the sequence of a memory is played. It goes on from where it is. The
   * speed fader sends its moves like any fader: collected for a moment, the newest only.
   */
  setSequenceSpeed(memory: number, speed: number): void {
    const old = this.sequences()[memory];
    if (!old || (old.speed ?? 1) === speed) return;
    this.sequences.update((all) => ({ ...all, [memory]: { ...old, speed } }));
    this.sendSoon(`speed ${memory}`, 'SetSequenceSpeed', memory, speed);
  }

  /**
   * Puts one more step at the end of the sequence in a memory. An empty memory gets a new
   * sequence, played in a loop. The backend adds the step by itself, so that two quick
   * presses cannot lose one.
   */
  addSequenceStep(memory: number, step: TimedStep): void {
    const old = this.sequences()[memory];
    if (old && old.steps.length >= MAX_SEQUENCE_STEPS) {
      this.say(`A sequence can have at most ${MAX_SEQUENCE_STEPS} steps.`);
      return;
    }

    // How it is played stays as it was; a new sequence loops, in turn, at speed 1.
    const sequence: Sequence = old ? { ...old, steps: [...old.steps, step] } : { loop: DEFAULT_LOOP, steps: [step] };
    this.sequences.update((all) => ({ ...all, [memory]: sequence }));
    this.markStored(memory, 'sequence');
    this.run('AddSequenceStep', memory, step);
  }

  // ---------------------------------------------------------------- the stack
  // The backend keeps the steps in order and gives them their numbers. After every change it
  // sends the whole stack to every page, this one included.

  /**
   * Switches the stack live or off. Going live brings every fader to zero and starts the first
   * step; from then on nothing that is stored can be changed. Switching it off stops the stack
   * and leaves the faders where they are.
   */
  setStackLive(on: boolean): void {
    this.mode.set(IDLE);
    if (on) this.dropOutbox(); // fader moves that are still waiting must not arrive after the faders went down
    this.run('SetStackLive', on);
  }

  addStackStep(step: TimedStep): void {
    this.run('AddStackStep', step);
  }

  /**
   * Puts a step at the end of the stack: the first one, or one like the last on the next
   * channel or memory. A new first step waits for Play, as a step of the stack mostly does.
   */
  addStackStepAtEnd(): void {
    if (this.live()) return; // a live stack keeps its steps
    const last = this.stack().steps.at(-1);
    this.addStackStep(last ? stepAfter(last) : firstStep(0));
  }

  updateStackStep(id: number, step: TimedStep): void {
    this.stack.update((stack) => ({
      steps: stack.steps.map((old) => (old.id === id ? { ...old, ...step } : old)),
    }));
    this.run('UpdateStackStep', id, step);
  }

  deleteStackStep(id: number): void {
    this.run('DeleteStackStep', id);
  }

  /** Puts a step right after another one; `null` puts it at the very top. It gets a new number there. */
  moveStackStep(id: number, afterId: number | null): void {
    this.run('MoveStackStep', id, afterId);
  }

  /** Gives a step the number typed for it, for example "3" or "2.1". */
  renumberStackStep(id: number, cue: string): void {
    this.run('RenumberStackStep', id, cue);
  }

  stackPlay(): void {
    this.run('StackPlay');
  }

  stackPause(): void {
    this.run('StackPause');
  }

  stackNext(): void {
    this.run('StackNext');
  }

  stackPrevious(): void {
    this.run('StackPrevious');
  }

  // ---------------------------------------------------------------- LAN

  /**
   * Lets other devices in the network use the desk (with the password), or cuts them off.
   * Only a page on the PC itself can do this.
   */
  setLan(on: boolean): void {
    const lan = this.lan();
    if (!lan?.local || lan.on === on) return;
    // Lit at once. The desk answers with the address and the password a moment later.
    this.lan.set({ ...lan, on, password: on ? lan.password : null });
    this.run('SetLanAccess', on);
  }

  /**
   * Chooses the address of this PC at which devices in the network may come in, or lets the
   * desk choose by itself (null). The devices that are connected connect again.
   */
  setLanAddress(address: string | null): void {
    if (!this.lan()?.local) return;
    this.run('SetLanAddress', address);
  }

  /**
   * Makes the typed text the LAN password: every device has to give the new one. Resolves when
   * the desk took it; rejects with the desk's message (too short, other characters) when it did not.
   */
  async setLanPassword(password: string): Promise<void> {
    await this.connection.invoke<void>('SetLanPassword', password);
  }

  /** Makes a new random LAN password. */
  newLanPassword(): void {
    if (!this.lan()?.local) return;
    this.run('NewLanPassword');
  }

  // ---------------------------------------------------------------- projects

  listProjects(): Promise<ProjectList> {
    return this.connection.invoke<ProjectList>('GetProjects');
  }

  async saveProject(name: string): Promise<void> {
    this.project.set(await this.connection.invoke<ProjectInfo>('SaveProject', name));
    this.say(`Project "${name}" saved`);
  }

  /** Starts an empty project: no memories, no notes, every fader at zero. */
  async newProject(): Promise<void> {
    this.mode.set(IDLE);
    this.dropOutbox();
    this.project.set(await this.connection.invoke<ProjectInfo>('NewProject'));
  }

  async loadProject(name: string): Promise<void> {
    this.mode.set(IDLE);
    this.project.set(await this.connection.invoke<ProjectInfo>('LoadProject', name));
  }

  /**
   * Opens the show of a theatre console, from a file in the USITT ASCII format, as a project in
   * place of the open one, as loading does. Gives back what the desk made of it, sentence by sentence.
   */
  async importShow(fileName: string, text: string): Promise<string[]> {
    this.mode.set(IDLE);
    const answer = await this.connection.invoke<ImportAnswer>('ImportProject', fileName, text);
    this.project.set(answer.project);
    return answer.report;
  }

  // ---------------------------------------------------------------- inner workings

  private follow(step: Step): void {
    this.mode.set(step.mode);
    if (step.notice) this.say(step.notice);
    if (step.command) this.carryOut(step.command);
  }

  private carryOut(command: DeskCommand): void {
    // Fader moves made just before must reach the backend first: a memory stores what the faders show.
    this.flushOutbox();

    switch (command.kind) {
      case 'saveMemory':
        this.markStored(command.memory, 'look');
        this.run('SaveMemory', command.memory);
        break;
      case 'loadMemory':
        this.run('LoadMemory', command.memory);
        break;
      case 'copyMemory':
        this.markStored(command.to, this.memoryContent(command.from));
        this.run('CopyMemory', command.from, command.to);
        break;
      case 'deleteMemory':
        this.markStored(command.memory, 'nothing');
        this.run('DeleteMemory', command.memory);
        break;
      case 'addStep':
        this.addSequenceStep(command.memory, recordedStep(command.target, command.number));
        break;
      case 'toggleFader':
        // The fader's own level counts: a channel a memory holds at full still goes to full by its key.
        if (command.target === 'channel') {
          const own = this.channels()[command.number - 1];
          this.setChannel(command.number, own === MAX_LEVEL ? 0 : MAX_LEVEL);
        } else {
          const level = this.memoryLevels()[command.number - 1];
          this.setMemoryLevel(command.number, level === MAX_LEVEL ? 0 : MAX_LEVEL);
        }
        break;
    }
  }

  /**
   * Gives the memory's key its look right away: lit or not, red or running into yellow. The
   * backend confirms with the full state a moment later.
   */
  private markStored(memory: number, holds: MemoryContent): void {
    this.memoryStored.update((flags) => withItem(flags, memory - 1, holds !== 'nothing'));
    this.memorySequence.update((flags) => withItem(flags, memory - 1, holds === 'sequence'));
  }

  /** Asks the backend to do something and shows its message if it refuses. */
  private run(method: string, ...args: unknown[]): void {
    this.connection.invoke<void>(method, ...args).catch((error: Error) => this.say(error.message));
  }

  private sendSoon(key: string, method: string, ...args: unknown[]): void {
    this.outbox.set(key, { method, args });
    this.outboxTimer ??= setTimeout(() => this.flushOutbox(), SEND_DELAY_MS);
  }

  private flushOutbox(): void {
    const waiting = [...this.outbox.values()];
    this.dropOutbox();
    for (const { method, args } of waiting) this.connection.send(method, ...args);
  }

  private dropOutbox(): void {
    if (this.outboxTimer !== null) clearTimeout(this.outboxTimer);
    this.outboxTimer = null;
    this.outbox.clear();
  }

  private say(text: string, ms = NOTICE_MS): void {
    this.notice.set(text);
    if (this.noticeTimer !== null) clearTimeout(this.noticeTimer);
    this.noticeTimer = setTimeout(() => this.notice.set(''), ms);
  }

  /** Fetches the whole state. Runs on every connect, so a reconnect cannot leave the page out of date. */
  private async refresh(): Promise<void> {
    try {
      const welcome = await this.connection.invoke<Welcome>('Hello');
      this.version.set(welcome.version);
      this.takeDesk(welcome.desk);
      this.box.set(welcome.box);
      this.project.set(welcome.project);
      this.loaded.set(true);
      void this.fetchLan();
      void this.fetchHistory();
      void this.fetchAutosave();
      void this.tellRecovered();
    } catch {
      // The connection dropped again before the answer arrived. The next connect asks again.
    }
  }

  private async fetchLan(): Promise<void> {
    try {
      this.lan.set((await this.connection.invoke<LanAccess | undefined>('GetLanAccess')) ?? null);
    } catch {
      // A desk program from before LAN does not know the question. Then the page has no LAN key.
      this.lan.set(null);
    }
    await this.joinProjectSettings();
  }

  /**
   * On the PC the desk program runs on, the layout and the shortcuts belong to the project: the
   * page takes the project's, or, while the desk program has none (after a start without a
   * project), hands it its own. From then on every change goes there too. A phone or a tablet
   * keeps its settings to itself.
   */
  private async joinProjectSettings(): Promise<void> {
    if (!this.lan()?.local) {
      this.settings.followProject(null);
      return;
    }

    try {
      let project = await this.connection.invoke<unknown>('GetSettings');
      if (project === null || project === undefined) {
        const taken = await this.connection.invoke<boolean>('AdoptSettings', this.settings.projectSettings());
        if (!taken) project = await this.connection.invoke<unknown>('GetSettings');
      }
      if (project !== null && project !== undefined) this.settings.takeProject(project);
      this.settings.followProject((settings: ProjectSettingsData) => this.run('SetSettings', settings));
    } catch {
      // A desk program from before settings in the project: this browser keeps them, as before.
      this.settings.followProject(null);
    }
  }

  private async fetchHistory(): Promise<void> {
    try {
      this.history.set((await this.connection.invoke<HistoryState | undefined>('GetHistory')) ?? NO_HISTORY);
    } catch {
      // A desk program from before undo: nothing can be undone.
      this.history.set(NO_HISTORY);
    }
  }

  private async fetchAutosave(): Promise<void> {
    try {
      this.autosave.set((await this.connection.invoke<AutosaveOptions | undefined>('GetAutosave')) ?? null);
    } catch {
      // A desk program from before the autosave options.
      this.autosave.set(null);
    }
  }

  // Once per page: unsaved work that the desk program brought back at its start.
  private async tellRecovered(): Promise<void> {
    if (this.recoveryTold) return;
    try {
      const kept = await this.connection.invoke<string | null | undefined>('GetRecovered');
      if (!kept) return;
      this.recoveryTold = true;
      this.say(
        `The work that was not saved when the desk program stopped (${kept.slice(11, 16)}) is back. ` +
          'Save it, or load the project again to drop it.',
        RECOVERED_NOTICE_MS,
      );
    } catch {
      // A desk program from before the autosave.
    }
  }

  /**
   * Keeps the layout in step with the size of the window: the number of faders in a row, and
   * the classes on <html> ("medium", "narrow", "mobile", "phone") that the style sheets go by.
   */
  private followWindowSize(): void {
    if (typeof matchMedia !== 'function') return; // not in a browser, for example in the tests

    const below = (pixels: number): number => pixels - 0.02; // "narrower than", also for half pixels
    const medium = matchMedia(`(max-width: ${below(MEDIUM_BELOW_PX)}px)`);
    const narrow = matchMedia(`(max-width: ${below(NARROW_BELOW_PX)}px)`);
    const mobile = matchMedia(
      `(max-width: ${below(MOBILE_BELOW_WIDTH_PX)}px), (max-height: ${below(MOBILE_BELOW_HEIGHT_PX)}px)`,
    );
    const phone = matchMedia(`(max-width: ${below(PHONE_BELOW_PX)}px)`);

    const classes = document.documentElement.classList;
    const update = (): void => {
      this.windowStripsPerRow.set(medium.matches ? STRIPS_PER_ROW_MEDIUM : STRIPS_PER_ROW_WIDE);
      this.mobile.set(mobile.matches);
      this.phone.set(phone.matches);
      classes.toggle('medium', medium.matches);
      classes.toggle('narrow', narrow.matches);
      classes.toggle('mobile', mobile.matches);
      classes.toggle('phone', phone.matches);
    };

    update();
    for (const query of [medium, narrow, mobile, phone]) query.addEventListener('change', update);
  }

  private takeDesk(desk: DeskState): void {
    this.channels.set(desk.channels);
    this.memoryLevels.set(desk.memoryLevels);
    this.memoryStored.set(desk.memoryStored);
    this.memorySequence.set(desk.memorySequence ?? allFalse(MEMORY_COUNT));
    // An older desk program does not know notes or independent keys and leaves them out.
    // Then the page keeps what it has, instead of falling back to empty notes and keys 1 to 16.
    if (desk.channelNotes) this.channelNotes.set(desk.channelNotes);
    if (desk.memoryNotes) this.memoryNotes.set(desk.memoryNotes);
    if (desk.independents) this.independents.set(desk.independents);
    if (desk.memoryIndependents) this.memoryIndependents.set(desk.memoryIndependents);
    this.master.set(desk.master);
    this.blackout.set(desk.blackout);
    this.rate.set(desk.rate ?? 1);
    this.rateOn.set(desk.rateOn ?? false);
    // The whole desk comes when a memory was stored, copied or emptied, or a project loaded:
    // exactly when a look, a sequence or the stack may be another one.
    void this.fetchMemoryLooks();
    void this.fetchSequencesAndStack();
  }

  private async fetchSequencesAndStack(): Promise<void> {
    try {
      const [sequences, stack, playback] = await Promise.all([
        this.connection.invoke<Sequences | undefined>('GetSequences'),
        this.connection.invoke<StackState | undefined>('GetStack'),
        this.connection.invoke<Playback | undefined>('GetPlayback'),
      ]);
      this.sequences.set(sequences ?? {});
      this.stack.set(stack ?? { steps: [] });
      this.takePlayback(playback ?? NO_PLAYBACK);
      this.playbackKnown.set(true);
    } catch {
      // An older desk program knows neither sequences nor the stack. The two views say so.
      this.sequences.set({});
      this.stack.set({ steps: [] });
      this.playback.set(NO_PLAYBACK);
      this.playbackKnown.set(false);
    }
  }

  private takePlayback(playback: Playback): void {
    this.playback.set(playback);
    // A live stack locks the record keys: whatever was begun with one of them is over.
    if (playback.stack.live) this.mode.set(IDLE);
  }

  private async fetchMemoryLooks(): Promise<void> {
    try {
      this.memoryLooks.set((await this.connection.invoke<MemoryLooks | undefined>('GetMemories')) ?? {});
      this.memoryLooksKnown.set(true);
    } catch {
      // An older desk program does not know the question. Then no fader follows a memory.
      this.memoryLooks.set({});
      this.memoryLooksKnown.set(false);
    }
  }
}

function zeros(count: number): number[] {
  return new Array<number>(count).fill(0);
}

/** 1, 2, 3 ... count: where the independent keys start, key 1 on channel or memory 1, and so on. */
function oneToCount(count: number): number[] {
  return Array.from({ length: count }, (_, index) => index + 1);
}

function noNotes(count: number): string[] {
  return new Array<string>(count).fill('');
}

function allFalse(count: number): boolean[] {
  return new Array<boolean>(count).fill(false);
}

/** A copy of `list` with one item replaced. Signals need a new list to notice the change. */
function withItem<T>(list: readonly T[], index: number, value: T): T[] {
  const copy = list.slice();
  copy[index] = value;
  return copy;
}
