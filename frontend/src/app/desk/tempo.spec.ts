import { MAX_BPM, MIN_BPM, TAPS_KEPT, roundedBpm, tempoOfTaps, withTap } from './tempo';

describe('the tempo of the rate', () => {
  it('is whole beats per minute, from 30 to 300', () => {
    expect(roundedBpm(127.6)).toBe(128);
    expect(roundedBpm(10)).toBe(MIN_BPM);
    expect(roundedBpm(1000)).toBe(MAX_BPM);
  });

  it('comes from the taps of TEMPO: their average beat, from the second tap on', () => {
    let taps: number[] = [];
    taps = withTap(taps, 1000);
    expect(tempoOfTaps(taps)).toBeNull(); // one tap says nothing yet
    taps = withTap(taps, 1500);
    expect(tempoOfTaps(taps)).toBe(120); // half a second: 120 beats a minute
    taps = withTap(taps, 1980); // a little early, then on time again: the average counts
    taps = withTap(taps, 2500);
    expect(tempoOfTaps(taps)).toBe(120);
  });

  it('counts only the last taps, and starts afresh after a pause of more than two seconds', () => {
    let taps: number[] = [];
    for (let time = 0; time <= 4000; time += 400) taps = withTap(taps, time);
    expect(taps.length).toBe(TAPS_KEPT);
    expect(tempoOfTaps(taps)).toBe(150);

    taps = withTap(taps, 7000); // a pause: this tap is the first again
    expect(taps).toEqual([7000]);
    taps = withTap(taps, 7250);
    expect(tempoOfTaps(taps)).toBe(240);
    expect(withTap([5000], 4000)).toEqual([4000]); // a clock that went back: afresh too
  });
});
