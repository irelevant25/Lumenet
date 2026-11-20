import { rateOfStep, stepOfRate } from './rate-wheel';

describe('the steps of the rate wheel', () => {
  it('go from x0.1 to x10, 40 each way from x1, each about 6 % faster', () => {
    expect(rateOfStep(0)).toBe(1);
    expect(rateOfStep(1)).toBe(1.06);
    expect(rateOfStep(-1)).toBe(0.94);
    expect(rateOfStep(12)).toBe(2);
    expect(rateOfStep(-12)).toBe(0.5);
    expect(rateOfStep(40)).toBe(10);
    expect(rateOfStep(-40)).toBe(0.1);
    expect(rateOfStep(55)).toBe(10); // no further
    expect(rateOfStep(-55)).toBe(0.1);
  });

  it('never get slower towards x10, and a rate of the wheel finds its step again', () => {
    for (let step = -40; step < 40; step++) {
      expect(rateOfStep(step + 1)).toBeGreaterThanOrEqual(rateOfStep(step));
    }
    // To two decimals two steps near x0.1 can have the same rate; from x0.2 up each has its own.
    for (let step = -28; step <= 40; step++) {
      expect(stepOfRate(rateOfStep(step))).toBe(step);
    }
  });
});
