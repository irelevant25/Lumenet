import { EffectRowCount } from '../desk/desk-settings';
import { VIEWS, ViewName } from '../desk/views';

/** A setup of the page for a kind of show: the views in the top bar and the rows of Main. */
export interface Setup {
  readonly id: 'theatre' | 'theatre-lights' | 'concert' | 'everything';
  /** What the key of the setup says. */
  readonly name: string;
  /** What it is for, in one sentence. */
  readonly about: string;
  /** The views in the top bar, Settings always among them. */
  readonly views: readonly ViewName[];
  /** A row of effect faders on Main (1) or none (0). */
  readonly effectRows: EffectRowCount;
  /** A row of sound faders on Main (1) or none (0). */
  readonly soundRows: EffectRowCount;
}

/**
 * The setups the page offers on a device's first start, and Settings (Setup…) any time: by the kind
 * of show rather than its size. None has Notes in the top bar: Settings shows it with a tick, for a
 * show whose notes are written there. Settings changes each part on its own afterwards.
 */
export const SETUPS: readonly Setup[] = [
  {
    id: 'theatre',
    name: 'Theatre',
    about:
      'Light and sound in one stack of cues, played with GO: the stack on Main, a row of sounds under the faders, and the views for the stack, the stage and the sounds.',
    views: ['main', 'stack', 'stage', 'sounds', 'settings'],
    effectRows: 0,
    soundRows: 1,
  },
  {
    id: 'theatre-lights',
    name: 'Theatre with moving lights',
    about:
      'The same, and the lights of today: fixtures and moving heads, their effects, the memories with their looks, and sequences.',
    views: [
      'main',
      'memories',
      'sequences',
      'stack',
      'stage',
      'fixtures',
      'effects',
      'sounds',
      'settings',
    ],
    effectRows: 1,
    soundRows: 1,
  },
  {
    id: 'concert',
    name: 'Concert',
    about:
      "Lights played live to the music: the faders, effects with the tempo, fixtures, sequences (chases) and a MIDI controller. The band's sound is the mixer's, so no sounds.",
    views: ['main', 'sequences', 'stage', 'fixtures', 'effects', 'midi', 'settings'],
    effectRows: 1,
    soundRows: 0,
  },
  {
    id: 'everything',
    name: 'Everything',
    about:
      'Every view of the desk but Notes (Settings shows it with a tick), and the rows of effects and sounds on Main.',
    views: VIEWS.map((view) => view.name).filter((name) => name !== 'notes'),
    effectRows: 1,
    soundRows: 1,
  },
];

/** The setup whose views the top bar shows now, if it is one (the rows of Main may have changed since). */
export function setupOf(shown: readonly ViewName[]): Setup | undefined {
  return SETUPS.find(
    (setup) =>
      setup.views.length === shown.length && setup.views.every((view) => shown.includes(view)),
  );
}

/** The names of the views of a setup, as its links say them: "Main, Stack, Stage". */
export function viewNames(setup: Setup): string {
  return VIEWS.filter((view) => setup.views.includes(view.name))
    .map((view) => view.label)
    .join(', ');
}
