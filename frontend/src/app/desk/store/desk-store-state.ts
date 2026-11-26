import { computed, effect, inject, signal } from '@angular/core';
import { DeskConnection, HubArgs, HubMethod } from '../desk-connection';
import { DeskSettings } from '../desk-settings';
import { DEFAULT_BPM } from '../tempo';
import {
  AutosaveOptions,
  BoxView,
  EMPTY_STAGE,
  HistoryState,
  CHANNEL_INDEPENDENT_COUNT,
  LanAccess,
  MAX_LEVEL,
  MEDIUM_BELOW_PX,
  MEMORY_COUNT,
  MEMORY_INDEPENDENT_COUNT,
  MemoryLooks,
  MOBILE_BELOW_HEIGHT_PX,
  MOBILE_BELOW_WIDTH_PX,
  NARROW_BELOW_PX,
  NEW_PROJECT_NAME,
  NO_HISTORY,
  NO_SOLO,
  OutputView,
  pageStart,
  PHONE_BELOW_PX,
  Playback,
  ProjectInfo,
  Sequences,
  SoloState,
  StackState,
  StackStep,
  StagePlan,
  STRIPS_PER_ROW_MEDIUM,
  STRIPS_PER_ROW_WIDE,
  TimedStep,
  UNIVERSE_SIZE,
} from '../desk-types';
import { FunctionMode, IDLE } from '../function-mode';
import {
  applyLimits,
  AttributeLight,
  fixtureLabels,
  lookHolds,
  rolesOf,
  stepLights,
} from '../../fixtures/fixtures';
import { universeOf } from '../universes';
import {
  Effect,
  EFFECT_INDEPENDENT_COUNT,
  EffectPlayback,
  withEffectLevels,
} from '../../effects/effects';
import {
  DEFAULT_SOUND_DECIBELS,
  ListenView,
  MixerView,
  NO_LISTEN,
  NO_MIXER,
  Sound,
  SOUND_INDEPENDENT_COUNT,
  SoundOutput,
  SoundPlayback,
} from '../../sounds/sounds';
import {
  allFalse,
  lookChannels,
  NO_PLAYBACK,
  noNotes,
  NOTICE_MS,
  oneToCount,
  RecordedSteps,
  SEND_DELAY_MS,
  StackPosition,
  withoutChannels,
  zeros,
} from './desk-store-helpers';

/**
 * The first layer of DeskStore: the state of the desk as this page sees it (what the backend holds
 * and what only this page knows), and the way changes go out to the backend: fader moves collected
 * for a moment, calls, and the notices of the top bar. See desk-store.ts.
 */
export abstract class DeskStoreState {
  protected readonly connection = inject(DeskConnection);

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
  /** The sound each sound key of the independents starts and stops. */
  readonly soundIndependents = signal<readonly number[]>(oneToCount(SOUND_INDEPENDENT_COUNT));
  /** The effects of the project, in the order of their numbers. */
  readonly effects = signal<readonly Effect[]>([]);
  /** False while the effects could not be fetched: a desk program from before effects. */
  readonly effectsKnown = signal(false);
  /** The effects by their numbers. */
  readonly effectsByNumber = computed(
    () => new Map(this.effects().map((effect) => [effect.number, effect])),
  );
  /** The playbacks of the effects by their numbers: whether each runs, its fader and its live values. */
  readonly effectPlaybacks = signal<ReadonlyMap<number, EffectPlayback>>(new Map());
  /** The sounds of the project, in the order of their numbers. */
  readonly sounds = signal<readonly Sound[]>([]);
  /** False while the sounds could not be fetched: a desk program from before sounds. */
  readonly soundsKnown = signal(false);
  /** The sounds by their numbers. */
  readonly soundsByNumber = computed(
    () => new Map(this.sounds().map((sound) => [sound.number, sound])),
  );
  /** The playbacks of the sounds by their numbers: whether each plays, its fader, where it is. */
  readonly soundPlaybacks = signal<ReadonlyMap<number, SoundPlayback>>(new Map());
  /** For each sound that played on this page, when it last started: the higher, the later. */
  protected readonly soundStarts = signal<ReadonlyMap<number, number>>(new Map());
  protected soundStartCount = 0;
  /**
   * The sound whose wave the Stack and Stage views show: of the sounds that play, the one that started
   * last; while none plays, the one that played last. Undefined before any played.
   */
  readonly shownSound = computed(() => {
    const starts = this.soundStarts();
    const playbacks = this.soundPlaybacks();
    const sounds = this.soundsByNumber();
    const ranked = [...starts.entries()]
      .filter(([number]) => sounds.has(number))
      .sort((a, b) => b[1] - a[1])
      .map(([number]) => number);
    const playing = ranked.filter((number) => playbacks.get(number)?.playing);
    return playing[0] ?? ranked[0];
  });
  /**
   * The volume of all sounds, -100 dB to 0 dB (every sound at its own level), -20 dB at first; like
   * the master fader it belongs to the evening.
   */
  readonly soundDecibels = signal(DEFAULT_SOUND_DECIBELS);
  /** MUTE: no sound goes out while it is on. */
  readonly soundMute = signal(false);
  /** The device of the sound as the desk program last told this page (only a page on the PC hears of it). */
  readonly soundOutputNews = signal<SoundOutput | null>(null);
  /** The Soundcraft Ui mixer of the tracks: whether it answers, what it is, its playlist. Every page hears of it. */
  readonly mixer = signal<MixerView>(NO_MIXER);
  /** The tracks of the playlist chosen in Settings (or every track), while the mixer answers: the steps and the Sounds view offer them. */
  readonly playlistTracks = signal<readonly string[]>([]);
  protected playlistAsked = 0;
  /** LISTEN ON: what goes out plays on the device to listen on as well. Every page hears of it. */
  readonly listen = signal<ListenView>(NO_LISTEN);
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
    if (this.roles().attributeLights.length === 0)
      return effects ? withEffectLevels(shown ?? own.slice(), effects) : (shown ?? own);

    shown ??= own.slice();
    this.attributesTaken(shown);
    return withEffectLevels(shown, effects);
  });

  /**
   * The stack's crossfade as the desk program counts it: none while a key held with SOLO DOWN is
   * on one of its faders (then every fader gives its light on its own again).
   */
  protected readonly crossfade = computed(() => {
    const cross = this.playback().crossfade;
    if (!cross) return null;
    const held = this.solo().down.some((key) =>
      (key.target === 'channel' ? cross.channels : cross.memories).includes(key.number),
    );
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
  protected attributesTaken(shown: number[]): void {
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
      if (step.target === 'memory')
        return !!looks[step.number] && lookHolds(looks[step.number], light);
      return stepLights(plan, step).some((held) => light.channels.includes(held.address - 1));
    };
    for (const light of this.roles().attributeLights) {
      let latest = -1;
      for (let index = 0; index < MEMORY_COUNT; index++) {
        if (!levels[index] || (latest >= 0 && memoryClaims[index] <= memoryClaims[latest]))
          continue;
        const look = looks[index + 1];
        const sequence = sequences[index + 1];
        const holds = look
          ? lookHolds(look, light)
          : (sequence?.steps ?? []).some((step) => stepHolds(step, light));
        if (holds) latest = index;
      }
      for (const channel of light.attributes) {
        const taken = latest >= 0 && memoryClaims[latest] > channelClaims[channel];
        shown[channel] = taken
          ? (looks[latest + 1]?.[channel] ?? running[channel] ?? 0)
          : own[channel];
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
      levels[index] = roles.attribute[index]
        ? shown[index]
        : Math.floor((levels[index] * master + 127) / MAX_LEVEL);
    }
    return levels;
  });

  /**
   * What the faders give, as a solo leaves it, the same way as the desk program: with keys held
   * with SOLO UP only those shine (a channel at full, a memory's look at full, a sequence as it
   * plays); with keys held with SOLO DOWN their channels and memories give nothing.
   */
  protected readonly soloLevels = computed<readonly number[]>(() => {
    const solo = this.solo();
    const shown = this.shownChannels();
    if (solo.up.length > 0) {
      const levels = zeros(shown.length);
      const raise = (index: number, level: number) =>
        (levels[index] = Math.max(levels[index], level));
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
            step.target === 'channel'
              ? [step.number - 1]
              : step.target === 'memory'
                ? lookChannels(this.memoryLooks()[step.number])
                : stepLights(this.stage(), step).flatMap((light) =>
                    [...light.channels].map((_, offset) => light.address - 1 + offset),
                  );
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
  protected claimCount = 0;
  // The color each fixture that mixes its color had last: brought up from dark, it comes back in it.
  protected readonly lastColors = new Map<number, [number, number, number]>();

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
    // the step Prev goes to: the one before, past the steps that are skipped, going round
    let previous: StackStep | null = null;
    for (let back = 1; index >= 0 && back < steps.length && previous === null; back++) {
      const step = steps[(((index - back) % steps.length) + steps.length) % steps.length];
      if (!step.disabled) previous = step;
    }
    return {
      previous,
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
  protected readonly settings = inject(DeskSettings);
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
  protected readonly wantedChannel = signal(1);
  protected readonly wantedMemory = signal(1);
  /** The first channel of the page that is shown; in the last universe when the project has fewer than it had. */
  readonly firstChannel = computed(() =>
    pageStart(Math.min(this.wantedChannel(), this.channelCount()), this.channelsPerPage()),
  );
  /** The universe whose channels the channel faders show: its U key is lit, and its pages are offered. */
  readonly shownUniverse = computed(() => universeOf(this.firstChannel()));
  /** The first memory of the page that is shown on the main view. */
  readonly firstMemory = computed(() => pageStart(this.wantedMemory(), this.memoriesPerPage()));
  /** The first memory of the page that is shown on the Memories and Sequences views. */
  readonly viewFirstMemory = computed(() =>
    pageStart(this.wantedMemory(), this.viewMemoriesPerPage()),
  );
  /** An effect that must be on the page of effects shown: on the main view a row as long as the memories', on the Effects view the window's. */
  protected readonly wantedEffect = signal(1);
  readonly firstEffect = computed(() => pageStart(this.wantedEffect(), this.stripsPerRow()));
  readonly viewFirstEffect = computed(() =>
    pageStart(this.wantedEffect(), this.viewMemoriesPerPage()),
  );
  /** The effect the editor of the Effects view works on. */
  readonly chosenEffect = signal(1);
  /** A sound that must be on the page of sounds shown: on the main view a row as long as the memories', on the Sounds view the window's. */
  protected readonly wantedSound = signal(1);
  readonly firstSound = computed(() => pageStart(this.wantedSound(), this.stripsPerRow()));
  readonly viewFirstSound = computed(() =>
    pageStart(this.wantedSound(), this.viewMemoriesPerPage()),
  );
  /** The sound the Sounds view works on. */
  readonly chosenSound = signal(1);
  /** Where a record key (save or edit a memory, record a sequence, copy, delete) stands. */
  readonly mode = signal<FunctionMode>(IDLE);
  /** The steps of the sequence that is being recorded, by what they use; null while none is recorded. */
  readonly recordedSteps = computed<RecordedSteps | null>(() => {
    const mode = this.mode();
    if (mode.kind !== 'recording') return null;

    const used = {
      channel: new Map<number, number[]>(),
      memory: new Map<number, number[]>(),
      fixture: new Map<number, number[]>(),
      group: new Map<number, number[]>(),
    };
    (this.sequences()[mode.memory]?.steps ?? []).forEach((step, index) => {
      if (step.target === 'release' || step.target === 'effect' || step.target === 'sound') return; // no key records these
      const users = used[step.target];
      const numbers = users.get(step.number);
      if (numbers) numbers.push(index + 1);
      else users.set(step.number, [index + 1]);
    });
    return {
      channels: used.channel,
      memories: used.memory,
      fixtures: used.fixture,
      groups: used.group,
    };
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
  protected readonly held = new Map<string, { level: number; up: boolean }>();
  /**
   * The rate (RATE, its fader and TEMPO): a tempo for all sequences, in beats per minute, and
   * whether it counts. While it does, every sequence takes one step per beat.
   */
  readonly bpm = signal(DEFAULT_BPM);
  readonly rateOn = signal(false);
  // The taps of TEMPO that count, in milliseconds of the page (see desk/tempo.ts).
  protected taps: number[] = [];
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
  protected stagePictureId: string | null = null;
  // True once this page said that unsaved work was brought back.
  protected recoveryTold = false;

  protected readonly loaded = signal(false);

  /** True when the page is connected to the desk program and has received its state. */
  readonly online = computed(() => this.connection.online() && this.loaded());

  /** Fader moves waiting to be sent: one entry per fader, so only its newest value goes out. */
  protected readonly outbox = new Map<string, { method: HubMethod; args: unknown[] }>();
  // Effects waiting to be stored, by number; and when this page last moved a fader or live value of
  // an effect, by "3 level", "3 speed" and so on.
  protected readonly effectTimers = new Map<number, ReturnType<typeof setTimeout>>();
  protected readonly effectTouched = new Map<string, number>();
  // Sounds: by number, the one that went to the desk program a moment ago (until SOUND_SEND_MS is
  // over) with the change that waits for its turn; how many times this page changed each; and when
  // this page last moved a fader of one.
  protected readonly soundSends = new Map<
    number,
    { timer: ReturnType<typeof setTimeout>; waiting: Sound | null }
  >();
  protected readonly soundChanges = new Map<number, number>();
  protected readonly soundTouched = new Map<number, number>();
  protected outboxTimer: ReturnType<typeof setTimeout> | null = null;
  protected noticeTimer: ReturnType<typeof setTimeout> | null = null;

  /** Asks the backend to do something and shows its message if it refuses. */
  protected run<Method extends HubMethod>(method: Method, ...args: HubArgs<Method>): void {
    this.connection.invoke(method, ...args).catch((error: Error) => this.say(error.message));
  }

  protected sendSoon<Method extends HubMethod>(
    key: string,
    method: Method,
    ...args: HubArgs<Method>
  ): void {
    this.outbox.set(key, { method, args });
    this.outboxTimer ??= setTimeout(() => this.flushOutbox(), SEND_DELAY_MS);
  }

  protected flushOutbox(): void {
    const waiting = [...this.outbox.values()];
    this.dropOutbox();
    // each entry was typed when it went in (sendSoon)
    for (const { method, args } of waiting)
      this.connection.send(method, ...(args as HubArgs<typeof method>));
  }

  protected dropOutbox(): void {
    if (this.outboxTimer !== null) clearTimeout(this.outboxTimer);
    this.outboxTimer = null;
    this.outbox.clear();
  }

  /** Shows a few words for the owner, as the desk's own notices are shown. */
  tell(text: string): void {
    this.say(text);
  }

  protected say(text: string, ms = NOTICE_MS): void {
    this.notice.set(text);
    if (this.noticeTimer !== null) clearTimeout(this.noticeTimer);
    this.noticeTimer = setTimeout(() => this.notice.set(''), ms);
  }

  /**
   * Keeps the layout in step with the size of the window: the number of faders in a row, and
   * the classes on <html> ("medium", "narrow", "mobile", "phone") that the style sheets go by. The
   * limits are the page's pixels: a page zoomed to 120 % (Settings) has fewer of them in the
   * window, and the browser's media queries count the window's, so they are zoomed with it.
   */
  protected followWindowSize(): void {
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
}
