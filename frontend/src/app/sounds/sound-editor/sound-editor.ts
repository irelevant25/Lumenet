import { Component, ElementRef, computed, inject, input, signal, viewChild } from '@angular/core';
import { DeskStore } from '../../desk/desk-store';
import { uploadSoundFile } from '../sound-files';
import { Fader } from '../../shared/fader/fader';
import {
  MAX_ECHO_REPEATS,
  MAX_ECHO_TIME,
  MAX_SOUND_PITCH,
  MAX_SOUND_SPEED,
  MAX_DECIBELS,
  MIN_DECIBELS,
  MIN_ECHO_TIME,
  MIN_SOUND_SPEED,
  SOUND_FILE_ENDINGS,
  SOUND_NAME_LENGTH,
  Sound,
  decibelsAt,
  isTrack,
  placeOf,
  timeText,
  trackText,
} from '../sounds';

/** The longest fade, as for a step. */
const MAX_FADE = 999.9;

/**
 * What can change while the stack is live: how a sound sounds (a sound the audience laughs over, say),
 * not what it plays.
 */
const LIVE_KEYS: readonly (keyof Sound)[] = [
  'decibels',
  'pan',
  'speed',
  'pitch',
  'echo',
  'echoTime',
  'echoRepeats',
  'reverb',
  'room',
];

/**
 * The editor of a sound: its name, its file (another one can
 * take its place), where it starts and ends in the file, whether it loops, its level, its fade times,
 * pan, speed and pitch, echo and reverb, the sliders the lying faders of the desk (the wheel, the arrows
 * and a drag move them, as on the Effects view). Every change is stored at once (the store sends quick changes
 * together). While the stack is live only how it sounds changes: level, pan, speed, pitch, echo and
 * reverb with their repeats and room. A sound
 * that plays goes on with the change; with another file it starts again. A track of the mixer has only
 * its name, its level and its fades: the mixer's player plays it as it is, PLAYER L/R at its level.
 */
@Component({
  imports: [Fader],
  selector: 'lm-sound-editor',
  // the form of the effect editor, and what only a sound has
  styleUrls: ['../../effects/effect-editor/effect-editor.css', './sound-editor.css'],
  templateUrl: './sound-editor.html',
})
export class SoundEditor {
  protected readonly store = inject(DeskStore);
  readonly sound = input.required<Sound>();

  protected readonly nameLength = SOUND_NAME_LENGTH;
  protected readonly locked = this.store.live;
  protected readonly minDecibels = MIN_DECIBELS;
  protected readonly maxDecibels = MAX_DECIBELS;
  /** Where the fader of its level stands, 0 to 255: the gain is the place squared, as on the fader of all sounds. */
  protected readonly levelPlace = computed(() => placeOf(this.sound().decibels));
  protected readonly minSpeed = MIN_SOUND_SPEED;
  protected readonly maxSpeed = MAX_SOUND_SPEED;
  protected readonly maxPitch = MAX_SOUND_PITCH;
  protected readonly minEchoTime = MIN_ECHO_TIME;
  protected readonly maxEchoTime = MAX_ECHO_TIME;
  protected readonly maxEchoRepeats = MAX_ECHO_REPEATS;
  protected readonly maxFade = MAX_FADE;
  protected readonly accept = SOUND_FILE_ENDINGS.join(',');

  /** The file and how long it is: "thunder.mp3, 12.5 s". */
  protected readonly fileText = computed(
    () => `${this.sound().file}, ${timeText(this.sound().seconds)}`,
  );
  /** True for a track of the mixer's USB stick. */
  protected readonly track = computed(() => isTrack(this.sound()));
  /** The playlist and the track: "Show / Intro.mp3". */
  protected readonly trackText = computed(() => trackText(this.sound()));
  /** True while another file goes to the desk program. */
  protected readonly sending = signal(false);

  private readonly picker = viewChild<ElementRef<HTMLInputElement>>('picker');

  protected onName(event: Event): void {
    const field = event.target as HTMLInputElement;
    const name = field.value.trim().slice(0, SOUND_NAME_LENGTH);
    field.value = name;
    this.update({ name });
  }

  protected onLoop(event: Event): void {
    this.update({ loop: (event.target as HTMLInputElement).checked });
  }

  /** Where it starts: from 0 to before its end (or the end of the file). */
  protected onStart(event: Event): void {
    const sound = this.sound();
    const last = Math.max(0, (sound.end ?? sound.seconds) - 0.01);
    this.onNumber('start', event, 0, sound.seconds > 0 ? last : 86400, 2);
  }

  /** Where it ends: after its start, up to the end of the file; empty: the end of the file. */
  protected onEnd(event: Event): void {
    const field = event.target as HTMLInputElement;
    const sound = this.sound();
    if (field.value.trim() === '') {
      this.update({ end: null });
      return;
    }
    const value = Number(field.value.trim().replace(',', '.'));
    const top = sound.seconds > 0 ? sound.seconds : 86400;
    if (!Number.isFinite(value) || value <= sound.start || value > top) {
      field.value = sound.end == null ? '' : String(sound.end);
      return;
    }
    // to the very end of the file is the same as none
    const end = Math.round(value * 100) / 100;
    this.update({ end: sound.seconds > 0 && end >= sound.seconds ? null : end });
  }

  /**
   * A number typed into a field: taken over when it is a number within the range, rounded to
   * `digits` places after the point; otherwise the field shows what the sound has again.
   */
  protected onNumber(key: NumberKey, event: Event, min: number, max: number, digits = 0): void {
    const field = event.target as HTMLInputElement;
    const value = Number(field.value.trim().replace(',', '.'));
    if (field.value.trim() === '' || !Number.isFinite(value) || value < min || value > max) {
      field.value = String(this.sound()[key]);
      return;
    }
    const factor = 10 ** digits;
    this.update({ [key]: Math.round(value * factor) / factor } as Partial<Sound>);
  }

  /** The fader of its level moved: the decibels where it stands. */
  protected onLevel(place: number): void {
    this.update({ decibels: decibelsAt(place) });
  }

  /**
   * Where the speed stands on its fader, 0 to 200: half as fast at the left, twice as fast at the
   * right, 1× in the middle, each step the same share faster.
   */
  protected readonly speedPlace = computed(
    () => Math.round(Math.log2(this.sound().speed) * 100) + 100,
  );

  /** The speed fader moved: the speed where it stands, rounded as the field shows it. */
  protected onSpeed(place: number): void {
    const speed = Math.round(2 ** ((place - 100) / 100) * 100) / 100;
    this.update({ speed: Math.min(MAX_SOUND_SPEED, Math.max(MIN_SOUND_SPEED, speed)) });
  }

  protected pickFile(): void {
    this.picker()?.nativeElement.click();
  }

  /**
   * Another file in place of the sound's: it goes to the desk program, and the sound takes it with
   * what else it has. Where it starts and ends stays as far as the new file is long enough.
   */
  protected async onFile(event: Event): Promise<void> {
    const field = event.target as HTMLInputElement;
    const file = field.files?.[0];
    field.value = '';
    if (!file || this.locked()) return;
    this.sending.set(true);
    try {
      const info = await uploadSoundFile(file);
      if (typeof info === 'string') {
        this.store.tell(info);
        return;
      }
      const sound = this.sound();
      const fits =
        info.seconds <= 0 || ((sound.end ?? 0) <= info.seconds && sound.start < info.seconds);
      this.update({
        file: info.name,
        hash: info.hash,
        seconds: info.seconds,
        ...(fits ? {} : { start: 0, end: null }),
      });
    } finally {
      this.sending.set(false);
    }
  }

  protected leave(event: Event): void {
    (event.target as HTMLElement).blur();
  }

  /** Stores the sound with the change; while the stack is live only how it sounds changes. */
  protected update(change: Partial<Sound>): void {
    const live = Object.keys(change).every((key) => LIVE_KEYS.includes(key as keyof Sound));
    if (this.locked() && !live) return;
    this.store.setSound({ ...this.sound(), ...change });
  }
}

/** The fields of a sound that are typed as numbers. */
type NumberKey =
  | 'start'
  | 'decibels'
  | 'fadeIn'
  | 'fadeOut'
  | 'pan'
  | 'speed'
  | 'pitch'
  | 'echo'
  | 'echoTime'
  | 'echoRepeats'
  | 'reverb'
  | 'room';
