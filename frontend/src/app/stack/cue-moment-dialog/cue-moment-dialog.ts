import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { DeskStore } from '../../desk/desk-store';
import { SoundTime } from '../../desk/desk-types';
import { DialogDismiss } from '../../shared/dialog-dismiss';
import { LampButton } from '../../shared/lamp-button/lamp-button';
import { MAX_MOMENT, momentText, parseMoment } from '../moments';

/**
 * The window that sets the moment of a sound a cue of the stack comes at, instead of GO: the sound,
 * and where in it, typed ("1:15.5", "75.5") or taken with Now from where the sound is while it plays.
 * Use gives the cue that moment; None takes it away, and the cue waits for GO again; Cancel and Esc
 * leave it as it was. The window opens on the cue's own moment, or on the sound that plays (else the
 * one that played last) at the moment it is at.
 */
@Component({
  imports: [DialogDismiss, LampButton],
  selector: 'lm-cue-moment-dialog',
  styleUrl: './cue-moment-dialog.css',
  templateUrl: './cue-moment-dialog.html',
})
export class CueMomentDialog {
  private readonly store = inject(DeskStore);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  protected readonly title = signal('');
  /** The sounds of the project, to choose from. */
  protected readonly sounds = this.store.sounds;
  /** The sound chosen, by its number; 0 while the project has none. */
  protected readonly sound = signal(0);
  /** The moment as it stands in the field. */
  protected readonly typed = signal('');
  /** True when the cue has a moment: None takes it away. */
  protected readonly had = signal(false);
  private done: ((at: SoundTime | null) => void) | null = null;

  /** Where the chosen sound is while it plays, in seconds of its file; null while it is quiet. */
  protected readonly position = computed(() => {
    const playback = this.store.soundPlaybacks().get(this.sound());
    return playback?.playing ? playback.position : null;
  });

  /** Beside Now: where the chosen sound is, "It plays at 0:12.40.", or that it is quiet. */
  protected readonly where = computed(() => {
    const position = this.position();
    return position === null ? 'It is quiet.' : `It plays at ${momentText(position)}.`;
  });

  /**
   * What is wrong with the moment as it stands, in words; null when there is nothing to say against
   * it. A field still empty is no mistake: it says nothing, and Use waits for a moment (usable).
   */
  protected readonly problem = computed(() => {
    if (this.sounds().length === 0) return 'The project has no sounds: the Sounds view puts them in.';
    if (this.typed().trim() === '') return null;
    const seconds = parseMoment(this.typed());
    if (seconds === null || seconds > MAX_MOMENT) {
      return 'Type the moment in minutes and seconds, like 1:15.5, or in seconds, like 75.5.';
    }
    const length = this.store.soundsByNumber().get(this.sound())?.seconds ?? 0;
    return length > 0 && seconds > length
      ? `The sound is ${momentText(length)} long: it never comes to ${momentText(seconds)}.`
      : null;
  });

  /** True when Use can give the cue the moment: one is typed, and nothing speaks against it. */
  protected readonly usable = computed(() => this.problem() === null && parseMoment(this.typed()) !== null);

  /**
   * Opens the window on the cue's moment, or on none. `done` gets the moment used, or null for None;
   * closed without either, nothing changes.
   */
  open(title: string, at: SoundTime | null | undefined, done: (at: SoundTime | null) => void): void {
    this.title.set(title);
    this.had.set(!!at);
    this.done = done;
    if (at) {
      this.sound.set(at.sound);
      this.typed.set(momentText(at.seconds));
    } else {
      const shown = this.store.shownSound() ?? this.sounds()[0]?.number ?? 0;
      this.sound.set(shown);
      const position = this.position();
      this.typed.set(position === null ? '' : momentText(position));
    }
    this.dialog().nativeElement.showModal();
  }

  protected chooseSound(event: Event): void {
    this.sound.set(Number((event.target as HTMLSelectElement).value));
  }

  protected type(event: Event): void {
    this.typed.set((event.target as HTMLInputElement).value);
  }

  /** Now: the moment the chosen sound is at, while it plays. */
  protected now(): void {
    const position = this.position();
    if (position !== null) this.typed.set(momentText(position));
  }

  /** Use: the cue comes at the moment chosen. */
  protected use(event: Event): void {
    event.preventDefault();
    const seconds = parseMoment(this.typed());
    if (!this.usable() || seconds === null) return;
    this.finish({ sound: this.sound(), seconds });
  }

  /** None: the cue has no moment, and waits for GO again. */
  protected none(): void {
    this.finish(null);
  }

  protected close(): void {
    this.done = null;
    this.dialog().nativeElement.close();
  }

  private finish(at: SoundTime | null): void {
    const done = this.done;
    this.close();
    done?.(at);
  }
}
