import { Component, computed, inject, input } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { StageLight } from '../desk/desk-types';
import { typeOf } from '../desk/fixtures';
import { LETTER_COLORS, LETTER_NAMES } from '../desk/stage-lights';
import { Fader } from '../fader/fader';

/**
 * The faders of a light of the stage plan: one for each of its channels, in their order, each
 * with its level above it and under it the letter of what it is (R red, D dimmer, P pan) and the
 * channel's number; the pointer on the letter names it, for a light of the library with the
 * library's name. They are the channel faders of the main view: what one does here, the other
 * shows at once. A memory that holds a channel up holds its fader up here too.
 */
@Component({
  imports: [Fader],
  selector: 'lm-light-faders',
  styleUrl: './light-faders.css',
  templateUrl: './light-faders.html',
})
export class LightFaders {
  protected readonly store = inject(DeskStore);

  readonly light = input.required<StageLight>();

  protected readonly faders = computed(() => {
    const light = this.light();
    const own = this.store.channels();
    const shown = this.store.shownChannels();
    const type = typeOf(this.store.stage(), light);
    return [...light.channels].map((letter, offset) => {
      const channel = light.address + offset;
      const name = type?.channels[offset]?.name ?? LETTER_NAMES[letter] ?? 'no light';
      return {
        channel,
        letter,
        level: shown[channel - 1] ?? 0,
        own: own[channel - 1] ?? 0,
        label: `Channel ${channel}, ${name}`,
        name,
        color: LETTER_COLORS[letter] ?? (letter === 'D' ? 'var(--channel)' : 'var(--legend-dim)'),
      };
    });
  });
}
