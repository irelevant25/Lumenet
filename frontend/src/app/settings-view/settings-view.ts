import { Component, computed, inject, signal } from '@angular/core';
import {
  CHANNEL_SIZES,
  ChannelSize,
  DeskSettings,
  FONT_SCALE_MAX,
  FONT_SCALE_MIN,
  ROW_COUNTS,
  RowCount,
  WHEEL_SIZES,
  WheelSize,
} from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import {
  CHANNEL_COUNT,
  CHANNEL_INDEPENDENT_COUNT,
  MEMORY_COUNT,
  MEMORY_INDEPENDENT_COUNT,
  lanAddressInUse,
} from '../desk/desk-types';

/**
 * Settings: what this browser shows of the main view and how, the size of all lettering, and the
 * LAN address and password of the desk program. (The keyboard shortcuts have a view of their own.)
 *
 * - The main view: how many channels a page holds (chosen by the window, or fixed), in one or two
 *   rows of faders, and one or two rows of memory faders; rows wider than the window scroll
 *   sideways. How many steps the stack wheel shows, and how many keys of each kind the
 *   independents show (0 to 32; none of either: no independents at all).
 * - The size of all lettering.
 * - While LAN is on: the address of this PC that phones use, and the password. On the PC only.
 *
 * Everything but LAN belongs to this browser on this device, see desk-settings.ts.
 */
@Component({
  selector: 'lm-settings-view',
  styleUrl: './settings-view.css',
  templateUrl: './settings-view.html',
})
export class SettingsView {
  protected readonly settings = inject(DeskSettings);
  protected readonly store = inject(DeskStore);

  // ---------------------------------------------------------------- layout and lettering

  protected readonly sizes = CHANNEL_SIZES;
  protected readonly rowCounts = ROW_COUNTS;
  protected readonly wheelSizes = WHEEL_SIZES;
  protected readonly maxChannelKeys = CHANNEL_INDEPENDENT_COUNT;
  protected readonly maxMemoryKeys = MEMORY_INDEPENDENT_COUNT;
  protected readonly smallest = FONT_SCALE_MIN;
  protected readonly biggest = FONT_SCALE_MAX;

  /** What the chosen channel size and rows make of the main view, in words. */
  protected readonly sizeSaid = computed(() => {
    const perRow = this.store.stripsPerRow();
    const channels = this.store.channelsPerPage();
    const memories = this.store.memoriesPerPage();
    const layout =
      `${rows(this.settings.channelRows(), perRow)}, ${CHANNEL_COUNT / channels} pages; ` +
      `memories: ${rows(this.settings.memoryRows(), perRow)}, ${MEMORY_COUNT / memories} pages`;
    return this.settings.channelSize() === 'auto'
      ? `This window shows ${channels} channels on a page now: ${layout}.`
      : `${channels} channels on a page: ${layout}. Rows wider than the window scroll sideways.`;
  });

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

  protected readonly fontScaleSaid = computed(() => `×${this.settings.fontScale().toFixed(1)}`);

  // ---------------------------------------------------------------- LAN

  /** LAN as the desk program has it, for a page on the PC; null on another device and with an older desk program. */
  protected readonly lan = computed(() => {
    const lan = this.store.lan();
    return lan?.local ? lan : null;
  });

  /** The address phones use now. */
  protected readonly addressInUse = computed(() => {
    const lan = this.lan();
    return lan ? lanAddressInUse(lan) : null;
  });

  /** True while an address is chosen that this PC does not have now. */
  protected readonly chosenGone = computed(() => {
    const lan = this.lan();
    return !!lan?.chosen && !lan.addresses.some((address) => address.split(':')[0] === lan.chosen);
  });

  protected readonly passwordProblem = signal('');
  protected readonly passwordDone = signal('');

  protected onAddress(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.store.setLanAddress(value === '' ? null : value);
  }

  protected async setPassword(field: HTMLInputElement): Promise<void> {
    this.passwordProblem.set('');
    this.passwordDone.set('');
    try {
      await this.store.setLanPassword(field.value);
      field.value = '';
      this.passwordDone.set('The new password is set. Every phone and tablet has to give it now.');
    } catch (error) {
      this.passwordProblem.set(plainMessage(error));
    }
  }

  protected newPassword(): void {
    this.passwordProblem.set('');
    this.passwordDone.set('A new password is made. Every phone and tablet has to give it now.');
    this.store.newLanPassword();
  }
}

/** "two rows of 16", "one row of 32". */
function rows(count: number, perRow: number): string {
  return `${count === 1 ? 'one row' : 'two rows'} of ${perRow}`;
}

/** The desk's own sentence from a refusal, without what SignalR puts before it. */
function plainMessage(error: unknown): string {
  const text = error instanceof Error ? error.message : String(error);
  return text.replace(/^.*HubException: /, '');
}
