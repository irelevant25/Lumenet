import { Component, ElementRef, Injector, afterNextRender, computed, effect, inject, signal } from '@angular/core';
import { ACTION_GROUPS, ActionGroup, DESK_ACTIONS, DeskAction, actionWithId, shortcutName } from '../desk/desk-actions';
import { DeskSettings } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import { CHANNEL_COUNT, MEMORY_COUNT } from '../desk/desk-types';
import { Keyboard } from '../desk/keyboard';

/** One tab of the list: a kind of keys, how many there are, and how many the search finds. */
interface Tab {
  readonly group: ActionGroup;
  readonly total: number;
  readonly found: number;
}

/**
 * Keyboard: a shortcut for every key of the desk. The kinds of keys are tabs side by side, each
 * with the number of its keys (and, while a search runs, how many of them it finds); the list
 * under them has the keys of one tab at the left and their shortcuts at the right. Set records
 * one, and Listen finds a key by a double click on it on the desk. On the PC the shortcuts belong to
 * the project, see desk-settings.ts; keyboard.ts does the listening and the pressing.
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
  /** The tab chosen last. */
  private readonly chosenGroup = signal<ActionGroup>(ACTION_GROUPS[0]);

  /** True while a search runs. */
  protected readonly searching = computed(() => this.search().trim() !== '');

  // For each kind of keys: its keys that the search finds (all of them without a search).
  private readonly found = computed(() => {
    const words = this.search().trim().toLowerCase();
    const shortcuts = this.settings.shortcuts();
    return new Map(
      ACTION_GROUPS.map((group) => {
        const all = DESK_ACTIONS.filter((action) => action.group === group);
        return [group, words === '' ? all : all.filter((action) => this.matches(action, words, shortcuts[action.id]))];
      }),
    );
  });

  /** The tabs, one for each kind of keys. */
  protected readonly tabs = computed<readonly Tab[]>(() =>
    ACTION_GROUPS.map((group) => ({
      group,
      total: DESK_ACTIONS.filter((action) => action.group === group).length,
      found: this.found().get(group)?.length ?? 0,
    })),
  );

  /**
   * The tab that is shown: the one chosen, unless a search finds nothing there; then the first
   * tab where it finds something.
   */
  protected readonly shownGroup = computed<ActionGroup>(() => {
    const chosen = this.chosenGroup();
    if (!this.searching() || (this.found().get(chosen)?.length ?? 0) > 0) return chosen;
    return this.tabs().find((tab) => tab.found > 0)?.group ?? chosen;
  });

  /** The keys of the tab that is shown, as the search finds them. */
  protected readonly rows = computed(() => this.found().get(this.shownGroup()) ?? []);

  /** How many keys have a shortcut. (An empty one is a usual shortcut that was taken away.) */
  protected readonly mapped = computed(
    () => Object.values(this.settings.shortcuts()).filter((keys) => keys !== '').length,
  );

  constructor() {
    // A key picked by listening, or chosen to record: its tab is shown, and its row comes into
    // the middle of the list.
    effect(() => {
      const picked = this.keyboard.picked();
      const action = picked ? actionWithId(picked) : undefined;
      if (!action) return;

      this.chosenGroup.set(action.group);
      this.search.set('');
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

  protected choose(group: ActionGroup): void {
    this.chosenGroup.set(group);
  }

  /** The arrow keys go from tab to tab, as in every row of tabs. */
  protected onTabKey(event: KeyboardEvent): void {
    const step = event.key === 'ArrowRight' ? 1 : event.key === 'ArrowLeft' ? -1 : 0;
    if (step === 0) return;
    event.preventDefault();
    const tabs = ACTION_GROUPS;
    const next = tabs[(tabs.indexOf(this.shownGroup()) + step + tabs.length) % tabs.length];
    this.chosenGroup.set(next);
    afterNextRender(() => this.host.querySelector<HTMLElement>('[role="tab"][aria-selected="true"]')?.focus(), {
      injector: this.injector,
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
