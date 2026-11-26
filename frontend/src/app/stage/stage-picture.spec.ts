import { PICTURE_SIDE, scaledSize } from './stage-picture';

describe('a picture under the stage plan', () => {
  it('is made smaller to 1600 pixels on its longer side, keeping its proportions', () => {
    expect(PICTURE_SIDE).toBe(1600);
    expect(scaledSize(4000, 3000, PICTURE_SIDE)).toEqual({ width: 1600, height: 1200 });
    expect(scaledSize(1000, 5000, PICTURE_SIDE)).toEqual({ width: 320, height: 1600 });
  });

  it('keeps its size when it is small enough', () => {
    expect(scaledSize(800, 600, PICTURE_SIDE)).toEqual({ width: 800, height: 600 });
    expect(scaledSize(0, 0, PICTURE_SIDE)).toEqual({ width: 1, height: 1 });
  });
});
