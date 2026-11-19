import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { DeskStore } from '../desk/desk-store';
import { CHANNEL_COUNT, MAX_LEVEL, MAX_SEQUENCE_STEPS, MEMORY_COUNT, StepTarget } from '../desk/desk-types';
import { TEMPLATES, TemplateInfo, TemplateName, makeSequence, readNumbers, stepsNeeded } from '../desk/sequence-templates';
import { LampButton } from '../lamp-button/lamp-button';

/**
 * The window that fills the sequence of a memory from a template: fire, chase, wave, strobe or
 * storm, for the lights the owner names. The steps it makes replace those of the sequence, and
 * can be changed in the table like any others.
 */
@Component({
  imports: [LampButton],
  selector: 'lm-template-dialog',
  styleUrl: './template-dialog.css',
  templateUrl: './template-dialog.html',
})
export class TemplateDialog {
  private readonly store = inject(DeskStore);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  protected readonly templates = TEMPLATES;

  /** The memory whose sequence the template fills. */
  protected readonly memory = signal(1);
  protected readonly name = signal<TemplateName>('fire');
  protected readonly target = signal<StepTarget>('channel');
  protected readonly numbers = signal('1, 2');
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
    this.target.set((event.target as HTMLSelectElement).value as StepTarget);
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

    const highest = this.target() === 'channel' ? CHANNEL_COUNT : MEMORY_COUNT;
    const numbers = readNumbers(this.numbers(), highest);
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
      target: this.target(),
      numbers,
      low: this.low(),
      high: this.high(),
      time: this.time(),
    });
    const old = this.store.sequences()[this.memory()];
    this.store.setSequence(this.memory(), { ...made, speed: old?.speed ?? 1 });
    this.close();
  }
}
