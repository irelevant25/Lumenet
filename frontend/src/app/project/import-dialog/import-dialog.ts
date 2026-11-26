import { Component, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { DeskMidi } from '../../midi/desk-midi';
import { DeskSettings } from '../../desk/desk-settings';
import { DeskStore } from '../../desk/desk-store';
import { IMPORT_LIMIT_BYTES } from '../../desk/desk-types';
import type { MidiSetup } from '../../midi/midi';
import { readShowFile } from '../show-file';
import { unpackImport } from '../../sounds/sound-files';
import {
  ImportKind,
  ImportSource,
  TRANSFER_PARTS,
  TransferPart,
  TransferPartView,
  importKind,
  partsIn,
  pcPartNote,
  readImportFile,
} from '../transfer';
import { DialogDismiss } from '../../shared/dialog-dismiss';
import { LampButton } from '../../shared/lamp-button/lamp-button';

/**
 * The window of Import: a file first, then what to take from it. A ZIP that Export made (a backup is
 * one too), a file of Export of before, or a project file lists its parts: the project, the stage,
 * the settings of this device, its keyboard shortcuts and (on the PC) the MIDI controllers, each with
 * a checkbox, none checked at first; a part the file does not have cannot be checked. A ZIP goes to
 * the desk program as it is chosen, which takes its sounds and gives back its parts. The show file
 * of a theatre console (.asc, .txt: USITT ASCII) is taken whole. Once it is done the window says what
 * became of the file.
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
  protected readonly file = signal<File | null>(null);
  /** What the chosen file is; null before one is chosen. */
  protected readonly kind = signal<ImportKind | null>(null);
  /** The parts the file has to offer, once it is read; null before, and for the show of a console. */
  protected readonly source = signal<ImportSource | null>(null);
  /** True while the file is read (a ZIP: while the desk program unpacks it). */
  protected readonly reading = signal(false);
  protected readonly error = signal('');
  protected readonly busy = signal(false);
  /** What became of the file, sentence by sentence; null until it is imported. */
  protected readonly report = signal<string[] | null>(null);

  /** The files of a ZIP that are no sounds the desk program can play. */
  private leftOut: readonly string[] = [];

  /** The show file of a theatre console, taken whole. */
  protected readonly asc = computed(() => this.kind() === 'asc');

  /** The parts the file has. */
  protected readonly inFile = computed(() => {
    const source = this.source();
    return source ? partsIn(source) : new Set<TransferPart>();
  });

  /** A project or a show in place of the open project throws away what was changed and not saved. */
  protected readonly losesChanges = computed(
    () => (this.asc() || this.chosen().has('project')) && this.store.project().unsaved,
  );

  /** A file larger than the desk takes in one go; a ZIP brings its sounds, and the desk program takes it in pieces. */
  private readonly tooLarge = computed(
    () => this.kind() !== 'zip' && (this.file()?.size ?? 0) > IMPORT_LIMIT_BYTES,
  );

  protected readonly canImport = computed(
    () =>
      this.file() !== null &&
      !this.tooLarge() &&
      !this.reading() &&
      !this.busy() &&
      (this.asc() || this.chosen().size > 0),
  );

  /** Opens the window with no file. */
  open(): void {
    this.chosen.set(new Set());
    this.file.set(null);
    this.kind.set(null);
    this.source.set(null);
    this.leftOut = [];
    this.reading.set(false);
    this.error.set('');
    this.busy.set(false);
    this.report.set(null);
    this.dialog().nativeElement.showModal();
  }

  /** True when a part can be checked: the file has it, and this page may take it. */
  protected can(part: TransferPartView): boolean {
    return this.inFile().has(part.part) && this.note(part) === '';
  }

  /** What the list says beside a part: why it cannot be checked, or what it holds. */
  protected said(part: TransferPartView): string {
    if (!this.inFile().has(part.part)) return 'not in this file';
    return this.note(part) || part.hint;
  }

  protected onCheck(part: TransferPart, event: Event): void {
    const chosen = new Set(this.chosen());
    if ((event.target as HTMLInputElement).checked) chosen.add(part);
    else chosen.delete(part);
    this.chosen.set(chosen);
  }

  protected choose(): void {
    const picker = this.picker().nativeElement;
    picker.value = ''; // so that choosing the same file again counts as a choice
    picker.click();
  }

  protected async onPicked(event: Event): Promise<void> {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return; // the choice was cancelled: the file chosen before stays

    const kind = importKind(file.name);
    this.file.set(file);
    this.kind.set(kind);
    this.source.set(null);
    this.chosen.set(new Set());
    this.leftOut = [];
    this.error.set('');
    if (this.tooLarge()) {
      this.error.set(
        `"${file.name}" has ${megabytes(file.size)} MB. A file can have up to ${megabytes(IMPORT_LIMIT_BYTES)} MB.`,
      );
      return;
    }
    if (kind === 'asc') return; // read when it is imported

    this.reading.set(true);
    try {
      const unpacked = kind === 'zip' ? await unpackImport(file) : null;
      if (this.file() !== file) return; // another file was chosen meanwhile
      this.leftOut = unpacked?.leftOut ?? [];
      this.source.set(readImportFile(file.name, unpacked?.text ?? (await readShowFile(file))));
    } catch (error) {
      if (this.file() === file) this.error.set(error instanceof Error ? error.message : String(error));
    } finally {
      if (this.file() === file) this.reading.set(false);
    }
  }

  protected async confirm(event: Event): Promise<void> {
    event.preventDefault(); // the form must not close the window before the desk has answered
    const file = this.file();
    if (!file || !this.canImport()) return;

    this.busy.set(true);
    this.error.set('');
    try {
      const source = this.source();
      this.report.set(
        this.asc() || !source
          ? await this.store.importShow(file.name, await readShowFile(file))
          : await this.importParts(source),
      );
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

  /** Why a part of the PC cannot be checked here: on a phone, or with a desk program without MIDI; empty when it can. */
  private note(part: TransferPartView): string {
    return pcPartNote(part, this.store.lan()?.local !== false, this.midi.state() !== null);
  }

  // The checked parts of the file, each as the desk takes it.
  private async importParts(source: ImportSource): Promise<string[]> {
    const chosen = this.chosen();
    const report: string[] = [];
    if (this.leftOut.length) {
      report.push(`These files of the ZIP are no sounds the desk program can play: ${this.leftOut.join(', ')}.`);
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
