import { ComponentFixture, TestBed } from '@angular/core/testing';
import { App } from '../app';
import { DeskSettings } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import { EMPTY_STAGE, StagePlan } from '../desk/desk-types';
import { deskPageHelpers, FakeConnection, LIVE, startDeskPage } from '../testing/desk-page.testing';

describe('the desk page: the Stage view', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;
  const {
    show,
    button,
    press,
    stackStands,
    until,
    textOf,
    lenses,
    stageLight,
    planWith,
    pressLight,
  } = deskPageHelpers(() => ({ connection, fixture, page, store }));

  beforeEach(async () => {
    ({ connection, fixture, page, store } = await startDeskPage());
  });

  it('the Stage view shows every light in the color its channels give it, after the master, dark in blackout', async () => {
    await planWith(
      stageLight(1, 'Pot red', 3, 'D', '#ff2000', 2, 8),
      stageLight(2, 'LED bar', 11, 'RGBRGB', null, 8, 1),
      stageLight(3, 'Spot', 5, 'D', null, 8, 5),
      stageLight(4, 'Hazer', 20, 'X', null, -1, 1),
    );
    await show('/stage');
    expect(page.querySelectorAll('lm-stage-view .light').length).toBe(4);
    expect(lenses(1)).toEqual(['0 0 0']); // dark until its channel is up

    store.setChannel(3, 255);
    store.setChannel(11, 255); // red of the first cell of the bar
    store.setChannel(16, 255); // blue of the second
    store.setChannel(20, 255);
    await fixture.whenStable();
    expect(lenses(1)).toEqual(['255 32 0']);
    expect(lenses(2)).toEqual(['255 0 0', '0 0 255']);
    expect(page.querySelector<HTMLElement>('[data-light="4"] .level')?.style.height).toBe('100%'); // the hazer gives no light

    // A memory that holds the channel lights it too: memory 2 has channel 5 at 200.
    store.setMemoryLevel(2, 255);
    await fixture.whenStable();
    expect(lenses(3)).not.toEqual(['0 0 0']);

    // The master dims what goes out; blackout darkens it all.
    store.setMaster(128);
    await fixture.whenStable();
    expect(lenses(1)).toEqual(['186 23 0']);
    connection.emit('BlackoutChanged', true);
    await fixture.whenStable();
    expect(lenses(1)).toEqual(['0 0 0']);
    expect(textOf('lm-stage-view .summary')).toContain('Blackout is on');
  });

  it('Edit puts a light on the next free channel, named after its note; it is moved, changed and taken away', async () => {
    await planWith(stageLight(1, 'Par', 1, 'DRGBS', null, 2, 8));
    await show('/stage');
    expect(button('Add light').disabled).toBe(true); // only while editing
    await press(button('Edit the plan'));
    expect(button('Edit the plan').getAttribute('aria-pressed')).toBe('true');

    // channels 1 to 5 are the par's: the new light is on 6, which has the note "Front / left"
    await press(button('Add light'));
    const added = (connection.last('SetStage')?.[0] as StagePlan).lights[1];
    expect(added).toMatchObject({ id: 2, name: 'Front left', address: 6, channels: 'D' });
    expect(page.querySelector<HTMLInputElement>('#light-name')?.value).toBe('Front left'); // chosen, in the panel

    // An RGB bar of four cells, without a dimmer.
    const kind = page.querySelector<HTMLSelectElement>('#light-type')!;
    kind.value = 'rgb';
    kind.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    const cells = page.querySelector<HTMLInputElement>('#light-cells')!;
    cells.value = '4';
    cells.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.stage().lights[1].channels).toBe('RGBRGBRGBRGB');
    expect(page.querySelectorAll('[data-light="2"] .lens').length).toBe(4);

    // The arrow keys move it by half a square; Delete takes it away.
    const element = page.querySelector<HTMLElement>('[data-light="2"]')!;
    const before = store.stage().lights[1];
    element.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true }),
    );
    await fixture.whenStable();
    expect(store.stage().lights[1].x).toBe(before.x + 0.5);
    expect(connection.last('SetStage')?.[0]).toEqual(store.stage());

    element.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Delete', bubbles: true, cancelable: true }),
    );
    await fixture.whenStable();
    expect(store.stage().lights.map((light) => light.id)).toEqual([1]);
    expect(page.querySelector('#stage-width')).toBeTruthy(); // nothing chosen: the panel shows the stage
  });

  it('a click on a light shows it at the right with the faders of its channels, which work as those of Main', async () => {
    await planWith(
      stageLight(1, 'Par', 11, 'DRGB', null, 2, 8),
      stageLight(2, 'Spot', 5, 'D', null, 8, 5),
    );
    await show('/stage');
    // The panel is always there, so that the plan keeps its size: with nothing chosen, what a click does.
    const nothingChosen = () =>
      textOf('lm-stage-panel h3') === 'Light' && page.querySelector('lm-light-faders') === null;
    expect(nothingChosen()).toBe(true);

    await pressLight(1);
    expect(textOf('lm-stage-panel h3')).toBe('Par');
    expect(textOf('lm-stage-panel .facts')).toBe('RGB, dimmer first, on channels 11 to 14 (U1)');
    expect(page.querySelector('#light-name')).toBeNull(); // out of Edit nothing about it can be changed...
    const faders = [...page.querySelectorAll<HTMLElement>('lm-light-faders lm-fader')];
    expect(faders.map((fader) => fader.getAttribute('aria-label'))).toEqual([
      'Channel 11 (U1), dimmer',
      'Channel 12 (U1), red',
      'Channel 13 (U1), green',
      'Channel 14 (U1), blue',
    ]);

    // ...but its faders work: the dimmer and the red to full, and the light is red.
    for (const fader of faders.slice(0, 2))
      fader.dispatchEvent(new KeyboardEvent('keydown', { key: 'End', bubbles: true }));
    await fixture.whenStable();
    expect(store.channels().slice(10, 14)).toEqual([255, 255, 0, 0]);
    expect(lenses(1)).toEqual(['255 0 0']);
    await new Promise((resolve) => setTimeout(resolve, 40)); // fader moves are sent a moment later
    expect(connection.calls).toContainEqual({ method: 'SetChannel', args: [12, 255] });

    // A memory that holds a channel holds its fader up here too: memory 2 has channel 5 at 200.
    await pressLight(2);
    store.setMemoryLevel(2, 255);
    await fixture.whenStable();
    expect(page.querySelector('lm-light-faders lm-fader')?.getAttribute('aria-valuenow')).toBe(
      '200',
    );

    // A press beside the lights, or Close, lets the light go.
    page
      .querySelector('lm-stage-view .plan')!
      .dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, button: 0 }));
    await fixture.whenStable();
    expect(nothingChosen()).toBe(true);
    await pressLight(2);
    await press(button('Close'));
    expect(nothingChosen()).toBe(true);

    // In Edit the faders stand under what can be changed.
    await press(button('Edit the plan'));
    await pressLight(1);
    expect(page.querySelector('#light-name')).toBeTruthy();
    expect(page.querySelectorAll('lm-light-faders lm-fader').length).toBe(4);
  });

  it('a double click puts the faders of a light to full and back; Clear does what Clear on Main does', async () => {
    await planWith(
      stageLight(1, 'Par', 11, 'DRGBS', null, 2, 8),
      stageLight(2, 'Spot', 5, 'D', null, 8, 5),
    );
    await show('/stage');
    const twice = (id: number) =>
      page
        .querySelector(`[data-light="${id}"]`)!
        .dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));

    twice(1);
    await fixture.whenStable();
    expect(store.channels().slice(10, 15)).toEqual([255, 255, 255, 255, 0]); // the strobe stays
    expect(lenses(1)).toEqual(['255 255 255']);
    twice(1);
    await fixture.whenStable();
    expect(store.channels().slice(10, 15)).toEqual([0, 0, 0, 0, 0]);

    // Clear: the key of the main view, the same action: every channel fader and memory fader to zero.
    twice(1);
    twice(2);
    store.setChannel(30, 99);
    store.setMemoryLevel(2, 120);
    await fixture.whenStable();
    const clear = page.querySelector<HTMLButtonElement>(
      'lm-stage-view [data-action="master.clear"]',
    )!;
    expect(clear.textContent?.trim()).toBe('Clear');
    await press(clear);
    expect(store.channels().slice(10, 15)).toEqual([0, 0, 0, 0, 0]);
    expect(store.channels()[4]).toBe(0);
    expect(store.channels()[29]).toBe(0); // a channel that no light uses goes down too, as on Main
    expect(store.memoryLevels()[1]).toBe(0);
    expect(connection.methods()).toContain('Clear');
  });

  it('a light that strobes flashes on the Stage view, never more than three times a second, with a mark of how fast', async () => {
    const color = (name: string, code: string) => ({ name, code, fine: null, ranges: [] });
    const strobe = {
      name: 'Strobe',
      code: 'S',
      fine: null,
      ranges: [
        { from: 0, to: 31, label: 'Closed', color: null },
        { from: 32, to: 63, label: 'Open', color: null },
        { from: 64, to: 255, label: 'Strobe, 1Hz to 20Hz', color: null },
      ],
    };
    connection.emit('StageChanged', {
      ...EMPTY_STAGE,
      lights: [
        { ...stageLight(1, 'Bar', 1, 'RGBS', null, 2, 2), type: 1 },
        stageLight(2, 'Par', 11, 'RGBS', null, 6, 2),
      ],
      types: [
        {
          id: 1,
          maker: 'Maker',
          model: 'Bar',
          mode: '4-channel',
          source: 'maker/bar',
          channels: [color('Red', 'R'), color('Green', 'G'), color('Blue', 'B'), strobe],
        },
      ],
    } satisfies StagePlan);
    await show('/stage');
    const body = (id: number) => page.querySelector<HTMLElement>(`[data-light="${id}"] .fixture`)!;
    const mark = (id: number) =>
      page.querySelector(`[data-light="${id}"] .strobe-mark`)?.textContent?.trim();

    // red with its shutter open: steady light
    store.setChannel(1, 255);
    store.setChannel(4, 40);
    await fixture.whenStable();
    expect(body(1).classList).not.toContain('strobe');
    expect(mark(1)).toBeUndefined();

    // a strobe of 20 a second: the screen flashes 3 times a second, and the mark says the 20
    store.setChannel(4, 255);
    await fixture.whenStable();
    expect(body(1).classList).toContain('strobe');
    expect(body(1).dataset['strobe']).toBe('strobe');
    expect(body(1).style.getPropertyValue('--strobe-period')).toBe('0.333s');
    expect(mark(1)).toBe('⚡ 20 Hz');
    expect(page.querySelector('[data-light="1"]')?.getAttribute('title')).toBe(
      'Bar: channels 1 to 4 (U1); strobe, 20 Hz',
    );

    // a light of letters says nothing of its strobe: above its first values it strobes, at a speed it does not say
    store.setChannel(11, 255);
    store.setChannel(14, 128);
    await fixture.whenStable();
    expect(mark(2)).toBe('⚡');

    // a light that gives no light shows no strobe
    store.setChannel(1, 0);
    await fixture.whenStable();
    expect(body(1).classList).not.toContain('strobe');
  });

  it('the faders of the main view say what the plan has on a channel where no note was typed', async () => {
    await planWith(stageLight(1, 'Spot', 1, 'PTD', null, 2, 2));
    const fields = [
      ...page.querySelectorAll<HTMLInputElement>(
        '[aria-label="Note for Channel 1 (U1)"], [aria-label="Note for Channel 1 (U1), second line"]',
      ),
    ];
    expect(fields.map((field) => field.placeholder)).toEqual(['Spot', 'Pan']);
    expect(fields.map((field) => field.value)).toEqual(['', '']);
  });

  it('a light is round or square and as big as some squares; one cell lights all of it, cells divide it', async () => {
    await planWith(stageLight(1, 'Bar', 11, 'RGB', null, 3, 2));
    await show('/stage');
    await press(button('Edit the plan'));
    await pressLight(1);

    const shape = page.querySelector<HTMLSelectElement>('#light-shape')!;
    shape.value = 'square';
    shape.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    const width = page.querySelector<HTMLInputElement>('#light-width')!;
    width.value = '4';
    width.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    // four squares wide and one deep, lying on the lines of the grid (from 2 to 6 across, 2 to 3 deep)
    expect(store.stage().lights[0]).toMatchObject({
      shape: 'square',
      width: 4,
      depth: 1,
      x: 4,
      y: 2.5,
    });
    const fixtureOf = () => page.querySelector<HTMLElement>('[data-light="1"] .fixture')!;
    expect(fixtureOf().classList).toContain('square');
    expect(fixtureOf().style.getPropertyValue('--w')).toBe('3.92');

    // One cell: red on its channel, and all of the bar is red.
    store.setChannel(11, 255);
    await fixture.whenStable();
    expect(lenses(1)).toEqual(['255 0 0']);

    // Four cells: they divide it, and only the first is red.
    const cells = page.querySelector<HTMLInputElement>('#light-cells')!;
    cells.value = '4';
    cells.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(lenses(1)).toEqual(['255 0 0', '0 0 0', '0 0 0', '0 0 0']);
  });

  it('a picture lies under the stage or under the whole plan, and shows through as brightly as set', async () => {
    await show('/stage');
    await press(button('Edit the plan'));
    expect(page.querySelector('lm-stage-view img.picture')).toBeNull();

    // Another page put a picture under the plan: the plan names it, and the page fetches it.
    connection.stagePicture = 'data:image/png;base64,iVBORw0KGgo=';
    connection.emit('StageChanged', {
      ...EMPTY_STAGE,
      pictureId: '0123456789abcdef',
    } satisfies StagePlan);
    await until(() => page.querySelector('lm-stage-view img.picture') !== null);
    const picture = () => page.querySelector<HTMLImageElement>('lm-stage-view img.picture')!;
    expect(picture().src).toBe('data:image/png;base64,iVBORw0KGgo=');
    expect(picture().style.opacity).toBe('0.6');
    expect(picture().style.left).toBe('10%'); // on the stage: 2 of the 20 squares of the plan beside it

    const over = page.querySelector<HTMLSelectElement>('#picture-over')!;
    over.value = 'plan';
    over.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetStage')?.[0]).toMatchObject({ pictureOver: 'plan' });
    expect(picture().style.left).toBe('0%');

    const opacity = page.querySelector<HTMLInputElement>('#picture-opacity')!;
    opacity.value = '30';
    opacity.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetStage')?.[0]).toMatchObject({ pictureOpacity: 0.3 });
    expect(picture().style.opacity).toBe('0.3');

    await press(button('Remove the picture'));
    expect(connection.last('SetStagePicture')).toEqual([null]);
    expect(page.querySelector('lm-stage-view img.picture')).toBeNull();
  });

  it('the stage plan cannot be changed while the stack is live', async () => {
    await show('/stage');
    await press(button('Edit the plan'));
    expect(page.querySelector('#stage-width')).toBeTruthy();

    await stackStands(LIVE);
    expect(page.querySelector('#stage-width')).toBeNull(); // Edit is over: the panel says what a click does
    expect(button('Edit the plan').disabled).toBe(true);
    expect(textOf('lm-stage-view .summary')).toContain('The stack is live');
    expect(textOf('lm-stage-view .nothing')).toBe('No lights yet. Edit, then Add light.');
  });

  it('the Stage view hides the panel of the light with its key Light, and Edit shows it again', async () => {
    await show('/stage');
    expect(page.querySelector('lm-stage-panel')).toBeTruthy();
    await press(button('Light panel'));
    expect(page.querySelector('lm-stage-panel')).toBeNull();
    expect(page.querySelector('lm-stage-view')?.classList).toContain('no-panel');
    expect(TestBed.inject(DeskSettings).stagePanel()).toBe(false);
    await press(button('Edit the plan'));
    expect(page.querySelector('lm-stage-panel')).toBeTruthy();
    await press(button('Edit the plan'));
    await press(button('Light panel'));
    expect(page.querySelector('lm-stage-panel')).toBeTruthy();
  });
});
