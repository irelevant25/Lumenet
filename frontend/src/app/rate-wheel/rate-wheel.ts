import { Component, computed, input, linkedSignal, output } from '@angular/core';

/** The steps of the wheel from x1 to x10, and as many from x1 to x0.1: each step about 6 % faster or slower. */
const STEPS_PER_TENFOLD = 40;
const LOWEST_STEP = -STEPS_PER_TENFOLD;
const HIGHEST_STEP = STEPS_PER_TENFOLD;

/** How far the wheel turns for one step, and how far a pointer moves sideways for one. */
const DEGREES_PER_STEP = 15;
const PIXELS_PER_STEP = 6;

/** The marks around the wheel: short lines from the outside towards the middle. */
const TICK_COUNT = 24;

/** The step of the wheel for a rate: x1 is 0, x10 is 40, x0.1 is -40. */
export function stepOfRate(rate: number): number {
  return Math.round(STEPS_PER_TENFOLD * Math.log10(rate));
}

/** The rate of a step of the wheel, to two decimals. */
export function rateOfStep(step: number): number {
  const kept = Math.min(HIGHEST_STEP, Math.max(LOWEST_STEP, step));
  return Math.round(10 ** (kept / STEPS_PER_TENFOLD) * 100) / 100;
}

/**
 * The rate wheel of the master section: a grey wheel with a big dot that turns with it, and
 * marks around it that stay. Drag it sideways (to the right: faster), turn the mouse wheel over
 * it, or use the arrow keys; a double click sets it back to x1. Locked (dim) while RATE is dark.
 */
@Component({
  host: {
    role: 'slider',
    'aria-label': 'Rate',
    'aria-valuemin': '0.1',
    'aria-valuemax': '10',
    '[attr.aria-valuenow]': 'rate()',
    '[attr.aria-valuetext]': "'x' + rate().toFixed(2)",
    '[attr.aria-disabled]': 'disabled() || null',
    '[attr.tabindex]': 'disabled() ? -1 : 0',
    '[class.disabled]': 'disabled()',
    '(pointerdown)': 'onPointerDown($event)',
    '(pointermove)': 'onPointerMove($event)',
    '(pointerup)': 'onPointerUp($event)',
    '(pointercancel)': 'onPointerUp($event)',
    '(wheel)': 'onWheel($event)',
    '(keydown)': 'onKey($event)',
    '(dblclick)': 'turnTo(0)',
  },
  selector: 'lm-rate-wheel',
  styleUrl: './rate-wheel.css',
  templateUrl: './rate-wheel.html',
})
export class RateWheel {
  /** The rate, from 0.1 to 10. */
  readonly value = input.required<number>();
  /** True while RATE is dark: the wheel does not turn. */
  readonly disabled = input(false);
  readonly valueChange = output<number>();

  /**
   * The step the wheel stands at (x1 is 0). It follows the input, and it is also set the moment
   * the wheel turns: the input only follows a little later, and two quick notches must count as
   * two. (Near x0.1 two steps can have the same rate to two decimals; the wheel turns on all the
   * same, so that the next notch gets further.)
   */
  private readonly step = linkedSignal(() => stepOfRate(this.value()));

  /** The rate of the step the wheel stands at. */
  protected readonly rate = computed(() => rateOfStep(this.step()));

  /** How far the wheel stands turned. */
  protected readonly angle = computed(() => this.step() * DEGREES_PER_STEP);

  /** The marks around the wheel, as lines in the drawing's units (it is 100 wide). */
  protected readonly ticks = Array.from({ length: TICK_COUNT }, (_, index) => {
    const turn = (index / TICK_COUNT) * 2 * Math.PI;
    const point = (radius: number) => ({ x: 50 + radius * Math.sin(turn), y: 50 - radius * Math.cos(turn) });
    const outer = point(48);
    const inner = point(42);
    return { x1: outer.x, y1: outer.y, x2: inner.x, y2: inner.y };
  });

  // Where a drag began: the pointer's x and the step of the wheel then.
  private drag: { x: number; step: number; pointer: number } | null = null;

  protected onPointerDown(event: PointerEvent): void {
    if (this.disabled() || event.button !== 0) return;
    (event.currentTarget as Element).setPointerCapture?.(event.pointerId);
    this.drag = { x: event.clientX, step: this.step(), pointer: event.pointerId };
    event.preventDefault();
  }

  protected onPointerMove(event: PointerEvent): void {
    if (!this.drag || event.pointerId !== this.drag.pointer) return;
    this.turnTo(this.drag.step + Math.round((event.clientX - this.drag.x) / PIXELS_PER_STEP));
  }

  protected onPointerUp(event: PointerEvent): void {
    if (this.drag?.pointer === event.pointerId) this.drag = null;
  }

  protected onWheel(event: WheelEvent): void {
    if (this.disabled() || event.deltaY === 0) return;
    event.preventDefault();
    this.turnTo(this.step() + (event.deltaY < 0 ? 1 : -1));
  }

  protected onKey(event: KeyboardEvent): void {
    if (this.disabled()) return;
    const step = this.step();
    const to: Record<string, number> = {
      ArrowUp: step + 1,
      ArrowRight: step + 1,
      ArrowDown: step - 1,
      ArrowLeft: step - 1,
      PageUp: step + 5,
      PageDown: step - 5,
      Home: LOWEST_STEP,
      End: HIGHEST_STEP,
    };
    if (!(event.key in to)) return;
    event.preventDefault();
    this.turnTo(to[event.key]);
  }

  /** Turns the wheel to a step (x1 is 0) and tells its rate. */
  protected turnTo(step: number): void {
    const kept = Math.min(HIGHEST_STEP, Math.max(LOWEST_STEP, step));
    if (this.disabled() || kept === this.step()) return;
    this.step.set(kept);
    this.valueChange.emit(rateOfStep(kept));
  }
}
