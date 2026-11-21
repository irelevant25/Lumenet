import { Component, input, output } from '@angular/core';
import { Fader } from '../fader/fader';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * One fixture, or one group of fixtures, on the Fixtures page: its color on top (a press
 * chooses it, and the panel shows its color, where it points and all its channels), its
 * intensity as a number and a fader, its key (to full, and to zero), and its name.
 */
@Component({
  host: {
    '[class.up]': 'level() > 0',
    '[class.chosen]': 'chosen()',
    '[class.group]': 'group()',
  },
  imports: [Fader, LampButton],
  selector: 'lm-fixture-strip',
  styleUrl: './fixture-strip.css',
  templateUrl: './fixture-strip.html',
})
export class FixtureStrip {
  /** The fixture's or the group's name. */
  readonly name = input.required<string>();
  /** What its key says: the fixture's address, or the group's number. */
  readonly keyText = input.required<string>();
  /** Its intensity, 0 to 255. */
  readonly level = input(0);
  /** The intensity the owner asks for with the fader. */
  readonly levelChange = output<number>();
  /** Its color as CSS, like "#ff2000"; null for none to show (a hazer). */
  readonly color = input<string | null>(null);
  /** True while the panel shows it. */
  readonly chosen = input(false);
  /** True for a group: its fader and key are amber, those of a fixture green as the channels. */
  readonly group = input(false);
  /** True while a sequence is recorded: the key blinks, and a press puts in a step. */
  readonly blink = input(false);
  /** While a sequence is recorded: the numbers of its steps that use this fixture or group, shown on the key. */
  readonly steps = input<readonly number[]>([]);
  /** The key was pressed. */
  readonly keyPress = output<void>();
  /** The color on top was pressed: the panel shows this fixture or group. */
  readonly choose = output<void>();
}
