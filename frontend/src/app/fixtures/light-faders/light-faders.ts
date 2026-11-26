import { Component, computed, inject, input, signal } from '@angular/core';
import { DeskStore } from '../../desk/desk-store';
import { StageLight } from '../../desk/desk-types';
import { rangeAt, rangeText, typeOf, valuesOf } from '../fixtures';
import { LETTER_COLORS, LETTER_NAMES } from '../../stage/stage-lights';
import { channelLabel, channelNumber } from '../../desk/universes';
import { Fader } from '../../shared/fader/fader';

/**
 * The faders of a light of the stage plan: one for each of its channels, in their order, each
 * with its level above it and under it the letter of what it is (R red, D dimmer, P pan) and the
 * channel's number; the pointer on the letter names it, for a light of the library with the
 * library's name and what the channel does at its values. Under the faders a line says what the
 * fader moved last does at its value ("S (8) at 32: Open (32 to 63)"), so the manual of the light
 * is seldom needed. They are the channel faders of the main view: what one does here, the other
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

  /** The channel whose fader was moved last, or reached with the keyboard; null for none yet. */
  private readonly touched = signal<number | null>(null);

  protected readonly faders = computed(() => {
    const light = this.light();
    const own = this.store.channels();
    const shown = this.store.shownChannels();
    const type = typeOf(this.store.stage(), light);
    return [...light.channels].map((letter, offset) => {
      const channel = light.address + offset;
      const kind = type?.channels[offset];
      const name = kind?.name ?? LETTER_NAMES[letter] ?? 'no light';
      const ranges = kind?.ranges ?? [];
      const level = shown[channel - 1] ?? 0;
      const band = rangeAt(ranges, level);
      return {
        channel,
        // under the fader: its number in its universe (the light says its universe above)
        number: channelNumber(channel),
        letter,
        level,
        own: own[channel - 1] ?? 0,
        label: `Channel ${channelLabel(channel)}, ${name}`,
        name,
        band,
        // the pointer on the letter: what the channel is, and what it does at its values
        about: [name, ...ranges.map((range) => `${valuesOf(range)}: ${range.label}`)].join('\n'),
        // what a screen reader says for its value: the number, and what it does there
        valueText: band ? `${level}, ${band.label}` : null,
        color: LETTER_COLORS[letter] ?? (letter === 'D' ? 'var(--channel)' : 'var(--legend-dim)'),
      };
    });
  });

  /** True for a light with a channel that does several things at its values. */
  protected readonly hasBands = computed(() => typeOf(this.store.stage(), this.light())?.channels.some((channel) => channel.ranges?.length) ?? false);

  /** What the fader moved last does at its value: "S (8) at 32: Open (32 to 63)"; null before one was moved. */
  protected readonly said = computed(() => {
    const fader = this.faders().find((candidate) => candidate.channel === this.touched());
    if (!fader) return null;
    return `${fader.letter} (${fader.number}) at ${fader.level}: ${fader.band ? rangeText(fader.band) : fader.name}`;
  });

  protected move(channel: number, level: number): void {
    this.touched.set(channel);
    this.store.setChannel(channel, level);
  }

  protected touch(channel: number): void {
    this.touched.set(channel);
  }
}
