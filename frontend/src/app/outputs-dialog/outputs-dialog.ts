import {
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ArtNetOutputs } from '../artnet-outputs/artnet-outputs';
import { BoxOutputs } from '../box-outputs/box-outputs';
import { DeskStore } from '../desk/desk-store';
import { NetworkChoice } from '../desk/desk-types';
import { outputName, sharedUniverses, unplayedUniverses } from '../desk/outputs';
import { LampButton } from '../lamp-button/lamp-button';
import { SacnOutputs } from '../sacn-outputs/sacn-outputs';
import { UsbOutputs } from '../usb-outputs/usb-outputs';

/** How often the window reads the serial ports of the PC while it is open. */
const PORTS_EVERY_MS = 2000;

/**
 * The Outputs window, opened from the top left: where every universe of the project goes. A
 * section for each kind of output, one under the other: the dmxboxes, Art-Net nodes, sACN, and
 * DMX USB interfaces. Over them stands what needs saying about all of them together: a universe
 * that reaches no cable, a universe on more than one output, an output that would reach a
 * dmxbox. Nothing goes out on the network unless an output is added here.
 */
@Component({
  imports: [ArtNetOutputs, BoxOutputs, LampButton, SacnOutputs, UsbOutputs],
  selector: 'lm-outputs-dialog',
  styleUrl: './outputs-dialog.css',
  templateUrl: './outputs-dialog.html',
})
export class OutputsDialog {
  protected readonly store = inject(DeskStore);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly boxSection = viewChild.required(BoxOutputs);
  private readonly artNetSection = viewChild.required(ArtNetOutputs);
  private readonly sacnSection = viewChild.required(SacnOutputs);
  private readonly usbSection = viewChild.required(UsbOutputs);

  /** The networks of the PC a broadcast or a multicast can go out on, read when the window opens. */
  protected readonly networks = signal<readonly NetworkChoice[]>([]);
  /** The serial ports of the PC, read when the window opens and every 2 seconds while it is: an interface plugged in shows. */
  protected readonly serialPorts = signal<readonly string[]>([]);
  private portsTimer: ReturnType<typeof setInterval> | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.onClosed());
  }

  /** What needs saying about all outputs together. */
  protected readonly remarks = computed(() => {
    const boxes = this.store.boxes();
    const outputs = this.store.outputs();
    const remarks: string[] = [];
    const unplayed = unplayedUniverses(boxes, outputs, this.store.universes());
    if (unplayed.length > 0) {
      const names = unplayed.map((universe) => `U${universe}`).join(', ');
      const one = unplayed.length === 1;
      remarks.push(
        `${names} ${one ? 'reaches' : 'reach'} no cable: no output that answers or sends plays ${one ? 'it' : 'them'}.`,
      );
    }
    for (const universe of sharedUniverses(boxes, outputs)) {
      remarks.push(`U${universe} is on more than one output, as with a splitter.`);
    }
    for (const output of outputs.filter((item) => item.state === 'conflict')) {
      remarks.push(
        `${outputName(output.plan)} is a dmxbox, which plays in the dmxbox section: nothing goes out to it here.`,
      );
    }
    return remarks;
  });

  open(): void {
    for (const section of [
      this.boxSection(),
      this.artNetSection(),
      this.sacnSection(),
      this.usbSection(),
    ])
      section.reset();
    void this.readNetworks();
    void this.readSerialPorts();
    if (this.portsTimer === null)
      this.portsTimer = setInterval(() => void this.readSerialPorts(), PORTS_EVERY_MS);
    this.dialog().nativeElement.showModal();
  }

  protected close(): void {
    this.dialog().nativeElement.close();
  }

  /** The window closed, by OK or by Esc: the serial ports are not read any more. */
  protected onClosed(): void {
    if (this.portsTimer !== null) clearInterval(this.portsTimer);
    this.portsTimer = null;
  }

  private async readSerialPorts(): Promise<void> {
    const ports = await this.store.serialPorts();
    const known = this.serialPorts();
    if (ports.length !== known.length || ports.some((port, index) => port !== known[index]))
      this.serialPorts.set(ports);
  }

  private async readNetworks(): Promise<void> {
    this.networks.set(await this.store.networks());
  }
}
