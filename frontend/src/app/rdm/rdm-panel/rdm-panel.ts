import { Component, DestroyRef, effect, inject, signal, untracked } from '@angular/core';
import { DeskConnection } from '../../desk/desk-connection';
import type { RdmLightChangeSent } from '../../desk/hub-contract';
import {
  RdmBoxView,
  RdmLightView,
  addressRemarks,
  boxTitle,
  channelsText,
  lightAbout,
  lightKey,
  lightName,
  modeLabel,
} from '../rdm';

/** How often the panel asks again while a box searches its line. */
const SEARCH_POLL_MS = 600;

/**
 * The lights with RDM on the DMX lines of the boxes, a tab of Settings: for each box
 * that answers, a table of its lights, each with its DMX address, its mode and its name, which
 * change on the light itself, and a key that makes it show where it is. Search has the box look
 * for lights again. What two lights take of the same channels is said under the table.
 *
 * The lights are read one after the other, each from the light itself through its box, so the
 * table fills in as they answer. A light that shows where it is stops when the tab is left.
 */
@Component({
  selector: 'lm-rdm-panel',
  styleUrl: './rdm-panel.css',
  templateUrl: './rdm-panel.html',
})
export class RdmPanel {
  private readonly connection = inject(DeskConnection);

  /** The boxes as the desk program last told them; null until its first answer. */
  protected readonly boxes = signal<RdmBoxView[] | null>(null);
  /** The lights read so far, by lightKey. */
  private readonly lights = signal<ReadonlyMap<string, RdmLightView>>(new Map());
  /** What went wrong with the last change of a light, by lightKey. */
  private readonly refusals = signal<ReadonlyMap<string, string>>(new Map());
  /** The lights being changed right now. */
  private readonly changing = signal<ReadonlySet<string>>(new Set());
  /** Why there is no list at all: a desk program without RDM, a lost connection. */
  protected readonly failure = signal<string | null>(null);

  protected readonly boxTitle = boxTitle;
  protected readonly lightName = lightName;
  protected readonly lightAbout = lightAbout;
  protected readonly channelsText = channelsText;
  protected readonly modeLabel = modeLabel;

  private timer: ReturnType<typeof setTimeout> | undefined;
  private reading = false;
  private gone = false;
  /** The lights this panel made show where they are: they stop when it goes. */
  private readonly identifying = new Set<string>();

  constructor() {
    // Asked as soon as the page is connected, and again after the connection came back.
    effect(() => {
      if (this.connection.online()) untracked(() => void this.refresh());
    });
    inject(DestroyRef).onDestroy(() => {
      this.gone = true;
      clearTimeout(this.timer);
      for (const key of this.identifying) {
        const light = this.lights().get(key);
        if (light?.identify)
          void this.connection
            .invoke('SetRdmLight', light.box, light.id, { identify: false })
            .catch(() => undefined);
      }
    });
  }

  /** A light of a box as read, or null while it is being read. */
  protected lightOf(box: RdmBoxView, id: string): RdmLightView | null {
    return this.lights().get(lightKey(box.address, id)) ?? null;
  }

  protected refusalOf(box: RdmBoxView, id: string): string | null {
    return this.refusals().get(lightKey(box.address, id)) ?? null;
  }

  protected isChanging(light: RdmLightView): boolean {
    return this.changing().has(lightKey(light.box, light.id));
  }

  /** What the line under the box's heading says: how many lights, or that it searches. */
  protected boxState(box: RdmBoxView): string {
    if (box.error || !box.rdm) return '';
    const found = box.lights.length === 1 ? '1 light' : `${box.lights.length} lights`;
    return box.searching ? `searching, ${found} so far` : found;
  }

  /** What needs saying about the channels of the lights read on a box's line. */
  protected remarksOf(box: RdmBoxView): string[] {
    const read = box.lights.map((id) => this.lightOf(box, id)).filter((light) => light !== null);
    return addressRemarks(read);
  }

  /** Search: the box looks for the lights on its line again; the table is read anew. */
  protected async search(box: RdmBoxView): Promise<void> {
    try {
      await this.connection.invoke('SearchRdm', box.address);
    } catch (error) {
      this.failure.set((error as Error).message);
      return;
    }
    this.lights.update(
      (lights) => new Map([...lights].filter(([, light]) => light.box !== box.address)),
    );
    this.boxes.update(
      (boxes) =>
        boxes?.map((other) => (other === box ? { ...box, searching: true } : other)) ?? null,
    );
    clearTimeout(this.timer);
    await this.refresh();
  }

  protected onAddress(light: RdmLightView, event: Event): void {
    const input = event.target as HTMLInputElement;
    const address = Number(input.value);
    if (!Number.isInteger(address) || address < 1 || address > 512) {
      input.value = String(light.address);
      this.refuse(light, 'A DMX address is 1 to 512.');
      return;
    }
    if (address !== light.address)
      void this.change(light, { address }, input, String(light.address));
  }

  protected onMode(light: RdmLightView, event: Event): void {
    const select = event.target as HTMLSelectElement;
    const mode = Number(select.value);
    if (mode !== light.mode) void this.change(light, { mode }, select, String(light.mode));
  }

  protected onLabel(light: RdmLightView, event: Event): void {
    const input = event.target as HTMLInputElement;
    const label = input.value.trim();
    if (label !== light.label) void this.change(light, { label }, input, light.label);
  }

  protected identify(light: RdmLightView): void {
    const key = lightKey(light.box, light.id);
    if (!light.identify) this.identifying.add(key);
    void this.change(light, { identify: !light.identify });
  }

  // Asks the desk program for the boxes, reads the lights not read yet, and asks again soon while a
  // box still searches.
  private async refresh(): Promise<void> {
    clearTimeout(this.timer);
    if (this.gone) return;
    let boxes: RdmBoxView[];
    try {
      boxes = (await this.connection.invoke('GetRdmBoxes')) ?? [];
    } catch (error) {
      this.failure.set((error as Error).message);
      return;
    }
    if (this.gone) return;
    this.failure.set(null);
    this.boxes.set(boxes);
    await this.readLights();
    if (!this.gone && (this.boxes() ?? []).some((box) => box.searching))
      this.timer = setTimeout(() => void this.refresh(), SEARCH_POLL_MS);
  }

  // One light after the other, so that the box answers each between its DMX frames.
  private async readLights(): Promise<void> {
    if (this.reading) return;
    this.reading = true;
    try {
      for (const box of this.boxes() ?? []) {
        for (const id of box.lights) {
          if (this.gone) return;
          if (this.lights().has(lightKey(box.address, id))) continue;
          try {
            const light = await this.connection.invoke('GetRdmLight', box.address, id);
            if (light) this.keep(light);
          } catch (error) {
            this.refuse({ box: box.address, id }, (error as Error).message);
          }
        }
      }
    } finally {
      this.reading = false;
    }
  }

  // A change of a light; refused, the field that asked for it shows the light's value again.
  private async change(
    light: RdmLightView,
    change: RdmLightChangeSent,
    field?: HTMLInputElement | HTMLSelectElement,
    was?: string,
  ): Promise<void> {
    const key = lightKey(light.box, light.id);
    this.changing.update((keys) => new Set(keys).add(key));
    try {
      const changed = await this.connection.invoke('SetRdmLight', light.box, light.id, change);
      if (changed) this.keep(changed);
      this.refusals.update((refusals) => {
        const rest = new Map(refusals);
        rest.delete(key);
        return rest;
      });
    } catch (error) {
      this.refuse(light, (error as Error).message);
      if (field && was !== undefined) field.value = was;
    } finally {
      this.changing.update((keys) => {
        const rest = new Set(keys);
        rest.delete(key);
        return rest;
      });
    }
  }

  private keep(light: RdmLightView): void {
    this.lights.update((lights) => new Map(lights).set(lightKey(light.box, light.id), light));
  }

  private refuse(light: Pick<RdmLightView, 'box' | 'id'>, message: string): void {
    this.refusals.update((refusals) =>
      new Map(refusals).set(lightKey(light.box, light.id), message),
    );
  }
}
