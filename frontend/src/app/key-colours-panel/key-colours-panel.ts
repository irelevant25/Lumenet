import { Component, computed, inject } from '@angular/core';
import { DeskSettings } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import { KEY_COLOURS, KEY_COLOUR_GROUPS, KeyColour } from '../desk/key-colours';

/**
 * The colours of the keys, in Settings: a colour field for each kind of key (channels, memories,
 * sequences) and for each other key, group by group, each with a key back to its usual colour.
 * While a colour is being picked, the desk shows it at once; it is kept when the picker closes.
 * On the PC the colours belong to the project, like the layout; a phone keeps its own.
 */
@Component({
  selector: 'lm-key-colours-panel',
  styleUrl: './key-colours-panel.css',
  templateUrl: './key-colours-panel.html',
})
export class KeyColoursPanel {
  protected readonly settings = inject(DeskSettings);
  protected readonly store = inject(DeskStore);

  protected readonly groups = KEY_COLOUR_GROUPS.map((name) => ({
    name,
    entries: KEY_COLOURS.filter((entry) => entry.group === name),
  }));

  /** True while some colour differs from the usual one. */
  protected readonly changed = computed(() => Object.keys(this.settings.colours()).length > 0);

  protected colourOf(entry: KeyColour): string {
    return this.settings.colours()[entry.id] ?? entry.standard;
  }

  /** While the colour is picked: shown at once, not kept yet. */
  protected onInput(entry: KeyColour, event: Event): void {
    document.documentElement.style.setProperty(entry.variable, (event.target as HTMLInputElement).value);
  }

  /** The picker closed: the colour is kept (and goes with the project, on the PC). */
  protected onChange(entry: KeyColour, event: Event): void {
    this.settings.setColour(entry.id, (event.target as HTMLInputElement).value);
  }
}
