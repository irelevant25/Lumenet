import { DestroyRef, Directive, ElementRef, computed, inject, input, signal } from '@angular/core';

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
export function keyColumns(
  count: number,
  height: number,
  lowest = KEY_MIN_HEIGHT_PX,
  lead = false,
): KeyLayout {
  if (count <= 0) return { columns: 1, rows: 1 };
  if (!(height > 0)) return { columns: 1, rows: count };

  const fit = Math.max(1, Math.floor((height + KEY_GAP_PX) / (lowest + KEY_GAP_PX)));
  const columns = Math.ceil(count / fit);
  if (!lead || columns === 1) return { columns, rows: Math.ceil(count / columns) };

  // the first key alone in a row over all the columns (Edit), the others in the rows under it
  const rest = count - 1;
  const wide = Math.ceil(rest / Math.max(1, fit - 1));
  return { columns: wide, rows: 1 + Math.ceil(rest / wide) };
}

/**
 * Puts a group of keys into as many columns as its height needs (see keyColumns). The element
 * gets the variables --key-columns and --key-rows; its style sheet lays the keys out with them,
 * for example `grid-template-rows: repeat(var(--key-rows), ...)` with `grid-auto-flow: column`.
 * Its value is the number of keys. With lmKeyColumnsLead the first key stands alone in its row, over
 * all the columns (its style sheet places it so: `grid-column: 1 / -1`).
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
  readonly lead = input(false, { alias: 'lmKeyColumnsLead' });

  private readonly height = signal(0);
  protected readonly layout = computed(() =>
    keyColumns(this.count(), this.height(), KEY_MIN_HEIGHT_PX, this.lead()),
  );

  constructor() {
    if (typeof ResizeObserver === 'undefined') return; // not in a browser, for example in the tests

    const element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    const observer = new ResizeObserver(([entry]) => this.height.set(entry.contentRect.height));
    observer.observe(element);
    inject(DestroyRef).onDestroy(() => observer.disconnect());
  }
}
