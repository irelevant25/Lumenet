/*
 * MIDI controllers on the PC the desk program runs on: their keys press the desk's keys, their
 * faders move its faders. The desk program opens the devices and does what the controls are mapped
 * to; the desk's page on the PC that was used last presses the keys (as its keyboard shortcuts
 * would) and tells the desk program how its keys are lit, for the lights of the controllers.
 *
 * A control works a target by its name: a key by the name of desk-actions.ts ("memory.5"), a fader
 * by a name of midi-targets.ts ("fader.channel.12", "fader.slot.memory.3"). OSC is to use the same
 * names. See DmxBox.Server/ControlTarget.cs.
 */

/** The kinds of MIDI messages a control can be. */
export type MidiMessageKind = 'note' | 'cc' | 'cc14' | 'pitchbend' | 'program';

/** How a controller (CC) moves a fader: where it stands, or the ticks of an endless encoder. */
export type MidiMode = 'absolute' | 'relative-twos' | 'relative-offset' | 'relative-sign';

/** One state of a light: a value, and for a controller that takes it from the channel, the channel (0: the control's own). */
export interface MidiLight {
  readonly value: number;
  readonly channel: number;
}

/** How a key's light is sent for each state of the desk's lamp; no blink: the desk blinks it. */
export interface MidiLights {
  readonly off: MidiLight;
  readonly half: MidiLight;
  readonly full: MidiLight;
  readonly blink?: MidiLight | null;
  /** The note or controller the light is sent on, when it is not the key's own. */
  readonly number?: number | null;
}

/** What one control of a controller does, as the desk program keeps it (MidiMapping). */
export interface MidiMapping {
  readonly device: string;
  readonly channel: number;
  readonly message: MidiMessageKind;
  readonly number: number;
  readonly target: string;
  readonly mode: MidiMode;
  /** For a fader: it takes over only once the control reaches the desk's value. */
  readonly pickup: boolean;
  /** The desk sends back what it has: a fader's value (a motor, a ring of lights), a key's lamp (its light). */
  readonly feedback: boolean;
  readonly lights?: MidiLights | null;
}

/** A MIDI device as the list of devices shows it. */
export interface MidiDeviceView {
  readonly name: string;
  readonly input: boolean;
  readonly output: boolean;
  readonly used: boolean;
  /** "open", "missing" (used, not plugged in), "failed" (cannot be opened: problem), "unused". */
  readonly state: 'open' | 'missing' | 'failed' | 'unused';
  readonly problem: string | null;
  readonly simulated: boolean;
}

/** A ready-made mapping of a controller. */
export interface MidiPresetView {
  readonly id: string;
  readonly name: string;
  /** The name the controller usually has on a PC. */
  readonly device: string;
  readonly about: string;
}

/** The MIDI of the PC. */
export interface MidiState {
  /** True on a system with MIDI devices for the desk (Windows, macOS); simulators work everywhere. */
  readonly works: boolean;
  readonly devices: readonly MidiDeviceView[];
  readonly mappings: readonly MidiMapping[];
  /** The target the next control that moves is to work; null while none. */
  readonly learning: string | null;
  readonly presets: readonly MidiPresetView[];
}

/** The MIDI setup of the PC as Export and Import carry it. */
export interface MidiSetup {
  readonly devices: readonly string[];
  readonly mappings: readonly MidiMapping[];
}

/** One message that came in, and what became of it. */
export interface MidiMonitorLine {
  /** Milliseconds since 1970. */
  readonly at: number;
  readonly device: string;
  readonly message: string;
  readonly target: string | null;
  readonly result: string;
  readonly value: number | null;
  readonly desk: number | null;
  readonly reason: string | null;
}

/** Whether this page presses the keys of the controllers, and the keys whose lamps the desk program needs. */
export interface MidiRole {
  readonly controls: boolean;
  readonly keys: readonly string[];
}

/** The lamp of a key, for the lights of the controllers. */
export interface MidiLamp {
  readonly key: string;
  readonly level: 1 | 2 | 3;
  readonly blink: boolean;
}

/** The pages of faders the page shows: the slots of the controllers follow them. */
export interface MidiPages {
  readonly firstChannel: number;
  readonly channelsPerPage: number;
  readonly firstMemory: number;
  readonly memoriesPerPage: number;
  readonly firstEffect: number;
  readonly effectsPerPage: number;
}
