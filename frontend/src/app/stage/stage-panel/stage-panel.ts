import { Component, computed, inject, input, model, viewChild } from '@angular/core';
import { DeskStore } from '../../desk/desk-store';
import { LIGHT_MAX_SIZE, LightShape, NO_LIMIT, STAGE_MAX_SIZE, STAGE_MIN_SIZE, StageLight } from '../../desk/desk-types';
import { typeOf } from '../../fixtures/fixtures';
import {
  DimmerPlace,
  LIGHT_TYPES,
  LightType,
  MAX_CELLS,
  MAX_LETTERS,
  WARM_WHITE,
  channelRange,
  copiedLight,
  fittingAddress,
  formOf,
  hasCells,
  keptOn,
  lettersOf,
  resized,
  usesOwnColor,
  withLight,
  withoutLight,
} from '../stage-lights';
import { channelField, channelFromText, universeOf } from '../../desk/universes';
import { LibraryDialog } from '../../fixtures/library-dialog/library-dialog';
import { LightFaders } from '../../fixtures/light-faders/light-faders';
import { StagePictureForm } from '../stage-picture-form/stage-picture-form';

/**
 * The panel at the right of the Stage view. For the light chosen on the plan: its name, kind and
 * channels, and the faders of its channels (they work as those of the main view do); while Edit
 * is lit, also everything about it that can be changed (name, address, kind, cells, color,
 * shape and size, how it is turned). While Edit is lit and no light is chosen: the stage itself
 * (its size) and the picture under it. Every change goes to the plan at once, see
 * DeskStore.setStage.
 */
@Component({
  host: { role: 'complementary', '[attr.aria-label]': 'title()' },
  imports: [LibraryDialog, LightFaders, StagePictureForm],
  selector: 'lm-stage-panel',
  styleUrl: './stage-panel.css',
  templateUrl: './stage-panel.html',
})
export class StagePanel {
  protected readonly store = inject(DeskStore);
  private readonly library = viewChild.required(LibraryDialog);

  /** The number of the light chosen on the plan; null: none, the panel shows the stage. */
  readonly lightId = model<number | null>(null);
  /** True while Edit is lit: the light and the stage can be changed. */
  readonly editing = input(false);

  protected readonly types = LIGHT_TYPES;
  protected readonly minSize = STAGE_MIN_SIZE;
  protected readonly maxSize = STAGE_MAX_SIZE;
  protected readonly maxLightSize = LIGHT_MAX_SIZE;
  protected readonly maxCells = MAX_CELLS;
  protected readonly maxLetters = MAX_LETTERS;
  protected readonly warmWhite = WARM_WHITE;
  protected readonly hasCells = hasCells;

  protected readonly light = computed(() => this.store.stage().lights.find((light) => light.id === this.lightId()) ?? null);

  /** What screen readers call the panel. */
  protected readonly title = computed(() => {
    const light = this.light();
    return light ? `The light ${light.name || this.range(light)}` : 'The stage';
  });

  /** The channels of a light: "30 to 41 (U1)". */
  protected range(light: StageLight): string {
    return channelRange(light);
  }

  /** The address as its field shows it: "113 U2" in a project of several universes, "113" in one of one. */
  protected addressField(light: StageLight): string {
    return channelField(light.address, this.store.universes());
  }

  /** The chosen light as the form shows it. */
  protected readonly form = computed(() => {
    const light = this.light();
    return light ? formOf(light.channels) : null;
  });

  /** The kind of light from the fixture library the chosen light is; undefined for one of letters. */
  protected readonly libraryType = computed(() => {
    const light = this.light();
    return light ? typeOf(this.store.stage(), light) : undefined;
  });

  protected readonly noLimit = NO_LIMIT;

  /** The kind of the chosen light, in words: "RGB in 4 cells, dimmer first". */
  protected readonly kindSaid = computed(() => {
    const form = this.form();
    const light = this.light();
    const type = this.libraryType();
    if (!form || !light) return '';
    if (type) return `${type.maker} ${type.model}, ${type.mode}`;
    if (form.type === 'letters') return `Channels ${light.channels}`;
    const label = LIGHT_TYPES.find((type) => type.type === form.type)?.label ?? '';
    if (!hasCells(form.type)) return label;
    const cells = form.cells === 1 ? '' : ` in ${form.cells} cells`;
    const dimmer = form.dimmer === 'none' ? '' : `, dimmer ${form.dimmer}`;
    return `${label}${cells}${dimmer}`;
  });

  /** True when the chosen light takes its color from the color field. */
  protected readonly ownColor = computed(() => {
    const light = this.light();
    return light ? usesOwnColor(light.channels) : false;
  });

  protected rename(light: StageLight, event: Event): void {
    this.change(light, { name: (event.target as HTMLInputElement).value.trim().slice(0, 24) });
  }

  /**
   * A typed address: "113 U2", or "113" in the universe the light is in (see universes.ts). The light moves down where its channels
   * would run past the end of the project's channels or of its universe; a text that names no
   * channel leaves it where it is.
   */
  protected setAddress(light: StageLight, event: Event): void {
    const field = event.target as HTMLInputElement;
    const count = this.store.channelCount();
    const typed = channelFromText(field.value, count, universeOf(light.address));
    const address = typed === null ? light.address : fittingAddress(typed, light.channels.length, count);
    field.value = channelField(address, this.store.universes());
    if (address !== light.address) this.change(light, { address });
  }

  protected setType(light: StageLight, event: Event): void {
    const select = event.target as HTMLSelectElement;
    const chosen = select.value;
    if (chosen === 'library-search') {
      // The window chooses; until then (and if it is closed without) the light stays what it is.
      select.value = this.libraryType() ? 'library' : formOf(light.channels).type;
      this.library().open(light);
      return;
    }
    if (chosen === 'library') return;

    const type = chosen as LightType;
    const form = formOf(light.channels);
    this.setLetters(light, lettersOf({ ...form, type, cells: type === form.type ? form.cells : 1 }, light.channels));
  }

  /** The most the light gives, in per cent: 1 to 100. */
  protected setLimit(light: StageLight, event: Event): void {
    const field = event.target as HTMLInputElement;
    const limit = Math.min(NO_LIMIT, Math.max(1, Math.round(Number(field.value)) || NO_LIMIT));
    field.value = String(limit);
    this.change(light, { limit });
  }

  /** A copy of the light, on the channels after the last light, and chosen. */
  protected copy(light: StageLight): void {
    const plan = this.store.stage();
    const copy = copiedLight(plan, light, this.store.channelCount(), this.store.nextLightId());
    this.store.setStage({ ...plan, lights: [...plan.lights, copy] });
    this.lightId.set(copy.id);
  }

  protected setDimmer(light: StageLight, event: Event): void {
    const dimmer = (event.target as HTMLSelectElement).value as DimmerPlace;
    this.setLetters(light, lettersOf({ ...formOf(light.channels), dimmer }, light.channels));
  }

  protected setCells(light: StageLight, event: Event): void {
    const field = event.target as HTMLInputElement;
    const cells = Math.min(MAX_CELLS, Math.max(1, Math.round(Number(field.value)) || 1));
    field.value = String(cells);
    this.setLetters(light, lettersOf({ ...formOf(light.channels), cells }, light.channels));
  }

  protected onLetters(light: StageLight, event: Event): void {
    const field = event.target as HTMLInputElement;
    const letters = lettersOf({ type: 'letters', dimmer: 'none', cells: 1 }, field.value);
    field.value = letters || light.channels;
    if (letters) this.setLetters(light, letters);
  }

  protected setColor(light: StageLight, event: Event): void {
    this.change(light, { color: (event.target as HTMLInputElement).value.toLowerCase() });
  }

  protected setShape(light: StageLight, event: Event): void {
    const shape: LightShape = (event.target as HTMLSelectElement).value === 'square' ? 'square' : 'round';
    this.reshape(light, { shape });
  }

  protected setSize(light: StageLight, side: 'width' | 'depth', event: Event): void {
    const field = event.target as HTMLInputElement;
    const size = Math.min(LIGHT_MAX_SIZE, Math.max(1, Math.round(Number(field.value)) || 1));
    field.value = String(size);
    this.reshape(light, { [side]: size });
  }

  protected turn(light: StageLight, degrees: number): void {
    this.reshape(light, { angle: (((light.angle + degrees) % 360) + 360) % 360 });
  }

  protected remove(light: StageLight): void {
    this.store.setStage(withoutLight(this.store.stage(), light.id));
    this.lightId.set(null);
  }

  protected setStageSize(side: 'width' | 'depth', event: Event): void {
    const field = event.target as HTMLInputElement;
    const plan = resized(this.store.stage(), side, Number(field.value));
    field.value = String(plan[side]);
    this.store.setStage(plan);
  }

  protected removeAll(): void {
    const plan = this.store.stage();
    this.store.setStage({ ...plan, lights: [], groups: plan.groups.map((group) => ({ ...group, lights: [] })) });
  }

  private change(light: StageLight, changes: Partial<StageLight>): void {
    this.store.setStage(withLight(this.store.stage(), light.id, changes));
  }

  // Another shape, size or angle: a square light comes to lie on the lines of the grid again.
  private reshape(light: StageLight, changes: Partial<StageLight>): void {
    const shaped = { ...light, ...changes };
    this.change(light, { ...changes, ...keptOn(this.store.stage(), shaped.x, shaped.y, shaped) });
  }

  // New channels for a light: if they do not fit after its address, the address moves down. A
  // light of the library becomes one of letters.
  private setLetters(light: StageLight, channels: string): void {
    this.change(light, { channels, type: null, address: fittingAddress(light.address, channels.length, this.store.channelCount()) });
  }
}
