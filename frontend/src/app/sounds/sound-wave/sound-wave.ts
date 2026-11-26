import {
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { DeskStore } from '../../desk/desk-store';
import { fetchSoundWave } from '../sound-files';
import { Sound, SoundPlayback, SoundWave, isTrack, timeText } from '../sounds';

/** A bar of the wave and the room after it, in pixels of the screen. */
const BAR = 2;
const GAP = 1;

/** How far the keys move a sound that plays: the arrows, and Page Up and Page Down. */
const STEP_SECONDS = 5;
const PAGE_SECONDS = 30;

/**
 * The line's clock (see SoundWaveStrip): a word of the desk program further from it than this, in
 * seconds, is a jump (a double click, a new start, a loop going round) and puts the line there at once;
 * a nearer one pulls it this share of the way, never back; without a new word the line stops after
 * this long, in milliseconds (a track that the mixer paused).
 */
const JUMP_SECONDS = 1;
const PULL = 0.3;
const RUN_ON_MS = 1500;

/**
 * The whole file of a sound as a wave, with a line where the sound is while it plays: the time it
 * is at on the left, how long the file is on the right. What lies before its start and after its
 * end (From and To of the editor) is dim; what it has played of the part it plays is brighter than
 * what is still to come. The desk program tells where the sound is about 10 times a second (a track
 * as often as the mixer tells it); the line has a clock of its own that runs at the sound's speed and
 * takes those words in gently, so that it glides on and never trembles back.
 *
 * A track of the mixer has no file here: its wave is empty, a flat line, with the line where it is
 * and its length as the mixer tells while it plays. While a sound plays, a double click on the wave
 * plays it from there; with the focus on the wave, the arrows go 5 seconds back and on, Page Up and
 * Page Down 30, Home to its start.
 */
@Component({
  host: {
    role: 'group',
    '[attr.aria-label]': 'said()',
  },
  selector: 'lm-sound-wave',
  styleUrl: './sound-wave.css',
  templateUrl: './sound-wave.html',
})
export class SoundWaveStrip {
  readonly sound = input.required<Sound>();
  readonly playback = input<SoundPlayback | undefined>();

  private readonly store = inject(DeskStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly canvas = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');

  /** The wave of the sound's file; null while it is fetched, when it cannot be had, and for a track. */
  private readonly wave = signal<SoundWave | null>(null);

  protected readonly playing = computed(() => this.playback()?.playing ?? false);

  /** True for a track of the mixer: no wave of its own. */
  private readonly track = computed(() => isTrack(this.sound()));

  /** Where it starts: a file at From of the editor, a track at its start. */
  private readonly start = computed(() => (this.track() ? 0 : this.sound().start));

  /** How long the file is, in seconds (a track: as the mixer said; 0 while that is not known). */
  private readonly seconds = computed(() => {
    if (this.track()) return this.playback()?.length ?? 0;
    const sound = this.sound();
    return sound.seconds > 0 ? sound.seconds : (this.wave()?.seconds ?? 0);
  });

  /** Where the sound is: while it plays the desk program's word, else where it starts. */
  protected readonly now = computed(() => {
    const playback = this.playback();
    return timeText(playback?.playing ? playback.position : this.start());
  });

  /** How long the file is; empty for a track whose length is not known yet. */
  protected readonly total = computed(() => (this.seconds() > 0 ? timeText(this.seconds()) : ''));

  /** For screen readers: "Sound 3 plays, at 1:23.4 of 3:05.2". */
  protected readonly said = computed(
    () =>
      `Sound ${this.sound().number} ${this.playing() ? 'plays' : 'is quiet'}, at ${this.now()}${this.total() ? ` of ${this.total()}` : ''}`,
  );

  /** For the keys on the wave, while the sound plays: where it is, and how long it is, in whole seconds. */
  protected readonly at = computed(() => Math.round(this.playback()?.position ?? 0));
  protected readonly length = computed(() => Math.max(Math.round(this.seconds()), this.at()));

  // The line's clock: where the sound was (clockPosition, seconds of its file) at a moment of the page
  // (clockAt, performance.now()); what the desk program told last; where the line stood last, unwrapped
  // (it goes on from there, never back, but for a jump).
  private clockAt = 0;
  private clockPosition = 0;
  private told = Number.NaN;
  private drawn = Number.NEGATIVE_INFINITY;
  private frame = 0;
  private readonly still =
    typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;

  constructor() {
    // the wave of each file, once; a file that cannot be read shows a flat line, a track as well
    effect((onCleanup) => {
      const hash = this.sound().hash;
      const track = this.track();
      let gone = false;
      this.wave.set(null);
      if (!track && hash) {
        void fetchSoundWave(hash).then((wave) => {
          if (!gone) this.wave.set(wave);
        });
      }
      onCleanup(() => (gone = true));
    });
    effect(() => {
      const playback = this.playback();
      untracked(() => this.take(playback));
    });

    const injector = inject(Injector);
    const destroy = inject(DestroyRef);
    afterNextRender(() => {
      // drawn again when anything it shows changes, and every frame while the sound plays
      effect(
        () => {
          this.wave();
          this.sound();
          this.playback();
          this.drawSoon();
        },
        { injector },
      );
      if (typeof ResizeObserver === 'undefined') return; // not in a browser, for example in the tests
      const observer = new ResizeObserver(() => this.drawSoon());
      observer.observe(this.canvas().nativeElement);
      destroy.onDestroy(() => observer.disconnect());
    });
    destroy.onDestroy(() => cancelAnimationFrame(this.frame));
  }

  /** A double click on the wave of a sound that plays: it plays on from there. */
  protected onDoubleClick(event: MouseEvent): void {
    const seconds = this.seconds();
    const box = (event.currentTarget as HTMLElement).getBoundingClientRect();
    if (!this.playing() || seconds <= 0 || box.width <= 0) return;
    const part = Math.min(1, Math.max(0, (event.clientX - box.left) / box.width));
    this.store.seekSound(this.sound().number, part * seconds);
  }

  /** The keys on the wave of a sound that plays: back and on, or to its start. */
  protected onKey(event: KeyboardEvent): void {
    if (!this.playing() || event.defaultPrevented) return;
    const at = this.playback()?.position ?? 0;
    const to =
      event.key === 'ArrowRight' || event.key === 'ArrowUp'
        ? at + STEP_SECONDS
        : event.key === 'ArrowLeft' || event.key === 'ArrowDown'
          ? at - STEP_SECONDS
          : event.key === 'PageUp'
            ? at + PAGE_SECONDS
            : event.key === 'PageDown'
              ? at - PAGE_SECONDS
              : event.key === 'Home'
                ? this.start()
                : null;
    if (to === null) return;
    event.preventDefault();
    this.store.seekSound(this.sound().number, Math.max(this.start(), to));
  }

  // The next frame draws; while the sound plays (and motion is welcome) the one after it too.
  private drawSoon(): void {
    if (this.frame || typeof requestAnimationFrame !== 'function') return;
    this.frame = requestAnimationFrame(() => {
      this.frame = 0;
      this.draw();
      if (this.playing() && !this.still) this.drawSoon();
    });
  }

  /** What the desk program told of the sound: the line's clock takes it in (see the constants above). */
  private take(playback: SoundPlayback | undefined): void {
    if (!playback?.playing) {
      this.told = Number.NaN;
      this.drawn = Number.NEGATIVE_INFINITY;
      return;
    }
    if (playback.position === this.told) return; // nothing new: the clock goes on
    const now = performance.now();
    const first = Number.isNaN(this.told);
    const guess = this.clock(now);
    const off = playback.position - guess;
    const jump = first || Math.abs(off) > JUMP_SECONDS;
    this.told = playback.position;
    this.clockPosition = jump ? playback.position : guess + off * PULL;
    this.clockAt = now;
    if (jump) this.drawn = Number.NEGATIVE_INFINITY; // it may go back: a jump
  }

  /** The clock: where the sound is now by the last word taken in, at its speed since (not on and on). */
  private clock(now: number): number {
    const speed = this.track() ? 1 : this.sound().speed;
    return this.clockPosition + (Math.min(now - this.clockAt, RUN_ON_MS) / 1000) * speed;
  }

  /** Where the sound is now, in seconds of its file: the line's clock, never back but for a jump. */
  private position(): number {
    const playback = this.playback();
    const sound = this.sound();
    const track = this.track();
    const start = this.start();
    if (!playback?.playing) return start;
    if (this.still) return playback.position;
    const end = track ? this.seconds() : (sound.end ?? sound.seconds);
    let at = Math.max(this.clock(performance.now()), this.drawn);
    this.drawn = at;
    if (end > 0 && at > end)
      at = !track && sound.loop && end > start ? start + ((at - start) % (end - start)) : end;
    return at;
  }

  private draw(): void {
    const canvas = this.canvas().nativeElement;
    const box = canvas.getBoundingClientRect();
    if (box.width === 0 || box.height === 0) return; // not laid out, or hidden
    const ratio = window.devicePixelRatio || 1;
    const width = Math.max(1, Math.round(box.width * ratio));
    const height = Math.max(1, Math.round(box.height * ratio));
    if (canvas.width !== width || canvas.height !== height) {
      canvas.width = width;
      canvas.height = height;
    }
    const paint = canvas.getContext('2d');
    if (!paint) return;

    const style = getComputedStyle(this.host.nativeElement);
    const sound = this.sound();
    const tone = style.getPropertyValue('--sound').trim() || '#2ad4c8';
    const line = style.getPropertyValue('--legend').trim() || '#f1f3f6';
    paint.clearRect(0, 0, width, height);

    const seconds = this.seconds();
    const xOf = (time: number) =>
      seconds > 0 ? Math.min(1, Math.max(0, time / seconds)) * width : 0;
    const startX = xOf(this.start());
    const endX = this.track() || sound.end == null ? width : xOf(sound.end);
    const playing = this.playing();
    const atX = playing ? xOf(this.position()) : startX;

    const middle = height / 2;
    const wave = this.wave();
    if (!wave || wave.peaks.length === 0) {
      // no wave to show (a track, a file that cannot be read): a flat line, brighter where it has played
      const thin = Math.max(1, Math.round(ratio));
      paint.fillStyle = tone;
      paint.globalAlpha = 0.35;
      paint.fillRect(0, Math.floor(middle), width, thin);
      if (playing) {
        paint.globalAlpha = 0.9;
        paint.fillRect(0, Math.floor(middle), atX, thin);
      }
    } else {
      const bar = Math.max(1, Math.round(BAR * ratio));
      const step = bar + Math.max(1, Math.round(GAP * ratio));
      const parts = wave.peaks.length;
      const reach = middle - ratio;
      paint.fillStyle = tone;
      for (let x = 0; x < width; x += step) {
        // the parts under this bar: the loudest of them
        const first = Math.floor((x / width) * parts);
        const after = Math.max(first + 1, Math.floor(((x + step) / width) * parts));
        let peak = 0;
        let level = 0;
        for (let part = first; part < after && part < parts; part++) {
          peak = Math.max(peak, wave.peaks[part]);
          level = Math.max(level, wave.levels[part]);
        }
        const outside = x + bar <= startX || x >= endX;
        const played = playing && x < atX;
        const peakHeight = Math.max(ratio, (peak / 255) * reach);
        const levelHeight = Math.max(ratio, (level / 255) * reach);
        paint.globalAlpha = outside ? 0.14 : played ? 0.55 : 0.32;
        paint.fillRect(x, middle - peakHeight, bar, peakHeight * 2);
        paint.globalAlpha = outside ? 0.3 : played ? 1 : 0.7;
        paint.fillRect(x, middle - levelHeight, bar, levelHeight * 2);
      }
    }

    // where it is: a line over the whole height
    paint.globalAlpha = playing ? 1 : 0.45;
    paint.fillStyle = playing ? line : tone;
    const thick = Math.max(1, Math.round(2 * ratio));
    paint.fillRect(
      Math.min(width - thick, Math.max(0, Math.round(atX - thick / 2))),
      0,
      thick,
      height,
    );
    paint.globalAlpha = 1;
  }
}
