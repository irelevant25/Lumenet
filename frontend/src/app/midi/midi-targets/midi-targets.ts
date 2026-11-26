import {
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { DESK_ACTIONS } from '../../desk/desk-actions';
import { DeskMidi } from '../desk-midi';
import { DeskStore } from '../../desk/desk-store';
import { Keyboard } from '../../desk/keyboard';
import type { MidiMapping, MidiMode } from '../midi';
import {
  FADER_TARGETS,
  MIDI_MODES,
  SLOT_KEYS,
  TARGET_GROUPS,
  controlText,
  isFader,
  targetGroup,
  targetLabel,
} from '../midi-targets';
import { bringToMiddle } from '../../shared/scroll';

/** A key or fader a control can work, as the list shows it. */
interface Target {
  readonly id: string;
  readonly group: string;
  readonly label: string;
}

/** One kind of targets: how many there are, how many have a control, and how many the search finds. */
interface Kind {
  readonly group: string;
  readonly total: number;
  readonly mapped: number;
  readonly found: number;
}

/**
 * The keys and faders of the desk in the MIDI view, kind by kind, as the Keyboard panel lists the
 * keys: one kind open at a time, with its targets, the controls that work each, Learn and ✕. Learn
 * waits for the next control that moves (pressed again, it stops). Listen picks a key or a fader on
 * the desk itself, as Listen of the Keyboard panel picks a key: it is shown here, marked and with
 * the focus on its Learn, and nothing more. Under a fader that has a control stand its options: how
 * a controller moves it (a fader, or an endless encoder of one of three kinds), whether it takes
 * over only at the desk's value, and whether the desk sends its value back (a motor, a ring of
 * lights); under a key's control, whether the desk lights it.
 */
@Component({
  selector: 'lm-midi-targets',
  styleUrl: './midi-targets.css',
  templateUrl: './midi-targets.html',
})
export class MidiTargets {
  protected readonly midi = inject(DeskMidi);
  protected readonly keyboard = inject(Keyboard);
  private readonly store = inject(DeskStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly injector = inject(Injector);

  protected readonly modes = MIDI_MODES;
  protected readonly isFader = isFader;

  protected readonly search = signal('');
  /** The kind that is open; null while all are closed. */
  protected readonly openGroup = signal<string | null>(null);
  private openBeforeSearch: string | null = null;
  protected readonly searching = computed(() => this.search().trim() !== '');

  protected readonly learning = computed(() => this.midi.state()?.learning ?? null);
  private readonly mappings = computed(() => this.midi.state()?.mappings ?? []);
  private readonly byTarget = computed(() => {
    const byTarget = new Map<string, MidiMapping[]>();
    for (const mapping of this.mappings())
      byTarget.set(mapping.target, [...(byTarget.get(mapping.target) ?? []), mapping]);
    return byTarget;
  });

  /** The targets this project has: the channel keys and faders of its universes, a U key for each of several. */
  private readonly targets = computed<readonly Target[]>(() => {
    const channels = this.store.channelCount();
    const universes = this.store.universes();
    const channelOf = (id: string): number | null => {
      const channel = /^(?:fader\.)?channel\.(\d+)$/.exec(id);
      return channel ? Number(channel[1]) : null;
    };
    const keys = DESK_ACTIONS.filter((action) => {
      const universe = /^universe\.(\d+)$/.exec(action.id);
      if (universe) return universes > 1 && Number(universe[1]) <= universes;
      return (channelOf(action.id) ?? 0) <= channels;
    });
    const faders = FADER_TARGETS.filter((fader) => (channelOf(fader.id) ?? 0) <= channels);
    // every target called as targetLabel calls it: a channel with its universe, "Channel fader 113 (U2)"
    const listed = (target: { id: string; group: string }): Target => ({
      id: target.id,
      group: target.group,
      label: targetLabel(target.id),
    });
    return [...keys.map(listed), ...SLOT_KEYS, ...faders.map(listed)];
  });

  // Each kind's targets: all of them, and those the search finds.
  private readonly all = computed(() => this.byGroup(this.targets()));
  private readonly found = computed(() => {
    const words = this.search().trim().toLowerCase();
    return words === ''
      ? this.all()
      : this.byGroup(this.targets().filter((target) => this.matches(target, words)));
  });

  /** The kinds, one under the other. */
  protected readonly kinds = computed<readonly Kind[]>(() => {
    const mapped = this.byTarget();
    return TARGET_GROUPS.map((group) => {
      const targets = this.all().get(group) ?? [];
      return {
        group,
        total: targets.length,
        mapped: targets.filter((target) => mapped.has(target.id)).length,
        found: this.found().get(group)?.length ?? 0,
      };
    });
  });

  /** The targets of the kind that is open, as the search finds them. */
  protected readonly rows = computed(() => {
    const open = this.openGroup();
    return open ? (this.found().get(open) ?? []) : [];
  });

  constructor() {
    // A key or fader picked on the desk by Listen: its kind opens, and its row (marked, see the
    // template) comes into the middle of the list with the focus on its Learn. Learn waits for a
    // press of it, as Set does in the Keyboard panel.
    effect(() => {
      const picked = this.keyboard.midiPicked();
      if (!picked) return;
      untracked(() => {
        this.search.set('');
        this.openGroup.set(targetGroup(picked) ?? null);
        afterNextRender(
          () => {
            const row = this.host.querySelector<HTMLElement>(`[data-row="${CSS.escape(picked)}"]`);
            if (!row) return;
            bringToMiddle(row);
            row.querySelector<HTMLButtonElement>('button.learn')?.focus({ preventScroll: true });
          },
          { injector: this.injector },
        );
      });
    });
  }

  /** A press on a kind opens it and closes the others; on the kind that is open, closes it. */
  protected toggle(group: string): void {
    this.openGroup.update((open) => (open === group ? null : group));
  }

  /** The arrow keys, Home and End go from kind to kind, as in every accordion. */
  protected onHeadKey(event: KeyboardEvent, index: number): void {
    const count = TARGET_GROUPS.length;
    const to = {
      ArrowDown: (index + 1) % count,
      ArrowUp: (index - 1 + count) % count,
      Home: 0,
      End: count - 1,
    }[event.key];
    if (to === undefined) return;
    event.preventDefault();
    this.host.querySelector<HTMLElement>(`#midi-kind-${to}`)?.focus();
  }

  /** The search, as in the Keyboard panel: the kind open stays open while it has targets it finds, else the first that has some opens. */
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

  /** What a screen reader says for a kind: "Stack: 9 targets, 2 with a control". */
  protected kindName(kind: Kind): string {
    const targets = `${kind.total} ${kind.total === 1 ? 'target' : 'targets'}, ${kind.mapped} with a control`;
    return `${kind.group}: ${targets}${this.searching() ? `, ${kind.found} found` : ''}`;
  }

  protected mappingsOf(id: string): readonly MidiMapping[] {
    return this.byTarget().get(id) ?? [];
  }

  /** A control as the list says it: "APC mini mk2: Note 56, channel 1". */
  protected controlOf(mapping: MidiMapping): string {
    return `${mapping.device}: ${controlText(mapping)}`;
  }

  /** Learn for this target; pressed again while it waits, it stops. */
  protected learn(id: string): void {
    void this.midi.learn(this.learning() === id ? null : id);
  }

  /** Takes every control of this target away. */
  protected remove(id: string): void {
    void this.midi.setMappings(this.mappings().filter((mapping) => mapping.target !== id));
  }

  protected setMode(mapping: MidiMapping, event: Event): void {
    this.change(mapping, { mode: (event.target as HTMLSelectElement).value as MidiMode });
  }

  protected setPickup(mapping: MidiMapping, event: Event): void {
    this.change(mapping, { pickup: (event.target as HTMLInputElement).checked });
  }

  protected setFeedback(mapping: MidiMapping, event: Event): void {
    this.change(mapping, { feedback: (event.target as HTMLInputElement).checked });
  }

  // One mapping changed, the others as they are.
  private change(mapping: MidiMapping, change: Partial<MidiMapping>): void {
    void this.midi.setMappings(
      this.mappings().map((found) => (found === mapping ? { ...found, ...change } : found)),
    );
  }

  private byGroup(targets: readonly Target[]): Map<string, Target[]> {
    const groups = new Map<string, Target[]>(TARGET_GROUPS.map((group) => [group, []]));
    for (const target of targets) groups.get(target.group)?.push(target);
    return groups;
  }

  private matches(target: Target, words: string): boolean {
    return (
      target.label.toLowerCase().includes(words) ||
      this.mappingsOf(target.id).some((mapping) =>
        this.controlOf(mapping).toLowerCase().includes(words),
      )
    );
  }
}
