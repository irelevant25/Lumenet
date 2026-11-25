import { Component, ElementRef, signal, viewChild } from '@angular/core';
import { ColorPick, ColorPicker } from '../color-picker/color-picker';
import { hexOf } from '../desk/fixtures';
import { DialogDismiss } from '../desk/dialog-dismiss';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * The window that gives a step of a fixture or a group its color: the colors of the Fixtures
 * page to press, the field of every hue to drag across, or White (no color). A press or a drag
 * only chooses: the window shows the color chosen, and Use gives it to the step. Cancel and Esc
 * leave the step as it was. A moving head with a color wheel turns it to the slot nearest the color.
 */
@Component({
  imports: [ColorPicker, DialogDismiss, LampButton],
  selector: 'lm-step-color-dialog',
  styleUrl: './step-color-dialog.css',
  templateUrl: './step-color-dialog.html',
})
export class StepColorDialog {
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  protected readonly title = signal('');
  /** The color chosen so far, like "#ff2000"; null for white (no color). The step gets it with Use. */
  protected readonly chosen = signal<string | null>(null);
  private done: ((color: string | null) => void) | null = null;

  /** Opens the window on the step's color; `done` gets the color used, or null for white. Closed without Use, nothing changes. */
  open(title: string, color: string | null, done: (color: string | null) => void): void {
    this.title.set(title);
    this.chosen.set(color);
    this.done = done;
    this.dialog().nativeElement.showModal();
  }

  protected onPick(pick: ColorPick): void {
    if (pick.kind === 'mix') this.chosen.set(hexOf(pick.rgb));
  }

  protected white(): void {
    this.chosen.set(null);
  }

  /** Use: the step gets the color chosen. */
  protected use(event: Event): void {
    event.preventDefault();
    const done = this.done;
    const color = this.chosen();
    this.close();
    done?.(color);
  }

  protected close(): void {
    this.done = null;
    this.dialog().nativeElement.close();
  }
}
