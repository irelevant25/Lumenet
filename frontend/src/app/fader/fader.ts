import {
  Component,
  ElementRef,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { MAX_LEVEL } from '../desk/desk-types';
import { LampTone } from '../lamp-button/lamp-button';

/** One notch of the mouse wheel. With Shift held it is 1. */
const WHEEL_STEP = 5;

/** Page Up and Page Down. */
const PAGE_STEP = 16;

/**
 * A fader: a cap that slides in a slot, from 0 at the bottom (or left) to 255, or to another
 * top that the owner names.
 *
 * - Drag the cap, or press anywhere in the slot to bring the cap there.
 * - A finger must take the cap: a touch beside it does nothing. On a phone the faders cover
 *   most of the screen, and a thumb that scrolls the page must not move a light.
 * - The mouse wheel moves it in small steps. In the mobile layout it scrolls the page instead.
 * - With the keyboard: arrows, Page Up and Page Down, Home and End.
 *
 * The fader does not keep its level itself. It shows `value` and asks for another level with
 * `valueChange`; whoever owns the level decides what the fader shows next. That matters for a
 * channel a memory holds up: the owner may pull its fader down, but the cap stays where the
 * memory holds it.
 */
@Component({
  host: {
    role: 'slider',
    tabindex: '0',
    'aria-valuemin': '0',
    '[attr.aria-valuemax]': 'max()',
    '[attr.aria-valuenow]': 'value()',
    '[attr.aria-valuetext]': 'valueText()',
    '[attr.aria-label]': 'label()',
    '[attr.aria-orientation]': 'orientation()',
    '[class]': 'hostClass()',
    '[style.--level]': 'fraction()',
    '[style.--own]': 'ownFraction()',
    '(pointerdown)': 'onPointerDown($event)',
    '(pointermove)': 'onPointerMove($event)',
    '(pointerup)': 'onPointerEnd($event)',
    '(pointercancel)': 'onPointerEnd($event)',
    '(wheel)': 'onWheel($event)',
    '(keydown)': 'onKeyDown($event)',
  },
  selector: 'lm-fader',
  styleUrl: './fader.css',
  templateUrl: './fader.html',
})
export class Fader {
  /** The level the fader shows, 0 to 255. */
  readonly value = input(0);
  /** The level the owner asks for by moving the fader. */
  readonly valueChange = output<number>();
  /**
   * The fader's own level, when something else (a memory) holds the fader higher. The slot
   * is then lit in the fader's colour up to here, and in the colour of the memories above.
   */
  readonly ownLevel = input<number | null>(null);
  /** What a screen reader calls this fader, for example "Channel 5". */
  readonly label = input.required<string>();
  readonly tone = input<LampTone>('channel');
  readonly orientation = input<'vertical' | 'horizontal'>('vertical');
  /** The top of the scale. A level fader goes to 255; another scale, like the speed of a sequence, names its own. */
  readonly max = input(MAX_LEVEL);
  /** How far an arrow key or a notch of the wheel moves the fader. Shift with the wheel moves it by 1. */
  readonly step = input(1);
  /** What a screen reader says for the value, when the number itself means nothing to a person, like "1.50 times". */
  readonly valueText = input<string | null>(null);

  /** Page Up and Page Down: 16 levels on a level fader, four steps on another scale. */
  private readonly pageStep = computed(() => (this.step() === 1 ? PAGE_STEP : this.step() * 4));

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly cap = viewChild.required<ElementRef<HTMLElement>>('cap');

  /** The pointer (mouse button or finger) that is dragging, or null. Each finger can hold its own fader. */
  private dragPointer: number | null = null;
  /** How far from the cap's middle it was grabbed, so that the cap does not jump under the finger. */
  private grabOffset = 0;
  private readonly dragging = signal(false);

  protected readonly fraction = computed(() => this.value() / this.max());
  /** Where the fader's own part of the lit slot ends: at the cap, unless something holds the fader higher. */
  protected readonly ownFraction = computed(
    () => Math.min(this.ownLevel() ?? this.value(), this.value()) / this.max(),
  );

  protected readonly hostClass = computed(() => {
    const classes = [`tone-${this.tone()}`, this.orientation()];
    if (this.dragging()) classes.push('dragging');
    return classes.join(' ');
  });

  protected onPointerDown(event: PointerEvent): void {
    if (event.button !== 0 || this.dragPointer !== null) return;

    const cap = this.cap().nativeElement;
    const onCap = event.target === cap;
    if (event.pointerType === 'touch' && !onCap) return; // a finger beside the cap: see above

    const host = this.host.nativeElement;
    host.setPointerCapture(event.pointerId);
    host.focus();
    this.dragPointer = event.pointerId;
    this.dragging.set(true);

    if (onCap) {
      // Grabbed by the cap: remember where, and do not move yet.
      const box = cap.getBoundingClientRect();
      const middle = this.isVertical() ? box.top + box.height / 2 : box.left + box.width / 2;
      this.grabOffset = this.positionOf(event) - middle;
    } else {
      // Pressed in the slot: the cap comes to the finger.
      this.grabOffset = 0;
      this.moveCapTo(event);
    }

    event.preventDefault(); // no text selection while dragging
  }

  protected onPointerMove(event: PointerEvent): void {
    if (event.pointerId === this.dragPointer) this.moveCapTo(event);
  }

  protected onPointerEnd(event: PointerEvent): void {
    if (event.pointerId !== this.dragPointer) return;
    this.dragPointer = null;
    this.dragging.set(false);
  }

  protected onWheel(event: WheelEvent): void {
    // In the mobile layout the page scrolls, and the wheel belongs to the page: someone who
    // scrolls down must not change every fader the pointer passes over.
    const page = this.host.nativeElement.ownerDocument.documentElement;
    if (page.classList.contains('mobile')) return;

    event.preventDefault(); // the wheel moves the fader, not the page

    // With Shift held, some browsers report the turn as sideways scrolling.
    const turn = event.deltaY !== 0 ? event.deltaY : event.deltaX;
    if (turn === 0) return;

    const step = event.shiftKey ? 1 : this.step() === 1 ? WHEEL_STEP : this.step();
    this.nudge(turn < 0 ? step : -step);
  }

  protected onKeyDown(event: KeyboardEvent): void {
    const target = this.levelAfterKey(event.key);
    if (target === null) return;

    event.preventDefault();
    this.setLevel(target);
  }

  private levelAfterKey(key: string): number | null {
    switch (key) {
      case 'ArrowUp':
      case 'ArrowRight':
        return this.own() + this.step();
      case 'ArrowDown':
      case 'ArrowLeft':
        return this.own() - this.step();
      case 'PageUp':
        return this.own() + this.pageStep();
      case 'PageDown':
        return this.own() - this.pageStep();
      case 'Home':
        return 0;
      case 'End':
        return this.max();
      default:
        return null;
    }
  }

  private nudge(by: number): void {
    this.setLevel(this.own() + by);
  }

  /**
   * The level the owner has set, which is what steps and "no change" are measured against.
   * It is the level shown, unless something holds the fader higher: then a step down must
   * lower the hidden own level, not start from where the cap is held.
   *
   * It follows the inputs, and it is also set the moment the fader asks for a level: the
   * inputs only follow a little later, and two quick steps of the wheel must count as two.
   */
  private readonly own = linkedSignal(() => this.ownLevel() ?? this.value());

  private setLevel(level: number): void {
    const wanted = Math.min(this.max(), Math.max(0, Math.round(level)));
    if (wanted === this.own()) return;

    this.own.set(wanted);
    this.valueChange.emit(wanted);
  }

  private isVertical(): boolean {
    return this.orientation() === 'vertical';
  }

  /** Where the pointer is along the direction of travel. */
  private positionOf(event: PointerEvent): number {
    return this.isVertical() ? event.clientY : event.clientX;
  }

  /** Sets the level so that the cap's middle is under the pointer. */
  private moveCapTo(event: PointerEvent): void {
    const slot = this.host.nativeElement.getBoundingClientRect();
    const capBox = this.cap().nativeElement.getBoundingClientRect();
    const vertical = this.isVertical();

    // The cap's middle travels from half a cap inside one end to half a cap inside the other.
    const capLength = vertical ? capBox.height : capBox.width;
    const travel = (vertical ? slot.height : slot.width) - capLength;
    if (travel <= 0) return;

    const middle = this.positionOf(event) - this.grabOffset;
    const fromZeroEnd = vertical ? slot.bottom - middle : middle - slot.left;
    this.setLevel(((fromZeroEnd - capLength / 2) / travel) * this.max());
  }
}
