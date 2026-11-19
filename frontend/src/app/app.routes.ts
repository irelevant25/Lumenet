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
  { path: '**', redirectTo: '' },
];
