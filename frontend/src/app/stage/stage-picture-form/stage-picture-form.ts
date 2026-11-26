import { Component, computed, inject, signal } from '@angular/core';
import { DeskStore } from '../../desk/desk-store';
import { STAGE_BACK_ROOM, STAGE_FRONT_ROOM, STAGE_SIDE_ROOM, StagePlan } from '../../desk/desk-types';
import { readPicture, sizeOf } from '../stage-picture';
import { resized } from '../stage-lights';

/**
 * The picture under the stage plan, in the panel of the Stage view while Edit is lit and no
 * light is chosen: choose one (the stage then takes its proportions), say whether it lies under
 * the stage or under the whole plan, how brightly it shows through, or take it away.
 */
@Component({
  selector: 'lm-stage-picture-form',
  styleUrl: './stage-picture-form.css',
  templateUrl: './stage-picture-form.html',
})
export class StagePictureForm {
  protected readonly store = inject(DeskStore);

  protected readonly problem = signal('');
  protected readonly busy = signal(false);

  /** How brightly the picture shows through, in per cent. */
  protected readonly opacity = computed(() => Math.round(this.store.stage().pictureOpacity * 100));

  protected async onFile(event: Event): Promise<void> {
    const field = event.target as HTMLInputElement;
    const file = field.files?.[0];
    field.value = ''; // the same file can be chosen again
    if (!file) return;

    this.problem.set('');
    this.busy.set(true);
    try {
      const picture = await readPicture(file);
      this.store.setStagePicture(picture.data);
      this.store.setStage(fitted(this.store.stage(), picture));
    } catch (error) {
      this.problem.set(error instanceof Error ? error.message : String(error));
    } finally {
      this.busy.set(false);
    }
  }

  /** The stage takes the proportions of the picture: its depth changes, its width stays. */
  protected async fit(): Promise<void> {
    const picture = this.store.stagePicture();
    if (!picture) return;
    try {
      this.store.setStage(fitted(this.store.stage(), await sizeOf(picture)));
    } catch (error) {
      this.problem.set(error instanceof Error ? error.message : String(error));
    }
  }

  protected setOver(event: Event): void {
    const over = (event.target as HTMLSelectElement).value === 'plan' ? 'plan' : 'stage';
    this.store.setStage({ ...this.store.stage(), pictureOver: over });
  }

  /** While the slider moves the picture shows it at once; once let go, the plan keeps it. */
  protected previewOpacity(event: Event): void {
    const opacity = Number((event.target as HTMLInputElement).value) / 100;
    this.store.stage.update((plan) => ({ ...plan, pictureOpacity: opacity }));
  }

  protected setOpacity(event: Event): void {
    const opacity = Number((event.target as HTMLInputElement).value) / 100;
    this.store.setStage({ ...this.store.stage(), pictureOpacity: Math.min(1, Math.max(0.1, opacity)) });
  }

  protected remove(): void {
    this.problem.set('');
    this.store.setStagePicture(null);
  }
}

/** The plan with a stage as deep as the picture says for its width (or the whole plan, if it lies under that). */
function fitted(plan: StagePlan, picture: { width: number; height: number }): StagePlan {
  const ratio = picture.height / picture.width;
  const depth =
    plan.pictureOver === 'plan'
      ? (plan.width + 2 * STAGE_SIDE_ROOM) * ratio - STAGE_BACK_ROOM - STAGE_FRONT_ROOM
      : plan.width * ratio;
  return resized(plan, 'depth', depth);
}
