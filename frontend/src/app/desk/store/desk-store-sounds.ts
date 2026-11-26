import { MAX_LEVEL } from '../desk-types';
import {
  FoundMixer,
  MAX_DECIBELS,
  MAX_SOUNDS,
  MIN_DECIBELS,
  newTrack,
  quietPlayback,
  Sound,
  SOUND_INDEPENDENT_COUNT,
  SoundControl,
  SoundPlayback,
  tracksPlaylist,
} from '../../sounds/sounds';
import { EFFECT_TOUCH_MS, SOUND_SEND_MS, withItem } from './desk-store-helpers';
import { DeskStoreEffects } from './desk-store-effects';

/**
 * A layer of DeskStore: the sounds, their pages, keys and faders, the editor's changes, the sound
 * section (volume, MUTE, LISTEN), a Soundcraft Ui mixer and its tracks, and the sounds' independent keys.
 */
export abstract class DeskStoreSounds extends DeskStoreEffects {
  /** A sound key of the independents starts or stops its sound, as the key of the sound does. Solo leaves it alone. */
  toggleSoundIndependent(key: number): void {
    this.toggleSound(this.soundIndependents()[key - 1]);
  }

  /** Binds a sound key of the independents to another sound. */
  setSoundIndependent(key: number, sound: number): void {
    if (key < 1 || key > SOUND_INDEPENDENT_COUNT || sound < 1 || sound > MAX_SOUNDS) return;
    this.soundIndependents.update((sounds) => withItem(sounds, key - 1, sound));
    this.run('SetSoundIndependent', key, sound);
  }

  /** Shows the page of sounds that starts at `first`, on the main view and the Sounds view. */
  showSoundPage(first: number): void {
    this.wantedSound.set(first);
  }

  /**
   * Stores a sound, new or changed. The page shows it at once and sends it at once; while it keeps
   * changing (a slider dragged) the newest goes out every SOUND_SEND_MS, so a sound that plays
   * follows the slider. The desk program makes one step of the history of them.
   */
  /** LISTEN ON on or off: the key LISTEN of the sound section (the device to listen on is chosen in Settings). */
  toggleListen(): void {
    const listen = !this.listen().listen;
    this.listen.update((now) => ({ ...now, listen }));
    this.connection
      .invoke('SetSoundListen', listen)
      .then((view) => {
        if (view) this.listen.set(view);
      })
      .catch((error: Error) => this.say(error.message));
  }

  /**
   * A track of the mixer's playlist becomes a sound in the lowest free number, unless it is one already:
   * its number either way; null when every number has a sound.
   */
  addTrackSound(track: string): number | null {
    const playlist = tracksPlaylist(this.mixer());
    const known = this.sounds().find(
      (sound) => sound.playlist === playlist && sound.file === track,
    );
    if (known) return known.number;
    const taken = this.soundsByNumber();
    for (let number = 1; number <= MAX_SOUNDS; number++) {
      if (!taken.has(number)) {
        this.setSound(newTrack(number, playlist, track));
        return number;
      }
    }
    this.say('Every number has a sound: take one out on the Sounds view first.');
    return null;
  }

  /** Every track of a playlist of the mixer that is no sound yet becomes one, in the free numbers. */
  async addMixerTracks(playlist: string): Promise<void> {
    try {
      const added = (await this.connection.invoke('AddMixerTracks', playlist)) ?? 0;
      this.say(
        added > 0
          ? `${added} ${added === 1 ? 'track' : 'tracks'} of ${playlist.normalize('NFC')} became sounds.`
          : `Every track of ${playlist.normalize('NFC')} is a sound already, or every number has one.`,
      );
    } catch (error) {
      this.say((error as Error).message);
    }
  }

  // The tracks of the playlist chosen in Settings, for the steps and the Sounds view; none without one.
  protected async askPlaylistTracks(playlist: string): Promise<void> {
    const asked = ++this.playlistAsked;
    const tracks = playlist ? await this.mixerTracks(playlist) : [];
    if (asked === this.playlistAsked) this.playlistTracks.set(tracks);
  }

  /** The Soundcraft Ui mixers in the networks of the PC (the key Find of Settings); none when the search fails. */
  async findMixers(): Promise<FoundMixer[]> {
    try {
      return (await this.connection.invoke('FindMixers')) ?? [];
    } catch (error) {
      this.say((error as Error).message);
      return [];
    }
  }

  /** The playlists of the mixer's USB stick; none while the mixer does not answer, or from an older desk program. */
  async mixerPlaylists(): Promise<string[]> {
    try {
      return (await this.connection.invoke('GetMixerPlaylists')) ?? [];
    } catch {
      return [];
    }
  }

  /** The tracks of a playlist of the mixer's USB stick, by their names there. */
  async mixerTracks(playlist: string): Promise<string[]> {
    try {
      return (await this.connection.invoke('GetMixerTracks', playlist)) ?? [];
    } catch {
      return [];
    }
  }

  setSound(sound: Sound): void {
    this.putSound(sound);
    this.soundChanges.set(sound.number, (this.soundChanges.get(sound.number) ?? 0) + 1);
    const sending = this.soundSends.get(sound.number);
    if (sending) sending.waiting = sound;
    else this.sendSound(sound);
  }

  /** Takes a sound out of the project; it stops. A change of it that waits goes with it. */
  deleteSound(number: number): void {
    const sending = this.soundSends.get(number);
    if (sending) clearTimeout(sending.timer);
    this.soundSends.delete(number);
    this.soundChanges.set(number, (this.soundChanges.get(number) ?? 0) + 1);
    this.sounds.update((all) => all.filter((item) => item.number !== number));
    this.run('DeleteSound', number);
  }

  // Sends a sound, and holds the next change of it back for SOUND_SEND_MS. The answer is the sound as
  // the desk program keeps it (a file name made unique, numbers rounded): the page takes it unless
  // it changed the sound again since.
  protected sendSound(sound: Sound): void {
    const change = this.soundChanges.get(sound.number) ?? 0;
    const entry = {
      waiting: null as Sound | null,
      timer: setTimeout(() => {
        this.soundSends.delete(sound.number);
        if (entry.waiting) this.sendSound(entry.waiting);
      }, SOUND_SEND_MS),
    };
    this.soundSends.set(sound.number, entry);
    this.connection
      .invoke('SetSound', sound)
      .then((kept) => {
        if (kept && this.soundChanges.get(sound.number) === change) this.putSound(kept);
      })
      .catch((error: Error) => {
        // refused: what waits goes too, and the page shows the sound as the desk program has it
        const sending = this.soundSends.get(sound.number);
        if (sending) clearTimeout(sending.timer);
        this.soundSends.delete(sound.number);
        this.say(error.message);
        void this.fetchSounds();
      });
  }

  // One sound in the page's list, in place of the one under its number.
  protected putSound(sound: Sound): void {
    this.sounds.update((all) =>
      [...all.filter((item) => item.number !== sound.number), sound].sort(
        (a, b) => a.number - b.number,
      ),
    );
  }

  // The sounds as the desk program lists them. A sound this page sends right now stays as the page
  // has it: the list may still be from a change before (a slider would jump back for a moment).
  protected takeSounds(sounds: readonly Sound[]): void {
    if (this.soundSends.size === 0) {
      this.sounds.set([...sounds]);
      return;
    }
    const mine = this.soundsByNumber();
    const kept = sounds.filter((sound) => !this.soundSends.has(sound.number));
    for (const number of this.soundSends.keys()) {
      const own = mine.get(number);
      if (own) kept.push(own);
    }
    this.sounds.set(kept.sort((a, b) => a.number - b.number));
  }

  /** Stores a copy of a sound under another number. */
  copySound(from: number, to: number): void {
    this.run('CopySound', from, to);
  }

  /** The key of a sound: brings its fader up in its fade-in time, which starts it, or down in its fade-out time, which stops it. */
  toggleSound(number: number): void {
    if (!this.soundsByNumber().has(number)) {
      this.say(`There is no sound ${number} yet. On the Sounds view, add a file to it.`);
      return;
    }
    this.soundControl(number, 'toggle');
  }

  /** The fader of a sound, 0 to 255: from zero it starts the sound, at zero it stops it. */
  setSoundLevel(number: number, level: number): void {
    this.soundControl(number, 'level', (level / MAX_LEVEL) * 100);
  }

  /**
   * Works the playback of a sound (SetSoundControl in the desk program): "go" and "toggle" are
   * answered with the playback; "level" (per cent) moves like a fader: the page shows it at once and
   * sends the newest only.
   */
  soundControl(number: number, control: SoundControl, value = 0): void {
    if (control === 'level') {
      this.touchSound(number, value);
      this.sendSoon(`sound ${number}`, 'SetSoundControl', number, control, value);
      return;
    }
    this.connection
      .invoke('SetSoundControl', number, control, value)
      .then((playback) => {
        if (playback) this.takeSoundPlaybacks([playback], false);
      })
      .catch((error: Error) => this.say(error.message));
  }

  /** A sound that plays goes on from another place of its file, in seconds: a double click on its wave. */
  seekSound(number: number, seconds: number): void {
    this.connection
      .invoke('SeekSound', number, Math.max(0, seconds))
      .then((playback) => {
        if (playback) this.takeSoundPlaybacks([playback], false);
      })
      .catch((error: Error) => this.say(error.message));
  }

  /** The volume of all sounds, -100 dB to 0 dB, to a tenth of a decibel. */
  setSoundVolume(decibels: number): void {
    const kept = Math.max(MIN_DECIBELS, Math.min(MAX_DECIBELS, Math.round(decibels * 10) / 10)) + 0;
    this.soundDecibels.set(kept);
    this.sendSoon('sound volume', 'SetSoundVolume', kept);
  }

  /** MUTE of the sound: no sound goes out while it is on. */
  toggleSoundMute(): void {
    const mute = !this.soundMute();
    this.soundMute.set(mute);
    this.run('SetSoundMute', mute);
  }

  // The playback shows a moved fader at once; a fader that leaves zero starts the sound and one that
  // comes to zero stops it, as the desk program does.
  protected touchSound(number: number, percent: number): void {
    this.soundTouched.set(number, Date.now());
    const old = this.soundPlaybacks().get(number) ?? quietPlayback(number);
    const level = Math.round((percent * MAX_LEVEL) / 100);
    const playing = level > 0 ? old.playing || this.soundsByNumber().has(number) : false;
    this.soundPlaybacks.update((all) =>
      new Map(all).set(number, { ...old, level, playing, fading: false }),
    );
  }

  // Playbacks from the desk program. A fader this page moved a moment ago keeps the page's value.
  // "whole": the list of every playback; else only these change.
  protected takeSoundPlaybacks(playbacks: readonly SoundPlayback[], whole: boolean): void {
    const now = Date.now();
    const old = this.soundPlaybacks();
    const kept = whole ? new Map<number, SoundPlayback>() : new Map(old);
    let starts: Map<number, number> | null = null;
    for (const playback of playbacks) {
      const mine = old.get(playback.number);
      const recent = now - (this.soundTouched.get(playback.number) ?? 0) < EFFECT_TOUCH_MS;
      kept.set(playback.number, mine && recent ? { ...playback, level: mine.level } : playback);
      if (playback.playing && !mine?.playing) {
        starts ??= new Map(this.soundStarts());
        starts.set(playback.number, ++this.soundStartCount);
      }
    }
    this.soundPlaybacks.set(kept);
    if (starts) this.soundStarts.set(starts);
  }

  protected async fetchSounds(): Promise<void> {
    try {
      this.takeSounds((await this.connection.invoke('GetSounds')) ?? []);
      this.soundsKnown.set(true);
    } catch {
      // A desk program from before sounds. The Sounds view says so.
      this.sounds.set([]);
      this.soundsKnown.set(false);
    }
  }
}
