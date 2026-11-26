import {
  Component,
  ElementRef,
  afterNextRender,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { Router } from '@angular/router';
import { DeskSettings } from '../../desk/desk-settings';
import { VIEWS } from '../../desk/views';
import { DialogDismiss } from '../../shared/dialog-dismiss';
import { LampButton } from '../../shared/lamp-button/lamp-button';
import { SETUPS, Setup, setupOf, viewNames } from '../setups';
import { Welcome } from '../welcome';

/**
 * The window of the setups: on the first start of the page on a device (it keeps no settings yet),
 * and from Settings' Setup… key. First the kind of show (Theatre, Theatre with moving lights,
 * Concert, Everything): it sets the views of the top bar and the rows of Main on this device. Then
 * the first steps: the outputs, the lights, an example. Later leaves everything as it is.
 */
@Component({
  imports: [DialogDismiss, LampButton],
  selector: 'lm-welcome-dialog',
  styleUrl: './welcome-dialog.css',
  templateUrl: './welcome-dialog.html',
})
export class WelcomeDialog {
  private readonly settings = inject(DeskSettings);
  private readonly welcome = inject(Welcome);
  private readonly router = inject(Router);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  protected readonly setups = SETUPS;
  /** The setup the keys show as chosen. */
  protected readonly chosen = signal<Setup>(SETUPS[0]);
  /** 'choose' while the kind of show is chosen; 'start' once it was used, with the first steps. */
  protected readonly stage = signal<'choose' | 'start'>('choose');
  /** True while the device was never set up: the window welcomes. */
  protected readonly first = signal(false);

  /** The views the chosen setup shows, as their links say them. */
  protected readonly views = computed(() => viewNames(this.chosen()));

  constructor() {
    afterNextRender(() => {
      if (this.settings.fresh()) this.open(true);
    });
    // Settings' Setup… key
    effect(() => {
      if (this.welcome.asked() === 0) return;
      untracked(() => this.open(false));
    });
  }

  /** Shows the window: on a first start with Theatre chosen, else with the setup of this device. */
  private open(first: boolean): void {
    const hidden = this.settings.hiddenViews();
    const shown = VIEWS.map((view) => view.name).filter((name) => !hidden.includes(name));
    this.chosen.set((first ? undefined : setupOf(shown)) ?? SETUPS[0]);
    this.first.set(first);
    this.stage.set('choose');
    const dialog = this.dialog().nativeElement;
    if (!dialog.open) dialog.showModal();
  }

  protected choose(setup: Setup): void {
    this.chosen.set(setup);
  }

  /** Use this setup: the views and rows of the setup on this device, then the first steps. */
  protected use(event: Event): void {
    event.preventDefault();
    const setup = this.chosen();
    this.settings.useSetup(setup.views, setup.effectRows, setup.soundRows);
    this.stage.set('start');
  }

  /** Later, Esc, ✕ or a press outside: nothing changes, and a first start is not asked again. */
  protected later(): void {
    this.settings.welcomed();
    this.close();
  }

  /** A first step: a view of the desk. */
  protected go(url: string): void {
    this.close();
    void this.router.navigateByUrl(url);
  }

  /** A first step: the Load window, with the examples under the projects. */
  protected examples(): void {
    this.close();
    this.welcome.load();
  }

  protected close(): void {
    this.dialog().nativeElement.close();
  }
}
