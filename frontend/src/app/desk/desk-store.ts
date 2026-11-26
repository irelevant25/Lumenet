import { effect, Service, untracked } from '@angular/core';
import { DEFAULT_BPM } from './tempo';
import { DeskState, MEMORY_COUNT, NO_SOLO } from './desk-types';
import { NO_LISTEN, NO_MIXER, tracksPlaylist } from '../sounds/sounds';
import { allFalse, withItem, zeros } from './store/desk-store-helpers';
import { DeskStoreProject } from './store/desk-store-project';

/**
 * The state of the desk as this page sees it, and everything the page can do with it.
 *
 * The backend holds the real state. The page changes its own copy at once, so that
 * faders feel direct, and tells the backend. Changes made elsewhere come back as messages.
 *
 * DeskStore is built in layers, one file each in desk/store, the way the desk program's Desk is one
 * class in several files: state, faders, effects, sounds, solo, stack and project, each extending the
 * one before. Here on top: the messages of the backend, and what it sends when the page connects.
 */
@Service()
export class DeskStore extends DeskStoreProject {
  constructor() {
    super();
    const connection = this.connection;

    connection.on('ChannelChanged', (channel, level) => {
      this.channels.update((levels) => withItem(levels, channel - 1, level));
      this.claimChannel(channel);
    });
    connection.on('MemoryLevelChanged', (memory, level) => {
      this.claimMemory(memory, level);
      this.memoryLevels.update((levels) => withItem(levels, memory - 1, level));
    });
    connection.on('ChannelNoteChanged', (channel, text) =>
      this.channelNotes.update((notes) => withItem(notes, channel - 1, text)),
    );
    connection.on('MemoryNoteChanged', (memory, text) =>
      this.memoryNotes.update((notes) => withItem(notes, memory - 1, text)),
    );
    connection.on('IndependentChanged', (key, channel) =>
      this.independents.update((channels) => withItem(channels, key - 1, channel)),
    );
    connection.on('MemoryIndependentChanged', (key, memory) =>
      this.memoryIndependents.update((memories) => withItem(memories, key - 1, memory)),
    );
    connection.on('MasterChanged', (level) => this.master.set(level));
    connection.on('BlackoutChanged', (on) => this.blackout.set(on));
    connection.on('DeskChanged', (desk) => this.takeDesk(desk));
    connection.on('BoxesChanged', (boxes) => this.boxes.set(boxes));
    connection.on('OutputsChanged', (outputs) => this.outputs.set(outputs));
    connection.on('ProjectChanged', (project) => this.project.set(project));
    connection.on('StackChanged', (stack) => this.stack.set(stack));
    connection.on('PlaybackChanged', (playback) => this.takePlayback(playback));
    connection.on('LanAccessChanged', (lan) => this.lan.set(lan));
    connection.on('HistoryChanged', (history) => this.history.set(history));
    connection.on('RateChanged', (on, bpm) => {
      this.rateOn.set(on);
      this.bpm.set(bpm);
    });
    connection.on('AutosaveChanged', (options) => this.autosave.set(options));
    connection.on('StageChanged', (plan) => this.takeStage(plan));
    connection.on('SoloChanged', (solo) => this.solo.set(solo));
    connection.on('NotesChanged', (notes) => this.notes.set(notes ?? ''));
    connection.on('SequenceSpeedChanged', (memory, speed) =>
      this.sequences.update((all) =>
        all[memory] ? { ...all, [memory]: { ...all[memory], speed } } : all,
      ),
    );
    connection.on('EffectsChanged', (effects) => this.effects.set(effects ?? []));
    connection.on('EffectPlaybackChanged', (playback) =>
      this.takeEffectPlaybacks([playback], false),
    );
    connection.on('SoundsChanged', (sounds) => this.takeSounds(sounds ?? []));
    connection.on('SoundPlaybackChanged', (playback) => this.takeSoundPlaybacks([playback], false));
    connection.on('SoundMasterChanged', (decibels, mute) => {
      this.soundDecibels.set(decibels);
      this.soundMute.set(mute);
    });
    connection.on('SoundOutputChanged', (output) => this.soundOutputNews.set(output));
    connection.on('MixerChanged', (mixer) => this.mixer.set(mixer ?? NO_MIXER));
    connection.on('SoundListenChanged', (listen) => this.listen.set(listen ?? NO_LISTEN));
    // the tracks of the playlist chosen in Settings (or every track), once the mixer answers
    effect(() => {
      const mixer = this.mixer();
      const playlist = mixer.state === 'connected' ? tracksPlaylist(mixer) : '';
      untracked(() => void this.askPlaylistTracks(playlist));
    });
    connection.on('SoundIndependentChanged', (key, sound) =>
      this.soundIndependents.update((sounds) => withItem(sounds, key - 1, sound)),
    );
    connection.on('EffectIndependentChanged', (key, effect) =>
      this.effectIndependents.update((effects) => withItem(effects, key - 1, effect)),
    );

    connection.whenConnected(() => void this.refresh());
    connection.start();

    this.followWindowSize();
  }

  /** Fetches the whole state. Runs on every connect, so a reconnect cannot leave the page out of date. */
  protected async refresh(): Promise<void> {
    try {
      const welcome = await this.connection.invoke('Hello');
      this.version.set(welcome.version);
      this.takeDesk(welcome.desk);
      this.boxes.set(welcome.boxes ?? []);
      this.outputs.set(welcome.outputs ?? []);
      this.mixer.set(welcome.mixer ?? NO_MIXER);
      this.listen.set(welcome.listen ?? NO_LISTEN);
      this.project.set(welcome.project);
      this.loaded.set(true);
      void this.fetchLan();
      void this.fetchHistory();
      void this.fetchAutosave();
      void this.tellRecovered();
    } catch {
      // The connection dropped again before the answer arrived. The next connect asks again.
    }
  }

  protected takeDesk(desk: DeskState): void {
    this.universes.set(desk.universes ?? 1);
    this.channels.set(desk.channels);
    this.memoryLevels.set(desk.memoryLevels);
    this.memoryStored.set(desk.memoryStored);
    this.memorySequence.set(desk.memorySequence ?? allFalse(MEMORY_COUNT));
    // An older desk program does not know notes or independent keys and leaves them out.
    // Then the page keeps what it has, instead of falling back to empty notes and keys 1 to 16.
    if (desk.channelNotes) this.channelNotes.set(desk.channelNotes);
    if (desk.memoryNotes) this.memoryNotes.set(desk.memoryNotes);
    if (desk.independents) this.independents.set(desk.independents);
    if (desk.memoryIndependents) this.memoryIndependents.set(desk.memoryIndependents);
    if (desk.effectIndependents) this.effectIndependents.set(desk.effectIndependents);
    if (desk.soundIndependents) this.soundIndependents.set(desk.soundIndependents);
    if (desk.effects) this.takeEffectPlaybacks(desk.effects, true);
    if (desk.sounds) this.takeSoundPlaybacks(desk.sounds, true);
    if (desk.soundDecibels !== undefined) this.soundDecibels.set(desk.soundDecibels);
    if (desk.soundMute !== undefined) this.soundMute.set(desk.soundMute);
    this.master.set(desk.master);
    this.blackout.set(desk.blackout);
    this.bpm.set(desk.bpm ?? DEFAULT_BPM);
    this.rateOn.set(desk.rateOn ?? false);
    this.solo.set(desk.solo ?? NO_SOLO);
    // The order in which faders took the attributes of the lights; the page counts on from there.
    const channelClaims = desk.channelClaims ?? zeros(desk.channels.length);
    const memoryClaims = desk.memoryClaims ?? zeros(MEMORY_COUNT);
    this.channelClaims.set(channelClaims);
    this.memoryClaims.set(memoryClaims);
    this.claimCount = Math.max(this.claimCount, ...channelClaims, ...memoryClaims);
    // The whole desk comes when a memory was stored, copied or emptied, or a project loaded:
    // exactly when a look, a sequence or the stack may be another one.
    void this.fetchMemoryLooks();
    void this.fetchSequencesAndStack();
    void this.fetchStage();
    void this.fetchNotes();
    void this.fetchEffects();
    void this.fetchSounds();
  }
}
