import { DestroyRef, Service, effect, inject, signal, untracked } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { actionLamp } from './action-lamps';
import { DeskConnection } from './desk-connection';
import { DeskStore } from './desk-store';
import { Keyboard } from './keyboard';
import type {
  MidiLamp,
  MidiMapping,
  MidiMonitorLine,
  MidiPages,
  MidiRole,
  MidiSetup,
  MidiState,
} from './midi';

/** How many incoming messages the list keeps. */
const KEPT_LINES = 60;

/** A page that is used says so at most this often (ms): it presses the keys of the controllers then. */
const CLAIM_EVERY_MS = 1000;

/** The lamps that changed go to the desk program together, at most this often (ms). */
const LAMPS_EVERY_MS = 40;

/**
 * The MIDI controllers of the PC, as this page knows them (see midi.ts). Every page hears what the
 * desk program says about them; the page on the PC that was used last also presses their keys (as
 * its keyboard shortcuts would: keyboard.ts) and tells the desk program how those keys are lit and
 * which pages of faders it shows, for the lights and the slots of the controllers.
 */
@Service()
export class DeskMidi {
  private readonly connection = inject(DeskConnection);
  private readonly store = inject(DeskStore);
  private readonly keyboard = inject(Keyboard);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  /** The MIDI of the PC; null before it is known, and from a desk program without MIDI. */
  readonly state = signal<MidiState | null>(null);

  /** True once the desk program has answered whether it knows MIDI. */
  readonly answered = signal(false);

  /** The last messages that came in, the newest last, while a page shows the list (see watch). */
  readonly lines = signal<readonly MidiMonitorLine[]>([]);

  /** True while this page presses the keys of the controllers. */
  readonly controls = signal(false);

  /** Why the last change was refused; empty after one that went through. */
  readonly problem = signal('');

  // The keys the controllers have, whose lamps the desk program needs; the view shown, for the links.
  private readonly keys = signal<readonly string[]>([]);
  private readonly path = signal('/');

  // The lamps the desk program has, as "3" or "3 blink"; and those that wait to go.
  private readonly sent = new Map<string, string>();
  private readonly waiting = new Map<string, MidiLamp>();
  private lampTimer: ReturnType<typeof setTimeout> | null = null;
  private claimedAt = Number.NEGATIVE_INFINITY;
  private started = false;

  constructor() {
    const connection = this.connection;
    connection.on<[MidiState]>('MidiChanged', (state) => this.state.set(state));
    connection.on<[MidiMonitorLine[]]>('MidiMessages', (lines) =>
      this.lines.update((kept) => [...kept, ...lines].slice(-KEPT_LINES)),
    );
    connection.on<[string, boolean]>('MidiKey', (key, down) => this.keyboard.midiKey(key, down));
    connection.on<[MidiRole]>('MidiRole', (role) => {
      this.sent.clear();
      this.waiting.clear();
      this.keys.set(role.keys ?? []);
      this.controls.set(role.controls);
    });
    connection.whenConnected(() => void this.connected());

    // While this page presses the keys: how they are lit, as they change.
    effect(() => {
      if (!this.controls()) return;
      const path = this.path();
      const lamps = this.keys().map((key): MidiLamp => ({
        key,
        ...actionLamp(key, this.store, path),
      }));
      untracked(() => this.sendLamps(lamps));
    });

    // While this page presses the keys: the pages of faders it shows, for the slots.
    effect(() => {
      if (!this.controls()) return;
      const pages: MidiPages = {
        firstChannel: this.store.firstChannel(),
        channelsPerPage: this.store.channelsPerPage(),
        firstMemory: this.store.firstMemory(),
        memoriesPerPage: this.store.memoriesPerPage(),
        firstEffect: this.store.firstEffect(),
        effectsPerPage: this.store.stripsPerRow(),
      };
      untracked(() => this.connection.send('ReportMidiPages', pages));
    });
  }

  /** Starts: a page on the PC takes the keys of the controllers whenever it is used. The page calls it once. */
  start(): void {
    if (this.started || typeof document === 'undefined') return;
    this.started = true;

    this.path.set(pathOf(this.router.url));
    const navigations = this.router.events
      .pipe(filter((event) => event instanceof NavigationEnd))
      .subscribe((event) => this.path.set(pathOf(event.urlAfterRedirects)));

    const used = (): void => this.claim();
    window.addEventListener('focus', used);
    document.addEventListener('pointerdown', used, true);
    document.addEventListener('keydown', used, true);
    this.destroyRef.onDestroy(() => {
      navigations.unsubscribe();
      window.removeEventListener('focus', used);
      document.removeEventListener('pointerdown', used, true);
      document.removeEventListener('keydown', used, true);
      if (this.lampTimer !== null) clearTimeout(this.lampTimer);
    });

    if (this.connection.online()) void this.connected();
  }

  /** Uses a device (the desk program opens it), or leaves it to other programs. */
  setDevice(name: string, use: boolean): Promise<void> {
    return this.change('SetMidiDevice', name, use);
  }

  /** Every mapping, as the panel has it now. */
  setMappings(mappings: readonly MidiMapping[]): Promise<void> {
    return this.change('SetMidiMappings', mappings);
  }

  /** Learn: the next control that moves works this key or fader; null stops. */
  learn(target: string | null): Promise<void> {
    return this.change('LearnMidi', target);
  }

  /** Gives a device the ready-made mapping of its controller. */
  usePreset(preset: string, device: string): Promise<void> {
    return this.change('UseMidiPreset', preset, device);
  }

  /** Shows the list of incoming messages (with the last ones at once), or no more. */
  async watch(on: boolean): Promise<void> {
    const lines = await this.connection.invoke<MidiMonitorLine[] | undefined>('WatchMidi', on);
    if (on) this.lines.set((lines ?? []).slice(-KEPT_LINES));
  }

  /** The MIDI setup of the PC, for Export. */
  setup(): Promise<MidiSetup> {
    return this.connection.invoke<MidiSetup>('GetMidiSetup');
  }

  /** A setup from a file takes the place of the PC's (Import). */
  async takeSetup(setup: MidiSetup): Promise<void> {
    this.state.set(await this.connection.invoke<MidiState>('SetMidiSetup', setup));
  }

  // A change of the MIDI: the desk program answers with the MIDI after it, or says why it refuses.
  private async change(method: string, ...args: unknown[]): Promise<void> {
    try {
      const state = await this.connection.invoke<MidiState | undefined>(method, ...args);
      if (state) this.state.set(state);
      this.problem.set('');
    } catch (error) {
      this.problem.set(error instanceof Error ? error.message : String(error));
    }
  }

  // Connected (again): the MIDI of the PC, and a page on the PC takes the keys.
  private async connected(): Promise<void> {
    try {
      this.state.set((await this.connection.invoke<MidiState | undefined>('GetMidi')) ?? null);
    } catch {
      this.state.set(null); // a desk program from before MIDI
      return;
    } finally {
      this.answered.set(true);
    }
    this.claimedAt = Number.NEGATIVE_INFINITY;
    this.claim();
  }

  // This page is used: it presses the keys of the controllers from now on (only a page on the PC does).
  private claim(): void {
    const now = performance.now();
    if (
      now - this.claimedAt < CLAIM_EVERY_MS ||
      this.state() === null ||
      this.store.lan()?.local === false
    )
      return;
    this.claimedAt = now;
    this.connection.send('ClaimMidi');
  }

  // The lamps that changed since the desk program last heard of them, a moment later, together.
  private sendLamps(lamps: readonly MidiLamp[]): void {
    for (const lamp of lamps) {
      const said = `${lamp.level}${lamp.blink ? ' blink' : ''}`;
      if (this.sent.get(lamp.key) === said) continue;
      this.sent.set(lamp.key, said);
      this.waiting.set(lamp.key, lamp);
    }
    if (this.waiting.size === 0 || this.lampTimer !== null) return;
    this.lampTimer = setTimeout(() => {
      this.lampTimer = null;
      const changed = [...this.waiting.values()];
      this.waiting.clear();
      if (changed.length > 0 && this.controls()) this.connection.send('ReportMidiLamps', changed);
    }, LAMPS_EVERY_MS);
  }
}

// The path of a view's address, without what follows it: "/settings#outputs" is "/settings".
function pathOf(url: string): string {
  return url.split(/[?#]/)[0] || '/';
}
