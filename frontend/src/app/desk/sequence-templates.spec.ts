import { MAX_SEQUENCE_STEPS } from './desk-types';
import { TEMPLATES, TemplateSettings, makeSequence, readNumbers, stepsNeeded } from './sequence-templates';

/** Numbers that look random but are the same in every run of the tests. */
function sameEveryTime(): () => number {
  let state = 12345;
  return () => {
    state = (state * 1103515245 + 12345) % 2147483648;
    return state / 2147483648;
  };
}

function settings(more: Partial<TemplateSettings>): TemplateSettings {
  return { name: 'fire', target: 'channel', numbers: [1, 2], low: 90, high: 255, time: 0.2, ...more };
}

describe('sequence templates', () => {
  it('fire: every light flickers on its own between low and high, played at random', () => {
    const made = makeSequence(settings({}), sameEveryTime());

    expect(made.random).toBe(true);
    expect(made.loop).toBe(true);
    expect(made.steps.length).toBe(16); // eight for each of the two lights
    expect(made.steps.filter((step) => step.number === 1).length).toBe(8);
    expect(made.steps.every((step) => step.target === 'channel')).toBe(true);
    // between low and high, apart from the dips, which stay above 40 % of low
    expect(made.steps.every((step) => step.value >= 36 && step.value <= 255)).toBe(true);
    expect(new Set(made.steps.map((step) => step.value)).size).toBeGreaterThan(10);
    // short, uneven times on tenths, and down longer than up
    expect(made.steps.every((step) => step.up <= 0.1 && step.wait <= 0.2 && step.down >= 0.3)).toBe(true);
    expect(made.steps.every((step) => Number.isInteger(Math.round(step.down * 10)))).toBe(true);
    expect(made.steps.every((step) => step.down > step.up)).toBe(true);
  });

  it('chase: the lights one after the other, in the order typed', () => {
    const made = makeSequence(settings({ name: 'chase', numbers: [3, 1, 2], time: 0.5 }));

    expect(made).toEqual({
      loop: true,
      random: false,
      steps: [3, 1, 2].map((number) => ({ target: 'channel', number, value: 255, up: 0, wait: 0.5, down: 0.3 })),
    });
  });

  it('wave: each light swells and fades in turn; a single light breathes all the way down', () => {
    const two = makeSequence(settings({ name: 'wave', time: 1 }));
    expect(two.steps).toEqual([
      { target: 'channel', number: 1, value: 255, up: 1, wait: 0, down: 1 },
      { target: 'channel', number: 2, value: 255, up: 1, wait: 0, down: 1 },
    ]);

    const one = makeSequence(settings({ name: 'wave', target: 'memory', numbers: [5], time: 2 }));
    expect(one.steps).toEqual([
      { target: 'memory', number: 5, value: 255, up: 2, wait: 0, down: 2 },
      { target: 'memory', number: 5, value: 0, up: 0, wait: 2, down: 0 }, // dark while the first fades
    ]);
  });

  it('strobe: a flash as short as a step can be, then dark', () => {
    const made = makeSequence(settings({ name: 'strobe', numbers: [4], high: 200, time: 0.5 }));

    expect(made.steps).toEqual([
      { target: 'channel', number: 4, value: 200, up: 0, wait: 0, down: 0 },
      { target: 'channel', number: 4, value: 0, up: 0, wait: 0.5, down: 0 },
    ]);
  });

  it('storm: pauses, strikes and faint flickers, played at random', () => {
    const made = makeSequence(settings({ name: 'lightning', numbers: [1, 2], low: 60, time: 3 }), sameEveryTime());

    expect(made.random).toBe(true);
    expect(made.steps.length).toBe(9);
    const pauses = made.steps.filter((step) => step.value === 0);
    expect(pauses.length).toBe(3);
    expect(pauses.every((step) => step.wait >= 1.5 && step.wait <= 4.5)).toBe(true);
    expect(made.steps.filter((step) => step.value === 255).length).toBe(2); // a full strike for each light
  });

  it('a template never makes more steps than a sequence can hold', () => {
    for (const template of TEMPLATES) {
      for (const lights of [1, 2, 10, 33, 49]) {
        const needed = stepsNeeded(template.name, lights);
        const numbers = Array.from({ length: lights }, (_, index) => index + 1);
        if (needed > MAX_SEQUENCE_STEPS) continue; // the window refuses those
        expect(makeSequence(settings({ name: template.name, numbers }), sameEveryTime()).steps.length).toBe(needed);
      }
    }
    expect(stepsNeeded('fire', 49)).toBe(98); // many lights share the 99 steps: two each
    expect(stepsNeeded('fire', 50)).toBeGreaterThan(MAX_SEQUENCE_STEPS);
  });

  it('reads the numbers as typed: lists, ranges, each once', () => {
    expect(readNumbers('1, 2', 512)).toEqual([1, 2]);
    expect(readNumbers(' 4-1 , 9,2 ', 512)).toEqual([4, 3, 2, 1, 9]);
    expect(readNumbers('', 512)).toContain('Type the numbers');
    expect(readNumbers('1, x', 512)).toContain('"x" is no number');
    expect(readNumbers('120-130', 128)).toBe('The numbers go from 1 to 128.');
    expect(readNumbers('0', 512)).toBe('The numbers go from 1 to 512.');
  });
});
