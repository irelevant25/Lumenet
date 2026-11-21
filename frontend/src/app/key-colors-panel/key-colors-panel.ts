import { Component, computed, inject, signal } from '@angular/core';
import { DeskSettings } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import { KEY_COLORS, KEY_COLOR_GROUPS, KeyColor, STANDARD_KEY_COLORS } from '../desk/key-colors';

/**
 * The colors of the keys, in Settings: for each kind of key (channels, memories, sequences) and
 * for each other key, group by group, its color and a key back to its usual one; ↺ beside the
 * title takes all of them back. A press on a color opens the colors the keys have at first, to
 * take one of them, and a field for any other color. While that one is being picked the desk
 * shows it at once; it is kept when the picker closes. On the PC the colors belong to the
 * project, like the layout; a phone keeps its own.
 */
@Component({
  selector: 'lm-key-colors-panel',
  styleUrl: './key-colors-panel.css',
  templateUrl: './key-colors-panel.html',
})
export class KeyColorsPanel {
  protected readonly settings = inject(DeskSettings);
  protected readonly store = inject(DeskStore);

  protected readonly groups = KEY_COLOR_GROUPS.map((name) => ({
    name,
    entries: KEY_COLORS.filter((entry) => entry.group === name),
  }));

  protected readonly standards = STANDARD_KEY_COLORS;

  /** The color whose palette is open; null for none. */
  protected readonly open = signal<string | null>(null);

  /** True while some color differs from the usual one. */
  protected readonly changed = computed(() => Object.keys(this.settings.colors()).length > 0);

  protected colorOf(entry: KeyColor): string {
    return this.settings.colors()[entry.id] ?? entry.standard;
  }

  /** A press on a color: its palette opens, or closes when it was open. */
  protected toggle(entry: KeyColor): void {
    this.open.update((open) => (open === entry.id ? null : entry.id));
  }

  /** One of the colors the keys have at first. */
  protected choose(entry: KeyColor, color: string): void {
    this.settings.setColor(entry.id, color);
    this.open.set(null);
  }

  /** While another color is picked: shown at once, not kept yet. */
  protected onInput(entry: KeyColor, event: Event): void {
    document.documentElement.style.setProperty(entry.variable, (event.target as HTMLInputElement).value);
  }

  /** The picker closed: the color is kept (and goes with the project, on the PC). */
  protected onChange(entry: KeyColor, event: Event): void {
    this.settings.setColor(entry.id, (event.target as HTMLInputElement).value);
  }
}
