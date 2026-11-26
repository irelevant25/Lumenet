import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { DeskStore } from '../../desk/desk-store';
import { uploadSoundFile } from '../sound-files';
import {
  MAX_SOUNDS,
  SOUND_FILE_ENDINGS,
  isTrack,
  newSound,
  newTrack,
  timeText,
  trackText,
  tracksPlaylist,
} from '../sounds';
import { RecordSection } from '../../shared/record-section/record-section';
import { SoundEditor } from '../sound-editor/sound-editor';
import { SoundMaster } from '../sound-master/sound-master';
import { SoundSection } from '../sound-section/sound-section';
import { SoundWaveStrip } from '../sound-wave/sound-wave';

/**
 * The Sounds view: the sounds with their faders and keys at the top, as on the main view, with Copy
 * and Del beside them and the sound section (MUTE and the volume of all sounds); under them the sound
 * chosen by its name: where it is, and its editor (the device the sounds play on is in Settings). An
 * empty number takes files: chosen, or dropped onto it. While a Soundcraft Ui mixer answers, it takes
 * a track of the mixer's USB stick too: of the playlist chosen in Settings, or any of its tracks.
 */
@Component({
  host: {
    '(dragover)': 'onDragOver($event)',
    '(drop)': 'onDrop($event)',
  },
  imports: [RecordSection, SoundEditor, SoundMaster, SoundSection, SoundWaveStrip],
  selector: 'lm-sounds-view',
  styleUrl: './sounds-view.css',
  templateUrl: './sounds-view.html',
})
export class SoundsView {
  protected readonly store = inject(DeskStore);
  protected readonly accept = SOUND_FILE_ENDINGS.join(',');

  protected readonly chosen = this.store.chosenSound;
  protected readonly sound = computed(() => this.store.soundsByNumber().get(this.chosen()));
  protected readonly playback = computed(() => this.store.soundPlaybacks().get(this.chosen()));

  /** The file that goes to the desk program now, as the page says it; empty while none does. */
  protected readonly sending = signal('');

  private readonly picker = viewChild<ElementRef<HTMLInputElement>>('picker');

  /** One sentence about the sound that is chosen. */
  protected readonly summary = computed(() => {
    if (!this.store.soundsKnown())
      return 'The desk program does not know sounds yet: start the newest Lumenet.';
    const sound = this.sound();
    if (!sound)
      return `Sound ${this.chosen()} is empty. Add a file to it, or choose another sound by its name.`;
    const playback = this.playback();
    const track = isTrack(sound);
    const state = playback?.missing
      ? track
        ? 'it did not play: the mixer does not answer, or its stick has no such track'
        : 'its file cannot be played'
      : !playback?.playing
        ? 'quiet'
        : track && playback.length
          ? `playing, ${timeText(playback.position)} of ${timeText(playback.length)}`
          : playback.fading
            ? 'fading'
            : 'playing';
    const source = track
      ? `a track of the mixer (${trackText(sound)})`
      : `${sound.file}, ${timeText(sound.seconds)}`;
    return `Sound ${sound.number}, ${sound.name || 'without a name'}: ${source}, ${state}.`;
  });

  /** The first line of the list of tracks of the playlist chosen in Settings (or of every track). */
  protected readonly tracksText = computed(() => {
    const count = this.store.playlistTracks().length;
    return count === 0 ? 'No tracks in it' : `Track… (${count})`;
  });

  /** A playlist as the page shows it: the mixer's own two by what they hold. */
  protected playlistText(name: string): string {
    if (name === '~all~') return 'All tracks';
    if (name === '~root~') return 'Tracks outside the folders';
    return this.shown(name);
  }

  /** A name of the stick in the usual form of its letters (a stick may keep "é" as two characters). */
  protected shown(name: string): string {
    return name.normalize('NFC');
  }

  /** The playlist whose tracks are offered: the one of Settings, or every track. */
  protected readonly playlist = computed(() => tracksPlaylist(this.store.mixer()));

  /** A track chosen becomes the sound of the chosen number, named after it. */
  protected onTrack(event: Event): void {
    const field = event.target as HTMLSelectElement;
    const name = field.value;
    field.value = '';
    if (name === '' || this.store.live()) return;
    this.store.setSound(newTrack(this.chosen(), this.playlist(), name));
  }

  protected pick(): void {
    this.picker()?.nativeElement.click();
  }

  protected async onFiles(event: Event): Promise<void> {
    const field = event.target as HTMLInputElement;
    const files = [...(field.files ?? [])];
    field.value = '';
    await this.add(files);
  }

  // Files dragged over the view can be dropped onto an empty number.
  protected onDragOver(event: DragEvent): void {
    if (!this.sound() && !this.store.live() && event.dataTransfer?.types.includes('Files')) {
      event.preventDefault();
      event.dataTransfer.dropEffect = 'copy';
    }
  }

  protected async onDrop(event: DragEvent): Promise<void> {
    if (this.sound() || this.store.live()) return;
    event.preventDefault();
    await this.add([...(event.dataTransfer?.files ?? [])]);
  }

  /**
   * Files become sounds: the first under the chosen number, the others under the next empty numbers
   * after it. Each goes to the desk program first; one it does not take ends the row, and the page
   * says why.
   */
  private async add(files: File[]): Promise<void> {
    if (files.length === 0 || this.store.live() || this.sending()) return;
    let number = this.chosen();
    try {
      for (const [index, file] of files.entries()) {
        this.sending.set(
          files.length > 1 ? `${file.name} (${index + 1} of ${files.length})` : file.name,
        );
        const info = await uploadSoundFile(file);
        if (typeof info === 'string') {
          this.store.tell(info);
          return;
        }
        this.store.setSound(newSound(number, info));
        const next = this.nextFree(number);
        if (next === null) {
          if (index < files.length - 1)
            this.store.tell(
              `Every number has a sound: ${files.length - index - 1} files were left out.`,
            );
          return;
        }
        number = next;
      }
    } finally {
      this.sending.set('');
    }
  }

  // The next number after this one without a sound, going round; null when every one has one.
  private nextFree(after: number): number | null {
    const taken = this.store.soundsByNumber();
    for (let step = 1; step < MAX_SOUNDS; step++) {
      const number = ((after - 1 + step) % MAX_SOUNDS) + 1;
      if (!taken.has(number)) return number;
    }
    return null;
  }
}
