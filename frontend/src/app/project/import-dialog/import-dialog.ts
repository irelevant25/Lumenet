import { Component, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { DeskMidi } from '../../midi/desk-midi';
import { DeskSettings } from '../../desk/desk-settings';
import { DeskStore } from '../../desk/desk-store';
import { IMPORT_LIMIT_BYTES } from '../../desk/desk-types';
import type { MidiSetup } from '../../midi/midi';
import { readShowFile } from '../show-file';
import { unpackImport } from '../../sounds/sound-files';
import { TRANSFER_PARTS, TransferPart, TransferPartView, missingParts, pcPartNote, readImportFile } from '../transfer';
import { DialogDismiss } from '../../shared/dialog-dismiss';
import { LampButton } from '../../shared/lamp-button/lamp-button';

/**
 * The window of Import: what to take from a file of this device, each with a checkbox, none of
 * them checked at first. The project, the stage, the settings of this device, its keyboard
 * shortcuts and (on the PC) the MIDI controllers come from a file that Export made (or a project
 * file); .ASC is the show of a theatre
 * console in the USITT ASCII format, a file of its own, so it is taken alone. Once it is done the
 * window says what became of the file.
 */
@Component({
  imports: [DialogDismiss, LampButton],
  selector: 'lm-import-dialog',
  styleUrl: './import-dialog.css',
  templateUrl: './import-dialog.html',
})
export class ImportDialog {
  private readonly store = inject(DeskStore);
  private readonly settings = inject(DeskSettings);
  private readonly midi = inject(DeskMidi);
  private readonly injector = inject(Injector);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly picker = viewChild.required<ElementRef<HTMLInputElement>>('picker');
  private readonly okKey = viewChild('ok', { read: ElementRef<HTMLButtonElement> });

  protected readonly parts = TRANSFER_PARTS;
  protected readonly chosen = signal<ReadonlySet<TransferPart>>(new Set());
  /** True for the show file of a theatre console. */
  protected readonly asc = signal(false);
  protected readonly file = signal<File | null>(null);
  protected readonly error = signal('');
  protected readonly busy = signal(false);
  /** What became of the file, sentence by sentence; null until it is imported. */
  protected readonly report = signal<string[] | null>(null);

  /** A ZIP of Export: a file of Export with the sounds of its project. */
  private readonly zip = computed(() => /\.zip$/i.test(this.file()?.name ?? ''));

  /** A file larger than the desk takes in one go; a ZIP brings its sounds, and the desk program takes it in pieces. */
  private readonly tooLarge = computed(() => !this.zip() && (this.file()?.size ?? 0) > IMPORT_LIMIT_BYTES);

  /** A project or a show in place of the open project throws away what was changed and not saved. */
  protected readonly losesChanges = computed(() => (this.asc() || this.chosen().has('project')) && this.store.project().unsaved);

  /** The kinds of files the system's window offers. */
  protected readonly accept = computed(() => (this.asc() ? '.asc,.txt' : '.json,.zip'));

  protected readonly canImport = computed(
    () => this.file() !== null && !this.tooLarge() && !this.busy() && (this.asc() || this.chosen().size > 0),
  );

  /** Opens the window with nothing checked and no file. */
  open(): void {
    this.chosen.set(new Set());
    this.asc.set(false);
    this.file.set(null);
    this.error.set('');
    this.busy.set(false);
    this.report.set(null);
    this.dialog().nativeElement.showModal();
  }

  /** Why a part of the PC cannot be checked here: on a phone, or with a desk program without MIDI; empty when it can. */
  protected note(part: TransferPartView): string {
    return pcPartNote(part, this.store.lan()?.local !== false, this.midi.state() !== null);
  }

  protected onCheck(part: TransferPart, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    const chosen = new Set(this.chosen());
    if (checked) chosen.add(part);
    else chosen.delete(part);
    this.chosen.set(chosen);
    if (checked && this.asc()) this.takeAsc(false);
  }

  protected onCheckAsc(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    if (checked) this.chosen.set(new Set());
    this.takeAsc(checked);
  }

  protected choose(): void {
    const picker = this.picker().nativeElement;
    picker.value = ''; // so that choosing the same file again counts as a choice
    picker.click();
  }

  protected onPicked(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return; // the choice was cancelled: the file chosen before stays

    this.file.set(file);
    this.error.set(
      this.tooLarge()
        ? `"${file.name}" has ${megabytes(file.size)} MB. A file can have up to ${megabytes(IMPORT_LIMIT_BYTES)} MB.`
        : '',
    );
  }

  protected async confirm(event: Event): Promise<void> {
    event.preventDefault(); // the form must not close the window before the desk has answered
    const file = this.file();
    if (!file || !this.canImport()) return;

    this.busy.set(true);
    this.error.set('');
    try {
      this.report.set(this.asc() ? await this.store.importShow(file.name, await readShowFile(file)) : await this.importParts(file));
      // The Import key is gone with the question; the key that closes the window takes the focus.
      afterNextRender(() => this.okKey()?.nativeElement.focus(), { injector: this.injector });
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : String(error));
    } finally {
      this.busy.set(false);
    }
  }

  protected close(): void {
    this.dialog().nativeElement.close();
  }

  // .ASC on or off: a file of the other kind that was chosen goes.
  private takeAsc(on: boolean): void {
    if (this.asc() === on) return;
    this.asc.set(on);
    this.file.set(null);
    this.error.set('');
  }

  // The checked parts from a file of Export or a project file: all of them, or none when the file lacks one.
  // A ZIP of Export goes to the desk program first, which takes its sounds and gives back its file of Export.
  private async importParts(file: File): Promise<string[]> {
    const unpacked = this.zip() ? await unpackImport(file) : null;
    const source = readImportFile(file.name, unpacked?.text ?? (await readShowFile(file)));
    const chosen = this.chosen();
    const missing = missingParts(chosen, source);
    if (missing) throw new Error(`"${file.name}" does not have ${missing}. Nothing was imported.`);

    const report: string[] = [];
    if (unpacked?.leftOut.length) {
      report.push(`These files of the ZIP are no sounds the desk program can play: ${unpacked.leftOut.join(', ')}.`);
    }
    if (chosen.has('project') && source.project !== null) {
      await this.store.importProjectFile(source.name, source.project);
      report.push(`The project "${this.store.project().name}" is open. Save keeps it in the projects folder.`);
    }
    if (chosen.has('stage') && source.stage !== null) {
      await this.store.importStage(source.stage);
      const lights = (JSON.parse(source.stage) as { lights?: unknown[] }).lights?.length ?? 0;
      report.push(`The stage is that of the file, with ${lights} ${lights === 1 ? 'light' : 'lights'}. Undo takes it back.`);
    }
    if (chosen.has('settings')) {
      this.settings.takeSettings(source.settings);
      report.push('The settings of this device are those of the file.');
    }
    if (chosen.has('keyboard')) {
      this.settings.takeShortcuts(source.shortcuts);
      report.push('The keyboard shortcuts of this device are those of the file.');
    }
    if (chosen.has('midi') && source.midi !== null) {
      await this.midi.takeSetup(source.midi as unknown as MidiSetup);
      report.push('The MIDI controllers of this PC, and what their controls do, are those of the file.');
    }
    return report;
  }
}

/** Megabytes with one digit after the point: "8.0", "12.4". */
function megabytes(bytes: number): string {
  return (bytes / 1024 / 1024).toFixed(1);
}
