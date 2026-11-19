import { Component, computed, inject, viewChild } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { DeskStore } from '../desk/desk-store';
import { LampButton } from '../lamp-button/lamp-button';
import { ProjectDialog } from '../project-dialog/project-dialog';

/**
 * The strip along the top: name and version with the box light and the LAN key, the links to
 * the views, and the project with its keys New, Save and Load.
 */
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

  /** The LAN key, on the PC only: a page on a phone cannot switch LAN, and needs no key for it. */
  protected readonly lanSwitch = computed(() => {
    const lan = this.store.lan();
    return lan?.local ? lan : null;
  });

  /** The line under the box light while LAN is lit: the address to open on a phone, and the password. */
  protected readonly lanLine = computed(() => {
    const lan = this.lanSwitch();
    if (!lan?.on) return null;

    const [best, ...others] = lan.addresses;
    const more = others.length > 0 ? ` It can also be reached at ${others.join(', ')}.` : '';
    return {
      address: best ?? 'no network',
      // A moment after the key was pressed, until the desk has answered
      password: lan.password ?? '…',
      hint: best
        ? `On a phone or tablet in the same network, open http://${best} and give the password.${more}`
        : 'This PC is in no network that a phone could reach it through.',
    };
  });
}
