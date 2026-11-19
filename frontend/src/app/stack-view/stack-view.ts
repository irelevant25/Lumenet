import { Component, computed, inject, signal } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { StackStep, TimedStep, firstStep, stepAfter } from '../desk/desk-types';
import { splitNote } from '../desk/notes';
import { LampButton } from '../lamp-button/lamp-button';
import { StepRow } from '../step-row/step-row';

/** Where a dragged step would land: before or after the step with this id. */
interface DropMark {
  id: number;
  after: boolean;
}

/**
 * The Stack view: the one stack of the project. Its steps are gone through with Play, Pause,
 * Next and Previous. Going to a step moves the real fader of its channel or memory up to the
 * step's value; leaving it moves that fader back to zero.
 *
 * The steps stand in the order of their numbers ("2.0", "2.1"). A step is moved by dragging
 * its grip, or with the arrow keys at the end of its row; the desk then gives it a number
 * that fits its new place.
 */
@Component({
  imports: [LampButton, StepRow],
  selector: 'lm-stack-view',
  styleUrl: './stack-view.css',
  templateUrl: './stack-view.html',
})
export class StackView {
  protected readonly store = inject(DeskStore);

  protected readonly steps = computed(() => this.store.stack().steps);
  private readonly playback = computed(() => this.store.playback().stack);

  protected readonly current = computed(() => this.stepWithId(this.playback().currentId));
  protected readonly next = computed(() => this.stepWithId(this.playback().nextId));
  protected readonly paused = computed(() => this.playback().paused);

  /** True while the stack moves by itself: a fade or a countdown that is not paused. */
  protected readonly running = computed(() => {
    const { state, paused } = this.playback();
    return !paused && (state === 'fading' || state === 'waiting');
  });

  /** The step that is being dragged, and where it would land. */
  private readonly dragged = signal<number | null>(null);
  protected readonly dropMark = signal<DropMark | null>(null);

  protected readonly summary = computed(() => {
    if (!this.store.playbackKnown()) {
      return 'The desk program is older than this page and knows no stack. Start run.cmd again.';
    }
    const count = this.steps().length;
    if (count === 0) return 'The stack is empty. Add step puts in the first step.';
    return `The stack has ${count} ${count === 1 ? 'step' : 'steps'}. Going to a step moves the fader of its channel or memory.`;
  });

  /** What the stack is doing with the current step, in a few words. */
  protected readonly progress = computed(() => {
    const { state, paused, remaining } = this.playback();
    const left = `${remaining.toFixed(1)} s`;
    if (paused) return remaining > 0 ? `Paused, with ${left} left. Play goes on.` : 'Paused. Play goes on.';

    switch (state) {
      case 'fading':
        return `On its way up, ${left} left.`;
      case 'waiting':
        return `Up. The stack goes on by itself in ${left}.`;
      case 'holding':
        return this.next() ? 'Up. The stack waits for Play.' : 'Up. This is the last step.';
      default:
        return this.steps().length > 0 ? 'Play goes to the next step.' : '';
    }
  });

  /** A step in words: "Channel 5 (PAR 1 red) at 200". */
  protected describe(step: TimedStep): string {
    const notes = step.target === 'channel' ? this.store.channelNotes() : this.store.memoryNotes();
    const note = splitNote(notes[step.number - 1] ?? '').join(' ').trim();
    const name = `${step.target === 'channel' ? 'Channel' : 'Memory'} ${step.number}`;
    return `${note ? `${name} (${note})` : name} at ${step.value}`;
  }

  /** The times of a step in words, for the "next" card. */
  protected times(step: TimedStep): string {
    const wait = step.wait > 0 ? `then goes on after ${step.wait.toFixed(1)} s` : 'then waits for Play';
    return `Up in ${step.up.toFixed(1)} s, ${wait}.`;
  }

  /** Puts a step at the end: the first one, or one like the last on the next channel or memory. */
  protected addStep(): void {
    const last = this.steps().at(-1);
    // A wait of 0 is the stack's usual case: it stays on a step until Play is pressed.
    this.store.addStackStep(last ? stepAfter(last) : firstStep(0));
  }

  /** A row hands back its step with one field changed. Only the step itself goes to the desk, not its name and number. */
  protected changeStep(id: number, step: TimedStep): void {
    const { target, number, value, up, wait, down } = step;
    this.store.updateStackStep(id, { target, number, value, up, wait, down });
  }

  /**
   * The number typed for a step. The field goes back to the number the step has; if the desk
   * takes the new one, it sends the stack again and the field shows it.
   */
  protected renumber(step: StackStep, event: Event): void {
    const field = event.target as HTMLInputElement;
    const typed = field.value.trim();
    field.value = step.cue;
    if (typed !== '' && typed !== step.cue) this.store.renumberStackStep(step.id, typed);
  }

  protected finish(event: Event): void {
    (event.target as HTMLInputElement).blur();
  }

  /** One place up: right after the step that stands two places above, or at the very top. */
  protected moveUp(index: number): void {
    const steps = this.steps();
    if (index === 0) return;
    this.store.moveStackStep(steps[index].id, index >= 2 ? steps[index - 2].id : null);
  }

  /** One place down: right after the step that follows. */
  protected moveDown(index: number): void {
    const steps = this.steps();
    if (index >= steps.length - 1) return;
    this.store.moveStackStep(steps[index].id, steps[index + 1].id);
  }

  // ---------------------------------------------------------------- dragging a step by its grip

  protected onDragStart(event: DragEvent, step: StackStep): void {
    const row = (event.target as HTMLElement).closest('tr');
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData('text/plain', step.cue);
      if (row) event.dataTransfer.setDragImage(row, 16, 16);
    }
    this.dragged.set(step.id);
  }

  /** While a step is dragged over a row: the upper half means "before this step", the lower "after". */
  protected onDragOver(event: DragEvent, step: StackStep): void {
    if (this.dragged() === null) return;
    event.preventDefault(); // without this the browser allows no drop here

    const row = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const after = event.clientY > row.top + row.height / 2;
    const mark = this.dropMark();
    if (mark?.id !== step.id || mark.after !== after) this.dropMark.set({ id: step.id, after });
  }

  protected onDrop(event: DragEvent, index: number): void {
    event.preventDefault();
    const id = this.dragged();
    const mark = this.dropMark();
    this.onDragEnd();
    if (id === null || !mark) return;

    const steps = this.steps();
    const before = index > 0 ? steps[index - 1].id : null;
    const afterId = mark.after ? steps[index].id : before;
    if (afterId !== id) this.store.moveStackStep(id, afterId);
  }

  protected onDragEnd(): void {
    this.dragged.set(null);
    this.dropMark.set(null);
  }

  private stepWithId(id: number | null): StackStep | null {
    return id === null ? null : (this.steps().find((step) => step.id === id) ?? null);
  }
}
