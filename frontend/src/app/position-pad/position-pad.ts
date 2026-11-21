import { Component, input, output } from '@angular/core';

/** Where a moving head points: its pan across and its tilt down, each from 0 to 1. */
export interface PadPosition {
  readonly x: number;
  readonly y: number;
}

/**
 * Where a moving head points: a pad to press or drag on (its pan across, its tilt from top to
 * bottom), and a slider for each, which the keyboard moves too. With fine channels the head
 * moves in 65536 steps, otherwise in 256.
 */
@Component({
  selector: 'lm-position-pad',
  styleUrl: './position-pad.css',
  templateUrl: './position-pad.html',
})
export class PositionPad {
  readonly x = input(0.5);
  readonly y = input(0.5);
  /** A new place was chosen. */
  readonly move = output<PadPosition>();

  /** The sliders count in tenths of a per cent. */
  protected readonly steps = 1000;
  private dragPointer: number | null = null;

  protected percent(value: number): string {
    return (value * 100).toFixed(1);
  }

  protected onPadDown(event: PointerEvent): void {
    if (event.button !== 0) return;
    (event.currentTarget as HTMLElement).setPointerCapture?.(event.pointerId);
    this.dragPointer = event.pointerId;
    this.moveTo(event);
  }

  protected onPadMove(event: PointerEvent): void {
    if (event.pointerId === this.dragPointer) this.moveTo(event);
  }

  protected onPadUp(event: PointerEvent): void {
    if (event.pointerId === this.dragPointer) this.dragPointer = null;
  }

  protected onSlider(axis: 'x' | 'y', event: Event): void {
    const value = Number((event.target as HTMLInputElement).value) / this.steps;
    this.move.emit(axis === 'x' ? { x: value, y: this.y() } : { x: this.x(), y: value });
  }

  protected centre(): void {
    this.move.emit({ x: 0.5, y: 0.5 });
  }

  private moveTo(event: PointerEvent): void {
    const box = (event.currentTarget as HTMLElement).getBoundingClientRect();
    if (box.width <= 0 || box.height <= 0) return;
    this.move.emit({
      x: Math.min(1, Math.max(0, (event.clientX - box.left) / box.width)),
      y: Math.min(1, Math.max(0, (event.clientY - box.top) / box.height)),
    });
  }
}
