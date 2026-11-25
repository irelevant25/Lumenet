import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { outputName, outputsSummary, playsText, stateText } from '../desk/outputs';

/**
 * How many outputs play, at the top left, under the name: "Outputs connected: 2", or "2 of 3" once
 * the project lists boxes or has outputs besides them. The dmxboxes count while they answer; an
 * Art-Net node or a USB interface while it answers, sACN and an Art-Net broadcast while they go out.
 * Its light is green while they play, amber while one of the project's does not. The pointer on it
 * says what each plays; Settings shows and changes them (the Outputs panel).
 */
@Component({
  selector: 'lm-outputs-status',
  styleUrl: './outputs-status.css',
  templateUrl: './outputs-status.html',
})
export class OutputsStatus {
  private readonly store = inject(DeskStore);

  protected readonly summary = computed(() =>
    outputsSummary(this.store.boxes(), this.store.outputs()),
  );

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
      return 'No box answers, and the project has no other output. The desk works without one. Settings shows the outputs.';
    }

    const boxLines = boxes.map((box) => {
      const plays = box.universes
        .map((universe) => (universe > 0 ? `U${universe}` : 'off'))
        .join(', ');
      return `dmxbox ${box.address}: ${box.kind === 'new' ? 'new, plays nothing' : box.connected ? plays : `not connected (${plays})`}`;
    });
    const outputLines = outputs.map((item) => {
      const plays = playsText(item.plan);
      const state =
        item.state === 'connected' || item.state === 'sending'
          ? plays
          : `${stateText(item).toLowerCase()} (${plays})`;
      return `${outputName(item.plan)}: ${state}`;
    });
    return `${[...boxLines, ...outputLines].join('\n')}\nSettings shows the outputs.`;
  });
}
