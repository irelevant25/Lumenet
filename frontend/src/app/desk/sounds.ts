import { LampLevel } from './lamps';

/**
 * Sounds: files (music, a noise, a voice), which the desk program plays on a device of its PC
 * (SoundEngine.cs). Like an effect, a sound has a fader and a key: from zero its fader starts it,
 * at zero it stops; its key brings the fader up in its fade-in time and down in its fade-out time.
 * Each sound has its own level in decibels, -100 dB (nothing) to 0 dB (its file as it is); over all
 * sounds stand the volume of all sounds, in decibels as well, and MUTE. The files go to the desk program
 * over HTTP (POST /api/sounds); a project keeps them in a folder beside its file.
 *
 * A sound can also be a track of the USB stick of a Soundcraft Ui mixer (a sound with a playlist): it
 * plays on the mixer's own player, one at a time, while the mixer answers, beside the files; the
 * mixer's PLAYER L/R are then the volume of all sounds and MUTE, both ways, and the track that plays
 * adds its own level and its fader on top (UiTrackPlayer.cs).
 */

/** The most sounds a project can have. */
export const MAX_SOUNDS = 64;
/** The sound keys of the independents: each starts and stops a sound. */
export const SOUND_INDEPENDENT_COUNT = 32;
/** The longest name. */
export const SOUND_NAME_LENGTH = 24;
/** A level in decibels: from nothing at the bottom to a file as it is at the top (and at first). */
export const MIN_DECIBELS = -100;
export const MAX_DECIBELS = 0;
/** The volume of all sounds at first, and after a double click on its fader: well under full. */
export const DEFAULT_SOUND_DECIBELS = -20;
/** The places of a fader or a slider of a level: 0 at the bottom (nothing) to this at the top (0 dB). */
export const LEVEL_TOP = 255;
/** A sound's speed (its pitch stays) and pitch (its speed stays). */
export const MIN_SOUND_SPEED = 0.5;
export const MAX_SOUND_SPEED = 2;
export const MAX_SOUND_PITCH = 12;
/** The time between the repeats of an echo, and how much of it comes back. */
export const MIN_ECHO_TIME = 0.02;
export const MAX_ECHO_TIME = 2;
export const MAX_ECHO_REPEATS = 90;
/** The kinds of files a sound can be. */
export const SOUND_FILE_ENDINGS = [
  '.mp3',
  '.wav',
  '.aif',
  '.aiff',
  '.flac',
  '.m4a',
  '.aac',
  '.wma',
] as const;

/** A sound as the desk program keeps it (Sound.cs). */
export interface Sound {
  number: number;
  name: string;
  /** The name of its file, like "thunder.mp3". */
  file: string;
  /** What the desk program keeps the file under. */
  hash: string;
  /** How long the file is. */
  seconds: number;
  /** Where it starts and ends in its file, in seconds; no end: to the end of the file. */
  start: number;
  end?: number | null;
  loop: boolean;
  /** Its level, -100 dB (nothing) to 0 dB (its file as it is): its fader at full gives this. */
  decibels: number;
  fadeIn: number;
  fadeOut: number;
  /** -100 all left, 0 in the middle, 100 all right. */
  pan: number;
  /** 0.5 to 2 times; its pitch stays. */
  speed: number;
  /** Semitones up or down; its speed stays. */
  pitch: number;
  /** The echo: how loud (0 for none), the seconds between its repeats, how much comes back. */
  echo: number;
  echoTime: number;
  echoRepeats: number;
  /** The reverb: how much (0 for none), and how big the room. */
  reverb: number;
  room: number;
  /**
   * For a track of the mixer's USB player: the playlist it is in, as the mixer names it. Its name on
   * the stick is then `file`, it has no hash, and of its settings only its level and the fades count.
   */
  playlist?: string | null;
}

/**
 * The decibels at a place of a fader or slider of a level, 0 to LEVEL_TOP: the gain is the place
 * squared (half way is -12 dB), as on a MIDI controller. To a tenth of a decibel.
 */
export function decibelsAt(place: number): number {
  if (place <= 0) return MIN_DECIBELS;
  const decibels = 40 * Math.log10(Math.min(place, LEVEL_TOP) / LEVEL_TOP);
  return Math.max(MIN_DECIBELS, Math.round(decibels * 10) / 10) + 0; // + 0: never "minus 0"
}

/** Where a fader or slider of a level stands for these decibels, 0 to LEVEL_TOP. */
export function placeOf(decibels: number): number {
  if (decibels <= MIN_DECIBELS) return 0;
  return Math.round(LEVEL_TOP * 10 ** (Math.min(decibels, MAX_DECIBELS) / 40));
}

/** Decibels as the page shows them: "0 dB", "-12.5 dB", "-100 dB". */
export function decibelText(decibels: number): string {
  const tenth = Math.round(decibels * 10) / 10 + 0;
  return `${tenth.toFixed(1).replace(/\.0$/, '')} dB`;
}

/** True for a track of the mixer's USB player. */
export function isTrack(sound: Sound | undefined): boolean {
  return typeof sound?.playlist === 'string';
}

/** The playback of a sound (SoundPlayback in the desk program). None of it is stored. */
export interface SoundPlayback {
  number: number;
  /** From its start until it is over or stopped. */
  playing: boolean;
  /** Its fader, 0 to 255. */
  level: number;
  /** True while its key moves its fader. */
  fading: boolean;
  /** How strongly the sequences play it, 0 to 255. */
  played?: number;
  /** Where it is in its file, in seconds. */
  position: number;
  /** True when its file could not be found or played, or its track (no mixer answers, or its stick has no such track). */
  missing?: boolean;
  /** For a track of the mixer: how long it is, in seconds, as the mixer says while it plays; 0 when unknown. */
  length?: number;
}

/** What a sound's playback takes: its key, or its fader. */
export type SoundControl = 'go' | 'toggle' | 'level';

/** A file the desk program took in: what POST /api/sounds answers. */
export interface SoundFileInfo {
  hash: string;
  name: string;
  seconds: number;
  channels: number;
  rate: number;
}

/** How loud a sound's file is along its length, in parts of the same length (SoundWave in the desk program). */
export interface SoundWave {
  /** How long the file is. */
  seconds: number;
  /** The loudest sample of each part, 0 to 255. */
  peaks: Uint8Array;
  /** The mean loudness of each part, 0 to 255. */
  levels: Uint8Array;
}

/** A device of the PC that plays sound. */
export interface SoundDevice {
  id: string;
  name: string;
  channels: number;
}

/** The device of the sound and its pair of outputs (from 1), as the PC keeps them. */
export interface SoundSetup {
  /** Windows' id of the device; empty for Windows' default device. */
  device: string;
  /** The pair of outputs the sounds go out on: 1 for outputs 1 and 2. */
  outputs: number;
  /** True: what goes out plays on the device to listen on as well (LISTEN ON). */
  listen: boolean;
  /** The device to listen on, by Windows' id; empty for Windows' default device. */
  listenDevice: string;
  /** A Soundcraft Ui mixer on the network, by its IP address or name; empty for none. */
  mixer: string;
  /** The playlist of the mixer whose tracks the steps and the Sounds view offer. Empty for all its tracks. */
  playlist: string;
}

/** Whether a mixer is asked for and answers: none, looked for, or there. */
export type MixerState = '' | 'connecting' | 'connected';

/** The Soundcraft Ui mixer of the tracks as every page hears of it (MixerView in the desk program). */
export interface MixerView {
  state: MixerState;
  /** What the mixer is, as it says, like "ui16, firmware 1.0.7548"; empty until it said. */
  name: string;
  /** The playlist chosen in Settings; empty for all the tracks of the stick. Missing from an older desk program. */
  playlist?: string;
}

/** The playlist of every track of a mixer's stick, as the mixer names it. */
export const ALL_TRACKS = '~all~';

/** The playlist whose tracks the steps and the Sounds view offer: the one of Settings, or every track. */
export function tracksPlaylist(mixer: MixerView): string {
  return mixer.playlist || ALL_TRACKS;
}

/** A Soundcraft Ui mixer that the Find key of Settings found: its address and what it says it is. */
export interface FoundMixer {
  address: string;
  name: string;
}

/** LISTEN ON as every page hears of it: on or off, and the state of the device to listen on. */
export interface ListenView {
  listen: boolean;
  /** Empty while it is off; else as SoundOutput.listenState. */
  state: string;
}

/** LISTEN off: what a page knows before the desk program said, or from an older one. */
export const NO_LISTEN: ListenView = { listen: false, state: '' };

/** No mixer: what a page knows before the desk program said, or from an older one. */
export const NO_MIXER: MixerView = { state: '', name: '' };

/** The device of the sound as the desk program tells it (GetSoundOutput). */
export interface SoundOutput extends SoundSetup {
  devices: SoundDevice[];
  /** playing: open; missing: the chosen device is not there; none: the PC has no device; off: no devices (another system, a test desk). */
  state: 'playing' | 'missing' | 'none' | 'off';
  /** The device in use. */
  name: string;
  /** The outputs the sound has now. */
  channels: number;
  /** The device to listen on: empty while listening is off; else as state, or same: it is the device of the sound. */
  listenState: '' | 'playing' | 'missing' | 'none' | 'off' | 'same';
  /** The device listened on. */
  listenName: string;
  /** The mixer: none asked for, looked for, or there. Missing from an older desk program. */
  mixerState?: MixerState;
  /** What the mixer is, as it says. */
  mixerName?: string;
}

/** A new sound of a file: named after the file, as it is. */
export function newSound(number: number, file: SoundFileInfo): Sound {
  const dot = file.name.lastIndexOf('.');
  const name = (dot > 0 ? file.name.slice(0, dot) : file.name).slice(0, SOUND_NAME_LENGTH);
  return {
    number,
    name,
    file: file.name,
    hash: file.hash,
    seconds: file.seconds,
    start: 0,
    end: null,
    loop: false,
    decibels: 0,
    fadeIn: 0,
    fadeOut: 0,
    pan: 0,
    speed: 1,
    pitch: 0,
    echo: 0,
    echoTime: 0.3,
    echoRepeats: 40,
    reverb: 0,
    room: 50,
  };
}

/**
 * A new sound of a track of the mixer: named after the track without its ending (in the usual form
 * of its letters, cut to the longest name), played as it is. The playlist and the track's name stay
 * exactly as the mixer gave them: they go back to it so.
 */
export function newTrack(number: number, playlist: string, track: string): Sound {
  const dot = track.lastIndexOf('.');
  const name = (dot > 0 ? track.slice(0, dot) : track).normalize('NFC').slice(0, SOUND_NAME_LENGTH);
  return {
    ...newSound(number, { hash: '', name: track, seconds: 0, channels: 2, rate: 0 }),
    name,
    playlist,
  };
}

/** A track's name as the page shows it: in the usual form of its letters. */
export function trackText(sound: Sound): string {
  return `${sound.playlist ?? ''} / ${sound.file.normalize('NFC')}`;
}

/** The lamp of a sound's key: faint where there is no sound, half lit for one that is quiet, full while it plays. */
export function soundLamp(
  sound: Sound | undefined,
  playback: SoundPlayback | undefined,
): LampLevel {
  if (!sound) return 1;
  return playback?.playing ? 3 : 2;
}

/**
 * The lamp of the key LISTEN: faint while it is off, full while what goes out plays on the device to
 * listen on, half lit while that device is not there (or is the device of the sound itself).
 */
export function listenLamp(listen: ListenView): LampLevel {
  if (!listen.listen) return 1;
  return listen.state === 'playing' ? 3 : 2;
}

/** Seconds as the page shows them: "3:05.2", or "12.5 s" under a minute. */
export function timeText(seconds: number): string {
  if (seconds < 60) return `${seconds.toFixed(1)} s`;
  const minutes = Math.floor(seconds / 60);
  const rest = seconds - minutes * 60;
  return `${minutes}:${rest.toFixed(1).padStart(4, '0')}`;
}

/** The playback of a sound that has never played: quiet, its fader at zero. */
export function quietPlayback(number: number): SoundPlayback {
  return {
    number,
    playing: false,
    level: 0,
    fading: false,
    played: 0,
    position: 0,
    missing: false,
  };
}
