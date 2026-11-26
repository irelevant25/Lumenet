import { Component, computed, input, output } from '@angular/core';
import { hsvOf, rgbOfHex, rgbOfHsv } from '../../fixtures/fixtures';

/** A color chosen: one to mix from color channels, or a slot of a color wheel. */
export type ColorPick =
  | { readonly kind: 'mix'; readonly rgb: [number, number, number] }
  | { readonly kind: 'slot'; readonly channel: number; readonly value: number };

/** A color of a color wheel: the channel and the value that bring it. */
export interface WheelSlot {
  readonly channel: number;
  readonly value: number;
  readonly color: string;
  readonly label: string;
}

/** The colors a press gives, as gels in a box. */
const PRESETS: readonly { readonly label: string; readonly hex: string }[] = [
  { label: 'White', hex: '#ffffff' },
  { label: 'Warm white', hex: '#ffd9a6' },
  { label: 'Red', hex: '#ff0000' },
  { label: 'Orange', hex: '#ff6000' },
  { label: 'Yellow', hex: '#ffc800' },
  { label: 'Green', hex: '#00ff00' },
  { label: 'Cyan', hex: '#00ffff' },
  { label: 'Light blue', hex: '#3c8cff' },
  { label: 'Blue', hex: '#0000ff' },
  { label: 'Lavender', hex: '#8c50ff' },
  { label: 'Magenta', hex: '#ff00ff' },
  { label: 'Pink', hex: '#ff3c8c' },
];

/**
 * The color of a fixture: for one that mixes its color, twelve colors to press and a field of
 * every hue (across) and how deep (from white at the bottom to full at the top); for one with a
 * color wheel, the colors of its wheel.
 */
@Component({
  selector: 'lm-color-picker',
  styleUrl: './color-picker.css',
  templateUrl: './color-picker.html',
})
export class ColorPicker {
  /** The color the fixture shows now, like "#ff2000"; null for none. */
  readonly color = input<string | null>(null);
  /** True for a fixture that mixes any color from color channels. */
  readonly mixes = input(false);
  /** The colors of its color wheel. */
  readonly slots = input<readonly WheelSlot[]>([]);
  /** A color was chosen. */
  readonly pick = output<ColorPick>();

  protected readonly presets = PRESETS;

  /** Where the color is in the field, in per cent: its hue across, its depth from the bottom. */
  protected readonly marker = computed(() => {
    const color = this.color();
    if (!color) return null;
    const [hue, saturation] = hsvOf(rgbOfHex(color));
    return { x: (hue / 360) * 100, y: (1 - saturation) * 100 };
  });

  private dragPointer: number | null = null;

  protected choosePreset(hex: string): void {
    this.pick.emit({ kind: 'mix', rgb: rgbOfHex(hex) });
  }

  protected chooseSlot(slot: WheelSlot): void {
    this.pick.emit({ kind: 'slot', channel: slot.channel, value: slot.value });
  }

  protected onFieldDown(event: PointerEvent): void {
    if (event.button !== 0) return;
    (event.currentTarget as HTMLElement).setPointerCapture?.(event.pointerId);
    this.dragPointer = event.pointerId;
    this.pickAt(event);
  }

  protected onFieldMove(event: PointerEvent): void {
    if (event.pointerId === this.dragPointer) this.pickAt(event);
  }

  protected onFieldUp(event: PointerEvent): void {
    if (event.pointerId === this.dragPointer) this.dragPointer = null;
  }

  // The hue across the field, the depth from white at the bottom to full at the top.
  private pickAt(event: PointerEvent): void {
    const box = (event.currentTarget as HTMLElement).getBoundingClientRect();
    if (box.width <= 0 || box.height <= 0) return;
    const across = Math.min(1, Math.max(0, (event.clientX - box.left) / box.width));
    const down = Math.min(1, Math.max(0, (event.clientY - box.top) / box.height));
    this.pick.emit({ kind: 'mix', rgb: rgbOfHsv(across * 360, 1 - down) });
  }
}
