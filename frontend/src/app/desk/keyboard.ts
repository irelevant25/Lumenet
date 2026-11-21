import { DestroyRef, Service, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ActionContext, DESK_ACTIONS, actionWithId, shortcutOf } from './desk-actions';
import { DEFAULT_SHORTCUTS, DeskSettings } from './desk-settings';
import { DeskStore } from './desk-store';

/** Where the Keyboard view is; listening comes back to it. */
const KEYBOARD_URL = '/keyboard';

/**
 * The keyboard: the shortcuts of the Keyboard view press the keys of the desk, a shortcut can be
 * recorded for a key, and "listening" finds a key by a click on it.
 *
 * - A shortcut presses its key on the page, as a click would, when the key is there and not
 *   locked; otherwise it does what the key does (see desk-actions.ts). Shortcuts rest while a
 *   text field has the keyboard (a note is being typed) and while a window such as Load is open.
 * - Recording: the next key pressed (with Ctrl, Alt, Shift held as wanted) becomes the shortcut
 *   of one key of the desk. A shortcut that another key had moves over. Esc stops recording.
 * - While a solo is lit, the shortcut of a key under a fader or of the independents holds that key's
 *   fader (at full or at zero) for as long as it is held.
 * - Listening: the desk is shown, and the next click on one of its keys does not press it but
 *   picks it, and the Keyboard view comes back with that key chosen in its list. Esc stops
 *   listening.
 */
@Service()
export class Keyboard {
  private readonly settings = inject(DeskSettings);
  private readonly store = inject(DeskStore);
  private readonly router = inject(Router);

  /** True while the next click on a key of the desk only picks that key for the Keyboard view. */
  readonly listening = signal(false);
  /** The key of the desk chosen in the Keyboard view: by listening, or in its list. */
  readonly picked = signal<string | null>(null);
  /** The key whose shortcut is being recorded; null while none is. */
  readonly recording = signal<string | null>(null);
  /** What the last recording did besides, like "Ctrl + 1 was the shortcut of Clear before." */
  readonly lastMove = signal('');

  private started = false;

  // A key was picked by the press of a pointer: the click that follows must not press it.
  private swallowClick = false;

  // While a solo is lit: the keys of the keyboard held down now that hold a key of the desk, by the
  // physical key (KeyboardEvent.code), with the name of that key of the desk.
  private readonly held = new Map<string, string>();

  private readonly destroyRef = inject(DestroyRef);

  /** Starts listening to the keyboard and to clicks. The page calls it once. */
  start(): void {
    if (this.started || typeof document === 'undefined') return;
    this.started = true;

    const keyDown = (event: KeyboardEvent): void => this.onKeyDown(event);
    const keyUp = (event: KeyboardEvent): void => this.onKeyUp(event);
    const pointerDown = (event: PointerEvent): void => this.onPointerDown(event);
    const click = (event: MouseEvent): void => this.onClick(event);
    document.addEventListener('keydown', keyDown, true);
    document.addEventListener('keyup', keyUp, true);
    // In the capture phase: a press that picks a key must not reach the key itself. The pick
    // happens when the pointer goes down: a locked key (Play while the stack is not live) gets
    // no click at all, but it is a key like the others and can have a shortcut.
    document.addEventListener('pointerdown', pointerDown, true);
    document.addEventListener('click', click, true);
    this.destroyRef.onDestroy(() => {
      document.removeEventListener('keydown', keyDown, true);
      document.removeEventListener('keyup', keyUp, true);
      document.removeEventListener('pointerdown', pointerDown, true);
      document.removeEventListener('click', click, true);
    });
  }

  /** Shows the desk; the next key clicked there is picked, see the class. */
  listen(): void {
    this.recording.set(null);
    this.listening.set(true);
    void this.router.navigateByUrl('/');
  }

  stopListening(): void {
    if (!this.listening()) return;
    this.listening.set(false);
    void this.router.navigateByUrl(KEYBOARD_URL);
  }

  /** The next keys pressed become the shortcut of this key of the desk. */
  record(id: string): void {
    this.listening.set(false);
    this.lastMove.set('');
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

  private onKeyDown(event: KeyboardEvent): void {
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

    const shortcut = shortcutOf(event);
    if (shortcut === null) return;

    const id = Object.entries(this.settings.shortcuts()).find(([, keys]) => keys === shortcut)?.[0];
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

    const shortcut = shortcutOf(event);
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

  private onPointerDown(event: PointerEvent): void {
    this.swallowClick = false; // a press that picked a key and then never became a click
    if (!this.listening()) return;

    const id = keyUnder(event);
    if (id === null) return; // not a key of the desk: it works as always

    event.preventDefault();
    event.stopImmediatePropagation();
    this.swallowClick = true;
    this.picked.set(id);
    this.listening.set(false);
    void this.router.navigateByUrl(KEYBOARD_URL);
  }

  private onClick(event: MouseEvent): void {
    // The click of the press that picked a key, or one made without a pointer (Enter on a key).
    const picked = this.swallowClick || (this.listening() && keyUnder(event) !== null);
    if (!picked) return;

    event.preventDefault();
    event.stopImmediatePropagation();
    if (this.swallowClick) {
      this.swallowClick = false;
      return;
    }

    this.picked.set(keyUnder(event));
    this.listening.set(false);
    void this.router.navigateByUrl(KEYBOARD_URL);
  }
}

// Takes the shortcut of a key of the desk away. A usual one (DEFAULT_SHORTCUTS) is marked as
// taken away, so that it does not come back by itself.
function takeAway(shortcuts: Record<string, string>, id: string): void {
  if (id in DEFAULT_SHORTCUTS) shortcuts[id] = '';
  else delete shortcuts[id];
}

/** The name of the key of the desk an event happened on, or null. */
function keyUnder(event: Event): string | null {
  const element = (event.target as Element | null)?.closest?.('[data-action]');
  const id = element?.getAttribute('data-action') ?? null;
  return id !== null && actionWithId(id) ? id : null;
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
