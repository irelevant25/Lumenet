import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { DeskConnection } from '../../desk/desk-connection';
import { DeskStore } from '../../desk/desk-store';
import { DeskUpdate } from '../desk-update';

/** A part Lumenet is made with, its license, and where it lives. */
interface MadeWith {
  readonly name: string;
  readonly license: string;
  readonly url: string;
}

/** What Lumenet is made with: the desk program, the page, and the libraries of fixtures it searches. */
const MADE_WITH: readonly MadeWith[] = [
  { name: '.NET, ASP.NET Core and SignalR', license: 'MIT', url: 'https://dotnet.microsoft.com' },
  { name: 'Angular', license: 'MIT', url: 'https://angular.dev' },
  { name: 'RxJS', license: 'Apache 2.0', url: 'https://rxjs.dev' },
  { name: 'NAudio', license: 'MIT', url: 'https://github.com/naudio/NAudio' },
  { name: 'DryWetMIDI', license: 'MIT', url: 'https://github.com/melanchall/drywetmidi' },
  { name: 'Open Fixture Library', license: 'MIT', url: 'https://open-fixture-library.org' },
  { name: 'Fixtures of QLC+', license: 'Apache 2.0', url: 'https://github.com/mcallegari/qlcplus' },
];

/**
 * The About tab of Settings: the version of Lumenet, the newest release on GitHub (asked for when the
 * tab opens), an update to it on a press, and what Lumenet is made with. Only the PC updates Lumenet;
 * the desk of run.cmd says that git brings the new version.
 */
@Component({
  imports: [DatePipe],
  selector: 'lm-about-panel',
  styleUrl: './about-panel.css',
  templateUrl: './about-panel.html',
})
export class AboutPanel {
  private readonly update = inject(DeskUpdate);
  private readonly connection = inject(DeskConnection);
  protected readonly store = inject(DeskStore);

  protected readonly info = this.update.info;
  protected readonly progress = this.update.progress;
  protected readonly restarting = this.update.restarting;
  protected readonly madeWith = MADE_WITH;
  protected readonly checking = signal(false);
  protected readonly problem = signal('');
  /** True while the question before the update is shown. */
  protected readonly asking = signal(false);

  /** True on a phone or a tablet: Lumenet is updated on the PC. */
  protected readonly elsewhere = computed(() => this.store.lan()?.local === false);

  /**
   * What the newest release brings, as paragraphs: the notes of a release are the messages of its
   * commits in Markdown, each under a "## " title, which is shown as a title.
   */
  protected readonly notes = computed(() =>
    (this.info()?.notes ?? '')
      .split(/\r?\n\s*\r?\n/)
      .map((paragraph) => paragraph.trim())
      .filter((paragraph) => paragraph !== '')
      .map((paragraph) => {
        const title = /^#{1,6}\s+/.exec(paragraph);
        return {
          title: title !== null,
          text: title ? paragraph.slice(title[0].length) : paragraph,
        };
      }),
  );

  /** The size of the new Lumenet.exe: "65.4 MB"; empty while not known. */
  protected readonly size = computed(() => {
    const bytes = this.info()?.size;
    return bytes ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : '';
  });

  constructor() {
    // asked when the tab opens, and again after the desk program was away (not while it starts anew)
    effect(() => {
      if (this.connection.online() && !this.elsewhere() && untracked(this.restarting) === null) {
        untracked(() => void this.check());
      }
    });
  }

  protected async check(): Promise<void> {
    this.checking.set(true);
    this.problem.set('');
    try {
      await this.update.check();
    } catch (error) {
      this.problem.set(messageOf(error));
    } finally {
      this.checking.set(false);
    }
  }

  protected async install(): Promise<void> {
    this.asking.set(false);
    this.problem.set('');
    try {
      await this.update.install();
    } catch (error) {
      this.problem.set(messageOf(error));
    }
  }
}

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
