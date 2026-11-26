import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../../desk/desk-store';
import { lanAddressInUse } from '../../desk/desk-types';
import { DeskMidi } from '../desk-midi';
import { OSC_DEFAULT_PORT, OscView } from '../midi';

/** OSC while the desk program has said nothing of it: off, on the usual port. */
const NO_OSC: OscView = { on: false, port: OSC_DEFAULT_PORT, state: 'off', problem: null };

/**
 * OSC on the MIDI view: whether the desk program receives OSC from other programs (QLab, TouchOSC, a
 * show controller), on which UDP port, and where they send to. A message works the key or fader its
 * address names (oscAddress, the list at the right shows them while OSC is on), as a control of a MIDI
 * controller does; the messages are listed with those of the controllers. From another computer they
 * count only while LAN is on. It belongs to the PC, as MIDI does.
 */
@Component({
  selector: 'lm-osc-panel',
  styleUrl: './osc-panel.css',
  templateUrl: './osc-panel.html',
})
export class OscPanel {
  private readonly midi = inject(DeskMidi);
  private readonly store = inject(DeskStore);

  protected readonly osc = computed(() => this.midi.state()?.osc ?? NO_OSC);

  /** False for a desk program of before OSC: the panel says so and offers nothing. */
  protected readonly known = computed(() => this.midi.state()?.osc !== undefined);

  /** What OSC is doing, in words. */
  protected readonly said = computed(() => {
    const osc = this.osc();
    switch (osc.state) {
      case 'listening':
        return `Listening on UDP port ${osc.port}.`;
      case 'failed':
        return osc.problem ?? `UDP port ${osc.port} cannot be used.`;
      default:
        return 'Off.';
    }
  });

  /**
   * Where other programs send to: this PC's address in the network while LAN is on; while it is
   * off, only programs on this PC reach the desk.
   */
  protected readonly where = computed(() => {
    const lan = this.store.lan();
    const address = lan?.on ? lanAddressInUse(lan) : null;
    const port = this.osc().port;
    return address
      ? `Programs on other computers send to ${address.split(':')[0]}, port ${port}; programs on this PC to 127.0.0.1.`
      : `LAN is off: only programs on this PC reach the desk, at 127.0.0.1, port ${port}. With LAN on, other computers do too.`;
  });

  protected toggle(event: Event): void {
    void this.midi.setOsc((event.target as HTMLInputElement).checked, this.osc().port);
  }

  /** A port typed: a whole number from 1 to 65535 is taken, anything else is put back as it was. */
  protected setPort(event: Event): void {
    const field = event.target as HTMLInputElement;
    const typed = field.value.trim();
    const port = /^\d{1,5}$/.test(typed) ? Number(typed) : 0;
    if (port < 1 || port > 65535) {
      field.value = String(this.osc().port);
      return;
    }
    if (port !== this.osc().port) void this.midi.setOsc(this.osc().on, port);
  }

  protected finish(event: Event): void {
    (event.target as HTMLInputElement).blur();
  }
}
