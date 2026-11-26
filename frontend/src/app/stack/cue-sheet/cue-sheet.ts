import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { StackStep } from '../../desk/desk-types';
import { DeskStore } from '../../desk/desk-store';
import { momentWords } from '../moments';

/** A line of the cue sheet: one cue of the stack, in words. */
interface SheetLine {
  readonly id: number;
  readonly cue: string;
  readonly what: string;
  readonly note: string;
  readonly value: string;
  readonly with: string;
  readonly up: string;
  readonly wait: string;
  readonly down: string;
  /** The moment of a sound the cue comes at, "0:31.25 of Thunder"; empty for none. */
  readonly at: string;
  readonly skipped: boolean;
}

/**
 * The cue sheet: the stack on paper, for rehearsals and for whoever calls the cues. Every cue with its
 * number, what it plays and its note, its value, Add and Continue, its three times (a wait of 0: it
 * waits for GO), the moment of a sound it comes at, skipped cues struck through, and a column left
 * empty to write in. On the screen it
 * looks like the paper; Print hands it to the browser's printing (Save as PDF too), which leaves out
 * the rest of the desk (styles.css, @media print).
 */
@Component({
  imports: [RouterLink],
  selector: 'lm-cue-sheet',
  styleUrl: './cue-sheet.css',
  templateUrl: './cue-sheet.html',
})
export class CueSheet {
  protected readonly store = inject(DeskStore);

  /** When the sheet was made, as the paper says it. */
  protected readonly made = new Date().toLocaleString(undefined, {
    dateStyle: 'long',
    timeStyle: 'short',
  });

  protected readonly lines = computed<readonly SheetLine[]>(() =>
    this.store.stack().steps.map((step) => this.lineOf(step)),
  );

  /** "14 cues, 2 of them skipped". */
  protected readonly count = computed(() => {
    const lines = this.lines();
    const skipped = lines.filter((line) => line.skipped).length;
    const cues = `${lines.length} ${lines.length === 1 ? 'cue' : 'cues'}`;
    return skipped ? `${cues}, ${skipped} of them skipped` : cues;
  });

  protected print(): void {
    window.print();
  }

  private lineOf(step: StackStep): SheetLine {
    const release = step.target === 'release';
    return {
      id: step.id,
      cue: step.cue,
      what: this.store.stepName(step),
      note: release ? '' : this.store.stepNote(step),
      value: release ? '' : String(step.value),
      with: [step.add ? 'Add' : '', step.continue ? 'Continue' : ''].filter(Boolean).join(', '),
      up: release ? '' : seconds(step.up),
      wait: step.wait > 0 ? seconds(step.wait) : 'GO',
      down: seconds(step.down),
      at: step.at ? momentWords(step.at, this.store.soundsByNumber()) : '',
      skipped: step.disabled === true,
    };
  }
}

/** Seconds as the sheet writes them: "1.5 s", "0 s". */
function seconds(value: number): string {
  return `${Number(value.toFixed(1))} s`;
}
