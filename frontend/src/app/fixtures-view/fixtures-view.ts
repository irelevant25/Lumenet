import { Component, computed, inject, signal } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { LightGroup, MAX_GROUPS } from '../desk/desk-types';
import { intensityOf, swatchOf, typeOf } from '../desk/fixtures';
import { channelNumber, universeOf } from '../desk/universes';
import { FixtureChoice, FixturePanel } from '../fixture-panel/fixture-panel';
import { FixtureStrip } from '../fixture-strip/fixture-strip';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * Fixtures: the lights of the stage plan played as fixtures instead of loose channels. Each has
 * a strip: its color on top, one fader and one key for its intensity (its dimmer, or the colors
 * of a light without one, which keep their mix), and its name. A group of lights has a strip of
 * its own, whose fader moves all of them, each in its own color. The panel at the right shows
 * the fixture or group chosen: color, where a moving head points, its wheels, its channels.
 *
 * Everything here moves the channel faders, as the main view does: memories store it, and a
 * memory that holds a light up holds its strip up too. The groups belong to the stage plan.
 */
@Component({
  imports: [FixturePanel, FixtureStrip, LampButton],
  selector: 'lm-fixtures-view',
  styleUrl: './fixtures-view.css',
  templateUrl: './fixtures-view.html',
})
export class FixturesView {
  protected readonly store = inject(DeskStore);

  /** The fixture or group the panel shows; null for none. */
  protected readonly choice = signal<FixtureChoice | null>(null);

  /**
   * The fixtures, by their address, with their intensity and color now. A key prints the address
   * in its universe, with the universe under it in a project of several.
   */
  protected readonly fixtures = computed(() => {
    const plan = this.store.stage();
    const shown = this.store.shownChannels();
    const choice = this.choice();
    const several = this.store.universes() > 1;
    return [...plan.lights]
      .sort((a, b) => a.address - b.address || a.id - b.id)
      .map((light) => ({
        light,
        key: several ? `${channelNumber(light.address)} U${universeOf(light.address)}` : String(light.address),
        level: intensityOf(light, shown),
        color: swatchOf(light, typeOf(plan, light), shown),
        chosen: choice?.kind === 'light' && choice.id === light.id,
      }));
  });

  /** The groups, with the intensity of their brightest light and the color of their first one that has one. */
  protected readonly groups = computed(() => {
    const fixtures = this.fixtures();
    const choice = this.choice();
    return this.store.stage().groups.map((group, index) => {
      const members = fixtures.filter((fixture) => group.lights.includes(fixture.light.id));
      return {
        group,
        number: index + 1,
        level: Math.max(0, ...members.map((member) => member.level)),
        color: members.find((member) => member.color)?.color ?? null,
        chosen: choice?.kind === 'group' && choice.id === group.id,
      };
    });
  });

  /** True while a sequence is recorded (REC SEQ): a key of a fixture or a group puts in a step of it. */
  protected readonly recording = computed(() => this.store.mode().kind === 'recording');

  /** True when a new group can be made: the plan is known, the stack is not live, and there is room. */
  protected readonly canAddGroup = computed(
    () => this.store.stageKnown() && !this.store.live() && this.store.stage().groups.length < MAX_GROUPS,
  );

  protected readonly summary = computed(() => {
    if (!this.store.stageKnown()) return 'The desk program is older than this page and keeps no stage plan.';
    if (this.store.stage().lights.length === 0) return 'No fixtures yet: put lights on the plan in the Stage view (Edit, Add light).';
    const mode = this.store.mode();
    if (mode.kind === 'recording') {
      return `Recording the sequence of memory ${mode.memory}: a key of a fixture or a group puts in a step of it, in the color it shows now. Rec seq on the main view ends it.`;
    }
    return 'One fader for each fixture of the stage plan and each group. Press the color on top for its color, where it points and all its channels.';
  });

  /** Shows a fixture or a group in the panel; pressed again, the panel lets it go. */
  protected choose(kind: FixtureChoice['kind'], id: number): void {
    const choice = this.choice();
    this.choice.set(choice?.kind === kind && choice.id === id ? null : { kind, id });
  }

  /** A new group, of the fixture chosen (if one is), shown in the panel to name it and tick its lights. */
  protected newGroup(): void {
    if (!this.canAddGroup()) return;
    const plan = this.store.stage();
    const id = this.store.nextGroupId();
    const choice = this.choice();
    const group: LightGroup = { id, name: `Group ${id}`, lights: choice?.kind === 'light' ? [choice.id] : [] };
    this.store.setStage({ ...plan, groups: [...plan.groups, group] });
    this.choice.set({ kind: 'group', id });
  }
}
