import { ComponentFixture, TestBed } from '@angular/core/testing';
import { App } from '../app';
import { ACTION_GROUPS, actionWithId } from '../desk/desk-actions';
import { DeskSettings } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import { Effect } from './effects';
import { deskPageHelpers, FakeConnection, startDeskPage } from '../testing/desk-page.testing';

describe('the desk page: effects', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;
  const { show, button, press, until, lenses, stageLight, planWith, wait } = deskPageHelpers(
    () => ({ connection, fixture, page, store }),
  );

  beforeEach(async () => {
    ({ connection, fixture, page, store } = await startDeskPage());
  });

  /** An effect of the project, as the desk program keeps it. */
  function anEffect(number: number, more: Partial<Effect> = {}): Effect {
    return {
      number,
      name: `Effect ${number}`,
      kind: 'intensity',
      shape: 'sine',
      group: null,
      lights: [],
      speed: 60,
      size: 100,
      spread: 100,
      direction: 'forward',
      order: 'address',
      blocks: 1,
      wings: 1,
      fadeIn: 0,
      fadeOut: 0,
      duty: 50,
      colors: [],
      angle: 0,
      seed: 0,
      ...more,
    };
  }

  it('the Effects view makes an effect of a kind, and its editor changes it', async () => {
    await show('/effects');
    expect(page.querySelector('lm-effects-view .summary')?.textContent).toContain(
      'Effect 1 is empty',
    );

    // the key of an empty number chooses it, and the kinds to make it are offered
    await press(page.querySelector<HTMLButtonElement>('button[aria-label="Effect 3 key"]'));
    expect(page.querySelector('lm-effects-view .summary')?.textContent).toContain(
      'Effect 3 is empty',
    );
    expect(connection.methods()).not.toContain('SetEffectControl');
    await press(page.querySelector<HTMLButtonElement>('button[aria-label="Effect 1 key"]'));

    await press(button('Intensity'));
    expect(store.effects().map((effect) => effect.number)).toEqual([1]);
    await wait(200);
    const made = connection.last('SetEffect')?.[0] as Effect;
    expect(made).toMatchObject({ number: 1, kind: 'intensity', shape: 'sine', name: 'Sine' });

    const speed = page.querySelector<HTMLInputElement>('#effect-speed')!;
    speed.value = '128';
    speed.dispatchEvent(new Event('change'));
    await wait(200);
    expect((connection.last('SetEffect')?.[0] as Effect).speed).toBe(128);

    // a number out of range is put back as it was
    speed.value = '9000';
    speed.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(speed.value).toBe('128');
  });

  it('a new movement goes around a center of its own, which a press on the preview moves', async () => {
    await show('/effects');
    await press(button('Movement'));
    await wait(200);
    expect(connection.last('SetEffect')?.[0]).toMatchObject({
      kind: 'movement',
      centerPan: 50,
      centerTilt: 50,
    });

    // the square of the heads' range: 200 pixels, 8 from the edges of the preview
    const canvas = page.querySelector<HTMLCanvasElement>('lm-effect-preview canvas')!;
    expect(canvas.classList).toContain('movable');
    const real = canvas.getBoundingClientRect.bind(canvas);
    canvas.getBoundingClientRect = () =>
      ({
        left: 0,
        top: 0,
        right: 216,
        bottom: 216,
        width: 216,
        height: 216,
        x: 0,
        y: 0,
        toJSON: () => ({}),
      }) as DOMRect;
    canvas.dispatchEvent(
      new MouseEvent('pointerdown', { bubbles: true, button: 0, clientX: 58, clientY: 158 }),
    );
    canvas.dispatchEvent(new MouseEvent('pointerup', { bubbles: true }));
    canvas.getBoundingClientRect = real;
    await wait(200);
    expect(connection.last('SetEffect')?.[0]).toMatchObject({
      kind: 'movement',
      centerPan: 25,
      centerTilt: 75,
    });
  });

  it('the key of an effect starts and stops it, its fader moves like a fader, and the desk tells how it runs', async () => {
    connection.emit('EffectsChanged', [anEffect(2, { name: 'Pulse' })]);
    await show('/effects');

    await press(page.querySelector<HTMLButtonElement>('button[aria-label="Effect 2 key"]'));
    expect(connection.last('SetEffectControl')).toEqual([2, 'toggle', 0]);

    store.setEffectLevel(2, 128);
    await wait(40);
    const [number, control, value] = connection.last('SetEffectControl') as [
      number,
      string,
      number,
    ];
    expect([number, control]).toEqual([2, 'level']);
    expect(value).toBeCloseTo(50.2, 1);

    connection.emit('EffectPlaybackChanged', {
      number: 2,
      running: true,
      level: 255,
      speed: 60,
      size: 100,
      spread: 100,
      fading: false,
      phase: 0.5,
      bpm: 60,
      changed: false,
    });
    await fixture.whenStable();
    expect(page.querySelector('button[aria-label="Effect 2 key"]')?.classList).toContain('level-3');
    // the fader this page moved a moment ago keeps the page's level
    expect(store.effectPlaybacks().get(2)?.level).toBe(128);
  });

  it('the Effects view says when a sequence plays an effect whose fader is down, and its key is lit', async () => {
    connection.emit('EffectsChanged', [anEffect(2, { name: 'Pulse' })]);
    await show('/effects');
    store.chosenEffect.set(2);
    connection.emit('EffectPlaybackChanged', {
      number: 2,
      running: true,
      level: 0,
      played: 200,
      speed: 60,
      size: 100,
      spread: 100,
      fading: false,
      phase: 0.5,
      bpm: 60,
      changed: false,
    });
    await fixture.whenStable();
    expect(page.querySelector('lm-effects-view .summary')?.textContent).toContain(
      ', played by a sequence.',
    );
    expect(page.querySelector('button[aria-label="Effect 2 key"]')?.classList).toContain('level-3');
  });

  it('on the Stage view a light in a running effect shows a change at once and keeps it a sixth of a second', async () => {
    await planWith(stageLight(1, 'Par', 1, 'D', '#ffffff', 2, 2));
    connection.emit('EffectsChanged', [
      anEffect(1, { name: 'Pulse', shape: 'pulse', lights: [1], speed: 120 }),
    ]);
    connection.emit('EffectPlaybackChanged', {
      number: 1,
      running: true,
      level: 255,
      speed: 120,
      size: 100,
      spread: 100,
      fading: false,
      phase: 0,
      bpm: 120,
      changed: false,
    });
    await show('/stage');
    const start = performance.now() + 1000; // after the looks the plan has shown so far
    let now = start;
    const clock = vi.spyOn(performance, 'now').mockImplementation(() => now);
    // the desk program's playback at a moment, and the lens the plan shows then
    const at = async (ms: number, level: number): Promise<string> => {
      now = start + ms;
      connection.emit('PlaybackChanged', {
        ...connection.playback,
        effectLevels: { channels: [1], levels: [level] },
      });
      await fixture.whenStable();
      return lenses(1)[0];
    };
    try {
      const dark = await at(0, 0);
      expect(await at(200, 0)).toBe(dark);
      // a short flash comes: it shows at once, whenever it comes ...
      const lit = await at(300, 255);
      expect(lit).not.toBe(dark);
      // ... and stays a sixth of a second on the screen, though it is over sooner
      expect(await at(390, 0)).toBe(lit);
      expect(await at(470, 0)).toBe(dark);
      expect(await at(800, 255)).toBe(lit);
    } finally {
      clock.mockRestore();
    }
  });

  it('a key of an effect that is not there says so and sends nothing', async () => {
    store.toggleEffect(7);
    await fixture.whenStable();
    expect(store.notice()).toContain('There is no effect 7 yet');
    expect(connection.methods()).not.toContain('SetEffectControl');
  });

  it('the channels show what the running effects give, as the desk program mixes them', async () => {
    connection.emit('PlaybackChanged', {
      ...connection.playback,
      effectLevels: { channels: [1, 6], levels: [200, 90] },
    });
    await fixture.whenStable();
    expect(store.shownChannels()[0]).toBe(200);
    expect(store.shownChannels()[5]).toBe(90);
    expect(store.output()[0]).toBe(200);

    connection.emit('PlaybackChanged', { ...connection.playback, effectLevels: null });
    await fixture.whenStable();
    expect(store.shownChannels()[0]).toBe(0);
  });

  it('Settings shows effect keys in the independents and a row of effect faders on the main view', async () => {
    connection.emit('EffectsChanged', [anEffect(1, { name: 'Circle' })]);
    const settings = TestBed.inject(DeskSettings);
    settings.setEffectKeys(2);
    settings.setEffectRows(1);
    await show('/memories');
    await show('/');
    await until(() => !!page.querySelector('lm-main-view lm-effect-section'));
    await fixture.whenStable();

    const key = page.querySelector<HTMLButtonElement>(
      'button[aria-label="Independent 21, effect 1, Circle"]',
    );
    expect(key).toBeTruthy();
    await press(key);
    expect(connection.last('SetEffectControl')).toEqual([1, 'toggle', 0]);
    expect(
      page.querySelectorAll('lm-main-view lm-effect-section lm-fader-strip').length,
    ).toBeGreaterThan(0);

    // Edit binds an effect key to another effect
    await press(button('Edit the channels, memories, effects and sounds of the independent keys'));
    const field = page.querySelector<HTMLInputElement>(
      'input[aria-label="Effect of independent key 21"]',
    )!;
    field.value = '5';
    field.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetEffectIndependent')).toEqual([1, 5]);
  });

  it('every effect key, effect key of the independents and the Effects view has a shortcut', () => {
    for (const id of [
      'view.effects',
      'effect.1',
      'effect.64',
      'ind.effect.32',
      'page.effect.2',
      'effects.keep',
      'effects.reset',
    ]) {
      expect(actionWithId(id)).toBeTruthy();
    }
    expect(ACTION_GROUPS).toContain('Effect keys');
  });

  // ---------------------------------------------------------------- sounds

  it('Copy and Del work on the effects of the row on Main too', async () => {
    connection.emit('EffectsChanged', [anEffect(2, { name: 'Pulse' })]);
    TestBed.inject(DeskSettings).setEffectRows(1);
    await show('/memories');
    await show('/');
    await until(() => !!page.querySelector('lm-main-view lm-effect-section'));
    await fixture.whenStable();

    const key = (number: number) =>
      page.querySelector<HTMLButtonElement>(
        `lm-effect-section button[aria-label="Effect ${number} key"]`,
      )!;
    await press(button('Copy memory'));
    await press(key(2));
    await press(key(4));
    expect(connection.last('CopyEffect')).toEqual([2, 4]);
    await press(button('Delete memory'));
    await press(key(2));
    expect(connection.last('DeleteEffect')).toEqual([2]);
    expect(connection.methods()).not.toContain('SetEffectControl');
  });

  it('the Effects view has Copy and Del beside its row, and a copy made there is chosen', async () => {
    connection.emit('EffectsChanged', [anEffect(2, { name: 'Pulse' })]);
    await show('/effects');
    await until(() => !!page.querySelector('lm-effects-view lm-record-section'));
    const keys = [...page.querySelectorAll('lm-effects-view lm-record-section button')].map(
      (item) => item.getAttribute('aria-label'),
    );
    expect(keys).toEqual(['Copy effect', 'Delete effect']);
    const key = (number: number) =>
      page.querySelector<HTMLButtonElement>(
        `lm-effects-view button[aria-label="Effect ${number} key"]`,
      )!;
    await press(button('Copy effect'));
    await press(key(2));
    await press(key(5));
    expect(connection.last('CopyEffect')).toEqual([2, 5]);
    expect(store.chosenEffect()).toBe(5);
    await press(button('Delete effect'));
    await press(key(2));
    expect(connection.last('DeleteEffect')).toEqual([2]);
    expect(connection.methods()).not.toContain('SetEffectControl');
  });
});
