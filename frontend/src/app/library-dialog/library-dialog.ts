import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { StageLight } from '../desk/desk-types';
import {
  LIBRARY_NAMES,
  LibraryEntry,
  LibraryFixture,
  libraryFixture,
  libraryIndex,
  searchLibrary,
  withLibraryType,
} from '../desk/fixture-library';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * The window that makes a light of the stage plan a light of a fixture library (the Open Fixture
 * Library, or that of QLC+): search the maker and the model in both, choose a light (the list says
 * which library it is from), choose its mode, and the light gets the channels of that mode, each
 * with its name and what it does at its values.
 */
@Component({
  imports: [LampButton],
  selector: 'lm-library-dialog',
  styleUrl: './library-dialog.css',
  templateUrl: './library-dialog.html',
})
export class LibraryDialog {
  private readonly store = inject(DeskStore);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');
  private readonly searchField = viewChild<ElementRef<HTMLInputElement>>('searchBox');

  /** The light that is to become a light of the library. */
  private readonly lightId = signal<number | null>(null);
  protected readonly names = LIBRARY_NAMES;
  /** Every light of both libraries; null until the lists have come. */
  protected readonly entries = signal<LibraryEntry[] | null>(null);
  /** A library whose list could not be read: what went wrong. Its lights are missing from the list. */
  protected readonly problems = signal<string[]>([]);
  /** How many lights each library has in the list. */
  protected readonly counts = computed(() => {
    const entries = this.entries() ?? [];
    return { ofl: entries.filter((entry) => entry.library === 'ofl').length, qlcplus: entries.filter((entry) => entry.library === 'qlcplus').length };
  });
  protected readonly search = signal('');
  protected readonly found = computed(() => searchLibrary(this.entries() ?? [], this.search()));
  /** The model chosen, read from its file; null while the list is shown. */
  protected readonly chosen = signal<LibraryFixture | null>(null);
  protected readonly modeIndex = signal(0);
  protected readonly mode = computed(() => this.chosen()?.modes[this.modeIndex()] ?? null);
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  /** Opens the window for a light. The search stays from the last time. */
  open(light: StageLight): void {
    this.lightId.set(light.id);
    this.chosen.set(null);
    this.error.set('');
    this.dialog().nativeElement.showModal();
    if (this.entries() === null) void this.loadList(false);
    setTimeout(() => this.searchField()?.nativeElement.focus());
  }

  protected close(): void {
    this.dialog().nativeElement.close();
  }

  /** The lists of the libraries: from the browser's copies, or fetched again. */
  protected async loadList(fresh: boolean): Promise<void> {
    this.busy.set(true);
    try {
      const index = await libraryIndex(fetch, Date.now(), fresh);
      this.entries.set(index.entries);
      this.problems.set(index.problems);
      this.error.set('');
    } catch (error) {
      this.error.set((error as Error).message);
      if (this.entries() === null) this.entries.set([]);
    } finally {
      this.busy.set(false);
    }
  }

  protected onSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  /** A model of the list: its file is read, and its modes are shown. */
  protected async choose(entry: LibraryEntry): Promise<void> {
    this.busy.set(true);
    try {
      const fixture = await libraryFixture(entry.source, entry.maker);
      if (fixture.modes.length === 0) throw new Error(`The library has no mode of ${entry.model}.`);
      this.chosen.set(fixture);
      this.modeIndex.set(0);
      this.error.set('');
    } catch (error) {
      this.error.set((error as Error).message);
    } finally {
      this.busy.set(false);
    }
  }

  protected onMode(event: Event): void {
    this.modeIndex.set(Number((event.target as HTMLSelectElement).value));
  }

  protected back(): void {
    this.chosen.set(null);
    this.error.set('');
  }

  /** The light becomes the chosen model in the chosen mode. */
  protected use(): void {
    const fixture = this.chosen();
    const mode = this.mode();
    const id = this.lightId();
    if (!fixture || !mode || id === null) return;
    this.store.setStage(withLibraryType(this.store.stage(), id, fixture, mode));
    this.close();
  }
}
