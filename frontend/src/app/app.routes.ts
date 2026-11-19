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
  { path: '**', redirectTo: '' },
];
