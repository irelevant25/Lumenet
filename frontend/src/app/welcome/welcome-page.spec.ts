import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { App } from '../app';
import { DeskSettings, SETTINGS_STORAGE_KEY } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import { VIEWS } from '../desk/views';
import { deskPageHelpers, FakeConnection, startDeskPage } from '../testing/desk-page.testing';

describe('the desk page: the setups of a first start', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;
  const { show, button, press } = deskPageHelpers(() => ({ connection, fixture, page, store }));

  const window = () => page.querySelector<HTMLDialogElement>('lm-welcome-dialog dialog')!;
  const title = () => page.querySelector('#welcome-title')?.textContent?.trim();
  const links = () =>
    [...page.querySelectorAll('nav[aria-label="Views"] a')].map((link) => link.textContent?.trim());

  it('a device that keeps no settings gets the setups; Theatre keeps the views of a theatre', async () => {
    ({ connection, fixture, page, store } = await startDeskPage(true));
    await fixture.whenStable();
    expect(window().open).toBe(true);
    expect(title()).toBe('Welcome to Lumenet');
    expect(button('Theatre').getAttribute('aria-pressed')).toBe('true'); // chosen at first

    await press(button('Use this setup'));
    expect(links()).toEqual(['Main', 'Stack', 'Stage', 'Sounds', 'Settings']); // Notes only with a tick in Settings
    const settings = TestBed.inject(DeskSettings);
    expect(settings.soundRows()).toBe(1);
    expect(settings.effectRows()).toBe(0);
    expect(localStorage.getItem(SETTINGS_STORAGE_KEY)).not.toBeNull(); // not asked again

    // then the first steps: Outputs opens their tab of Settings
    expect(title()).toBe('Ready: Theatre');
    await press(button('Outputs'));
    expect(window().open).toBe(false);
    expect(TestBed.inject(Router).url).toBe('/settings#outputs');
  });

  it('Later changes nothing and asks no more; Setup… of Settings offers the setups again', async () => {
    ({ connection, fixture, page, store } = await startDeskPage(true));
    await fixture.whenStable();
    await press(button('Later'));
    expect(window().open).toBe(false);
    expect(links()).toEqual(VIEWS.filter((view) => view.name !== 'notes').map((view) => view.label));
    expect(localStorage.getItem(SETTINGS_STORAGE_KEY)).not.toBeNull();

    await show('/settings');
    await press(button('Setup…'));
    expect(window().open).toBe(true);
    expect(title()).toBe('Setup of this device');
    expect(button('Everything').getAttribute('aria-pressed')).toBe('true'); // every view but Notes is shown now

    await press(button('Concert'));
    expect(page.querySelector('lm-welcome-dialog .about')?.textContent).toContain('played live');
    await press(button('Use this setup'));
    expect(links()).toEqual([
      'Main',
      'Sequences',
      'Stage',
      'Fixtures',
      'Effects',
      'MIDI',
      'Settings',
    ]);

    // Examples: the Load window, which asks the desk program for the projects and the examples
    await press(button('Examples'));
    expect(window().open).toBe(false);
    expect(connection.last('GetProjects')).toEqual([]);
    expect(page.querySelector('lm-project-dialog dialog')?.hasAttribute('open')).toBe(true);
  });

  it('a device that keeps its settings starts without the window', async () => {
    ({ connection, fixture, page, store } = await startDeskPage());
    expect(window().open).toBe(false);
  });
});
