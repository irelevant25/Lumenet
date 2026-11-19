import { Service, computed, inject, signal } from '@angular/core';
import { DeskConnection } from './desk-connection';
import {
  BoxStatus,
  CHANNEL_COUNT,
  DEFAULT_LOOP,
  DeskState,
  INDEPENDENT_COUNT,
  MAX_LEVEL,
  MAX_SEQUENCE_STEPS,
  MEDIUM_BELOW_PX,
  MEMORY_COUNT,
  MOBILE_BELOW_HEIGHT_PX,
  MOBILE_BELOW_WIDTH_PX,
  MemoryLooks,
  PHONE_BELOW_PX,
  Playback,
  ProjectInfo,
  ProjectList,
  STRIPS_PER_ROW_MEDIUM,
  STRIPS_PER_ROW_WIDE,
  Sequence,
  Sequences,
  StackState,
  TimedStep,
  Welcome,
  pageStart,
  recordedStep,
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

/** Fader moves are collected for this long and then sent together, the newest value of each fader only. */
const SEND_DELAY_MS = 15;

/** How long a notice stays in the top bar. */
const NOTICE_MS = 5000;

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
  /** The channel each of the 16 independent keys switches: 1 to 16 until the owner changes them. */
  readonly independents = signal<readonly number[]>(defaultIndependents());
  readonly master = signal(MAX_LEVEL);
  readonly blackout = signal(false);
  readonly box = signal<BoxStatus>({ connected: false, host: '', firmware: null });
  readonly project = signal<ProjectInfo>({ name: 'Untitled', path: null, unsaved: false });
  readonly version = signal('');

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

  // ---- what only this page knows
  /** Faders in one row: 32 on a wide window, 16 on a narrower one (the limit is in desk-types.ts). */
  readonly stripsPerRow = signal(STRIPS_PER_ROW_WIDE);
  /** True in the mobile layout, where the sections stand under each other and the page scrolls. */
  readonly mobile = signal(false);
  /** True on a phone held upright, where a row of faders is shown as lines of 8. */
  readonly phone = signal(false);
  /** The memory the Memories view shows the content of. */
  readonly inspectedMemory = signal(1);
  /** A page of channels is two rows of faders; a page of memories is one row. */
  readonly channelsPerPage = computed(() => 2 * this.stripsPerRow());
  readonly memoriesPerPage = computed(() => this.stripsPerRow());

  /** A channel and a memory that must be on the pages shown. The pages follow the window size. */
  private readonly wantedChannel = signal(1);
  private readonly wantedMemory = signal(1);
  /** The first channel of the page that is shown. */
  readonly firstChannel = computed(() => pageStart(this.wantedChannel(), this.channelsPerPage()));
  /** The first memory of the page that is shown. */
  readonly firstMemory = computed(() => pageStart(this.wantedMemory(), this.memoriesPerPage()));
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
    connection.on<[number]>('MasterChanged', (level) => this.master.set(level));
    connection.on<[boolean]>('BlackoutChanged', (on) => this.blackout.set(on));
    connection.on<[DeskState]>('DeskChanged', (desk) => this.takeDesk(desk));
    connection.on<[BoxStatus]>('BoxChanged', (box) => this.box.set(box));
    connection.on<[ProjectInfo]>('ProjectChanged', (project) => this.project.set(project));
    connection.on<[StackState]>('StackChanged', (stack) => this.stack.set(stack));
    connection.on<[Playback]>('PlaybackChanged', (playback) => this.takePlayback(playback));

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

  /** An independent key switches its channel: to zero when it is up, to full when it is at zero. */
  toggleIndependent(key: number): void {
    const channel = this.independents()[key - 1];
    this.setChannel(channel, this.channels()[channel - 1] > 0 ? 0 : MAX_LEVEL);
  }

  /** Binds an independent key to another channel. */
  setIndependent(key: number, channel: number): void {
    if (this.independents()[key - 1] === channel) return;
    this.independents.update((channels) => withItem(channels, key - 1, channel));
    this.run('SetIndependent', key, channel);
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
    this.follow(pressMemory(this.mode(), memory, this.memoryContent(memory)));
  }

  /** The key under a channel fader. It does something only while a sequence is recorded. */
  pressChannel(channel: number): void {
    this.follow(pressChannel(this.mode(), channel));
  }

  /** What a memory holds: nothing, a look, or a sequence. */
  memoryContent(memory: number): MemoryContent {
    if (!this.memoryStored()[memory - 1]) return 'nothing';
    return this.memorySequence()[memory - 1] ? 'sequence' : 'look';
  }

  // ---------------------------------------------------------------- sequences

  /** Stores a sequence in a memory that is empty or holds a sequence already. */
  setSequence(memory: number, sequence: Sequence): void {
    this.sequences.update((all) => ({ ...all, [memory]: sequence }));
    this.markStored(memory, 'sequence');
    this.run('SetSequence', memory, sequence.loop, sequence.steps);
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

    const sequence: Sequence = { loop: old?.loop ?? DEFAULT_LOOP, steps: [...(old?.steps ?? []), step] };
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

  private say(text: string): void {
    this.notice.set(text);
    if (this.noticeTimer !== null) clearTimeout(this.noticeTimer);
    this.noticeTimer = setTimeout(() => this.notice.set(''), NOTICE_MS);
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
    } catch {
      // The connection dropped again before the answer arrived. The next connect asks again.
    }
  }

  /**
   * Keeps the layout in step with the size of the window: the number of faders in a row, and
   * the classes on <html> ("medium", "mobile", "phone") that the style sheets go by.
   */
  private followWindowSize(): void {
    if (typeof matchMedia !== 'function') return; // not in a browser, for example in the tests

    const below = (pixels: number): number => pixels - 0.02; // "narrower than", also for half pixels
    const medium = matchMedia(`(max-width: ${below(MEDIUM_BELOW_PX)}px)`);
    const mobile = matchMedia(
      `(max-width: ${below(MOBILE_BELOW_WIDTH_PX)}px), (max-height: ${below(MOBILE_BELOW_HEIGHT_PX)}px)`,
    );
    const phone = matchMedia(`(max-width: ${below(PHONE_BELOW_PX)}px)`);

    const classes = document.documentElement.classList;
    const update = (): void => {
      this.stripsPerRow.set(medium.matches ? STRIPS_PER_ROW_MEDIUM : STRIPS_PER_ROW_WIDE);
      this.mobile.set(mobile.matches);
      this.phone.set(phone.matches);
      classes.toggle('medium', medium.matches);
      classes.toggle('mobile', mobile.matches);
      classes.toggle('phone', phone.matches);
    };

    update();
    for (const query of [medium, mobile, phone]) query.addEventListener('change', update);
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
    this.master.set(desk.master);
    this.blackout.set(desk.blackout);
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

/** Independent key 1 switches channel 1, key 2 channel 2, and so on. */
function defaultIndependents(): number[] {
  return Array.from({ length: INDEPENDENT_COUNT }, (_, index) => index + 1);
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
