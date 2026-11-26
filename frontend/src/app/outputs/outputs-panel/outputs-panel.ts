import {
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { withBox, withOutput as withBoxOutput, withoutBox } from '../boxes';
import { DeskStore } from '../../desk/desk-store';
import {
  BoxPlan,
  BoxView,
  FoundNode,
  MAX_BOX_OUTPUTS,
  MAX_OUTPUT_PORTS,
  MAX_PRIORITY,
  MAX_SACN_UNIVERSE,
  NetworkChoice,
  OutputKind,
  OutputPlan,
  OutputView,
} from '../../desk/desk-types';
import {
  boxAbout,
  boxName,
  multicastAddress,
  newPlanWithPorts,
  nodeName,
  outputRemarks,
  planOfNode,
  portAddress,
  portAddressParts,
  stateText,
  withNewPort,
  withOutput,
  withPlan,
  withPort,
  withoutOutput,
  withoutPort,
} from '../outputs';
import { bringToTop } from '../../shared/scroll';
import { LampButton } from '../../shared/lamp-button/lamp-button';

/** How often the panel reads the serial ports of the PC while it is shown: an interface plugged in shows. */
const PORTS_EVERY_MS = 2000;

/** How long a page that has just loaded takes to have its outputs, about. */
const SETTLE_MS = 700;

/** The highest Net, and the highest Sub-Net and Universe, of an Art-Net Port-Address. */
const PART_MAX = { net: 127, subNet: 15, universe: 15 } as const;
type Part = keyof typeof PART_MAX;

/** What "Add an output" adds, with what it asks for and the sentence under it. */
type AddKind = 'artnet' | 'broadcast' | 'multicast' | 'sacn' | 'usb' | 'dmxbox';
const ADD_KINDS: readonly { kind: AddKind; label: string; address: string; hint: string }[] = [
  {
    kind: 'artnet',
    label: 'Art-Net node',
    address: '192.168.1.50',
    hint: 'A node at its address: also one in another network, which the search does not reach.',
  },
  {
    kind: 'broadcast',
    label: 'Art-Net broadcast',
    address: '',
    hint: 'Every Art-Net node of a network takes it, for older gear; the network is chosen in the table.',
  },
  {
    kind: 'multicast',
    label: 'sACN multicast',
    address: '',
    hint: 'Every sACN receiver in the network can take it; the network is chosen in the table.',
  },
  {
    kind: 'sacn',
    label: 'sACN to a receiver',
    address: '192.168.1.60',
    hint: 'sACN to the address of one receiver.',
  },
  {
    kind: 'usb',
    label: 'USB DMX interface',
    address: '',
    hint: 'An Enttec DMX USB Pro, or an interface that speaks its protocol, at a serial port of this PC. An Open DMX USB does not work here yet.',
  },
  {
    kind: 'dmxbox',
    label: 'dmxbox by its address',
    address: '10.1.2.1',
    hint: 'A box with the number n on its switch answers at 10.1.n.1. One added before it is there shows as not answering until it does.',
  },
];

/** The kinds of output as the table names them. */
const KIND_LABELS: Readonly<Record<OutputKind | 'dmxbox', string>> = {
  dmxbox: 'dmxbox',
  artnet: 'Art-Net',
  sacn: 'sACN',
  usb: 'USB DMX',
};

/** A line of the table: an output of a dmxbox, or a port of another output. */
interface Row {
  readonly key: string;
  readonly kind: OutputKind | 'dmxbox';
  /** Its name; after the first line of an output with ", port 2" or ", output 2". */
  readonly name: string;
  /** The line under the name, on the first line of the output. */
  readonly about: string;
  /** connected, missing, sending, conflict or new: the class of the state. */
  readonly state: string;
  readonly stateText: string;
  readonly detail: string;
  readonly first: boolean;
  readonly last: boolean;
  /** The output of the box, or the port, from 0. */
  readonly part: number;
  readonly universe: number;
  readonly box: BoxView | null;
  readonly view: OutputView | null;
  /** The output's place in the project's list, which SetOutputs takes; -1 for a box. */
  readonly index: number;
  /** For screen readers: "output 1 of dmxbox 192.168.7.1", "port 2 of Node 2". */
  readonly label: string;
}

/**
 * The outputs of the project, a panel of Settings: one table with a line for every output of a
 * dmxbox and every port of the other outputs (Art-Net nodes, sACN, DMX USB interfaces), each with
 * its state, the universe it plays and where that goes on the cable. Under it the Art-Net nodes
 * that answered the search, the line that adds an output, and what needs saying about all of them
 * together. dmxboxes are found by themselves; the others send nothing until they are added here.
 * The project keeps the list (SetBoxes, SetOutputs); locked while the stack is live.
 */
@Component({
  host: { id: 'outputs', role: 'region', 'aria-labelledby': 'outputs-title' },
  imports: [LampButton],
  selector: 'lm-outputs-panel',
  styleUrl: './outputs-panel.css',
  templateUrl: './outputs-panel.html',
})
export class OutputsPanel {
  protected readonly store = inject(DeskStore);

  protected readonly addKinds = ADD_KINDS;
  protected readonly portCounts = Array.from({ length: 8 }, (_, index) => index + 1);
  protected readonly maxBoxOutputs = MAX_BOX_OUTPUTS;
  protected readonly maxUniverse = MAX_SACN_UNIVERSE;
  protected readonly maxPriority = MAX_PRIORITY;

  /** The networks of the PC a broadcast or a multicast can go out on. */
  protected readonly networks = signal<readonly NetworkChoice[]>([]);
  protected readonly serialPorts = signal<readonly string[]>([]);
  /** What the search found; null before it answered. */
  protected readonly found = signal<readonly FoundNode[] | null>(null);
  protected readonly finding = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  protected readonly addKind = signal<AddKind>('artnet');
  protected readonly addAddress = signal('');
  protected readonly addCount = signal(1);
  protected readonly addPort = signal('');
  protected readonly addChoice = computed(
    () => ADD_KINDS.find((choice) => choice.kind === this.addKind()) ?? ADD_KINDS[0],
  );

  protected readonly locked = computed(() => this.store.live() || this.busy());
  protected readonly universeChoices = computed(() =>
    Array.from({ length: this.store.universes() }, (_, index) => index + 1),
  );
  /** True while the project lists its boxes: then Auto gives them back to themselves. */
  protected readonly listedBoxes = computed(() =>
    this.store.boxes().some((box) => box.kind === 'listed'),
  );
  protected readonly remarks = computed(() =>
    outputRemarks(this.store.boxes(), this.store.outputs(), this.store.universes()),
  );
  /** The serial ports no interface of the list has. */
  protected readonly freePorts = computed(() =>
    this.serialPorts().filter(
      (port) =>
        !this.store.outputs().some((item) => item.plan.kind === 'usb' && item.plan.target === port),
    ),
  );

  /** The lines of the table: the boxes first, then the other outputs in the project's order. */
  protected readonly rows = computed<readonly Row[]>(() => {
    const rows: Row[] = [];
    for (const box of this.store.boxes()) {
      const name = boxName(box);
      box.universes.forEach((universe, part) =>
        rows.push({
          key: `box ${box.address} ${part}`,
          kind: 'dmxbox',
          name: part === 0 ? name : `${name}, output ${part + 1}`,
          about: boxAbout(box),
          state: box.kind === 'new' ? 'new' : box.connected ? 'connected' : 'missing',
          stateText:
            box.kind === 'new'
              ? 'New, plays nothing'
              : box.connected
                ? 'Connected'
                : 'Not answering',
          detail: '',
          first: part === 0,
          last: part === box.universes.length - 1,
          part,
          universe,
          box,
          view: null,
          index: -1,
          label: `output ${part + 1} of dmxbox ${box.address}`,
        }),
      );
    }

    const found = this.found() ?? [];
    this.store.outputs().forEach((view, index) => {
      const { name, about } = this.naming(view, found);
      const ports = view.plan.ports;
      ports.forEach((port, part) =>
        rows.push({
          key: `output ${index} ${part}`,
          kind: view.plan.kind,
          name: part === 0 ? name : `${name}, port ${part + 1}`,
          about:
            view.plan.kind === 'sacn' && view.plan.target === 'multicast'
              ? `${multicastAddress(port.address)}, ${about}`
              : about,
          state: view.state,
          stateText: stateText(view),
          detail: this.detailOf(view, name),
          first: part === 0,
          last: part === ports.length - 1,
          part,
          universe: port.universe,
          box: null,
          view,
          index,
          label: ports.length > 1 ? `port ${part + 1} of ${name}` : name,
        }),
      );
    });
    return rows;
  });

  constructor() {
    // The PC's networks and serial ports, and a search for nodes, once the page is connected to the
    // desk program: a page loaded at Settings is not, at first. Again when the connection comes back.
    effect(() => {
      if (!this.store.online()) return;
      untracked(() => {
        void this.readNetworks();
        void this.readSerialPorts();
        void this.search();
      });
    });
    const timer = setInterval(() => {
      if (this.store.online()) void this.readSerialPorts();
    }, PORTS_EVERY_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));

    // Settings opened at #outputs (the shortcut of Outputs): the panel comes into sight, and once
    // more a moment later, when a page that has just loaded has its outputs and the table its rows.
    const host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    const injector = inject(Injector);
    inject(ActivatedRoute)
      .fragment.pipe(takeUntilDestroyed())
      .subscribe((fragment) => {
        if (fragment !== 'outputs') return;
        afterNextRender(() => bringToTop(host), { injector });
        setTimeout(() => bringToTop(host), SETTLE_MS);
      });
  }

  protected kindLabel(kind: OutputKind | 'dmxbox'): string {
    return KIND_LABELS[kind];
  }

  /** The universe a line plays now is offered even when the project has fewer (a list kept from before). */
  protected beyond(universe: number): boolean {
    return universe > this.store.universes();
  }

  protected part(row: Row, which: Part): number {
    return portAddressParts(row.view?.plan.ports[row.part].address ?? 0)[which];
  }

  protected partMax(which: Part): number {
    return PART_MAX[which];
  }

  protected address(row: Row): number {
    return row.view?.plan.ports[row.part].address ?? 0;
  }

  /** True for a broadcast or a multicast, which goes out on a network of the PC. */
  protected networked(row: Row): boolean {
    return row.view?.plan.target === 'broadcast' || row.view?.plan.target === 'multicast';
  }

  protected networkLabel(choice: NetworkChoice): string {
    return `${choice.address} (${choice.adapter})`;
  }

  /** A network chosen before that this PC is not in now: it stays chosen, and says so. */
  protected networkGone(row: Row): boolean {
    const network = row.view?.plan.network ?? '';
    return network !== '' && !this.networks().some((choice) => choice.network === network);
  }

  /** True on the last line of an Art-Net or sACN output that can have another port. */
  protected morePorts(row: Row): boolean {
    return (
      row.last &&
      (row.kind === 'artnet' || row.kind === 'sacn') &&
      (row.view?.plan.ports.length ?? 0) < MAX_OUTPUT_PORTS
    );
  }

  /** What a found node is to the project: a dmxbox, one of its outputs already, or one to add. */
  protected foundAs(node: FoundNode): 'dmxbox' | 'listed' | 'new' {
    if (node.dmxbox) return 'dmxbox';
    return this.store
      .outputs()
      .some((item) => item.plan.kind === 'artnet' && item.plan.target === node.ip)
      ? 'listed'
      : 'new';
  }

  protected portsOf(node: FoundNode): string {
    const count = Math.max(1, node.ports.length);
    return count === 1 ? '1 port' : `${count} ports`;
  }

  // ---------------------------------------------------------------- changes

  protected onUniverse(row: Row, event: Event): void {
    const universe = Number((event.target as HTMLSelectElement).value);
    if (row.box)
      void this.sendBoxes(withBoxOutput(this.store.boxes(), row.box.address, row.part, universe));
    else
      void this.send(
        withPlan(this.store.outputs(), row.index, (plan) => withPort(plan, row.part, { universe })),
      );
  }

  /** A Net, Sub-Net or Universe of a Port-Address typed; a number out of range is refused. */
  protected onPart(row: Row, which: Part, event: Event): void {
    const field = event.target as HTMLInputElement;
    const value = Number(field.value);
    if (!Number.isInteger(value) || value < 0 || value > PART_MAX[which]) {
      this.error.set(
        `The ${which === 'subNet' ? 'Sub-Net' : which === 'net' ? 'Net' : 'Universe'} is 0 to ${PART_MAX[which]}.`,
      );
      field.value = String(this.part(row, which));
      return;
    }
    const parts = { ...portAddressParts(this.address(row)), [which]: value };
    const address = portAddress(parts.net, parts.subNet, parts.universe);
    void this.send(
      withPlan(this.store.outputs(), row.index, (plan) => withPort(plan, row.part, { address })),
    );
  }

  /** The sACN universe of a port: 1 to 63999. */
  protected onSacnUniverse(row: Row, event: Event): void {
    const field = event.target as HTMLInputElement;
    const address = Number(field.value);
    if (!Number.isInteger(address) || address < 1 || address > MAX_SACN_UNIVERSE) {
      this.error.set(`An sACN universe is 1 to ${MAX_SACN_UNIVERSE}.`);
      field.value = String(this.address(row));
      return;
    }
    void this.send(
      withPlan(this.store.outputs(), row.index, (plan) => withPort(plan, row.part, { address })),
    );
  }

  /** The priority of sACN: 0 to 200. */
  protected onPriority(row: Row, event: Event): void {
    const field = event.target as HTMLInputElement;
    const priority = Number(field.value);
    if (!Number.isInteger(priority) || priority < 0 || priority > MAX_PRIORITY) {
      this.error.set(`The priority is 0 to ${MAX_PRIORITY}.`);
      field.value = String(row.view?.plan.priority ?? 100);
      return;
    }
    void this.send(withPlan(this.store.outputs(), row.index, (plan) => ({ ...plan, priority })));
  }

  protected onNetwork(row: Row, event: Event): void {
    const network = (event.target as HTMLSelectElement).value;
    void this.send(withPlan(this.store.outputs(), row.index, (plan) => ({ ...plan, network })));
  }

  protected morePort(row: Row): void {
    void this.send(
      withPlan(this.store.outputs(), row.index, (plan) =>
        withNewPort(plan, this.store.universes()),
      ),
    );
  }

  /** ✕: a box leaves the list; a port leaves its output, and an output of one port leaves the list. */
  protected remove(row: Row): void {
    if (row.box) {
      void this.sendBoxes(withoutBox(this.store.boxes(), row.box.address));
      return;
    }
    const ports = row.view?.plan.ports.length ?? 1;
    void this.send(
      ports > 1
        ? withPlan(this.store.outputs(), row.index, (plan) => withoutPort(plan, row.part))
        : withoutOutput(this.store.outputs(), row.index),
    );
  }

  /** A new box (it answers, but is not on the list) joins it. */
  protected addBox(box: BoxView): void {
    void this.sendBoxes(
      withBox(this.store.boxes(), box.address, box.outputs, this.store.universes()),
    );
  }

  /** Back to automatic: the project lists no boxes, and every box that answers plays by itself. */
  protected automatic(): void {
    void this.sendBoxes([]);
  }

  /** Asks the Art-Net nodes on the PC's networks to answer (ArtPoll). */
  protected async search(): Promise<void> {
    this.finding.set(true);
    try {
      this.found.set(await this.store.findArtNetNodes());
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : String(error));
    } finally {
      this.finding.set(false);
    }
  }

  protected addFound(node: FoundNode): void {
    const plan = planOfNode(node, this.store.boxes(), this.store.outputs(), this.store.universes());
    void this.send(withOutput(this.store.outputs(), plan));
  }

  protected onAddKind(event: Event): void {
    this.addKind.set((event.target as HTMLSelectElement).value as AddKind);
    this.addCount.set(1);
    this.error.set('');
  }

  protected onAddAddress(event: Event): void {
    this.addAddress.set((event.target as HTMLInputElement).value);
  }

  protected onAddCount(event: Event): void {
    this.addCount.set(Number((event.target as HTMLSelectElement).value));
  }

  protected onAddPort(event: Event): void {
    this.addPort.set((event.target as HTMLSelectElement).value);
  }

  /** "Add an output": what the kind asks for, checked here as far as can be; the desk program checks addresses. */
  protected async add(event: Event): Promise<void> {
    event.preventDefault();
    const kind = this.addKind();
    const address = this.addAddress().trim();
    const count = this.addCount();
    const plan = (target: string, of: OutputKind) =>
      newPlanWithPorts(
        of,
        target,
        this.store.boxes(),
        this.store.outputs(),
        this.store.universes(),
        count,
      );

    let sent: Promise<boolean>;
    if (kind === 'broadcast')
      sent = this.send(withOutput(this.store.outputs(), plan('broadcast', 'artnet')));
    else if (kind === 'multicast')
      sent = this.send(withOutput(this.store.outputs(), plan('multicast', 'sacn')));
    else if (kind === 'usb') {
      const free = this.freePorts();
      const port = free.includes(this.addPort()) ? this.addPort() : free[0];
      if (!port) {
        this.error.set(
          'No serial port is free. Plug the interface in: its port shows here a moment later.',
        );
        return;
      }
      sent = this.send(
        withOutput(
          this.store.outputs(),
          newPlanWithPorts(
            'usb',
            port,
            this.store.boxes(),
            this.store.outputs(),
            this.store.universes(),
            1,
          ),
        ),
      );
    } else if (address === '') {
      this.error.set(`Type the address, like ${this.addChoice().address}.`);
      return;
    } else if (kind === 'dmxbox') {
      sent = this.sendBoxes(withBox(this.store.boxes(), address, count, this.store.universes()));
    } else {
      sent = this.send(withOutput(this.store.outputs(), plan(address, kind)));
    }
    if (await sent) this.addAddress.set('');
  }

  // ---------------------------------------------------------------- the desk program

  // The new list goes to the desk program; it answers every page with the outputs. True when it took it.
  private async send(plans: OutputPlan[]): Promise<boolean> {
    return this.sending(() => this.store.setOutputs(plans));
  }

  private async sendBoxes(plans: BoxPlan[]): Promise<boolean> {
    return this.sending(() => this.store.setBoxes(plans));
  }

  private async sending(change: () => Promise<unknown>): Promise<boolean> {
    if (this.locked()) return false;
    this.busy.set(true);
    this.error.set('');
    try {
      await change();
      return true;
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : String(error));
      return false;
    } finally {
      this.busy.set(false);
    }
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

  // ---------------------------------------------------------------- names

  // The name of an output in the table and the line under it.
  private naming(view: OutputView, found: readonly FoundNode[]): { name: string; about: string } {
    const plan = view.plan;
    switch (plan.kind) {
      case 'artnet': {
        if (plan.target === 'broadcast')
          return { name: 'Broadcast', about: 'Art-Net to every node of a network' };
        const name = nodeName(view, found);
        return name
          ? { name, about: `${plan.target}, Art-Net` }
          : { name: plan.target, about: 'Art-Net' };
      }
      case 'sacn':
        return plan.target === 'multicast'
          ? { name: 'Multicast', about: 'source Lumenet' }
          : { name: plan.target, about: 'sACN, source Lumenet' };
      default:
        return { name: plan.target, about: 'DMX USB Pro' };
    }
  }

  // What the state says besides its word: why an output does not answer, or what answers. A node's
  // name stands in the table already, so its answer says only the rest.
  private detailOf(view: OutputView, name: string): string {
    const detail = view.detail ?? '';
    return detail.startsWith(`${name}, `)
      ? detail.slice(name.length + 2).replace(/^f/, 'F')
      : detail;
  }
}
