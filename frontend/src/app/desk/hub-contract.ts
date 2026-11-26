// The contract of the desk program's hub (/hub/desk), WRITTEN BY THE DESK PROGRAM'S TESTS: do not
// change it by hand. After a change of the hub or of what goes over it, run
//   dotnet test backend/DmxBox.sln --filter TypeScriptContract -e LUMENET_WRITE_CONTRACT=1
// and the page's build says where the page has to follow. A type named with "Sent" at its end
// is the form a page sends: it may leave out what the desk program fills in by itself.

/** The methods a page calls on the hub, with their parameters and their answers. */
export interface DeskHubMethods {
  AddMixerTracks(playlist: string): number;
  AddSequenceStep(memory: number, step: StepSent): void;
  AddStackStep(step: StepSent): number;
  AddStackStepAfter(afterId: number, step: StepSent): number;
  ClaimMidi(): void;
  Clear(): void;
  CopyEffect(from: number, to: number): void;
  CopyMemory(from: number, to: number): void;
  CopySound(from: number, to: number): void;
  DeleteEffect(number: number): void;
  DeleteMemory(memory: number): void;
  DeleteSound(number: number): void;
  DeleteStackStep(id: number): void;
  ExportProject(): string;
  FindMixers(): FoundMixer[];
  GetAutosave(): AutosaveOptions;
  GetBoxes(): BoxView[];
  GetEffects(): Effect[];
  GetHistory(): HistoryState;
  GetLanAccess(): LanState;
  GetMemories(): Record<number, number[]>;
  GetMidi(): MidiState;
  GetMidiSetup(): MidiSetup;
  GetMixerPlaylists(): string[];
  GetMixerTracks(playlist: string): string[];
  GetNetworks(): NetworkChoice[];
  GetNotes(): string;
  GetOutputs(): OutputView[];
  GetPlayback(): Playback;
  GetProjects(): ProjectList;
  GetRecovered(): string | null;
  GetSequences(): Record<number, Sequence>;
  GetSerialPorts(): string[];
  GetSoundOutput(): SoundOutputView;
  GetSounds(): Sound[];
  GetStack(): StackState;
  GetStage(): StagePlan;
  GetStagePicture(): string | null;
  Hello(): HelloAnswer;
  HoldSolo(up: boolean, target: string, number: number, held: boolean): void;
  ImportProject(fileName: string, text: string): ImportAnswer;
  ImportProjectFile(name: string, text: string): ProjectInfo;
  ImportStage(text: string): void;
  KeepEffect(number: number): void;
  LearnMidi(target: string | null): MidiState;
  LoadExample(name: string): ProjectInfo;
  LoadMemory(memory: number): void;
  LoadProject(name: string): ProjectInfo;
  MoveStackStep(id: number, afterId: number | null): void;
  NewLanPassword(): void;
  NewProject(): ProjectInfo;
  PreviewEffect(effect: EffectSent): EffectPreview;
  Redo(): string;
  RenumberStackStep(id: number, cue: string): void;
  ReportMidiLamps(lamps: readonly MidiLamp[]): void;
  ReportMidiPages(pages: MidiPages): void;
  SaveMemory(memory: number): void;
  SaveProject(name: string | null): ProjectInfo;
  SeekSound(number: number, seconds: number): SoundPlayback;
  SetAutosave(on: boolean, seconds: number): AutosaveOptions;
  SetBlackout(on: boolean): void;
  SetBoxes(boxes: readonly BoxPlan[]): void;
  SetChannel(channel: number, value: number): void;
  SetChannelNote(channel: number, text: string): void;
  SetEffect(effect: EffectSent): Effect;
  SetEffectControl(number: number, control: string, value: number): EffectPlayback;
  SetEffectIndependent(index: number, effect: number): void;
  SetIndependent(index: number, channel: number): void;
  SetLanAccess(on: boolean): void;
  SetLanAddress(address: string | null): void;
  SetLanPassword(password: string): void;
  SetMaster(value: number): void;
  SetMemoryChannel(memory: number, channel: number, value: number): void;
  SetMemoryIndependent(index: number, memory: number): void;
  SetMemoryLevel(memory: number, value: number): void;
  SetMemoryNote(memory: number, text: string): void;
  SetMidiDevice(name: string, use: boolean): MidiState;
  SetMidiMappings(mappings: readonly MidiMappingSent[]): MidiState;
  SetMidiSetup(setup: MidiSetupSent): MidiState;
  SetNotes(notes: string): void;
  SetOsc(on: boolean, port: number): MidiState;
  SetOutputs(outputs: readonly OutputPlanSent[]): void;
  SetRate(on: boolean, bpm: number): void;
  SetSequence(memory: number, sequence: SequenceSent): void;
  SetSequenceSpeed(memory: number, speed: number): void;
  SetSequenceStepDisabled(memory: number, step: number, disabled: boolean): void;
  SetSound(sound: SoundSent): Sound;
  SetSoundControl(number: number, control: string, value: number): SoundPlayback;
  SetSoundIndependent(index: number, sound: number): void;
  SetSoundListen(listen: boolean): ListenView;
  SetSoundMute(mute: boolean): void;
  SetSoundOutput(setup: SoundSetupSent): SoundOutputView;
  SetSoundVolume(decibels: number): void;
  SetStackLearning(on: boolean): void;
  SetStackLive(on: boolean): void;
  SetStackStepAt(id: number, at: SoundTime | null): void;
  SetStackStepDisabled(id: number, disabled: boolean): void;
  SetStage(plan: StagePlanSent): void;
  SetStagePicture(picture: string | null): void;
  SetUniverses(universes: number): void;
  StackNext(): void;
  StackPause(): void;
  StackPlay(): void;
  StackPrevious(): void;
  Undo(): string;
  UpdateStackStep(id: number, step: StepSent): void;
  UseMidiPreset(preset: string, device: string): MidiState;
  WatchMidi(on: boolean): MidiMonitorLine[];
}

/** The methods that answer as a stream: the page takes their last item. */
export interface DeskHubStreams {
  FindArtNetNodes(): FoundNode[];
}

/** What the desk program sends to the pages on its own (IDeskClient). */
export interface DeskClientEvents {
  AutosaveChanged(options: AutosaveOptions): void;
  BlackoutChanged(on: boolean): void;
  BoxesChanged(boxes: BoxView[]): void;
  ChannelChanged(channel: number, value: number): void;
  ChannelNoteChanged(channel: number, text: string): void;
  DeskChanged(desk: DeskState): void;
  EffectIndependentChanged(index: number, effect: number): void;
  EffectPlaybackChanged(playback: EffectPlayback): void;
  EffectsChanged(effects: Effect[]): void;
  HistoryChanged(history: HistoryState): void;
  IndependentChanged(index: number, channel: number): void;
  LanAccessChanged(lan: LanState): void;
  MasterChanged(value: number): void;
  MemoryIndependentChanged(index: number, memory: number): void;
  MemoryLevelChanged(memory: number, value: number): void;
  MemoryNoteChanged(memory: number, text: string): void;
  MidiChanged(state: MidiState): void;
  MidiKey(key: string, down: boolean): void;
  MidiMessages(lines: MidiMonitorLine[]): void;
  MidiRole(role: MidiRole): void;
  MixerChanged(mixer: MixerView): void;
  NotesChanged(notes: string): void;
  OutputsChanged(outputs: OutputView[]): void;
  PlaybackChanged(playback: Playback): void;
  ProjectChanged(project: ProjectInfo): void;
  RateChanged(on: boolean, bpm: number): void;
  SequenceSpeedChanged(memory: number, speed: number): void;
  SoloChanged(solo: SoloState): void;
  SoundIndependentChanged(index: number, sound: number): void;
  SoundListenChanged(listen: ListenView): void;
  SoundMasterChanged(decibels: number, mute: boolean): void;
  SoundOutputChanged(output: SoundOutputView): void;
  SoundPlaybackChanged(playback: SoundPlayback): void;
  SoundsChanged(sounds: Sound[]): void;
  StackChanged(stack: StackState): void;
  StageChanged(plan: StagePlan): void;
}

export interface AutosaveOptions {
  readonly on: boolean;
  readonly seconds: number;
}

export interface BoxPlan {
  readonly address: string;
  readonly universes: number[];
}

export interface BoxView {
  readonly address: string;
  readonly connected: boolean;
  readonly firmware: string | null;
  readonly outputs: number;
  readonly universes: number[];
  readonly kind: 'automatic' | 'listed' | 'new';
}

export interface DeskState {
  readonly channels: number[];
  readonly memoryLevels: number[];
  readonly memoryStored: boolean[];
  readonly master: number;
  readonly blackout: boolean;
  readonly channelNotes: string[];
  readonly memoryNotes: string[];
  readonly independents: number[];
  readonly memorySequence: boolean[];
  readonly memoryIndependents: number[];
  readonly bpm: number;
  readonly rateOn: boolean;
  readonly solo: SoloState;
  readonly channelClaims: number[];
  readonly memoryClaims: number[];
  readonly universes: number;
  readonly effectIndependents: number[];
  readonly soundIndependents: number[];
  readonly effects: EffectPlayback[];
  readonly sounds: SoundPlayback[];
  readonly soundDecibels: number;
  readonly soundMute: boolean;
}

export interface Effect {
  readonly number: number;
  readonly name: string;
  readonly kind: 'movement' | 'intensity' | 'color' | 'pixel';
  readonly shape: string;
  readonly group: number | null;
  readonly lights: number[];
  readonly speed: number;
  readonly size: number;
  readonly spread: number;
  readonly direction: 'forward' | 'back';
  readonly order: 'address' | 'left' | 'center' | 'random';
  readonly blocks: number;
  readonly wings: number;
  readonly fadeIn: number;
  readonly fadeOut: number;
  readonly duty: number;
  readonly colors: string[];
  readonly centerPan: number | null;
  readonly centerTilt: number | null;
  readonly angle: number;
  readonly pointX: number | null;
  readonly pointY: number | null;
  readonly seed: number;
}

export interface EffectLevels {
  readonly channels: number[];
  readonly levels: number[];
}

export interface EffectPlayback {
  readonly number: number;
  readonly running: boolean;
  readonly level: number;
  readonly speed: number;
  readonly size: number;
  readonly spread: number;
  readonly played: number;
  readonly fading: boolean;
  readonly phase: number;
  readonly bpm: number;
  readonly changed: boolean;
}

export interface EffectPreview {
  readonly kind: 'movement' | 'intensity' | 'color' | 'pixel';
  readonly frames: number;
  readonly items: EffectPreviewItem[];
}

export interface EffectPreviewItem {
  readonly light: number;
  readonly cell: number;
  readonly x: number;
  readonly y: number;
  readonly pan: number[];
  readonly tilt: number[];
  readonly levels: number[];
  readonly colors: string[];
}

export interface EffectSent {
  readonly number: number;
  readonly name: string;
  readonly kind: 'movement' | 'intensity' | 'color' | 'pixel';
  readonly shape: string;
  readonly group?: number | null;
  readonly lights?: readonly number[];
  readonly speed?: number;
  readonly size?: number;
  readonly spread?: number;
  readonly direction?: 'forward' | 'back';
  readonly order?: 'address' | 'left' | 'center' | 'random';
  readonly blocks?: number;
  readonly wings?: number;
  readonly fadeIn?: number;
  readonly fadeOut?: number;
  readonly duty?: number;
  readonly colors?: readonly string[];
  readonly centerPan?: number | null;
  readonly centerTilt?: number | null;
  readonly angle?: number;
  readonly pointX?: number | null;
  readonly pointY?: number | null;
  readonly seed?: number;
}

export interface FixtureChannel {
  readonly name: string;
  readonly code: string;
  readonly fine: number | null;
  readonly ranges: FixtureRange[];
}

export interface FixtureChannelSent {
  readonly name: string;
  readonly code: string;
  readonly fine?: number | null;
  readonly ranges?: readonly FixtureRangeSent[];
}

export interface FixtureRange {
  readonly from: number;
  readonly to: number;
  readonly label: string;
  readonly color: string | null;
}

export interface FixtureRangeSent {
  readonly from: number;
  readonly to: number;
  readonly label: string;
  readonly color?: string | null;
}

export interface FixtureType {
  readonly id: number;
  readonly maker: string;
  readonly model: string;
  readonly mode: string;
  readonly source: string;
  readonly channels: FixtureChannel[];
  readonly codes: string;
}

export interface FixtureTypeSent {
  readonly id: number;
  readonly maker: string;
  readonly model: string;
  readonly mode: string;
  readonly source: string;
  readonly channels: readonly FixtureChannelSent[];
  readonly codes?: string;
}

export interface FoundMixer {
  readonly address: string;
  readonly name: string;
}

export interface FoundNode {
  readonly ip: string;
  readonly shortName: string;
  readonly longName: string;
  readonly firmware: string;
  readonly ports: number[];
  readonly dmxbox: boolean;
}

export interface HelloAnswer {
  readonly version: string;
  readonly desk: DeskState;
  readonly boxes: BoxView[];
  readonly project: ProjectInfo;
  readonly outputs: OutputView[];
  readonly mixer: MixerView;
  readonly listen: ListenView;
}

export interface HistoryState {
  readonly undo: string | null;
  readonly redo: string | null;
}

export interface ImportAnswer {
  readonly project: ProjectInfo;
  readonly report: string[];
}

export interface LanState {
  readonly local: boolean;
  readonly on: boolean;
  readonly addresses: string[];
  readonly password: string | null;
  readonly chosen: string | null;
}

export interface LightGroup {
  readonly id: number;
  readonly name: string;
  readonly lights: number[];
  readonly note: string;
}

export interface LightGroupSent {
  readonly id: number;
  readonly name: string;
  readonly lights: readonly number[];
  readonly note?: string;
}

export interface ListenView {
  readonly listen: boolean;
  readonly state: '' | 'playing' | 'missing' | 'none' | 'off' | 'same';
}

export interface MidiDeviceView {
  readonly name: string;
  readonly input: boolean;
  readonly output: boolean;
  readonly used: boolean;
  readonly state: 'open' | 'missing' | 'failed' | 'unused';
  readonly problem: string | null;
  readonly simulated: boolean;
}

export interface MidiLamp {
  readonly key: string;
  readonly level: number;
  readonly blink: boolean;
}

export interface MidiLight {
  readonly value: number;
  readonly channel: number;
}

export interface MidiLightSent {
  readonly value: number;
  readonly channel?: number;
}

export interface MidiLights {
  readonly off: MidiLight;
  readonly half: MidiLight;
  readonly full: MidiLight;
  readonly blink: MidiLight | null;
  readonly number: number | null;
}

export interface MidiLightsSent {
  readonly off: MidiLightSent;
  readonly half: MidiLightSent;
  readonly full: MidiLightSent;
  readonly blink?: MidiLightSent | null;
  readonly number?: number | null;
}

export interface MidiMapping {
  readonly device: string;
  readonly channel: number;
  readonly message: 'note' | 'cc' | 'cc14' | 'pitchbend' | 'program';
  readonly number: number;
  readonly target: string;
  readonly mode: 'absolute' | 'relative-twos' | 'relative-offset' | 'relative-sign';
  readonly pickup: boolean;
  readonly feedback: boolean;
  readonly lights: MidiLights | null;
}

export interface MidiMappingSent {
  readonly device?: string;
  readonly channel?: number;
  readonly message?: 'note' | 'cc' | 'cc14' | 'pitchbend' | 'program';
  readonly number?: number;
  readonly target?: string;
  readonly mode?: 'absolute' | 'relative-twos' | 'relative-offset' | 'relative-sign';
  readonly pickup?: boolean;
  readonly feedback?: boolean;
  readonly lights?: MidiLightsSent | null;
}

export interface MidiMonitorLine {
  readonly at: number;
  readonly device: string;
  readonly message: string;
  readonly target: string | null;
  readonly result: string;
  readonly value: number | null;
  readonly desk: number | null;
  readonly reason: string | null;
}

export interface MidiPages {
  readonly firstChannel: number;
  readonly channelsPerPage: number;
  readonly firstMemory: number;
  readonly memoriesPerPage: number;
  readonly firstEffect: number;
  readonly effectsPerPage: number;
}

export interface MidiPresetView {
  readonly id: string;
  readonly name: string;
  readonly device: string;
  readonly about: string;
}

export interface MidiRole {
  readonly controls: boolean;
  readonly keys: string[];
}

export interface MidiSetup {
  readonly devices: string[];
  readonly mappings: MidiMapping[];
}

export interface MidiSetupSent {
  readonly devices: readonly string[];
  readonly mappings: readonly MidiMappingSent[];
}

export interface MidiState {
  readonly works: boolean;
  readonly devices: MidiDeviceView[];
  readonly mappings: MidiMapping[];
  readonly learning: string | null;
  readonly presets: MidiPresetView[];
  readonly osc: OscView;
}

export interface MixerView {
  readonly state: '' | 'connecting' | 'connected';
  readonly name: string;
  readonly playlist: string;
}

export interface NetworkChoice {
  readonly network: string;
  readonly address: string;
  readonly adapter: string;
  readonly router: boolean;
}

export interface OscView {
  readonly on: boolean;
  readonly port: number;
  readonly state: 'off' | 'listening' | 'failed';
  readonly problem: string | null;
}

export interface OutputPlan {
  readonly kind: 'artnet' | 'sacn' | 'usb';
  readonly target: string;
  readonly ports: OutputPort[];
  readonly network: string;
  readonly priority: number;
}

export interface OutputPlanSent {
  readonly kind: 'artnet' | 'sacn' | 'usb';
  readonly target: string;
  readonly ports: readonly OutputPort[];
  readonly network?: string;
  readonly priority?: number;
}

export interface OutputPort {
  readonly universe: number;
  readonly address: number;
}

export interface OutputView {
  readonly plan: OutputPlan;
  readonly state: 'connected' | 'missing' | 'sending' | 'conflict';
  readonly detail: string;
}

export interface Playback {
  readonly levels: number[];
  readonly sequenceSteps: Record<number, number>;
  readonly stack: StackPlayback;
  readonly sequenceOn: Record<number, number[]>;
  readonly crossfade?: StackCrossfade | null;
  readonly effects: EffectPlayback[];
  readonly effectLevels?: EffectLevels | null;
  readonly sounds: SoundPlayback[];
}

export interface ProjectEntry {
  readonly name: string;
  readonly path: string;
  readonly modified: string;
}

export interface ProjectInfo {
  readonly name: string;
  readonly path: string | null;
  readonly unsaved: boolean;
}

export interface ProjectList {
  readonly folder: string;
  readonly projects: ProjectEntry[];
  readonly examples: string[];
}

export interface Sequence {
  readonly loop: boolean;
  readonly steps: Step[];
  readonly random: boolean;
  readonly speed: number;
}

export interface SequenceSent {
  readonly loop: boolean;
  readonly steps: readonly StepSent[];
  readonly random?: boolean;
  readonly speed?: number;
}

export interface SoloKey {
  readonly target: 'channel' | 'memory';
  readonly number: number;
}

export interface SoloState {
  readonly up: SoloKey[];
  readonly down: SoloKey[];
}

export interface Sound {
  readonly number: number;
  readonly name: string;
  readonly file: string;
  readonly hash: string;
  readonly seconds: number;
  readonly start: number;
  readonly end: number | null;
  readonly loop: boolean;
  readonly decibels: number;
  readonly fadeIn: number;
  readonly fadeOut: number;
  readonly pan: number;
  readonly speed: number;
  readonly pitch: number;
  readonly echo: number;
  readonly echoTime: number;
  readonly echoRepeats: number;
  readonly reverb: number;
  readonly room: number;
  readonly playlist?: string | null;
}

export interface SoundDevice {
  readonly id: string;
  readonly name: string;
  readonly channels: number;
}

export interface SoundOutputView {
  readonly devices: SoundDevice[];
  readonly device: string;
  readonly outputs: number;
  readonly state: 'playing' | 'missing' | 'none' | 'off';
  readonly name: string;
  readonly channels: number;
  readonly listen: boolean;
  readonly listenDevice: string;
  readonly listenState: '' | 'playing' | 'missing' | 'none' | 'off' | 'same';
  readonly listenName: string;
  readonly mixer: string;
  readonly playlist: string;
  readonly mixerState: '' | 'connecting' | 'connected';
  readonly mixerName: string;
}

export interface SoundPlayback {
  readonly number: number;
  readonly playing: boolean;
  readonly level: number;
  readonly fading: boolean;
  readonly played: number;
  readonly position: number;
  readonly missing: boolean;
  readonly length: number;
}

export interface SoundSent {
  readonly number: number;
  readonly name: string;
  readonly file: string;
  readonly hash: string;
  readonly seconds?: number;
  readonly start?: number;
  readonly end?: number | null;
  readonly loop?: boolean;
  readonly decibels?: number;
  readonly fadeIn?: number;
  readonly fadeOut?: number;
  readonly pan?: number;
  readonly speed?: number;
  readonly pitch?: number;
  readonly echo?: number;
  readonly echoTime?: number;
  readonly echoRepeats?: number;
  readonly reverb?: number;
  readonly room?: number;
  readonly playlist?: string | null;
}

export interface SoundSetupSent {
  readonly device?: string;
  readonly outputs?: number;
  readonly music?: number | null;
  readonly listen?: boolean;
  readonly listenDevice?: string;
  readonly mixer?: string;
  readonly playlist?: string;
}

export interface SoundTime {
  readonly sound: number;
  readonly seconds: number;
}

export interface StackCrossfade {
  readonly channels: number[];
  readonly memories: number[];
  readonly levels: number[];
}

export interface StackPlayback {
  readonly currentId: number | null;
  readonly nextId: number | null;
  readonly state: 'idle' | 'fading' | 'waiting' | 'holding';
  readonly paused: boolean;
  readonly remaining: number;
  readonly live: boolean;
  readonly on: number[];
  readonly learning?: boolean;
}

export interface StackState {
  readonly steps: StackStep[];
}

export interface StackStep {
  readonly id: number;
  readonly cue: string;
  readonly target: 'channel' | 'memory' | 'fixture' | 'group' | 'release' | 'effect' | 'sound';
  readonly number: number;
  readonly value: number;
  readonly up: number;
  readonly wait: number;
  readonly down: number;
  readonly color?: string | null;
  readonly add?: boolean;
  readonly continue?: boolean;
  readonly disabled?: boolean;
  readonly at?: SoundTime | null;
}

export interface StageLight {
  readonly id: number;
  readonly name: string;
  readonly address: number;
  readonly channels: string;
  readonly color: string | null;
  readonly x: number;
  readonly y: number;
  readonly angle: number;
  readonly shape: 'round' | 'square';
  readonly width: number;
  readonly depth: number;
  readonly type: number | null;
  readonly limit: number;
  readonly note: string;
}

export interface StageLightSent {
  readonly id: number;
  readonly name: string;
  readonly address: number;
  readonly channels: string;
  readonly color: string | null;
  readonly x: number;
  readonly y: number;
  readonly angle: number;
  readonly shape?: 'round' | 'square';
  readonly width?: number;
  readonly depth?: number;
  readonly type?: number | null;
  readonly limit?: number;
  readonly note?: string;
}

export interface StagePlan {
  readonly width: number;
  readonly depth: number;
  readonly lights: StageLight[];
  readonly pictureId: string | null;
  readonly pictureOpacity: number;
  readonly pictureOver: 'stage' | 'plan';
  readonly types: FixtureType[];
  readonly groups: LightGroup[];
}

export interface StagePlanSent {
  readonly width: number;
  readonly depth: number;
  readonly lights: readonly StageLightSent[];
  readonly pictureId?: string | null;
  readonly pictureOpacity?: number;
  readonly pictureOver?: 'stage' | 'plan';
  readonly types?: readonly FixtureTypeSent[];
  readonly groups?: readonly LightGroupSent[];
}

export interface Step {
  readonly target: 'channel' | 'memory' | 'fixture' | 'group' | 'release' | 'effect' | 'sound';
  readonly number: number;
  readonly value: number;
  readonly up: number;
  readonly wait: number;
  readonly down: number;
  readonly color?: string | null;
  readonly add?: boolean;
  readonly continue?: boolean;
  readonly disabled?: boolean;
  readonly at?: SoundTime | null;
}

export interface StepSent {
  readonly target: 'channel' | 'memory' | 'fixture' | 'group' | 'release' | 'effect' | 'sound';
  readonly number: number;
  readonly value: number;
  readonly up: number;
  readonly wait: number;
  readonly down: number;
  readonly color?: string | null;
  readonly add?: boolean;
  readonly continue?: boolean;
  readonly disabled?: boolean;
  readonly at?: SoundTime | null;
}
