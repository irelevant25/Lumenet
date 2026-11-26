import { LightGroup, MAX_LEVEL, MEMORY_COUNT, StageLight } from '../desk-types';
import { IDLE, MemoryContent } from '../function-mode';
import {
  colorChanges,
  hasDimmer,
  intensityChanges,
  intensityOf,
  LevelChanges,
  mixedColor,
  panTiltOf,
  positionChanges,
  shutterOpening,
  typeOf,
} from '../../fixtures/fixtures';
import { universeStart } from '../universes';
import { withItem, zeros } from './desk-store-helpers';
import { DeskStoreState } from './desk-store-state';

/**
 * A layer of DeskStore: the pages of channels and memories, the faders and the master, fixtures and
 * groups, the notes under the faders, a level of a look on the Memories view, the independent keys
 * of channels and memories, Black out and Clear.
 */
export abstract class DeskStoreFaders extends DeskStoreState {
  /** Shows the page of channels that starts at `first`. */
  showChannelPage(first: number): void {
    this.wantedChannel.set(first);
  }

  /** Shows the first page of a universe's channels (its U key). */
  showUniverse(universe: number): void {
    if (universe >= 1 && universe <= this.universes())
      this.wantedChannel.set(universeStart(universe));
  }

  /** Shows the page of memories that starts at `first`. */
  showMemoryPage(first: number): void {
    this.wantedMemory.set(first);
  }

  setChannel(channel: number, level: number): void {
    if (this.channels()[channel - 1] === level) return;
    this.channels.update((levels) => withItem(levels, channel - 1, level));
    this.claimChannel(channel);
    this.sendSoon(`channel ${channel}`, 'SetChannel', channel, level);
  }

  setMemoryLevel(memory: number, level: number): void {
    if (this.memoryLevels()[memory - 1] === level) return;
    this.claimMemory(memory, level);
    this.memoryLevels.update((levels) => withItem(levels, memory - 1, level));
    this.sendSoon(`memory ${memory}`, 'SetMemoryLevel', memory, level);
  }

  // A channel fader that moves takes its channel, if that is an attribute of a light.
  protected claimChannel(channel: number): void {
    if (!this.roles().attribute[channel - 1]) return;
    this.channelClaims.update((claims) => withItem(claims, channel - 1, ++this.claimCount));
  }

  // A memory fader that leaves zero takes the lights its memory holds. Call before the level changes.
  protected claimMemory(memory: number, level: number): void {
    if (level === 0 || this.memoryLevels()[memory - 1] !== 0) return;
    this.memoryClaims.update((claims) => withItem(claims, memory - 1, ++this.claimCount));
  }

  /** Sets many channel faders at once, each as if it was moved. */
  setChannels(changes: LevelChanges): void {
    for (const [channel, level] of changes) this.setChannel(channel, level);
  }

  /** The intensity of a fixture as the faders show it, 0 to 255: its dimmer, or its strongest color. */
  fixtureIntensity(light: StageLight): number {
    return intensityOf(light, this.shownChannels());
  }

  /**
   * A fixture to another intensity: its dimmers, or its colors keeping their mix (from dark:
   * the color it had last, or white); a moving head's shutter opens with it.
   */
  setFixtureIntensity(light: StageLight, level: number): void {
    const shown = this.shownChannels();
    const color = mixedColor(light, shown);
    if (color) this.lastColors.set(light.id, color);
    this.setChannels(
      intensityChanges(
        light,
        typeOf(this.stage(), light),
        level,
        shown,
        this.lastColors.get(light.id) ?? null,
      ),
    );
  }

  /** The key of a fixture: to full, and to zero when it gives light. */
  toggleFixture(light: StageLight): void {
    this.setFixtureIntensity(light, this.fixtureIntensity(light) > 0 ? 0 : MAX_LEVEL);
  }

  /**
   * A number for a new light (or group) that no light (or group) has, and no step of the
   * sequences or the stack points to: a step of a light that is gone must not come to play a new one.
   */
  nextLightId(): number {
    return this.nextId('fixture', this.stage().lights);
  }

  nextGroupId(): number {
    return this.nextId('group', this.stage().groups);
  }

  protected nextId(target: 'fixture' | 'group', items: readonly { id: number }[]): number {
    const steps = [
      ...Object.values(this.sequences()).flatMap((sequence) => sequence.steps),
      ...this.stack().steps,
    ];
    const used = steps.filter((step) => step.target === target).map((step) => step.number);
    return Math.max(0, ...items.map((item) => item.id), ...used) + 1;
  }

  /**
   * A fixture in a color (red, green and blue, 0 to 255): its color channels mix it. A fixture
   * that is dark comes up to full with it, so that the color shows.
   */
  setFixtureColor(light: StageLight, color: [number, number, number]): void {
    this.lastColors.set(light.id, color);
    const shown = this.shownChannels();
    const intensity = intensityOf(light, shown);
    const type = typeOf(this.stage(), light);
    const changes = new Map<number, number>();
    if (hasDimmer(light)) {
      for (const [channel, level] of colorChanges(light, color, MAX_LEVEL))
        changes.set(channel, level);
      if (intensity === 0)
        for (const [channel, level] of intensityChanges(light, type, MAX_LEVEL, shown, color))
          changes.set(channel, level);
    } else {
      for (const [channel, level] of colorChanges(light, color, intensity || MAX_LEVEL))
        changes.set(channel, level);
      for (const [channel, level] of shutterOpening(light, type, shown))
        changes.set(channel, level);
    }
    this.setChannels(changes);
  }

  /** Points a moving head: pan `x` and tilt `y`, each from 0 to 1. */
  setFixturePosition(light: StageLight, x: number, y: number): void {
    const axes = panTiltOf(light, typeOf(this.stage(), light));
    if (axes) this.setChannels(positionChanges(axes, x, y));
  }

  /** The lights of a group, as the plan has them now. */
  groupLights(group: LightGroup): StageLight[] {
    const lights = this.stage().lights;
    return group.lights.flatMap((id) => lights.filter((light) => light.id === id));
  }

  /** The intensity of a group: that of its brightest light. */
  groupIntensity(group: LightGroup): number {
    return Math.max(0, ...this.groupLights(group).map((light) => this.fixtureIntensity(light)));
  }

  /** The fader of a group: every light of it to this intensity, each in its own color. */
  setGroupIntensity(group: LightGroup, level: number): void {
    for (const light of this.groupLights(group)) this.setFixtureIntensity(light, level);
  }

  /** The key of a group: all its lights to full, and to zero when any gives light. */
  toggleGroup(group: LightGroup): void {
    this.setGroupIntensity(group, this.groupIntensity(group) > 0 ? 0 : MAX_LEVEL);
  }

  setMaster(level: number): void {
    if (this.master() === level) return;
    this.master.set(level);
    this.sendSoon('master', 'SetMaster', level);
  }

  // A note arrives here as joinNote made it. It is not trimmed again: a note with text only in
  // its second line starts with the line separator, and trimming would take that away.
  setChannelNote(channel: number, note: string): void {
    if (this.channelNotes()[channel - 1] === note) return;
    this.channelNotes.update((notes) => withItem(notes, channel - 1, note));
    this.run('SetChannelNote', channel, note);
  }

  setMemoryNote(memory: number, note: string): void {
    if (this.memoryNotes()[memory - 1] === note) return;
    this.memoryNotes.update((notes) => withItem(notes, memory - 1, note));
    this.run('SetMemoryNote', memory, note);
  }

  /**
   * Changes the level one channel has in the look of a memory, from the table of the Memories
   * view: shown at once, and the desk program sends the whole desk, after which the looks are
   * fetched again. An empty memory gets a look with only that channel (a 0 leaves it empty); a
   * memory that holds a sequence is changed on the Sequences view instead.
   */
  setMemoryChannel(memory: number, channel: number, level: number): void {
    if (this.live() || this.memorySequence()[memory - 1]) return;
    const looks = this.memoryLooks();
    const look = looks[memory];
    if (look ? look[channel - 1] === level : level === 0) return;

    const changed = (look ?? zeros(this.channelCount())).slice();
    changed[channel - 1] = level;
    this.memoryLooks.set({ ...looks, [memory]: changed });
    if (!look) this.markStored(memory, 'look');
    this.run('SetMemoryChannel', memory, channel, level);
  }

  /**
   * A channel key of the independents switches its channel: to zero when it is up, to full when
   * it is at zero. While a solo is lit the key holds its fader instead, while it is held (holdIndependent).
   */
  toggleIndependent(key: number): void {
    if (this.soloMode()) return;
    const channel = this.independents()[key - 1];
    this.setChannel(channel, this.channels()[channel - 1] > 0 ? 0 : MAX_LEVEL);
  }

  /** A memory key of the independents switches the fader of its memory, the same way. */
  toggleMemoryIndependent(key: number): void {
    if (this.soloMode()) return;
    const memory = this.memoryIndependents()[key - 1];
    this.setMemoryLevel(memory, this.memoryLevels()[memory - 1] > 0 ? 0 : MAX_LEVEL);
  }

  /** Binds a channel key of the independents to another channel. */
  setIndependent(key: number, channel: number): void {
    if (this.independents()[key - 1] === channel) return;
    this.independents.update((channels) => withItem(channels, key - 1, channel));
    this.run('SetIndependent', key, channel);
  }

  /** Binds a memory key of the independents to another memory. */
  setMemoryIndependent(key: number, memory: number): void {
    if (this.memoryIndependents()[key - 1] === memory) return;
    this.memoryIndependents.update((memories) => withItem(memories, key - 1, memory));
    this.run('SetMemoryIndependent', key, memory);
  }

  /** Blackout sends zero to every address but leaves the desk as it is. */
  toggleBlackout(): void {
    const on = !this.blackout();
    this.blackout.set(on);
    this.connection.send('SetBlackout', on);
  }

  /** Cancels an unfinished function and brings every channel fader and memory fader to zero. */
  clear(): void {
    this.mode.set(IDLE);
    this.dropOutbox(); // fader moves that are still waiting must not arrive after the clear
    this.channels.set(zeros(this.channelCount()));
    this.memoryLevels.set(zeros(MEMORY_COUNT));
    this.run('Clear');
  }

  /**
   * Gives the memory's key its look right away: lit or not, red or running into yellow. The
   * backend confirms with the full state a moment later.
   */
  protected markStored(memory: number, holds: MemoryContent): void {
    this.memoryStored.update((flags) => withItem(flags, memory - 1, holds !== 'nothing'));
    this.memorySequence.update((flags) => withItem(flags, memory - 1, holds === 'sequence'));
  }

  protected async fetchMemoryLooks(): Promise<void> {
    try {
      this.memoryLooks.set((await this.connection.invoke('GetMemories')) ?? {});
      this.memoryLooksKnown.set(true);
    } catch {
      // An older desk program does not know the question. Then no fader follows a memory.
      this.memoryLooks.set({});
      this.memoryLooksKnown.set(false);
    }
  }
}
