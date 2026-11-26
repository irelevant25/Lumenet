import { Component, ElementRef, Injector, afterNextRender, computed, inject } from '@angular/core';
import { DEFAULT_HIDDEN_VIEWS, DeskSettings } from '../../desk/desk-settings';
import { ALWAYS_SHOWN, DEFAULT_VIEW_ORDER, ViewName, viewNamed } from '../../desk/views';
import { Welcome } from '../../welcome/welcome';

/**
 * The Views part of the General tab of Settings: which views have a link in the top bar, and in
 * which order. Every one but Settings can be left out; ▲ and ▼ move one, ↺ brings back those of the
 * start (all but Notes) in the usual order, and Setup… offers the setups for a kind of show.
 */
@Component({
  selector: 'lm-views-panel',
  styleUrl: './views-panel.css',
  templateUrl: './views-panel.html',
})
export class ViewsPanel {
  protected readonly settings = inject(DeskSettings);
  /** Setup…: the window of the setups for a kind of show. */
  protected readonly welcome = inject(Welcome);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);

  protected readonly alwaysShown = ALWAYS_SHOWN;

  /** The views in the order of their links in the top bar. */
  protected readonly orderedViews = computed(() => this.settings.viewOrder().map(viewNamed));

  /** True while the top bar has the views of the start (every one but Notes), in the usual order: then ↺ has nothing to do. */
  protected readonly viewsUsual = computed(() => {
    const hidden = this.settings.hiddenViews();
    return (
      hidden.length === DEFAULT_HIDDEN_VIEWS.length &&
      DEFAULT_HIDDEN_VIEWS.every((name) => hidden.includes(name)) &&
      this.settings.viewOrder().every((name, at) => name === DEFAULT_VIEW_ORDER[at])
    );
  });

  protected viewShown(view: ViewName): boolean {
    return !this.settings.hiddenViews().includes(view);
  }

  protected onViewShown(view: ViewName, event: Event): void {
    this.settings.setViewShown(view, (event.target as HTMLInputElement).checked);
  }

  /**
   * ▲ or ▼: the view one place earlier or later. The keyboard focus stays with it, on the same
   * arrow, or on the other one where this one has gone dark (at the top or the bottom).
   */
  protected moveView(view: ViewName, by: -1 | 1): void {
    this.settings.moveView(view, by);
    afterNextRender(
      () => {
        const arrow = (way: number) =>
          this.host.nativeElement.querySelector<HTMLButtonElement>(`[data-move="${view}:${way}"]`);
        const same = arrow(by);
        (same && !same.disabled ? same : arrow(-by))?.focus();
      },
      { injector: this.injector },
    );
  }
}
