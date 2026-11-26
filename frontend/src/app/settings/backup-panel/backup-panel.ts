import { DatePipe } from '@angular/common';
import { Component, effect, inject, signal, untracked } from '@angular/core';
import { DeskConnection } from '../../desk/desk-connection';
import { DeskStore } from '../../desk/desk-store';
import type { BackupSetup } from '../../desk/hub-contract';
import { DeskBackups } from '../../project/desk-backups';

/** The most backups a folder keeps (BackupSetup.MaxKeep of the desk program). */
const MAX_KEEP = 1000;

/**
 * The Backup tab of Settings: the folder the backups go to (one that Google Drive for desktop syncs
 * takes them to Google Drive), how many it keeps, whether a save makes one, and Back up now. A backup
 * is one ZIP of everything Export takes; Import brings it back. The backups belong to the PC.
 */
@Component({
  imports: [DatePipe],
  selector: 'lm-backup-panel',
  styleUrl: './backup-panel.css',
  templateUrl: './backup-panel.html',
})
export class BackupPanel {
  private readonly backups = inject(DeskBackups);
  private readonly connection = inject(DeskConnection);
  protected readonly store = inject(DeskStore);

  protected readonly view = this.backups.view;
  protected readonly maxKeep = MAX_KEEP;
  /** Why the backups cannot be set here (another device, an older desk program); empty while they can. */
  protected readonly unavailable = signal('');
  protected readonly problem = signal('');
  protected readonly done = signal('');
  protected readonly busy = signal(false);

  constructor() {
    // asked when the tab opens, and again after the desk program was away
    effect(() => {
      if (this.connection.online()) untracked(() => void this.load());
    });
  }

  protected onFolder(event: Event): void {
    const field = event.target as HTMLInputElement;
    void this.change(
      { folder: field.value.trim() },
      () => (field.value = this.view()?.folder ?? ''),
    );
  }

  protected useGoogleDrive(folder: string): void {
    void this.change({ folder });
  }

  protected onKeep(event: Event): void {
    const field = event.target as HTMLInputElement;
    void this.change(
      { keep: Number(field.value) },
      () => (field.value = String(this.view()?.keep ?? '')),
    );
  }

  protected onOnSave(event: Event): void {
    void this.change({ onSave: (event.target as HTMLSelectElement).value === 'on' });
  }

  protected async backUpNow(): Promise<void> {
    this.busy.set(true);
    this.problem.set('');
    this.done.set('');
    try {
      const made = await this.backups.backUp();
      this.done.set(
        made.missing.length
          ? `Backed up as "${made.file}", without these sound files, which the PC does not have: ${made.missing.join(', ')}.`
          : `Backed up as "${made.file}".`,
      );
    } catch (error) {
      this.problem.set(messageOf(error));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    if (this.store.lan()?.local === false) {
      this.unavailable.set(
        'The backups belong to the PC the desk program runs on: set them there.',
      );
      return;
    }
    try {
      await this.backups.fetch();
      this.unavailable.set('');
    } catch (error) {
      this.unavailable.set(messageOf(error));
    }
  }

  // A change of the setup: the desk program checks it; a refused one goes back in its field.
  private async change(part: Partial<BackupSetup>, putBack?: () => void): Promise<void> {
    const view = this.view();
    if (!view) return;
    this.problem.set('');
    this.done.set('');
    try {
      await this.backups.set({
        folder: view.folder,
        onSave: view.onSave,
        keep: view.keep,
        ...part,
      });
    } catch (error) {
      this.problem.set(messageOf(error));
      putBack?.();
    }
  }
}

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
