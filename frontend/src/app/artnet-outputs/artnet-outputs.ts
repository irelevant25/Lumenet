import { Component, computed, inject, input, signal } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import {
  FoundNode,
  MAX_OUTPUT_PORTS,
  NetworkChoice,
  OutputPlan,
  OutputView,
} from '../desk/desk-types';
import {
  newPlan,
  planOfNode,
  portAddress,
  portAddressParts,
  portAddressText,
  stateText,
  withNewPort,
  withPlan,
  withPort,
  withOutput,
  withoutOutput,
  withoutPort,
} from '../desk/outputs';
import { LampButton } from '../lamp-button/lamp-button';

/** One output of the project's list, with its place in the whole list (which SetOutputs takes). */
interface Listed {
  readonly view: OutputView;
  readonly index: number;
}

/** The highest Net, and the highest Sub-Net and Universe, of a Port-Address. */
const PART_MAX = { net: 127, subNet: 15, universe: 15 } as const;
type Part = keyof typeof PART_MAX;

/**
 * The Art-Net section of the Outputs window: the nodes the project sends to, each with its state
 * and its ports, and for each port the universe of the project it plays and the Port-Address it
 * goes to (Net, Sub-Net and Universe, as nodes show them). A node is added from the search, which
 * asks every node on the PC's networks to answer, or by its address; a broadcast reaches every
 * node of a network, for older gear. A dmxbox answers the search too, but plays in the dmxbox
 * section: the desk never sends it Art-Net as well.
 */
@Component({
  imports: [LampButton],
  selector: 'lm-artnet-outputs',
  styleUrls: ['../outputs-dialog/output-section.css', './artnet-outputs.css'],
  templateUrl: './artnet-outputs.html',
})
export class ArtNetOutputs {
  protected readonly store = inject(DeskStore);

  /** The networks of the PC a broadcast can go out on. */
  readonly networks = input<readonly NetworkChoice[]>([]);

  protected readonly maxPorts = MAX_OUTPUT_PORTS;
  protected readonly busy = signal(false);
  protected readonly finding = signal(false);
  protected readonly error = signal('');
  /** What the last search found; null before a search. */
  protected readonly found = signal<readonly FoundNode[] | null>(null);

  protected readonly nodes = computed<readonly Listed[]>(() =>
    this.store
      .outputs()
      .map((view, index) => ({ view, index }))
      .filter((item) => item.view.plan.kind === 'artnet'),
  );
  protected readonly locked = computed(() => this.store.live() || this.busy());
  protected readonly universeChoices = computed(() =>
    Array.from({ length: this.store.universes() }, (_, index) => index + 1),
  );

  /** The window opens: an old message and an old search go. */
  reset(): void {
    this.error.set('');
    this.found.set(null);
  }

  protected stateOf(view: OutputView): string {
    return stateText(view);
  }

  protected part(address: number, which: Part): number {
    return portAddressParts(address)[which];
  }

  protected partMax(which: Part): number {
    return PART_MAX[which];
  }

  protected beyond(universe: number): boolean {
    return universe > this.store.universes();
  }

  /** "Network of 192.168.1.20 (Wi-Fi)", for the list of networks of a broadcast. */
  protected networkLabel(choice: NetworkChoice): string {
    return `${choice.address} (${choice.adapter})`;
  }

  /** A network a broadcast was given that this PC is not in now: it stays chosen, and says so. */
  protected networkGone(plan: OutputPlan): boolean {
    return (
      plan.network !== '' && !this.networks().some((choice) => choice.network === plan.network)
    );
  }

  protected portsText(node: FoundNode): string {
    return node.ports.length === 0
      ? 'no ports listed'
      : `ports at ${node.ports.map(portAddressText).join(', ')}`;
  }

  /** True for a found node the project sends to already. */
  protected isListed(node: FoundNode): boolean {
    return this.nodes().some((item) => item.view.plan.target === node.ip);
  }

  protected onUniverse(item: Listed, port: number, event: Event): void {
    const universe = Number((event.target as HTMLSelectElement).value);
    void this.send(
      withPlan(this.store.outputs(), item.index, (plan) => withPort(plan, port, { universe })),
    );
  }

  /** A Net, Sub-Net or Universe typed: the Port-Address of the port changes; a number out of range is refused. */
  protected onPart(item: Listed, port: number, which: Part, event: Event): void {
    const field = event.target as HTMLInputElement;
    const value = Number(field.value);
    const address = item.view.plan.ports[port].address;
    if (!Number.isInteger(value) || value < 0 || value > PART_MAX[which]) {
      this.error.set(
        `The ${which === 'subNet' ? 'Sub-Net' : which === 'net' ? 'Net' : 'Universe'} is 0 to ${PART_MAX[which]}.`,
      );
      field.value = String(this.part(address, which));
      return;
    }

    const parts = { ...portAddressParts(address), [which]: value };
    const changed = portAddress(parts.net, parts.subNet, parts.universe);
    void this.send(
      withPlan(this.store.outputs(), item.index, (plan) =>
        withPort(plan, port, { address: changed }),
      ),
    );
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

  /** Asks the nodes to answer; the window lists those that did. */
  protected async find(): Promise<void> {
    this.finding.set(true);
    this.error.set('');
    try {
      this.found.set(await this.store.findArtNetNodes());
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : String(error));
    } finally {
      this.finding.set(false);
    }
  }

  protected addFound(node: FoundNode): void {
    void this.send(
      withOutput(
        this.store.outputs(),
        planOfNode(node, this.store.boxes(), this.store.outputs(), this.store.universes()),
      ),
    );
  }

  protected addBroadcast(): void {
    void this.send(withOutput(this.store.outputs(), this.planFor('broadcast')));
  }

  /** A node added by its address. The desk program checks the address. */
  protected async addByHand(event: Event, field: HTMLInputElement): Promise<void> {
    event.preventDefault(); // the form must not close the window
    const address = field.value.trim();
    if (address === '') {
      this.error.set('Type the address of the node, like 192.168.1.50.');
      return;
    }
    if (await this.send(withOutput(this.store.outputs(), this.planFor(address)))) field.value = '';
  }

  private planFor(target: string): OutputPlan {
    return newPlan(
      'artnet',
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
