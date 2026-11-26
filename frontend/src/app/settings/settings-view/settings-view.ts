import { Component, ElementRef, Injector, afterNextRender, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { OutputsPanel } from '../../outputs/outputs-panel/outputs-panel';
import { SoundOutputPanel } from '../../sounds/sound-output/sound-output';
import { AboutPanel } from '../about-panel/about-panel';
import { BackupPanel } from '../backup-panel/backup-panel';
import { KeyColorsPanel } from '../key-colors-panel/key-colors-panel';
import { KeyboardPanel } from '../keyboard-panel/keyboard-panel';
import { LanPanel } from '../lan-panel/lan-panel';
import { MainViewPanel } from '../main-view-panel/main-view-panel';
import { SETTINGS_TABS, SettingsTab, tabOf } from '../settings-tabs';
import { ViewsPanel } from '../views-panel/views-panel';

/**
 * Settings, in tabs, in the middle of the view: General two sections side by side, every other tab
 * one section of the same width:
 *
 * - General: the main view (its pages and rows of faders, the stack wheel, the keys of the
 *   independents), the universes of the project, the zoom and the autosave (main-view-panel), and
 *   the views in the top bar (views-panel).
 * - LAN: the address phones come in at and their password (lan-panel), on the PC only.
 * - Sound: the device of the sounds, its outputs, listening, the Soundcraft mixer (sound-output).
 * - Colors: the colors of the keys (key-colors-panel).
 * - Keyboard: the shortcuts (keyboard-panel).
 * - Outputs: the dmxboxes, Art-Net nodes, sACN and DMX USB interfaces of the project
 *   (outputs-panel). The MIDI controllers and OSC have a view of their own (midi-view).
 * - Backup: a copy of everything in a folder, one that Google Drive or another cloud syncs
 *   (backup-panel).
 * - About: the version of Lumenet, a newer one to update to, and the parts it is made with
 *   (about-panel).
 *
 * The tab shown is the fragment of the address (/settings#keyboard), see settings-tabs.ts.
 */
@Component({
  imports: [
    AboutPanel,
    BackupPanel,
    KeyColorsPanel,
    KeyboardPanel,
    LanPanel,
    MainViewPanel,
    OutputsPanel,
    SoundOutputPanel,
    ViewsPanel,
  ],
  selector: 'lm-settings-view',
  styleUrl: './settings-view.css',
  templateUrl: './settings-view.html',
})
export class SettingsView {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  protected readonly tabs = SETTINGS_TABS;

  private readonly fragment = toSignal(this.route.fragment, {
    initialValue: this.route.snapshot.fragment,
  });

  /** The tab shown: the one the address names, General at first. */
  protected readonly shown = computed<SettingsTab>(() => tabOf(this.fragment()));

  /** Shows a tab: the address names it, so that going back to Settings and a reload keep it. */
  protected show(tab: SettingsTab): void {
    if (tab === this.shown()) return;
    void this.router.navigate([], { relativeTo: this.route, fragment: tab, replaceUrl: true });
  }

  /** The arrow keys, Home and End go from tab to tab, as tabs do; the tab under the focus is shown. */
  protected onTabKey(event: KeyboardEvent, index: number): void {
    const count = this.tabs.length;
    const to =
      event.key === 'ArrowRight'
        ? (index + 1) % count
        : event.key === 'ArrowLeft'
          ? (index + count - 1) % count
          : event.key === 'Home'
            ? 0
            : event.key === 'End'
              ? count - 1
              : null;
    if (to === null) return;
    event.preventDefault();
    const tab = this.tabs[to].id;
    this.show(tab);
    afterNextRender(
      () => this.host.nativeElement.querySelector<HTMLElement>(`#settings-tab-${tab}`)?.focus(),
      { injector: this.injector },
    );
  }
}
