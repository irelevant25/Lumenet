import {
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { BlinkClock } from '../desk/blink-clock';
import { DeskStore } from '../desk/desk-store';
import { effectLights, effectSummary, flashesPerSecond, speedText } from '../desk/effect-shapes';
import { Effect, EffectPlayback, EffectPreview } from '../desk/effects';

/** The most flashes a second the preview shows (WCAG 2.3.1 allows 3); a faster effect is shown slower. */
const MOST_FLASHES = 2.5;

/** The room around the square of a movement, in pixels of the window. */
const MARGIN = 16;

/**
 * A preview of one cycle of an effect, as the desk program plays it (it samples the cycle,
 * PreviewEffect): a movement as the path of each head over its pan and tilt with a dot where it is,
 * an intensity as a bar for each light, a color as a swatch for each light, a pattern as the cells
 * of its lights where they are on the plan. While the effect runs the preview goes with it, at the
 * live speed, size and spread of its run; else it runs at the effect's speed. It never flashes
 * faster than 3 times a second (an effect that would is shown slower, and says so); with reduced
 * motion it stands still. For a movement it shows the center the heads go around, and a press or a
 * drag on it moves that center (the Pan and Tilt fields of the editor do the same).
 */
@Component({
  selector: 'lm-effect-preview',
  styleUrl: './effect-preview.css',
  templateUrl: './effect-preview.html',
})
export class EffectPreviewPanel {
  private readonly store = inject(DeskStore);
  private readonly still = inject(BlinkClock).reducedMotion;
  readonly effect = input.required<Effect>();
  readonly playback = input<EffectPlayback | undefined>(undefined);

  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');
  private readonly preview = signal<EffectPreview | null>(null);
  // When the playback came, to go on from its phase between two of them; and when the preview started, for an effect that does not run.
  private playbackAt = 0;
  private readonly startedAt = performance.now();

  /** The speed the preview goes at: the effect's, as it runs. */
  private readonly bpm = computed(() => this.playback()?.bpm ?? this.effect().speed);

  /**
   * The effect as the preview shows it: with the live size and spread of its run, which the live
   * faders move. A new one only when they change, not with every word of the playback.
   */
  private readonly shown = computed(
    () => {
      const effect = this.effect();
      const playback = this.playback();
      return playback ? { ...effect, size: playback.size, spread: playback.spread } : effect;
    },
    { equal: (a, b) => a === b || JSON.stringify(a) === JSON.stringify(b) },
  );

  /** A movement, while the stack is not live: a press on the preview moves its center. */
  protected readonly movable = computed(
    () => this.effect().kind === 'movement' && !this.store.live(),
  );
  private dragging = false;

  /** Shown slower than it runs: it would flash more than 3 times a second. */
  private readonly slowedBy = computed(() => {
    const flashes = flashesPerSecond(this.effect(), this.bpm());
    return flashes > MOST_FLASHES ? MOST_FLASHES / flashes : 1;
  });

  /** What the preview shows, in words: for screen readers, and under it. */
  protected readonly description = computed(() => {
    const effect = this.effect();
    const lights = effectLights(effect, this.store.stage()).length;
    const what = effectSummary(effect, lights, this.bpm());
    return `Preview of effect ${effect.number}: ${what.charAt(0).toLowerCase()}${what.slice(1)}.`;
  });

  /** A line under the preview when it does not show the real speed. */
  protected readonly note = computed(() => {
    if (this.still) return 'Still, as this device asks for less motion.';
    if (this.slowedBy() < 1) {
      const flashes = flashesPerSecond(this.effect(), this.bpm());
      return `Shown slower: at ${speedText(this.bpm())} it flashes ${flashes.toFixed(1)} times a second.`;
    }
    return this.preview()?.items.length === 0
      ? 'No light of the stage plan is in this effect yet.'
      : '';
  });

  constructor() {
    // A new cycle from the desk program when the effect, its live values or the plan change; a
    // little later, as the editor and the live faders change things in a row.
    effect((onCleanup) => {
      const shown = this.shown();
      this.store.stage();
      const timer = setTimeout(
        () => void this.store.previewEffect(shown).then((preview) => this.preview.set(preview)),
        200,
      );
      onCleanup(() => clearTimeout(timer));
    });
    effect(() => {
      this.playback();
      this.playbackAt = performance.now();
    });

    let frame = 0;
    const destroy = inject(DestroyRef);
    const injector = inject(Injector);
    afterNextRender(() => {
      if (this.still) {
        // with reduced motion: drawn again when something changes, not every frame
        effect(
          () => {
            this.preview();
            this.draw();
          },
          { injector },
        );
        return;
      }
      const draw = () => {
        this.draw();
        frame = requestAnimationFrame(draw);
      };
      draw();
    });
    destroy.onDestroy(() => cancelAnimationFrame(frame));
  }

  // ---------------------------------------------------------------- the center of a movement

  protected onDown(event: PointerEvent): void {
    if (!this.movable() || event.button !== 0) return;
    this.dragging = true;
    try {
      (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
    } catch {
      // a pointer the browser does not know (a test's event): the press works without the capture
    }
    this.moveCenter(event);
  }

  protected onMove(event: PointerEvent): void {
    if (this.dragging) this.moveCenter(event);
  }

  protected onUp(event: PointerEvent): void {
    this.dragging = false;
    try {
      (event.currentTarget as HTMLElement).releasePointerCapture(event.pointerId);
    } catch {
      // not captured
    }
  }

  // The center where the pointer is, in the square of the heads' range that drawMovement draws:
  // pan across, tilt down, in per cent with one decimal, as the editor's fields have them.
  private moveCenter(event: PointerEvent): void {
    const box = this.canvas().nativeElement.getBoundingClientRect();
    const side = Math.min(box.width, box.height) - MARGIN;
    if (side <= 0) return;
    const left = box.left + (box.width - side) / 2;
    const top = box.top + (box.height - side) / 2;
    const share = (value: number) => Math.round(Math.min(1, Math.max(0, value)) * 1000) / 10;
    const effect = this.effect();
    const centerPan = share((event.clientX - left) / side);
    const centerTilt = share((event.clientY - top) / side);
    if (effect.centerPan === centerPan && effect.centerTilt === centerTilt) return;
    this.store.setEffect({ ...effect, centerPan, centerTilt });
  }

  /** Where the effect is in its cycle now: with its playback while it runs, by its speed otherwise. */
  private phase(now: number): number {
    if (this.still) return 0;
    const playback = this.playback();
    const perMs = (this.bpm() / 60000) * this.slowedBy();
    if (playback?.running && this.slowedBy() === 1)
      return playback.phase + (now - this.playbackAt) * perMs;
    return (now - this.startedAt) * perMs;
  }

  private draw(): void {
    const canvas = this.canvas().nativeElement;
    const box = canvas.getBoundingClientRect();
    if (box.width === 0 || box.height === 0) return; // not laid out, or hidden: nothing to show
    const ratio = window.devicePixelRatio || 1;
    const width = Math.max(1, Math.round(box.width * ratio));
    const height = Math.max(1, Math.round(box.height * ratio));
    if (canvas.width !== width || canvas.height !== height) {
      canvas.width = width;
      canvas.height = height;
    }

    const paint = canvas.getContext('2d');
    if (!paint) return;
    paint.fillStyle = '#0e1013';
    paint.fillRect(0, 0, width, height);
    const preview = this.preview();
    if (!preview || preview.items.length === 0 || preview.frames === 0) return;

    const phase = this.phase(performance.now());
    const at = Math.floor((phase - Math.floor(phase)) * preview.frames) % preview.frames;
    switch (preview.kind) {
      case 'movement': {
        const effect = this.effect();
        const center =
          effect.centerPan != null && effect.centerTilt != null
            ? { pan: effect.centerPan / 100, tilt: effect.centerTilt / 100 }
            : null;
        drawMovement(paint, preview, at, width, height, ratio, center);
        break;
      }
      case 'intensity':
        drawBars(paint, preview, at, width, height);
        break;
      case 'color':
        drawSwatches(paint, preview, at, width, height);
        break;
      default:
        drawPixels(paint, preview, at, width, height);
    }
  }
}

/**
 * Each head's path over its pan (across) and tilt (down), dim, and a dot where it is now; a
 * movement around a center of its own shows that center as a cross.
 */
function drawMovement(
  paint: CanvasRenderingContext2D,
  preview: EffectPreview,
  at: number,
  width: number,
  height: number,
  ratio: number,
  center: { pan: number; tilt: number } | null,
): void {
  const side = Math.min(width, height) - MARGIN * ratio;
  const left = (width - side) / 2;
  const top = (height - side) / 2;
  paint.strokeStyle = '#363c46';
  paint.lineWidth = ratio;
  paint.strokeRect(left, top, side, side);

  if (center) {
    const x = left + center.pan * side;
    const y = top + center.tilt * side;
    const arm = 9 * ratio;
    paint.strokeStyle = '#d9dde3';
    paint.lineWidth = 1.5 * ratio;
    paint.beginPath();
    paint.moveTo(x - arm, y);
    paint.lineTo(x + arm, y);
    paint.moveTo(x, y - arm);
    paint.lineTo(x, y + arm);
    paint.stroke();
    paint.lineWidth = ratio;
  }

  const violet =
    getComputedStyle(document.documentElement).getPropertyValue('--effect').trim() || '#b388ff';
  preview.items.forEach((item) => {
    if (item.pan.length === 0) return;
    paint.strokeStyle = `${violet}66`;
    paint.beginPath();
    item.pan.forEach((pan, frame) => {
      const x = left + pan * side;
      const y = top + item.tilt[frame] * side;
      if (frame === 0) paint.moveTo(x, y);
      else paint.lineTo(x, y);
    });
    paint.closePath();
    paint.stroke();
  });
  paint.fillStyle = violet;
  preview.items.forEach((item) => {
    if (item.pan.length === 0) return;
    paint.beginPath();
    paint.arc(left + item.pan[at] * side, top + item.tilt[at] * side, 5 * ratio, 0, Math.PI * 2);
    paint.fill();
  });
}

/** A bar for each light, in the effect's order, as high as the light is bright. */
function drawBars(
  paint: CanvasRenderingContext2D,
  preview: EffectPreview,
  at: number,
  width: number,
  height: number,
): void {
  const count = preview.items.length;
  const slot = width / count;
  const gap = Math.min(6, slot * 0.2);
  preview.items.forEach((item, index) => {
    const level = item.levels[at] ?? 0;
    paint.fillStyle = '#23272e';
    paint.fillRect(index * slot + gap / 2, 0, slot - gap, height);
    paint.fillStyle = `rgb(255 244 214 / ${0.25 + level * 0.75})`;
    paint.fillRect(index * slot + gap / 2, height * (1 - level), slot - gap, height * level);
  });
}

/** A swatch for each light, in the effect's order, in the color it has now. */
function drawSwatches(
  paint: CanvasRenderingContext2D,
  preview: EffectPreview,
  at: number,
  width: number,
  height: number,
): void {
  const count = preview.items.length;
  const columns = Math.ceil(Math.sqrt((count * width) / height));
  const rows = Math.ceil(count / columns);
  const cell = Math.min(width / columns, height / rows);
  preview.items.forEach((item, index) => {
    paint.fillStyle = item.colors[at] ?? '#000000';
    const x = (index % columns) * cell;
    const y = Math.floor(index / columns) * cell;
    paint.fillRect(x + 2, y + 2, cell - 4, cell - 4);
  });
}

/** Every cell where it is on the plan, in its color, as bright as the pattern makes it there. */
function drawPixels(
  paint: CanvasRenderingContext2D,
  preview: EffectPreview,
  at: number,
  width: number,
  height: number,
): void {
  const xs = preview.items.map((item) => item.x);
  const ys = preview.items.map((item) => item.y);
  const minX = Math.min(...xs) - 0.5;
  const minY = Math.min(...ys) - 0.5;
  const spanX = Math.max(...xs) + 0.5 - minX;
  const spanY = Math.max(...ys) + 0.5 - minY;
  const scale = Math.min(width / spanX, height / spanY);
  const left = (width - spanX * scale) / 2;
  const top = (height - spanY * scale) / 2;
  const size = Math.max(3, scale * 0.8);
  for (const item of preview.items) {
    paint.globalAlpha = Math.max(0.08, item.levels[at] ?? 0);
    paint.fillStyle = item.colors[at] ?? '#ffffff';
    paint.fillRect(
      left + (item.x - minX) * scale - size / 2,
      top + (item.y - minY) * scale - size / 2,
      size,
      size,
    );
  }
  paint.globalAlpha = 1;
}
