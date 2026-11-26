import { Component, ElementRef, afterRenderEffect, computed, inject, viewChild } from '@angular/core';
import { DeskSettings } from '../../desk/desk-settings';
import { DeskStore } from '../../desk/desk-store';
import { WheelPlace, wheelPlaces, wheelRoll } from '../stack-wheel';
import { pageZoom } from '../../shared/zoom';
import { LampButton } from '../../shared/lamp-button/lamp-button';

/** Places made beyond those seen, on each side, for the wheel to roll in from. */
const EXTRA_PLACES = 1;

/** How long the wheel rolls on to another step. */
const ROLL_MS = 240;

/**
 * The stack on the main view: a section across the top of the three columns at the right. Its
 * keys stand in the first column: Stack takes the stack live, as the key of the Stack view does;
 * Prev, Play, Pause and Next go through it.
 *
 * Beside the keys, over the other two columns, stands a wheel of the stack's steps, each number
 * with its note: the one that is on in the middle (green, with the arrow beside it), the one
 * before it above (blue), the one Play goes to below (yellow), the others grey. Settings says how
 * many it shows: 1, 3, 5, 7 or 9. When another step comes on, the wheel rolls to it; on a low
 * window it shows fewer, always the middle.
 */
@Component({
  imports: [LampButton],
  selector: 'lm-stack-section',
  styleUrl: './stack-section.css',
  templateUrl: './stack-section.html',
})
export class StackSection {
  protected readonly store = inject(DeskStore);
  private readonly settings = inject(DeskSettings);
  private readonly roller = viewChild<ElementRef<HTMLElement>>('roller');

  protected readonly live = this.store.live;
  protected readonly empty = computed(() => this.store.stack().steps.length === 0);
  protected readonly size = this.settings.wheelSize;

  /** The place of the step that is on in the stack (-1 while none is), and of the one Play goes to. */
  private readonly position = computed(() => {
    const steps = this.store.stack().steps;
    const { currentId, nextId } = this.store.playback().stack;
    return {
      count: steps.length,
      current: currentId === null ? -1 : steps.findIndex((step) => step.id === currentId),
      next: nextId === null ? -1 : steps.findIndex((step) => step.id === nextId),
    };
  });

  protected readonly places = computed(() => {
    const { current, next } = this.position();
    return wheelPlaces(this.store.stack().steps, current, next, this.size(), EXTRA_PLACES);
  });

  /** Where the wheel stood when it was drawn last. */
  private drawn: { count: number; current: number } | null = null;

  constructor() {
    // Another step came on: the wheel starts where it stood and rolls to the new one.
    afterRenderEffect(() => {
      const { count, current } = this.position();
      const roller = this.roller()?.nativeElement;
      const before = this.drawn;
      this.drawn = { count, current };
      if (!roller || before === null || before.count !== count) return;

      const reach = (this.size() - 1) / 2 + EXTRA_PLACES;
      const roll = Math.max(-reach, Math.min(reach, wheelRoll(before.current, current, count)));
      if (roll === 0 || typeof roller.animate !== 'function' || lessMotion()) return;

      // in the page's pixels, as the move is (see zoom.ts)
      const placeHeight = (roller.firstElementChild?.getBoundingClientRect().height ?? 0) / pageZoom();
      roller.animate([{ transform: `translateY(${roll * placeHeight}px)` }, { transform: 'none' }], {
        duration: ROLL_MS,
        easing: 'cubic-bezier(0.2, 0.8, 0.3, 1)',
      });
    });
  }

  /** True for a place inside the wheel's window; the extra ones only roll in. */
  protected seen(place: WheelPlace): boolean {
    return Math.abs(place.offset) <= (this.size() - 1) / 2;
  }

  /**
   * The note beside a step's number: the note of its channel or memory, the name of its fixture
   * or group, what a release takes down; for a step without one, what it is ("Channel 5 (U1)").
   */
  protected noteOf(place: WheelPlace): string {
    if (!place.step) return '';
    return this.store.stepNote(place.step) || this.store.stepName(place.step);
  }

  /** What a screen reader says for a place, and the pointer on it; null for one that says nothing. */
  protected placeName(place: WheelPlace): string | null {
    if (!this.seen(place)) return null;
    if (!place.step) return place.offset === 0 ? 'No cue is on' : null;
    const step = `${place.step.cue}, ${this.noteOf(place)}${place.step.disabled ? ', skipped' : ''}`;
    switch (place.kind) {
      case 'previous':
        return `Previous cue: ${step}`;
      case 'current':
        return `Current cue: ${step}`;
      case 'next':
        return `Next cue: ${step}`;
      default:
        return `Cue ${step}`;
    }
  }
}

/** True for people who asked their system for less motion: then the wheel does not roll. */
function lessMotion(): boolean {
  return typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
}
