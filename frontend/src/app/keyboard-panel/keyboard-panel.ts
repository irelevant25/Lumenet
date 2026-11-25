import {
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import {
  ACTION_GROUPS,
  ActionGroup,
  DESK_ACTIONS,
  DeskAction,
  actionWithId,
  shortcutName,
} from '../desk/desk-actions';
import { DeskSettings } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import { MEMORY_COUNT, UNIVERSE_SIZE } from '../desk/desk-types';
import { Keyboard } from '../desk/keyboard';
import { bringToMiddle } from '../desk/scroll';
import { channelLabel } from '../desk/universes';

/** One kind of keys: how many there are, how many have a shortcut, and how many the search finds. */
interface Kind {
  readonly group: ActionGroup;
  readonly total: number;
  readonly bound: number;
  readonly found: number;
}

/**
 * The keyboard shortcuts, a panel of Settings: a shortcut for every key of the desk. The kinds of
 * keys stand one under the other, each with the number of its keys, how many of them have a
 * shortcut and, while a search runs, how many it finds ("Stack 6 (4) 2"). A press on a kind opens
 * its keys, at the left, with their shortcuts at the right, and closes any other: one is open at a
 * time, none at first. A search opens the first kind where it finds keys, unless the one that is
 * open has some. Set records a shortcut, and Listen finds a key by a double click on it on the
 * desk. The shortcuts belong to this device, see desk-settings.ts; keyboard.ts does the listening
 * and the pressing.
 */
@Component({
  selector: 'lm-keyboard-panel',
  styleUrl: './keyboard-panel.css',
  templateUrl: './keyboard-panel.html',
})
export class KeyboardPanel {
  protected readonly settings = inject(DeskSettings);
  protected readonly store = inject(DeskStore);
  protected readonly keyboard = inject(Keyboard);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly injector = inject(Injector);

  protected readonly search = signal('');
  /** The kind of keys that is open; null while all are closed. */
  protected readonly openGroup = signal<ActionGroup | null>(null);
  // The kind that was open when a search began: open again when it ends.
  private openBeforeSearch: ActionGroup | null = null;

  /** True while a search runs. */
  protected readonly searching = computed(() => this.search().trim() !== '');

  /**
   * The keys this project has: the channel keys of its universes, and a U key for each of them
   * (none for a project of one). The shortcuts of the others are kept for a project that has them.
   */
  private readonly actions = computed(() => {
    const channels = this.store.channelCount();
    const universes = this.store.universes();
    return DESK_ACTIONS.filter((action) => {
      const channel = /^channel\.(\d+)$/.exec(action.id);
      if (channel) return Number(channel[1]) <= channels;
      const universe = /^universe\.(\d+)$/.exec(action.id);
      return !universe || (universes > 1 && Number(universe[1]) <= universes);
    });
  });

  // For each kind of keys: its keys that the search finds (all of them without a search).
  private readonly found = computed(() => {
    const words = this.search().trim().toLowerCase();
    const shortcuts = this.settings.shortcuts();
    const actions = this.actions();
    return new Map(
      ACTION_GROUPS.map((group) => {
        const all = actions.filter((action) => action.group === group);
        return [
          group,
          words === ''
            ? all
            : all.filter((action) => this.matches(action, words, shortcuts[action.id])),
        ];
      }),
    );
  });

  /** The kinds of keys, one under the other. */
  protected readonly kinds = computed<readonly Kind[]>(() => {
    const shortcuts = this.settings.shortcuts();
    return ACTION_GROUPS.map((group) => {
      const keys = this.actions().filter((action) => action.group === group);
      return {
        group,
        total: keys.length,
        bound: keys.filter((action) => !!shortcuts[action.id]).length,
        found: this.found().get(group)?.length ?? 0,
      };
    });
  });

  /** The keys of the kind that is open, as the search finds them. */
  protected readonly rows = computed(() => {
    const open = this.openGroup();
    return open ? (this.found().get(open) ?? []) : [];
  });

  /** How many keys have a shortcut. (An empty one is a usual shortcut that was taken away.) */
  protected readonly mapped = computed(
    () => Object.values(this.settings.shortcuts()).filter((keys) => keys !== '').length,
  );

  constructor() {
    // A key picked by listening, or chosen to record: its kind opens, and its row comes into the
    // middle of the list, with the panel in sight.
    effect(() => {
      const picked = this.keyboard.picked();
      const action = picked ? actionWithId(picked) : undefined;
      if (!action) return;

      this.openGroup.set(action.group);
      this.search.set('');
      afterNextRender(
        () => {
          const row = this.host.querySelector<HTMLElement>(`[data-row="${CSS.escape(action.id)}"]`);
          if (!row) return;
          bringToMiddle(this.host);
          bringToMiddle(row);
          row.querySelector<HTMLButtonElement>('button.set')?.focus({ preventScroll: true });
        },
        { injector: this.injector },
      );
    });
  }

  /** A press on a kind opens it and closes the others; on the kind that is open, closes it. */
  protected toggle(group: ActionGroup): void {
    this.openGroup.update((open) => (open === group ? null : group));
  }

  /** The arrow keys, Home and End go from kind to kind, as in every accordion. */
  protected onHeadKey(event: KeyboardEvent, index: number): void {
    const count = ACTION_GROUPS.length;
    const to =
      event.key === 'ArrowDown'
        ? (index + 1) % count
        : event.key === 'ArrowUp'
          ? (index - 1 + count) % count
          : event.key === 'Home'
            ? 0
            : event.key === 'End'
              ? count - 1
              : -1;
    if (to < 0) return;
    event.preventDefault();
    this.host.querySelector<HTMLElement>(`#kind-${to}`)?.focus();
  }

  /**
   * The search: the kind that is open stays open while it has keys the search finds; otherwise
   * the first kind that has some opens. When the search is emptied, the kind open before it opens
   * again.
   */
  protected onSearch(event: Event): void {
    const text = (event.target as HTMLInputElement).value;
    if (!this.searching() && text.trim() !== '') this.openBeforeSearch = this.openGroup();
    this.search.set(text);

    if (!this.searching()) {
      this.openGroup.set(this.openBeforeSearch);
      return;
    }
    const open = this.openGroup();
    if (open === null || (this.found().get(open)?.length ?? 0) === 0) {
      this.openGroup.set(this.kinds().find((kind) => kind.found > 0)?.group ?? null);
    }
  }

  /** What a screen reader says for a kind: "Stack: 6 keys, 4 with a shortcut, 2 found". */
  protected kindName(kind: Kind): string {
    const keys = `${kind.total} ${kind.total === 1 ? 'key' : 'keys'}, ${kind.bound} with a shortcut`;
    return `${kind.group}: ${keys}${this.searching() ? `, ${kind.found} found` : ''}`;
  }

  /**
   * What the list calls a key: a channel key with its universe, "113 (U2)"; a page
   * key with the channels or memories of its page (channels in the universe shown).
   */
  protected label(action: DeskAction): string {
    const channel = /^channel\.(\d+)$/.exec(action.id);
    if (channel) return `Channel key ${channelLabel(Number(channel[1]))}`;
    const page = /^page\.(channel|memory)\.(\d+)$/.exec(action.id);
    if (!page) return action.label;

    const perPage =
      page[1] === 'channel' ? this.store.channelsPerPage() : this.store.memoriesPerPage();
    const count = page[1] === 'channel' ? UNIVERSE_SIZE : MEMORY_COUNT;
    const first = (Number(page[2]) - 1) * perPage + 1;
    const where =
      page[1] === 'channel' && this.store.universes() > 1 ? ' of the universe shown' : '';
    return first > count
      ? `${action.label} (not there with this channel size)`
      : `${action.label} (${first}-${first + perPage - 1}${where})`;
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
