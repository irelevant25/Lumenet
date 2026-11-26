import { Component, DestroyRef, computed, effect, inject } from '@angular/core';
import { DeskMidi } from '../desk-midi';
import { DeskStore } from '../../desk/desk-store';
import { targetLabel } from '../midi-targets';
import { MidiDevices } from '../midi-devices/midi-devices';
import { MidiMonitor } from '../midi-monitor/midi-monitor';
import { MidiTargets } from '../midi-targets/midi-targets';

/**
 * MIDI, a view of its own: the MIDI controllers plugged into the PC and what their controls do.
 * At the left the devices with the ready-made mappings (midi-devices), what Learn waits for, and
 * the messages that came in (midi-monitor); at the right every key and fader of the desk, kind by
 * kind, each with its controls, Learn and ✕ (midi-targets). Only the PC sets them: they belong to
 * it (see midi/midi.ts).
 */
@Component({
  host: { '(keydown.escape)': 'stop()' },
  imports: [MidiDevices, MidiMonitor, MidiTargets],
  selector: 'lm-midi-view',
  styleUrl: './midi-view.css',
  templateUrl: './midi-view.html',
})
export class MidiView {
  protected readonly midi = inject(DeskMidi);
  private readonly store = inject(DeskStore);

  /** True on the PC: a phone or a tablet sees that only the PC sets MIDI. */
  protected readonly pc = computed(() => this.store.lan()?.local !== false);

  /** How many controls are mapped. */
  protected readonly mapped = computed(() => this.midi.state()?.mappings.length ?? 0);

  /** What Learn waits for, as it is called; null while it waits for nothing. */
  protected readonly learning = computed(() => {
    const target = this.midi.state()?.learning;
    return target ? targetLabel(target) : null;
  });

  constructor() {
    // The list of incoming messages, while the view is shown on the PC; Learn stops when it closes.
    let watching = false;
    effect(() => {
      if (watching || !this.pc() || this.midi.state() === null) return;
      watching = true;
      void this.midi.watch(true);
    });
    inject(DestroyRef).onDestroy(() => {
      if (!watching) return;
      void this.midi.watch(false);
      if (this.midi.state()?.learning) void this.midi.learn(null);
    });
  }

  /** Stops Learn (Esc too). */
  protected stop(): void {
    if (this.midi.state()?.learning) void this.midi.learn(null);
  }
}
