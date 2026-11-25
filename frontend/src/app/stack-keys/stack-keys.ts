import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * The keys a show is run with: Stack, which takes the stack live, and Prev, Play, Pause and
 * Next, which go through it. The Stack view and the Stage view both show them, alike, with MUTE of
 * the sound at the end of their row (lm-mute-key).
 */
@Component({
  imports: [LampButton],
  selector: 'lm-stack-keys',
  styleUrl: './stack-keys.css',
  templateUrl: './stack-keys.html',
})
export class StackKeys {
  protected readonly store = inject(DeskStore);

  /** True while the stack is live: it runs, and its steps are locked. */
  protected readonly live = this.store.live;
  protected readonly paused = this.store.stackPaused;
  /** True while the stack moves by itself: a fade or a countdown that is not paused. */
  protected readonly running = this.store.stackRunning;
  protected readonly empty = computed(() => this.store.stack().steps.length === 0);
}
