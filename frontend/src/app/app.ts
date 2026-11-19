import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { DeskStore } from './desk/desk-store';
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
}
