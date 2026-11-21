import { Component, computed, inject, viewChild } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import {
  DEFAULT_LOOP,
  MAX_SEQUENCE_STEPS,
  Sequence,
  TimedStep,
  firstStep,
  stepAfter,
  stripNumbers,
} from '../desk/desk-types';
import { LampLevel } from '../desk/lamps';
import { storedMemoryPages } from '../desk/memory-pages';
import { noteInOneLine } from '../desk/notes';
import { SPEED_KEY_STEP, SPEED_POSITIONS, speedAt, speedPosition, speedText } from '../desk/speed';
import { StepChoices, lightChoices, stepChoices } from '../desk/step-choices';
import { Fader } from '../fader/fader';
import { LampButton, LampTone } from '../lamp-button/lamp-button';
import { NoteLines } from '../note-lines/note-lines';
import { StepColorDialog } from '../step-color-dialog/step-color-dialog';
import { StepRow } from '../step-row/step-row';
import { TemplateDialog } from '../template-dialog/template-dialog';

/** How a sequence is played: once, in a loop, or in a loop with a new random order every round. */
type PlayMode = 'once' | 'loop' | 'random';

/**
 * `lines` is what the key shows, a long word split so that the key keeps the size of all keys;
 * `name` is the word for screen readers.
 */
const PLAY_MODES: readonly { mode: PlayMode; lines: readonly string[]; name: string }[] = [
  { mode: 'once', lines: ['Once'], name: 'Once' },
  { mode: 'loop', lines: ['Loop'], name: 'Loop' },
  { mode: 'random', lines: ['Ran', 'dom'], name: 'Random' },
];

/**
 * The Sequences view: the steps of the sequence a memory holds. A memory key chooses the
 * memory; the table under the keys is its sequence, and every change goes to the desk at once.
 *
 * A sequence runs while the fader of its memory is up, in the main view. Each step brings a
 * channel or a memory up to its value, stays for its wait, and goes down again when the next
 * step takes over. A sequence can also be recorded key by key in the main view, with Rec seq,
 * or made from a template here. Its speed fader makes all its times shorter or longer.
 *
 * While the stack is live the view only shows: nothing that is stored can be changed then.
 */
@Component({
  imports: [Fader, LampButton, NoteLines, StepColorDialog, StepRow, TemplateDialog],
  selector: 'lm-sequences-view',
  styleUrl: './sequences-view.css',
  templateUrl: './sequences-view.html',
})
export class SequencesView {
  protected readonly store = inject(DeskStore);
  private readonly templateDialog = viewChild.required(TemplateDialog);
  private readonly colorDialog = viewChild.required(StepColorDialog);

  protected readonly playModes = PLAY_MODES;
  protected readonly speedPositions = SPEED_POSITIONS;
  protected readonly speedKeyStep = SPEED_KEY_STEP;

  /**
   * The memory numbers on the page that is shown: as many as faders stand in a row on this window.
   * What Settings says about the main view does not count here.
   */
  protected readonly numbers = computed(() =>
    stripNumbers(this.store.viewFirstMemory(), this.store.viewMemoriesPerPage()),
  );

  /** The page keys: full for the page that is shown, half for a page with a stored memory. */
  protected readonly pages = computed(() =>
    storedMemoryPages(
      this.store.memoryStored(),
      this.store.viewMemoriesPerPage(),
      this.store.viewFirstMemory(),
    ),
  );

  /** The memory that is worked on. The Memories view looks at the same one. */
  protected readonly memory = this.store.inspectedMemory;

  /** The sequence of that memory, or null when it holds none (yet). */
  protected readonly sequence = computed(() => this.store.sequences()[this.memory()] ?? null);

  /** True when the memory holds a look: that is not turned into a sequence without being asked. */
  protected readonly holdsLook = computed(() => this.store.memoryContent(this.memory()) === 'look');

  protected readonly steps = computed<readonly TimedStep[]>(() => this.sequence()?.steps ?? []);
  protected readonly full = computed(() => this.steps().length >= MAX_SEQUENCE_STEPS);

  /** How the sequence is played. One that does not exist yet will be played in a loop. */
  protected readonly mode = computed<PlayMode>(() => {
    const sequence = this.sequence();
    if (sequence?.random) return 'random';
    return (sequence?.loop ?? DEFAULT_LOOP) ? 'loop' : 'once';
  });

  /** How fast it is played: 1 as its times say. */
  protected readonly speed = computed(() => this.sequence()?.speed ?? 1);
  protected readonly speedPosition = computed(() => speedPosition(this.speed()));
  protected readonly speedText = computed(() => speedText(this.speed()));
  protected readonly speedSaid = computed(() => `${this.speed().toFixed(2)} times`);

  /** True while nothing can be changed here: the stack is live. */
  protected readonly locked = this.store.live;

  /** The step the sequence is on right now, counted from 1; 0 while it does not run. */
  protected readonly runningStep = computed(
    () => this.store.playback().sequenceSteps[this.memory()] ?? 0,
  );

  /**
   * What the number of a step is chosen from: every channel, the memories that hold a look, and
   * the fixtures and groups of the stage plan. A memory that holds a sequence is not offered,
   * because a sequence cannot play another one; neither is an empty memory.
   */
  protected readonly choices = computed<StepChoices>(() => ({
    channel: stepChoices(this.store.channelNames()),
    memory: stepChoices(
      this.store.memoryNotes(),
      (memory) => this.store.memoryContent(memory) === 'look',
    ),
    ...lightChoices(this.store.stage()),
  }));

  /** One sentence about the memory that is worked on. */
  protected readonly summary = computed(() => {
    const memory = this.memory();
    if (!this.store.playbackKnown()) {
      return 'The desk program is older than this page and knows no sequences. Start run.cmd again.';
    }

    const live = this.locked() ? ' The stack is live, so nothing can be changed here.' : '';
    if (this.holdsLook()) return `Memory ${memory} holds a look, not a sequence.${live}`;
    if (!this.sequence()) {
      return this.locked()
        ? `Memory ${memory} is empty.${live}`
        : `Memory ${memory} is empty. Add step or Template starts a sequence in it.`;
    }

    const count = this.steps().length;
    const how = { once: 'once', loop: 'in a loop', random: 'in a random order' }[this.mode()];
    const speed = this.speed() === 1 ? '' : ` at ${this.speedText()} speed`;
    const what = `Memory ${memory} holds a sequence of ${count} ${count === 1 ? 'step' : 'steps'}, played ${how}${speed}.`;
    const running = this.runningStep();
    return running
      ? `${what} It is running: step ${running}.${live}`
      : `${what} It runs while the fader of memory ${memory} is up.${live}`;
  });

  /** Full for the memory that is worked on, half for any other that holds something. */
  protected keyLamp(memory: number): LampLevel {
    if (memory === this.memory()) return 3;
    return this.store.memoryStored()[memory - 1] ? 2 : 1;
  }

  /** Red, or red running into yellow for a memory that holds a sequence. */
  protected keyTone(memory: number): LampTone {
    return this.store.memorySequence()[memory - 1] ? 'sequence' : 'memory';
  }

  /** The note under the fader of the step's channel or memory, or the name of its fixture or group, in one line. */
  protected noteOf(step: TimedStep): string {
    return this.store.stepNote(step);
  }

  /** The color of a step of a fixture or a group was pressed: the window asks for another. */
  protected pickColor(index: number, step: TimedStep): void {
    this.colorDialog().open(`Color of step ${index + 1}`, step.color ?? null, (color) => this.changeStep(index, { ...step, color }));
  }

  protected setMode(mode: PlayMode): void {
    if (this.sequence() && mode === this.mode()) return;
    this.save(this.steps(), mode);
  }

  /** Opens the window that fills the sequence from a template. */
  protected openTemplate(): void {
    this.templateDialog().open(this.memory());
  }

  /** The speed fader was moved: its position becomes a speed in steps of 0.05. */
  protected onSpeedPosition(position: number): void {
    this.store.setSequenceSpeed(this.memory(), speedAt(position));
  }

  protected resetSpeed(): void {
    this.store.setSequenceSpeed(this.memory(), 1);
  }

  /** Puts a step at the end: the first one of a new sequence, or one like the last on the next channel or memory. */
  protected addStep(): void {
    const steps = this.steps();
    const last = steps.at(-1);
    this.save([...steps, last ? stepAfter(last, this.store.stage()) : firstStep(1)]);
  }

  protected changeStep(index: number, step: TimedStep): void {
    this.save(this.steps().map((old, at) => (at === index ? step : old)));
  }

  /** Moves a step one place up (-1) or down (+1). */
  protected moveStep(index: number, by: -1 | 1): void {
    const steps = [...this.steps()];
    const other = index + by;
    if (other < 0 || other >= steps.length) return;
    [steps[index], steps[other]] = [steps[other], steps[index]];
    this.save(steps);
  }

  protected deleteStep(index: number): void {
    this.save(this.steps().filter((_, at) => at !== index));
  }

  /** Sends the sequence to the desk, played as before unless another mode is given, at its speed. */
  private save(steps: readonly TimedStep[], mode: PlayMode = this.mode()): void {
    const sequence: Sequence = {
      loop: mode !== 'once',
      random: mode === 'random',
      speed: this.speed(),
      steps: [...steps],
    };
    this.store.setSequence(this.memory(), sequence);
  }
}
