import { decibelText, decibelsAt, placeOf } from './sounds';

describe('levels in decibels', () => {
  it('stand on a fader as on a MIDI controller: the gain is the place squared', () => {
    expect(decibelsAt(255)).toBe(0);
    expect(decibelsAt(128)).toBe(-12);
    expect(decibelsAt(1)).toBe(-96.3);
    expect(decibelsAt(0)).toBe(-100);
    expect(placeOf(0)).toBe(255);
    expect(placeOf(-12)).toBe(128);
    expect(placeOf(-100)).toBe(0);
    expect(placeOf(5)).toBe(255); // over 0 dB: the top
  });

  it('are shown to a tenth of a decibel, never as "minus 0"', () => {
    expect(decibelText(0)).toBe('0 dB');
    expect(decibelText(-0.04)).toBe('0 dB');
    expect(decibelText(-12.5)).toBe('-12.5 dB');
    expect(decibelText(-100)).toBe('-100 dB');
    expect(Object.is(decibelsAt(254.9), 0)).toBe(true);
  });
});
