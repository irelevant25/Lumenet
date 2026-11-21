import { Routes } from '@angular/router';
import { MainView } from './main-view/main-view';

/**
 * The views of the desk, chosen with the links in the top bar. The main view is part of the
 * page from the start; the other one is fetched when it is first opened.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', component: MainView, title: 'Lumenet' },
  {
    path: 'memories',
    loadComponent: () => import('./memories-view/memories-view').then((file) => file.MemoriesView),
    title: 'Lumenet: memories',
  },
  {
    path: 'sequences',
    loadComponent: () =>
      import('./sequences-view/sequences-view').then((file) => file.SequencesView),
    title: 'Lumenet: sequences',
  },
  {
    path: 'stack',
    loadComponent: () => import('./stack-view/stack-view').then((file) => file.StackView),
    title: 'Lumenet: stack',
  },
  {
    path: 'stage',
    loadComponent: () => import('./stage-view/stage-view').then((file) => file.StageView),
    title: 'Lumenet: stage',
  },
  {
    path: 'fixtures',
    loadComponent: () => import('./fixtures-view/fixtures-view').then((file) => file.FixturesView),
    title: 'Lumenet: fixtures',
  },
  {
    path: 'settings',
    loadComponent: () => import('./settings-view/settings-view').then((file) => file.SettingsView),
    title: 'Lumenet: settings',
  },
  {
    path: 'keyboard',
    loadComponent: () => import('./keyboard-view/keyboard-view').then((file) => file.KeyboardView),
    title: 'Lumenet: keyboard',
  },
  { path: '**', redirectTo: '' },
];
