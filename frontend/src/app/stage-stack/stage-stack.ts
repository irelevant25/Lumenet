import {
  Component,
  ElementRef,
  afterRenderEffect,
  computed,
  inject,
  viewChild,
} from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { TimedStep } from '../desk/desk-types';
import { StackCards } from '../stack-cards/stack-cards';
import { StackKeys } from '../stack-keys/stack-keys';

/**
 * The stack at the left of the Stage view, to run a show while the plan shows its lights: the
 * keys of the Stack view (Stack, Prev, Play, Pause, Next), its three cards one under the other,
 * and its steps with their notes and times, a tick at those that add, and the steps that are on
 * marked. Nothing can be changed here: the Stack view does that.
 *
 * The list of steps follows the stack: when another step comes on and it or the next one is out
 * of sight, the list scrolls so that the step before it stands at the top.
 */
@Component({
  imports: [StackCards, StackKeys],
  selector: 'lm-stage-stack',
  styleUrl: './stage-stack.css',
  templateUrl: './stage-stack.html',
})
export class StageStack {
  private readonly store = inject(DeskStore);
  private readonly tableBox = viewChild.required<ElementRef<HTMLElement>>('tableBox');

  protected readonly steps = computed(() => this.store.stack().steps);
  protected readonly previous = computed(() => this.store.stackPosition().previous?.id);
  protected readonly current = computed(() => this.store.stackPosition().current?.id);
  protected readonly next = computed(() => this.store.stackPosition().next?.id);
  /** The steps that are on: the current one and those it came on top of (Add). */
  protected readonly on = this.store.stackOn;

  constructor() {
    afterRenderEffect(() => {
      if (this.current() !== undefined) followStack(this.tableBox().nativeElement);
    });
  }

  /** The note under the fader of the step's channel or memory, or the name of its fixture or group. */
  protected noteOf(step: TimedStep): string {
    return this.store.stepNote(step);
  }

  protected seconds(value: number): string {
    return value.toFixed(1);
  }
}

/**
 * Scrolls the list (only the list: never the page, whose top bar must stay) so that the step
 * that is on and the next one under it are in sight, with the step before it at the top where
 * there is room. Nothing moves while they are in sight already.
 */
function followStack(box: HTMLElement): void {
  const row = box.querySelector<HTMLElement>('tr.current');
  if (!row) return;

  const frame = box.getBoundingClientRect().top - box.scrollTop;
  const top = (element: Element) => element.getBoundingClientRect().top - frame;
  const bottom = (element: Element) => element.getBoundingClientRect().bottom - frame;

  // the next step counts only where it comes after this one: from the last, the list starts again
  const next = row.nextElementSibling?.classList.contains('next') ? row.nextElementSibling : row;
  const above = row.previousElementSibling ?? row;
  const seen = top(row) >= box.scrollTop && bottom(next) <= box.scrollTop + box.clientHeight;
  if (seen) return;

  // the step before at the top, unless the next one would then be out of sight; never past this one
  const wanted = Math.max(
    0,
    Math.min(Math.max(top(above), bottom(next) - box.clientHeight), top(row)),
  );
  const smooth =
    typeof matchMedia === 'function' && !matchMedia('(prefers-reduced-motion: reduce)').matches;
  box.scrollTo({ top: wanted, behavior: smooth ? 'smooth' : 'auto' });
}
