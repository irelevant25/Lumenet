import { Component, computed, inject } from '@angular/core';
import {
  CHANNEL_SIZES,
  ChannelSize,
  DeskSettings,
  ROW_COUNTS,
  RowCount,
  WHEEL_SIZES,
  WheelSize,
  ZOOM_MAX,
  ZOOM_MIN,
} from '../../desk/desk-settings';
import { DeskStore } from '../../desk/desk-store';
import {
  CHANNEL_INDEPENDENT_COUNT,
  MAX_UNIVERSES,
  MEMORY_INDEPENDENT_COUNT,
  UNIVERSE_SIZE,
} from '../../desk/desk-types';
import { EFFECT_INDEPENDENT_COUNT } from '../../effects/effects';
import { SOUND_INDEPENDENT_COUNT } from '../../sounds/sounds';

/** How often the autosave can keep the work, in seconds: as the desk program offers it. */
const AUTOSAVE_INTERVALS: readonly number[] = [5, 30, 60, 300];

/**
 * The Main view part of the General tab of Settings: how many channels a page holds (chosen by the
 * window, or fixed), in one or two rows of faders, and one or two rows of memory faders; rows wider
 * than the window scroll sideways. How many cues the stack wheel shows, and how many keys of each
 * kind the independents show (0 to 32; none of any: no independents at all). Under them the
 * universes of the project (1 to 8; the project keeps the number), the zoom of the page (50 to 200
 * per cent), and the autosave (on or off, and how often; on the PC only).
 */
@Component({
  selector: 'lm-main-view-panel',
  styleUrl: './main-view-panel.css',
  templateUrl: './main-view-panel.html',
})
export class MainViewPanel {
  protected readonly settings = inject(DeskSettings);
  protected readonly store = inject(DeskStore);

  protected readonly sizes = CHANNEL_SIZES;
  protected readonly rowCounts = ROW_COUNTS;
  protected readonly wheelSizes = WHEEL_SIZES;
  protected readonly maxChannelKeys = CHANNEL_INDEPENDENT_COUNT;
  protected readonly maxMemoryKeys = MEMORY_INDEPENDENT_COUNT;
  protected readonly maxEffectKeys = EFFECT_INDEPENDENT_COUNT;
  protected readonly maxSoundKeys = SOUND_INDEPENDENT_COUNT;
  protected readonly smallest = ZOOM_MIN;
  protected readonly biggest = ZOOM_MAX;

  protected sizeName(size: ChannelSize): string {
    return size === 'auto' ? 'Auto (by the window)' : `${size}`;
  }

  protected rowsName(count: RowCount): string {
    return count === 1 ? '1 row' : `${count} rows`;
  }

  protected onSize(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.settings.setChannelSize(value === 'auto' ? 'auto' : (Number(value) as ChannelSize));
  }

  protected onChannelRows(event: Event): void {
    this.settings.setChannelRows(Number((event.target as HTMLSelectElement).value) as RowCount);
  }

  protected onMemoryRows(event: Event): void {
    this.settings.setMemoryRows(Number((event.target as HTMLSelectElement).value) as RowCount);
  }

  protected onWheelSize(event: Event): void {
    this.settings.setWheelSize(Number((event.target as HTMLSelectElement).value) as WheelSize);
  }

  protected onChannelKeys(event: Event): void {
    const field = event.target as HTMLInputElement;
    this.settings.setChannelKeys(Number(field.value));
    field.value = String(this.settings.channelKeys());
  }

  protected onMemoryKeys(event: Event): void {
    const field = event.target as HTMLInputElement;
    this.settings.setMemoryKeys(Number(field.value));
    field.value = String(this.settings.memoryKeys());
  }

  protected onEffectKeys(event: Event): void {
    const field = event.target as HTMLInputElement;
    this.settings.setEffectKeys(Number(field.value));
    field.value = String(this.settings.effectKeys());
  }

  protected onSoundKeys(event: Event): void {
    const field = event.target as HTMLInputElement;
    this.settings.setSoundKeys(Number(field.value));
    field.value = String(this.settings.soundKeys());
  }

  protected onEffectRows(event: Event): void {
    this.settings.setEffectRows((event.target as HTMLSelectElement).value === '1' ? 1 : 0);
  }

  protected onSoundRows(event: Event): void {
    this.settings.setSoundRows((event.target as HTMLSelectElement).value === '1' ? 1 : 0);
  }

  protected readonly zoomSaid = computed(() => `${Math.round(this.settings.zoom() * 100)} %`);

  // ---------------------------------------------------------------- universes

  protected readonly universeCounts = Array.from(
    { length: MAX_UNIVERSES },
    (_, index) => index + 1,
  );
  protected readonly universeSize = UNIVERSE_SIZE;

  /**
   * Another number of universes for the project. The list shows the number the project has until
   * the desk program has taken the new one: it refuses fewer while something uses a channel of a
   * universe that would go, and the top bar says why.
   */
  protected onUniverses(event: Event): void {
    const field = event.target as HTMLSelectElement;
    const universes = Number(field.value);
    field.value = String(this.store.universes());
    this.store.setUniverses(universes);
  }

  // ---------------------------------------------------------------- autosave

  protected readonly autosaveIntervals = AUTOSAVE_INTERVALS;

  /** The autosave of the desk program, for a page on the PC; null on another device and with an older desk program. */
  protected readonly autosave = computed(() =>
    this.store.lan()?.local ? this.store.autosave() : null,
  );

  protected intervalName(seconds: number): string {
    return seconds < 60
      ? `${seconds} seconds`
      : seconds === 60
        ? '1 minute'
        : `${seconds / 60} minutes`;
  }

  protected onAutosaveOn(event: Event): void {
    const autosave = this.autosave();
    if (autosave)
      this.store.setAutosave((event.target as HTMLSelectElement).value === 'on', autosave.seconds);
  }

  protected onAutosaveEvery(event: Event): void {
    const autosave = this.autosave();
    if (autosave)
      this.store.setAutosave(autosave.on, Number((event.target as HTMLSelectElement).value));
  }
}
