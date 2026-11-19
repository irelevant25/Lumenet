import { DatePipe } from '@angular/common';
import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { ProjectFile } from '../desk/desk-types';
import { LampButton } from '../lamp-button/lamp-button';

/** What the window is asked to do. */
export type ProjectTask = 'save' | 'load' | 'new';

/**
 * The window for saving the project under a name, for choosing a project to load,
 * or for confirming that a new, empty project is wanted.
 */
@Component({
  imports: [DatePipe, LampButton],
  selector: 'lm-project-dialog',
  styleUrl: './project-dialog.css',
  templateUrl: './project-dialog.html',
})
export class ProjectDialog {
  private readonly store = inject(DeskStore);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  protected readonly task = signal<ProjectTask>('save');
  /** The folder on the PC where the backend keeps the project files. */
  protected readonly folder = signal('');
  protected readonly projects = signal<ProjectFile[]>([]);
  /** The name typed in (save) or picked from the list (load). */
  protected readonly name = signal('');
  protected readonly error = signal('');
  protected readonly busy = signal(false);

  /** Saving under a name that exists replaces that project. */
  protected readonly replaces = computed(
    () => this.task() === 'save' && this.exists(this.name().trim()),
  );

  /** Loading or starting anew throws away what was changed and not saved. */
  protected readonly losesChanges = computed(
    () => this.task() !== 'save' && this.store.project().unsaved,
  );

  protected readonly title = computed(() => TITLES[this.task()]);
  /** What the key that confirms says, and its full name for screen readers. */
  protected readonly confirmLabel = computed(() => CONFIRM_LABELS[this.task()]);

  protected readonly canConfirm = computed(() => {
    if (this.busy()) return false;
    if (this.task() === 'new') return true;

    const name = this.name().trim();
    if (name === '') return false;
    return this.task() === 'save' || this.exists(name);
  });

  async open(task: ProjectTask): Promise<void> {
    const current = this.store.project();
    this.task.set(task);
    this.error.set('');
    this.busy.set(false);
    // A project that already has a file keeps its name. A new one waits for a name to be typed.
    this.name.set(task === 'save' && current.path !== null ? current.name : '');
    this.dialog().nativeElement.showModal();
    if (task === 'new') return; // nothing to choose: only yes or no

    try {
      const list = await this.store.listProjects();
      this.folder.set(list.folder);
      this.projects.set(list.projects);
    } catch (error) {
      this.error.set(messageOf(error));
    }
  }

  protected close(): void {
    this.dialog().nativeElement.close();
  }

  protected onNameInput(event: Event): void {
    this.name.set((event.target as HTMLInputElement).value);
  }

  protected pick(name: string): void {
    this.name.set(name);
  }

  protected async confirm(event: Event): Promise<void> {
    event.preventDefault(); // the form must not close the window before the backend has answered
    if (!this.canConfirm()) return;

    const name = this.name().trim();
    this.busy.set(true);
    this.error.set('');
    try {
      if (this.task() === 'new') await this.store.newProject();
      else if (this.task() === 'save') await this.store.saveProject(name);
      else await this.store.loadProject(name);
      this.close();
    } catch (error) {
      this.error.set(messageOf(error));
    } finally {
      this.busy.set(false);
    }
  }

  /** Windows does not tell "Show" from "show", so neither does this check. */
  private exists(name: string): boolean {
    const wanted = name.toLowerCase();
    return this.projects().some((project) => project.name.toLowerCase() === wanted);
  }
}

const TITLES: Record<ProjectTask, string> = {
  save: 'Save project',
  load: 'Load project',
  new: 'New project',
};

const CONFIRM_LABELS: Record<ProjectTask, { key: string; name: string }> = {
  save: { key: 'Save', name: 'Save project' },
  load: { key: 'Load', name: 'Load project' },
  new: { key: 'New', name: 'Start a new project' },
};

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
