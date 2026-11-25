import { Component, computed, inject, input } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { stripNumbers } from '../desk/desk-types';
import { heldItem, itemBlinks, itemWaits } from '../desk/function-mode';
import { LampLevel } from '../desk/lamps';
import { DragScroll } from '../desk/drag-scroll';
import { PressedInSight } from '../desk/pressed-in-sight';
import { MAX_SOUNDS, soundLamp } from '../desk/sounds';
import { FaderStrip } from '../fader-strip/fader-strip';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * A row of sound faders, with the keys that choose which sounds it shows: on the main view (when
 * Settings shows it), and at the top of the Sounds view. Each sound has its fader (raised from zero
 * it starts the sound, at zero it stops it; at full it plays at the sound's own level), its key
 * under it (moves the fader up in the sound's fade-in time and down in its fade-out time), and its
 * name under the key. Sound effects are pink; music, empty numbers and the page keys cyan. On the
 * Sounds view the name chooses the sound the editor works on, as the key of an empty number does,
 * and the row has as many faders as the window has room for.
 */
@Component({
  imports: [DragScroll, FaderStrip, LampButton, PressedInSight],
  selector: 'lm-sound-section',
  // the same layout as the row of memories
  styleUrl: '../memory-section/memory-section.css',
  templateUrl: './sound-section.html',
})
export class SoundSection {
  protected readonly store = inject(DeskStore);

  /** True on the Sounds view: the names choose the sound to edit. */
  readonly choosing = input(false);

  private readonly perPage = computed(() =>
    this.choosing() ? this.store.viewMemoriesPerPage() : this.store.stripsPerRow(),
  );
  /** The first sound of the page that is shown. */
  protected readonly first = computed(() =>
    this.choosing() ? this.store.viewFirstSound() : this.store.firstSound(),
  );

  /** The sounds of the page that is shown. */
  protected readonly numbers = computed(() =>
    stripNumbers(this.first(), this.perPage()).filter((number) => number <= MAX_SOUNDS),
  );

  /** The page keys: full for the page that is shown, half for a page with a sound on it. */
  protected readonly pages = computed(() => {
    const perPage = this.perPage();
    const shown = this.first();
    const sounds = this.store.soundsByNumber();
    return Array.from({ length: Math.ceil(MAX_SOUNDS / perPage) }, (_, page) => {
      const first = page * perPage + 1;
      const last = Math.min(MAX_SOUNDS, first + perPage - 1);
      const holds = stripNumbers(first, last - first + 1).some((number) => sounds.has(number));
      const lamp: LampLevel = first === shown ? 3 : holds ? 2 : 1;
      return { first, last, shown: first === shown, lamp };
    });
  });

  /**
   * The key of a sound moves its fader (see DeskStore.toggleSound), or Copy or Del works on it. On
   * the Sounds view the key of an empty number chooses it instead (unless Copy waits for the number
   * to copy to), and the view offers to add a file; a copy made there is chosen.
   */
  protected press(number: number): void {
    const mode = this.store.mode();
    if (this.choosing() && !this.store.soundsByNumber().has(number) && !itemWaits(mode)) {
      this.store.chosenSound.set(number);
      return;
    }
    const copying = mode.kind === 'copy-target' && mode.of === 'sound' && mode.from !== number;
    this.store.pressItem('sound', number);
    if (this.choosing() && copying) this.store.chosenSound.set(number);
  }

  protected levelOf(number: number): number {
    return this.store.soundPlaybacks().get(number)?.level ?? 0;
  }

  /** Full while it plays, half for a quiet sound; the sound Copy copies from stays lit. */
  protected lamp(number: number): LampLevel {
    if (heldItem(this.store.mode(), 'sound') === number) return 3;
    return soundLamp(
      this.store.soundsByNumber().get(number),
      this.store.soundPlaybacks().get(number),
    );
  }

  /** While its key moves its fader, and while Copy or Del waits for it. */
  protected blinks(number: number): boolean {
    return (
      (this.store.soundPlaybacks().get(number)?.fading ?? false) ||
      itemBlinks(this.store.mode(), 'sound', number, this.store.soundsByNumber().has(number))
    );
  }

  protected nameOf(number: number): string {
    return this.store.soundsByNumber().get(number)?.name ?? '';
  }
}
