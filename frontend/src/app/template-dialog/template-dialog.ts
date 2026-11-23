import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { ColorPick, ColorPicker } from '../color-picker/color-picker';
import { DeskStore } from '../desk/desk-store';
import { MAX_LEVEL, MAX_SEQUENCE_STEPS, MEMORY_COUNT, UNIVERSE_SIZE } from '../desk/desk-types';
import { hexOf } from '../desk/fixtures';
import { TEMPLATES, TemplateInfo, TemplateName, makeSequence, readNumbers, stepsNeeded } from '../desk/sequence-templates';
import { universeStart } from '../desk/universes';
import { LampButton } from '../lamp-button/lamp-button';

/** What a template goes through: channels or memories by their numbers, or the fixtures of a group. */
type TemplateLights = 'channel' | 'memory' | 'group';

/**
 * The window that fills the sequence of a memory from a template: fire, chase, wave, strobe or
 * storm, for the lights the owner names: channels, memories, or the fixtures of a group (in the
 * order of their addresses, in a color). The steps it makes replace those of the sequence, and
 * can be changed in the table like any others. In a project of several universes numbered by
 * universe (Settings), the channels are those of a universe chosen beside them; numbered on
 * through the universes, they are numbers like 625.
 */
@Component({
  imports: [ColorPicker, LampButton],
  selector: 'lm-template-dialog',
  styleUrl: './template-dialog.css',
  templateUrl: './template-dialog.html',
})
export class TemplateDialog {
  private readonly store = inject(DeskStore);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  protected readonly templates = TEMPLATES;

  /** True when the channels are chosen in a universe: several universes, numbered 1 to 512 in each. */
  protected readonly universeChoice = computed(
    () => this.target() === 'channel' && this.store.universes() > 1,
  );
  protected readonly universeChoices = computed(() => Array.from({ length: this.store.universes() }, (_, index) => index + 1));
  private readonly chosenUniverse = signal(1);
  /** The universe of the channels: the one chosen, the last there is when the project has fewer. */
  protected readonly universe = computed(() => Math.min(this.chosenUniverse(), this.store.universes()));

  /** The memory whose sequence the template fills. */
  protected readonly memory = signal(1);
  protected readonly name = signal<TemplateName>('fire');
  protected readonly target = signal<TemplateLights>('channel');
  protected readonly numbers = signal('1, 2');
  /** For the fixtures of a group: which group, and their color (null for white). */
  protected readonly groupId = signal<number | null>(null);
  protected readonly color = signal<string | null>(null);
  protected readonly groups = computed(() => this.store.stage().groups);
  protected readonly group = computed(() => this.groups().find((group) => group.id === this.groupId()) ?? this.groups()[0] ?? null);
  protected readonly low = signal(TEMPLATES[0].low);
  protected readonly high = signal(TEMPLATES[0].high);
  protected readonly time = signal(TEMPLATES[0].time);
  protected readonly error = signal('');

  protected readonly info = computed<TemplateInfo>(
    () => TEMPLATES.find((template) => template.name === this.name()) ?? TEMPLATES[0],
  );

  /** The steps the memory holds now, which the template replaces. */
  protected readonly replacing = computed(() => this.store.sequences()[this.memory()]?.steps.length ?? 0);

  /** Opens the window for a memory. The template, the lights and the values stay from the last time. */
  open(memory: number): void {
    this.memory.set(memory);
    this.error.set('');
    this.dialog().nativeElement.showModal();
  }

  protected close(): void {
    this.dialog().nativeElement.close();
  }

  /** Another template: its own values come in, the lights stay. */
  protected choose(name: TemplateName): void {
    const template = TEMPLATES.find((item) => item.name === name);
    if (!template) return;
    this.name.set(name);
    this.low.set(template.low);
    this.high.set(template.high);
    this.time.set(template.time);
    this.error.set('');
  }

  protected onTarget(event: Event): void {
    this.target.set((event.target as HTMLSelectElement).value as TemplateLights);
  }

  protected onGroup(event: Event): void {
    this.groupId.set(Number((event.target as HTMLSelectElement).value));
  }

  protected onUniverse(event: Event): void {
    this.chosenUniverse.set(Number((event.target as HTMLSelectElement).value));
  }

  protected onColor(pick: ColorPick): void {
    if (pick.kind === 'mix') this.color.set(hexOf(pick.rgb));
  }

  protected onNumbers(event: Event): void {
    this.numbers.set((event.target as HTMLInputElement).value);
  }

  /** A level field: a whole number from 0 to 255; anything else leaves the old one. */
  protected onLevel(which: 'low' | 'high', event: Event): void {
    const field = event.target as HTMLInputElement;
    const level = Number(field.value.trim());
    const value = which === 'low' ? this.low : this.high;
    if (Number.isInteger(level) && level >= 0 && level <= MAX_LEVEL) value.set(level);
    field.value = String(value());
  }

  /** The time field: seconds from 0.1 to 60, a comma taken as a point. */
  protected onTime(event: Event): void {
    const field = event.target as HTMLInputElement;
    const seconds = Number(field.value.trim().replace(',', '.'));
    if (Number.isFinite(seconds) && seconds >= 0.1 && seconds <= 60) this.time.set(Math.round(seconds * 10) / 10);
    field.value = String(this.time());
  }

  protected make(event: Event): void {
    event.preventDefault(); // the form must not close the window on a mistake

    const fixtures = this.target() === 'group';
    let numbers: number[] | string;
    if (fixtures) {
      const group = this.group();
      const lights = group ? this.store.groupLights(group).sort((a, b) => a.address - b.address) : [];
      numbers = lights.length > 0 ? lights.map((light) => light.id) : 'The group has no lights: tick them on the Fixtures page.';
    } else if (this.universeChoice()) {
      // 1 to 512 in the universe chosen
      const read = readNumbers(this.numbers(), UNIVERSE_SIZE);
      const start = universeStart(this.universe());
      numbers = typeof read === 'string' ? read : read.map((number) => start + number - 1);
    } else {
      numbers = readNumbers(this.numbers(), this.target() === 'channel' ? this.store.channelCount() : MEMORY_COUNT);
    }
    if (typeof numbers === 'string') {
      this.error.set(numbers);
      return;
    }
    if (stepsNeeded(this.name(), numbers.length) > MAX_SEQUENCE_STEPS) {
      this.error.set(`That is too many lights for one sequence: it can have ${MAX_SEQUENCE_STEPS} steps.`);
      return;
    }
    if (this.info().usesLow && this.low() > this.high()) {
      this.error.set('Low must not be higher than High.');
      return;
    }

    const made = makeSequence({
      name: this.name(),
      target: fixtures ? 'fixture' : this.target() === 'channel' ? 'channel' : 'memory',
      numbers,
      color: fixtures ? this.color() : null,
      low: this.low(),
      high: this.high(),
      time: this.time(),
    });
    const old = this.store.sequences()[this.memory()];
    this.store.setSequence(this.memory(), { ...made, speed: old?.speed ?? 1 });
    this.close();
  }
}
