import { Component, ElementRef, computed, inject, signal } from '@angular/core';
import { DeskSettings } from '../../desk/desk-settings';
import { KEY_COLORS, KEY_COLOR_GROUPS, KeyColor, STANDARD_KEY_COLORS } from '../../desk/key-colors';

/**
 * The colors of the keys, in Settings: for each kind of key (channels, memories, sequences) and
 * for each other key, group by group, its color and a key back to its usual one; ↺ beside the
 * title takes all of them back. A press on a color opens the colors the keys have at first, to
 * take one of them, and a field for any other color; a press anywhere else, or Esc, closes them.
 * While another color is being picked the desk shows it at once; it is kept when the picker
 * closes. The colors belong to this device, like the rest of the settings. The panel is as big as
 * the other panels of Settings: the groups stand one under the other and scroll.
 */
@Component({
  host: {
    '(document:click)': 'closeOutside($event)',
    '(keydown.escape)': 'close()',
  },
  selector: 'lm-key-colors-panel',
  styleUrl: './key-colors-panel.css',
  templateUrl: './key-colors-panel.html',
})
export class KeyColorsPanel {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly settings = inject(DeskSettings);

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
    this.close();
  }

  /** A press anywhere but in the open palette or on a color (that one opens or closes itself) closes it. */
  protected closeOutside(event: Event): void {
    if (this.open() === null) return;
    const target = event.target;
    if (target instanceof Element && this.host.nativeElement.contains(target) && target.closest('.palette, .color .swatch')) return;
    this.close();
  }

  /** The palette closes; the keyboard, when it was in there, goes back to the color that opened it. */
  protected close(): void {
    const host = this.host.nativeElement;
    if (host.querySelector('.palette')?.contains(document.activeElement)) {
      host.querySelector<HTMLElement>('.swatch[aria-expanded="true"]')?.focus();
    }
    this.open.set(null);
  }

  /** While another color is picked: shown at once, not kept yet. */
  protected onInput(entry: KeyColor, event: Event): void {
    document.documentElement.style.setProperty(entry.variable, (event.target as HTMLInputElement).value);
  }

  /** The picker closed: the color is kept. */
  protected onChange(entry: KeyColor, event: Event): void {
    this.settings.setColor(entry.id, (event.target as HTMLInputElement).value);
  }
}
