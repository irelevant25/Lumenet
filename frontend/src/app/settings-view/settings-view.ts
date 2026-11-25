import { Component, ElementRef, Injector, afterNextRender, computed, inject, signal } from '@angular/core';
import {
  CHANNEL_SIZES,
  ChannelSize,
  DeskSettings,
  ROW_COUNTS,
  RowCount,
  WHEEL_SIZES,
  WheelSize,
  ZOOM_MAX,
  ZOOM_MIN,
} from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import { EFFECT_INDEPENDENT_COUNT } from '../desk/effects';
import {
  CHANNEL_INDEPENDENT_COUNT,
  MAX_UNIVERSES,
  MEMORY_INDEPENDENT_COUNT,
  UNIVERSE_SIZE,
  lanAddressInUse,
} from '../desk/desk-types';
import { ALWAYS_SHOWN, DEFAULT_VIEW_ORDER, ViewName, viewNamed } from '../desk/views';
import { KeyColorsPanel } from '../key-colors-panel/key-colors-panel';
import { KeyboardPanel } from '../keyboard-panel/keyboard-panel';
import { OutputsPanel } from '../outputs-panel/outputs-panel';

/** How often the autosave can keep the work, in seconds: as the desk program offers it. */
const AUTOSAVE_INTERVALS: readonly number[] = [5, 30, 60, 300];

/**
 * Settings, in four panels side by side:
 *
 * - Main view: how many channels a page holds (chosen by the window, or fixed), in one or two
 *   rows of faders, and one or two rows of memory faders; rows wider than the window scroll
 *   sideways. How many steps the stack wheel shows, and how many keys of each kind the
 *   independents show (0 to 32; none of either: no independents at all). Under them the
 *   universes of the project (1 to 8; the project keeps the number), the zoom of the page (50 to
 *   200 per cent), the autosave (on or off, and how often) and, while LAN is on, the address of
 *   this PC that phones use and the password; the autosave and LAN on the PC only.
 * - Which views have a link in the top bar, and in which order: all but Settings can be left out.
 * - The colors of the keys (key-colors-panel), one under the other in a list that scrolls.
 * - The keyboard shortcuts (keyboard-panel), the kinds of keys one under the other.
 *
 * Under them, across the whole width, the outputs of the project (outputs-panel): the dmxboxes,
 * Art-Net nodes, sACN and DMX USB interfaces, and the universe each plays.
 *
 * The universes and the outputs belong to the project, the autosave and LAN to the desk program;
 * the rest is kept as desk-settings.ts says.
 */
@Component({
  imports: [KeyColorsPanel, KeyboardPanel, OutputsPanel],
  selector: 'lm-settings-view',
  styleUrl: './settings-view.css',
  templateUrl: './settings-view.html',
})
export class SettingsView {
  protected readonly settings = inject(DeskSettings);
  protected readonly store = inject(DeskStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  // ---------------------------------------------------------------- layout and zoom

  protected readonly sizes = CHANNEL_SIZES;
  protected readonly rowCounts = ROW_COUNTS;
  protected readonly wheelSizes = WHEEL_SIZES;
  protected readonly maxChannelKeys = CHANNEL_INDEPENDENT_COUNT;
  protected readonly maxMemoryKeys = MEMORY_INDEPENDENT_COUNT;
  protected readonly smallest = ZOOM_MIN;
  protected readonly biggest = ZOOM_MAX;

  // ---------------------------------------------------------------- the views in the top bar

  protected readonly alwaysShown = ALWAYS_SHOWN;

  /** The views in the order of their links in the top bar. */
  protected readonly orderedViews = computed(() => this.settings.viewOrder().map(viewNamed));

  /** True while every view is in the top bar in the usual order: then ↺ has nothing to do. */
  protected readonly viewsUsual = computed(
    () =>
      this.settings.hiddenViews().length === 0 &&
      this.settings.viewOrder().every((name, at) => name === DEFAULT_VIEW_ORDER[at]),
  );

  protected viewShown(view: ViewName): boolean {
    return !this.settings.hiddenViews().includes(view);
  }

  protected onViewShown(view: ViewName, event: Event): void {
    this.settings.setViewShown(view, (event.target as HTMLInputElement).checked);
  }

  /**
   * ▲ or ▼: the view one place earlier or later. The keyboard focus stays with it, on the same
   * arrow, or on the other one where this one has gone dark (at the top or the bottom).
   */
  protected moveView(view: ViewName, by: -1 | 1): void {
    this.settings.moveView(view, by);
    afterNextRender(
      () => {
        const arrow = (way: number) => this.host.nativeElement.querySelector<HTMLButtonElement>(`[data-move="${view}:${way}"]`);
        const same = arrow(by);
        (same && !same.disabled ? same : arrow(-by))?.focus();
      },
      { injector: this.injector },
    );
  }

  protected sizeName(size: ChannelSize): string {
    return size === 'auto' ? 'Auto (by the window)' : `${size}`;
  }

  protected rowsName(count: RowCount): string {
    return count === 1 ? '1 row' : `${count} rows`;
  }

  protected onSize(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.settings.setChannelSize(value === 'auto' ? 'auto' : (Number(value) as ChannelSize));
  }

  protected onChannelRows(event: Event): void {
    this.settings.setChannelRows(Number((event.target as HTMLSelectElement).value) as RowCount);
  }

  protected onMemoryRows(event: Event): void {
    this.settings.setMemoryRows(Number((event.target as HTMLSelectElement).value) as RowCount);
  }

  protected onWheelSize(event: Event): void {
    this.settings.setWheelSize(Number((event.target as HTMLSelectElement).value) as WheelSize);
  }

  protected onChannelKeys(event: Event): void {
    const field = event.target as HTMLInputElement;
    this.settings.setChannelKeys(Number(field.value));
    field.value = String(this.settings.channelKeys());
  }

  protected onMemoryKeys(event: Event): void {
    const field = event.target as HTMLInputElement;
    this.settings.setMemoryKeys(Number(field.value));
    field.value = String(this.settings.memoryKeys());
  }

  protected readonly maxEffectKeys = EFFECT_INDEPENDENT_COUNT;

  protected onEffectKeys(event: Event): void {
    const field = event.target as HTMLInputElement;
    this.settings.setEffectKeys(Number(field.value));
    field.value = String(this.settings.effectKeys());
  }

  protected onEffectRows(event: Event): void {
    this.settings.setEffectRows((event.target as HTMLSelectElement).value === '1' ? 1 : 0);
  }

  protected readonly zoomSaid = computed(() => `${Math.round(this.settings.zoom() * 100)} %`);

  // ---------------------------------------------------------------- universes

  protected readonly universeCounts = Array.from({ length: MAX_UNIVERSES }, (_, index) => index + 1);
  protected readonly universeSize = UNIVERSE_SIZE;

  /**
   * Another number of universes for the project. The list shows the number the project has until
   * the desk program has taken the new one: it refuses fewer while something uses a channel of a
   * universe that would go, and the top bar says why.
   */
  protected onUniverses(event: Event): void {
    const field = event.target as HTMLSelectElement;
    const universes = Number(field.value);
    field.value = String(this.store.universes());
    this.store.setUniverses(universes);
  }

  // ---------------------------------------------------------------- autosave

  protected readonly autosaveIntervals = AUTOSAVE_INTERVALS;

  /** The autosave of the desk program, for a page on the PC; null on another device and with an older desk program. */
  protected readonly autosave = computed(() => (this.store.lan()?.local ? this.store.autosave() : null));

  protected intervalName(seconds: number): string {
    return seconds < 60 ? `${seconds} seconds` : seconds === 60 ? '1 minute' : `${seconds / 60} minutes`;
  }

  protected onAutosaveOn(event: Event): void {
    const autosave = this.autosave();
    if (autosave) this.store.setAutosave((event.target as HTMLSelectElement).value === 'on', autosave.seconds);
  }

  protected onAutosaveEvery(event: Event): void {
    const autosave = this.autosave();
    if (autosave) this.store.setAutosave(autosave.on, Number((event.target as HTMLSelectElement).value));
  }

  // ---------------------------------------------------------------- LAN

  /** LAN as the desk program has it, for a page on the PC; null on another device and with an older desk program. */
  protected readonly lan = computed(() => {
    const lan = this.store.lan();
    return lan?.local ? lan : null;
  });

  /** The address phones use now. */
  protected readonly addressInUse = computed(() => {
    const lan = this.lan();
    return lan ? lanAddressInUse(lan) : null;
  });

  /** True while an address is chosen that this PC does not have now. */
  protected readonly chosenGone = computed(() => {
    const lan = this.lan();
    return !!lan?.chosen && !lan.addresses.some((address) => address.split(':')[0] === lan.chosen);
  });

  protected readonly passwordProblem = signal('');
  protected readonly passwordDone = signal('');

  protected onAddress(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.store.setLanAddress(value === '' ? null : value);
  }

  protected async setPassword(field: HTMLInputElement): Promise<void> {
    this.passwordProblem.set('');
    this.passwordDone.set('');
    try {
      await this.store.setLanPassword(field.value);
      field.value = '';
      this.passwordDone.set('The new password is set. Every phone and tablet has to give it now.');
    } catch (error) {
      this.passwordProblem.set(plainMessage(error));
    }
  }

  protected newPassword(): void {
    this.passwordProblem.set('');
    this.passwordDone.set('A new password is made. Every phone and tablet has to give it now.');
    this.store.newLanPassword();
  }
}

/** The desk's own sentence from a refusal, without what SignalR puts before it. */
function plainMessage(error: unknown): string {
  const text = error instanceof Error ? error.message : String(error);
  return text.replace(/^.*HubException: /, '');
}
