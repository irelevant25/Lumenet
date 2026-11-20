import {
  DestroyRef,
  Directive,
  ElementRef,
  afterRenderEffect,
  computed,
  inject,
  input,
  numberAttribute,
  signal,
} from '@angular/core';
import { DeskSettings } from './desk-settings';

/** The lowest a key in a column gets before the column goes on in another one: --key-min-h in styles.css. */
export const KEY_MIN_HEIGHT_PX = 20;
/** The room between two keys: --strip-gap in styles.css. */
export const KEY_GAP_PX = 4;

/** How a group of keys stands: in how many columns, and how many keys high. */
export interface KeyLayout {
  readonly columns: number;
  readonly rows: number;
}

/**
 * How `count` keys stand in a place `height` pixels high: in one column as long as they fit,
 * getting flatter down to `lowest` pixels; beyond that in as few columns as needed, side by
 * side, instead of a column that scrolls. Before the height is known, in one column.
 */
export function keyColumns(count: number, height: number, lowest = KEY_MIN_HEIGHT_PX): KeyLayout {
  if (count <= 0) return { columns: 1, rows: 1 };
  if (!(height > 0)) return { columns: 1, rows: count };

  const fit = Math.max(1, Math.floor((height + KEY_GAP_PX) / (lowest + KEY_GAP_PX)));
  const columns = Math.ceil(count / fit);
  return { columns, rows: Math.ceil(count / columns) };
}

/**
 * The lowest a key may get with lettering of `fontSize` pixels in `lines` lines: KEY_MIN_HEIGHT_PX,
 * unless the lettering is bigger than usual (Settings) and needs more.
 */
export function lowestKey(fontSize: number, lines: number, scale: number): number {
  if (scale <= 1 || !(fontSize > 0)) return KEY_MIN_HEIGHT_PX;
  return Math.max(KEY_MIN_HEIGHT_PX, Math.ceil(fontSize * 1.05 * lines + 2)); // the line height of a key, and its border
}

/**
 * Puts a group of keys into as many columns as its height needs (see keyColumns). The element
 * gets the variables --key-columns and --key-rows; its style sheet lays the keys out with them,
 * for example `grid-template-rows: repeat(var(--key-rows), ...)` with `grid-auto-flow: column`.
 * Its value is the number of keys; `lmKeyLines` says how many lines of lettering a key that got
 * flatter must still hold: 2 for a range like 129-192, which bigger lettering breaks after the dash.
 */
@Directive({
  selector: '[lmKeyColumns]',
  host: {
    '[style.--key-columns]': 'layout().columns',
    '[style.--key-rows]': 'layout().rows',
  },
})
export class KeyColumns {
  readonly count = input.required<number>({ alias: 'lmKeyColumns' });
  readonly lines = input(1, { alias: 'lmKeyLines', transform: numberAttribute });

  private readonly height = signal(0);
  private readonly lowest = signal(KEY_MIN_HEIGHT_PX);
  protected readonly layout = computed(() => keyColumns(this.count(), this.height(), this.lowest()));

  constructor() {
    if (typeof ResizeObserver === 'undefined') return; // not in a browser, for example in the tests

    const element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    const settings = inject(DeskSettings);
    const measure = (): void => {
      const key = element.querySelector<HTMLElement>('[lmLamp]');
      const fontSize = key ? parseFloat(getComputedStyle(key).fontSize) : 0;
      this.lowest.set(lowestKey(fontSize, this.lines(), settings.fontScale()));
    };

    const observer = new ResizeObserver(([entry]) => {
      this.height.set(entry.contentRect.height);
      measure();
    });
    observer.observe(element);
    inject(DestroyRef).onDestroy(() => observer.disconnect());

    // Bigger or smaller lettering: measured again once it is drawn.
    afterRenderEffect(() => {
      settings.fontScale();
      this.lines();
      measure();
    });
  }
}
