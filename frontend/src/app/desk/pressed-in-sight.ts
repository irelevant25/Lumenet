import { Directive, ElementRef, afterRenderEffect, inject, input } from '@angular/core';
import { bringIntoBox } from './scroll';

/**
 * Keeps the lit key of a column of page keys in sight. Its value is the page that is shown: when
 * another one is shown (by its key, a shortcut or another view), the column scrolls just so far
 * that its pressed key (aria-pressed) shows whole. A column scrolled by hand stays where it is until
 * then, and a column that does not scroll is left alone.
 */
@Directive({ selector: '[lmPressedInSight]' })
export class PressedInSight {
  readonly shown = input.required<number>({ alias: 'lmPressedInSight' });

  constructor() {
    const column = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    afterRenderEffect(() => {
      this.shown();
      const pressed = column.querySelector<HTMLElement>('[aria-pressed="true"]');
      if (pressed) bringIntoBox(column, pressed);
    });
  }
}
