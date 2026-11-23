import { Component, computed, inject, output } from '@angular/core';
import { boxesSummary } from '../desk/boxes';
import { DeskStore } from '../desk/desk-store';

/**
 * How many boxes answer, at the top left: "Boxes connected: 2", or "2 of 3" while the project
 * lists boxes. Its light is green while they answer, amber while a box of the list does not. The
 * pointer on it says what each box plays; a press opens the window of the boxes.
 */
@Component({
  selector: 'lm-boxes-button',
  styleUrl: './boxes-button.css',
  templateUrl: './boxes-button.html',
})
export class BoxesButton {
  private readonly store = inject(DeskStore);

  /** The button was pressed: the window of the boxes is to open. */
  readonly open = output<void>();

  protected readonly summary = computed(() => boxesSummary(this.store.boxes()));

  /** "connected", "missing" (a box of the list does not answer) or "none". */
  protected readonly light = computed(() => {
    const summary = this.summary();
    if (summary.missing > 0) return 'missing';
    return summary.connected > 0 ? 'connected' : 'none';
  });

  /** What each box plays, for the pointer. */
  protected readonly hint = computed(() => {
    const boxes = this.store.boxes();
    if (boxes.length === 0) return 'No box answers. The desk works without one. Click for the boxes.';
    const lines = boxes.map((box) => {
      const plays = box.universes.map((universe) => (universe > 0 ? `U${universe}` : 'off')).join(', ');
      return `${box.address}: ${box.kind === 'new' ? 'new, plays nothing' : box.connected ? plays : `not connected (${plays})`}`;
    });
    return `${lines.join('\n')}\nClick for the boxes.`;
  });
}
