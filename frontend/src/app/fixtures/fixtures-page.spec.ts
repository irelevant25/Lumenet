import { ComponentFixture } from '@angular/core/testing';
import { App } from '../app';
import { DeskStore } from '../desk/desk-store';
import { EMPTY_STAGE, StageLight, StagePlan, TimedStep, UNIVERSE_SIZE } from '../desk/desk-types';
import { NOTE_LINE_SEPARATOR } from '../desk/notes';
import { deskPageHelpers, FakeConnection, startDeskPage, step } from '../testing/desk-page.testing';

describe('the desk page: fixtures', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;
  const { show, button, press, stackStands, textOf, stageLight, planWith, pressLight } =
    deskPageHelpers(() => ({ connection, fixture, page, store }));

  beforeEach(async () => {
    ({ connection, fixture, page, store } = await startDeskPage());
  });

  /** The desk program sends a plan with these lights and one group, "Pars", of the first light. */
  async function planWithGroup(...lights: StageLight[]): Promise<void> {
    connection.emit('StageChanged', {
      ...EMPTY_STAGE,
      lights,
      groups: [{ id: 1, name: 'Pars', lights: [lights[0].id] }],
    } satisfies StagePlan);
    await fixture.whenStable();
  }

  it('the Fixtures view plays the lights of the plan: a fader each, a color, and groups that move all their lights', async () => {
    await planWith(
      stageLight(1, 'Par', 11, 'RGB', null, 2, 8),
      stageLight(2, 'Front', 5, 'D', null, 8, 5),
    );
    await show('/fixtures');
    const strip = (name: string) =>
      [...page.querySelectorAll('lm-fixture-strip')].find(
        (item) =>
          item.querySelector('.swatch')?.getAttribute('aria-label') ===
          `${name}: color and settings`,
      )!;
    // under each key the note, as under a channel fader: where nothing was typed, the name from the stage, dimmed
    const notes = () => [
      ...page.querySelectorAll<HTMLInputElement>('lm-fixture-strip .note input:first-child'),
    ];
    expect(notes().map((field) => [field.placeholder, field.value])).toEqual([
      ['Front', ''],
      ['Par', ''],
    ]);

    // a fixture without a dimmer comes up in white from dark, and its fader keeps its colors' mix
    strip('Par')
      .querySelector('lm-fader')!
      .dispatchEvent(new KeyboardEvent('keydown', { key: 'End' }));
    await fixture.whenStable();
    expect(store.channels().slice(10, 13)).toEqual([255, 255, 255]);
    await press(strip('Front').querySelector<HTMLButtonElement>('button[lmLamp]'));
    expect(store.channels()[4]).toBe(255);

    // its color on top shows it in the panel, with colors to press
    await press(button('Par: color and settings'));
    expect(page.querySelector('lm-fixture-panel h3')?.textContent?.trim()).toBe('Par');
    await press(button('Red'));
    expect(store.channels().slice(10, 13)).toEqual([255, 0, 0]);
    expect(
      strip('Par').querySelector<HTMLElement>('.swatch')!.style.getPropertyValue('--swatch'),
    ).toBe('#ff0000');

    // a new group takes the fixture chosen; its fader moves all its lights, each in its own color
    await press(button('New group'));
    expect(connection.stagePlan.groups).toEqual([{ id: 1, name: 'Group 1', lights: [1] }]);
    const front = page.querySelector<HTMLInputElement>(
      'lm-fixture-panel .members li:first-child input',
    )!;
    front.click(); // Front, the first by its address, joins the group
    await fixture.whenStable();
    expect(connection.stagePlan.groups[0].lights).toEqual([1, 2]);
    strip('Group 1')
      .querySelector('lm-fader')!
      .dispatchEvent(new KeyboardEvent('keydown', { key: 'Home' }));
    await fixture.whenStable();
    expect(store.channels()[4]).toBe(0);
    expect(store.channels().slice(10, 13)).toEqual([0, 0, 0]);
    strip('Group 1')
      .querySelector('lm-fader')!
      .dispatchEvent(new KeyboardEvent('keydown', { key: 'End' }));
    await fixture.whenStable();
    expect(store.channels()[4]).toBe(255);
    expect(store.channels().slice(10, 13)).toEqual([255, 0, 0]); // red again, as it was

    // A note typed under a key belongs to that fixture or group, with the stage plan; the name stays.
    const write = async (name: string, line: 0 | 1, text: string) => {
      const field = strip(name).querySelectorAll<HTMLInputElement>('.note input')[line];
      field.value = text;
      field.dispatchEvent(new Event('change'));
      await fixture.whenStable();
    };
    await write('Par', 1, 'stage left');
    await write('Group 1', 0, 'All');
    expect(connection.stagePlan.lights.find((light) => light.id === 1)).toMatchObject({
      name: 'Par',
      note: NOTE_LINE_SEPARATOR + 'stage left',
    });
    expect(connection.stagePlan.groups[0]).toMatchObject({ name: 'Group 1', note: 'All' });
    expect(
      [...strip('Par').querySelectorAll<HTMLInputElement>('.note input')].map(
        (field) => field.value,
      ),
    ).toEqual(['', 'stage left']);
    expect(strip('Par').querySelector('.note')?.getAttribute('title')).toBe('stage left');

    // While the stack is live a note is lettering only.
    await stackStands({ ...connection.playback.stack, live: true });
    expect(notes().every((field) => field.readOnly)).toBe(true);
  });

  it('the panel of a fixture says what its channels do at their values: in its lists, under its faders and on its letters', async () => {
    const strobe = {
      name: 'Strobe',
      code: 'S',
      fine: null,
      ranges: [
        { from: 0, to: 31, label: 'Closed', color: null },
        { from: 32, to: 63, label: 'Open', color: null },
        { from: 64, to: 95, label: 'Strobe, slow to fast', color: null },
        { from: 96, to: 255, label: 'Open', color: null },
      ],
    };
    connection.emit('StageChanged', {
      ...EMPTY_STAGE,
      lights: [{ ...stageLight(1, 'Hex bar', 1, 'RGBS', null, 2, 8), type: 1 }],
      types: [
        {
          id: 1,
          maker: 'American DJ',
          model: 'Ultra Hex Bar 12',
          mode: '4-channel',
          source: 'american-dj/ultra-hex-bar-12',
          channels: [
            { name: 'Red', code: 'R', fine: null, ranges: [] },
            { name: 'Green', code: 'G', fine: null, ranges: [] },
            { name: 'Blue', code: 'B', fine: null, ranges: [] },
            strobe,
          ],
        },
      ],
    } satisfies StagePlan);
    await fixture.whenStable();
    await show('/fixtures');
    await press(button('Hex bar: color and settings'));

    // the list of the strobe: every choice with its values, so the two Opens are told apart
    const list = page.querySelector<HTMLSelectElement>('lm-fixture-panel .form select')!;
    expect([...list.options].map((option) => option.text.trim())).toEqual([
      'Closed (0 to 31)',
      'Open (32 to 63)',
      'Strobe, slow to fast (64 to 95)',
      'Open (96 to 255)',
    ]);

    // under the faders: what the fader moved last does at its value
    const said = () =>
      page.querySelector('lm-fixture-panel lm-light-faders .said')?.textContent?.trim();
    expect(said()).toBe(
      'Move a fader to see here what its value does; the pointer on its letter shows all its values.',
    );
    const fader = page.querySelectorAll<HTMLElement>(
      'lm-fixture-panel lm-light-faders lm-fader',
    )[3];
    for (const press of [1, 2])
      fader.dispatchEvent(
        new KeyboardEvent('keydown', { key: 'PageUp', bubbles: true, detail: press }),
      );
    await fixture.whenStable();
    expect(store.channels()[3]).toBe(32);
    expect(said()).toBe('S (4) at 32: Open (32 to 63)');
    expect(fader.getAttribute('aria-valuetext')).toBe('32, Open');
    expect(list.selectedOptions[0].text.trim()).toBe('Open (32 to 63)');

    // the pointer on the letter: all its values
    const letters = page.querySelectorAll<HTMLElement>('lm-fixture-panel lm-light-faders .letter');
    expect(letters[3].title).toBe(
      'Strobe\n0 to 31: Closed\n32 to 63: Open\n64 to 95: Strobe, slow to fast\n96 to 255: Open',
    );
    expect(letters[0].title).toBe('Red');
  });

  it('a moving head takes its place from the memory raised last; the master and blackout leave it where it is', async () => {
    await planWith(stageLight(1, 'Head', 1, 'PTD', null, 2, 2));
    const look = (pan: number, tilt: number) => [
      pan,
      tilt,
      255,
      ...new Array<number>(UNIVERSE_SIZE - 3).fill(0),
    ];
    store.memoryLooks.set({ 4: look(10, 20), 5: look(200, 220) });

    store.setMemoryLevel(4, 128);
    expect(store.shownChannels().slice(0, 3)).toEqual([10, 20, 128]); // the place as stored, the dimmer at half
    store.setMemoryLevel(5, 255);
    expect(store.shownChannels().slice(0, 3)).toEqual([200, 220, 255]);
    store.setChannel(1, 99); // a fader moved later takes its own channel
    expect(store.shownChannels().slice(0, 3)).toEqual([99, 220, 255]);
    store.setMemoryLevel(5, 0);
    expect(store.shownChannels().slice(0, 3)).toEqual([99, 20, 128]);

    store.setMaster(128);
    expect(store.output().slice(0, 3)).toEqual([99, 20, 64]);
    connection.emit('BlackoutChanged', true);
    expect(store.output().slice(0, 3)).toEqual([99, 20, 0]);
  });

  it('a light can be limited, copied, and made a light of the fixture library', async () => {
    await planWith(stageLight(1, 'Spot 1', 11, 'D', null, 2, 2));
    await show('/stage');
    await press(button('Edit the plan'));
    await pressLight(1);

    const limit = page.querySelector<HTMLInputElement>('#light-limit')!;
    limit.value = '60';
    limit.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.stagePlan.lights[0].limit).toBe(60);

    await press(button('Copy the light'));
    expect(connection.stagePlan.lights[1]).toMatchObject({
      id: 2,
      name: 'Spot 2',
      address: 12,
      limit: 60,
    });
    // the copy is chosen now

    // the library: its list is fetched (here from a pretend library), searched, and a mode chosen
    const files =
      'https://raw.githubusercontent.com/OpenLightingProject/open-fixture-library/master/fixtures';
    const answers: Record<string, unknown> = {
      [`${files}/manufacturers.json`]: { maker: { name: 'Maker' } },
      'https://api.github.com/repos/OpenLightingProject/open-fixture-library/git/trees/master:fixtures?recursive=1':
        {
          tree: [{ path: 'maker/head.json', type: 'blob' }],
        },
      [`${files}/maker/head.json`]: {
        name: 'Head',
        availableChannels: {
          Pan: { capability: { type: 'Pan' } },
          Tilt: { capability: { type: 'Tilt' } },
          Dimmer: { capability: { type: 'Intensity' } },
        },
        modes: [{ name: '3-channel', channels: ['Pan', 'Tilt', 'Dimmer'] }],
      },
    };
    vi.stubGlobal('fetch', async (url: string) => ({
      ok: url in answers,
      status: url in answers ? 200 : 404,
      json: async () => answers[url],
    }));
    try {
      const kind = page.querySelector<HTMLSelectElement>('#light-type')!;
      kind.value = 'library-search';
      kind.dispatchEvent(new Event('change'));
      await fixture.whenStable();
      await new Promise((resolve) => setTimeout(resolve, 0));
      await fixture.whenStable();
      const search = page.querySelector<HTMLInputElement>(
        'lm-library-dialog input[type="search"]',
      )!;
      search.value = 'maker head';
      search.dispatchEvent(new Event('input'));
      await fixture.whenStable();
      await press(page.querySelector<HTMLButtonElement>('lm-library-dialog .found button'));
      await new Promise((resolve) => setTimeout(resolve, 0));
      await fixture.whenStable();
      await press(button('Use this mode'));
    } finally {
      vi.unstubAllGlobals();
      localStorage.clear();
    }
    expect(connection.stagePlan.types).toEqual([
      {
        id: 1,
        maker: 'Maker',
        model: 'Head',
        mode: '3-channel',
        source: 'maker/head',
        channels: [
          { name: 'Pan', code: 'P', fine: null, ranges: [] },
          { name: 'Tilt', code: 'T', fine: null, ranges: [] },
          { name: 'Dimmer', code: 'D', fine: null, ranges: [] },
        ],
      },
    ]);
    expect(connection.stagePlan.lights[1]).toMatchObject({
      type: 1,
      channels: 'PTD',
      name: 'Spot 2',
      address: 12,
    });
    expect(connection.stagePlan.lights[0]).toMatchObject({ channels: 'D' });
    expect(
      page.querySelector<HTMLSelectElement>('#light-type')!.selectedOptions[0].textContent?.trim(),
    ).toBe('Head, 3-channel');
  });

  it('a step of a sequence can be a fixture or a group, in a color', async () => {
    await planWithGroup(
      stageLight(1, 'Par', 11, 'RGB', null, 2, 8),
      stageLight(2, 'Front', 5, 'D', null, 8, 5),
    );
    await show('/sequences');
    await press(button('Work on memory 3'));
    const kind = page.querySelector<HTMLSelectElement>(
      'select[aria-label="Step 1, channel, memory, fixture, group, effect, sound or release"]',
    )!;
    expect([...kind.options].map((option) => option.text.trim())).toEqual([
      'Channel (U1)',
      'Memory',
      'Fixture',
      'Group',
      'Effect',
      'Sound',
      'Release',
    ]);

    // a fixture: the first by its address, which the closed field shows; its name is the note
    kind.value = 'fixture';
    kind.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetSequence')?.[1]).toMatchObject({
      steps: [
        { ...step(5, 200), target: 'fixture', number: 2, color: null },
        step(6, 100, { wait: 0 }),
      ],
    });
    const first = page.querySelectorAll('lm-sequences-view table.steps tbody tr')[0];
    expect(
      first.querySelector<HTMLSelectElement>('td.number select')!.selectedOptions[0].text.trim(),
    ).toBe('5 (U1)');
    expect(first.querySelector('td.note')?.textContent?.trim()).toBe('Front');

    // its color: the colors of the Fixtures page. A press only chooses, the window shows it; Use gives it to the step.
    const colorWindow = page.querySelector<HTMLDialogElement>('lm-step-color-dialog dialog')!;
    const sent = connection.calls.length;
    await press(button('Step 1, color'));
    await press(
      page.querySelector<HTMLButtonElement>('lm-step-color-dialog button[aria-label="Blue"]'),
    );
    await press(
      page.querySelector<HTMLButtonElement>('lm-step-color-dialog button[aria-label="Red"]'),
    );
    expect(colorWindow.open).toBe(true);
    expect(textOf('lm-step-color-dialog .chosen')).toBe('Chosen: #ff0000');
    expect(connection.calls.slice(sent).map((call) => call.method)).not.toContain('SetSequence');
    await press(button('Use this color'));
    expect(colorWindow.open).toBe(false);
    expect(connection.last('SetSequence')?.[1]).toMatchObject({
      steps: [{ target: 'fixture', number: 2, color: '#ff0000' }, step(6, 100, { wait: 0 })],
    });

    // Cancel leaves the step as it was
    const before = connection.calls.length;
    await press(button('Step 1, color'));
    expect(textOf('lm-step-color-dialog .chosen')).toBe('Chosen: #ff0000'); // it opens on the step's color
    await press(button('White, no color'));
    expect(textOf('lm-step-color-dialog .chosen')).toBe('Chosen: white, no color');
    await press(
      page.querySelector<HTMLButtonElement>('lm-step-color-dialog .actions button:nth-child(2)'),
    ); // Cancel
    expect(colorWindow.open).toBe(false);
    expect(connection.calls.slice(before).map((call) => call.method)).not.toContain('SetSequence');

    // a group: its name is the note, "G1" the closed field
    kind.value = 'group';
    kind.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetSequence')?.[1]).toMatchObject({
      steps: [{ target: 'group', number: 1, color: '#ff0000' }, step(6, 100, { wait: 0 })],
    });
    expect(
      first.querySelector<HTMLSelectElement>('td.number select')!.selectedOptions[0].text.trim(),
    ).toBe('G1');
    expect(first.querySelector('td.note')?.textContent?.trim()).toBe('Pars');
  });

  it('Rec seq takes steps of fixtures and groups from the Fixtures page, in the color they show', async () => {
    await planWithGroup(
      stageLight(1, 'Par', 11, 'RGB', null, 2, 8),
      stageLight(2, 'Front', 5, 'D', null, 8, 5),
    );
    store.setChannel(11, 255); // the par is red
    store.pressFunction('record');
    store.pressMemory(9);
    await show('/fixtures');
    const key = (name: string) =>
      [...page.querySelectorAll('lm-fixture-strip')]
        .find(
          (item) =>
            item.querySelector('.swatch')?.getAttribute('aria-label') ===
            `${name}: color and settings`,
        )!
        .querySelector<HTMLButtonElement>('button[lmLamp]')!;

    await press(key('Par'));
    expect(connection.last('AddSequenceStep')).toEqual([
      9,
      { ...step(1, 255), target: 'fixture', color: '#ff0000' },
    ]);
    await press(key('Pars'));
    expect(connection.last('AddSequenceStep')).toEqual([
      9,
      { ...step(1, 255), target: 'group', color: '#ff0000' },
    ]);
    expect(store.channels()[10]).toBe(255); // the key put in a step and did not switch the par
    expect(key('Par').querySelector('.steps')?.textContent?.trim()).toBe('1');
  });

  it('a template runs over the fixtures of a group, in a color', async () => {
    connection.emit('StageChanged', {
      ...EMPTY_STAGE,
      lights: [
        stageLight(4, 'Right', 30, 'RGB', null, 2, 8),
        stageLight(3, 'Left', 20, 'RGB', null, 8, 5),
      ],
      groups: [{ id: 1, name: 'Pars', lights: [4, 3] }],
    } satisfies StagePlan);
    await fixture.whenStable();
    await show('/sequences');
    await press(button('Work on memory 3'));
    await press(button('Make the steps from a template'));
    await press(button('Chase'));
    const target = page.querySelector<HTMLSelectElement>('#template-target')!;
    target.value = 'group';
    target.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    await press(
      page.querySelector<HTMLButtonElement>('lm-template-dialog button[aria-label="Blue"]'),
    );
    await press(button('Make the steps'));

    const steps = (connection.last('SetSequence')?.[1] as { steps: TimedStep[] }).steps;
    // the lights of the group by their address: Left (20), then Right (30)
    expect(steps.map((made) => [made.target, made.number, made.color])).toEqual([
      ['fixture', 3, '#0000ff'],
      ['fixture', 4, '#0000ff'],
    ]);
  });
});
