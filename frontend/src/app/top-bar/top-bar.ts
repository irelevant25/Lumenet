import { Component, computed, inject, viewChild } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { DeskSettings } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import { lanAddressInUse } from '../desk/desk-types';
import { viewNamed } from '../desk/views';
import { ExportDialog } from '../project/export-dialog/export-dialog';
import { ImportDialog } from '../project/import-dialog/import-dialog';
import { LampButton } from '../shared/lamp-button/lamp-button';
import { OutputsStatus } from '../outputs/outputs-status/outputs-status';
import { ProjectDialog } from '../project/project-dialog/project-dialog';

/**
 * The strip along the top: name and version with the outputs (how many play; Settings shows them)
 * and the LAN key, the links to the views (those Settings does not leave out), and the project with
 * its keys New, Save, Load, Import and Export.
 */
@Component({
  host: {
    role: 'banner',
  },
  imports: [ExportDialog, ImportDialog, LampButton, OutputsStatus, ProjectDialog, RouterLink, RouterLinkActive],
  selector: 'lm-top-bar',
  styleUrl: './top-bar.css',
  templateUrl: './top-bar.html',
})
export class TopBar {
  protected readonly store = inject(DeskStore);
  private readonly settings = inject(DeskSettings);
  protected readonly dialog = viewChild.required(ProjectDialog);
  protected readonly importDialog = viewChild.required(ImportDialog);
  protected readonly exportDialog = viewChild.required(ExportDialog);

  /** The links to the views: in the order Settings gives them, all but those it leaves out. */
  protected readonly links = computed(() => {
    const hidden = this.settings.hiddenViews();
    return this.settings
      .viewOrder()
      .filter((name) => !hidden.includes(name))
      .map(viewNamed);
  });

  /** The LAN key, on the PC only: a page on a phone cannot switch LAN, and needs no key for it. */
  protected readonly lanSwitch = computed(() => {
    const lan = this.store.lan();
    return lan?.local ? lan : null;
  });

  /**
   * The line under the box light while LAN is lit: the address to open on a phone (the one
   * chosen in Settings, or the best one), and the password.
   */
  protected readonly lanLine = computed(() => {
    const lan = this.lanSwitch();
    if (!lan?.on) return null;

    const address = lanAddressInUse(lan);
    // With an address chosen, devices come in there only; without, at the others as well.
    const others = lan.chosen && address?.split(':')[0] === lan.chosen ? [] : lan.addresses.filter((other) => other !== address);
    const more = others.length > 0 ? ` It can also be reached at ${others.join(', ')}.` : '';
    return {
      address: address ?? 'no network',
      // A moment after the key was pressed, until the desk has answered
      password: lan.password ?? '…',
      hint: address
        ? `On a phone or tablet in the same network, open http://${address} and give the password.${more}`
        : 'This PC is in no network that a phone could reach it through.',
    };
  });
}
