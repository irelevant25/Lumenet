import { Component, ElementRef, signal, viewChild } from '@angular/core';
import { ColorPick, ColorPicker } from '../color-picker/color-picker';
import { hexOf } from '../desk/fixtures';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * The window that gives a step of a fixture or a group its color: the colors of the Fixtures
 * page to press, or White. A moving head with a color wheel turns it to the slot nearest the
 * color.
 */
@Component({
  imports: [ColorPicker, LampButton],
  selector: 'lm-step-color-dialog',
  styleUrl: './step-color-dialog.css',
  templateUrl: './step-color-dialog.html',
})
export class StepColorDialog {
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  protected readonly title = signal('');
  /** The color the step has now; null for white. */
  protected readonly color = signal<string | null>(null);
  private done: ((color: string | null) => void) | null = null;

  /** Opens the window; `done` gets the color chosen, or null for white. Closed without a choice, nothing changes. */
  open(title: string, color: string | null, done: (color: string | null) => void): void {
    this.title.set(title);
    this.color.set(color);
    this.done = done;
    this.dialog().nativeElement.showModal();
  }

  protected onPick(pick: ColorPick): void {
    if (pick.kind === 'mix') this.finish(hexOf(pick.rgb));
  }

  protected white(): void {
    this.finish(null);
  }

  protected close(): void {
    this.done = null;
    this.dialog().nativeElement.close();
  }

  private finish(color: string | null): void {
    const done = this.done;
    this.close();
    done?.(color);
  }
}
