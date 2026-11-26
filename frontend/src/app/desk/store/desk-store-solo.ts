import { roundedBpm, tempoOfTaps, withTap } from '../tempo';
import { MAX_LEVEL, StepTarget } from '../desk-types';
import { DeskStoreSounds } from './desk-store-sounds';

/**
 * A layer of DeskStore: SOLO UP and SOLO DOWN with the keys held for them, and the rate (a tempo).
 */
export abstract class DeskStoreSolo extends DeskStoreSounds {
  /** SOLO UP: lit, a held key puts its fader to full and every other light goes dark; dark, the keys switch their faders on and off. */
  toggleSoloUp(): void {
    this.switchSolo('up');
  }

  /** SOLO DOWN: lit, a held key takes only its own fader down; the rest stays. */
  toggleSoloDown(): void {
    this.switchSolo('down');
  }

  // The two solos exclude each other. A key held when its solo goes dark is let go.
  protected switchSolo(solo: 'up' | 'down'): void {
    this.releaseHeld();
    this.soloMode.update((lit) => (lit === solo ? null : solo));
  }

  /**
   * The key under the fader of a channel or a memory goes down (true) or up again (false). While
   * a solo is lit, the fader goes to full (SOLO UP) or to zero (SOLO DOWN) while the key is held,
   * and the desk program lets every other light (SOLO UP), or this one (SOLO DOWN), go dark; let
   * go, the fader is back where it was. Not while a record key wants the key: a memory key while
   * one waits for a memory, a channel key while a sequence is recorded.
   */
  holdKey(target: StepTarget, number: number, down: boolean): void {
    if (!down) this.holdOff(target, number);
    else if (this.soloMode() && this.keyIsFree(target)) this.holdOn(target, number);
  }

  /** The same for a key of the independents: it holds the channel or the memory it is bound to. */
  holdIndependent(kind: StepTarget, key: number, down: boolean): void {
    const number =
      kind === 'channel' ? this.independents()[key - 1] : this.memoryIndependents()[key - 1];
    if (number === undefined) return;
    if (!down) this.holdOff(kind, number);
    else if (this.soloMode()) this.holdOn(kind, number);
  }

  // True when a key under a fader is free to hold its fader: no record key wants it.
  protected keyIsFree(target: StepTarget): boolean {
    const kind = this.mode().kind;
    return target === 'channel' ? kind !== 'recording' : kind === 'idle';
  }

  protected holdOn(target: StepTarget, number: number): void {
    const name = `${target} ${number}`;
    const up = this.soloMode() === 'up';
    if (this.held.has(name)) return;
    // A solo is for the light: the key of an attribute (a pan, a tilt) does not move it.
    if (target === 'channel' && this.roles().attribute[number - 1]) return;

    const level =
      target === 'channel' ? this.channels()[number - 1] : this.memoryLevels()[number - 1];
    this.held.set(name, { level, up });
    this.setLevel(target, number, up ? MAX_LEVEL : 0);
    this.holdSolo(up, target, number, true);
  }

  protected holdOff(target: StepTarget, number: number): void {
    const name = `${target} ${number}`;
    const hold = this.held.get(name);
    if (hold === undefined) return;

    this.held.delete(name);
    this.setLevel(target, number, hold.level);
    this.holdSolo(hold.up, target, number, false);
  }

  /**
   * Tells the desk program that a key is held for a solo, or let go: at once, after the fader
   * move (so that the fader is where it belongs when the solo begins and ends). The page shows
   * it at once; the desk program answers every page with the keys held.
   */
  protected holdSolo(up: boolean, target: StepTarget, number: number, held: boolean): void {
    const side = up ? 'up' : 'down';
    const others = this.solo()[side].filter(
      (key) => key.target !== target || key.number !== number,
    );
    this.solo.update((solo) => ({
      ...solo,
      [side]: held ? [...others, { target, number }] : others,
    }));
    if (this.outboxTimer !== null) this.flushOutbox();
    this.run('HoldSolo', up, target, number, held);
  }

  // Every fader a key holds goes back, as if its key were let go.
  protected releaseHeld(): void {
    for (const name of [...this.held.keys()]) {
      const [target, number] = name.split(' ');
      this.holdOff(target as StepTarget, Number(number));
    }
  }

  protected setLevel(target: StepTarget, number: number, level: number): void {
    if (target === 'channel') this.setChannel(number, level);
    else this.setMemoryLevel(number, level);
  }

  /**
   * RATE: lit, every sequence takes one step per beat of the tempo (the fader under the key and
   * TEMPO set it); dark, every sequence plays its own times. Like the master fader it is not
   * saved with the project.
   */
  toggleRate(): void {
    this.rateOn.update((on) => !on);
    this.taps = [];
    this.sendSoon('rate', 'SetRate', this.rateOn(), this.bpm());
  }

  /** Sets the tempo of the rate, 30 to 300 beats per minute (its fader); it counts while RATE is lit. */
  setBpm(bpm: number): void {
    const kept = roundedBpm(bpm);
    if (kept === this.bpm()) return;
    this.bpm.set(kept);
    this.sendSoon('rate', 'SetRate', this.rateOn(), kept);
  }

  /**
   * A tap of TEMPO, in time with the music: from the second tap on, the tempo of the rate is the
   * beat of the taps (the average of the last four beats; a pause of more than two seconds starts
   * afresh). Only while RATE is lit.
   */
  tapTempo(now: number = performance.now()): void {
    if (!this.rateOn()) return;
    this.taps = withTap(this.taps, now);
    const bpm = tempoOfTaps(this.taps);
    if (bpm !== null) this.setBpm(bpm);
  }
}
