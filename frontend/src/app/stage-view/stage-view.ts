import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { CHANNEL_COUNT, STAGE_BACK_ROOM, STAGE_FRONT_ROOM, STAGE_SIDE_ROOM, StageLight } from '../desk/desk-types';
import {
  SNAP,
  channelRange,
  freeSpot,
  keptOn,
  lookOf,
  nameFor,
  nextAddress,
  withLight,
  withoutLight,
} from '../desk/stage-lights';
import { LampButton } from '../lamp-button/lamp-button';
import { StagePanel } from '../stage-panel/stage-panel';

/** A round light is a little smaller than its squares, a square one a hair, so that the grid shows between lights. */
const ROUND_INSET = 0.1;
const SQUARE_INSET = 0.08;

/** A light as the plan draws it. */
interface ShownLight {
  readonly light: StageLight;
  /** Its middle, in per cent of the plan's width and height. */
  readonly left: number;
  readonly top: number;
  /** Its size in squares, before it is turned: across and from back to front. */
  readonly width: number;
  readonly depth: number;
  readonly square: boolean;
  readonly device: boolean;
  /** True when its cells lie one under the other: it is deeper than wide. */
  readonly vertical: boolean;
  /** For a device: the level of its first channel, in per cent. */
  readonly level: number;
  /** Its cells: the colour of each lens and of its glow, how bright it is, and where its middle is, in per cent of the light. */
  readonly cells: readonly { lens: string; glow: string; brightness: number; x: number; y: number }[];
  /** How big the glow of a cell is, in squares: across and from back to front. */
  readonly glowWidth: number;
  readonly glowDepth: number;
  /** From the middle of the light to its name under it, in squares. */
  readonly below: number;
  readonly title: string;
}

/**
 * Stage: the stage seen from above, a grid of squares with the audience at the bottom, and the
 * lights of the rig on it, each in the colour its channels give it at this moment (after the
 * master; dark in blackout), over a picture of the stage if the plan has one. For programming
 * without the rig: open it in a second window or on a tablet, and every change on the desk
 * shows at once. The colours come from stage-lights.ts.
 *
 * A click on a light shows it in the panel at the right, with the faders of its channels. Edit
 * places the lights: drag one to move it (the arrow keys move the chosen one), change it in the
 * panel (stage-panel), Add light puts a new one on the plan. The plan belongs to the project;
 * like everything stored, it cannot change while the stack is live.
 */
@Component({
  host: { '[class.with-panel]': 'panelShown()' },
  imports: [LampButton, StagePanel],
  selector: 'lm-stage-view',
  styleUrl: './stage-view.css',
  templateUrl: './stage-view.html',
})
export class StageView {
  protected readonly store = inject(DeskStore);
  private readonly planBox = viewChild.required<ElementRef<HTMLElement>>('plan');

  private readonly editMode = signal(false);
  /** True while the plan is edited: never while the stack is live. */
  protected readonly editing = computed(() => this.editMode() && !this.store.live());
  /** True when the plan can be edited now. */
  protected readonly canEdit = computed(() => this.store.stageKnown() && !this.store.live());
  /** True when every channel has a light: there is no room for another. */
  protected readonly full = computed(() => this.store.stage().lights.length >= CHANNEL_COUNT);

  /** The number of the light that is chosen; null for none. */
  protected readonly selectedId = signal<number | null>(null);
  private readonly selected = computed(() => this.store.stage().lights.find((light) => light.id === this.selectedId()) ?? null);

  /** The panel stands at the right while Edit is lit or a light is chosen. */
  protected readonly panelShown = computed(() => this.editing() || this.selected() !== null);

  // A light being dragged: where it is now. The plan gets it once it is let go.
  private readonly dragged = signal<{ id: number; x: number; y: number } | null>(null);
  private drag: { id: number; pointer: number; startX: number; startY: number; light: StageLight } | null = null;

  /** The whole plan in squares: the stage and the room around it. */
  protected readonly totalWidth = computed(() => this.store.stage().width + 2 * STAGE_SIDE_ROOM);
  protected readonly totalDepth = computed(() => this.store.stage().depth + STAGE_BACK_ROOM + STAGE_FRONT_ROOM);

  /** Where the stage itself is on the plan, in per cent. */
  protected readonly stageBox = computed(() => ({
    left: (STAGE_SIDE_ROOM / this.totalWidth()) * 100,
    top: (STAGE_BACK_ROOM / this.totalDepth()) * 100,
    width: (this.store.stage().width / this.totalWidth()) * 100,
    height: (this.store.stage().depth / this.totalDepth()) * 100,
  }));

  /** Where the picture lies, in per cent: on the stage, or over the whole plan. */
  protected readonly pictureBox = computed(() =>
    this.store.stage().pictureOver === 'plan' ? { left: 0, top: 0, width: 100, height: 100 } : this.stageBox(),
  );

  /** The lights as the plan draws them. */
  protected readonly shown = computed<readonly ShownLight[]>(() => {
    const output = this.store.output();
    const dragged = this.dragged();
    const plan = this.store.stage();
    return plan.lights.map((light) => {
      const at = dragged?.id === light.id ? dragged : light;
      const look = lookOf(light, output);
      const square = light.shape === 'square';
      const width = light.width - (square ? SQUARE_INSET : ROUND_INSET);
      const depth = light.depth - (square ? SQUARE_INSET : ROUND_INSET);
      const count = Math.max(1, look.cells.length);
      const vertical = light.depth > light.width;
      const turn = (light.angle * Math.PI) / 180;
      const halfHeight = (Math.abs(Math.sin(turn)) * width + Math.abs(Math.cos(turn)) * depth) / 2;
      return {
        light,
        left: ((at.x + STAGE_SIDE_ROOM) / this.totalWidth()) * 100,
        top: ((at.y + STAGE_BACK_ROOM) / this.totalDepth()) * 100,
        width,
        depth,
        square,
        device: look.device,
        vertical,
        level: Math.round((look.level / 255) * 100),
        cells: look.cells.map((cell, index) => {
          const along = ((index + 0.5) / count) * 100;
          return { ...cell, x: vertical ? 50 : along, y: vertical ? along : 50 };
        }),
        glowWidth: (vertical ? width : width / count) + 2,
        glowDepth: (vertical ? depth / count : depth) + 2,
        below: halfHeight + 0.12,
        title: `${light.name || 'A light'}: ${light.channels.length === 1 ? 'channel' : 'channels'} ${channelRange(light)}`,
      };
    });
  });

  /** What the plan says about itself, for screen readers. */
  protected readonly planLabel = computed(() => {
    const count = this.store.stage().lights.length;
    const lights = count === 1 ? '1 light' : `${count} lights`;
    return this.editing()
      ? `Stage plan with ${lights}: drag a light to move it, click it to change it`
      : `Stage plan with ${lights}: click a light to see it and its faders`;
  });

  /** The sentence under the title. */
  protected readonly summary = computed(() => {
    if (!this.store.stageKnown()) return 'The desk program is older than this page and keeps no stage plan.';
    const parts = ['Every light shows what its channels give it now, after the master.'];
    if (this.store.blackout()) parts.push('Blackout is on: the rig is dark.');
    if (this.store.live()) parts.push('The stack is live, so the plan cannot be changed.');
    else if (this.editing()) parts.push('Drag a light to move it; click it to change it at the right.');
    else parts.push('Click a light for its faders; Edit places the lights.');
    return parts.join(' ');
  });

  protected toggleEdit(): void {
    if (!this.canEdit()) return;
    this.editMode.update((on) => !on);
  }

  /** Puts a new light on the plan, on the next free channel and a free place, and chooses it. */
  protected addLight(): void {
    const plan = this.store.stage();
    if (!this.editing() || this.full()) return;

    const address = nextAddress(plan.lights);
    const id = plan.lights.reduce((highest, light) => Math.max(highest, light.id), 0) + 1;
    const note = this.store.channelNotes()[address - 1] ?? '';
    const light: StageLight = {
      id,
      name: nameFor(address, note),
      address,
      channels: 'D',
      colour: null,
      angle: 0,
      shape: 'round',
      width: 1,
      depth: 1,
      ...freeSpot(plan),
    };
    this.store.setStage({ ...plan, lights: [...plan.lights, light] });
    this.selectedId.set(id);
  }

  /** A press on the plan outside every light: no light is chosen any more. */
  protected onPlanDown(event: PointerEvent): void {
    if (!(event.target as Element).closest('.light')) this.selectedId.set(null);
  }

  /** A light is chosen by a press on it; while Edit is lit, it is dragged as well. */
  protected onLightDown(event: PointerEvent, light: StageLight): void {
    if (event.button !== 0) return;
    this.selectedId.set(light.id);
    if (!this.editing()) return;

    event.preventDefault();
    const element = event.currentTarget as HTMLElement;
    element.setPointerCapture?.(event.pointerId);
    element.focus({ preventScroll: true });
    this.drag = { id: light.id, pointer: event.pointerId, startX: event.clientX, startY: event.clientY, light };
  }

  protected onLightMove(event: PointerEvent): void {
    const drag = this.drag;
    if (!drag || event.pointerId !== drag.pointer) return;
    const square = this.planBox().nativeElement.getBoundingClientRect().width / this.totalWidth();
    if (!(square > 0)) return;
    const place = keptOn(
      this.store.stage(),
      drag.light.x + (event.clientX - drag.startX) / square,
      drag.light.y + (event.clientY - drag.startY) / square,
      drag.light,
    );
    const now = this.dragged();
    if (now?.x !== place.x || now?.y !== place.y) this.dragged.set({ id: drag.id, ...place });
  }

  protected onLightUp(event: PointerEvent): void {
    const drag = this.drag;
    if (!drag || event.pointerId !== drag.pointer) return;
    this.drag = null;
    const place = this.dragged();
    this.dragged.set(null);
    if (place && (place.x !== drag.light.x || place.y !== drag.light.y)) {
      this.store.setStage(withLight(this.store.stage(), drag.id, { x: place.x, y: place.y }));
    }
  }

  /**
   * Enter or Space chooses the light, Esc lets it go. While Edit is lit the arrow keys move it by
   * half a square (with Shift two), and Delete takes it away.
   */
  protected onLightKey(event: KeyboardEvent, light: StageLight): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.selectedId.set(light.id);
      return;
    }
    if (event.key === 'Escape') {
      this.selectedId.set(null);
      return;
    }
    if (!this.editing()) return;
    if (event.key === 'Delete' || event.key === 'Backspace') {
      event.preventDefault();
      this.store.setStage(withoutLight(this.store.stage(), light.id));
      this.selectedId.set(null);
      return;
    }

    const step = event.shiftKey ? 4 * SNAP : SNAP;
    const moves: Record<string, [number, number]> = {
      ArrowLeft: [-step, 0],
      ArrowRight: [step, 0],
      ArrowUp: [0, -step],
      ArrowDown: [0, step],
    };
    const move = moves[event.key];
    if (!move) return;
    event.preventDefault();
    this.selectedId.set(light.id);
    // a square light moves by whole squares, staying on the lines of the grid
    const grid = light.shape === 'square' && light.angle % 90 === 0 ? 2 : 1;
    const place = keptOn(this.store.stage(), light.x + move[0] * grid, light.y + move[1] * grid, light);
    this.store.setStage(withLight(this.store.stage(), light.id, place));
  }
}
