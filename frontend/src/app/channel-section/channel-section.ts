import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { CHANNEL_COUNT, stripNumbers } from '../desk/desk-types';
import { LampLevel, channelLamp, channelPageLamp } from '../desk/lamps';
import { FaderStrip } from '../fader-strip/fader-strip';
import { LampButton } from '../lamp-button/lamp-button';

/** The channels: two rows of faders, and the keys that choose which channels the rows show. */
@Component({
  imports: [FaderStrip, LampButton],
  selector: 'lm-channel-section',
  styleUrl: './channel-section.css',
  templateUrl: './channel-section.html',
})
export class ChannelSection {
  protected readonly store = inject(DeskStore);

  /** The channel numbers of the two rows on the page that is shown. */
  protected readonly rows = computed(() => {
    const first = this.store.firstChannel();
    const perRow = this.store.stripsPerRow();
    return [stripNumbers(first, perRow), stripNumbers(first + perRow, perRow)];
  });

  /** The page keys: full for the page that is shown, half for a page with a channel above zero. */
  protected readonly pages = computed(() => {
    const levels = this.store.shownChannels();
    const perPage = this.store.channelsPerPage();
    const shown = this.store.firstChannel();
    return Array.from({ length: CHANNEL_COUNT / perPage }, (_, page) => {
      const first = page * perPage + 1;
      return {
        first,
        last: first + perPage - 1,
        shown: first === shown,
        lamp: channelPageLamp(levels, first, perPage, first === shown),
      };
    });
  });

  /** Lit when the channel is above zero, whether by its own fader or by a memory. */
  protected lamp(channel: number): LampLevel {
    return channelLamp(this.store.shownChannels()[channel - 1]);
  }
}