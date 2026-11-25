import {
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { DeskMidi } from '../desk/desk-midi';
import { DeskStore } from '../desk/desk-store';
import { targetLabel } from '../desk/midi-targets';
import { bringToTop } from '../desk/scroll';
import { MidiDevices } from '../midi-devices/midi-devices';
import { MidiMonitor } from '../midi-monitor/midi-monitor';
import { MidiTargets } from '../midi-targets/midi-targets';

/** A page that has just loaded gets its MIDI a moment later: #midi brings the panel into sight again then (ms). */
const SETTLE_MS = 700;

/**
 * MIDI, a panel of Settings: the MIDI controllers plugged into the PC and what their controls do.
 * At its top the devices (midi-devices), what Learn waits for, and the messages that came in
 * (midi-monitor); under them every key and fader of the desk, kind by kind, each with its controls,
 * Learn and ✕ (midi-targets). Only the PC sets them: they belong to it (see desk/midi.ts).
 */
@Component({
  host: {
    id: 'midi',
    role: 'region',
    'aria-labelledby': 'midi-title',
    '(keydown.escape)': 'stop()',
  },
  imports: [MidiDevices, MidiMonitor, MidiTargets],
  selector: 'lm-midi-panel',
  styleUrl: './midi-panel.css',
  templateUrl: './midi-panel.html',
})
export class MidiPanel {
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
    // The list of incoming messages, while the panel is shown on the PC; Learn stops when it closes.
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

    // Settings opened at #midi (back from Listen): the panel comes into sight, and once more a
    // moment later, when a page that has just loaded has its MIDI.
    const host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    const injector = inject(Injector);
    inject(ActivatedRoute)
      .fragment.pipe(takeUntilDestroyed())
      .subscribe((fragment) => {
        if (fragment !== 'midi') return;
        afterNextRender(() => bringToTop(host), { injector });
        setTimeout(() => bringToTop(host), SETTLE_MS);
      });
  }

  /** Stops Learn (Esc too). */
  protected stop(): void {
    if (this.midi.state()?.learning) void this.midi.learn(null);
  }
}
