import { Component, computed, inject, input } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { stripNumbers } from '../desk/desk-types';
import { MAX_EFFECTS, effectLamp } from '../desk/effects';
import { heldItem, itemBlinks, itemWaits } from '../desk/function-mode';
import { LampLevel } from '../desk/lamps';
import { DragScroll } from '../desk/drag-scroll';
import { PressedInSight } from '../desk/pressed-in-sight';
import { FaderStrip } from '../fader-strip/fader-strip';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * A row of effect faders, with the keys that choose which effects it shows: on the main view (when
 * Settings shows it), and at the top of the Effects view. Each effect has its fader (raised from zero
 * it starts the effect, at zero it stops it), its key under it (moves the fader up in the effect's
 * fade-in time and down in its fade-out time), and its name under the key. On the Effects view the
 * name chooses the effect the editor works on, as the key of an empty number does, and the row has
 * as many faders as the window has room for.
 */
@Component({
  imports: [DragScroll, FaderStrip, LampButton, PressedInSight],
  selector: 'lm-effect-section',
  // the same layout as the row of memories
  styleUrl: '../memory-section/memory-section.css',
  templateUrl: './effect-section.html',
})
export class EffectSection {
  protected readonly store = inject(DeskStore);

  /** True on the Effects view: the names choose the effect to edit. */
  readonly choosing = input(false);

  private readonly perPage = computed(() =>
    this.choosing() ? this.store.viewMemoriesPerPage() : this.store.stripsPerRow(),
  );
  /** The first effect of the page that is shown. */
  protected readonly first = computed(() =>
    this.choosing() ? this.store.viewFirstEffect() : this.store.firstEffect(),
  );

  /** The effects of the page that is shown. */
  protected readonly numbers = computed(() =>
    stripNumbers(this.first(), this.perPage()).filter((number) => number <= MAX_EFFECTS),
  );

  /** The page keys: full for the page that is shown, half for a page with an effect on it. */
  protected readonly pages = computed(() => {
    const perPage = this.perPage();
    const shown = this.first();
    const effects = this.store.effectsByNumber();
    return Array.from({ length: Math.ceil(MAX_EFFECTS / perPage) }, (_, page) => {
      const first = page * perPage + 1;
      const last = Math.min(MAX_EFFECTS, first + perPage - 1);
      const holds = stripNumbers(first, last - first + 1).some((number) => effects.has(number));
      const lamp: LampLevel = first === shown ? 3 : holds ? 2 : 1;
      return { first, last, shown: first === shown, lamp };
    });
  });

  /**
   * The key of an effect moves its fader (see DeskStore.toggleEffect), or Copy or Del works on it
   * (DeskStore.pressItem). On the Effects view the key of an empty number chooses it instead (unless
   * Copy waits for the number to copy to), and the view offers the kinds to make it; a copy made
   * there is chosen.
   */
  protected press(number: number): void {
    const mode = this.store.mode();
    if (this.choosing() && !this.store.effectsByNumber().has(number) && !itemWaits(mode)) {
      this.store.chosenEffect.set(number);
      return;
    }
    const copying = mode.kind === 'copy-target' && mode.of === 'effect' && mode.from !== number;
    this.store.pressItem('effect', number);
    if (this.choosing() && copying) this.store.chosenEffect.set(number);
  }

  protected levelOf(number: number): number {
    return this.store.effectPlaybacks().get(number)?.level ?? 0;
  }

  /** The effect Copy copies from stays lit. */
  protected lamp(number: number): LampLevel {
    if (heldItem(this.store.mode(), 'effect') === number) return 3;
    return effectLamp(
      this.store.effectsByNumber().get(number),
      this.store.effectPlaybacks().get(number),
    );
  }

  /** While its key moves its fader, and while Copy or Del waits for it. */
  protected blinks(number: number): boolean {
    return (
      (this.store.effectPlaybacks().get(number)?.fading ?? false) ||
      itemBlinks(this.store.mode(), 'effect', number, this.store.effectsByNumber().has(number))
    );
  }

  protected nameOf(number: number): string {
    return this.store.effectsByNumber().get(number)?.name ?? '';
  }
}
