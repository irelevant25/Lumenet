import { MAX_LEVEL } from '../desk-types';
import {
  Effect,
  EFFECT_INDEPENDENT_COUNT,
  EffectControl,
  EffectPlayback,
  EffectPreview,
  MAX_EFFECTS,
} from '../../effects/effects';
import {
  EFFECT_SEND_DELAY_MS,
  EFFECT_TOUCH_MS,
  emptyPlayback,
  withItem,
} from './desk-store-helpers';
import { DeskStoreFaders } from './desk-store-faders';

/**
 * A layer of DeskStore: the effects, their pages, keys, faders and live values, and their independent keys.
 */
export abstract class DeskStoreEffects extends DeskStoreFaders {
  /** An effect key of the independents starts or stops its effect, as the key of the effect does. Solo leaves it alone. */
  toggleEffectIndependent(key: number): void {
    this.toggleEffect(this.effectIndependents()[key - 1]);
  }

  /** Binds an effect key of the independents to another effect. */
  setEffectIndependent(key: number, effect: number): void {
    if (key < 1 || key > EFFECT_INDEPENDENT_COUNT || effect < 1 || effect > MAX_EFFECTS) return;
    this.effectIndependents.update((effects) => withItem(effects, key - 1, effect));
    this.run('SetEffectIndependent', key, effect);
  }

  /** Shows the page of effects that starts at `first`, on the main view and the Effects view. */
  showEffectPage(first: number): void {
    this.wantedEffect.set(first);
  }

  /**
   * Stores an effect, new or changed. The page shows it at once; changes of one effect that follow
   * each other quickly (a number typed, a list gone through) go out together.
   */
  setEffect(effect: Effect): void {
    this.effects.update((all) =>
      [...all.filter((item) => item.number !== effect.number), effect].sort(
        (a, b) => a.number - b.number,
      ),
    );
    const waiting = this.effectTimers.get(effect.number);
    if (waiting !== undefined) clearTimeout(waiting);
    this.effectTimers.set(
      effect.number,
      setTimeout(() => {
        this.effectTimers.delete(effect.number);
        this.connection.invoke('SetEffect', effect).catch((error: Error) => {
          this.say(error.message);
          void this.fetchEffects();
        });
      }, EFFECT_SEND_DELAY_MS),
    );
  }

  /** Takes an effect out of the project; it stops. */
  deleteEffect(number: number): void {
    this.effects.update((all) => all.filter((item) => item.number !== number));
    this.run('DeleteEffect', number);
  }

  /** Stores a copy of an effect under another number. */
  copyEffect(from: number, to: number): void {
    this.run('CopyEffect', from, to);
  }

  /** Stores the live speed, size and spread of an effect's playback in the effect (Keep). */
  keepEffect(number: number): void {
    this.run('KeepEffect', number);
  }

  /**
   * The key of an effect: moves its fader up to full in the effect's fade-in time, which starts it,
   * or down to zero in its fade-out time, which stops it.
   */
  toggleEffect(number: number): void {
    if (!this.effectsByNumber().has(number)) {
      this.say(
        `There is no effect ${number} yet. On the Effects view, its key offers the kinds to make it.`,
      );
      return;
    }
    this.effectControl(number, 'toggle');
  }

  /** The fader of an effect, 0 to 255: from zero it starts the effect, at zero it stops it. */
  setEffectLevel(number: number, level: number): void {
    this.effectControl(number, 'level', (level / MAX_LEVEL) * 100);
  }

  /**
   * Works the playback of an effect (SetEffectControl in the desk program): "go", "toggle" and
   * "reset" are answered with the playback; "level" (per cent), "speed" (BPM), "size" and "spread"
   * (per cent) move like faders: the page shows them at once and sends the newest only.
   */
  effectControl(number: number, control: EffectControl, value = 0): void {
    if (control === 'level' || control === 'speed' || control === 'size' || control === 'spread') {
      this.touchEffect(number, control, value);
      this.sendSoon(`effect ${number} ${control}`, 'SetEffectControl', number, control, value);
      return;
    }
    this.connection
      .invoke('SetEffectControl', number, control, value)
      .then((playback) => {
        if (playback) this.takeEffectPlaybacks([playback], false);
      })
      .catch((error: Error) => this.say(error.message));
  }

  /** One cycle of an effect as the desk program would play it, for the preview; null if it cannot be had. */
  async previewEffect(effect: Effect): Promise<EffectPreview | null> {
    try {
      return (await this.connection.invoke('PreviewEffect', effect)) ?? null;
    } catch {
      return null;
    }
  }

  // The playback shows a moved fader or live value at once; a fader that leaves zero starts the
  // effect and one that comes to zero stops it, as the desk program does.
  protected touchEffect(
    number: number,
    control: 'level' | 'speed' | 'size' | 'spread',
    value: number,
  ): void {
    this.effectTouched.set(`${number} ${control}`, Date.now());
    const old = this.effectPlaybacks().get(number) ?? emptyPlayback(number);
    const exists = this.effectsByNumber().has(number);
    let changed: EffectPlayback;
    if (control === 'level') {
      const level = Math.round((value * MAX_LEVEL) / 100);
      const running = exists && level > 0;
      changed = { ...old, level, running, fading: false };
    } else {
      changed = { ...old, [control]: control === 'speed' ? value : Math.round(value) };
    }
    this.effectPlaybacks.update((all) => new Map(all).set(number, changed));
  }

  // Playbacks from the desk program. A fader or live value this page moved a moment ago keeps the
  // page's value: the desk program's may still be the one from before. "whole": the list of every
  // playback (Playback, DeskChanged); else only these change.
  protected takeEffectPlaybacks(playbacks: readonly EffectPlayback[], whole: boolean): void {
    const now = Date.now();
    const old = this.effectPlaybacks();
    const kept = whole ? new Map<number, EffectPlayback>() : new Map(old);
    for (const playback of playbacks) {
      const mine = old.get(playback.number);
      const recent = (control: string) =>
        now - (this.effectTouched.get(`${playback.number} ${control}`) ?? 0) < EFFECT_TOUCH_MS;
      kept.set(
        playback.number,
        mine
          ? {
              ...playback,
              level: recent('level') ? mine.level : playback.level,
              speed: recent('speed') ? mine.speed : playback.speed,
              size: recent('size') ? mine.size : playback.size,
              spread: recent('spread') ? mine.spread : playback.spread,
            }
          : playback,
      );
    }
    this.effectPlaybacks.set(kept);
  }

  protected async fetchEffects(): Promise<void> {
    try {
      this.effects.set((await this.connection.invoke('GetEffects')) ?? []);
      this.effectsKnown.set(true);
    } catch {
      // A desk program from before effects. The Effects view says so.
      this.effects.set([]);
      this.effectsKnown.set(false);
    }
  }
}
