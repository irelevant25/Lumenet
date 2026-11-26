import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../../desk/desk-store';
import { DEFAULT_BPM, MAX_BPM, MIN_BPM } from '../../desk/tempo';
import { Fader } from '../../shared/fader/fader';
import { LampButton } from '../../shared/lamp-button/lamp-button';

/**
 * General: Clear, Solo, and Rate (a tempo for the sequences) with its fader, the tempo above the
 * fader and TEMPO under it. A column beside the master section on the main view, under the stack;
 * in the mobile layout a line across the top.
 *
 * While RATE is lit every sequence takes one step per beat of the tempo. The fader goes from 30
 * beats per minute at the bottom to 300 at the top, in whole beats; a mark stands at 120, and a
 * double click puts it back there. TEMPO, tapped in time with the music, sets it too; it works
 * only while RATE is lit. Dark, the fader can still be set for the next time.
 */
@Component({
  imports: [Fader, LampButton],
  selector: 'lm-general-section',
  styleUrl: './general-section.css',
  templateUrl: './general-section.html',
})
export class GeneralSection {
  protected readonly store = inject(DeskStore);

  /** The fader's positions: one for each beat per minute from 30 to 300. */
  protected readonly positions = MAX_BPM - MIN_BPM;
  protected readonly position = computed(() => this.store.bpm() - MIN_BPM);
  protected readonly bpmText = computed(() => `${this.store.bpm()} BPM`);
  protected readonly bpmSaid = computed(() => `${this.store.bpm()} beats per minute`);
  /** Where 120 beats per minute stands on the fader, from 0 at the bottom to 1 at the top: its mark. */
  protected readonly mark = (DEFAULT_BPM - MIN_BPM) / (MAX_BPM - MIN_BPM);

  protected onPosition(position: number): void {
    this.store.setBpm(MIN_BPM + position);
  }

  protected resetBpm(): void {
    this.store.setBpm(DEFAULT_BPM);
  }
}
