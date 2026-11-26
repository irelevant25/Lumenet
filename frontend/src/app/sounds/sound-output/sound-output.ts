import { Component, computed, effect, inject, signal } from '@angular/core';
import { DeskConnection } from '../../desk/desk-connection';
import { DeskStore } from '../../desk/desk-store';
import { ALL_TRACKS, FoundMixer, SoundOutput, SoundSetup } from '../sounds';

/**
 * The Sound part of Settings (in the panel of the views in the top bar): the device the desk program
 * plays the sounds on, and its pair of outputs, 1-2, 3-4 and so on: one device at a time, chosen on the
 * PC the desk program runs on (a page elsewhere only says where it is chosen). Windows' default device
 * follows Windows; a device chosen that is not there plays nothing until it is back.
 *
 * Listen on: the device on which what goes out (to a mixing desk, say) plays once more, in stereo, while
 * the key LISTEN of the sound section is lit: headphones, the speakers of the PC. Not on the device of the
 * sound itself, which plays it already.
 *
 * Mixer: a Soundcraft Ui mixer on the network, by its address, typed or found by the key Find. While
 * it answers, the tracks of its USB stick play on its own player, one at a time, beside the files, and
 * PLAYER L/R are the volume of all sounds and MUTE (UiTrackPlayer.cs). Its playlist is the one whose
 * tracks the steps and the Sounds view offer (every track while none is chosen); Add all its tracks
 * makes each of them a sound. The outputs show only for a device of more than two.
 */
@Component({
  selector: 'lm-sound-output',
  styleUrl: './sound-output.css',
  templateUrl: './sound-output.html',
})
export class SoundOutputPanel {
  protected readonly store = inject(DeskStore);
  private readonly connection = inject(DeskConnection);

  /** The playlists of the mixer's stick, once it answers; and whether they are being asked for. */
  protected readonly playlists = signal<string[]>([]);
  private readonly listing = signal(false);
  /** True while the tracks of the playlist become sounds. */
  protected readonly adding = signal(false);

  /** True while the networks are looked through for mixers (the key Find). */
  protected readonly finding = signal(false);
  /** The mixers the last search found; null before one. */
  protected readonly found = signal<FoundMixer[] | null>(null);

  /** What the last search says, when it found none or several (one is taken at once). */
  protected readonly findLine = computed(() => {
    const found = this.found();
    if (!found || found.length === 1) return '';
    return found.length === 0
      ? 'No Soundcraft Ui mixer answered in the networks of this PC. Its address can be typed instead.'
      : 'Several mixers answered: choose one.';
  });

  /** What the desk program said last; null on a page that is not on the PC (or a desk program without sound). */
  protected readonly output = signal<SoundOutput | null>(null);
  /** False until the first answer came. */
  protected readonly asked = signal(false);

  /** The pairs of outputs of the device in use: "1 and 2", "3 and 4" ... */
  protected readonly pairs = computed(() => {
    const channels = Math.max(2, this.output()?.channels ?? 2);
    return Array.from({ length: Math.floor(channels / 2) }, (_, index) => ({
      value: index + 1,
      text: `${index * 2 + 1} and ${index * 2 + 2} (of ${channels})`,
    }));
  });

  /** True when the device to listen on that was chosen is not among the devices of the PC. */
  protected readonly listenGone = computed(() => {
    const output = this.output();
    return (
      !!output?.listenDevice && !output.devices.some((device) => device.id === output.listenDevice)
    );
  });

  /** One sentence about listening, while it is on; empty while it is off. */
  protected readonly listening = computed(() => {
    const output = this.output();
    if (!output?.listen) return '';
    switch (output.listenState) {
      case 'playing':
        return `Listening on ${output.listenName}.`;
      case 'missing':
        return 'The device to listen on is not there.';
      case 'same':
        return 'The device to listen on is the device of the sound: it plays there already.';
      case 'none':
        return 'This PC has no device to listen on.';
      default:
        return '';
    }
  });

  /** One sentence about the mixer; empty while none is asked for. */
  protected readonly mixerLine = computed(() => {
    const output = this.output();
    if (!output?.mixer) return '';
    switch (output.mixerState) {
      case 'connected':
        return `The mixer answers${output.mixerName ? `: ${output.mixerName}` : ''}.`;
      case 'connecting':
        return `Looking for the mixer at ${output.mixer}: no track plays until it answers.`;
      default:
        return '';
    }
  });

  /** One sentence about the device. */
  protected readonly state = computed(() => {
    const output = this.output();
    if (!output) return '';
    switch (output.state) {
      case 'playing':
        return `Plays on ${output.name}, ${output.channels} outputs.`;
      case 'missing':
        return 'The device chosen is not there: no sound goes out until it is back.';
      case 'none':
        return 'This PC has no device that plays sound.';
      default:
        return 'This desk program plays no sound (a desk for tests, or a system without Windows).';
    }
  });

  constructor() {
    void this.ask();
    // the playlists, once the mixer answers
    effect(() => {
      if (this.output()?.mixerState === 'connected') void this.listPlaylists();
      else this.playlists.set([]);
    });
    // what another page on the PC chose, or a device that came or went
    effect(() => {
      const news = this.store.soundOutputNews();
      if (news) this.output.set(news);
    });
  }

  protected async onDevice(event: Event): Promise<void> {
    await this.change({ device: (event.target as HTMLSelectElement).value });
  }

  protected async onOutputs(event: Event): Promise<void> {
    await this.change({ outputs: Number((event.target as HTMLSelectElement).value) });
  }

  protected async onListenDevice(event: Event): Promise<void> {
    await this.change({ listenDevice: (event.target as HTMLSelectElement).value });
  }

  protected async onMixer(event: Event): Promise<void> {
    await this.change({ mixer: (event.target as HTMLInputElement).value.trim() });
  }

  /** The key Find: the networks of the PC are looked through; one mixer found is taken at once. */
  protected async find(): Promise<void> {
    if (this.finding()) return;
    this.finding.set(true);
    this.found.set(null);
    try {
      const found = await this.store.findMixers();
      this.found.set(found);
      if (found.length === 1) await this.change({ mixer: found[0].address });
    } finally {
      this.finding.set(false);
    }
  }

  /** One of several mixers found, chosen. */
  protected async useMixer(address: string): Promise<void> {
    this.found.set(null);
    await this.change({ mixer: address });
  }

  protected async onPlaylist(event: Event): Promise<void> {
    await this.change({ playlist: (event.target as HTMLSelectElement).value });
  }

  /** Every track of the playlist (or of the stick) that is no sound yet becomes one, in the free numbers. */
  protected async addTracks(playlist: string): Promise<void> {
    this.adding.set(true);
    try {
      await this.store.addMixerTracks(playlist);
    } finally {
      this.adding.set(false);
    }
  }

  /** A playlist as the page shows it: the mixer's own two by what they hold. */
  protected playlistText(name: string): string {
    if (name === '~all~') return 'All tracks';
    if (name === '~root~') return 'Tracks outside the folders';
    return name.normalize('NFC');
  }

  /** Enter in the field of the mixer takes it, as leaving it does. */
  protected leave(event: Event): void {
    (event.target as HTMLInputElement).blur();
  }

  private async listPlaylists(): Promise<void> {
    if (this.listing() || this.playlists().length > 0) return;
    this.listing.set(true);
    try {
      // every track is the first line of the list
      this.playlists.set((await this.store.mixerPlaylists()).filter((name) => name !== ALL_TRACKS));
    } finally {
      this.listing.set(false);
    }
  }

  // The desk program answers only a page on its own PC; another page, or a desk program from before
  // sounds, gets none.
  private async ask(): Promise<void> {
    try {
      this.output.set(
        (await this.connection.invoke('GetSoundOutput')) ?? null,
      );
    } catch {
      this.output.set(null);
    }
    this.asked.set(true);
  }

  private async change(part: Partial<SoundSetup>): Promise<void> {
    const old = this.output();
    if (!old) return;
    const setup: SoundSetup = {
      device: old.device,
      outputs: old.outputs,
      listen: old.listen ?? false,
      listenDevice: old.listenDevice ?? '',
      mixer: old.mixer ?? '',
      playlist: old.playlist ?? '',
      ...part,
    };
    try {
      this.output.set(
        (await this.connection.invoke('SetSoundOutput', setup)) ?? old,
      );
    } catch (error) {
      this.store.tell((error as Error).message);
      this.output.set({ ...old }); // the fields show what is in use again
    }
  }
}
