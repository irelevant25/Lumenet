import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DeskStore } from '../../desk/desk-store';
import { SoundTime, StackStep, TimedStep } from '../../desk/desk-types';
import { MAX_EFFECTS } from '../../effects/effects';
import { MAX_SOUNDS, tracksPlaylist } from '../../sounds/sounds';
import { StepChoices, channelChoices, effectChoices, lightChoices, soundChoices, stepChoices } from '../step-choices';
import { LampButton } from '../../shared/lamp-button/lamp-button';
import { MasterSection } from '../../main/master-section/master-section';
import { SoundMaster } from '../../sounds/sound-master/sound-master';
import { SoundNow } from '../../sounds/sound-now/sound-now';
import { CueMomentDialog } from '../cue-moment-dialog/cue-moment-dialog';
import { momentWords } from '../moments';
import { StackCards } from '../stack-cards/stack-cards';
import { StackKeys } from '../stack-keys/stack-keys';
import { StepColorDialog } from '../step-color-dialog/step-color-dialog';
import { StepRow } from '../step-row/step-row';

/** Where a dragged step would land: before or after the step with this id. */
interface DropMark {
  id: number;
  after: boolean;
}

/**
 * The Stack view: the one stack of the project. The Stack key takes it live: every fader goes
 * to zero and the first step starts. From then on it is gone through with Play, Pause, Next and
 * Previous, round and round: after the last step comes the first. Going to a step moves the
 * real fader of its channel or memory up to the step's value (for a fixture or a group, the
 * channel faders of its lights, in the step's color); leaving it moves that fader back to zero.
 *
 * While the stack is live, nothing that is stored can be changed, here or anywhere on the
 * desk. While it is not live it does not run, and that is the time to change its steps.
 *
 * The steps stand in the order of their numbers ("2.0", "2.1"). A step is moved by dragging
 * its grip, or with the arrow keys at the end of its row; the desk then gives it a number
 * that fits its new place.
 */
@Component({
  host: { '[class.sounding]': 'sounding()' },
  imports: [
    CueMomentDialog,
    LampButton,
    MasterSection,
    RouterLink,
    SoundMaster,
    SoundNow,
    StackCards,
    StackKeys,
    StepColorDialog,
    StepRow,
  ],
  selector: 'lm-stack-view',
  styleUrl: './stack-view.css',
  templateUrl: './stack-view.html',
})
export class StackView {
  protected readonly store = inject(DeskStore);
  private readonly colorDialog = viewChild.required(StepColorDialog);
  private readonly momentDialog = viewChild.required(CueMomentDialog);

  /** True while Learn is on: each GO stores the moment of the sound that plays into the cue it goes to. */
  protected readonly learning = this.store.stackLearning;

  protected readonly steps = computed(() => this.store.stack().steps);

  /** True once a sound played: its wave stands over the steps. */
  protected readonly sounding = computed(() => this.store.shownSound() !== undefined);

  /** True while the stack is live: it runs, and its steps are locked. */
  protected readonly live = this.store.live;
  protected readonly previous = computed(() => this.store.stackPosition().previous);
  protected readonly current = computed(() => this.store.stackPosition().current);
  protected readonly next = computed(() => this.store.stackPosition().next);

  /** The step that is being dragged, and where it would land. */
  private readonly dragged = signal<number | null>(null);
  protected readonly dropMark = signal<DropMark | null>(null);

  /** The steps that are on: the current one and those it came on top of (Add). */
  protected readonly on = this.store.stackOn;

  /**
   * What the number of a step is chosen from: every channel, the memories that hold something
   * (a look or a sequence), the fixtures and groups of the stage plan, and for a release the
   * steps of the stack that are no release, "2.0: Memory 2 (Wash)".
   */
  protected readonly choices = computed<StepChoices>(() => ({
    channel: channelChoices(this.store.channelNames()),
    memory: stepChoices(this.store.memoryNotes(), (memory) => this.store.memoryStored()[memory - 1]),
    ...lightChoices(this.store.stage()),
    effect: effectChoices(this.store.effects(), MAX_EFFECTS),
    sound: soundChoices(
      this.store.sounds(),
      MAX_SOUNDS,
      this.store.playlistTracks(),
      this.store.mixer().state === 'connected' ? tracksPlaylist(this.store.mixer()) : '',
    ),
    release: this.steps()
      .filter((step) => step.target !== 'release')
      .map((step) => ({ number: step.id, text: `${step.cue}: ${this.store.stepWithNote(step)}`, short: step.cue })),
  }));

  protected readonly summary = computed(() => {
    if (!this.store.playbackKnown()) {
      return 'The desk program is older than this page and knows no stack. Start run.cmd again.';
    }
    const count = this.steps().length;
    if (count === 0) return 'The stack is empty. Add cue puts in the first cue.';

    const size = `The stack has ${count} ${count === 1 ? 'cue' : 'cues'}.`;
    if (this.learning()) {
      return `${size} Learn is on: each GO stores where the sound that plays is into the cue it goes to.`;
    }
    return this.live()
      ? `${size} It is live: nothing that is stored can be changed until Stack is pressed again.`
      : `${size} Stack takes it live: every fader goes to zero and the first cue starts.`;
  });

  /** A moment of a sound in words: "0:31.25 of Thunder". */
  protected momentOf(at: SoundTime): string {
    return momentWords(at, this.store.soundsByNumber());
  }

  /** What a screen reader calls the key of a cue's moment. */
  protected momentLabel(step: StackStep): string {
    return step.at
      ? `Cue ${step.cue} comes at ${this.momentOf(step.at)}: change the moment`
      : `Cue ${step.cue} comes with GO: give it a moment of a sound`;
  }

  /** The key of a cue's moment was pressed: the window asks for the sound and the moment. */
  protected pickMoment(step: StackStep): void {
    this.momentDialog().open(`Moment of cue ${step.cue}`, step.at, (at) => this.store.setStackStepAt(step.id, at));
  }

  /** The note under the fader of the step's channel or memory, or the name of its fixture or group, in one line. */
  protected noteOf(step: TimedStep): string {
    return this.store.stepNote(step);
  }

  /** The color of a step of a fixture or a group was pressed: the window asks for another. */
  protected pickColor(step: StackStep): void {
    this.colorDialog().open(`Color of cue ${step.cue}`, step.color ?? null, (color) => this.changeStep(step.id, { ...step, color }));
  }

  /** Puts a step at the end: the first one, or one like the last on the next channel or memory. */
  protected addStep(): void {
    this.store.addStackStepAtEnd();
  }

  /**
   * A row hands back its step with one field changed. Only the step itself goes to the desk, not
   * its name and number; whether it adds only when it does (a release never does), whether it plays
   * on only for a sound that does; its moment of a sound when it has one (the desk takes the step as
   * it is sent: without it, the moment would be gone).
   */
  protected changeStep(id: number, step: TimedStep): void {
    const { target, number, value, up, wait, down, color, add, at } = step;
    const sent: TimedStep = color === undefined ? { target, number, value, up, wait, down } : { target, number, value, up, wait, down, color };
    const adds = add && target !== 'release' ? { ...sent, add: true } : sent;
    const plays = step.continue && target === 'sound' ? { ...adds, continue: true } : adds;
    const heard = step.muted && target === 'sound' ? { ...plays, muted: true } : plays;
    const timed = at ? { ...heard, at } : heard;
    this.store.updateStackStep(id, step.disabled ? { ...timed, disabled: true } : timed);
  }

  /** The Mute box of a cue of a sound: checked, its sound plays unheard. Also while the stack is live. */
  protected setMuted(step: StackStep, event: Event): void {
    this.store.setStackStepMuted(step.id, (event.target as HTMLInputElement).checked);
  }

  /** The Disabled box of a step: checked, the stack passes over it. Also while the stack is live. */
  protected setDisabled(step: StackStep, event: Event): void {
    const box = event.target as HTMLInputElement;
    this.store.setStackStepDisabled(step.id, box.checked);
  }

  /** The Add box of a step: checked, the step comes up on top of what is on and takes nothing down. */
  protected setAdd(step: StackStep, event: Event): void {
    this.changeStep(step.id, { ...step, add: (event.target as HTMLInputElement).checked });
  }

  /** The Continue box of a step of a sound: checked, its sound plays on through the steps after it. */
  protected setContinue(step: StackStep, event: Event): void {
    this.changeStep(step.id, { ...step, continue: (event.target as HTMLInputElement).checked });
  }

  /**
   * The number typed for a step. The field goes back to the number the step has; if the desk
   * takes the new one, it sends the stack again and the field shows it.
   */
  protected renumber(step: StackStep, event: Event): void {
    const field = event.target as HTMLInputElement;
    const typed = field.value.trim();
    field.value = step.cue;
    if (typed !== '' && typed !== step.cue) this.store.renumberStackStep(step.id, typed);
  }

  protected finish(event: Event): void {
    (event.target as HTMLInputElement).blur();
  }

  /** One place up: right after the step that stands two places above, or at the very top. */
  protected moveUp(index: number): void {
    const steps = this.steps();
    if (index === 0) return;
    this.store.moveStackStep(steps[index].id, index >= 2 ? steps[index - 2].id : null);
  }

  /** One place down: right after the step that follows. */
  protected moveDown(index: number): void {
    const steps = this.steps();
    if (index >= steps.length - 1) return;
    this.store.moveStackStep(steps[index].id, steps[index + 1].id);
  }

  // ---------------------------------------------------------------- dragging a step by its grip

  protected onDragStart(event: DragEvent, step: StackStep): void {
    if (this.live()) {
      event.preventDefault(); // a live stack keeps its order
      return;
    }

    const row = (event.target as HTMLElement).closest('tr');
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
      event.dataTransfer.setData('text/plain', step.cue);
      if (row) event.dataTransfer.setDragImage(row, 16, 16);
    }
    this.dragged.set(step.id);
  }

  /** While a step is dragged over a row: the upper half means "before this step", the lower "after". */
  protected onDragOver(event: DragEvent, step: StackStep): void {
    if (this.dragged() === null) return;
    event.preventDefault(); // without this the browser allows no drop here

    const row = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const after = event.clientY > row.top + row.height / 2;
    const mark = this.dropMark();
    if (mark?.id !== step.id || mark.after !== after) this.dropMark.set({ id: step.id, after });
  }

  protected onDrop(event: DragEvent, index: number): void {
    event.preventDefault();
    const id = this.dragged();
    const mark = this.dropMark();
    this.onDragEnd();
    if (id === null || !mark) return;

    const steps = this.steps();
    const before = index > 0 ? steps[index - 1].id : null;
    const afterId = mark.after ? steps[index].id : before;
    if (afterId !== id) this.store.moveStackStep(id, afterId);
  }

  protected onDragEnd(): void {
    this.dragged.set(null);
    this.dropMark.set(null);
  }
}
