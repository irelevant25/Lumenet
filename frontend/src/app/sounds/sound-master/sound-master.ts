import { Component, computed, inject, input } from '@angular/core';
import { DeskStore } from '../../desk/desk-store';
import { DEFAULT_SOUND_DECIBELS, decibelText, decibelsAt, listenLamp, placeOf } from '../sounds';
import { Fader } from '../../shared/fader/fader';
import { LampButton } from '../../shared/lamp-button/lamp-button';

/**
 * Sound: MUTE and LISTEN over the master volume of the sound. A column beside the general section on
 * the main view, on the Sounds view, and beside the cards of the stack on the Stage view; a line lying
 * down on the Stack view ("lying") and in the mobile layout. MUTE blinks while it is on, as Blackout
 * does: no sound goes out, the sounds play on unheard. LISTEN plays what goes out on the device to
 * listen on as well (Settings, Sound): lit while it does, half lit while that device is not there. The
 * fader goes from -100 dB (nothing) at the bottom to 0 dB at the top (every sound at its own level), the
 * gain as its place squared (half way is -12 dB), -20 dB at first; the volume stands above it, and a
 * double click puts it back at -20 dB. While a Soundcraft Ui mixer answers it is the mixer's PLAYER
 * L/R, both ways.
 */
@Component({
  host: { '[class.lying]': 'lying()' },
  imports: [Fader, LampButton],
  selector: 'lm-sound-master',
  styleUrl: './sound-master.css',
  templateUrl: './sound-master.html',
})
export class SoundMaster {
  protected readonly store = inject(DeskStore);
  /** True: a line, the fader lying (the Stack view). */
  readonly lying = input(false);
  protected readonly listenLevel = computed(() => listenLamp(this.store.listen()));

  /** Where the fader stands, 0 to 255. */
  protected readonly place = computed(() => placeOf(this.store.soundDecibels()));
  protected readonly volumeText = computed(() => decibelText(this.store.soundDecibels()));
  protected readonly volumeSaid = computed(() => `${this.store.soundDecibels()} decibels`);

  protected onFader(place: number): void {
    this.store.setSoundVolume(decibelsAt(place));
  }

  protected resetVolume(): void {
    this.store.setSoundVolume(DEFAULT_SOUND_DECIBELS);
  }
}
