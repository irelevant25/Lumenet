import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../../desk/desk-store';
import { TimedStep } from '../../desk/desk-types';
import { momentWords } from '../moments';

/**
 * Where the stack stands, on three cards: the step before the one that is on, that one with
 * what the stack is doing with it, and the one Play goes to, each with the number of its step big
 * at the right. The Stack view shows them side by side; the Stage view, with the class "column",
 * one under the other.
 */
@Component({
  selector: 'lm-stack-cards',
  styleUrl: './stack-cards.css',
  templateUrl: './stack-cards.html',
})
export class StackCards {
  private readonly store = inject(DeskStore);

  protected readonly steps = computed(() => this.store.stack().steps);
  private readonly playback = computed(() => this.store.playback().stack);
  private readonly live = this.store.live;
  protected readonly previous = computed(() => this.store.stackPosition().previous);
  protected readonly current = computed(() => this.store.stackPosition().current);
  protected readonly next = computed(() => this.store.stackPosition().next);

  /** What the stack is doing with the current step, in a few words. */
  protected readonly progress = computed(() => {
    if (!this.live()) return this.steps().length > 0 ? 'The stack is not live.' : '';

    const { state, paused, remaining } = this.playback();
    const left = `${remaining.toFixed(1)} s`;
    if (paused) return remaining > 0 ? `Paused, with ${left} left. GO goes on.` : 'Paused. GO goes on.';

    // a release takes its step down: it has no way up, only that
    const release = this.current()?.target === 'release';
    switch (state) {
      case 'fading':
        return release ? `Taking its cue down, ${left} left.` : `On its way up, ${left} left.`;
      case 'waiting':
        return `${release ? 'Done' : 'Up'}. The stack goes on by itself in ${left}.`;
      case 'holding': {
        const next = this.next();
        const waits = next?.at ? `The stack waits for ${this.moment(next)}, or GO.` : 'The stack waits for GO.';
        return `${release ? 'Done' : 'Up'}. ${next ? waits : 'It is the only cue.'}`;
      }
      default:
        return 'GO goes to the first cue.';
    }
  });

  /**
   * A step in words: "Channel 5 (U1) (PAR 1 red)", "Fixture Spot 1", "adds" after one that comes on
   * top of what is on, "plays on" after a sound that does; "Release of step 2.0: Memory 2 (Wash)". Its
   * value is in the table.
   */
  protected describe(step: TimedStep): string {
    if (step.target === 'release') {
      const released = this.store.releasedBy(step);
      return released ? `${this.store.stepName(step)}: ${this.store.stepWithNote(released)}` : this.store.stepName(step);
    }
    return `${this.store.stepWithNote(step)}${step.add ? ', adds' : ''}${step.continue ? ', plays on' : ''}`;
  }

  /** The times of a step in words, for the "next" card; first the moment of a sound it comes at. */
  protected times(step: TimedStep): string {
    const wait = step.wait > 0 ? `then goes on after ${step.wait.toFixed(1)} s` : 'then waits for GO';
    const times =
      step.target === 'release'
        ? `Takes it down in ${step.down.toFixed(1)} s, ${wait}.`
        : `Up in ${step.up.toFixed(1)} s, ${wait}.`;
    return step.at ? `Comes at ${this.moment(step)}. ${times}` : times;
  }

  /** The moment of a sound a step comes at, "0:31.25 of Thunder". */
  private moment(step: TimedStep): string {
    return step.at ? momentWords(step.at, this.store.soundsByNumber()) : '';
  }
}
