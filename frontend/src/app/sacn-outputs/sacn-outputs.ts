import { Component, computed, inject, input, signal } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import {
  MAX_OUTPUT_PORTS,
  MAX_PRIORITY,
  MAX_SACN_UNIVERSE,
  NetworkChoice,
  OutputPlan,
  OutputView,
} from '../desk/desk-types';
import {
  newPlan,
  stateText,
  withNewPort,
  withOutput,
  withPlan,
  withPort,
  withoutOutput,
  withoutPort,
} from '../desk/outputs';
import { LampButton } from '../lamp-button/lamp-button';

/** One output of the project's list, with its place in the whole list (which SetOutputs takes). */
interface Listed {
  readonly view: OutputView;
  readonly index: number;
}

/**
 * The sACN section of the Outputs window (ANSI E1.31): by multicast, which every receiver in the
 * network can join, or to the address of one receiver. Each port plays a universe of the project
 * as an sACN universe; the priority decides between two sources of one universe. A receiver
 * cannot answer, so sACN says "Sending" while it goes out.
 */
@Component({
  imports: [LampButton],
  selector: 'lm-sacn-outputs',
  styleUrls: ['../outputs-dialog/output-section.css', './sacn-outputs.css'],
  templateUrl: './sacn-outputs.html',
})
export class SacnOutputs {
  protected readonly store = inject(DeskStore);

  /** The networks of the PC a multicast can go out on. */
  readonly networks = input<readonly NetworkChoice[]>([]);

  protected readonly maxPorts = MAX_OUTPUT_PORTS;
  protected readonly maxUniverse = MAX_SACN_UNIVERSE;
  protected readonly maxPriority = MAX_PRIORITY;
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  protected readonly streams = computed<readonly Listed[]>(() =>
    this.store
      .outputs()
      .map((view, index) => ({ view, index }))
      .filter((item) => item.view.plan.kind === 'sacn'),
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

  protected networkLabel(choice: NetworkChoice): string {
    return `${choice.address} (${choice.adapter})`;
  }

  protected networkGone(plan: OutputPlan): boolean {
    return (
      plan.network !== '' && !this.networks().some((choice) => choice.network === plan.network)
    );
  }

  protected onUniverse(item: Listed, port: number, event: Event): void {
    const universe = Number((event.target as HTMLSelectElement).value);
    void this.send(
      withPlan(this.store.outputs(), item.index, (plan) => withPort(plan, port, { universe })),
    );
  }

  /** The sACN universe of a port: 1 to 63999; another number is refused. */
  protected onAddress(item: Listed, port: number, event: Event): void {
    const field = event.target as HTMLInputElement;
    const address = Number(field.value);
    if (!Number.isInteger(address) || address < 1 || address > MAX_SACN_UNIVERSE) {
      this.error.set(`An sACN universe is 1 to ${MAX_SACN_UNIVERSE}.`);
      field.value = String(item.view.plan.ports[port].address);
      return;
    }
    void this.send(
      withPlan(this.store.outputs(), item.index, (plan) => withPort(plan, port, { address })),
    );
  }

  /** The priority: 0 to 200; another number is refused. */
  protected onPriority(item: Listed, event: Event): void {
    const field = event.target as HTMLInputElement;
    const priority = Number(field.value);
    if (!Number.isInteger(priority) || priority < 0 || priority > MAX_PRIORITY) {
      this.error.set(`The priority is 0 to ${MAX_PRIORITY}.`);
      field.value = String(item.view.plan.priority);
      return;
    }
    void this.send(withPlan(this.store.outputs(), item.index, (plan) => ({ ...plan, priority })));
  }

  protected onNetwork(item: Listed, event: Event): void {
    const network = (event.target as HTMLSelectElement).value;
    void this.send(withPlan(this.store.outputs(), item.index, (plan) => ({ ...plan, network })));
  }

  protected addPort(item: Listed): void {
    void this.send(
      withPlan(this.store.outputs(), item.index, (plan) =>
        withNewPort(plan, this.store.universes()),
      ),
    );
  }

  protected removePort(item: Listed, port: number): void {
    void this.send(withPlan(this.store.outputs(), item.index, (plan) => withoutPort(plan, port)));
  }

  protected remove(item: Listed): void {
    void this.send(withoutOutput(this.store.outputs(), item.index));
  }

  protected addMulticast(): void {
    void this.send(withOutput(this.store.outputs(), this.planFor('multicast')));
  }

  /** A receiver added by its address. The desk program checks the address. */
  protected async addByHand(event: Event, field: HTMLInputElement): Promise<void> {
    event.preventDefault(); // the form must not close the window
    const address = field.value.trim();
    if (address === '') {
      this.error.set('Type the address of the receiver, like 192.168.1.50.');
      return;
    }
    if (await this.send(withOutput(this.store.outputs(), this.planFor(address)))) field.value = '';
  }

  private planFor(target: string): OutputPlan {
    return newPlan(
      'sacn',
      target,
      this.store.boxes(),
      this.store.outputs(),
      this.store.universes(),
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
