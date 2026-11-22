import {
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  afterRenderEffect,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { NOTE_COLORS, NOTE_SIZES, USUAL_COLOR_MARK, USUAL_SIZE, cleanNotes, hexColor } from '../desk/rich-text';
import { LampButton } from '../lamp-button/lamp-button';

/** How long typing rests before the notes go to the desk program. */
const SEND_AFTER_MS = 600;

/** What the text at the cursor is: the tool keys light up for it. */
interface ToolState {
  readonly bold: boolean;
  readonly underline: boolean;
  readonly bulleted: boolean;
  readonly numbered: boolean;
  readonly size: string;
  /** The color of the text at the cursor, "#rrggbb"; null for the usual one. */
  readonly color: string | null;
}

const NO_TOOLS: ToolState = { bold: false, underline: false, bulleted: false, numbered: false, size: USUAL_SIZE, color: null };

/**
 * Notes: the notes of the project, instructions for the show, what it is and how to run it. They
 * are read here; Edit (faint while off, full while on) makes them a text to write in, with the
 * tool keys at the left: bold, underline, the size, a bulleted and a numbered list, and a color.
 * What is written goes to the desk program as soon as the typing rests, and when Edit goes off:
 * the notes belong to the project and are saved with it. Like everything stored, they cannot be
 * changed while the stack is live. The text is the browser's own editor (contenteditable); what
 * it writes is made into the marks of the notes (desk/rich-text.ts) before it is kept or shown.
 */
@Component({
  host: { '(document:selectionchange)': 'onSelectionChange()' },
  imports: [LampButton],
  selector: 'lm-notes-view',
  styleUrl: './notes-view.css',
  templateUrl: './notes-view.html',
})
export class NotesView {
  protected readonly store = inject(DeskStore);
  private readonly injector = inject(Injector);
  private readonly editor = viewChild.required<ElementRef<HTMLDivElement>>('editor');

  protected readonly sizes = NOTE_SIZES;
  protected readonly colors = NOTE_COLORS;
  protected readonly editing = signal(false);
  protected readonly tools = signal<ToolState>(NO_TOOLS);
  /** The desk program refused the notes: its message. */
  protected readonly error = signal('');

  // The part of the text the cursor was in last: a tool key works on it, also after the keyboard
  // went to the tool key.
  private range: Range | null = null;
  private sendTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    // While Edit is off, the notes are shown as the desk program keeps them; while it is on, the
    // text is the writer's, and the notes that come in meanwhile wait until Edit goes off.
    afterRenderEffect(() => {
      const notes = cleanNotes(this.store.notes());
      if (this.editing()) return;
      const editor = this.editor().nativeElement;
      if (editor.innerHTML !== notes) editor.innerHTML = notes;
    });

    // The stack went live while the notes were written: what was written goes, and Edit goes off.
    effect(() => {
      if (this.store.live() && untracked(this.editing)) {
        this.send();
        this.editing.set(false);
      }
    });

    inject(DestroyRef).onDestroy(() => this.send());
  }

  /** Edit: on, the notes are a text to write in, with the cursor at their end; off, what was written goes. */
  protected toggleEdit(): void {
    if (this.editing()) {
      this.send();
      this.editing.set(false);
      this.tools.set(NO_TOOLS);
      return;
    }
    if (this.store.live()) return;

    this.error.set('');
    this.editing.set(true);
    const editor = this.editor().nativeElement;
    // Once the text can be written in: the keyboard goes there, with the cursor at the end. Marks
    // as elements (<b>, <font>), not as styles, and a new line as a <div> in every browser.
    afterNextRender(
      () => {
        editor.focus();
        run('styleWithCSS', 'false');
        run('defaultParagraphSeparator', 'div');
        const end = document.createRange();
        end.selectNodeContents(editor);
        end.collapse(false);
        document.getSelection()?.removeAllRanges();
        document.getSelection()?.addRange(end);
        this.range = end;
        this.updateTools();
      },
      { injector: this.injector },
    );
  }

  /** A tool key: bold, underline or a list, on the part of the text the cursor is in. */
  protected apply(command: 'bold' | 'underline' | 'insertUnorderedList' | 'insertOrderedList'): void {
    this.exec(command);
  }

  protected onSize(event: Event): void {
    this.exec('fontSize', (event.target as HTMLSelectElement).value);
  }

  /** A color for the text, or the usual one (null). */
  protected setColor(color: string | null): void {
    this.exec('foreColor', color ?? USUAL_COLOR_MARK);
  }

  protected onInput(): void {
    this.updateTools();
    this.sendSoon();
  }

  /** Pasted text comes in as plain text: the marks of another program are not the notes'. */
  protected onPaste(event: ClipboardEvent): void {
    event.preventDefault();
    const text = event.clipboardData?.getData('text/plain') ?? '';
    if (text) this.exec('insertText', text);
  }

  /** The keys of the editor: Ctrl + B and Ctrl + U work as everywhere; italic is no mark of the notes. */
  protected onKeyDown(event: KeyboardEvent): void {
    if ((event.ctrlKey || event.metaKey) && event.code === 'KeyI') event.preventDefault();
  }

  protected onSelectionChange(): void {
    if (!this.editing()) return;
    const selection = document.getSelection();
    if (!selection || selection.rangeCount === 0) return;
    const range = selection.getRangeAt(0);
    if (!this.editor().nativeElement.contains(range.commonAncestorContainer)) return;
    this.range = range.cloneRange();
    this.updateTools();
  }

  /** True when the color is the one of the text at the cursor. */
  protected isColor(color: string | null): boolean {
    return this.tools().color === color;
  }

  private exec(command: string, value?: string): void {
    if (!this.editing()) return;
    const editor = this.editor().nativeElement;
    editor.focus();
    if (this.range) {
      document.getSelection()?.removeAllRanges();
      document.getSelection()?.addRange(this.range);
    }
    run(command, value);
    this.updateTools();
    this.sendSoon();
  }

  private updateTools(): void {
    if (typeof document.queryCommandState !== 'function') return;
    try {
      const color = hexColor(document.queryCommandValue('foreColor') || '');
      const usual = hexColor(getComputedStyle(this.editor().nativeElement).color);
      this.tools.set({
        bold: document.queryCommandState('bold'),
        underline: document.queryCommandState('underline'),
        bulleted: document.queryCommandState('insertUnorderedList'),
        numbered: document.queryCommandState('insertOrderedList'),
        size: document.queryCommandValue('fontSize') || USUAL_SIZE,
        color: color === usual || color === USUAL_COLOR_MARK ? null : color,
      });
    } catch {
      // A browser (or a test) without the editor's questions: the tool keys stay faint.
    }
  }

  private sendSoon(): void {
    if (this.sendTimer !== null) clearTimeout(this.sendTimer);
    this.sendTimer = setTimeout(() => this.send(), SEND_AFTER_MS);
  }

  /** What is written goes to the desk program now, if it differs from what it keeps. */
  private send(): void {
    if (this.sendTimer !== null) clearTimeout(this.sendTimer);
    this.sendTimer = null;
    if (!this.editing()) return;

    const notes = cleanNotes(this.editor().nativeElement.innerHTML);
    if (notes === this.store.notes()) return;
    this.store.setNotes(notes).catch((error: Error) => this.error.set(error.message));
  }
}

// One command of the browser's editor; a browser (or a test) without it does nothing.
function run(command: string, value?: string): void {
  if (typeof document.execCommand === 'function') document.execCommand(command, false, value);
}
