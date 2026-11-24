import { Component, computed, inject, output } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { outputName, outputsSummary, playsText, stateText } from '../desk/outputs';

/**
 * How many outputs play, at the top left: "Outputs connected: 2", or "2 of 3" once the project
 * lists boxes or has outputs besides them. The dmxboxes count while they answer; an Art-Net node
 * or a USB interface while it answers, sACN and an Art-Net broadcast while they go out. Its light
 * is green while they play, amber while one of the project's does not. The pointer on it says what
 * each plays; a press opens the Outputs window.
 */
@Component({
  selector: 'lm-outputs-button',
  styleUrl: './outputs-button.css',
  templateUrl: './outputs-button.html',
})
export class OutputsButton {
  private readonly store = inject(DeskStore);

  /** The button was pressed: the Outputs window is to open. */
  readonly open = output<void>();

  protected readonly summary = computed(() => outputsSummary(this.store.boxes(), this.store.outputs()));

  /** "connected", "missing" (one of the project's outputs does not play) or "none". */
  protected readonly light = computed(() => {
    const summary = this.summary();
    if (summary.missing > 0) return 'missing';
    return summary.working > 0 ? 'connected' : 'none';
  });

  /** What each box and each output plays, for the pointer. */
  protected readonly hint = computed(() => {
    const boxes = this.store.boxes();
    const outputs = this.store.outputs();
    if (boxes.length === 0 && outputs.length === 0) {
      return 'No box answers, and the project has no other output. The desk works without one. Click for the outputs.';
    }

    const boxLines = boxes.map((box) => {
      const plays = box.universes.map((universe) => (universe > 0 ? `U${universe}` : 'off')).join(', ');
      return `dmxbox ${box.address}: ${box.kind === 'new' ? 'new, plays nothing' : box.connected ? plays : `not connected (${plays})`}`;
    });
    const outputLines = outputs.map((item) => {
      const plays = playsText(item.plan);
      const state = item.state === 'connected' || item.state === 'sending' ? plays : `${stateText(item).toLowerCase()} (${plays})`;
      return `${outputName(item.plan)}: ${state}`;
    });
    return `${[...boxLines, ...outputLines].join('\n')}\nClick for the outputs.`;
  });
}
