import { Service, inject, signal } from '@angular/core';
import { DeskConnection } from '../desk/desk-connection';
import { DeskSettings } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import type { BackupSetup, BackupView } from '../desk/hub-contract';
import { DeskMidi } from '../midi/desk-midi';
import { TRANSFER_PARTS, TransferPart, exportText } from './transfer';

/** What the desk program answers for a backup it made (POST /api/backup). */
export interface BackupMade {
  /** The name of the ZIP in the folder. */
  readonly file: string;
  /** The sound files of the project the desk program does not have: the backup lacks them. */
  readonly missing: readonly string[];
  /** The backups after it. */
  readonly backups: BackupView;
}

/** Every part Export can take: a backup holds them all. */
const EVERY_PART: ReadonlySet<TransferPart> = new Set(TRANSFER_PARTS.map((part) => part.part));

/**
 * Backups: everything Export takes (the project with its sounds, the stage, the settings and the
 * keyboard of this PC, the MIDI), one ZIP each time, written by the desk program into a folder of
 * the PC; a folder that Google Drive for desktop, OneDrive or Dropbox syncs takes it to the cloud.
 * The page on the PC makes them: on a press of Back up now, and after the project was saved when
 * the backups say so. The setup belongs to the desk program (GetBackup, SetBackup).
 */
@Service()
export class DeskBackups {
  private readonly store = inject(DeskStore);
  private readonly settings = inject(DeskSettings);
  private readonly midi = inject(DeskMidi);
  private readonly connection = inject(DeskConnection);

  /** The backups as the desk program has them; null until asked, and where it does not answer. */
  readonly view = signal<BackupView | null>(null);

  /** Asks the desk program for the backups. Rejects with its message: on another device, an older desk program. */
  async fetch(): Promise<BackupView> {
    const view = await this.connection.invoke('GetBackup');
    this.view.set(view);
    return view;
  }

  /** Another folder, number to keep, or way with a save. Rejects with the desk program's message when it refuses. */
  async set(setup: BackupSetup): Promise<BackupView> {
    const view = await this.connection.invoke('SetBackup', setup);
    this.view.set(view);
    return view;
  }

  /** Makes a backup now. Rejects with what went wrong: no folder, a folder that cannot be written. */
  async backUp(): Promise<BackupMade> {
    const name = this.store.project().name;
    const [projectFile, midi] = await Promise.all([
      this.store.exportProject(),
      this.midi.state() !== null ? this.midi.setup() : Promise.resolve(undefined),
    ]);
    const text = exportText(EVERY_PART, {
      name,
      projectFile,
      settings: this.settings.settingsPart(),
      shortcuts: this.settings.shortcuts(),
      midi,
    });
    const made = await sendBackup(name, text);
    this.view.set(made.backups);
    return made;
  }

  /**
   * The project was saved: on the PC, a backup follows when the backups have a folder and are made
   * with a save. Says how it went, after the words of the save.
   */
  async afterSave(saved: string): Promise<void> {
    if (this.store.lan()?.local === false) return;
    let view: BackupView;
    try {
      view = await this.fetch();
    } catch {
      return; // a desk program from before the backups
    }
    if (!view.onSave || !view.folder) return;

    try {
      const made = await this.backUp();
      const missing = made.missing.length ? ` (without ${made.missing.join(', ')})` : '';
      this.store.tell(`${saved} and backed up${missing}`);
    } catch (error) {
      this.store.tell(
        `${saved}, but not backed up: ${error instanceof Error ? error.message : String(error)}`,
      );
    }
  }
}

/**
 * Hands the text of Export to the desk program, which writes it as a backup into its folder
 * (POST /api/backup). Fails with what it said.
 */
async function sendBackup(name: string, text: string): Promise<BackupMade> {
  let response: Response;
  try {
    response = await fetch(`/api/backup?name=${encodeURIComponent(name)}`, {
      method: 'POST',
      body: text,
      headers: { 'Content-Type': 'application/json' },
    });
  } catch {
    throw new Error('The desk program could not be reached.');
  }
  const answer = (await response.json().catch(() => null)) as
    (Partial<BackupMade> & { message?: string }) | null;
  if (!response.ok || typeof answer?.file !== 'string' || !answer.backups) {
    throw new Error(answer?.message ?? `The desk program made no backup (${response.status}).`);
  }
  return { file: answer.file, missing: answer.missing ?? [], backups: answer.backups };
}
