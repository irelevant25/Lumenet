import { describe, expect, it } from 'vitest';
import { StackStep } from './desk-types';
import { WheelPlace, wheelPlaces, wheelRoll } from './stack-wheel';

function stack(count: number): StackStep[] {
  return Array.from({ length: count }, (_, index) => ({
    id: 100 + index,
    cue: String(index + 1),
    target: 'memory',
    number: index + 1,
    value: 255,
    up: 1,
    wait: 0,
    down: 1,
  }));
}

/** The wheel as text, from the top: "4" for a step, "-" for an empty place, the middle in brackets. */
function seen(places: readonly WheelPlace[]): string {
  return places.map((place) => (place.offset === 0 ? `[${place.step?.cue ?? '-'}]` : (place.step?.cue ?? '-'))).join(' ');
}

describe('the stack wheel', () => {
  it('shows the steps around the current one, in the order of the stack', () => {
    const places = wheelPlaces(stack(9), 4, 5, 5);
    expect(seen(places)).toBe('3 4 [5] 6 7');
    expect(places.map((place) => place.kind)).toEqual(['other', 'previous', 'current', 'next', 'other']);
  });

  it('goes round: after the last step comes the first, before the first the last', () => {
    expect(seen(wheelPlaces(stack(9), 8, 0, 5))).toBe('7 8 [9] 1 2');
    expect(seen(wheelPlaces(stack(9), 0, 1, 3))).toBe('9 [1] 2');
  });

  it('shows one place, or as many as asked for', () => {
    expect(seen(wheelPlaces(stack(20), 9, 10, 1))).toBe('[10]');
    expect(seen(wheelPlaces(stack(20), 9, 10, 9))).toBe('6 7 8 9 [10] 11 12 13 14');
  });

  it('shows no step twice: a short stack leaves the outer places empty', () => {
    expect(seen(wheelPlaces(stack(4), 1, 2, 5))).toBe('- 1 [2] 3 4'); // the steps after it come first
    expect(seen(wheelPlaces(stack(3), 1, 2, 7))).toBe('- - 1 [2] 3 - -');
    expect(seen(wheelPlaces(stack(1), 0, -1, 3))).toBe('- [1] -');
  });

  it('shows the other of two steps above and below: Prev and Play both go there', () => {
    expect(seen(wheelPlaces(stack(2), 0, 1, 5))).toBe('- 2 [1] 2 -');
  });

  it('leaves the middle empty while no step is on, with the step Play goes to under it', () => {
    const places = wheelPlaces(stack(9), -1, 0, 5);
    expect(seen(places)).toBe('- - [-] 1 2');
    expect(places[3].kind).toBe('next');
    expect(seen(wheelPlaces(stack(1), -1, 0, 5))).toBe('- - [-] 1 -');
    expect(seen(wheelPlaces([], -1, -1, 3))).toBe('- [-] -');
  });

  it('makes extra places on each side for the wheel to roll in from', () => {
    const places = wheelPlaces(stack(9), 4, 5, 3, 1);
    expect(seen(places)).toBe('3 4 [5] 6 7');
    expect(places[0].offset).toBe(-2);
  });

  it('rolls the shorter way round', () => {
    expect(wheelRoll(3, 4, 9)).toBe(1); // Play: up
    expect(wheelRoll(4, 3, 9)).toBe(-1); // Prev: down
    expect(wheelRoll(8, 0, 9)).toBe(1); // from the last to the first: up, not eight places down
    expect(wheelRoll(0, 8, 9)).toBe(-1);
    expect(wheelRoll(-1, 0, 9)).toBe(1); // the first step that Play starts
    expect(wheelRoll(4, -1, 9)).toBe(0); // the stack stopped: nothing to roll to
    expect(wheelRoll(4, 4, 9)).toBe(0);
  });
});
