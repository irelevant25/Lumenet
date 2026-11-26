import { Service, inject, signal } from '@angular/core';
import { DeskConnection } from '../desk/desk-connection';
import type { UpdateInfo } from '../desk/hub-contract';

/**
 * Updates of Lumenet, for the About tab of Settings: the newest release on GitHub, asked for when the
 * tab opens, and on a press its Lumenet.exe downloaded by the desk program, which puts it in its own
 * place and starts it. The page then connects to the new one and loads again (DeskStore). Both answers
 * come as streams: GitHub may take its time, and the page's faders go on meanwhile.
 */
@Service()
export class DeskUpdate {
  private readonly connection = inject(DeskConnection);

  /** The versions as the desk program answered last; null until asked. */
  readonly info = signal<UpdateInfo | null>(null);
  /** How far the download of an update is, 0 to 100; null while none runs. */
  readonly progress = signal<number | null>(null);
  /** The version Lumenet starts again as, once the new one is in place; null before. */
  readonly restarting = signal<string | null>(null);

  /** Asks the desk program for the newest release. Rejects with its message (on another device). */
  async check(): Promise<UpdateInfo> {
    const info = await this.connection.ask('CheckUpdate');
    if (!info) throw new Error('The desk program did not answer.');
    this.info.set(info);
    return info;
  }

  /** Updates Lumenet to the newest release. Rejects with what went wrong; Lumenet then stays as it is. */
  async install(): Promise<void> {
    this.progress.set(0);
    try {
      const last = await this.connection.follow('InstallUpdate', (step) =>
        this.progress.set(step.percent),
      );
      if (last?.version) this.restarting.set(last.version);
    } finally {
      this.progress.set(null);
    }
  }
}
