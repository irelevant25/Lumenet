import { Component, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { DeskSettings } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import { saveTextFile } from '../desk/save-file';
import { TRANSFER_PARTS, TransferPart, exportFileName, exportText } from '../desk/transfer';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * The window of Export: the parts to put into a file on this device, each with a checkbox, none
 * of them checked at first: the project, the stage on its own, the settings of this device and its
 * keyboard shortcuts. Import (in its own window) takes them back, here or on another device.
 */
@Component({
  imports: [LampButton],
  selector: 'lm-export-dialog',
  styleUrl: './export-dialog.css',
  templateUrl: './export-dialog.html',
})
export class ExportDialog {
  private readonly store = inject(DeskStore);
  private readonly settings = inject(DeskSettings);
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
      const text = exportText(chosen, { name, projectFile, settings: this.settings.settingsPart(), shortcuts: this.settings.shortcuts() });
      const saved = await saveTextFile(exportFileName(chosen, name), text);
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
