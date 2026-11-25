import { Component, computed, inject, input } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { StageLight } from '../desk/desk-types';
import {
  EFFECT_KINDS,
  EFFECT_ORDERS,
  EFFECT_SHAPES,
  fitsKind,
  shapeLabel,
} from '../desk/effect-shapes';
import {
  EFFECT_NAME_LENGTH,
  Effect,
  EffectKind,
  EffectOrder,
  MAX_EFFECTS,
  MAX_EFFECT_COLORS,
} from '../desk/effects';
import { LampButton } from '../lamp-button/lamp-button';
import { channelLabel } from '../desk/universes';

/** What each kind's Size does, and what the kind does, in a sentence for under the editor. */
const EXPLAIN: Readonly<Record<EffectKind, string>> = {
  movement:
    'A movement moves each head around where it points, or around a center of its own, as big as Size of its range; its fader makes it smaller. Pan and tilt only: a dimmer it leaves to the rest.',
  intensity:
    'An intensity lets its lights rise and fall: a light gives the higher of the effect and the rest. Size says how deep the wave goes, 100 from dark to full.',
  color:
    "A color takes over the color of its lights, its fader from the color the rest gives to the effect's. A light nothing else lights it lights itself, as bright as its fader; a lit light keeps its brightness. Size says how deep the colors are, 0 white.",
  pixel:
    'A pattern lies on the stage plan and runs over the cells of its lights by where they are: it lights them itself, where it is black they stay as the rest has them. Size says how big it is; for sparkle how many flash.',
};

/**
 * The editor of an effect: its name, kind and shape, the lights it works on (a group, or lights of
 * the plan), its speed, size, spread, direction, order, blocks and wings, its fade times, and what
 * its shape needs besides (a duty, colors, an angle, a center, a point). Every change is stored at
 * once (the store sends quick changes together); nothing while the stack is live.
 */
@Component({
  imports: [LampButton],
  selector: 'lm-effect-editor',
  styleUrl: './effect-editor.css',
  templateUrl: './effect-editor.html',
})
export class EffectEditor {
  protected readonly store = inject(DeskStore);
  readonly effect = input.required<Effect>();

  protected readonly kinds = EFFECT_KINDS;
  protected readonly orders = EFFECT_ORDERS;
  protected readonly nameLength = EFFECT_NAME_LENGTH;
  protected readonly locked = this.store.live;

  protected readonly shapes = computed(() => EFFECT_SHAPES[this.effect().kind]);
  protected readonly groups = computed(() => this.store.stage().groups);
  protected readonly usesGroup = computed(() => this.effect().group != null);
  /** The lights of the plan that can do what the effect does, by address. */
  protected readonly fitting = computed(() => {
    const plan = this.store.stage();
    return plan.lights
      .filter((light) => fitsKind(light, this.effect().kind, plan))
      .sort((a, b) => a.address - b.address);
  });

  private readonly kind = computed(() => this.effect().kind);
  private readonly shape = computed(() => this.effect().shape);
  /** Spread, order, blocks and wings: for lights in a run, not for a pattern, which lies on the plan. */
  protected readonly inRun = computed(() => this.kind() !== 'pixel');
  protected readonly dutyShown = computed(
    () => this.shape() === 'pulse' || this.shape() === 'bars',
  );
  protected readonly angleShown = computed(
    () =>
      (this.kind() === 'movement' && this.shape() === 'line') ||
      (this.kind() === 'pixel' && ['gradient', 'bars', 'rainbow'].includes(this.shape())),
  );
  protected readonly centerShown = computed(() => this.kind() === 'movement');
  protected readonly pointShown = computed(
    () => this.kind() === 'pixel' && this.shape() === 'ripple',
  );
  protected readonly colorsShown = computed(
    () =>
      (this.kind() === 'color' && this.shape() !== 'rainbow') ||
      (this.kind() === 'pixel' && this.shape() !== 'rainbow'),
  );
  protected readonly randomShown = computed(
    () =>
      this.effect().order === 'random' || this.shape() === 'random' || this.shape() === 'sparkle',
  );
  protected readonly explain = computed(() => EXPLAIN[this.kind()]);
  protected readonly maxColors = MAX_EFFECT_COLORS;

  protected address(light: StageLight): string {
    return channelLabel(light.address);
  }

  protected onName(event: Event): void {
    const field = event.target as HTMLInputElement;
    const name = field.value.trim().slice(0, EFFECT_NAME_LENGTH);
    field.value = name;
    this.update({ name });
  }

  /** Another kind: its first shape; the lights stay (those that cannot do it are passed over). */
  protected onKind(event: Event): void {
    const kind = (event.target as HTMLSelectElement).value as EffectKind;
    const effect = this.effect();
    // a movement goes around a center of its own at first, as a new one does
    const center =
      kind === 'movement' && effect.kind !== 'movement' && effect.centerPan == null
        ? { centerPan: 50, centerTilt: 50 }
        : {};
    this.update({
      kind,
      shape: EFFECT_SHAPES[kind][0].shape,
      ...this.renamed(kind, EFFECT_SHAPES[kind][0].shape),
      ...center,
    });
  }

  protected onShape(event: Event): void {
    const shape = (event.target as HTMLSelectElement).value;
    this.update({ shape, ...this.renamed(this.kind(), shape) });
  }

  // An effect still called by its shape ("Circle", as it was made) is called by its new shape.
  private renamed(kind: EffectKind, shape: string): Partial<Effect> {
    const effect = this.effect();
    return effect.name === shapeLabel(effect.kind, effect.shape) || effect.name === ''
      ? { name: shapeLabel(kind, shape) }
      : {};
  }

  protected useLights(): void {
    this.update({ group: null });
  }

  protected useGroup(): void {
    const first = this.groups()[0];
    if (first) this.update({ group: first.id });
  }

  protected onGroup(event: Event): void {
    this.update({ group: Number((event.target as HTMLSelectElement).value) });
  }

  protected toggleLight(id: number, event: Event): void {
    const on = (event.target as HTMLInputElement).checked;
    const others = this.effect().lights.filter((light) => light !== id);
    this.update({ lights: on ? [...others, id] : others });
  }

  protected allLights(): void {
    this.update({ lights: this.fitting().map((light) => light.id) });
  }

  protected noLights(): void {
    this.update({ lights: [] });
  }

  /**
   * A number typed into a field: taken over when it is a number within the range, rounded to
   * `digits` places after the point; otherwise the field shows what the effect has again.
   */
  protected onNumber(key: NumberKey, event: Event, min: number, max: number, digits = 0): void {
    const field = event.target as HTMLInputElement;
    const value = Number(field.value.trim().replace(',', '.'));
    if (field.value.trim() === '' || !Number.isFinite(value) || value < min || value > max) {
      field.value = String(this.effect()[key] ?? '');
      return;
    }
    const factor = 10 ** digits;
    this.update({ [key]: Math.round(value * factor) / factor } as Partial<Effect>);
  }

  protected onDirection(event: Event): void {
    this.update({
      direction: (event.target as HTMLSelectElement).value === 'back' ? 'back' : 'forward',
    });
  }

  protected onOrder(event: Event): void {
    this.update({ order: (event.target as HTMLSelectElement).value as EffectOrder });
  }

  /** A movement around a center of its own (the middle of the range at first), or around where each head points. */
  protected onCenter(event: Event): void {
    const own = (event.target as HTMLInputElement).checked;
    this.update(own ? { centerPan: 50, centerTilt: 50 } : { centerPan: null, centerTilt: null });
  }

  /** A ripple from a point of its own (the middle of the stage at first), or from the middle of its lights. */
  protected onPoint(event: Event): void {
    const own = (event.target as HTMLInputElement).checked;
    const plan = this.store.stage();
    this.update(
      own ? { pointX: plan.width / 2, pointY: plan.depth / 2 } : { pointX: null, pointY: null },
    );
  }

  protected onColor(index: number, event: Event): void {
    const colors = [...this.effect().colors];
    colors[index] = (event.target as HTMLInputElement).value.toLowerCase();
    this.update({ colors });
  }

  protected addColor(): void {
    const colors = this.effect().colors;
    if (colors.length < MAX_EFFECT_COLORS)
      this.update({ colors: [...colors, colors.length % 2 === 0 ? '#ff2000' : '#2060ff'] });
  }

  protected removeColor(index: number): void {
    this.update({ colors: this.effect().colors.filter((_, other) => other !== index) });
  }

  /** Another random order and other random shapes. */
  protected newSeed(): void {
    this.update({ seed: Math.floor(Math.random() * 1_000_000) + 1 });
  }

  /** Copy: to the next number without an effect, which the editor then shows. */
  protected copy(): void {
    const from = this.effect().number;
    const taken = this.store.effectsByNumber();
    for (let step = 1; step < MAX_EFFECTS; step++) {
      const to = ((from - 1 + step) % MAX_EFFECTS) + 1;
      if (taken.has(to)) continue;
      this.store.copyEffect(from, to);
      this.store.chosenEffect.set(to);
      return;
    }
  }

  protected remove(): void {
    this.store.deleteEffect(this.effect().number);
  }

  protected leave(event: Event): void {
    (event.target as HTMLElement).blur();
  }

  private update(change: Partial<Effect>): void {
    if (this.locked()) return;
    this.store.setEffect({ ...this.effect(), ...change });
  }
}

/** The fields of an effect that are typed as numbers. */
type NumberKey =
  | 'speed'
  | 'size'
  | 'spread'
  | 'blocks'
  | 'wings'
  | 'fadeIn'
  | 'fadeOut'
  | 'duty'
  | 'angle'
  | 'centerPan'
  | 'centerTilt'
  | 'pointX'
  | 'pointY';
