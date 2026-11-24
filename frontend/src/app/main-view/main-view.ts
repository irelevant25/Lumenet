import { Component, computed, inject } from '@angular/core';
import { ChannelSection } from '../channel-section/channel-section';
import { DeskSettings } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import { Independents } from '../independents/independents';
import { GeneralSection } from '../general-section/general-section';
import { MasterSection } from '../master-section/master-section';
import { MemorySection } from '../memory-section/memory-section';
import { RecordSection } from '../record-section/record-section';
import { StackSection } from '../stack-section/stack-section';

/**
 * The main view: the channels, the memories, and at the right the stack across the top of three
 * columns: the master section over the record keys, the general section, and at the right edge
 * the independents (none when Settings shows no keys).
 *
 * What Settings says about the main view goes onto its element as variables, so that it holds
 * for the main view only: --line (faders side by side, for a channel size chosen in Settings;
 * the Memories and Sequences views keep the window's), the rows of channels and of memories, and
 * the steps of the stack wheel.
 */
@Component({
  host: {
    '[style.--line]': 'line()',
    '[style.--channel-share]': 'channelShare()',
    '[style.--memory-share]': 'memoryShare()',
    '[style.--wheel-size]': 'wheelSize()',
    '[class.no-independents]': '!independents()',
  },
  imports: [ChannelSection, GeneralSection, Independents, MasterSection, MemorySection, RecordSection, StackSection],
  selector: 'lm-main-view',
  styleUrl: './main-view.css',
  templateUrl: './main-view.html',
})
export class MainView {
  private readonly store = inject(DeskStore);
  private readonly settings = inject(DeskSettings);

  /** Faders side by side in a row, when Settings fixes the channel size; otherwise the window's. */
  protected readonly line = computed(() =>
    this.settings.channelSize() === 'auto' ? null : this.store.stripsPerRow(),
  );

  protected readonly channelShare = computed(() => `${this.settings.channelRows()}fr`);
  protected readonly memoryShare = computed(() => `${this.settings.memoryRows()}fr`);
  protected readonly wheelSize = this.settings.wheelSize;

  /** False when Settings shows no independent key of either kind: then the section is left out. */
  protected readonly independents = computed(
    () => this.settings.channelKeys() + this.settings.memoryKeys() > 0,
  );
}
