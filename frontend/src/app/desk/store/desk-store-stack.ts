import { computed } from '@angular/core';
import { enterFullscreen, leaveFullscreen } from '../fullscreen';
import {
  DEFAULT_LOOP,
  firstStep,
  LightGroup,
  MAX_LEVEL,
  MAX_SEQUENCE_STEPS,
  Playback,
  recordedStep,
  Sequence,
  SoundTime,
  StackStep,
  StageLight,
  stepAfter,
  TimedStep,
} from '../desk-types';
import {
  DeskCommand,
  FunctionName,
  IDLE,
  ItemKind,
  itemWaits,
  MemoryContent,
  MemoryScope,
  pressFunction,
  pressChannel,
  pressItem,
  pressMemory,
  Step,
} from '../function-mode';
import { recordedColorOf, typeOf } from '../../fixtures/fixtures';
import { noteInOneLine } from '../notes';
import { channelLabel } from '../universes';
import { NO_PLAYBACK } from './desk-store-helpers';
import { DeskStoreSolo } from './desk-store-solo';

/**
 * A layer of DeskStore: the record keys and what they do, the sequences, the stack and its steps,
 * playing it, and the names and notes of steps.
 */
export abstract class DeskStoreStack extends DeskStoreSolo {
  /** True while GO learns the moments of sounds (Learn on the Stack view), on every page. */
  readonly stackLearning = computed(() => this.playback().stack.learning === true);

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
    const color =
      this.groupLights(group)
        .map((light) => recordedColorOf(light, typeOf(plan, light), shown))
        .find((found) => found) ?? null;
    this.addSequenceStep(mode.memory, recordedStep('group', group.id, color));
  }

  /**
   * What a step stands for, in words: "Channel 5 (U1)", "Memory 2", "Fixture Spot 1", "Group Back",
   * "Release of step 2.0".
   */
  stepName(step: TimedStep): string {
    if (step.target === 'channel') return `Channel ${channelLabel(step.number)}`;
    if (step.target === 'memory') return `Memory ${step.number}`;
    if (step.target === 'effect') return `Effect ${step.number}`;
    if (step.target === 'sound') return `Sound ${step.number}`;
    if (step.target === 'release') {
      const released = this.releasedBy(step);
      return released ? `Release of cue ${released.cue}` : 'Release of no cue';
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
        return (
          this.stage().lights.find((light) => light.id === step.number)?.name ??
          'A light that is gone'
        );
      case 'group':
        return (
          this.stage().groups.find((group) => group.id === step.number)?.name ??
          'A group that is gone'
        );
      case 'effect':
        return this.effectsByNumber().get(step.number)?.name ?? 'No such effect yet';
      case 'sound':
        return this.soundsByNumber().get(step.number)?.name ?? 'No such sound yet';
      case 'release': {
        const released = this.releasedBy(step);
        return released ? this.stepWithNote(released) : 'No cue to take down: choose one';
      }
    }
  }

  /** A step in words with its note, "Channel 5 (U1) (PAR 1 red)", "Fixture Spot 1"; a fixture or a group is named enough. */
  stepWithNote(step: TimedStep): string {
    const name = this.stepName(step);
    if (step.target === 'fixture' || step.target === 'group' || step.target === 'release')
      return name;
    const note = this.stepNote(step);
    return note ? `${name} (${note})` : name;
  }

  /** The step of the stack a release takes down; undefined for a release of none, and for any other step. */
  releasedBy(step: TimedStep): StackStep | undefined {
    return step.target === 'release'
      ? this.stack().steps.find((other) => other.id === step.number)
      : undefined;
  }

  /** A record key; `only`: Copy or Del of the Memories view (looks) or the Sequences view (sequences). */
  pressFunction(name: FunctionName, only?: MemoryScope): void {
    if (this.live()) return; // a live stack locks what is stored
    this.follow(pressFunction(this.mode(), name, only));
  }

  /**
   * A memory key of the Memories view or the Sequences view while Copy or Del waits for one: it
   * works as a memory key of the main view does then. True when it was taken; false when no Copy or
   * Del waits, and the key chooses the memory to look at as always.
   */
  pressViewMemory(memory: number): boolean {
    const mode = this.mode();
    if (!itemWaits(mode)) return false;
    this.follow(pressMemory(mode, memory, this.memoryContent(memory)));
    return true;
  }

  pressMemory(memory: number): void {
    if (this.soloMode() && this.keyIsFree('memory')) return; // the key holds its fader at full while it is held, see holdKey
    this.follow(pressMemory(this.mode(), memory, this.memoryContent(memory)));
  }

  /**
   * The key of an effect or a sound: while Copy or Del waits for one it works on it, as on a memory;
   * otherwise it starts or stops its effect or sound.
   */
  pressItem(of: ItemKind, number: number): void {
    const exists = (of === 'effect' ? this.effectsByNumber() : this.soundsByNumber()).has(number);
    const step = pressItem(this.mode(), of, number, exists);
    if (step) this.follow(step);
    else if (of === 'effect') this.toggleEffect(number);
    else this.toggleSound(number);
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
    const sequence: Sequence = old
      ? { ...old, steps: [...old.steps, step] }
      : { loop: DEFAULT_LOOP, steps: [step] };
    this.sequences.update((all) => ({ ...all, [memory]: sequence }));
    this.markStored(memory, 'sequence');
    this.run('AddSequenceStep', memory, step);
  }

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
  protected stepFollowing(steps: readonly StackStep[]): TimedStep {
    const pattern = steps.filter((step) => step.target !== 'release').at(-1);
    return pattern ? stepAfter(pattern, this.stage(), this.channelCount()) : firstStep(0);
  }

  /**
   * Changes a step of the stack. A step without `add` adds no more, one without `continue` plays on
   * no more, one without `muted` is heard again: the desk sends them only when they are true.
   */
  updateStackStep(id: number, step: TimedStep): void {
    this.stack.update((stack) => ({
      steps: stack.steps.map((old) =>
        old.id === id
          ? { ...old, ...step, add: step.add, continue: step.continue, muted: step.muted }
          : old,
      ),
    }));
    this.run('UpdateStackStep', id, step);
  }

  /**
   * Mutes a cue of a sound or takes it back (Muted): its sound plays unheard. This works while the
   * stack is live too: a sound that plays falls silent, or is heard again, at once.
   */
  setStackStepMuted(id: number, muted: boolean): void {
    this.stack.update((stack) => ({
      steps: stack.steps.map((old) =>
        old.id === id ? { ...old, muted: muted || undefined } : old,
      ),
    }));
    this.connection.invoke('SetStackStepMuted', id, muted).catch((error: Error) => {
      this.say(error.message);
      void this.fetchSequencesAndStack();
    });
  }

  deleteStackStep(id: number): void {
    this.run('DeleteStackStep', id);
  }

  /**
   * Skips a step of the stack or takes it back (Disabled): the stack passes over a skipped step.
   * This works while the stack is live too; the desk refuses it for the step that is on.
   */
  setStackStepDisabled(id: number, disabled: boolean): void {
    this.stack.update((stack) => ({
      steps: stack.steps.map((old) =>
        old.id === id ? { ...old, disabled: disabled || undefined } : old,
      ),
    }));
    this.connection.invoke('SetStackStepDisabled', id, disabled).catch((error: Error) => {
      this.say(error.message);
      void this.fetchSequencesAndStack();
    });
  }

  /**
   * Skips a step of the sequence of a memory or takes it back (Disabled); `index` counts from 0.
   * This works while the stack is live too; the desk refuses it for the step that plays just then.
   */
  setSequenceStepDisabled(memory: number, index: number, disabled: boolean): void {
    const old = this.sequences()[memory];
    if (!old?.steps[index]) return;
    const steps = old.steps.map((step, at) =>
      at === index ? { ...step, disabled: disabled || undefined } : step,
    );
    this.sequences.update((all) => ({ ...all, [memory]: { ...old, steps } }));
    this.connection
      .invoke('SetSequenceStepDisabled', memory, index + 1, disabled)
      .catch((error: Error) => {
        this.say(error.message);
        void this.fetchSequencesAndStack();
      });
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

  /**
   * The moment of a sound a cue comes at instead of GO, or none (null). Also while the stack is
   * live: that is when the moments are tried out.
   */
  setStackStepAt(id: number, at: SoundTime | null): void {
    this.run('SetStackStepAt', id, at);
  }

  /** Learn on or off: while on, GO stores the moment of the sound that plays into the cue it goes to. */
  toggleStackLearning(): void {
    this.run('SetStackLearning', !this.stackLearning());
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

  protected follow(step: Step): void {
    this.mode.set(step.mode);
    if (step.notice) this.say(step.notice);
    if (step.command) this.carryOut(step.command);
  }

  protected carryOut(command: DeskCommand): void {
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
      case 'copyItem':
        if (command.of === 'effect') this.copyEffect(command.from, command.to);
        else this.copySound(command.from, command.to);
        break;
      case 'deleteItem':
        if (command.of === 'effect') this.deleteEffect(command.number);
        else this.deleteSound(command.number);
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

  protected async fetchSequencesAndStack(): Promise<void> {
    try {
      const [sequences, stack, playback] = await Promise.all([
        this.connection.invoke('GetSequences'),
        this.connection.invoke('GetStack'),
        this.connection.invoke('GetPlayback'),
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

  protected takePlayback(playback: Playback): void {
    this.playback.set(playback);
    if (playback.effects) this.takeEffectPlaybacks(playback.effects, true);
    if (playback.sounds) this.takeSoundPlaybacks(playback.sounds, true);
    // A live stack locks the record keys: whatever was begun with one of them is over.
    if (playback.stack.live) this.mode.set(IDLE);
  }
}
