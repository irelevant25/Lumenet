import { Component, computed, inject } from '@angular/core';
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
import { StepChoices, stepChoices } from '../desk/step-choices';
import { LampButton, LampTone } from '../lamp-button/lamp-button';
import { NoteLines } from '../note-lines/note-lines';
import { StepRow } from '../step-row/step-row';

/**
 * The Sequences view: the steps of the sequence a memory holds. A memory key chooses the
 * memory; the table under the keys is its sequence, and every change goes to the desk at once.
 *
 * A sequence runs while the fader of its memory is up, in the main view. Each step brings a
 * channel or a memory up to its value, stays for its wait, and goes down again when the next
 * step takes over. A sequence can also be recorded key by key in the main view, with Rec seq.
 *
 * While the stack is live the view only shows: nothing that is stored can be changed then.
 */
@Component({
  imports: [LampButton, NoteLines, StepRow],
  selector: 'lm-sequences-view',
  styleUrl: './sequences-view.css',
  templateUrl: './sequences-view.html',
})
export class SequencesView {
  protected readonly store = inject(DeskStore);

  /** The memory numbers on the page that is shown: as many as memory faders stand in a row. */
  protected readonly numbers = computed(() =>
    stripNumbers(this.store.firstMemory(), this.store.memoriesPerPage()),
  );

  /** The page keys: full for the page that is shown, half for a page with a stored memory. */
  protected readonly pages = computed(() =>
    storedMemoryPages(
      this.store.memoryStored(),
      this.store.memoriesPerPage(),
      this.store.firstMemory(),
    ),
  );

  /** The memory that is worked on. The Memories view looks at the same one. */
  protected readonly memory = this.store.inspectedMemory;

  /** The sequence of that memory, or null when it holds none (yet). */
  private readonly sequence = computed(() => this.store.sequences()[this.memory()] ?? null);

  /** True when the memory holds a look: that is not turned into a sequence without being asked. */
  protected readonly holdsLook = computed(() => this.store.memoryContent(this.memory()) === 'look');

  protected readonly steps = computed<readonly TimedStep[]>(() => this.sequence()?.steps ?? []);
  /** A sequence that does not exist yet will be played in a loop. */
  protected readonly loop = computed(() => this.sequence()?.loop ?? DEFAULT_LOOP);
  protected readonly full = computed(() => this.steps().length >= MAX_SEQUENCE_STEPS);

  /** True while nothing can be changed here: the stack is live. */
  protected readonly locked = this.store.live;

  /** The step the sequence is on right now, counted from 1; 0 while it does not run. */
  protected readonly runningStep = computed(
    () => this.store.playback().sequenceSteps[this.memory()] ?? 0,
  );

  /**
   * What the number of a step is chosen from: every channel, and the memories that hold a
   * look. A memory that holds a sequence is not offered, because a sequence cannot play
   * another one; neither is an empty memory.
   */
  protected readonly choices = computed<StepChoices>(() => ({
    channel: stepChoices(this.store.channelNotes()),
    memory: stepChoices(
      this.store.memoryNotes(),
      (memory) => this.store.memoryContent(memory) === 'look',
    ),
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
        : `Memory ${memory} is empty. Add step starts a sequence in it.`;
    }

    const count = this.steps().length;
    const what = `Memory ${memory} holds a sequence of ${count} ${count === 1 ? 'step' : 'steps'}, played ${this.loop() ? 'in a loop' : 'once'}.`;
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

  /** The note under the fader of the step's channel or memory, in one line. */
  protected noteOf(step: TimedStep): string {
    const notes = step.target === 'channel' ? this.store.channelNotes() : this.store.memoryNotes();
    return noteInOneLine(notes[step.number - 1] ?? '');
  }

  protected setLoop(loop: boolean): void {
    if (this.sequence() && loop === this.loop()) return;
    this.save(loop, this.steps());
  }

  /** Puts a step at the end: the first one of a new sequence, or one like the last on the next channel or memory. */
  protected addStep(): void {
    const steps = this.steps();
    const last = steps.at(-1);
    this.save(this.loop(), [...steps, last ? stepAfter(last) : firstStep(1)]);
  }

  protected changeStep(index: number, step: TimedStep): void {
    this.save(this.loop(), this.steps().map((old, at) => (at === index ? step : old)));
  }

  /** Moves a step one place up (-1) or down (+1). */
  protected moveStep(index: number, by: -1 | 1): void {
    const steps = [...this.steps()];
    const other = index + by;
    if (other < 0 || other >= steps.length) return;
    [steps[index], steps[other]] = [steps[other], steps[index]];
    this.save(this.loop(), steps);
  }

  protected deleteStep(index: number): void {
    this.save(this.loop(), this.steps().filter((_, at) => at !== index));
  }

  /** Sends the sequence to the desk. */
  private save(loop: boolean, steps: readonly TimedStep[]): void {
    const sequence: Sequence = { loop, steps: [...steps] };
    this.store.setSequence(this.memory(), sequence);
  }
}
