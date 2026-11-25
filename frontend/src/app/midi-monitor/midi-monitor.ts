import { Component, computed, inject } from '@angular/core';
import { DeskMidi } from '../desk/desk-midi';
import type { MidiMonitorLine } from '../desk/midi';
import { resultText } from '../desk/midi-targets';

/** How many of the last messages the list shows. */
const SHOWN = 40;

/**
 * The messages that came in from the devices the desk uses, the newest first, each with what it did:
 * for finding out what a control sends, and why it does nothing ("waits until the control reaches
 * it", "not mapped").
 */
@Component({
  selector: 'lm-midi-monitor',
  styleUrl: './midi-monitor.css',
  templateUrl: './midi-monitor.html',
})
export class MidiMonitor {
  private readonly midi = inject(DeskMidi);

  protected readonly lines = computed(() => [...this.midi.lines()].reverse().slice(0, SHOWN));

  /** When it came, as "18:42:05". */
  protected time(line: MidiMonitorLine): string {
    const at = new Date(line.at);
    return [at.getHours(), at.getMinutes(), at.getSeconds()]
      .map((part) => String(part).padStart(2, '0'))
      .join(':');
  }

  protected text(line: MidiMonitorLine): string {
    return resultText(line);
  }

  /** Lines that need a look: a control that does nothing, or is refused. */
  protected stands(line: MidiMonitorLine): boolean {
    return ['unmapped', 'no-page', 'pickup', 'refused', 'missing', 'learn-wait'].includes(
      line.result,
    );
  }
}
