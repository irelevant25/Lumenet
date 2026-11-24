import { Component, computed, inject, input, signal } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { OutputPlan, OutputView } from '../desk/desk-types';
import { newPlan, stateText, withOutput, withPlan, withPort, withoutOutput } from '../desk/outputs';
import { LampButton } from '../lamp-button/lamp-button';

/** One output of the project's list, with its place in the whole list (which SetOutputs takes). */
interface Listed {
  readonly view: OutputView;
  readonly index: number;
}

/**
 * The USB section of the Outputs window: DMX USB interfaces that speak the protocol of the Enttec
 * DMX USB Pro (DMXking ultraDMX and others), each at a serial port of the PC, each playing one
 * universe of the project. An interface can be plugged in and pulled out while the desk runs; it
 * shows as not answering meanwhile, and plays again once it is back.
 */
@Component({
  imports: [LampButton],
  selector: 'lm-usb-outputs',
  styleUrls: ['../outputs-dialog/output-section.css', './usb-outputs.css'],
  templateUrl: './usb-outputs.html',
})
export class UsbOutputs {
  protected readonly store = inject(DeskStore);

  /** The serial ports of the PC: "COM3", "/dev/ttyUSB0". The window reads them again every 2 seconds while it is open. */
  readonly serialPorts = input<readonly string[]>([]);

  protected readonly busy = signal(false);
  protected readonly error = signal('');
  /** The port chosen to add; empty for the first one that is not in use. */
  protected readonly chosen = signal('');

  protected readonly interfaces = computed<readonly Listed[]>(() =>
    this.store
      .outputs()
      .map((view, index) => ({ view, index }))
      .filter((item) => item.view.plan.kind === 'usb'),
  );
  /** The ports that no interface of the list has. */
  protected readonly freePorts = computed(() =>
    this.serialPorts().filter(
      (port) => !this.interfaces().some((item) => item.view.plan.target === port),
    ),
  );
  protected readonly locked = computed(() => this.store.live() || this.busy());
  protected readonly universeChoices = computed(() =>
    Array.from({ length: this.store.universes() }, (_, index) => index + 1),
  );

  /** The window opens: an old message goes. */
  reset(): void {
    this.error.set('');
  }

  protected stateOf(view: OutputView): string {
    return stateText(view);
  }

  protected beyond(universe: number): boolean {
    return universe > this.store.universes();
  }

  protected onUniverse(item: Listed, event: Event): void {
    const universe = Number((event.target as HTMLSelectElement).value);
    void this.send(
      withPlan(this.store.outputs(), item.index, (plan) => withPort(plan, 0, { universe })),
    );
  }

  protected onChoose(event: Event): void {
    this.chosen.set((event.target as HTMLSelectElement).value);
  }

  protected remove(item: Listed): void {
    void this.send(withoutOutput(this.store.outputs(), item.index));
  }

  /** The interface at the chosen port joins the list, with a universe that no output plays yet. */
  protected add(): void {
    const free = this.freePorts();
    const port = free.includes(this.chosen()) ? this.chosen() : free[0];
    if (!port) {
      this.error.set(
        'No serial port is free. Plug the interface in: its port shows here a moment later.',
      );
      return;
    }
    void this.send(
      withOutput(
        this.store.outputs(),
        newPlan('usb', port, this.store.boxes(), this.store.outputs(), this.store.universes()),
      ),
    );
  }

  // The new list goes to the desk program; it answers every page with the outputs. True when it took it.
  private async send(plans: OutputPlan[]): Promise<boolean> {
    if (this.locked()) return false;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.store.setOutputs(plans);
      return true;
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : String(error));
      return false;
    } finally {
      this.busy.set(false);
    }
  }
}
