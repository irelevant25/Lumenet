import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { TimedStep } from '../desk/desk-types';
import { StackCards } from '../stack-cards/stack-cards';
import { StackKeys } from '../stack-keys/stack-keys';

/**
 * The stack at the left of the Stage view, to run a show while the plan shows its lights: the
 * keys of the Stack view (Stack, Prev, Play, Pause, Next), its three cards one under the other,
 * and its steps with their notes and times, a tick at those that add, and the steps that are on
 * marked. Nothing can be changed here: the Stack view does that.
 */
@Component({
  imports: [StackCards, StackKeys],
  selector: 'lm-stage-stack',
  styleUrl: './stage-stack.css',
  templateUrl: './stage-stack.html',
})
export class StageStack {
  private readonly store = inject(DeskStore);

  protected readonly steps = computed(() => this.store.stack().steps);
  protected readonly previous = computed(() => this.store.stackPosition().previous?.id);
  protected readonly current = computed(() => this.store.stackPosition().current?.id);
  protected readonly next = computed(() => this.store.stackPosition().next?.id);
  /** The steps that are on: the current one and those it came on top of (Add). */
  protected readonly on = this.store.stackOn;

  /** The note under the fader of the step's channel or memory, or the name of its fixture or group. */
  protected noteOf(step: TimedStep): string {
    return this.store.stepNote(step);
  }

  protected seconds(value: number): string {
    return value.toFixed(1);
  }
}
