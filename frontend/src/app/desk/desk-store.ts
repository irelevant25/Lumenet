import { Service, computed, inject, signal } from '@angular/core';
import { DeskConnection } from './desk-connection';
import {
  BoxStatus,
  CHANNEL_COUNT,
  DeskState,
  INDEPENDENT_COUNT,
  MAX_LEVEL,
  MEDIUM_BELOW_PX,
  MEMORY_COUNT,
  MOBILE_BELOW_HEIGHT_PX,
  MOBILE_BELOW_WIDTH_PX,
  MemoryLooks,
  PHONE_BELOW_PX,
  ProjectInfo,
  ProjectList,
  STRIPS_PER_ROW_MEDIUM,
  STRIPS_PER_ROW_WIDE,
  Welcome,
  pageStart,
} from './desk-types';
import {
  DeskCommand,
  FunctionMode,
  FunctionName,
  IDLE,
  Step,
  pressFunction,
  pressMemory,
} from './function-mode';

/** Fader moves are collected for this long and then sent together, the newest value of each fader only. */
const SEND_DELAY_MS = 15;

/** How long a notice stays in the top bar. */
const NOTICE_MS = 5000;

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
   * give the channel, whichever is higher. The backend works out the output the same way,
   * before the master.
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
    return shown ?? own;
  });

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
  /** Where a memory function (save, edit, copy, delete) stands. */
  readonly mode = signal<FunctionMode>(IDLE);
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

  // ---------------------------------------------------------------- memory functions

  pressFunction(name: FunctionName): void {
    this.follow(pressFunction(this.mode(), name));
  }

  pressMemory(memory: number): void {
    this.follow(pressMemory(this.mode(), memory, this.memoryStored()[memory - 1]));
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
    if (step.command) this.carryOut(step.command);
  }

  private carryOut(command: DeskCommand): void {
    // Fader moves made just before must reach the backend first: a memory stores what the faders show.
    this.flushOutbox();

    switch (command.kind) {
      case 'saveMemory':
        this.markStored(command.memory, true);
        this.run('SaveMemory', command.memory);
        break;
      case 'loadMemory':
        this.run('LoadMemory', command.memory);
        break;
      case 'copyMemory':
        this.markStored(command.to, true);
        this.run('CopyMemory', command.from, command.to);
        break;
      case 'deleteMemory':
        this.markStored(command.memory, false);
        this.run('DeleteMemory', command.memory);
        break;
    }
  }

  /** Lights the memory's lamp right away. The backend confirms with the full state a moment later. */
  private markStored(memory: number, stored: boolean): void {
    this.memoryStored.update((flags) => withItem(flags, memory - 1, stored));
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
    // An older desk program does not know notes or independent keys and leaves them out.
    // Then the page keeps what it has, instead of falling back to empty notes and keys 1 to 16.
    if (desk.channelNotes) this.channelNotes.set(desk.channelNotes);
    if (desk.memoryNotes) this.memoryNotes.set(desk.memoryNotes);
    if (desk.independents) this.independents.set(desk.independents);
    this.master.set(desk.master);
    this.blackout.set(desk.blackout);
    // The whole desk comes when a memory was stored, copied or emptied, or a project loaded:
    // exactly when a look may be another one.
    void this.fetchMemoryLooks();
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
