import { Service, signal } from '@angular/core';

/**
 * Asks for the window of the setups (WelcomeDialog) from elsewhere, Settings' Setup… key, and for the
 * Load window from the window's first steps. Each ask counts one up, so the same ask works twice.
 */
@Service()
export class Welcome {
  /** How often the setups were asked for. */
  readonly asked = signal(0);
  /** How often the Load window was asked for, to open an example. */
  readonly loadAsked = signal(0);

  /** Shows the setups. */
  open(): void {
    this.asked.update((count) => count + 1);
  }

  /** Opens the Load window, with the examples under the projects. */
  load(): void {
    this.loadAsked.update((count) => count + 1);
  }
}
