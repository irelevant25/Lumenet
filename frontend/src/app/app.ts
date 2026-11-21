import { Component, inject } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter, take } from 'rxjs';
import { DeskSettings } from './desk/desk-settings';
import { DeskStore } from './desk/desk-store';
import { Keyboard } from './desk/keyboard';
import { viewNamed } from './desk/views';
import { TopBar } from './top-bar/top-bar';

/** The frame of the desk: the top bar, and under it the view that is chosen there. */
@Component({
  imports: [RouterOutlet, TopBar],
  selector: 'lm-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  protected readonly store = inject(DeskStore);
  protected readonly keyboard = inject(Keyboard);

  constructor() {
    // The settings of this browser, read before anything is drawn: the font size among them.
    const settings = inject(DeskSettings);
    this.keyboard.start();

    // A desk whose Settings leave Main out of the top bar opens on the first view that is in it.
    const router = inject(Router);
    router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        take(1),
      )
      .subscribe(() => {
        const hidden = settings.hiddenViews();
        if (router.url !== '/' || !hidden.includes('main')) return;
        const first = settings.viewOrder().find((name) => !hidden.includes(name));
        if (first) void router.navigateByUrl(viewNamed(first).url);
      });
  }
}
