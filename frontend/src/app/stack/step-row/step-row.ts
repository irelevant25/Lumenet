import {
  ChangeDetectorRef,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import {
  MAX_LEVEL,
  MAX_STEP_SECONDS,
  MEMORY_COUNT,
  StepKind,
  TimedStep,
  UNIVERSE_SIZE,
} from '../../desk/desk-types';
import { DeskStore } from '../../desk/desk-store';
import { StepChoice, StepChoices, channelsOf, withCurrent } from '../step-choices';
import { channelNumber, universeOf, universeStart } from '../../desk/universes';

/** One line of the field for the kind: "channel:2" is a channel of universe 2; with the color of that kind. */
interface KindChoice {
  readonly value: string;
  readonly text: string;
  readonly color: string;
}

/**
 * The tone of the keys each kind of step takes: a channel and a fixture green, a memory red, a group
 * amber, an effect violet, a sound cyan, a release white.
 */
const KIND_TONES: Record<StepKind, string> = {
  channel: 'channel',
  memory: 'memory',
  fixture: 'channel',
  group: 'system',
  effect: 'effect',
  sound: 'sound',
  release: 'white',
};

/** A tone of the keys as a color: "memory" is var(--key-memory). */
function keyColor(tone: string): string {
  return `var(--key-${tone})`;
}

/** The three times of a step. */
type TimeField = 'up' | 'wait' | 'down';

/**
 * The one row, of all tables, whose field for the number holds the whole list right now: the
 * row whose field is in use. See the note about the list at StepRow.
 */
const rowWithList = signal<StepRow | null>(null);

/**
 * One row of a table of steps: what the step stands for (a channel of a universe, a memory, a
 * fixture or a group, and which), the note of that channel or memory or the name of the fixture
 * or group (with the color of a fixture or group before it, a press on which asks for another),
 * the step's value, and its three times. Put it on a table row: `<tr lmStepRow>`. Cells that
 * belong before or after these seven are given by whoever uses the row, marked `before` and `after`.
 *
 * The kind says the universe of a channel: "Channel (U1)", "Channel (U2)" ... The number is
 * then chosen from that universe's channels, 1 to 512, each with the note under its fader, like
 * "5: Front left". The list is long, so only one row carries it at a time: the one whose field
 * is in use. A table of a hundred steps would otherwise hold fifty thousand entries. Every other
 * row holds just its number, which is also all a closed field shows: the note has a column of
 * its own.
 *
 * A typed field is taken over when it is left or Enter is pressed. A text that is no number in
 * the allowed range is put back as it was.
 */
@Component({
  host: { '[style.--kind]': 'kindColor()' },
  // A row of the table itself: an element of its own would break the table.
  // eslint-disable-next-line @angular-eslint/component-selector
  selector: 'tr[lmStepRow]',
  styleUrl: './step-row.css',
  templateUrl: './step-row.html',
})
export class StepRow {
  private readonly changes = inject(ChangeDetectorRef);
  private readonly store = inject(DeskStore);

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      if (rowWithList() === this) rowWithList.set(null);
    });
  }

  readonly step = input.required<TimedStep>();
  /** What a screen reader calls this row, for example "Step 2". */
  readonly label = input.required<string>();
  /**
   * The numbers a step can be given, for a channel and for a memory, each with its note; also the
   * effects, which makes Effect one of the kinds, and the steps a release can take down, which
   * makes Release one.
   */
  readonly choices = input.required<StepChoices>();
  /** The step itself, which a release cannot take down: its id in the stack, its number in a sequence. */
  readonly self = input<number | null>(null);
  /** The note under the fader of the step's channel or memory, as one line. It is shown, not changed here. */
  readonly note = input('');
  /** True while the step cannot be changed: the stack is live. */
  readonly locked = input(false);
  /** The step with one field changed. */
  readonly stepChange = output<TimedStep>();
  /** The color of a step of a fixture or a group was pressed: whoever uses the row asks for another. */
  readonly colorPick = output<void>();

  protected readonly maxSeconds = MAX_STEP_SECONDS;

  /** True while this row's field for the number holds the whole list. */
  protected readonly listed = computed(() => rowWithList() === this);

  /** True for a step of a fixture or a group, which has a color. */
  protected readonly lights = computed(() => isLights(this.step().target));

  /**
   * The color of the step's kind, which its dropdowns get as a key that is half lit: a channel and a
   * fixture green, a memory red, a group amber, an effect violet, a sound cyan, a release white.
   */
  protected readonly kindColor = computed(() => keyColor(KIND_TONES[this.step().target]));

  /** True for a release: it takes another step down, and has no value and no up time. */
  protected readonly release = computed(() => this.step().target === 'release');

  /** The universes of the project: as many as the channels on offer fill. */
  private readonly universes = computed(() => Math.max(1, Math.ceil(this.choices().channel.length / UNIVERSE_SIZE)));

  /** The steps a release of this row can take down: all but itself. Undefined where there are no releases. */
  private readonly releasable = computed(() => this.choices().release?.filter((choice) => choice.number !== this.self()));

  /**
   * The lines of the field for the kind: a channel of each universe, then memory, fixture, group,
   * effect, sound and release; each in the color of its kind, so the open list shows them as the
   * rows do.
   */
  protected readonly kinds = computed<KindChoice[]>(() => {
    const color = (kind: StepKind): string => keyColor(KIND_TONES[kind]);
    return [
      ...Array.from({ length: this.universes() }, (_, index) => ({
        value: `channel:${index + 1}`,
        text: `Channel (U${index + 1})`,
        color: color('channel'),
      })),
      { value: 'memory', text: 'Memory', color: color('memory') },
      { value: 'fixture', text: 'Fixture', color: color('fixture') },
      { value: 'group', text: 'Group', color: color('group') },
      ...(this.choices().effect ? [{ value: 'effect', text: 'Effect', color: color('effect') }] : []),
      ...(this.choices().sound ? [{ value: 'sound', text: 'Sound', color: color('sound') }] : []),
      ...(this.releasable() ? [{ value: 'release', text: 'Release', color: color('release') }] : []),
    ];
  });

  /** What a screen reader calls the field for the kind. */
  protected readonly kindLabel = computed(() => {
    const kinds = ['channel', 'memory', 'fixture', 'group'];
    if (this.choices().effect) kinds.push('effect');
    if (this.choices().sound) kinds.push('sound');
    if (this.releasable()) kinds.push('release');
    return `${this.label()}, ${kinds.slice(0, -1).join(', ')} or ${kinds.at(-1)}`;
  });

  /** What a screen reader calls the field for the number. */
  protected readonly numberLabel = computed(() =>
    this.release() ? `${this.label()}, the one it takes down` : `${this.label()}, number of the ${this.step().target}`,
  );

  /** The step's line in the field for the kind. */
  protected readonly kind = computed(() => {
    const { target, number } = this.step();
    return target === 'channel' ? `channel:${universeOf(number)}` : target;
  });

  /** The list for the kind of step this is (a channel: those of its universe), with the step's own number in it for sure. */
  protected readonly list = computed(() => {
    const { target, number } = this.step();
    if (target === 'release') {
      const offered = this.releasable() ?? [];
      // a release of none: nothing is chosen yet, or its step was taken out
      return number === 0 ? [{ number: 0, text: 'None', short: 'None' }, ...offered] : withCurrent(offered, number, true);
    }
    const offered: readonly StepChoice[] =
      target === 'channel' ? channelsOf(this.choices().channel, universeOf(number)) : (this.choices()[target] ?? []);
    return withCurrent(offered, number, this.lights());
  });

  /** What the closed field shows: a channel's number in its universe; a fixture's address, a group's "G2"; a step's number. */
  protected readonly shown = computed(() => {
    const { target, number } = this.step();
    if (target === 'channel') return String(channelNumber(number));
    return this.list().find((choice) => choice.number === number)?.short ?? String(number);
  });

  /** Seconds are shown with one digit after the point. */
  protected seconds(value: number): string {
    return value.toFixed(1);
  }

  /**
   * The field for the number is about to be used: by the pointer, or by the keyboard. The
   * whole list goes into this row now, before the browser opens it, and the row that had it
   * lets it go.
   */
  protected fillList(): void {
    if (this.listed()) return;
    rowWithList.set(this);
    this.changes.detectChanges(); // at once: the browser opens the list right after this event
  }

  /** The field for the number was left: it goes back to holding its number alone. */
  protected dropList(): void {
    if (this.listed()) rowWithList.set(null);
  }

  protected onTarget(event: Event): void {
    const [kind, universe] = (event.target as HTMLSelectElement).value.split(':');
    const target = kind as StepKind;
    // only a step of a sound plays on or is muted: another kind leaves Continue and Mute behind
    const { color, continue: playsOn, muted, ...rest } = this.step();
    if (target === 'sound' && playsOn) Object.assign(rest, { continue: true });
    if (target === 'sound' && muted) Object.assign(rest, { muted: true });
    const number =
      target === 'channel' ? this.channelIn(Number(universe))
      : target === 'release' ? this.stepAbove()
      : this.numberFor(target);
    // a release adds nothing; a step that was one comes back at full if it had no value
    const step =
      target === 'release' ? { ...rest, target, number, add: false }
      : this.release() ? { ...rest, target, number, value: rest.value || MAX_LEVEL }
      : { ...rest, target, number };
    // a fixture or a group keeps the color it had; a channel or a memory has none
    this.stepChange.emit(isLights(target) ? { ...step, color: color ?? null } : step);
  }

  protected onNumber(event: Event): void {
    const field = event.target as HTMLSelectElement;
    let number = Number(field.value);
    if (number < 0) {
      // a track of the mixer's playlist that is no sound yet: it becomes one first
      const track = this.list().find((choice) => choice.number === number)?.track;
      const made = track ? this.store.addTrackSound(track) : null;
      if (made === null) {
        field.value = String(this.step().number);
        return;
      }
      number = made;
    }
    if (number !== this.step().number) this.stepChange.emit({ ...this.step(), number });
  }

  protected onValue(event: Event): void {
    const field = event.target as HTMLInputElement;
    const value = whole(field.value, 0, MAX_LEVEL);
    if (value === null) field.value = String(this.step().value);
    else if (value !== this.step().value) this.stepChange.emit({ ...this.step(), value });
  }

  protected onTime(which: TimeField, event: Event): void {
    const field = event.target as HTMLInputElement;
    const time = secondsFrom(field.value);
    if (time === null) {
      field.value = this.seconds(this.step()[which]);
      return;
    }
    field.value = this.seconds(time); // "2" is shown as "2.0"
    if (time !== this.step()[which]) this.stepChange.emit({ ...this.step(), [which]: time });
  }

  /** Enter finishes typing: leaving the field takes the text over. */
  protected finish(event: Event): void {
    (event.target as HTMLInputElement).blur();
  }

  /**
   * The number a step gets when it is switched to a memory, a fixture or a group: the number it
   * has if that kind offers it, else the first number on offer, else the nearest number there is
   * (for a fixture or a group, while the plan has none: 0, none).
   */
  private numberFor(target: Exclude<StepKind, 'release'>): number {
    const old = this.release() ? 0 : this.step().number; // the id of a step is no channel or memory
    const offered = this.choices()[target] ?? [];
    if (offered.some((choice) => choice.number === old)) return old;
    if (offered[0]) return offered[0].number;
    return isLights(target) ? 0 : Math.min(Math.max(1, old), MEMORY_COUNT);
  }

  /**
   * The step a step switched to a release takes down at first: the one right above it, or else
   * the one below it; none without another step.
   */
  private stepAbove(): number {
    const offered = this.choices().release ?? [];
    const at = offered.findIndex((choice) => choice.number === this.self());
    const above = at > 0 ? offered[at - 1] : offered.find((choice) => choice.number !== this.self());
    return above?.number ?? 0;
  }

  /**
   * The channel a step gets when it is switched to a universe: a channel keeps its number in
   * its universe (113 of U1 becomes 113 of U2), a memory its number (memory 5 becomes channel 5);
   * a fixture, a group or a release starts at channel 1.
   */
  private channelIn(universe: number): number {
    const { target, number } = this.step();
    const inUniverse =
      target === 'channel' ? channelNumber(number) : target === 'memory' && number <= UNIVERSE_SIZE ? number : 1;
    return universeStart(universe) + inUniverse - 1;
  }
}

/** True for a step of a fixture or a group of the stage plan. */
function isLights(target: StepKind): boolean {
  return target === 'fixture' || target === 'group';
}

/** A whole number from `min` to `max`, or null for any other text. */
function whole(text: string, min: number, max: number): number | null {
  const trimmed = text.trim();
  if (!/^\d+$/.test(trimmed)) return null;
  const number = Number(trimmed);
  return number >= min && number <= max ? number : null;
}

/** Seconds from a text like "2", "1.5" or "1,5", rounded to tenths; null for any other text. */
function secondsFrom(text: string): number | null {
  const trimmed = text.trim().replace(',', '.');
  if (!/^\d+(\.\d*)?$|^\.\d+$/.test(trimmed)) return null;
  const tenths = Math.round(Number(trimmed) * 10) / 10;
  return tenths <= MAX_STEP_SECONDS ? tenths : null;
}
