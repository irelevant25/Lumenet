import { Directive, ElementRef, inject, output } from '@angular/core';

/**
 * A window of the page (a <dialog> opened with showModal) that a press outside it closes, as its ✕
 * and Esc do. The press must begin and end outside the window: a drag that began inside it (across
 * the field of colors, or marking a text) and ends outside closes nothing. `dismiss` asks the
 * component to close the window its own way, as its Cancel does.
 */
@Directive({
  selector: 'dialog[lmDialog]',
  host: {
    '(pointerdown)': 'onDown($event)',
    '(click)': 'onClick($event)',
  },
})
export class DialogDismiss {
  readonly dismiss = output<void>();

  private readonly dialog = inject<ElementRef<HTMLDialogElement>>(ElementRef).nativeElement;
  private downOutside = false;

  protected onDown(event: MouseEvent): void {
    this.downOutside = this.outside(event);
  }

  protected onClick(event: MouseEvent): void {
    const outside = this.downOutside && this.outside(event);
    this.downOutside = false;
    if (outside) this.dismiss.emit();
  }

  // The backdrop belongs to the dialog: a press on it has the dialog itself as its target, and lies
  // outside its box (a press on the window's own edge has the dialog as its target too, inside it).
  private outside(event: MouseEvent): boolean {
    if (event.target !== this.dialog) return false;
    const box = this.dialog.getBoundingClientRect();
    return (
      event.clientX < box.left ||
      event.clientX > box.right ||
      event.clientY < box.top ||
      event.clientY > box.bottom
    );
  }
}
