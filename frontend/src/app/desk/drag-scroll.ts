import { DestroyRef, Directive, ElementRef, inject } from '@angular/core';
import { pageZoom } from './zoom';

/** How far a press moves, in pixels of the page, before it pulls the column instead of pressing its key. */
const PULL_PX = 6;

/**
 * A column of keys that scrolls (the page keys) can be pulled with the mouse as well: pressed, held and
 * moved, it scrolls with the pointer, as a finger scrolls it on a touch screen; the wheel scrolls it too.
 * A press that moves less than a few pixels stays a press of its key; one that pulled the column does not
 * press the key it began on. A finger and a pen scroll it as the browser does.
 */
@Directive({
  selector: '[lmDragScroll]',
  host: {
    '(pointerdown)': 'onDown($event)',
    '(pointermove)': 'onMove($event)',
    '(pointerup)': 'onUp($event)',
    '(pointercancel)': 'onUp($event)',
  },
})
export class DragScroll {
  private readonly box = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private start: { pointer: number; x: number; y: number; left: number; top: number } | null = null;
  private pulling = false;
  private swallow = false;

  constructor() {
    // the click that ends a pull is no press of the key it began on
    const click = (event: MouseEvent): void => {
      if (!this.swallow) return;
      this.swallow = false;
      event.stopPropagation();
      event.preventDefault();
    };
    this.box.addEventListener('click', click, true);
    inject(DestroyRef).onDestroy(() => this.box.removeEventListener('click', click, true));
  }

  protected onDown(event: PointerEvent): void {
    this.swallow = false;
    if (event.pointerType !== 'mouse' || event.button !== 0) return;
    const box = this.box;
    if (box.scrollHeight <= box.clientHeight + 1 && box.scrollWidth <= box.clientWidth + 1) return;
    this.start = {
      pointer: event.pointerId,
      x: event.clientX,
      y: event.clientY,
      left: box.scrollLeft,
      top: box.scrollTop,
    };
  }

  protected onMove(event: PointerEvent): void {
    const start = this.start;
    if (!start || event.pointerId !== start.pointer) return;
    // the pointer's moves are in the window's pixels, the scrolling in the page's (see zoom.ts)
    const zoom = pageZoom();
    const x = (event.clientX - start.x) / zoom;
    const y = (event.clientY - start.y) / zoom;
    if (!this.pulling) {
      if (Math.hypot(x, y) < PULL_PX) return;
      this.pulling = true;
      this.box.setPointerCapture?.(event.pointerId);
    }
    this.box.scrollTop = start.top - y;
    this.box.scrollLeft = start.left - x;
  }

  protected onUp(event: PointerEvent): void {
    if (!this.start || event.pointerId !== this.start.pointer) return;
    this.start = null;
    if (!this.pulling) return;
    this.pulling = false;
    this.swallow = true;
    setTimeout(() => (this.swallow = false)); // a click comes right after, or none at all
  }
}
