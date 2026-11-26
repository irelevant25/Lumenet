import { Component, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { DeskMidi } from '../../midi/desk-midi';
import { DeskSettings } from '../../desk/desk-settings';
import { DeskStore } from '../../desk/desk-store';
import { saveZipFile } from '../save-file';
import { packExport } from '../../sounds/sound-files';
import { TRANSFER_PARTS, TransferPart, TransferPartView, exportFileName, exportText, pcPartNote } from '../transfer';
import { DialogDismiss } from '../../shared/dialog-dismiss';
import { LampButton } from '../../shared/lamp-button/lamp-button';

/**
 * The window of Export: the parts to put into a file on this device, each with a checkbox, none
 * of them checked at first: the project, the stage on its own, the settings of this device, its
 * keyboard shortcuts, and (on the PC) the MIDI controllers with what their controls do. The file is
 * one ZIP, which the desk program makes: each part a JSON file of its own, and the sound files of the
 * project. Import (in its own window) takes them back, here or on another device.
 */
@Component({
  imports: [DialogDismiss, LampButton],
  selector: 'lm-export-dialog',
  styleUrl: './export-dialog.css',
  templateUrl: './export-dialog.html',
})
export class ExportDialog {
  private readonly store = inject(DeskStore);
  private readonly settings = inject(DeskSettings);
  private readonly midi = inject(DeskMidi);
  private readonly injector = inject(Injector);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly okKey = viewChild('ok', { read: ElementRef<HTMLButtonElement> });

  protected readonly parts = TRANSFER_PARTS;
  protected readonly chosen = signal<ReadonlySet<TransferPart>>(new Set());
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  /** The name the file got; null until it is saved. */
  protected readonly saved = signal<string | null>(null);

  protected readonly canExport = computed(() => this.chosen().size > 0 && !this.busy());

  /** Why a part of the PC cannot be checked here: on a phone, or with a desk program without MIDI; empty when it can. */
  protected note(part: TransferPartView): string {
    return pcPartNote(part, this.store.lan()?.local !== false, this.midi.state() !== null);
  }

  /** Opens the window with nothing checked. */
  open(): void {
    this.chosen.set(new Set());
    this.busy.set(false);
    this.error.set('');
    this.saved.set(null);
    this.dialog().nativeElement.showModal();
  }

  protected onCheck(part: TransferPart, event: Event): void {
    const chosen = new Set(this.chosen());
    if ((event.target as HTMLInputElement).checked) chosen.add(part);
    else chosen.delete(part);
    this.chosen.set(chosen);
  }

  protected async confirm(event: Event): Promise<void> {
    event.preventDefault(); // the form must not close the window before the file is saved
    if (!this.canExport()) return;

    const chosen = this.chosen();
    const name = this.store.project().name;
    this.busy.set(true);
    this.error.set('');
    try {
      const projectFile = chosen.has('project') || chosen.has('stage') ? await this.store.exportProject() : undefined;
      const midi = chosen.has('midi') ? await this.midi.setup() : undefined;
      const text = exportText(chosen, {
        name,
        projectFile,
        settings: this.settings.settingsPart(),
        shortcuts: this.settings.shortcuts(),
        midi,
      });
      // One ZIP: each part a file of its own, and the sounds of the project with it.
      const fileName = exportFileName(chosen, name);
      const saved = await saveZipFile(fileName, () => packExport(fileName.replace(/\.zip$/, ''), text));
      if (saved === null) return; // no place was chosen: the window stays as it was
      this.saved.set(saved);
      // The Export key is gone with the question; the key that closes the window takes the focus.
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
}
