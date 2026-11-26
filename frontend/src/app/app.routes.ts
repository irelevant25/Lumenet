import { Routes } from '@angular/router';
import { MainView } from './main/main-view/main-view';

/**
 * The views of the desk, chosen with the links in the top bar. The main view is part of the
 * page from the start; the other one is fetched when it is first opened.
 */
export const routes: Routes = [
  { path: '', pathMatch: 'full', component: MainView, title: 'Lumenet' },
  {
    path: 'memories',
    loadComponent: () =>
      import('./memories/memories-view/memories-view').then((file) => file.MemoriesView),
    title: 'Lumenet: memories',
  },
  {
    path: 'sequences',
    loadComponent: () =>
      import('./sequences/sequences-view/sequences-view').then((file) => file.SequencesView),
    title: 'Lumenet: sequences',
  },
  {
    path: 'effects',
    loadComponent: () =>
      import('./effects/effects-view/effects-view').then((file) => file.EffectsView),
    title: 'Lumenet: effects',
  },
  {
    path: 'sounds',
    loadComponent: () => import('./sounds/sounds-view/sounds-view').then((file) => file.SoundsView),
    title: 'Lumenet: sounds',
  },
  {
    path: 'stack',
    loadComponent: () => import('./stack/stack-view/stack-view').then((file) => file.StackView),
    title: 'Lumenet: stack',
  },
  {
    path: 'stage',
    loadComponent: () => import('./stage/stage-view/stage-view').then((file) => file.StageView),
    title: 'Lumenet: stage',
  },
  {
    path: 'fixtures',
    loadComponent: () =>
      import('./fixtures/fixtures-view/fixtures-view').then((file) => file.FixturesView),
    title: 'Lumenet: fixtures',
  },
  {
    path: 'notes',
    loadComponent: () => import('./notes/notes-view/notes-view').then((file) => file.NotesView),
    title: 'Lumenet: notes',
  },
  {
    path: 'midi',
    loadComponent: () => import('./midi/midi-view/midi-view').then((file) => file.MidiView),
    title: 'Lumenet: MIDI',
  },
  {
    path: 'settings',
    loadComponent: () =>
      import('./settings/settings-view/settings-view').then((file) => file.SettingsView),
    title: 'Lumenet: settings',
  },
  // The keyboard shortcuts had a view of their own until 2026-11-24; they are a panel of Settings now.
  { path: 'keyboard', redirectTo: 'settings' },
  { path: '**', redirectTo: '' },
];
