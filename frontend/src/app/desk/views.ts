/**
 * The views of the desk, in the usual order of their links in the top bar: the name of each (as in
 * its keyboard action, "view.main"), what its link says, and where it goes. Settings can put them
 * in another order, and leave any out of the top bar but Settings itself, where they come back.
 */
export const VIEWS = [
  { name: 'main', label: 'Main', url: '/' },
  { name: 'memories', label: 'Memories', url: '/memories' },
  { name: 'sequences', label: 'Sequences', url: '/sequences' },
  { name: 'stack', label: 'Stack', url: '/stack' },
  { name: 'stage', label: 'Stage', url: '/stage' },
  { name: 'fixtures', label: 'Fixtures', url: '/fixtures' },
  { name: 'effects', label: 'Effects', url: '/effects' },
  { name: 'notes', label: 'Notes', url: '/notes' },
  { name: 'midi', label: 'MIDI', url: '/midi' },
  { name: 'settings', label: 'Settings', url: '/settings' },
] as const;
// (Keyboard was a view after Settings until 2026-11-24; its shortcuts are a panel of Settings now.
// A kept order or a kept list of hidden views that names it is read without it. MIDI came on
// 2026-11-25: a kept order gets it after Notes.)

/**
 * The usual order of the views while Effects stood after Sequences (2026-11-24, for a day): a kept
 * order that is just this was never chosen, and is read as the usual order of now.
 */
const ORDER_WITH_EFFECTS_AFTER_SEQUENCES: readonly string[] = [
  'main',
  'memories',
  'sequences',
  'effects',
  'stack',
  'stage',
  'fixtures',
  'notes',
  'settings',
];

export type ViewName = (typeof VIEWS)[number]['name'];

/** The view that is always in the top bar: Settings, where the others are shown again. */
export const ALWAYS_SHOWN: ViewName = 'settings';

/** The usual order of the views in the top bar. */
export const DEFAULT_VIEW_ORDER: readonly ViewName[] = VIEWS.map((view) => view.name);

/** The views that can be left out of the top bar, as a list of names: known ones, each once, never Settings. */
export function hiddenViewsOf(value: unknown): ViewName[] {
  if (!Array.isArray(value)) return [];
  return DEFAULT_VIEW_ORDER.filter((name) => name !== ALWAYS_SHOWN && value.includes(name));
}

/**
 * An order of the views: the known ones of the list as they come, each once. A view the list lacks,
 * like one that came after the list was kept, goes right after the view it follows in the usual
 * order (the first view goes first). The usual order of before is the usual order of now.
 */
export function viewOrderOf(value: unknown): ViewName[] {
  if (Array.isArray(value) && value.join() === ORDER_WITH_EFFECTS_AFTER_SEQUENCES.join()) return [...DEFAULT_VIEW_ORDER];
  const listed = Array.isArray(value) ? value.filter((name): name is ViewName => DEFAULT_VIEW_ORDER.includes(name)) : [];
  const order = [...new Set(listed)];
  DEFAULT_VIEW_ORDER.forEach((name, at) => {
    if (order.includes(name)) return;
    const after = at === 0 ? -1 : order.indexOf(DEFAULT_VIEW_ORDER[at - 1]);
    order.splice(after + 1, 0, name);
  });
  return order;
}

/** The view with this name. */
export function viewNamed(name: ViewName): (typeof VIEWS)[number] {
  return VIEWS.find((view) => view.name === name) ?? VIEWS[0];
}
