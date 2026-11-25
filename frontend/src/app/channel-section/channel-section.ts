import { Component, computed, inject } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { UNIVERSE_SIZE, stripNumbers } from '../desk/desk-types';
import { channelKeysArmed } from '../desk/function-mode';
import { LampLevel, channelLamp, channelPageLamp } from '../desk/lamps';
import { DragScroll } from '../desk/drag-scroll';
import { PressedInSight } from '../desk/pressed-in-sight';
import { channelLabel, channelNumber, universeStart } from '../desk/universes';
import { FaderStrip } from '../fader-strip/fader-strip';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * The channels: two rows of faders (or one, as Settings says), and the keys that choose which
 * channels the rows show. In a project of several universes a key for each (U1, U2 ...) stands
 * above the faders, and the page keys are those of the universe shown. The page keys stand in one
 * column, which scrolls when they are too many for its height and keeps the lit one in sight; the
 * rows scroll sideways only when a channel size chosen in Settings makes them wider than the
 * window. The key under a fader puts it to full, and back to zero.
 */
@Component({
  imports: [DragScroll, FaderStrip, LampButton, PressedInSight],
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

  /**
   * The U keys, in a project of more than one universe: full for the universe shown, half for
   * one with a channel above zero.
   */
  protected readonly universeKeys = computed(() => {
    const universes = this.store.universes();
    if (universes < 2) return [];
    const levels = this.store.shownChannels();
    const shown = this.store.shownUniverse();
    return Array.from({ length: universes }, (_, index) => ({
      universe: index + 1,
      shown: index + 1 === shown,
      lamp: channelPageLamp(levels, universeStart(index + 1), UNIVERSE_SIZE, index + 1 === shown),
    }));
  });

  /** The page keys of the universe shown: full for the page that is shown, half for a page with a channel above zero. */
  protected readonly pages = computed(() => {
    const levels = this.store.shownChannels();
    const perPage = this.store.channelsPerPage();
    const shown = this.store.firstChannel();
    const start = universeStart(this.store.shownUniverse());
    return Array.from({ length: UNIVERSE_SIZE / perPage }, (_, page) => {
      const first = start + page * perPage;
      const last = first + perPage - 1;
      return {
        first,
        from: channelNumber(first),
        to: channelNumber(last),
        name: `Channels ${channelNumber(first)} to ${channelLabel(last)}`,
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

  /** The number on the key under a fader: its number in its universe, 1 to 512. */
  protected caption(channel: number): number {
    return channelNumber(channel);
  }

  /** What a screen reader calls the fader: "Channel 113 (U2)". */
  protected called(channel: number): string {
    return `Channel ${channelLabel(channel)}`;
  }

  /** The steps of the sequence being recorded that use this channel. */
  protected steps(channel: number): readonly number[] | undefined {
    return this.store.recordedSteps()?.channels.get(channel);
  }
}
