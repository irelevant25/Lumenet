import { Component, ElementRef, Injector, afterNextRender, computed, effect, inject, signal } from '@angular/core';
import { ACTION_GROUPS, ActionGroup, DESK_ACTIONS, DeskAction, actionWithId, shortcutName } from '../desk/desk-actions';
import { DeskSettings } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import { CHANNEL_COUNT, MEMORY_COUNT } from '../desk/desk-types';
import { Keyboard } from '../desk/keyboard';

/** The groups of keys that the list shows open at first: the small ones. */
const OPEN_AT_FIRST: ReadonlySet<ActionGroup> = new Set<ActionGroup>([
  'Views',
  'Project and LAN',
  'Master',
  'Stack',
  'Record',
  'Sequences view',
]);

/** Rows the list shows at most for a search, so that "1" does not put 600 rows on the page. */
const MOST_FOUND = 150;

/**
 * Keyboard: a shortcut for every key of the desk. The list has the keys at the left and their
 * shortcuts at the right; Set records one, and Listen finds a key by a click on it on the desk.
 * The shortcuts belong to this browser on this device, see desk-settings.ts; keyboard.ts does
 * the listening and the pressing.
 */
@Component({
  selector: 'lm-keyboard-view',
  styleUrl: './keyboard-view.css',
  templateUrl: './keyboard-view.html',
})
export class KeyboardView {
  protected readonly settings = inject(DeskSettings);
  protected readonly store = inject(DeskStore);
  protected readonly keyboard = inject(Keyboard);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly injector = inject(Injector);

  protected readonly search = signal('');
  private readonly openGroups = signal<ReadonlySet<ActionGroup>>(OPEN_AT_FIRST);

  /** The groups of the list, each with its rows; a group that is closed has none. */
  protected readonly groups = computed(() => {
    const words = this.search().trim().toLowerCase();
    const shortcuts = this.settings.shortcuts();
    const open = this.openGroups();
    let room = MOST_FOUND;
    return ACTION_GROUPS.map((group) => {
      const all = DESK_ACTIONS.filter((action) => action.group === group);
      const found = words === '' ? all : all.filter((action) => this.matches(action, words, shortcuts[action.id]));
      const shown = words !== '' || open.has(group);
      const rows = shown ? found.slice(0, Math.max(0, room)) : [];
      if (words !== '') room -= rows.length;
      return { group, count: found.length, open: shown, rows, cut: shown && rows.length < found.length };
    }).filter((entry) => words === '' || entry.count > 0);
  });

  /** How many keys have a shortcut. */
  protected readonly mapped = computed(() => Object.keys(this.settings.shortcuts()).length);

  constructor() {
    // A key picked by listening, or chosen to record: its group opens, and its row comes into
    // the middle of the list.
    effect(() => {
      const picked = this.keyboard.picked();
      const action = picked ? actionWithId(picked) : undefined;
      if (!action) return;

      this.openGroups.update((open) => (open.has(action.group) ? open : new Set([...open, action.group])));
      afterNextRender(
        () => {
          const row = this.host.querySelector<HTMLElement>(`[data-row="${CSS.escape(action.id)}"]`);
          if (!row) return;
          bringToMiddle(row);
          row.querySelector<HTMLButtonElement>('button.set')?.focus({ preventScroll: true });
        },
        { injector: this.injector },
      );
    });
  }

  protected toggleGroup(group: ActionGroup): void {
    this.openGroups.update((open) => {
      const next = new Set(open);
      if (next.has(group)) next.delete(group);
      else next.add(group);
      return next;
    });
  }

  protected onSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  /** What the list calls a key: with the channels or memories of its page, where it has a page. */
  protected label(action: DeskAction): string {
    const page = /^page\.(channel|memory)\.(\d+)$/.exec(action.id);
    if (!page) return action.label;

    const perPage = page[1] === 'channel' ? this.store.channelsPerPage() : this.store.memoriesPerPage();
    const count = page[1] === 'channel' ? CHANNEL_COUNT : MEMORY_COUNT;
    const first = (Number(page[2]) - 1) * perPage + 1;
    return first > count
      ? `${action.label} (not there with this channel size)`
      : `${action.label} (${first}-${first + perPage - 1})`;
  }

  protected shortcutOf(action: DeskAction): string {
    const keys = this.settings.shortcuts()[action.id];
    return keys ? shortcutName(keys) : '';
  }

  private matches(action: DeskAction, words: string, keys: string | undefined): boolean {
    return (
      this.label(action).toLowerCase().includes(words) ||
      (keys !== undefined && shortcutName(keys).toLowerCase().includes(words))
    );
  }
}

/**
 * Scrolls the box the element scrolls in, and only that one, so that the element stands in its
 * middle. (The browser's scrollIntoView moves every box around it, the page itself as well, and
 * the desk's page cannot be scrolled back by hand: the top bar would stay out of sight.)
 */
function bringToMiddle(element: HTMLElement): void {
  const box = scrollingBox(element);
  if (!box) return;

  const page = box === document.scrollingElement;
  const top = page ? 0 : box.getBoundingClientRect().top;
  const height = page ? window.innerHeight : box.clientHeight;
  const place = element.getBoundingClientRect();
  box.scrollTop += place.top - top - (height - place.height) / 2;
}

/**
 * The nearest box around the element that the owner can scroll: the list, or on a small screen
 * the page. Null when nothing can be scrolled by hand.
 */
function scrollingBox(element: HTMLElement): Element | null {
  for (let box = element.parentElement; box && box !== document.body; box = box.parentElement) {
    const overflow = getComputedStyle(box).overflowY;
    if ((overflow === 'auto' || overflow === 'scroll') && box.scrollHeight > box.clientHeight) return box;
  }
  const pageScrolls = getComputedStyle(document.body).overflowY !== 'hidden';
  return pageScrolls ? document.scrollingElement : null;
}
