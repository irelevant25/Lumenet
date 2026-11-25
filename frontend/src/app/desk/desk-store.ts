import { Service, computed, effect, inject, signal } from '@angular/core';
import { DeskConnection } from './desk-connection';
import { DeskSettings } from './desk-settings';
import { enterFullscreen, leaveFullscreen } from './fullscreen';
import { DEFAULT_BPM, roundedBpm, tempoOfTaps, withTap } from './tempo';
import {
  AutosaveOptions,
  BoxPlan,
  BoxView,
  CHANNEL_INDEPENDENT_COUNT,
  DEFAULT_LOOP,
  DeskState,
  FoundNode,
  HistoryState,
  ImportAnswer,
  LanAccess,
  LightGroup,
  MAX_LEVEL,
  NetworkChoice,
  OutputPlan,
  OutputView,
  MAX_SEQUENCE_STEPS,
  MEDIUM_BELOW_PX,
  MEMORY_COUNT,
  NARROW_BELOW_PX,
  MEMORY_INDEPENDENT_COUNT,
  MOBILE_BELOW_HEIGHT_PX,
  MOBILE_BELOW_WIDTH_PX,
  MemoryLooks,
  NEW_PROJECT_NAME,
  EMPTY_STAGE,
  NO_HISTORY,
  NO_SOLO,
  PHONE_BELOW_PX,
  Playback,
  ProjectInfo,
  ProjectList,
  STRIPS_PER_ROW_MEDIUM,
  STRIPS_PER_ROW_WIDE,
  Sequence,
  Sequences,
  SoloState,
  StackState,
  StackStep,
  StageLight,
  StagePlan,
  StepTarget,
  TimedStep,
  UNIVERSE_SIZE,
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
import {
  AttributeLight,
  LevelChanges,
  applyLimits,
  colorChanges,
  fixtureLabels,
  hasDimmer,
  intensityChanges,
  intensityOf,
  lookHolds,
  mixedColor,
  panTiltOf,
  positionChanges,
  recordedColorOf,
  rolesOf,
  shutterOpening,
  stepLights,
  typeOf,
} from './fixtures';
import { noteInOneLine } from './notes';
import { channelLabel, universeOf, universeStart } from './universes';
import {
  EFFECT_INDEPENDENT_COUNT,
  Effect,
  EffectControl,
  EffectPlayback,
  EffectPreview,
  MAX_EFFECTS,
  withEffectLevels,
} from './effects';

/**
 * While a sequence is recorded: for every channel, memory, fixture and group it uses, the
 * numbers of the steps that use it, counted from 1. The keys show them.
 */
export interface RecordedSteps {
  readonly channels: ReadonlyMap<number, readonly number[]>;
  readonly memories: ReadonlyMap<number, readonly number[]>;
  readonly fixtures: ReadonlyMap<number, readonly number[]>;
  readonly groups: ReadonlyMap<number, readonly number[]>;
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

/** Changes of one effect in the editor are collected for this long and then stored together. */
const EFFECT_SEND_DELAY_MS = 150;

/** A fader or live value of an effect that the page moved keeps the page's value for this long. */
const EFFECT_TOUCH_MS = 600;

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
  /** How many universes the project has, 1 to 8 (Settings). */
  readonly universes = signal(1);
  /** How many channels the project has: 512 for each universe. The lists of the channels are that long. */
  readonly channelCount = computed(() => this.universes() * UNIVERSE_SIZE);
  readonly channels = signal<readonly number[]>(zeros(UNIVERSE_SIZE));
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
  readonly channelNotes = signal<readonly string[]>(noNotes(UNIVERSE_SIZE));
  readonly memoryNotes = signal<readonly string[]>(noNotes(MEMORY_COUNT));
  /** The channel each of the 10 channel keys of the independents switches: 1 to 10 until the owner changes them. */
  readonly independents = signal<readonly number[]>(oneToCount(CHANNEL_INDEPENDENT_COUNT));
  /** The memory each of the 10 memory keys of the independents switches: 1 to 10 until the owner changes them. */
  readonly memoryIndependents = signal<readonly number[]>(oneToCount(MEMORY_INDEPENDENT_COUNT));
  /** The effect each of the 32 effect keys of the independents starts and stops: 1 to 32 until the owner changes them. */
  readonly effectIndependents = signal<readonly number[]>(oneToCount(EFFECT_INDEPENDENT_COUNT));
  /** The effects of the project, in the order of their numbers. */
  readonly effects = signal<readonly Effect[]>([]);
  /** False while the effects could not be fetched: a desk program from before effects. */
  readonly effectsKnown = signal(false);
  /** The effects by their numbers. */
  readonly effectsByNumber = computed(() => new Map(this.effects().map((effect) => [effect.number, effect])));
  /** The playbacks of the effects by their numbers: whether each runs, its fader and its live values. */
  readonly effectPlaybacks = signal<ReadonlyMap<number, EffectPlayback>>(new Map());
  readonly master = signal(MAX_LEVEL);
  readonly blackout = signal(false);
  /**
   * The boxes, as the Boxes window shows them: which answer, and the universe each of their
   * outputs plays. Empty while none answers and the project lists none.
   */
  readonly boxes = signal<readonly BoxView[]>([]);
  /**
   * The project's outputs besides its boxes, as the Outputs window shows them: Art-Net nodes, sACN
   * and DMX USB interfaces, each with its state. Empty while the project lists none.
   */
  readonly outputs = signal<readonly OutputView[]>([]);
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
   * gives what the backend reports from moment to moment. While the stack goes from one look
   * to the next, the faders taking part give their light through its crossfade instead, as the
   * backend reports it: so a channel both looks hold shows no dip. The backend works out the
   * output the same way, before the master. An attribute of a light (a pan, a tilt) shows what
   * the fader that took it last gives it, as stored (see attributesTaken).
   */
  readonly shownChannels = computed<readonly number[]>(() => {
    const cross = this.crossfade();
    const own = cross ? withoutChannels(this.channels(), cross.channels) : this.channels();
    const levels = this.memoryLevels();
    let shown: number[] | null = null;
    for (const [memory, look] of Object.entries(this.memoryLooks())) {
      const level = levels[Number(memory) - 1];
      if (!level || cross?.memories.includes(Number(memory))) continue;

      shown ??= own.slice();
      for (let index = 0; index < look.length; index++) {
        const part = Math.floor((look[index] * level + 127) / MAX_LEVEL); // rounded like the backend does
        if (part > shown[index]) shown[index] = part;
      }
    }

    if (cross) {
      shown ??= own.slice();
      cross.levels.forEach((level, index) => {
        if (shown && index < shown.length && level > shown[index]) shown[index] = level;
      });
    }

    const running = this.playback().levels;
    for (let index = 0; index < running.length; index++) {
      if (running[index] > (shown ?? own)[index]) {
        shown ??= own.slice();
        shown[index] = running[index];
      }
    }

    // The running effects: the desk program mixes them with all of the above, so their channels
    // show what it reports (Desk.Effects.cs).
    const effects = this.playback().effectLevels;
    if (this.roles().attributeLights.length === 0) return effects ? withEffectLevels(shown ?? own.slice(), effects) : (shown ?? own);

    shown ??= own.slice();
    this.attributesTaken(shown);
    return withEffectLevels(shown, effects);
  });

  /**
   * The stack's crossfade as the desk program counts it: none while a key held with SOLO DOWN is
   * on one of its faders (then every fader gives its light on its own again).
   */
  private readonly crossfade = computed(() => {
    const cross = this.playback().crossfade;
    if (!cross) return null;
    const held = this.solo().down.some((key) => (key.target === 'channel' ? cross.channels : cross.memories).includes(key.number));
    return held ? null : cross;
  });

  /** The ids of the steps of the stack that are on: the current one and those it came on top of (Add). */
  readonly stackOn = computed<ReadonlySet<number>>(() => new Set(this.playback().stack.on ?? []));

  /**
   * Latest takes precedence, as in the desk program: each attribute of a light comes from the
   * fader that took it last, at the level it holds, not scaled by any fader. A channel fader
   * takes its channel when it moves; a memory fader takes the lights its memory holds when it
   * leaves zero (a look holds a light it gives any level, a sequence one its steps use).
   */
  private attributesTaken(shown: number[]): void {
    const own = this.channels();
    const levels = this.memoryLevels();
    const looks = this.memoryLooks();
    const sequences = this.sequences();
    const memoryClaims = this.memoryClaims();
    const channelClaims = this.channelClaims();
    const running = this.playback().levels;
    const plan = this.stage();
    const stepHolds = (step: TimedStep, light: AttributeLight): boolean => {
      if (step.target === 'channel') return light.channels.includes(step.number - 1);
      if (step.target === 'memory') return !!looks[step.number] && lookHolds(looks[step.number], light);
      return stepLights(plan, step).some((held) => light.channels.includes(held.address - 1));
    };
    for (const light of this.roles().attributeLights) {
      let latest = -1;
      for (let index = 0; index < MEMORY_COUNT; index++) {
        if (!levels[index] || (latest >= 0 && memoryClaims[index] <= memoryClaims[latest])) continue;
        const look = looks[index + 1];
        const sequence = sequences[index + 1];
        const holds = look ? lookHolds(look, light) : (sequence?.steps ?? []).some((step) => stepHolds(step, light));
        if (holds) latest = index;
      }
      for (const channel of light.attributes) {
        const taken = latest >= 0 && memoryClaims[latest] > channelClaims[channel];
        shown[channel] = taken ? (looks[latest + 1]?.[channel] ?? running[channel] ?? 0) : own[channel];
      }
    }
  }

  /**
   * What goes out to the rig for each channel: what the fader shows, as a solo leaves it, each
   * light held to its limit, scaled by the master and rounded as the desk program does, and dark
   * during blackout. The attributes of the lights (a pan, a tilt) are left as they are by all of
   * that. The stage plan draws its lights from it.
   */
  readonly output = computed<readonly number[]>(() => {
    const roles = this.roles();
    const shown = this.shownChannels();
    const levels = this.soloLevels().slice();
    applyLimits(levels, roles.limits);
    const master = this.blackout() ? 0 : this.master();
    for (let index = 0; index < levels.length; index++) {
      levels[index] = roles.attribute[index] ? shown[index] : Math.floor((levels[index] * master + 127) / MAX_LEVEL);
    }
    return levels;
  });

  /**
   * What the faders give, as a solo leaves it, the same way as the desk program: with keys held
   * with SOLO UP only those shine (a channel at full, a memory's look at full, a sequence as it
   * plays); with keys held with SOLO DOWN their channels and memories give nothing.
   */
  private readonly soloLevels = computed<readonly number[]>(() => {
    const solo = this.solo();
    const shown = this.shownChannels();
    if (solo.up.length > 0) {
      const levels = zeros(shown.length);
      const raise = (index: number, level: number) => (levels[index] = Math.max(levels[index], level));
      for (const key of solo.up) {
        if (key.target === 'channel') {
          raise(key.number - 1, MAX_LEVEL);
          continue;
        }
        const look = this.memoryLooks()[key.number];
        if (look) look.forEach((level, index) => raise(index, level));
        // a sequence: what it gives now, on the channels its steps use (its fader is at full)
        for (const step of this.sequences()[key.number]?.steps ?? []) {
          const channels =
            step.target === 'channel' ? [step.number - 1]
            : step.target === 'memory' ? lookChannels(this.memoryLooks()[step.number])
            : stepLights(this.stage(), step).flatMap((light) => [...light.channels].map((_, offset) => light.address - 1 + offset));
          for (const index of channels) raise(index, shown[index]);
        }
      }
      return levels;
    }
    if (solo.down.length === 0) return shown;

    const levels = shown.slice();
    for (const key of solo.down) {
      if (key.target === 'channel') levels[key.number - 1] = 0; // a memory: its fader is down already
    }
    return levels;
  });

  /** What the stage plan says about every channel: which are attributes of lights, and the lights' limits. */
  readonly roles = computed(() => rolesOf(this.stage()));
  /** For each channel: the light it belongs to and what it does there, as a note of two lines; empty for none. */
  readonly fixtureLabels = computed(() => fixtureLabels(this.stage()));
  /** For each channel: its note, or else what the plan says it is. The lists of steps name channels so. */
  readonly channelNames = computed(() => {
    const labels = this.fixtureLabels();
    return this.channelNotes().map((note, index) => (note.trim() !== '' ? note : labels[index]));
  });
  /** When each channel fader and each memory fader last took what it gives an attribute (a count that grows). */
  readonly channelClaims = signal<readonly number[]>(zeros(UNIVERSE_SIZE));
  readonly memoryClaims = signal<readonly number[]>(zeros(MEMORY_COUNT));
  private claimCount = 0;
  // The color each fixture that mixes its color had last: brought up from dark, it comes back in it.
  private readonly lastColors = new Map<number, [number, number, number]>();

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
  /** The first channel of the page that is shown; in the last universe when the project has fewer than it had. */
  readonly firstChannel = computed(() =>
    pageStart(Math.min(this.wantedChannel(), this.channelCount()), this.channelsPerPage()),
  );
  /** The universe whose channels the channel faders show: its U key is lit, and its pages are offered. */
  readonly shownUniverse = computed(() => universeOf(this.firstChannel()));
  /** The first memory of the page that is shown on the main view. */
  readonly firstMemory = computed(() => pageStart(this.wantedMemory(), this.memoriesPerPage()));
  /** The first memory of the page that is shown on the Memories and Sequences views. */
  readonly viewFirstMemory = computed(() => pageStart(this.wantedMemory(), this.viewMemoriesPerPage()));
  /** An effect that must be on the page of effects shown: on the main view a row as long as the memories', on the Effects view the window's. */
  private readonly wantedEffect = signal(1);
  readonly firstEffect = computed(() => pageStart(this.wantedEffect(), this.stripsPerRow()));
  readonly viewFirstEffect = computed(() => pageStart(this.wantedEffect(), this.viewMemoriesPerPage()));
  /** The effect the editor of the Effects view works on. */
  readonly chosenEffect = signal(1);
  /** Where a record key (save or edit a memory, record a sequence, copy, delete) stands. */
  readonly mode = signal<FunctionMode>(IDLE);
  /** The steps of the sequence that is being recorded, by what they use; null while none is recorded. */
  readonly recordedSteps = computed<RecordedSteps | null>(() => {
    const mode = this.mode();
    if (mode.kind !== 'recording') return null;

    const used = { channel: new Map<number, number[]>(), memory: new Map<number, number[]>(), fixture: new Map<number, number[]>(), group: new Map<number, number[]>() };
    (this.sequences()[mode.memory]?.steps ?? []).forEach((step, index) => {
      if (step.target === 'release' || step.target === 'effect') return; // only the stack has these
      const users = used[step.target];
      const numbers = users.get(step.number);
      if (numbers) numbers.push(index + 1);
      else users.set(step.number, [index + 1]);
    });
    return { channels: used.channel, memories: used.memory, fixtures: used.fixture, groups: used.group };
  });
  /** A short message for the top bar, for example an error from the backend. */
  readonly notice = signal('');
  /**
   * Which solo is lit, if any. SOLO UP: a key under a fader or of the independents puts its fader
   * to full while it is held, and every other light goes dark. SOLO DOWN: it takes only its own
   * fader down while it is held, and the rest stays. Let go, everything is as before. Dark: the
   * keys switch their faders on and off.
   */
  readonly soloMode = signal<'up' | 'down' | null>(null);
  /** The keys held for a solo now, on every page: the desk program lets only those (or all but those) out. */
  readonly solo = signal<SoloState>(NO_SOLO);
  // The faders a held key holds now, by "channel 5" or "memory 3": the level each had before, and which solo holds it.
  private readonly held = new Map<string, { level: number; up: boolean }>();
  /**
   * The rate (RATE, its fader and TEMPO): a tempo for all sequences, in beats per minute, and
   * whether it counts. While it does, every sequence takes one step per beat.
   */
  readonly bpm = signal(DEFAULT_BPM);
  readonly rateOn = signal(false);
  // The taps of TEMPO that count, in milliseconds of the page (see desk/tempo.ts).
  private taps: number[] = [];
  /** The autosave of the desk program: on or off, and how often; null while it is not known. */
  readonly autosave = signal<AutosaveOptions | null>(null);
  /** The stage plan of the project: the lights of the rig and where they hang. */
  readonly stage = signal<StagePlan>(EMPTY_STAGE);
  /** False while the plan could not be fetched: a desk program from before stage plans. */
  readonly stageKnown = signal(false);
  /** The picture under the plan, as a data URL; null without one (or until it has come). */
  readonly stagePicture = signal<string | null>(null);
  /** The notes of the project, instructions for the show, as the Notes view wrote them (a little HTML); empty for none. */
  readonly notes = signal('');
  /** False while the notes could not be fetched: a desk program from before notes. */
  readonly notesKnown = signal(false);
  // The name of the picture that stagePicture holds or is being fetched.
  private stagePictureId: string | null = null;
  // True once this page said that unsaved work was brought back.
  private recoveryTold = false;

  private readonly loaded = signal(false);

  /** True when the page is connected to the desk program and has received its state. */
  readonly online = computed(() => this.connection.online() && this.loaded());

  /** Fader moves waiting to be sent: one entry per fader, so only its newest value goes out. */
  private readonly outbox = new Map<string, { method: string; args: unknown[] }>();
  // Effects waiting to be stored, by number; and when this page last moved a fader or live value of
  // an effect, by "3 level", "3 speed" and so on.
  private readonly effectTimers = new Map<number, ReturnType<typeof setTimeout>>();
  private readonly effectTouched = new Map<string, number>();
  private outboxTimer: ReturnType<typeof setTimeout> | null = null;
  private noticeTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    const connection = this.connection;

    connection.on<[number, number]>('ChannelChanged', (channel, level) => {
      this.channels.update((levels) => withItem(levels, channel - 1, level));
      this.claimChannel(channel);
    });
    connection.on<[number, number]>('MemoryLevelChanged', (memory, level) => {
      this.claimMemory(memory, level);
      this.memoryLevels.update((levels) => withItem(levels, memory - 1, level));
    });
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
    connection.on<[BoxView[]]>('BoxesChanged', (boxes) => this.boxes.set(boxes));
    connection.on<[OutputView[]]>('OutputsChanged', (outputs) => this.outputs.set(outputs));
    connection.on<[ProjectInfo]>('ProjectChanged', (project) => this.project.set(project));
    connection.on<[StackState]>('StackChanged', (stack) => this.stack.set(stack));
    connection.on<[Playback]>('PlaybackChanged', (playback) => this.takePlayback(playback));
    connection.on<[LanAccess]>('LanAccessChanged', (lan) => this.lan.set(lan));
    connection.on<[HistoryState]>('HistoryChanged', (history) => this.history.set(history));
    connection.on<[boolean, number]>('RateChanged', (on, bpm) => {
      this.rateOn.set(on);
      this.bpm.set(bpm);
    });
    connection.on<[AutosaveOptions]>('AutosaveChanged', (options) => this.autosave.set(options));
    connection.on<[StagePlan]>('StageChanged', (plan) => this.takeStage(plan));
    connection.on<[SoloState]>('SoloChanged', (solo) => this.solo.set(solo));
    connection.on<[string]>('NotesChanged', (notes) => this.notes.set(notes ?? ''));
    connection.on<[number, number]>('SequenceSpeedChanged', (memory, speed) =>
      this.sequences.update((all) => (all[memory] ? { ...all, [memory]: { ...all[memory], speed } } : all)),
    );
    connection.on<[Effect[]]>('EffectsChanged', (effects) => this.effects.set(effects ?? []));
    connection.on<[EffectPlayback]>('EffectPlaybackChanged', (playback) => this.takeEffectPlaybacks([playback], false));
    connection.on<[number, number]>('EffectIndependentChanged', (key, effect) =>
      this.effectIndependents.update((effects) => withItem(effects, key - 1, effect)),
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

  /** Shows the first page of a universe's channels (its U key). */
  showUniverse(universe: number): void {
    if (universe >= 1 && universe <= this.universes()) this.wantedChannel.set(universeStart(universe));
  }

  /** Shows the page of memories that starts at `first`. */
  showMemoryPage(first: number): void {
    this.wantedMemory.set(first);
  }

  // ---------------------------------------------------------------- faders

  setChannel(channel: number, level: number): void {
    if (this.channels()[channel - 1] === level) return;
    this.channels.update((levels) => withItem(levels, channel - 1, level));
    this.claimChannel(channel);
    this.sendSoon(`channel ${channel}`, 'SetChannel', channel, level);
  }

  setMemoryLevel(memory: number, level: number): void {
    if (this.memoryLevels()[memory - 1] === level) return;
    this.claimMemory(memory, level);
    this.memoryLevels.update((levels) => withItem(levels, memory - 1, level));
    this.sendSoon(`memory ${memory}`, 'SetMemoryLevel', memory, level);
  }

  // A channel fader that moves takes its channel, if that is an attribute of a light.
  private claimChannel(channel: number): void {
    if (!this.roles().attribute[channel - 1]) return;
    this.channelClaims.update((claims) => withItem(claims, channel - 1, ++this.claimCount));
  }

  // A memory fader that leaves zero takes the lights its memory holds. Call before the level changes.
  private claimMemory(memory: number, level: number): void {
    if (level === 0 || this.memoryLevels()[memory - 1] !== 0) return;
    this.memoryClaims.update((claims) => withItem(claims, memory - 1, ++this.claimCount));
  }

  /** Sets many channel faders at once, each as if it was moved. */
  setChannels(changes: LevelChanges): void {
    for (const [channel, level] of changes) this.setChannel(channel, level);
  }

  // ---------------------------------------------------------------- fixtures and groups

  /** The intensity of a fixture as the faders show it, 0 to 255: its dimmer, or its strongest color. */
  fixtureIntensity(light: StageLight): number {
    return intensityOf(light, this.shownChannels());
  }

  /**
   * A fixture to another intensity: its dimmers, or its colors keeping their mix (from dark:
   * the color it had last, or white); a moving head's shutter opens with it.
   */
  setFixtureIntensity(light: StageLight, level: number): void {
    const shown = this.shownChannels();
    const color = mixedColor(light, shown);
    if (color) this.lastColors.set(light.id, color);
    this.setChannels(intensityChanges(light, typeOf(this.stage(), light), level, shown, this.lastColors.get(light.id) ?? null));
  }

  /** The key of a fixture: to full, and to zero when it gives light. */
  toggleFixture(light: StageLight): void {
    this.setFixtureIntensity(light, this.fixtureIntensity(light) > 0 ? 0 : MAX_LEVEL);
  }

  /**
   * The key of a fixture on the Fixtures page: while a sequence is recorded (REC SEQ) it puts in
   * a step of the fixture, at full, in the color it shows now; otherwise it switches it.
   */
  pressFixture(light: StageLight): void {
    const mode = this.mode();
    if (mode.kind !== 'recording') {
      this.toggleFixture(light);
      return;
    }
    const color = recordedColorOf(light, typeOf(this.stage(), light), this.shownChannels());
    this.addSequenceStep(mode.memory, recordedStep('fixture', light.id, color));
  }

  /** The key of a group: a step of the group while a sequence is recorded, in the color of its first light that shows one; otherwise it switches the group. */
  pressGroup(group: LightGroup): void {
    const mode = this.mode();
    if (mode.kind !== 'recording') {
      this.toggleGroup(group);
      return;
    }
    const plan = this.stage();
    const shown = this.shownChannels();
    const color = this.groupLights(group).map((light) => recordedColorOf(light, typeOf(plan, light), shown)).find((found) => found) ?? null;
    this.addSequenceStep(mode.memory, recordedStep('group', group.id, color));
  }

  /**
   * A number for a new light (or group) that no light (or group) has, and no step of the
   * sequences or the stack points to: a step of a light that is gone must not come to play a new one.
   */
  nextLightId(): number {
    return this.nextId('fixture', this.stage().lights);
  }

  nextGroupId(): number {
    return this.nextId('group', this.stage().groups);
  }

  private nextId(target: 'fixture' | 'group', items: readonly { id: number }[]): number {
    const steps = [...Object.values(this.sequences()).flatMap((sequence) => sequence.steps), ...this.stack().steps];
    const used = steps.filter((step) => step.target === target).map((step) => step.number);
    return Math.max(0, ...items.map((item) => item.id), ...used) + 1;
  }

  /**
   * What a step stands for, in words: "Channel 5 (U1)", "Memory 2", "Fixture Spot 1", "Group Back",
   * "Release of step 2.0".
   */
  stepName(step: TimedStep): string {
    if (step.target === 'channel') return `Channel ${channelLabel(step.number)}`;
    if (step.target === 'memory') return `Memory ${step.number}`;
    if (step.target === 'effect') return `Effect ${step.number}`;
    if (step.target === 'release') {
      const released = this.releasedBy(step);
      return released ? `Release of step ${released.cue}` : 'Release of no step';
    }
    const plan = this.stage();
    const found =
      step.target === 'fixture'
        ? plan.lights.find((light) => light.id === step.number)?.name
        : plan.groups.find((group) => group.id === step.number)?.name;
    return `${step.target === 'fixture' ? 'Fixture' : 'Group'} ${found ?? '(gone)'}`;
  }

  /**
   * The note of a step in one line: under the fader of its channel or memory; the name of its
   * fixture or group; for a release, what the step it takes down stands for, "Memory 2 (Wash)".
   */
  stepNote(step: TimedStep): string {
    switch (step.target) {
      case 'channel':
        return noteInOneLine(this.channelNames()[step.number - 1] ?? '');
      case 'memory':
        return noteInOneLine(this.memoryNotes()[step.number - 1] ?? '');
      case 'fixture':
        return this.stage().lights.find((light) => light.id === step.number)?.name ?? 'A light that is gone';
      case 'group':
        return this.stage().groups.find((group) => group.id === step.number)?.name ?? 'A group that is gone';
      case 'effect':
        return this.effectsByNumber().get(step.number)?.name ?? 'No such effect yet';
      case 'release': {
        const released = this.releasedBy(step);
        return released ? this.stepWithNote(released) : 'No step to take down: choose one';
      }
    }
  }

  /** A step in words with its note, "Channel 5 (U1) (PAR 1 red)", "Fixture Spot 1"; a fixture or a group is named enough. */
  stepWithNote(step: TimedStep): string {
    const name = this.stepName(step);
    if (step.target === 'fixture' || step.target === 'group' || step.target === 'release') return name;
    const note = this.stepNote(step);
    return note ? `${name} (${note})` : name;
  }

  /** The step of the stack a release takes down; undefined for a release of none, and for any other step. */
  releasedBy(step: TimedStep): StackStep | undefined {
    return step.target === 'release' ? this.stack().steps.find((other) => other.id === step.number) : undefined;
  }

  /**
   * A fixture in a color (red, green and blue, 0 to 255): its color channels mix it. A fixture
   * that is dark comes up to full with it, so that the color shows.
   */
  setFixtureColor(light: StageLight, color: [number, number, number]): void {
    this.lastColors.set(light.id, color);
    const shown = this.shownChannels();
    const intensity = intensityOf(light, shown);
    const type = typeOf(this.stage(), light);
    const changes = new Map<number, number>();
    if (hasDimmer(light)) {
      for (const [channel, level] of colorChanges(light, color, MAX_LEVEL)) changes.set(channel, level);
      if (intensity === 0) for (const [channel, level] of intensityChanges(light, type, MAX_LEVEL, shown, color)) changes.set(channel, level);
    } else {
      for (const [channel, level] of colorChanges(light, color, intensity || MAX_LEVEL)) changes.set(channel, level);
      for (const [channel, level] of shutterOpening(light, type, shown)) changes.set(channel, level);
    }
    this.setChannels(changes);
  }

  /** Points a moving head: pan `x` and tilt `y`, each from 0 to 1. */
  setFixturePosition(light: StageLight, x: number, y: number): void {
    const axes = panTiltOf(light, typeOf(this.stage(), light));
    if (axes) this.setChannels(positionChanges(axes, x, y));
  }

  /** The lights of a group, as the plan has them now. */
  groupLights(group: LightGroup): StageLight[] {
    const lights = this.stage().lights;
    return group.lights.flatMap((id) => lights.filter((light) => light.id === id));
  }

  /** The intensity of a group: that of its brightest light. */
  groupIntensity(group: LightGroup): number {
    return Math.max(0, ...this.groupLights(group).map((light) => this.fixtureIntensity(light)));
  }

  /** The fader of a group: every light of it to this intensity, each in its own color. */
  setGroupIntensity(group: LightGroup, level: number): void {
    for (const light of this.groupLights(group)) this.setFixtureIntensity(light, level);
  }

  /** The key of a group: all its lights to full, and to zero when any gives light. */
  toggleGroup(group: LightGroup): void {
    this.setGroupIntensity(group, this.groupIntensity(group) > 0 ? 0 : MAX_LEVEL);
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

  /**
   * The note under the key of a fixture, or of a group, on the Fixtures page. It belongs to the
   * stage plan, which goes to the desk program whole.
   */
  setLightNote(lightId: number, note: string): void {
    const plan = this.stage();
    if ((plan.lights.find((light) => light.id === lightId)?.note ?? '') === note) return;
    this.setStage({ ...plan, lights: plan.lights.map((light) => (light.id === lightId ? { ...light, note } : light)) });
  }

  setGroupNote(groupId: number, note: string): void {
    const plan = this.stage();
    if ((plan.groups.find((group) => group.id === groupId)?.note ?? '') === note) return;
    this.setStage({ ...plan, groups: plan.groups.map((group) => (group.id === groupId ? { ...group, note } : group)) });
  }

  // ---------------------------------------------------------------- the table of the Memories view

  /**
   * Changes the level one channel has in the look of a memory, from the table of the Memories
   * view: shown at once, and the desk program sends the whole desk, after which the looks are
   * fetched again. An empty memory gets a look with only that channel (a 0 leaves it empty); a
   * memory that holds a sequence is changed on the Sequences view instead.
   */
  setMemoryChannel(memory: number, channel: number, level: number): void {
    if (this.live() || this.memorySequence()[memory - 1]) return;
    const looks = this.memoryLooks();
    const look = looks[memory];
    if (look ? look[channel - 1] === level : level === 0) return;

    const changed = (look ?? zeros(this.channelCount())).slice();
    changed[channel - 1] = level;
    this.memoryLooks.set({ ...looks, [memory]: changed });
    if (!look) this.markStored(memory, 'look');
    this.run('SetMemoryChannel', memory, channel, level);
  }

  // ---------------------------------------------------------------- independent keys

  /**
   * A channel key of the independents switches its channel: to zero when it is up, to full when
   * it is at zero. While a solo is lit the key holds its fader instead, while it is held (holdIndependent).
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

  /** An effect key of the independents starts or stops its effect, as the key of the effect does. Solo leaves it alone. */
  toggleEffectIndependent(key: number): void {
    this.toggleEffect(this.effectIndependents()[key - 1]);
  }

  /** Binds an effect key of the independents to another effect. */
  setEffectIndependent(key: number, effect: number): void {
    if (key < 1 || key > EFFECT_INDEPENDENT_COUNT || effect < 1 || effect > MAX_EFFECTS) return;
    this.effectIndependents.update((effects) => withItem(effects, key - 1, effect));
    this.run('SetEffectIndependent', key, effect);
  }

  // ---------------------------------------------------------------- effects

  /** Shows the page of effects that starts at `first`, on the main view and the Effects view. */
  showEffectPage(first: number): void {
    this.wantedEffect.set(first);
  }

  /**
   * Stores an effect, new or changed. The page shows it at once; changes of one effect that follow
   * each other quickly (a number typed, a list gone through) go out together.
   */
  setEffect(effect: Effect): void {
    this.effects.update((all) => [...all.filter((item) => item.number !== effect.number), effect].sort((a, b) => a.number - b.number));
    const waiting = this.effectTimers.get(effect.number);
    if (waiting !== undefined) clearTimeout(waiting);
    this.effectTimers.set(
      effect.number,
      setTimeout(() => {
        this.effectTimers.delete(effect.number);
        this.connection.invoke<Effect>('SetEffect', effect).catch((error: Error) => {
          this.say(error.message);
          void this.fetchEffects();
        });
      }, EFFECT_SEND_DELAY_MS),
    );
  }

  /** Takes an effect out of the project; it stops. */
  deleteEffect(number: number): void {
    this.effects.update((all) => all.filter((item) => item.number !== number));
    this.run('DeleteEffect', number);
  }

  /** Stores a copy of an effect under another number. */
  copyEffect(from: number, to: number): void {
    this.run('CopyEffect', from, to);
  }

  /** Stores the live speed, size and spread of an effect's playback in the effect (Keep). */
  keepEffect(number: number): void {
    this.run('KeepEffect', number);
  }

  /**
   * The key of an effect: moves its fader up to full in the effect's fade-in time, which starts it,
   * or down to zero in its fade-out time, which stops it.
   */
  toggleEffect(number: number): void {
    if (!this.effectsByNumber().has(number)) {
      this.say(`There is no effect ${number} yet. On the Effects view, its key offers the kinds to make it.`);
      return;
    }
    this.effectControl(number, 'toggle');
  }

  /** The fader of an effect, 0 to 255: from zero it starts the effect, at zero it stops it. */
  setEffectLevel(number: number, level: number): void {
    this.effectControl(number, 'level', (level / MAX_LEVEL) * 100);
  }

  /**
   * Works the playback of an effect (SetEffectControl in the desk program): "go", "toggle" and
   * "reset" are answered with the playback; "level" (per cent), "speed" (BPM), "size" and "spread"
   * (per cent) move like faders: the page shows them at once and sends the newest only.
   */
  effectControl(number: number, control: EffectControl, value = 0): void {
    if (control === 'level' || control === 'speed' || control === 'size' || control === 'spread') {
      this.touchEffect(number, control, value);
      this.sendSoon(`effect ${number} ${control}`, 'SetEffectControl', number, control, value);
      return;
    }
    this.connection
      .invoke<EffectPlayback | undefined>('SetEffectControl', number, control, value)
      .then((playback) => {
        if (playback) this.takeEffectPlaybacks([playback], false);
      })
      .catch((error: Error) => this.say(error.message));
  }

  /** One cycle of an effect as the desk program would play it, for the preview; null if it cannot be had. */
  async previewEffect(effect: Effect): Promise<EffectPreview | null> {
    try {
      return (await this.connection.invoke<EffectPreview | undefined>('PreviewEffect', effect)) ?? null;
    } catch {
      return null;
    }
  }

  // The playback shows a moved fader or live value at once; a fader that leaves zero starts the
  // effect and one that comes to zero stops it, as the desk program does.
  private touchEffect(number: number, control: 'level' | 'speed' | 'size' | 'spread', value: number): void {
    this.effectTouched.set(`${number} ${control}`, Date.now());
    const old = this.effectPlaybacks().get(number) ?? emptyPlayback(number);
    const exists = this.effectsByNumber().has(number);
    let changed: EffectPlayback;
    if (control === 'level') {
      const level = Math.round((value * MAX_LEVEL) / 100);
      const running = exists && level > 0;
      changed = { ...old, level, running, fading: false };
    } else {
      changed = { ...old, [control]: control === 'speed' ? value : Math.round(value) };
    }
    this.effectPlaybacks.update((all) => new Map(all).set(number, changed));
  }

  // Playbacks from the desk program. A fader or live value this page moved a moment ago keeps the
  // page's value: the desk program's may still be the one from before. "whole": the list of every
  // playback (Playback, DeskChanged); else only these change.
  private takeEffectPlaybacks(playbacks: readonly EffectPlayback[], whole: boolean): void {
    const now = Date.now();
    const old = this.effectPlaybacks();
    const kept = whole ? new Map<number, EffectPlayback>() : new Map(old);
    for (const playback of playbacks) {
      const mine = old.get(playback.number);
      const recent = (control: string) => now - (this.effectTouched.get(`${playback.number} ${control}`) ?? 0) < EFFECT_TOUCH_MS;
      kept.set(
        playback.number,
        mine
          ? {
              ...playback,
              level: recent('level') ? mine.level : playback.level,
              speed: recent('speed') ? mine.speed : playback.speed,
              size: recent('size') ? mine.size : playback.size,
              spread: recent('spread') ? mine.spread : playback.spread,
            }
          : playback,
      );
    }
    this.effectPlaybacks.set(kept);
  }

  // ---------------------------------------------------------------- solo

  /** SOLO UP: lit, a held key puts its fader to full and every other light goes dark; dark, the keys switch their faders on and off. */
  toggleSoloUp(): void {
    this.switchSolo('up');
  }

  /** SOLO DOWN: lit, a held key takes only its own fader down; the rest stays. */
  toggleSoloDown(): void {
    this.switchSolo('down');
  }

  // The two solos exclude each other. A key held when its solo goes dark is let go.
  private switchSolo(solo: 'up' | 'down'): void {
    this.releaseHeld();
    this.soloMode.update((lit) => (lit === solo ? null : solo));
  }

  /**
   * The key under the fader of a channel or a memory goes down (true) or up again (false). While
   * a solo is lit, the fader goes to full (SOLO UP) or to zero (SOLO DOWN) while the key is held,
   * and the desk program lets every other light (SOLO UP), or this one (SOLO DOWN), go dark; let
   * go, the fader is back where it was. Not while a record key wants the key: a memory key while
   * one waits for a memory, a channel key while a sequence is recorded.
   */
  holdKey(target: StepTarget, number: number, down: boolean): void {
    if (!down) this.holdOff(target, number);
    else if (this.soloMode() && this.keyIsFree(target)) this.holdOn(target, number);
  }

  /** The same for a key of the independents: it holds the channel or the memory it is bound to. */
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
    const up = this.soloMode() === 'up';
    if (this.held.has(name)) return;
    // A solo is for the light: the key of an attribute (a pan, a tilt) does not move it.
    if (target === 'channel' && this.roles().attribute[number - 1]) return;

    const level = target === 'channel' ? this.channels()[number - 1] : this.memoryLevels()[number - 1];
    this.held.set(name, { level, up });
    this.setLevel(target, number, up ? MAX_LEVEL : 0);
    this.holdSolo(up, target, number, true);
  }

  private holdOff(target: StepTarget, number: number): void {
    const name = `${target} ${number}`;
    const hold = this.held.get(name);
    if (hold === undefined) return;

    this.held.delete(name);
    this.setLevel(target, number, hold.level);
    this.holdSolo(hold.up, target, number, false);
  }

  /**
   * Tells the desk program that a key is held for a solo, or let go: at once, after the fader
   * move (so that the fader is where it belongs when the solo begins and ends). The page shows
   * it at once; the desk program answers every page with the keys held.
   */
  private holdSolo(up: boolean, target: StepTarget, number: number, held: boolean): void {
    const side = up ? 'up' : 'down';
    const others = this.solo()[side].filter((key) => key.target !== target || key.number !== number);
    this.solo.update((solo) => ({ ...solo, [side]: held ? [...others, { target, number }] : others }));
    if (this.outboxTimer !== null) this.flushOutbox();
    this.run('HoldSolo', up, target, number, held);
  }

  // Every fader a key holds goes back, as if its key were let go.
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
   * RATE: lit, every sequence takes one step per beat of the tempo (the fader under the key and
   * TEMPO set it); dark, every sequence plays its own times. Like the master fader it is not
   * saved with the project.
   */
  toggleRate(): void {
    this.rateOn.update((on) => !on);
    this.taps = [];
    this.sendSoon('rate', 'SetRate', this.rateOn(), this.bpm());
  }

  /** Sets the tempo of the rate, 30 to 300 beats per minute (its fader); it counts while RATE is lit. */
  setBpm(bpm: number): void {
    const kept = roundedBpm(bpm);
    if (kept === this.bpm()) return;
    this.bpm.set(kept);
    this.sendSoon('rate', 'SetRate', this.rateOn(), kept);
  }

  /**
   * A tap of TEMPO, in time with the music: from the second tap on, the tempo of the rate is the
   * beat of the taps (the average of the last four beats; a pause of more than two seconds starts
   * afresh). Only while RATE is lit.
   */
  tapTempo(now: number = performance.now()): void {
    if (!this.rateOn()) return;
    this.taps = withTap(this.taps, now);
    const bpm = tempoOfTaps(this.taps);
    if (bpm !== null) this.setBpm(bpm);
  }

  // ---------------------------------------------------------------- the stage plan

  /**
   * Takes the stage plan as the owner edited it: shown at once, and sent whole. The desk
   * program answers every page with the plan as it keeps it; if it refuses, the page says why
   * and fetches the plan it has.
   */
  setStage(plan: StagePlan): void {
    this.stage.set(plan);
    this.connection.invoke<void>('SetStage', plan).catch((error: Error) => {
      this.say(error.message);
      void this.fetchStage();
    });
  }

  /**
   * Puts a picture under the plan (a data URL of a JPEG, PNG or WebP image), or takes it away
   * (null). Shown at once; the desk program answers every page with the plan, which names it.
   */
  setStagePicture(picture: string | null): void {
    this.stagePicture.set(picture);
    this.connection.invoke<void>('SetStagePicture', picture).catch((error: Error) => {
      this.say(error.message);
      this.stagePictureId = null; // fetched again with the plan
      void this.fetchStage();
    });
  }

  // ---------------------------------------------------------------- universes and boxes

  /**
   * How many universes the project has, 1 to 8. The desk program refuses fewer while a step, a
   * light of the plan or an independent key uses a channel of a universe that would go; the page
   * then says why. The whole desk comes back with the new number of channels.
   */
  setUniverses(universes: number): void {
    if (this.live() || universes === this.universes()) return;
    this.run('SetUniverses', universes);
  }

  /**
   * The project's list of boxes as the Boxes window changed it: a box added by hand, an output
   * given another universe, a box taken out; an empty list for automatic. The desk program
   * answers every page with the boxes as they are then. Rejects with its message when it refuses
   * the list (an address that is none, a box listed twice, the stack live).
   */
  async setBoxes(boxes: readonly BoxPlan[]): Promise<void> {
    await this.connection.invoke<void>('SetBoxes', boxes);
  }

  /**
   * The project's outputs besides its boxes as the Outputs window changed them: an Art-Net node,
   * sACN or a USB interface added, a port changed, an output taken out. The desk program answers
   * every page with the outputs and their states. Rejects with its message when it refuses the
   * list (an address that is none, an output listed twice, Art-Net to a dmxbox, the stack live).
   */
  async setOutputs(outputs: readonly OutputPlan[]): Promise<void> {
    await this.connection.invoke<void>('SetOutputs', outputs);
  }

  /** Asks the Art-Net nodes on the PC's networks to answer: those that did, after a second and a half. */
  async findArtNetNodes(): Promise<FoundNode[]> {
    return (await this.connection.invoke<FoundNode[] | undefined>('FindArtNetNodes')) ?? [];
  }

  /** The serial ports of the PC, for a DMX USB interface; none from a desk program without outputs. */
  async serialPorts(): Promise<string[]> {
    try {
      return (await this.connection.invoke<string[] | undefined>('GetSerialPorts')) ?? [];
    } catch {
      return [];
    }
  }

  /** The networks of the PC a broadcast or a multicast can go out on. */
  async networks(): Promise<NetworkChoice[]> {
    try {
      return (await this.connection.invoke<NetworkChoice[] | undefined>('GetNetworks')) ?? [];
    } catch {
      return [];
    }
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
    this.channels.set(zeros(this.channelCount()));
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
   *
   * A show runs on the whole screen: going live puts this page on it, going off takes it back.
   * Only on this device, where the key was pressed: the browser allows it only after a press.
   */
  setStackLive(on: boolean): void {
    this.mode.set(IDLE);
    if (on) this.dropOutbox(); // fader moves that are still waiting must not arrive after the faders went down
    if (on) enterFullscreen();
    else leaveFullscreen();
    this.run('SetStackLive', on);
  }

  addStackStep(step: TimedStep): void {
    this.run('AddStackStep', step);
  }

  /**
   * Puts a step at the end of the stack: the first one, or one like the last on the next
   * channel or memory (a release is passed over: the step before it is the pattern). A new first
   * step waits for Play, as a step of the stack mostly does. A new step never adds.
   */
  addStackStepAtEnd(): void {
    if (this.live()) return; // a live stack keeps its steps
    this.addStackStep(this.stepFollowing(this.stack().steps));
  }

  /**
   * Puts a new step right after `row`: the one Add step would make after it (the same kind and
   * times on the next channel or memory; after a release, after the step above it). The desk
   * gives it a number that fits between `row` and the step behind it: after 2.0, with 3.0
   * behind it, 2.1.
   */
  addStackStepAfter(row: StackStep): void {
    if (this.live()) return; // a live stack keeps its steps
    const steps = this.stack().steps;
    const at = steps.findIndex((step) => step.id === row.id);
    this.run('AddStackStepAfter', row.id, this.stepFollowing(steps.slice(0, at + 1)));
  }

  /** The step that comes after the last of `steps` that is no release, or the first step of all. */
  private stepFollowing(steps: readonly StackStep[]): TimedStep {
    const pattern = steps.filter((step) => step.target !== 'release').at(-1);
    return pattern ? stepAfter(pattern, this.stage(), this.channelCount()) : firstStep(0);
  }

  /** Changes a step of the stack. A step without `add` adds no more: the desk sends it only when it is true. */
  updateStackStep(id: number, step: TimedStep): void {
    this.stack.update((stack) => ({
      steps: stack.steps.map((old) => (old.id === id ? { ...old, ...step, add: step.add } : old)),
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

  /** Opens one of the examples that come with the desk program, as loading does; the example itself stays as it is. */
  async loadExample(name: string): Promise<void> {
    this.mode.set(IDLE);
    this.project.set(await this.connection.invoke<ProjectInfo>('LoadExample', name));
  }

  /** The open project as its file says it, for Export. Nothing changes. */
  exportProject(): Promise<string> {
    return this.connection.invoke<string>('ExportProject');
  }

  /**
   * Opens a project file from this device (Import) in place of the open project, as loading does.
   * The project is called `name` and has no file in the projects folder until it is saved.
   */
  async importProjectFile(name: string, text: string): Promise<void> {
    this.mode.set(IDLE);
    this.dropOutbox();
    this.project.set(await this.connection.invoke<ProjectInfo>('ImportProjectFile', name, text));
  }

  /** Puts a stage from a file of this device (Import) in place of the project's: its lights, groups and picture. */
  async importStage(text: string): Promise<void> {
    await this.connection.invoke<void>('ImportStage', text);
  }

  // ---------------------------------------------------------------- the notes of the project

  /**
   * The notes as the Notes view wrote them: shown here at once, and kept by the desk program,
   * which tells the other pages. Rejects with the desk program's message when it refuses them
   * (while the stack is live, or when they are too long).
   */
  async setNotes(notes: string): Promise<void> {
    this.notes.set(notes);
    try {
      await this.connection.invoke<void>('SetNotes', notes);
    } catch (error) {
      void this.fetchNotes(); // the notes the desk program keeps, in place of those it refused
      throw error;
    }
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
      this.boxes.set(welcome.boxes ?? []);
      this.outputs.set(welcome.outputs ?? []);
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
   * the classes on <html> ("medium", "narrow", "mobile", "phone") that the style sheets go by. The
   * limits are the page's pixels: a page zoomed to 120 % (Settings) has fewer of them in the
   * window, and the browser's media queries count the window's, so they are zoomed with it.
   */
  private followWindowSize(): void {
    if (typeof matchMedia !== 'function') return; // not in a browser, for example in the tests

    const classes = document.documentElement.classList;
    let forget = (): void => undefined;
    const follow = (zoom: number): void => {
      const below = (pixels: number): number => pixels * zoom - 0.02; // "narrower than", also for half pixels
      const medium = matchMedia(`(max-width: ${below(MEDIUM_BELOW_PX)}px)`);
      const narrow = matchMedia(`(max-width: ${below(NARROW_BELOW_PX)}px)`);
      const mobile = matchMedia(
        `(max-width: ${below(MOBILE_BELOW_WIDTH_PX)}px), (max-height: ${below(MOBILE_BELOW_HEIGHT_PX)}px)`,
      );
      const phone = matchMedia(`(max-width: ${below(PHONE_BELOW_PX)}px)`);
      const queries = [medium, narrow, mobile, phone];

      const update = (): void => {
        this.windowStripsPerRow.set(medium.matches ? STRIPS_PER_ROW_MEDIUM : STRIPS_PER_ROW_WIDE);
        this.mobile.set(mobile.matches);
        this.phone.set(phone.matches);
        classes.toggle('medium', medium.matches);
        classes.toggle('narrow', narrow.matches);
        classes.toggle('mobile', mobile.matches);
        classes.toggle('phone', phone.matches);
      };

      forget();
      update();
      for (const query of queries) query.addEventListener('change', update);
      forget = () => {
        for (const query of queries) query.removeEventListener('change', update);
      };
    };

    // At once for the page as it starts, and again whenever the zoom changes.
    let followed = this.settings.zoom();
    follow(followed);
    effect(() => {
      const zoom = this.settings.zoom();
      if (zoom === followed) return;
      followed = zoom;
      follow(zoom);
    });
  }

  private takeDesk(desk: DeskState): void {
    this.universes.set(desk.universes ?? 1);
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
    if (desk.effectIndependents) this.effectIndependents.set(desk.effectIndependents);
    if (desk.effects) this.takeEffectPlaybacks(desk.effects, true);
    this.master.set(desk.master);
    this.blackout.set(desk.blackout);
    this.bpm.set(desk.bpm ?? DEFAULT_BPM);
    this.rateOn.set(desk.rateOn ?? false);
    this.solo.set(desk.solo ?? NO_SOLO);
    // The order in which faders took the attributes of the lights; the page counts on from there.
    const channelClaims = desk.channelClaims ?? zeros(desk.channels.length);
    const memoryClaims = desk.memoryClaims ?? zeros(MEMORY_COUNT);
    this.channelClaims.set(channelClaims);
    this.memoryClaims.set(memoryClaims);
    this.claimCount = Math.max(this.claimCount, ...channelClaims, ...memoryClaims);
    // The whole desk comes when a memory was stored, copied or emptied, or a project loaded:
    // exactly when a look, a sequence or the stack may be another one.
    void this.fetchMemoryLooks();
    void this.fetchSequencesAndStack();
    void this.fetchStage();
    void this.fetchNotes();
    void this.fetchEffects();
  }

  private async fetchEffects(): Promise<void> {
    try {
      this.effects.set((await this.connection.invoke<Effect[] | undefined>('GetEffects')) ?? []);
      this.effectsKnown.set(true);
    } catch {
      // A desk program from before effects. The Effects view says so.
      this.effects.set([]);
      this.effectsKnown.set(false);
    }
  }

  private async fetchNotes(): Promise<void> {
    try {
      this.notes.set((await this.connection.invoke<string | undefined>('GetNotes')) ?? '');
      this.notesKnown.set(true);
    } catch {
      // A desk program from before notes. The Notes view says so.
      this.notes.set('');
      this.notesKnown.set(false);
    }
  }

  private async fetchStage(): Promise<void> {
    try {
      this.takeStage((await this.connection.invoke<StagePlan | undefined>('GetStage')) ?? EMPTY_STAGE);
      this.stageKnown.set(true);
    } catch {
      // A desk program from before stage plans. The Stage view says so.
      this.takeStage(EMPTY_STAGE);
      this.stageKnown.set(false);
    }
  }

  // The plan as the desk program keeps it, with the picture it names: fetched when that is another one.
  private takeStage(plan: StagePlan): void {
    // A desk program from before pictures and shapes leaves them out: a light is then round and one square big.
    const filled: StagePlan = {
      ...EMPTY_STAGE,
      ...plan,
      lights: (plan.lights ?? []).map((light) => ({ ...light, shape: light.shape ?? 'round', width: light.width ?? 1, depth: light.depth ?? 1 })),
      types: plan.types ?? [],
      groups: plan.groups ?? [],
    };
    this.stage.set(filled);
    if (filled.pictureId === this.stagePictureId) return;

    this.stagePictureId = filled.pictureId;
    if (filled.pictureId === null) {
      this.stagePicture.set(null);
      return;
    }
    const wanted = filled.pictureId;
    this.connection
      .invoke<string | null>('GetStagePicture')
      .then((picture) => {
        if (this.stagePictureId === wanted) this.stagePicture.set(picture ?? null);
      })
      .catch(() => this.stagePicture.set(null));
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
    if (playback.effects) this.takeEffectPlaybacks(playback.effects, true);
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

/** The playback of an effect that has never run: out, its fader at zero. */
function emptyPlayback(number: number): EffectPlayback {
  return { number, running: false, level: 0, speed: 30, size: 100, spread: 100, fading: false, phase: 0, bpm: 30, changed: false };
}

function zeros(count: number): number[] {
  return new Array<number>(count).fill(0);
}

/** The levels of the channel faders with those of `faders` (numbers from 1) at 0: they give their light through the crossfade. */
function withoutChannels(levels: readonly number[], faders: readonly number[]): number[] {
  const copy = levels.slice();
  for (const fader of faders) if (fader >= 1 && fader <= copy.length) copy[fader - 1] = 0;
  return copy;
}

/** The channels a look holds up, as indexes from 0; none for a memory without a look. */
function lookChannels(look: readonly number[] | undefined): number[] {
  return (look ?? []).flatMap((level, index) => (level > 0 ? [index] : []));
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
