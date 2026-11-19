import { Component, computed, inject, viewChild } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { DeskStore } from '../desk/desk-store';
import { LampButton } from '../lamp-button/lamp-button';
import { ProjectDialog } from '../project-dialog/project-dialog';

/** The strip along the top: name and version, the links to the views, and the project. */
@Component({
  host: {
    role: 'banner',
  },
  imports: [LampButton, ProjectDialog, RouterLink, RouterLinkActive],
  selector: 'lm-top-bar',
  styleUrl: './top-bar.css',
  templateUrl: './top-bar.html',
})
export class TopBar {
  protected readonly store = inject(DeskStore);
  protected readonly dialog = viewChild.required(ProjectDialog);

  /** Shown when the pointer rests on the connection light. */
  protected readonly boxHint = computed(() => {
    const box = this.store.box();
    if (!box.connected) return `No dmxbox answers at ${box.host}. The desk works without it.`;
    return `dmxbox at ${box.host}, firmware ${box.firmware ?? 'unknown'}`;
  });
}
