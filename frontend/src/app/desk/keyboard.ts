import { DestroyRef, Service, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ActionContext, DESK_ACTIONS, SIDED_KEYS, actionWithId, pressOf, shortcutOf, shortcutsOf, slotKey } from './desk-actions';
import { DEFAULT_SHORTCUTS, DeskSettings } from './desk-settings';
import { DeskStore } from './desk-store';

/** Where the keyboard shortcuts are (the Keyboard panel of Settings); listening comes back to them. */
const KEYBOARD_URL = '/settings';

/** Where the MIDI controllers are set; listening for them comes back there. */
const MIDI_URL = '/midi';

/** Two presses on the same key within this many milliseconds are a double click. */
const DOUBLE_PRESS_MS = 250;

/** How long after a key or fader was picked the browser's double click of that pick is passed over (ms). */
const PICKED_DOUBLE_CLICK_MS = 600;

/**
 * The keyboard: the shortcuts of the Keyboard panel of Settings press the keys of the desk, a shortcut can be
 * recorded for a key, and "listening" finds a key by a click on it.
 *
 * - A shortcut presses its key on the page, as a click would, when the key is there and not
 *   locked; otherwise it does what the key does (see desk-actions.ts). Shortcuts rest while a
 *   text field has the keyboard (a note is being typed) and while a window such as Load is open.
 * - Recording: the next key pressed (with Ctrl, Alt, Shift held as wanted) becomes the shortcut
 *   of one key of the desk. A shortcut that another key had moves over. Esc stops recording.
 * - Left and right: a shortcut names the Ctrl and the Alt it was recorded with, left or right
 *   ("Left Ctrl + Z"); the keyboard notes which of them are down from their own presses. A
 *   shortcut kept without a side (Ctrl + Z, as Undo has it at first) is pressed by either, unless
 *   another key has the shortcut with that side. AltGr is the right Alt; a shortcut recorded with
 *   it before the sides were told apart (Ctrl + Alt + ...) still answers to it.
 * - While a solo is lit, the shortcut of a key under a fader or of the independents holds that key's
 *   fader (at full or at zero) for as long as it is held.
 * - Listening: the desk is shown, and a double click on one of its keys does not press it but
 *   picks it, and Settings comes back with that key chosen in its Keyboard panel. A single click
 *   does what it always does, a moment later (when no second click has come): it opens another
 *   view, another page of faders, or presses a key, so that the key to pick can be found first.
 *   Enter on a key picks it at once. Esc stops listening. Listening for the MIDI view, a fader can
 *   be picked too (a press on it does not move it then), and the MIDI view comes back with the key
 *   or fader shown in its list.
 */
@Service()
export class Keyboard {
  private readonly settings = inject(DeskSettings);
  private readonly store = inject(DeskStore);
  private readonly router = inject(Router);

  /** True while the next click on a key of the desk only picks that key for the Keyboard panel. */
  readonly listening = signal(false);
  /** The key of the desk chosen in the Keyboard panel: by listening, or in its list. */
  readonly picked = signal<string | null>(null);
  /** Who listens: the Keyboard panel for a key, or the MIDI view for a key or a fader. */
  readonly pickedFor = signal<'keyboard' | 'midi'>('keyboard');
  /** The key or fader of the desk picked by listening for the MIDI view ("memory.5", "fader.channel.12"). */
  readonly midiPicked = signal<string | null>(null);
  /** The key whose shortcut is being recorded; null while none is. */
  readonly recording = signal<string | null>(null);
  /** What the last recording did besides, like "Ctrl + 1 was the shortcut of Clear before." */
  readonly lastMove = signal('');

  private started = false;

  // A key was pressed by a pointer while listening: the click that follows must not press it.
  private swallowClick = false;

  // While listening: the key pressed once, waiting for a second press (a double click picks it);
  // when none comes, its click is made then.
  private pending: { id: string; element: HTMLElement; timer: ReturnType<typeof setTimeout> } | null = null;

  // True while the click of a single press is made, a moment after the press: it is let through.
  private replaying = false;

  // When a key or fader was last picked by listening (performance.now()): the double click of that
  // pick, which the browser sends after it, is passed over.
  private pickedAt = Number.NEGATIVE_INFINITY;

  // While a solo is lit: the keys of the keyboard held down now that hold a key of the desk, by the
  // physical key (KeyboardEvent.code), with the name of that key of the desk.
  private readonly held = new Map<string, string>();

  // The Ctrl and Alt keys held down now, by their codes ("ControlLeft"): the browser says only that a
  // Ctrl is held, so the presses of the keys themselves tell which.
  private readonly sides = new Set<string>();

  // While a solo is lit: the keys of the desk that keys of MIDI controllers hold down now.
  private readonly midiHeld = new Set<string>();

  // The keys of the desk that keys of the pages shown ("slot.channel.3") pressed: let go are those,
  // also when the page has moved on meanwhile.
  private readonly midiSlots = new Map<string, string>();

  private readonly destroyRef = inject(DestroyRef);

  /** Starts listening to the keyboard and to clicks. The page calls it once. */
  start(): void {
    if (this.started || typeof document === 'undefined') return;
    this.started = true;

    const keyDown = (event: KeyboardEvent): void => this.onKeyDown(event);
    const keyUp = (event: KeyboardEvent): void => this.onKeyUp(event);
    const pointerDown = (event: PointerEvent): void => this.onPointerDown(event);
    // The click of a press comes right after the pointer goes up; a locked key gets none, and
    // then the next click (Enter on a key) must not be taken for it.
    const pointerUp = (): void => {
      if (this.swallowClick) setTimeout(() => (this.swallowClick = false));
    };
    const click = (event: MouseEvent): void => this.onClick(event);
    const doubleClick = (event: MouseEvent): void => this.onDoubleClick(event);
    // A key let go while the window had no focus says nothing: when it comes back, none is held.
    const away = (): void => this.sides.clear();
    document.addEventListener('keydown', keyDown, true);
    document.addEventListener('keyup', keyUp, true);
    // In the capture phase: a press that picks a key must not reach the key itself. The presses
    // are counted when the pointer goes down: a locked key (Play while the stack is not live) gets
    // no click at all, but it is a key like the others and can have a shortcut.
    document.addEventListener('pointerdown', pointerDown, true);
    document.addEventListener('pointerup', pointerUp, true);
    document.addEventListener('click', click, true);
    document.addEventListener('dblclick', doubleClick, true);
    window.addEventListener('blur', away);
    this.destroyRef.onDestroy(() => {
      document.removeEventListener('keydown', keyDown, true);
      document.removeEventListener('keyup', keyUp, true);
      document.removeEventListener('pointerdown', pointerDown, true);
      document.removeEventListener('pointerup', pointerUp, true);
      document.removeEventListener('click', click, true);
      document.removeEventListener('dblclick', doubleClick, true);
      window.removeEventListener('blur', away);
    });
  }

  // Notes which Ctrl and Alt keys are down. A key whose letting go was not seen (the event says
  // that no Ctrl is held any more) is let go here.
  private noteSides(event: KeyboardEvent, down: boolean): void {
    if (SIDED_KEYS.has(event.code)) {
      if (down) this.sides.add(event.code);
      else this.sides.delete(event.code);
    }
    if (!event.ctrlKey) ['ControlLeft', 'ControlRight'].forEach((code) => this.sides.delete(code));
    if (!event.altKey) ['AltLeft', 'AltRight'].forEach((code) => this.sides.delete(code));
  }

  /** Shows the desk; the next key clicked there is picked, see the class: for the Keyboard panel, or the MIDI panel. */
  listen(forPanel: 'keyboard' | 'midi' = 'keyboard'): void {
    this.recording.set(null);
    this.pickedFor.set(forPanel);
    this.listening.set(true);
    void this.router.navigateByUrl('/');
  }

  stopListening(): void {
    if (!this.listening()) return;
    this.dropPending();
    this.listening.set(false);
    void this.router.navigateByUrl(this.backUrl());
  }

  /** The next keys pressed become the shortcut of this key of the desk. */
  record(id: string): void {
    this.dropPending();
    this.listening.set(false);
    this.lastMove.set('');
    this.pickedFor.set('keyboard');
    this.picked.set(id);
    this.recording.set(id);
  }

  cancelRecording(): void {
    this.recording.set(null);
  }

  /** Takes the shortcut of a key of the desk away. A usual one (Ctrl + Z for Undo) stays away. */
  clear(id: string): void {
    const rest = { ...this.settings.shortcuts() };
    takeAway(rest, id);
    this.settings.setShortcuts(rest);
  }

  /** Presses the key of the desk with this name, as its shortcut does. */
  press(id: string): void {
    const element = document.querySelector<HTMLElement>(`[data-action="${CSS.escape(id)}"]`);
    if (element) {
      // A locked key stays locked for its shortcut too.
      if (!(element instanceof HTMLButtonElement && element.disabled)) element.click();
      return;
    }

    actionWithId(id)?.run?.(this.context());
  }

  /**
   * A key of a MIDI controller was pressed (down) or let go, and the desk program sent it to this
   * page (desk-midi.ts): it presses the key of the desk with this name as its shortcut would. The
   * same rules hold: a locked key stays locked, keys rest while a window is open or a key is being
   * picked, and while a solo is lit a key held down holds its fader until it is let go. A key of the
   * pages shown ("slot.channel.3", see slotKey) presses the key under that fader of the page shown.
   */
  midiKey(id: string, down: boolean): void {
    if (!down) {
      const key = this.midiSlots.get(id) ?? id;
      this.midiSlots.delete(id);
      if (this.midiHeld.delete(key)) actionWithId(key)?.hold?.(this.context(), false);
      return;
    }
    if (this.listening() || this.recording() !== null || document.querySelector('dialog[open]')) return;

    const slot = slotKey(id, this.store);
    if (slot === null) return;   // a place beyond the page shown
    const key = slot ?? id;
    if (slot) this.midiSlots.set(id, key);

    const hold = actionWithId(key)?.hold;
    if (hold && this.store.soloMode()) {
      this.midiHeld.add(key);
      hold(this.context(), true);
      return;
    }
    this.press(key);
  }

  private onKeyDown(event: KeyboardEvent): void {
    this.noteSides(event, true);
    const recording = this.recording();
    if (recording !== null) {
      this.takeShortcut(recording, event);
      return;
    }

    if (this.listening()) {
      if (event.key === 'Escape') {
        event.preventDefault();
        this.stopListening();
      }
      return;
    }

    if (event.repeat || typing(event.target) || document.querySelector('dialog[open]')) return;
    // A box or a choice keeps its own space bar: it ticks it. (A key of the desk does not: the space
    // bar is GO also when the key clicked last still has the focus.)
    if (event.code === 'Space' && ticks(event.target)) return;

    // The shortcut with the sides of its Ctrl and Alt first, then the one kept without a side (see shortcutsOf).
    const shortcuts = Object.entries(this.settings.shortcuts());
    const id = shortcutsOf(pressOf(event), this.sides)
      .map((name) => shortcuts.find(([, keys]) => keys === name)?.[0])
      .find((found) => found !== undefined);
    if (id === undefined) return;

    event.preventDefault();

    // While a solo is lit, the shortcut of a key under a fader or of the independents holds it, for as long as it is held.
    const hold = actionWithId(id)?.hold;
    if (hold && this.store.soloMode()) {
      this.held.set(event.code, id);
      hold(this.context(), true);
      return;
    }

    this.press(id);
  }

  // A key let go that holds a key of the desk: its fader goes back.
  private onKeyUp(event: KeyboardEvent): void {
    this.noteSides(event, false);
    const id = this.held.get(event.code);
    if (id === undefined) return;

    this.held.delete(event.code);
    actionWithId(id)?.hold?.(this.context(), false);
  }

  private context(): ActionContext {
    return { store: this.store, navigate: (url) => void this.router.navigateByUrl(url) };
  }

  // A key pressed while a shortcut is recorded: it becomes the shortcut, and goes no further.
  private takeShortcut(id: string, event: KeyboardEvent): void {
    event.preventDefault();
    event.stopPropagation();
    if (event.key === 'Escape') {
      this.recording.set(null);
      return;
    }

    const shortcut = shortcutOf(pressOf(event), this.sides);
    if (shortcut === null) return; // Ctrl or Shift alone: wait for the key they are held with

    const shortcuts = { ...this.settings.shortcuts() };
    const before = Object.entries(shortcuts).find(([other, keys]) => keys === shortcut && other !== id)?.[0];
    if (before !== undefined) {
      takeAway(shortcuts, before);
      const label = DESK_ACTIONS.find((action) => action.id === before)?.label ?? before;
      this.lastMove.set(`That shortcut was the one of ${label} before; ${label} has none now.`);
    }
    shortcuts[id] = shortcut;
    this.settings.setShortcuts(shortcuts);
    this.recording.set(null);
  }

  // While listening, a press on a key of the desk does not reach it. A second press on the same
  // key soon after picks the key; otherwise the key gets its click a moment later. Listening for
  // the MIDI view, a fader is held back the same way (it does not move), and two presses pick it.
  private onPointerDown(event: PointerEvent): void {
    this.swallowClick = false; // a press that was held back and then never became a click
    if (!this.listening() || event.button !== 0) return;

    const element = keyElementUnder(event, this.fadersToo());
    const id = element ? keyOf(element) : null;
    if (element === null || id === null) return; // not a key of the desk: it works as always

    event.preventDefault();
    event.stopImmediatePropagation();
    this.swallowClick = true;

    const first = this.pending;
    if (first?.id === id) {
      this.dropPending();
      this.pick(id);
      return;
    }

    if (first) this.replay(); // a press on another key: the first one's click happens now
    this.pending = { id, element, timer: setTimeout(() => this.replay(), DOUBLE_PRESS_MS) };
  }

  private onClick(event: MouseEvent): void {
    if (this.replaying) return; // the click of a single press, made a moment after it

    // The click of a press that was held back, or one made without a pointer (Enter on a key).
    const picked = this.swallowClick || (this.listening() && keyUnder(event, this.fadersToo()) !== null);
    if (!picked) return;

    event.preventDefault();
    event.stopImmediatePropagation();
    if (this.swallowClick) {
      this.swallowClick = false;
      return;
    }

    this.dropPending();
    this.pick(keyUnder(event, this.fadersToo()));
  }

  // The browser's double click after two presses: while listening it is a pick, and so is the one
  // that comes just after a pick. Neither does what a double click on the key or fader would do
  // (the rate fader goes back to 120 BPM).
  private onDoubleClick(event: MouseEvent): void {
    const pick = this.listening()
      ? keyElementUnder(event, this.fadersToo()) !== null
      : performance.now() - this.pickedAt < PICKED_DOUBLE_CLICK_MS;
    if (!pick) return;
    event.preventDefault();
    event.stopImmediatePropagation();
  }

  // A key is picked: Settings comes back with it chosen in its Keyboard panel, or the MIDI view with
  // the key or fader shown in its list.
  private pick(id: string | null): void {
    if (this.pickedFor() === 'midi') this.midiPicked.set(id);
    else this.picked.set(id);
    this.listening.set(false);
    this.pickedAt = performance.now();
    void this.router.navigateByUrl(this.backUrl());
  }

  // Where listening comes back to: the Keyboard panel of Settings, or the MIDI view.
  private backUrl(): string {
    return this.pickedFor() === 'midi' ? MIDI_URL : KEYBOARD_URL;
  }

  // True while listening for the MIDI view: its faders can be picked as well as its keys.
  private fadersToo(): boolean {
    return this.pickedFor() === 'midi';
  }

  // A single press on a key while listening, with no second one after it: the key gets its click
  // now, as without listening (a link opens its view, a page key its page). A locked key does
  // nothing, as always.
  private replay(): void {
    const pending = this.pending;
    this.dropPending();
    if (!pending || !this.listening()) return;

    const id = CSS.escape(pending.id);
    const element = pending.element.isConnected
      ? pending.element
      : document.querySelector<HTMLElement>(`[data-action="${id}"], [data-fader="${id}"]`);
    if (!element || (element instanceof HTMLButtonElement && element.disabled)) return;

    this.replaying = true;
    try {
      element.click();
    } finally {
      this.replaying = false;
    }
  }

  private dropPending(): void {
    if (this.pending) clearTimeout(this.pending.timer);
    this.pending = null;
  }
}

// Takes the shortcut of a key of the desk away. A usual one (DEFAULT_SHORTCUTS) is marked as
// taken away, so that it does not come back by itself.
function takeAway(shortcuts: Record<string, string>, id: string): void {
  if (id in DEFAULT_SHORTCUTS) shortcuts[id] = '';
  else delete shortcuts[id];
}

/** The name of the key of the desk an event happened on (or of the fader, with `faders`), or null. */
function keyUnder(event: Event, faders = false): string | null {
  const element = keyElementUnder(event, faders);
  return element ? keyOf(element) : null;
}

/**
 * The element of the key of the desk an event happened on, or null. With `faders`, a fader that
 * says its name for MIDI (`data-fader`, "fader.channel.12") counts as well.
 */
function keyElementUnder(event: Event, faders = false): HTMLElement | null {
  const marked = faders ? '[data-action], [data-fader]' : '[data-action]';
  return (event.target as Element | null)?.closest?.<HTMLElement>(marked) ?? null;
}

/**
 * The name of a fader: words with dashes after "fader.", and a number of any length at the end
 * ("fader.master", "fader.channel.12", "fader.effect-speed.3"); see FADER_TARGETS in midi-targets.ts.
 */
const FADER_NAME = /^fader(\.[a-z][a-z-]*)+(\.[0-9]+)?$/;

/** The name of a key of the desk, if the desk knows it, or of a fader ("fader.master"). */
function keyOf(element: Element): string | null {
  const fader = element.getAttribute('data-fader');
  if (fader !== null) return FADER_NAME.test(fader) ? fader : null;
  const id = element.getAttribute('data-action');
  return id !== null && actionWithId(id) ? id : null;
}

/** True for a checkbox or a radio button, which the space bar ticks. */
function ticks(target: EventTarget | null): boolean {
  return target instanceof HTMLInputElement && (target.type === 'checkbox' || target.type === 'radio');
}

/** True while the keyboard types into a field: then a shortcut must not take the key. */
function typing(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  if (target.isContentEditable) return true;
  if (target instanceof HTMLTextAreaElement || target instanceof HTMLSelectElement) return true;
  if (target instanceof HTMLInputElement) {
    // A range input (a fader) does not type; text, numbers and the like do.
    return !['range', 'checkbox', 'radio', 'button', 'submit', 'reset'].includes(target.type);
  }
  return false;
}
