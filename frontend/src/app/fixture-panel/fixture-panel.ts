import { Component, computed, inject, model } from '@angular/core';
import { ColorPick, ColorPicker, WheelSlot } from '../color-picker/color-picker';
import { DeskStore } from '../desk/desk-store';
import { GROUP_NAME_LENGTH, LightGroup, StageLight } from '../desk/desk-types';
import {
  choiceChannels,
  hexOf,
  mixedColor,
  mixesColor,
  panTiltOf,
  positionOf,
  rangeAt,
  typeOf,
  wheelColorOf,
  wheelColors,
} from '../desk/fixtures';
import { channelRange } from '../desk/stage-lights';
import { LightFaders } from '../light-faders/light-faders';
import { PositionPad } from '../position-pad/position-pad';

/** What the panel of the Fixtures page shows: a fixture, or a group, by its id. */
export interface FixtureChoice {
  readonly kind: 'light' | 'group';
  readonly id: number;
}

/**
 * The panel at the right of the Fixtures page (under the strips on a small screen), always
 * there. For the fixture chosen: its color, where a moving head points, what its wheels and
 * other settings are set to, and the faders of all its channels. For the group chosen: its
 * name, its lights, and a color for all of them. With nothing chosen: what the page does.
 */
@Component({
  host: { role: 'complementary', '[attr.aria-label]': 'title()' },
  imports: [ColorPicker, LightFaders, PositionPad],
  selector: 'lm-fixture-panel',
  styleUrl: './fixture-panel.css',
  templateUrl: './fixture-panel.html',
})
export class FixturePanel {
  protected readonly store = inject(DeskStore);

  /** The fixture or group chosen; null for none. */
  readonly choice = model<FixtureChoice | null>(null);

  protected readonly channelRange = channelRange;
  protected readonly nameLength = GROUP_NAME_LENGTH;

  protected readonly light = computed(() => {
    const choice = this.choice();
    return choice?.kind === 'light' ? (this.store.stage().lights.find((light) => light.id === choice.id) ?? null) : null;
  });

  protected readonly group = computed(() => {
    const choice = this.choice();
    return choice?.kind === 'group' ? (this.store.stage().groups.find((group) => group.id === choice.id) ?? null) : null;
  });

  protected readonly title = computed(() => this.light()?.name || this.group()?.name || 'Fixture');

  private readonly type = computed(() => {
    const light = this.light();
    return light ? typeOf(this.store.stage(), light) : undefined;
  });

  /** What the chosen fixture is, in words. */
  protected readonly kindSaid = computed(() => {
    const light = this.light();
    const type = this.type();
    if (!light) return '';
    const kind = type ? `${type.maker} ${type.model}, ${type.mode}` : `Channels ${light.channels}`;
    return `${kind}; ${light.channels.length === 1 ? 'channel' : 'channels'} ${channelRange(light)}`;
  });

  protected readonly mixes = computed(() => {
    const light = this.light();
    return light ? mixesColor(light) : false;
  });

  protected readonly slots = computed<WheelSlot[]>(() => {
    const light = this.light();
    return light ? wheelColors(light, this.type()) : [];
  });

  /** The color the fixture shows now. */
  protected readonly color = computed(() => {
    const light = this.light();
    if (!light) return null;
    const shown = this.store.shownChannels();
    const mixed = mixedColor(light, shown);
    return mixed ? hexOf(mixed) : wheelColorOf(light, this.type(), shown);
  });

  private readonly axes = computed(() => {
    const light = this.light();
    return light ? panTiltOf(light, this.type()) : null;
  });

  /** Where a moving head points now; null for a fixture without pan and tilt. */
  protected readonly position = computed(() => {
    const axes = this.axes();
    return axes ? positionOf(axes, this.store.shownChannels()) : null;
  });

  /** The channels that do several things (wheels, a shutter), with the range each stands in now. */
  protected readonly choices = computed(() => {
    const light = this.light();
    if (!light) return [];
    const shown = this.store.shownChannels();
    return choiceChannels(light, this.type()).map((choice) => {
      const value = shown[choice.channel - 1] ?? 0;
      const range = rangeAt(choice.ranges, value);
      return { ...choice, value, current: range ? choice.ranges.indexOf(range) : -1 };
    });
  });

  /** The lights of the plan by their address, for the group's list. */
  protected readonly lights = computed(() => [...this.store.stage().lights].sort((a, b) => a.address - b.address));

  /** True when some light of the chosen group mixes its color. */
  protected readonly groupMixes = computed(() => {
    const group = this.group();
    return group ? this.store.groupLights(group).some((light) => mixesColor(light)) : false;
  });

  protected onPick(light: StageLight, pick: ColorPick): void {
    if (pick.kind === 'mix') this.store.setFixtureColor(light, pick.rgb);
    else this.store.setChannel(pick.channel, pick.value);
  }

  protected onGroupPick(group: LightGroup, pick: ColorPick): void {
    if (pick.kind !== 'mix') return;
    for (const light of this.store.groupLights(group)) {
      if (mixesColor(light)) this.store.setFixtureColor(light, pick.rgb);
    }
  }

  protected setChoice(channel: number, event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);
    if (Number.isInteger(value)) this.store.setChannel(channel, value);
  }

  protected rename(group: LightGroup, event: Event): void {
    const field = event.target as HTMLInputElement;
    const name = field.value.trim().slice(0, GROUP_NAME_LENGTH);
    field.value = name;
    this.changeGroup(group, { name });
  }

  protected toggleMember(group: LightGroup, lightId: number, event: Event): void {
    const member = (event.target as HTMLInputElement).checked;
    const others = group.lights.filter((id) => id !== lightId);
    this.changeGroup(group, { lights: member ? [...others, lightId] : others });
  }

  protected removeGroup(group: LightGroup): void {
    const plan = this.store.stage();
    this.store.setStage({ ...plan, groups: plan.groups.filter((other) => other.id !== group.id) });
    this.choice.set(null);
  }

  private changeGroup(group: LightGroup, changes: Partial<LightGroup>): void {
    const plan = this.store.stage();
    this.store.setStage({ ...plan, groups: plan.groups.map((other) => (other.id === group.id ? { ...other, ...changes } : other)) });
  }
}
