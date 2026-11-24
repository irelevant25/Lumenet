import {
  BoxView,
  DEFAULT_PRIORITY,
  FoundNode,
  OutputKind,
  OutputPlan,
  OutputPort,
  OutputView,
} from './desk-types';

/**
 * The outputs as the Outputs window and the top bar work with them: the dmxboxes (see boxes.ts)
 * and the outputs besides them, Art-Net nodes, sACN and DMX USB interfaces. The desk program
 * sends the outputs as views (each with its state); the window sends back the project's whole
 * list as plans.
 */

/** What the top bar says about the outputs: how many play, of how many the project has. */
export interface OutputsSummary {
  /** Boxes and outputs that answer, or send where nothing can answer. */
  readonly working: number;
  /** Of the project's boxes and outputs, those that do not answer or are refused. */
  readonly missing: number;
  /** "Outputs connected: 2", or "Outputs connected: 2 of 3" when the project lists boxes or has outputs. */
  readonly text: string;
}

/**
 * The count of the top bar. A box counts while it plays (a new box, which answers but is not on
 * the project's list, does not); an output counts while it answers, or sends where nothing can
 * answer. "of" the boxes of the list and the outputs, once the project has either.
 */
export function outputsSummary(
  boxes: readonly BoxView[],
  outputs: readonly OutputView[],
): OutputsSummary {
  const playing = boxes.filter((box) => box.kind !== 'new');
  const listed = boxes.filter((box) => box.kind === 'listed');
  const boxesWorking = playing.filter((box) => box.connected).length;
  const outputsWorking = outputs.filter(isWorking).length;
  const working = boxesWorking + outputsWorking;
  const missing = listed.filter((box) => !box.connected).length + outputs.length - outputsWorking;
  const total = (listed.length > 0 ? listed.length : boxesWorking) + outputs.length;
  const of = listed.length > 0 || outputs.length > 0 ? ` of ${total}` : '';
  return { working, missing, text: `Outputs connected: ${working}${of}` };
}

/** True while an output answers, or sends where nothing can answer. */
export function isWorking(output: OutputView): boolean {
  return output.state === 'connected' || output.state === 'sending';
}

/** The project's outputs besides the boxes, as SetOutputs takes them: copies, to change. */
export function plansOf(outputs: readonly OutputView[]): OutputPlan[] {
  return outputs.map((output) => ({
    ...output.plan,
    ports: output.plan.ports.map((port) => ({ ...port })),
  }));
}

/** The list with one more output at its end. */
export function withOutput(outputs: readonly OutputView[], plan: OutputPlan): OutputPlan[] {
  return [...plansOf(outputs), plan];
}

/** The list without the output at an index. */
export function withoutOutput(outputs: readonly OutputView[], index: number): OutputPlan[] {
  return plansOf(outputs).filter((_, other) => other !== index);
}

/** The list with the output at an index changed. */
export function withPlan(
  outputs: readonly OutputView[],
  index: number,
  change: (plan: OutputPlan) => OutputPlan,
): OutputPlan[] {
  return plansOf(outputs).map((plan, other) => (other === index ? change(plan) : plan));
}

/** The plan with one port changed. */
export function withPort(plan: OutputPlan, port: number, change: Partial<OutputPort>): OutputPlan {
  return {
    ...plan,
    ports: plan.ports.map((item, other) => (other === port ? { ...item, ...change } : item)),
  };
}

/**
 * A new output, with a port for each universe of the project that no box or output plays yet, or
 * for U1 when they all play: Art-Net from Port-Address 0:0:0 on (U1 at 0:0:0 when it is free,
 * as most nodes come), sACN universe 1 for U1. A USB interface has one port.
 */
export function newPlan(
  kind: OutputKind,
  target: string,
  boxes: readonly BoxView[],
  outputs: readonly OutputView[],
  universes: number,
): OutputPlan {
  const free = freeUniverses(boxes, outputs, universes);
  const playing = free.length > 0 ? free : [1];
  const ports: OutputPort[] = (kind === 'usb' ? playing.slice(0, 1) : playing).map((universe) => ({
    universe,
    address: kind === 'artnet' ? universe - 1 : kind === 'sacn' ? universe : 0,
  }));
  return { kind, target, ports, network: '', priority: DEFAULT_PRIORITY };
}

/**
 * A found Art-Net node as a new output: a port for each of its ports, at its Port-Address, playing
 * the universes no box or output plays yet, in their order (U1 on the first when they all play,
 * as for a new output); the others play none. A node that says no ports gets one at 0:0:0.
 */
export function planOfNode(
  node: FoundNode,
  boxes: readonly BoxView[],
  outputs: readonly OutputView[],
  universes: number,
): OutputPlan {
  const free = freeUniverses(boxes, outputs, universes);
  const playing = free.length > 0 ? free : [1];
  const addresses = node.ports.length > 0 ? node.ports : [0];
  const ports = addresses.map((address, index) => ({ universe: playing[index] ?? 0, address }));
  return { kind: 'artnet', target: node.ip, ports, network: '', priority: DEFAULT_PRIORITY };
}

/** The plan with one more port: the next universe of the project, at the next address. */
export function withNewPort(plan: OutputPlan, universes: number): OutputPlan {
  const last = plan.ports[plan.ports.length - 1];
  const universe = last ? (last.universe % universes) + 1 : 1;
  const used = new Set(plan.ports.map((port) => port.address));
  let address = last ? last.address + 1 : plan.kind === 'sacn' ? 1 : 0;
  while (used.has(address)) address++;
  return { ...plan, ports: [...plan.ports, { universe, address }] };
}

/** The plan without one port. */
export function withoutPort(plan: OutputPlan, port: number): OutputPlan {
  return { ...plan, ports: plan.ports.filter((_, other) => other !== port) };
}

/** The Net (0 to 127), Sub-Net (0 to 15) and Universe (0 to 15) of an Art-Net Port-Address. */
export function portAddressParts(address: number): {
  net: number;
  subNet: number;
  universe: number;
} {
  return { net: address >> 8, subNet: (address >> 4) & 0x0f, universe: address & 0x0f };
}

/** A Port-Address out of its Net, Sub-Net and Universe. */
export function portAddress(net: number, subNet: number, universe: number): number {
  return (net << 8) | (subNet << 4) | universe;
}

/** A Port-Address as nodes show it: "0:1:1". */
export function portAddressText(address: number): string {
  const { net, subNet, universe } = portAddressParts(address);
  return `${net}:${subNet}:${universe}`;
}

/** What the window and the top bar call an output: "Art-Net 192.168.1.50", "sACN multicast", "USB COM3". */
export function outputName(plan: OutputPlan): string {
  const kind = plan.kind === 'artnet' ? 'Art-Net' : plan.kind === 'sacn' ? 'sACN' : 'USB';
  return `${kind} ${plan.target}`;
}

/** Where a port goes, as people read it: "0:0:1" for Art-Net, "1" for sACN, nothing for USB. */
export function addressText(plan: OutputPlan, port: OutputPort): string {
  return plan.kind === 'artnet'
    ? portAddressText(port.address)
    : plan.kind === 'sacn'
      ? String(port.address)
      : '';
}

/** What an output plays, for the pointer: "U1 at 0:0:0, U2 at 0:0:1", "U1". */
export function playsText(plan: OutputPlan): string {
  return plan.ports
    .map((port) => {
      if (port.universe === 0) return 'off';
      const at = addressText(plan, port);
      return at === '' ? `U${port.universe}` : `U${port.universe} at ${at}`;
    })
    .join(', ');
}

/** The state of an output in words: "Connected", "Not answering", "Sending", "A dmxbox". */
export function stateText(output: OutputView): string {
  switch (output.state) {
    case 'connected':
      return 'Connected';
    case 'sending':
      return 'Sending';
    case 'conflict':
      return 'A dmxbox';
    default:
      return 'Not answering';
  }
}

/**
 * The universes of the project that reach no cable: no box that answers plays them, and no output
 * that answers or sends.
 */
export function unplayedUniverses(
  boxes: readonly BoxView[],
  outputs: readonly OutputView[],
  universes: number,
): number[] {
  const played = new Set([
    ...boxes.filter((box) => box.connected && box.kind !== 'new').flatMap((box) => box.universes),
    ...outputs
      .filter(isWorking)
      .flatMap((output) => output.plan.ports.map((port) => port.universe)),
  ]);
  return Array.from({ length: universes }, (_, index) => index + 1).filter(
    (universe) => !played.has(universe),
  );
}

/** The universes that more than one output plays, boxes and others together: as a splitter would give them. */
export function sharedUniverses(
  boxes: readonly BoxView[],
  outputs: readonly OutputView[],
): number[] {
  const count = new Map<number, number>();
  const played = [
    ...boxes.filter((box) => box.kind !== 'new').flatMap((box) => box.universes),
    ...outputs.flatMap((output) => output.plan.ports.map((port) => port.universe)),
  ];
  for (const universe of played) {
    if (universe > 0) count.set(universe, (count.get(universe) ?? 0) + 1);
  }
  return [...count]
    .filter(([, times]) => times > 1)
    .map(([universe]) => universe)
    .sort((a, b) => a - b);
}

// The universes of the project that no box of the list (or, automatic, no box) and no output plays.
function freeUniverses(
  boxes: readonly BoxView[],
  outputs: readonly OutputView[],
  universes: number,
): number[] {
  const played = new Set([
    ...boxes.filter((box) => box.kind !== 'new').flatMap((box) => box.universes),
    ...outputs.flatMap((output) => output.plan.ports.map((port) => port.universe)),
  ]);
  return Array.from({ length: universes }, (_, index) => index + 1).filter(
    (universe) => !played.has(universe),
  );
}
