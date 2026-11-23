import { BoxPlan, BoxView } from './desk-types';

/**
 * The boxes as the Boxes window works with them. The desk program sends them as views (which box
 * answers, and what each output plays); the window sends back the project's list of boxes as
 * plans. While the project lists none, every box that answers plays by itself ("automatic"); the
 * first change in the window makes the boxes that answer the project's list, with what they play
 * now, and the change on top.
 */

/** What the top bar says about the boxes: how many answer, of how many the project lists. */
export interface BoxesSummary {
  readonly connected: number;
  /** The boxes the project lists; 0 while it lists none (automatic). */
  readonly listed: number;
  /** Boxes of the list that do not answer. */
  readonly missing: number;
  /** "Boxes connected: 2", or "Boxes connected: 1 of 2" when the project lists boxes. */
  readonly text: string;
}

export function boxesSummary(boxes: readonly BoxView[]): BoxesSummary {
  const connected = boxes.filter((box) => box.connected).length;
  const listed = boxes.filter((box) => box.kind === 'listed');
  const missing = listed.filter((box) => !box.connected).length;
  const of = listed.length > 0 ? ` of ${listed.length}` : '';
  return { connected, listed: listed.length, missing, text: `Boxes connected: ${connected}${of}` };
}

/**
 * The project's list as it is: the boxes it lists, or, while it lists none, every box that answers
 * with what it plays now. New boxes (answering, not listed) are not in it.
 */
export function plansOf(boxes: readonly BoxView[]): BoxPlan[] {
  const automatic = !boxes.some((box) => box.kind === 'listed');
  return boxes
    .filter((box) => (automatic ? box.kind === 'automatic' : box.kind === 'listed'))
    .map((box) => ({ address: box.address, universes: [...box.universes] }));
}

/** The list with one output of a box playing another universe (0: none). A new box joins the list with it. */
export function withOutput(boxes: readonly BoxView[], address: string, output: number, universe: number): BoxPlan[] {
  const plans = plansOf(boxes);
  let plan = plans.find((item) => item.address === address);
  if (!plan) {
    const box = boxes.find((item) => item.address === address);
    if (!box) return plans;
    plan = { address, universes: new Array<number>(box.outputs).fill(0) };
    plans.push(plan);
  }
  plan.universes = plan.universes.map((old, index) => (index === output ? universe : old));
  return plans;
}

/**
 * The list with a box added: one that answers but is not on it, or one that is planned by hand
 * and does not have to answer. Its outputs get the universes of the project that no box of the
 * list plays yet, in their order; outputs beyond them play none.
 */
export function withBox(boxes: readonly BoxView[], address: string, outputs: number, universes: number): BoxPlan[] {
  const plans = plansOf(boxes).filter((plan) => plan.address !== address);
  const played = new Set(plans.flatMap((plan) => plan.universes));
  const free = Array.from({ length: universes }, (_, index) => index + 1).filter((universe) => !played.has(universe));
  return [...plans, { address, universes: Array.from({ length: outputs }, (_, index) => free[index] ?? 0) }];
}

/** The list without a box. */
export function withoutBox(boxes: readonly BoxView[], address: string): BoxPlan[] {
  return plansOf(boxes).filter((plan) => plan.address !== address);
}

/** The universes of the project that no output of a box that answers plays: their channels reach no cable. */
export function unplayedUniverses(boxes: readonly BoxView[], universes: number): number[] {
  const played = new Set(boxes.filter((box) => box.connected).flatMap((box) => box.universes));
  return Array.from({ length: universes }, (_, index) => index + 1).filter((universe) => !played.has(universe));
}

/** The universes that more than one output plays: the same cable twice, as a splitter would give it. */
export function sharedUniverses(boxes: readonly BoxView[]): number[] {
  const count = new Map<number, number>();
  for (const universe of boxes.filter((box) => box.kind !== 'new').flatMap((box) => box.universes)) {
    if (universe > 0) count.set(universe, (count.get(universe) ?? 0) + 1);
  }
  return [...count].filter(([, outputs]) => outputs > 1).map(([universe]) => universe).sort((a, b) => a - b);
}

/** The number on the switch of a box at 10.1.n.1; null for any other address. */
export function switchNumber(address: string): number | null {
  const match = /^10\.1\.(\d{1,3})\.1$/.exec(address);
  return match && Number(match[1]) <= 255 ? Number(match[1]) : null;
}
