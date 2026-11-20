import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { CHANNEL_COUNT, stripNumbers } from '../desk/desk-types';
import { channelKeysArmed } from '../desk/function-mode';
import { KeyColumns } from '../desk/key-columns';
import { LampLevel, channelLamp, channelPageLamp } from '../desk/lamps';
import { FaderStrip } from '../fader-strip/fader-strip';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * The channels: two rows of faders (or one, as Settings says), and the keys that choose which
 * channels the rows show. The page keys go on in a second column (and a third...) when one
 * column cannot hold them; the rows scroll sideways when a channel size chosen in Settings makes
 * them wider than the window. The key under a fader puts it to full, and back to zero.
 */
@Component({
  imports: [FaderStrip, KeyColumns, LampButton],
  selector: 'lm-channel-section',
  styleUrl: './channel-section.css',
  templateUrl: './channel-section.html',
})
export class ChannelSection {
  protected readonly store = inject(DeskStore);

  /** The channel numbers of the rows on the page that is shown. */
  protected readonly rows = computed(() => {
    const first = this.store.firstChannel();
    const perRow = this.store.stripsPerRow();
    const count = this.store.channelsPerPage() / perRow;
    return Array.from({ length: count }, (_, row) => stripNumbers(first + row * perRow, perRow));
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

  /**
   * True while a sequence is recorded: then the channel keys blink and can be pressed, and
   * each press puts that channel into the sequence as a step.
   */
  protected readonly armed = computed(() => channelKeysArmed(this.store.mode()));

  /** Lit when the channel is above zero, whether by its own fader or by a memory. */
  protected lamp(channel: number): LampLevel {
    return channelLamp(this.store.shownChannels()[channel - 1]);
  }

  /** The steps of the sequence being recorded that use this channel. */
  protected steps(channel: number): readonly number[] | undefined {
    return this.store.recordedSteps()?.channels.get(channel);
  }
}
