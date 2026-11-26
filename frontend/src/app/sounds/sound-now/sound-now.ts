import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../../desk/desk-store';
import { SoundWaveStrip } from '../sound-wave/sound-wave';

/**
 * Over the steps of the Stack view and of the Stage view: the wave of the sound that plays, with its
 * number and name. Of the sounds that play, the one that started last; while none plays, the one
 * that played last (see DeskStore.shownSound). As on the Sounds view, a double click on the wave plays
 * it from there.
 */
@Component({
  imports: [SoundWaveStrip],
  selector: 'lm-sound-now',
  styleUrl: './sound-now.css',
  templateUrl: './sound-now.html',
})
export class SoundNow {
  private readonly store = inject(DeskStore);

  protected readonly sound = computed(() => {
    const number = this.store.shownSound();
    return number === undefined ? undefined : this.store.soundsByNumber().get(number);
  });

  protected readonly playback = computed(() => {
    const sound = this.sound();
    return sound ? this.store.soundPlaybacks().get(sound.number) : undefined;
  });
}
