import { Component, computed, inject, signal } from '@angular/core';
import { switchNumber, withBox, withOutput, withoutBox } from '../desk/boxes';
import { DeskStore } from '../desk/desk-store';
import { BoxPlan, BoxView, MAX_BOX_OUTPUTS } from '../desk/desk-types';
import { LampButton } from '../lamp-button/lamp-button';

/** The address of the box with the Pico, which has no switch. */
const PICO_BOX = '192.168.7.1';

/**
 * The dmxbox section of the Outputs window: every box with its address, whether it answers, and
 * for each of its outputs the universe it plays. While the project lists no boxes, every box that
 * answers plays by itself, in the order of the boxes (see BoxWiring in the desk program); the
 * first change here makes the project's list, which the project keeps, and Auto empties it again.
 * A box can be added by hand before it is there; it shows as not connected until it answers. A
 * box that answers but is not on the list is new, and plays nothing until it is added.
 */
@Component({
  imports: [LampButton],
  selector: 'lm-box-outputs',
  styleUrls: ['../outputs-dialog/output-section.css', './box-outputs.css'],
  templateUrl: './box-outputs.html',
})
export class BoxOutputs {
  protected readonly store = inject(DeskStore);

  protected readonly outputCounts = Array.from({ length: MAX_BOX_OUTPUTS }, (_, index) => index + 1);
  protected readonly busy = signal(false);
  protected readonly error = signal('');
  /** The outputs of a box to add by hand. */
  protected readonly newOutputs = signal(1);

  protected readonly boxes = computed(() => this.store.boxes());
  /** True while the project lists boxes: then Auto can empty the list. */
  protected readonly listed = computed(() => this.boxes().some((box) => box.kind === 'listed'));
  protected readonly locked = computed(() => this.store.live() || this.busy());
  /** The universes an output can play: those of the project. */
  protected readonly universeChoices = computed(() => Array.from({ length: this.store.universes() }, (_, index) => index + 1));

  /** The window opens: an old message goes. */
  reset(): void {
    this.error.set('');
  }

  /** What stands under a box's address: the number on its switch, or that it is the box with the Pico; its firmware. */
  protected about(box: BoxView): string {
    const number = switchNumber(box.address);
    const which = number !== null ? `Switch ${number}` : box.address === PICO_BOX ? 'Pico' : '';
    const firmware = box.firmware ? `firmware ${box.firmware}` : '';
    return [which, firmware].filter((part) => part !== '').join(', ');
  }

  protected state(box: BoxView): string {
    if (box.kind === 'new') return 'New, plays nothing';
    return box.connected ? 'Connected' : 'Not connected';
  }

  /** The universe an output plays now is offered even when the project has fewer (a list kept from before). */
  protected beyond(universe: number): boolean {
    return universe > this.store.universes();
  }

  protected onOutput(box: BoxView, output: number, event: Event): void {
    const universe = Number((event.target as HTMLSelectElement).value);
    void this.send(withOutput(this.boxes(), box.address, output, universe));
  }

  protected add(box: BoxView): void {
    void this.send(withBox(this.boxes(), box.address, box.outputs, this.store.universes()));
  }

  protected remove(box: BoxView): void {
    void this.send(withoutBox(this.boxes(), box.address));
  }

  /** Back to automatic: the project lists no boxes, and every box that answers plays by itself. */
  protected automatic(): void {
    void this.send([]);
  }

  protected onNewOutputs(event: Event): void {
    this.newOutputs.set(Number((event.target as HTMLSelectElement).value));
  }

  /** A box added by hand: its address, and as many outputs as chosen. The desk program checks the address. */
  protected async addByHand(event: Event, field: HTMLInputElement): Promise<void> {
    event.preventDefault(); // the form must not close the window
    const address = field.value.trim();
    if (address === '') {
      this.error.set('Type the address of the box, like 10.1.2.1 for the box with the number 2 on its switch.');
      return;
    }
    if (await this.send(withBox(this.boxes(), address, this.newOutputs(), this.store.universes()))) field.value = '';
  }

  // The new list goes to the desk program; it answers every page with the boxes. True when it took it.
  private async send(plans: BoxPlan[]): Promise<boolean> {
    if (this.locked()) return false;
    this.busy.set(true);
    this.error.set('');
    try {
      await this.store.setBoxes(plans);
      return true;
    } catch (error) {
      this.error.set(error instanceof Error ? error.message : String(error));
      return false;
    } finally {
      this.busy.set(false);
    }
  }
}
