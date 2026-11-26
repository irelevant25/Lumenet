import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { App } from '../app';
import { DeskSettings, SETTINGS_STORAGE_KEY } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import { LanAccess } from '../desk/desk-types';
import { deskPageHelpers, FakeConnection, LIVE, startDeskPage } from '../testing/desk-page.testing';

describe('the desk page: Settings', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;
  const { show, button, buttonIn, press, stackStands, until, textOf } = deskPageHelpers(() => ({
    connection,
    fixture,
    page,
    store,
  }));

  beforeEach(async () => {
    ({ connection, fixture, page, store } = await startDeskPage());
  });

  it('Settings leaves views out of the top bar, but never itself; without Main the desk opens on the next view', async () => {
    const links = () =>
      [...page.querySelectorAll('nav[aria-label="Views"] a')].map(
        (link) => link.textContent?.trim().split(/\s/)[0],
      );
    await show('/settings');
    const box = (label: string) =>
      [...page.querySelectorAll<HTMLLabelElement>('ul.views label')]
        .find((item) => item.textContent?.trim().startsWith(label))!
        .querySelector('input')!;
    expect(
      [...page.querySelectorAll('ul.views label')].map(
        (label) => label.textContent?.trim().split(/\s/)[0],
      ),
    ).toEqual([
      'Main',
      'Memories',
      'Sequences',
      'Stack',
      'Stage',
      'Fixtures',
      'Effects',
      'Sounds',
      'Notes',
      'MIDI',
      'Settings',
    ]); // Keyboard is a panel of Settings; MIDI a view of its own
    expect(box('Settings').checked).toBe(true);
    expect(box('Settings').disabled).toBe(true);

    box('Stage').click();
    box('Main').click();
    await fixture.whenStable();
    expect(links()).toEqual([
      'Memories',
      'Sequences',
      'Stack',
      'Fixtures',
      'Effects',
      'Sounds',
      'MIDI',
      'Settings',
    ]); // Notes is left out at first
    expect(box('Notes').checked).toBe(false);
    expect(JSON.parse(localStorage.getItem(SETTINGS_STORAGE_KEY)!)).toMatchObject({
      hiddenViews: ['main', 'stage', 'notes'],
    }); // kept on this device

    box('Stage').click();
    await fixture.whenStable();
    expect(links()).toContain('Stage');

    // Stage to the front
    for (let place = 0; place < 4; place++) await press(button('Move Stage up'));
    expect(links()).toEqual([
      'Stage',
      'Memories',
      'Sequences',
      'Stack',
      'Fixtures',
      'Effects',
      'Sounds',
      'MIDI',
      'Settings',
    ]);

    // a desk that starts at Main, while Main is left out, opens on the first view in the top bar
    const router = TestBed.inject(Router);
    TestBed.createComponent(App);
    await router.navigateByUrl('/');
    await fixture.whenStable();
    await until(() => router.url === '/stage');
  });

  it('Settings puts the views in another order with ▲ and ▼, and ↺ brings back the usual ones', async () => {
    // Settings lists every view; the top bar has those that are ticked, at first all but Notes
    const usual = [
      'Main',
      'Memories',
      'Sequences',
      'Stack',
      'Stage',
      'Fixtures',
      'Effects',
      'Sounds',
      'Notes',
      'MIDI',
      'Settings',
    ];
    const inTheTopBar = (views: string[]) => views.filter((view) => view !== 'Notes');
    const links = () =>
      [...page.querySelectorAll('nav[aria-label="Views"] a')].map(
        (link) => link.textContent?.trim().split(/\s/)[0],
      );
    const listed = () =>
      [...page.querySelectorAll('ul.views label')].map(
        (label) => label.textContent?.trim().split(/\s/)[0],
      );
    await show('/settings');
    expect(button('The usual views').disabled).toBe(true);
    expect(button('Move Main up').disabled).toBe(true); // the first
    expect(button('Move Settings down').disabled).toBe(true); // the last

    // One place earlier, in Settings and in the top bar; the keyboard focus goes with it.
    await press(button('Move Settings up'));
    const moved = [
      'Main',
      'Memories',
      'Sequences',
      'Stack',
      'Stage',
      'Fixtures',
      'Effects',
      'Sounds',
      'Notes',
      'Settings',
      'MIDI',
    ];
    expect(listed()).toEqual(moved);
    expect(links()).toEqual(inTheTopBar(moved));
    expect(document.activeElement).toBe(button('Move Settings up'));
    expect(JSON.parse(localStorage.getItem(SETTINGS_STORAGE_KEY)!)).toMatchObject({
      viewOrder: moved.map((name) => name.toLowerCase()),
    });
    expect(button('The usual views').disabled).toBe(false);

    // Back at the top, ▲ goes dark and the focus moves to ▼.
    await press(button('Move Main down'));
    expect(links().slice(0, 2)).toEqual(['Memories', 'Main']);
    await press(button('Move Main up'));
    expect(button('Move Main up').disabled).toBe(true);
    expect(document.activeElement).toBe(button('Move Main down'));

    // ↺: the views of the start again (every one but Notes), in the usual order.
    [...page.querySelectorAll<HTMLLabelElement>('ul.views label')]
      .find((label) => label.textContent?.includes('Stage'))!
      .querySelector('input')!
      .click();
    await fixture.whenStable();
    expect(links()).not.toContain('Stage');
    await press(button('The usual views'));
    expect(links()).toEqual(inTheTopBar(usual));
    expect(listed()).toEqual(usual);
    expect(button('The usual views').disabled).toBe(true);
  });

  it('the settings and the shortcuts belong to this device, on the PC as on a phone', async () => {
    await show('/settings');
    expect(textOf('lm-settings-view .summary')).toContain(
      'The settings and the keyboard shortcuts belong to this device',
    );
    const rows = page.querySelector<HTMLSelectElement>('#channel-rows')!;
    rows.value = '1';
    rows.dispatchEvent(new Event('change'));
    await press(button('Zoom in'));
    expect(JSON.parse(localStorage.getItem(SETTINGS_STORAGE_KEY)!)).toMatchObject({
      channelRows: 1,
      zoom: 1.1,
    });

    // Nothing of them goes to the desk program, and a project that is loaded leaves them as they are.
    expect(connection.methods().filter((method) => method.includes('Settings'))).toEqual([]);
    connection.emit('DeskChanged', connection.welcome.desk);
    await fixture.whenStable();
    expect(TestBed.inject(DeskSettings).channelRows()).toBe(1);
  });

  it('the autosave is switched and timed in Settings, on the PC only', async () => {
    await show('/settings');
    const on = page.querySelector<HTMLSelectElement>('#autosave-on')!;
    const every = page.querySelector<HTMLSelectElement>('#autosave-every')!;
    expect(on.value).toBe('on');
    expect(every.value).toBe('5');
    expect([...every.options].map((option) => option.textContent?.trim())).toEqual([
      'Every 5 seconds',
      'Every 30 seconds',
      'Every 1 minute',
      'Every 5 minutes',
    ]);

    every.value = '60';
    every.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetAutosave')).toEqual([true, 60]);

    on.value = 'off';
    on.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetAutosave')).toEqual([false, 60]);
    expect(every.disabled).toBe(true);

    // Another page on the PC switched it on again.
    connection.emit('AutosaveChanged', { on: true, seconds: 300 });
    await fixture.whenStable();
    expect(on.value).toBe('on');
    expect(every.value).toBe('300');

    // A phone cannot set it.
    connection.emit('LanAccessChanged', { ...connection.lan, local: false });
    await fixture.whenStable();
    expect(page.querySelector('#autosave-on')).toBeNull();
    expect(page.querySelector('lm-settings-view')?.textContent).toContain(
      'Only the PC the desk program runs on can set the autosave.',
    );
  });

  it('the color of a kind of keys or of one key is set in Settings, and kept on this device', async () => {
    const shown = (variable: string) => document.documentElement.style.getPropertyValue(variable);
    const sentColors = () =>
      (JSON.parse(localStorage.getItem(SETTINGS_STORAGE_KEY) ?? '{}') as { colors?: object })
        .colors;
    await show('/settings#colors');

    // A press on a color offers the colors of the desk; one of them is taken, and they close.
    const pause = button('Color of Pause');
    expect(pause.getAttribute('aria-expanded')).toBe('false');
    await press(pause);
    expect(pause.getAttribute('aria-expanded')).toBe('true');
    const offered = [...page.querySelectorAll('.palette button')].map((swatch) =>
      swatch.getAttribute('aria-label'),
    );
    expect(offered).toEqual(['Green', 'Red', 'Yellow', 'Amber', 'White', 'Blue', 'Violet', 'Cyan']);
    expect(button('White').getAttribute('aria-current')).toBe('true'); // the one it has
    await press(button('Red'));
    expect(shown('--key-pause')).toBe('#ff5247');
    expect(sentColors()).toEqual({ pause: '#ff5247' });
    expect(page.querySelector('.palette')).toBeNull();
    expect(button('The usual color for Pause').disabled).toBe(false);

    // Any other color: while it is picked, the desk shows it at once; when the picker closes, it is kept.
    await press(button('Color of Pause'));
    await press(page.querySelector<HTMLElement>('.palette .other')); // a press in there leaves it open
    const other = page.querySelector<HTMLInputElement>('.palette input[type="color"]')!;
    expect(other.value).toBe('#ff5247');
    other.value = '#ff00ff';
    other.dispatchEvent(new Event('input'));
    expect(shown('--key-pause')).toBe('#ff00ff');
    other.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(sentColors()).toEqual({ pause: '#ff00ff' });

    // A kind: all channel keys, pages and faders. One palette is open at a time.
    await press(button('Color of Channels: their keys, pages, independent keys and faders'));
    expect(page.querySelectorAll('.palette').length).toBe(1);
    await press(button('Blue'));
    expect(shown('--channel')).toBe('#4aa3ff');
    expect(sentColors()).toEqual({ pause: '#ff00ff', channel: '#4aa3ff' });

    // A press anywhere else closes the palette, and so does Esc, with the keyboard back on the color.
    const channels = button('Color of Channels: their keys, pages, independent keys and faders');
    await press(channels);
    await press(document.body);
    expect(page.querySelector('.palette')).toBeNull();
    expect(channels.getAttribute('aria-expanded')).toBe('false');
    await press(channels);
    button('Green').focus();
    button('Green').dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    await fixture.whenStable();
    expect(page.querySelector('.palette')).toBeNull();
    expect(document.activeElement).toBe(channels);
    expect(sentColors()).toEqual({ pause: '#ff00ff', channel: '#4aa3ff' }); // nothing taken

    // Back to the usual colors: one, then all with ↺ beside the title.
    await press(button('The usual color for Pause'));
    expect(shown('--key-pause')).toBe('');
    expect(sentColors()).toEqual({ channel: '#4aa3ff' });
    await press(button('All the usual colors'));
    expect(shown('--channel')).toBe('');
    expect(sentColors()).toEqual({});
    expect(button('All the usual colors').disabled).toBe(true);

    // Settings from a file (Import) bring theirs; what makes no sense is left out.
    TestBed.inject(DeskSettings).takeSettings({
      colors: { play: '#123456', nonsense: '#000000', next: 'red' },
    });
    await fixture.whenStable();
    expect(shown('--key-play')).toBe('#123456');
    expect(TestBed.inject(DeskSettings).colors()).toEqual({ play: '#123456' });
  });

  // ---------------------------------------------------------------- the stage plan

  it('Settings says how many steps the stack wheel shows', async () => {
    const steps = Array.from({ length: 9 }, (_, index) => ({
      id: 21 + index,
      cue: String(index + 1),
      target: 'memory' as const,
      number: index + 1,
      value: 255,
      up: 1,
      wait: 0,
      down: 1,
    }));
    connection.emit('StackChanged', { steps });
    await stackStands({ ...LIVE, currentId: 25, nextId: 26 }); // step 5 is on
    const seen = () =>
      [...page.querySelectorAll('lm-stack-section .roller li')]
        .filter((place) => place.getAttribute('aria-hidden') !== 'true')
        .map((place) => place.querySelector('.cue')?.textContent?.trim());
    expect(seen()).toEqual(['2', '3', '4', '5', '6', '7', '8']); // 7 at first

    await show('/settings');
    const size = page.querySelector<HTMLSelectElement>('#wheel-size')!;
    expect(size.value).toBe('7');
    size.value = '3';
    size.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    await show('/');
    expect(seen()).toEqual(['4', '5', '6']);
    // the main view sizes its side column with it, the section its window
    expect(page.querySelector('lm-main-view')?.getAttribute('style')).toContain('--wheel-size: 3');
  });

  // ---------------------------------------------------------------- settings

  it('the channel size of Settings sets how many faders a page holds, and the page keys follow', async () => {
    await show('/settings');
    const size = page.querySelector<HTMLSelectElement>('#channel-size');
    if (!size) throw new Error('no channel size');
    expect(size.value).toBe('auto');

    size.value = '8';
    size.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.stripsPerRow()).toBe(4);
    expect(JSON.parse(localStorage.getItem('lumenet-settings') ?? '{}').channelSize).toBe(8);

    await show('/');
    const line = () =>
      page.querySelector<HTMLElement>('lm-main-view')?.style.getPropertyValue('--line');
    expect(line()).toBe('4'); // on the main view only
    expect(document.documentElement.style.getPropertyValue('--line')).toBe('');
    expect(page.querySelectorAll('lm-channel-section lm-fader-strip').length).toBe(8); // two rows of 4
    expect(page.querySelectorAll('lm-memory-section lm-fader-strip').length).toBe(4); // one row
    expect(page.querySelectorAll('nav[aria-label="Channel pages"] button').length).toBe(64);
    expect(page.querySelectorAll('nav[aria-label="Memory pages"] button').length).toBe(32);
    await press(button('Channels 9 to 16 (U1)'));
    expect(page.querySelector('lm-fader[aria-label="Channel 9 (U1)"]')).toBeTruthy();

    // The Memories and Sequences views keep the row the window gives them.
    await show('/memories');
    expect(page.querySelectorAll('lm-memories-view .content .keys button').length).toBe(32);
    expect(page.querySelectorAll('lm-memories-view table.matrix tr')[0].children.length).toBe(32);
    await show('/sequences');
    expect(
      page.querySelectorAll('lm-sequences-view nav[aria-label="Memory pages"] button').length,
    ).toBe(4);

    await show('/settings');
    const again = page.querySelector<HTMLSelectElement>('#channel-size')!;
    again.value = 'auto';
    again.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.stripsPerRow()).toBe(32);
    await show('/');
    expect(line()).toBe('');
  });

  it('Settings says how many rows of channel faders and of memory faders the main view has', async () => {
    await show('/settings');
    const channelRows = page.querySelector<HTMLSelectElement>('#channel-rows')!;
    const memoryRows = page.querySelector<HTMLSelectElement>('#memory-rows')!;
    expect([channelRows.value, memoryRows.value]).toEqual(['2', '1']);

    channelRows.value = '1';
    channelRows.dispatchEvent(new Event('change'));
    memoryRows.value = '2';
    memoryRows.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect([channelRows.value, memoryRows.value]).toEqual(['1', '2']);

    await show('/');
    expect(page.querySelectorAll('lm-channel-section .row').length).toBe(1);
    expect(page.querySelectorAll('lm-channel-section lm-fader-strip').length).toBe(32);
    expect(page.querySelectorAll('lm-memory-section .row').length).toBe(2);
    expect(page.querySelectorAll('lm-memory-section lm-fader-strip').length).toBe(64);
    expect(button('Channels 33 to 64 (U1)')).toBeTruthy();
    expect(button('Memories 65 to 128')).toBeTruthy();
    const main = page.querySelector<HTMLElement>('lm-main-view')!;
    expect(main.style.getPropertyValue('--channel-share')).toBe('1fr');
    expect(main.style.getPropertyValue('--memory-share')).toBe('2fr');

    // On a narrower window a row is 16 faders.
    store.windowStripsPerRow.set(16);
    await fixture.whenStable();
    expect(page.querySelectorAll('lm-channel-section lm-fader-strip').length).toBe(16);
  });

  it('Settings has tabs, each a part with the whole width; the address names the tab shown', async () => {
    const router = TestBed.inject(Router);
    await show('/settings');
    const tabs = [...page.querySelectorAll<HTMLButtonElement>('lm-settings-view [role="tab"]')];
    expect(tabs.map((tab) => tab.textContent?.trim())).toEqual([
      'General',
      'LAN',
      'Sound',
      'Colors',
      'Keyboard',
      'Outputs',
      'Backup',
      'About',
    ]);
    const selected = () =>
      tabs
        .filter((tab) => tab.getAttribute('aria-selected') === 'true')
        .map((tab) => tab.textContent?.trim());
    const titles = (level: string) =>
      [...page.querySelectorAll(`lm-settings-view .panels ${level}`)].map((title) =>
        title.textContent?.trim(),
      );

    // General at first: the main view with the universes, the zoom and the autosave, and the views
    expect(selected()).toEqual(['General']);
    expect(titles('h3')).toEqual(['Main view', 'Views in the top bar']);
    expect(titles('h4')).toEqual(['Universes', 'Zoom', 'Autosave']);
    // fields only, no sentences about them, until the autosave, which keeps its own
    const general = page.querySelector('lm-main-view-panel')!;
    const autosave = general.querySelector('#autosave-title')!;
    expect(
      [...general.querySelectorAll('.hint')].every(
        (hint) => autosave.compareDocumentPosition(hint) & Node.DOCUMENT_POSITION_FOLLOWING,
      ),
    ).toBe(true);

    // a press on a tab shows its part alone, and the address names it
    await press(tabs[2]);
    await until(() => router.url === '/settings#sound');
    expect(selected()).toEqual(['Sound']);
    expect(page.querySelector('lm-settings-view .panels lm-sound-output')).toBeTruthy();
    expect(page.querySelector('#channel-size')).toBeNull();

    // the arrow keys, Home and End go from tab to tab, the focus with them
    const key = async (name: string) => {
      (document.activeElement ?? tabs[2]).dispatchEvent(
        new KeyboardEvent('keydown', { key: name, bubbles: true }),
      );
      await fixture.whenStable();
    };
    tabs[2].focus();
    await key('ArrowRight');
    await until(() => router.url === '/settings#colors');
    expect(document.activeElement).toBe(tabs[3]);
    await key('End');
    await until(() => router.url === '/settings#about');
    await key('ArrowRight');
    await until(() => router.url === '/settings#general');
    expect(document.activeElement).toBe(tabs[0]);

    // an address with a tab opens on it
    await show('/settings#keyboard');
    expect(selected()).toEqual(['Keyboard']);
    expect(page.querySelector('lm-settings-view .panels lm-keyboard-panel')).toBeTruthy();
  });

  it("Backup: a folder, Google Drive's at a press, how many to keep, and a backup now or with every save", async () => {
    await show('/settings#backup');
    await until(() => page.querySelector('#backup-folder') !== null);
    const folder = page.querySelector<HTMLInputElement>('#backup-folder')!;
    expect(folder.value).toBe('');
    expect(button('Back up now').disabled).toBe(true); // no folder yet

    // Google Drive for desktop is on the PC: its folder at a press
    await press(button('Use its folder'));
    await until(() => connection.last('SetBackup') !== undefined);
    expect(connection.last('SetBackup')).toEqual([
      { folder: 'G:\\My Drive\\Lumenet backups', onSave: true, keep: 30 },
    ]);
    await fixture.whenStable();
    expect(folder.value).toBe('G:\\My Drive\\Lumenet backups');
    expect(page.querySelector('lm-backup-panel')?.textContent).not.toContain('Use its folder');

    const keep = page.querySelector<HTMLInputElement>('#backup-keep')!;
    keep.value = '12';
    keep.dispatchEvent(new Event('change'));
    await until(() => (connection.last('SetBackup')?.[0] as { keep: number }).keep === 12);

    // Back up now: every part goes to the desk program, which writes the ZIP into the folder
    const asked: { url: string; body: string }[] = [];
    const made = 'Lumenet backup - Untitled - 2026-11-26 14.05.33.zip';
    const realFetch = globalThis.fetch;
    globalThis.fetch = (async (url: string, init?: RequestInit) => {
      asked.push({ url, body: init?.body as string });
      return new Response(
        JSON.stringify({
          file: made,
          missing: ['gone.mp3'],
          backups: {
            ...connection.backup,
            count: 1,
            last: made,
            lastAt: Date.UTC(2026, 10, 26, 13, 5, 33),
          },
        }),
        { status: 200 },
      );
    }) as typeof fetch;
    try {
      await press(button('Back up now'));
      await until(() => page.querySelector('lm-backup-panel .done') !== null);
      expect(asked[0].url).toBe('/api/backup?name=Untitled');
      expect(Object.keys(JSON.parse(asked[0].body))).toEqual([
        'format',
        'version',
        'name',
        'project',
        'stage',
        'settings',
        'keyboard',
      ]); // and the MIDI, with a desk program that knows MIDI
      expect(textOf('lm-backup-panel .done')).toBe(
        `Backed up as "${made}", without these sound files, which the PC does not have: gone.mp3.`,
      );
      expect(textOf('lm-backup-panel .status')).toMatch(
        /^1 in the folder, the newest from 26 Nov 2026/,
      );

      // a save on the PC backs up as well, and says so
      await press(button('Save project'));
      const name = page.querySelector<HTMLInputElement>('#project-name')!;
      name.value = 'Evening';
      name.dispatchEvent(new Event('input'));
      await fixture.whenStable();
      await press(page.querySelector<HTMLButtonElement>('lm-project-dialog button[type="submit"]'));
      await until(() => asked.length === 2);
      expect(asked[1].url).toBe('/api/backup?name=Evening');
      await until(() => store.notice().includes('backed up'));
      expect(store.notice()).toBe('Project "Evening" saved and backed up (without gone.mp3)');
    } finally {
      globalThis.fetch = realFetch;
    }
  });

  it('Backup and About belong to the PC: a phone sets and updates nothing', async () => {
    connection.emit('LanAccessChanged', { ...connection.lan, local: false });
    await show('/settings#backup');
    await until(() => textOf('lm-backup-panel .hint') !== undefined);
    expect(textOf('lm-backup-panel .hint')).toBe(
      'The backups belong to the PC the desk program runs on: set them there.',
    );
    expect(page.querySelector('#backup-folder')).toBeNull();

    await show('/settings#about');
    expect(textOf('lm-about-panel .hint')).toBe(
      'Lumenet is updated on the PC the desk program runs on.',
    );
    expect(connection.calls.map((call) => call.method)).not.toContain('CheckUpdate');
  });

  it('About: the version, the newest one with what it brings, and an update after a question', async () => {
    connection.update = {
      current: '0.1.0',
      latest: '0.1.45',
      newer: true,
      notes: '## Backups to Google Drive\n\nA folder that Drive syncs.\n\n## Settings in tabs',
      published: Date.UTC(2026, 10, 26, 14),
      size: 68_576_256,
      canUpdate: true,
      why: null,
      problem: null,
    };
    await show('/settings#about');
    await until(() => page.querySelector('lm-about-panel .news') !== null);
    expect(textOf('lm-about-panel .version')).toBe('Lumenet 0.1.0');
    expect(textOf('lm-about-panel .news')?.replace(/\s+/g, ' ')).toBe(
      'Lumenet 0.1.45 is out (from 26 Nov 2026)',
    );
    // the notes of the release, their titles as titles
    expect(
      [...page.querySelectorAll('lm-about-panel .notes .text p')].map((paragraph) =>
        paragraph.classList.contains('title')
          ? `# ${paragraph.textContent}`
          : paragraph.textContent,
      ),
    ).toEqual(['# Backups to Google Drive', 'A folder that Drive syncs.', '# Settings in tabs']);

    // a question first: what the update does, and that the lights go out
    connection.emit('ProjectChanged', { name: 'Untitled', path: null, unsaved: true });
    await press(button('Update to 0.1.45'));
    expect(textOf('lm-about-panel .ask')).toContain(
      'Lumenet downloads 0.1.45 (65.4 MB) from GitHub',
    );
    expect(textOf('lm-about-panel .ask')).toContain('the lights go out');
    expect(textOf('lm-about-panel .ask .problem')).toBe(
      'The project has changes that are not saved. Save it first.',
    );
    await press(buttonIn('lm-about-panel', 'Cancel'));
    expect(page.querySelector('lm-about-panel .ask')).toBeNull();

    await press(button('Update to 0.1.45'));
    await press(button('Update now'));
    await until(() => textOf('lm-about-panel .done') !== undefined);
    expect(connection.calls.map((call) => call.method)).toContain('InstallUpdate');
    expect(textOf('lm-about-panel .done')).toBe(
      'Lumenet starts again as 0.1.45. This page loads again by itself when it is back.',
    );
    expect(
      [...page.querySelectorAll('lm-about-panel .parts a')].map((link) => link.textContent?.trim()),
    ).toContain('NAudio');
  });

  it('About: GitHub that does not answer is said; the desk of run.cmd says that git brings the new version', async () => {
    connection.update = {
      current: '0.1.0',
      latest: null,
      newer: false,
      notes: null,
      published: null,
      size: null,
      canUpdate: false,
      why: null,
      problem: 'GitHub could not be asked: it did not answer in time.',
    };
    await show('/settings#about');
    await until(() => textOf('lm-about-panel .problem') !== undefined);
    expect(textOf('lm-about-panel .problem')).toBe(
      'GitHub could not be asked: it did not answer in time.',
    );

    connection.update = {
      ...connection.update,
      latest: '0.1.45',
      newer: true,
      why: 'This desk runs from the source code: git pull and run.cmd bring the new version.',
      problem: null,
    };
    await press(button('Check again'));
    await until(() => page.querySelector('lm-about-panel .news') !== null);
    expect(page.querySelector('lm-about-panel button.go')).toBeNull();
    expect(textOf('lm-about-panel .hint')).toBe(connection.update.why!);
  });

  it('the rows of the channels and of the memories scroll sideways each on their own, with a big scrollbar', () => {
    const channelRows = page.querySelector('lm-channel-section .rows');
    const memoryRows = page.querySelector('lm-memory-section .rows');
    expect(channelRows?.classList).toContain('big-scrollbar');
    expect(memoryRows?.classList).toContain('big-scrollbar');
    expect(page.querySelector('[lmScrollTogether]')).toBeNull();
  });

  it('Settings says how many keys of each kind the independents show', async () => {
    await show('/settings');
    const channels = page.querySelector<HTMLInputElement>('#channel-keys')!;
    const memories = page.querySelector<HTMLInputElement>('#memory-keys')!;
    expect([channels.value, memories.value]).toEqual(['10', '10']);

    channels.value = '3';
    channels.dispatchEvent(new Event('change'));
    memories.value = '99'; // more than there are: the number stays
    memories.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(memories.value).toBe('10');
    memories.value = '2';
    memories.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    await show('/');
    const keys = [...page.querySelectorAll('lm-independents button.number')];
    expect(keys.map((key) => key.getAttribute('aria-label'))).toEqual([
      'Independent 1, channel 1 (U1)',
      'Independent 2, channel 2 (U1)',
      'Independent 3, channel 3 (U1)',
      'Independent 4, memory 1',
      'Independent 5, memory 2',
    ]);

    // 0 keys of one kind: only the other kind; 0 of both: no independents at all.
    await show('/settings');
    const channelsAgain = page.querySelector<HTMLInputElement>('#channel-keys')!;
    channelsAgain.value = '0';
    channelsAgain.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(channelsAgain.value).toBe('0');
    await show('/');
    expect(page.querySelectorAll('lm-independents button.number').length).toBe(2);

    await show('/settings');
    const memoriesAgain = page.querySelector<HTMLInputElement>('#memory-keys')!;
    memoriesAgain.value = '0';
    memoriesAgain.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    await show('/');
    expect(page.querySelector('lm-independents')).toBeNull();
    expect(page.querySelector('lm-main-view')?.classList).toContain('no-independents');
  });

  it('the zoom makes the whole page bigger and smaller in steps of 10 per cent', async () => {
    await show('/settings');
    const zoom = () => document.documentElement.style.getPropertyValue('--zoom');
    expect(page.querySelector('.scale output')?.textContent?.trim()).toBe('100 %');

    await press(button('Zoom in'));
    await press(button('Zoom in'));
    expect(zoom()).toBe('1.2');
    expect(page.querySelector('.scale output')?.textContent?.trim()).toBe('120 %');

    await press(button('Reset'));
    await press(button('Zoom out'));
    expect(zoom()).toBe('0.9');
    await press(button('Reset'));
  });

  it('the LAN address and password are changed in Settings while LAN is on', async () => {
    await show('/settings#lan');
    expect(page.querySelector('lm-settings-view')?.textContent).toContain('LAN is off');

    connection.emit('LanAccessChanged', {
      local: true,
      on: true,
      addresses: ['192.168.1.20:5080', '10.0.0.5:5080'],
      password: 'K7M2Q',
      chosen: null,
    } satisfies LanAccess);
    await fixture.whenStable();

    const address = page.querySelector<HTMLSelectElement>('#lan-address')!;
    expect([...address.options].map((option) => option.textContent?.trim())).toEqual([
      'Automatic (192.168.1.20:5080)',
      '192.168.1.20:5080',
      '10.0.0.5:5080',
    ]);
    address.value = '10.0.0.5';
    address.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetLanAddress')).toEqual(['10.0.0.5']);

    const password = page.querySelector<HTMLInputElement>('#lan-password')!;
    password.value = 'show night 7';
    await press(button('Set'));
    expect(connection.last('SetLanPassword')).toEqual(['show night 7']);
    expect(page.querySelector('lm-settings-view .done')?.textContent).toContain(
      'The new password is set',
    );

    await press(button('New random'));
    expect(connection.methods()).toContain('NewLanPassword');
  });

  // ---------------------------------------------------------------- effects

  it('Settings offers the row of effect faders and the row of sound faders as None or 1', async () => {
    await show('/settings');
    for (const id of ['#effect-rows', '#sound-rows']) {
      const rows = page.querySelector<HTMLSelectElement>(id)!;
      expect([...rows.options].map((option) => option.textContent?.trim())).toEqual(['None', '1']);
    }
  });
});
