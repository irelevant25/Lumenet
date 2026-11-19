import { Component, ElementRef, Injector, afterNextRender, computed, inject, signal, viewChild } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { IMPORT_LIMIT_BYTES } from '../desk/desk-types';
import { readShowFile } from '../desk/show-file';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * The window for importing the show of a theatre console: a file in the USITT ASCII format
 * (".asc"). It opens with the system's window for choosing the file; once the desk has made a
 * project of the show, it says what became of it.
 */
@Component({
  imports: [LampButton],
  selector: 'lm-import-dialog',
  styleUrl: './import-dialog.css',
  templateUrl: './import-dialog.html',
})
export class ImportDialog {
  private readonly store = inject(DeskStore);
  private readonly injector = inject(Injector);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly picker = viewChild.required<ElementRef<HTMLInputElement>>('picker');
  private readonly okKey = viewChild('ok', { read: ElementRef<HTMLButtonElement> });

  protected readonly file = signal<File | null>(null);
  protected readonly error = signal('');
  protected readonly busy = signal(false);
  /** What the desk made of the show, sentence by sentence; null until it is imported. */
  protected readonly report = signal<string[] | null>(null);

  /** A file larger than the desk takes in one go. */
  private readonly tooLarge = computed(() => (this.file()?.size ?? 0) > IMPORT_LIMIT_BYTES);

  /** Importing throws away what was changed and not saved. */
  protected readonly losesChanges = computed(() => this.store.project().unsaved);

  protected readonly canImport = computed(() => this.file() !== null && !this.tooLarge() && !this.busy());

  /** Opens the window, and over it the system's window for choosing the file. */
  open(): void {
    this.file.set(null);
    this.error.set('');
    this.busy.set(false);
    this.report.set(null);
    this.dialog().nativeElement.showModal();
    this.choose();
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
        ? `"${file.name}" has ${megabytes(file.size)} MB. A show file can have up to ${megabytes(IMPORT_LIMIT_BYTES)} MB.`
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
      const text = await readShowFile(file);
      this.report.set(await this.store.importShow(file.name, text));
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
}

/** Megabytes with one digit after the point: "8.0", "12.4". */
function megabytes(bytes: number): string {
  return (bytes / 1024 / 1024).toFixed(1);
}
