import { Component, computed, inject, signal } from '@angular/core';
import { DeskStore } from '../../desk/desk-store';
import { lanAddressInUse } from '../../desk/desk-types';

/**
 * The LAN tab of Settings: while LAN is on, the address of this PC that phones and tablets come in
 * at, and the password they give. LAN belongs to the desk program: only the PC changes it, and the
 * LAN key at the top left switches it on and off.
 */
@Component({
  selector: 'lm-lan-panel',
  styleUrl: './lan-panel.css',
  templateUrl: './lan-panel.html',
})
export class LanPanel {
  protected readonly store = inject(DeskStore);

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

/** The desk's own sentence from a refusal, without what SignalR puts before it. */
function plainMessage(error: unknown): string {
  const text = error instanceof Error ? error.message : String(error);
  return text.replace(/^.*HubException: /, '');
}
