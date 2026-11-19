import { MAX_SPEED, MIN_SPEED, SPEED_STEP } from './desk-types';

/*
 * The speed fader of a sequence. 1x stands in the middle of its travel: the left half goes
 * from 0.1x to 1x, the right half from 1x to 10x. So slowing down and speeding up get the same
 * room, although the right half covers ten times as much. The fader has this many positions;
 * a speed is always a multiple of 0.05.
 */
export const SPEED_POSITIONS = 360;
const MIDDLE = SPEED_POSITIONS / 2;

/**
 * The fader's step for the arrow keys and the mouse wheel: 0.05 in the left half, 0.5 in the
 * right half. Dragging reaches every 0.05 on both sides.
 */
export const SPEED_KEY_STEP = 10;

/** The speed at a position of the fader. */
export function speedAt(position: number): number {
  const share = position <= MIDDLE ? position / MIDDLE : (position - MIDDLE) / MIDDLE;
  const speed = position <= MIDDLE ? MIN_SPEED + (1 - MIN_SPEED) * share : 1 + (MAX_SPEED - 1) * share;
  return roundedSpeed(speed);
}

/** Where the fader stands for a speed. */
export function speedPosition(speed: number): number {
  return speed <= 1
    ? Math.round(((speed - MIN_SPEED) / (1 - MIN_SPEED)) * MIDDLE)
    : MIDDLE + Math.round(((speed - 1) / (MAX_SPEED - 1)) * MIDDLE);
}

/** A speed on a step of 0.05, within its range; the desk keeps it the same way. */
export function roundedSpeed(speed: number): number {
  const stepped = Math.round(Math.round(speed / SPEED_STEP) * SPEED_STEP * 100) / 100;
  return Math.min(MAX_SPEED, Math.max(MIN_SPEED, stepped));
}

/** The speed as it is shown: "1.00×". */
export function speedText(speed: number): string {
  return `${speed.toFixed(2)}×`;
}
