import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { DeskSettings } from './desk/desk-settings';
import { DeskStore } from './desk/desk-store';
import { Keyboard } from './desk/keyboard';
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
    inject(DeskSettings);
    this.keyboard.start();
  }
}
