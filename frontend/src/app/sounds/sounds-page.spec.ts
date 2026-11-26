import { ComponentFixture, TestBed } from '@angular/core/testing';
import { App } from '../app';
import { ACTION_GROUPS, actionWithId } from '../desk/desk-actions';
import { DeskSettings } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import { Sequence } from '../desk/desk-types';
import { newSound, newTrack, quietPlayback, Sound, SoundSetup } from './sounds';
import { soundChoices } from '../stack/step-choices';
import { actionLamp } from '../desk/action-lamps';
import { deskPageHelpers, FakeConnection, startDeskPage, step } from '../testing/desk-page.testing';

describe('the desk page: sounds', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;
  const { show, button, buttonIn, memoryKey, press, stackStands, until, wait, aSound } =
    deskPageHelpers(() => ({ connection, fixture, page, store }));

  beforeEach(async () => {
    ({ connection, fixture, page, store } = await startDeskPage());
  });

  it('SOUND stands at the right of GENERAL: MUTE blinks while on, and the volume goes from -100 dB to 0 dB, -20 dB at first and by a double click', async () => {
    const mute = page.querySelector<HTMLButtonElement>(
      'lm-sound-master button[aria-label="Mute"]',
    )!;
    expect(mute.getAttribute('aria-pressed')).toBe('false');
    await press(mute);
    expect(connection.last('SetSoundMute')).toEqual([true]);
    expect(mute.getAttribute('aria-pressed')).toBe('true');
    expect(actionLamp('sound.mute', store, '/')).toEqual({ level: 1, blink: true });

    const fader = page.querySelector<HTMLElement>('lm-sound-master lm-fader')!;
    expect([fader.getAttribute('aria-valuemax'), fader.getAttribute('aria-valuenow')]).toEqual([
      '255',
      '81',
    ]);
    expect(fader.getAttribute('aria-valuetext')).toBe('-20 decibels');
    expect(page.querySelector('lm-sound-master .readout')?.textContent?.trim()).toBe('-20 dB');
    fader.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Home', bubbles: true, cancelable: true }),
    );
    await wait(40);
    await fixture.whenStable();
    expect(connection.last('SetSoundVolume')).toEqual([-100]);
    expect(page.querySelector('lm-sound-master .readout')?.textContent?.trim()).toBe('-100 dB');
    fader.dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
    await wait(40);
    expect(connection.last('SetSoundVolume')).toEqual([-20]);

    // what another page did: half way is -12 dB, as the gain is the place squared
    connection.emit('SoundMasterChanged', -12, false);
    await fixture.whenStable();
    expect(fader.getAttribute('aria-valuenow')).toBe('128');
    expect(page.querySelector('lm-sound-master .readout')?.textContent?.trim()).toBe('-12 dB');
    expect(mute.getAttribute('aria-pressed')).toBe('false');
  });

  it('the row of sounds on Main: a key starts and stops its sound, and Copy and Del work on sounds, a sound onto a sound', async () => {
    connection.emit('SoundsChanged', [aSound(2, { name: 'Rain' }), aSound(4, { name: 'Bell' })]);
    TestBed.inject(DeskSettings).setSoundRows(1);
    await show('/memories');
    await show('/');
    await until(() => !!page.querySelector('lm-main-view lm-sound-section'));
    await fixture.whenStable();

    const key = (number: number) =>
      page.querySelector<HTMLButtonElement>(
        `lm-sound-section button[aria-label="Sound ${number} key"]`,
      )!;
    expect(key(2).classList).toContain('tone-sound'); // every sound one color
    expect(key(3).classList).toContain('tone-sound'); // an empty number too, as the page keys
    expect(key(4).classList).toContain('tone-sound');
    await press(key(2));
    expect(connection.last('SetSoundControl')).toEqual([2, 'toggle', 0]);

    // Copy: the sound, then the number for the copy; a memory key says that a sound goes onto a sound
    await press(button('Copy memory'));
    await press(key(2));
    await press(memoryKey(1));
    expect(store.notice()).toBe('Copy puts sound 2 onto another sound: press the key of that one.');
    await press(key(5));
    expect(connection.last('CopySound')).toEqual([2, 5]);
    expect(connection.methods().filter((method) => method === 'SetSoundControl')).toEqual([
      'SetSoundControl',
    ]); // only the first press played it

    // Del: the sound to take out
    await press(button('Delete memory'));
    await press(key(2));
    expect(connection.last('DeleteSound')).toEqual([2]);
    expect(store.sounds().map((sound) => sound.number)).toEqual([4]);
  });

  it('the Sounds view takes a file for an empty number, and its editor changes the sound', async () => {
    await show('/sounds');
    expect(page.querySelector('lm-sounds-view .summary')?.textContent).toContain(
      'Sound 1 is empty',
    );

    // the key of an empty number chooses it
    await press(
      page.querySelector<HTMLButtonElement>('lm-sounds-view button[aria-label="Sound 3 key"]'),
    );
    expect(page.querySelector('lm-sounds-view .summary')?.textContent).toContain(
      'Sound 3 is empty',
    );
    expect(page.querySelector('lm-sounds-view .make p')?.textContent).toContain(
      'Add a file to sound 3',
    );
    expect(connection.methods()).not.toContain('SetSoundControl');

    // Copy and Del of the view, and MUTE with the volume
    const record = [...page.querySelectorAll('lm-sounds-view lm-record-section button')].map(
      (item) => item.getAttribute('aria-label'),
    );
    expect(record).toEqual(['Copy sound', 'Delete sound']);
    expect(
      page.querySelector('lm-sounds-view lm-sound-master button[aria-label="Mute"]'),
    ).toBeTruthy();
    // a page that is not on the PC is told where the device is chosen
    expect(page.querySelector('lm-sound-output')).toBeNull(); // the device is set up in Settings

    // a file goes to the desk program, and becomes sound 3
    const sent: string[] = [];
    const realFetch = globalThis.fetch;
    globalThis.fetch = (async (url: string) => {
      sent.push(url);
      const info = {
        hash: 'b'.repeat(32),
        name: 'thunder.mp3',
        seconds: 3.5,
        channels: 2,
        rate: 44100,
      };
      return new Response(JSON.stringify(info), { status: 200 });
    }) as typeof fetch;
    try {
      const picker = page.querySelector<HTMLInputElement>('lm-sounds-view input[type="file"]')!;
      Object.defineProperty(picker, 'files', {
        value: [new File(['x'], 'thunder.mp3')],
        configurable: true,
      });
      picker.dispatchEvent(new Event('change'));
      await until(() => store.sounds().length === 1);
      await until(() => sent.includes(`/api/sounds/${'b'.repeat(32)}/wave`));
    } finally {
      globalThis.fetch = realFetch;
    }
    expect(sent.filter((url) => !url.endsWith('/wave'))).toEqual(['/api/sounds?name=thunder.mp3']);
    expect(
      [...page.querySelectorAll('lm-sound-wave .time')].map((item) => item.textContent?.trim()),
    ).toEqual(['0.0 s', '3.5 s']);
    await wait(200);
    expect(connection.last('SetSound')?.[0]).toMatchObject({
      number: 3,
      name: 'thunder',
      file: 'thunder.mp3',
      hash: 'b'.repeat(32),
      decibels: 0,
    });
    expect(connection.last('SetSound')?.[0]).not.toHaveProperty('category');
    expect(page.querySelector('lm-sounds-view .summary')?.textContent).toContain(
      'Sound 3, thunder: thunder.mp3, 3.5 s, quiet.',
    );

    // the editor: a number within its range is taken, one out of it put back
    const level = page.querySelector<HTMLInputElement>('#sound-level')!;
    level.value = '-12.34';
    level.dispatchEvent(new Event('change'));
    await wait(200);
    expect((connection.last('SetSound')?.[0] as Sound).decibels).toBe(-12.3);
    level.value = '3'; // over 0 dB: put back
    level.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(level.value).toBe('-12.3');

    expect(page.querySelector('#sound-category')).toBeNull(); // no kinds: every sound is a sound
    expect(
      page.querySelector('lm-sounds-view button[aria-label="Sound 3 key"]')?.classList,
    ).toContain('tone-sound');

    // Copy of the view: the sound, then an empty number, which the editor shows then
    await press(button('Copy sound'));
    await press(
      page.querySelector<HTMLButtonElement>('lm-sounds-view button[aria-label="Sound 3 key"]'),
    );
    await press(
      page.querySelector<HTMLButtonElement>('lm-sounds-view button[aria-label="Sound 7 key"]'),
    );
    expect(connection.last('CopySound')).toEqual([3, 7]);
    expect(store.chosenSound()).toBe(7);
  });

  it('the sliders of the editor work while they move; speed, pitch, echo and reverb are sliders too', async () => {
    connection.emit('SoundsChanged', [aSound(1, { name: 'Rain' })]);
    await show('/sounds');
    await until(() => !!page.querySelector('lm-sound-editor'));
    // the lying faders of the desk, as on the Effects view: keys, the wheel and a drag move them
    const slider = (label: string) =>
      page.querySelector<HTMLElement>(`lm-sound-editor lm-fader[aria-label^="${label}"]`)!;
    const keyOn = (label: string, name: string) =>
      slider(label).dispatchEvent(
        new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }),
      );
    const pressOn = async (label: string, name: string, times = 1) => {
      for (let time = 0; time < times; time++) keyOn(label, name);
      await wait(100);
    };
    const turn = async (label: string, notches: number) => {
      for (let notch = 0; notch < Math.abs(notches); notch++)
        slider(label).dispatchEvent(
          new WheelEvent('wheel', {
            deltaY: notches > 0 ? -100 : 100,
            bubbles: true,
            cancelable: true,
          }),
        );
      await wait(100);
    };
    expect(page.querySelector('lm-sound-editor input[type="range"]')).toBeNull(); // no slider of the browser's own
    const last = () => connection.last('SetSound')?.[0] as Sound;
    const sends = () => connection.methods().filter((method) => method === 'SetSound').length;

    // the first move goes out at once, the newest of the next ones a moment later: a sound that plays follows
    // the level: 255 places, the gain as the place squared (254 is -0.1 dB, 250 -0.3 dB)
    expect(slider('Level').getAttribute('aria-valuenow')).toBe('255'); // 0 dB, its file as it is
    for (let time = 0; time < 5; time++) keyOn('Level', 'ArrowLeft');
    expect(sends()).toBe(1);
    expect(last().decibels).toBe(-0.1);
    await until(() => sends() === 2); // a moment later (SOUND_SEND_MS)
    expect(last().decibels).toBe(-0.3);
    // What the desk program says of a change before does not move the fader back. It comes while the
    // newest is sent: right after it, not after a time a slow machine may overrun.
    connection.emit('SoundsChanged', [aSound(1, { name: 'Rain', decibels: -0.1 })]);
    await fixture.whenStable();
    expect(store.soundsByNumber().get(1)?.decibels).toBe(-0.3);
    expect(page.querySelector<HTMLInputElement>('#sound-level')?.value).toBe('-0.3');
    expect(slider('Level').getAttribute('aria-valuetext')).toBe('-0.3 decibels');
    // the wheel: a notch down is 5 places
    await turn('Level', -1);
    expect(last().decibels).toBe(-0.6);

    // speed: half as fast at the left, 1x in the middle, twice as fast at the right
    await pressOn('Speed', 'End');
    expect(last().speed).toBe(2);
    await pressOn('Speed', 'Home');
    expect(last().speed).toBe(0.5);
    slider('Speed').dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
    await wait(100);
    expect(last().speed).toBe(1);
    // pitch: a key or a notch of the wheel is a semitone
    await pressOn('Pitch', 'Home');
    expect(last().pitch).toBe(-12);
    await turn('Pitch', 7);
    expect(last().pitch).toBe(-5);
    await turn('Echo', 8);
    expect(last().echo).toBe(40);
    expect(
      page.querySelector('lm-sound-editor input[aria-label^="Seconds between the repeats"]'),
    ).toBeTruthy();
    await turn('Reverb', 6);
    expect(last().reverb).toBe(30);
    expect(page.querySelector('#sound-room')).toBeTruthy();
    await pressOn('Pan', 'ArrowLeft', 3);
    expect(last().pan).toBe(-3);

    // Loop is a box, without a sentence
    expect(page.querySelector('label[for="sound-room"]')?.textContent?.trim()).toBe(
      'Room size (%)',
    ); // of the reverb
    expect(page.querySelector('label[for="sound-loop"]')?.textContent?.trim()).toBe('Loop');
    expect(page.querySelector('lm-sound-editor')?.textContent).not.toContain('After its end');
  });

  it('the wave of the Sounds view: where the sound is on the left, how long its file is on the right', async () => {
    connection.emit('SoundsChanged', [aSound(1, { name: 'Rain', start: 2.5 })]);
    await show('/sounds');
    await until(() => !!page.querySelector('lm-sound-wave'));
    const times = () =>
      [...page.querySelectorAll('lm-sound-wave .time')].map((item) => item.textContent?.trim());
    expect(times()).toEqual(['2.5 s', '12.5 s']); // quiet: where it starts
    connection.emit('SoundPlaybackChanged', {
      ...quietPlayback(1),
      playing: true,
      level: 255,
      position: 7.25,
    });
    await fixture.whenStable();
    expect(times()).toEqual(['7.3 s', '12.5 s']);
    expect(page.querySelector('lm-sound-wave')?.getAttribute('aria-label')).toBe(
      'Sound 1 plays, at 7.3 s of 12.5 s',
    );
  });

  it('a step of a sequence plays a sound, and Continue is a box of the steps of sounds only', async () => {
    connection.emit('SoundsChanged', [aSound(2, { name: 'Rain' })]);
    await show('/sequences');
    await press(button('Work on memory 3'));
    const row = (index: number) =>
      page.querySelectorAll('lm-sequences-view table.steps tbody tr')[index];
    expect(row(0).querySelector('td.continues input')).toBeNull(); // a channel plays on nothing

    const kind = row(0).querySelector<HTMLSelectElement>(
      'select[aria-label="Step 1, channel, memory, fixture, group, effect, sound or release"]',
    )!;
    kind.value = 'sound';
    kind.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    const sent = () => (connection.last('SetSequence')?.[1] as Sequence).steps[0];
    expect(sent()).toMatchObject({ target: 'sound', number: 5, value: 200 }); // its number stays where it can
    expect(row(0).querySelector('td.note')?.textContent?.trim()).toBe('No such sound yet');
    const number = row(0).querySelector<HTMLSelectElement>(
      'select[aria-label="Step 1, number of the sound"]',
    )!;
    number.dispatchEvent(new Event('focus'));
    await fixture.whenStable();
    expect([...number.options].slice(0, 2).map((option) => option.text.trim())).toEqual([
      '1',
      '2: Rain',
    ]);
    number.value = '2';
    number.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(row(0).querySelector('td.note')?.textContent?.trim()).toBe('Rain');

    // Continue: it plays on through the steps after it
    await press(
      row(0).querySelector<HTMLInputElement>(
        'input[aria-label="Step 1 plays its sound on through the steps after it"]',
      ),
    );
    expect(sent()).toMatchObject({ target: 'sound', number: 2, continue: true });

    // another kind leaves Continue behind
    kind.value = 'memory';
    kind.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(sent().target).toBe('memory');
    expect(sent().continue).toBeUndefined();
  });

  it('a step of the stack plays a sound, with Continue, and its card says that it plays on', async () => {
    connection.emit('SoundsChanged', [aSound(2, { name: 'Rain' })]);
    connection.emit('StackChanged', {
      steps: [
        { id: 11, cue: '1.0', ...step(2, 255, { target: 'sound', wait: 0 }), continue: true },
        { id: 12, cue: '2.0', ...step(2, 128, { target: 'memory', wait: 3 }) },
      ],
    });
    await show('/stack');
    const headers = [...page.querySelectorAll('lm-stack-view thead th')].map((header) =>
      header.textContent?.trim(),
    );
    expect(headers.slice(1, 5)).toEqual(['Cue', 'Add', 'Continue', 'Kind']);
    const box = page.querySelector<HTMLInputElement>(
      'input[aria-label="Cue 1.0 plays its sound on through the cues after it"]',
    )!;
    expect(box.checked).toBe(true);
    expect(
      page.querySelector(
        'input[aria-label="Cue 2.0 plays its sound on through the cues after it"]',
      ),
    ).toBeNull();
    expect(
      page
        .querySelectorAll('lm-stack-view tbody tr')[0]
        .querySelector('td.note')
        ?.textContent?.trim(),
    ).toBe('Rain');

    await press(box);
    expect(connection.last('UpdateStackStep')).toEqual([
      11,
      { target: 'sound', number: 2, value: 255, up: 1, wait: 0, down: 1 },
    ]);
    await press(box);
    expect(connection.last('UpdateStackStep')).toEqual([
      11,
      { target: 'sound', number: 2, value: 255, up: 1, wait: 0, down: 1, continue: true },
    ]);

    await stackStands({
      currentId: 11,
      nextId: 12,
      state: 'holding',
      paused: false,
      remaining: 0,
      live: true,
    });
    expect(page.querySelector('lm-stack-cards .card.current .what')?.textContent).toContain(
      'Sound 2 (Rain), plays on',
    );
  });

  it('Settings sets the device of the sound, its outputs and the device to listen on, on the PC', async () => {
    connection.soundOutput = {
      devices: [
        { id: 'd1', name: 'Speakers', channels: 2 },
        { id: 'd2', name: 'Line out', channels: 4 },
      ],
      device: '',
      outputs: 1,
      state: 'playing',
      name: 'Line out',
      channels: 4,
      listen: false,
      listenDevice: '',
      listenState: '',
      listenName: '',
      mixer: '',
      playlist: '',
    };
    await show('/settings');
    await until(() => !!page.querySelector('#sound-device'));
    const device = page.querySelector<HTMLSelectElement>('#sound-device')!;
    expect([...device.options].map((option) => option.textContent?.trim())).toEqual([
      "Windows' default device",
      'Speakers (2)',
      'Line out (4)',
    ]);
    expect(page.querySelector('lm-sound-output .state')?.textContent).toContain(
      'Plays on Line out, 4 outputs.',
    );

    // four outputs: two pairs, and the sounds on one of them
    expect(page.querySelector('#sound-music, #sound-effects')).toBeNull();
    const outputs = page.querySelector<HTMLSelectElement>('#sound-outputs')!;
    expect([...outputs.options].map((option) => option.textContent?.trim())).toEqual([
      '1 and 2 (of 4)',
      '3 and 4 (of 4)',
    ]);
    outputs.value = '2';
    outputs.dispatchEvent(new Event('change'));
    await until(() => connection.last('SetSoundOutput') !== undefined);
    expect(connection.last('SetSoundOutput')).toEqual([
      { device: '', outputs: 2, listen: false, listenDevice: '', mixer: '', playlist: '' },
    ]);

    device.value = 'd1';
    device.dispatchEvent(new Event('change'));
    await until(() => (connection.last('SetSoundOutput')?.[0] as SoundSetup).device === 'd1');
    expect(connection.last('SetSoundOutput')).toEqual([
      { device: 'd1', outputs: 2, listen: false, listenDevice: '', mixer: '', playlist: '' },
    ]);
    // a device of two outputs has only 1 and 2: no field for them
    connection.emit('SoundOutputChanged', { ...connection.soundOutput!, outputs: 1, channels: 2 });
    await fixture.whenStable();
    expect(page.querySelector('#sound-outputs')).toBeNull();

    // Listen on: the device of the PC that plays what goes out as well, while LISTEN is lit (no box here)
    expect(page.querySelector('#sound-listen')).toBeNull();
    const listenOn = page.querySelector<HTMLSelectElement>('#sound-listen-device')!;
    expect([...listenOn.options].map((option) => option.textContent?.trim())).toEqual([
      "Windows' default device",
      'Speakers',
      'Line out',
    ]);
    listenOn.value = 'd2';
    listenOn.dispatchEvent(new Event('change'));
    await until(() => (connection.last('SetSoundOutput')?.[0] as SoundSetup).listenDevice === 'd2');
    connection.emit('SoundOutputChanged', {
      ...connection.soundOutput!,
      listen: true,
      listenState: 'playing',
      listenName: 'Line out',
    });
    await fixture.whenStable();
    expect(
      [...page.querySelectorAll('lm-sound-output .state')].map((line) => line.textContent?.trim()),
    ).toEqual(['Plays on Line out, 4 outputs.', 'Listening on Line out.']);
    connection.emit('SoundOutputChanged', {
      ...connection.soundOutput!,
      listen: true,
      listenState: 'same',
      listenName: '',
    });
    await fixture.whenStable();
    expect(page.querySelectorAll('lm-sound-output .state')[1]?.textContent).toContain(
      'it plays there already',
    );
  });

  it('LISTEN in the sound section switches listening on and off, from any page', async () => {
    await show('/sounds');
    const listen = page.querySelector<HTMLButtonElement>(
      'lm-sound-master button[aria-label="Listen"]',
    )!;
    expect(listen.getAttribute('aria-pressed')).toBe('false');
    await press(listen);
    expect(connection.last('SetSoundListen')).toEqual([true]);
    expect(listen.getAttribute('aria-pressed')).toBe('true');
    connection.emit('SoundListenChanged', { listen: false, state: '' }); // another page took it back
    await fixture.whenStable();
    expect(listen.getAttribute('aria-pressed')).toBe('false');
    expect(actionWithId('sound.listen')).toBeTruthy();
    expect(page.querySelector('lm-sounds-view lm-sound-output')).toBeNull(); // the device is in Settings
  });

  it('the Sound part of Settings finds a Soundcraft mixer by its address or by Find, and its playlist; there is no player mode', async () => {
    connection.soundOutput = {
      devices: [],
      device: '',
      outputs: 1,
      state: 'playing',
      name: 'Speakers',
      channels: 2,
      listen: false,
      listenDevice: '',
      listenState: '',
      listenName: '',
      mixer: '',
      playlist: '',
      mixerState: '',
      mixerName: '',
    };
    connection.mixerPlaylists = ['Show', '~all~'];
    await show('/settings');
    await until(() => !!page.querySelector('#sound-mixer'));
    expect(page.querySelector('#sound-player')).toBeNull(); // no player mode: each sound plays where it belongs
    expect(page.querySelector('#sound-outputs')).toBeNull(); // two outputs: nothing to choose
    const lines = () =>
      [...page.querySelectorAll('lm-sound-output .state')].map((line) => line.textContent?.trim());

    // Find: none in the network, then several to choose from, then one, which is taken at once
    await press(buttonIn('lm-sound-output', 'Find'));
    await until(() => lines().some((line) => line?.startsWith('No Soundcraft Ui mixer answered')));
    connection.mixersFound = [
      { address: '10.10.1.1', name: 'ui16, firmware 1.0.7548' },
      { address: '192.168.1.40', name: 'ui24' },
    ];
    await press(buttonIn('lm-sound-output', 'Find'));
    await until(() => !!buttonIn('lm-sound-output', 'ui24 at 192.168.1.40'));
    expect(lines()).toContain('Several mixers answered: choose one.');
    await press(buttonIn('lm-sound-output', 'ui24 at 192.168.1.40'));
    await until(
      () =>
        (connection.last('SetSoundOutput')?.[0] as SoundSetup | undefined)?.mixer ===
        '192.168.1.40',
    );
    connection.mixersFound = [{ address: '10.10.1.1', name: 'ui16, firmware 1.0.7548' }];
    await press(buttonIn('lm-sound-output', 'Find'));
    await until(
      () =>
        (connection.last('SetSoundOutput')?.[0] as SoundSetup | undefined)?.mixer === '10.10.1.1',
    );
    expect(page.querySelector<HTMLInputElement>('#sound-mixer')?.value).toBe('10.10.1.1');

    const address = page.querySelector<HTMLInputElement>('#sound-mixer')!;
    address.value = ' 10.10.1.1 ';
    address.dispatchEvent(new Event('change'));

    connection.emit('SoundOutputChanged', { ...connection.soundOutput!, mixerState: 'connecting' });
    await fixture.whenStable();
    expect(lines()).toContain(
      'Looking for the mixer at 10.10.1.1: no track plays until it answers.',
    );
    expect(page.querySelector('lm-sound-output .hint')).toBeNull(); // not before it answers

    connection.soundOutput = {
      ...connection.soundOutput!,
      mixerState: 'connected',
      mixerName: 'ui16, firmware 1.0.7548',
    };
    connection.emit('SoundOutputChanged', connection.soundOutput);
    await fixture.whenStable();
    expect(lines()).toContain('The mixer answers: ui16, firmware 1.0.7548.');
    expect(page.querySelector('lm-sound-output .hint')?.textContent).toContain('one at a time');

    // the playlist: every track at first, and all the tracks of one as sounds at once
    await until(() => !!page.querySelector('#sound-playlist option[value="Show"]'));
    const playlist = page.querySelector<HTMLSelectElement>('#sound-playlist')!;
    expect([...playlist.options].map((option) => option.textContent?.trim())).toEqual([
      'All tracks',
      'Show',
    ]);
    playlist.value = 'Show';
    playlist.dispatchEvent(new Event('change'));
    await until(() => (connection.last('SetSoundOutput')?.[0] as SoundSetup).playlist === 'Show');
    await fixture.whenStable();
    await press(buttonIn('lm-sound-output', 'Add all its tracks'));
    expect(connection.last('AddMixerTracks')).toEqual(['Show']);
  });

  it('while the mixer answers a step offers the tracks of the playlist, and a track picked there becomes a sound', async () => {
    connection.mixerTracks = { Show: ['Overture.mp3', 'Finale.mp3'] };
    await show('/stack');
    connection.emit('StackChanged', {
      steps: [
        { id: 11, cue: '1.0', target: 'sound', number: 5, value: 255, up: 0, wait: 0, down: 0 },
      ],
    });
    connection.emit('MixerChanged', { state: 'connected', name: 'ui16', playlist: 'Show' });
    await until(() => connection.last('GetMixerTracks')?.[0] === 'Show');
    await fixture.whenStable();
    const number = page.querySelector<HTMLSelectElement>('lm-stack-view td.number select')!;
    number.dispatchEvent(new Event('focus'));
    await fixture.whenStable();
    expect([...number.options].map((option) => option.textContent?.trim())).toEqual(
      expect.arrayContaining(['Overture.mp3 (new sound)', 'Finale.mp3 (new sound)']),
    );
    number.value = [...number.options].find((option) =>
      option.textContent?.includes('Finale'),
    )!.value;
    number.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetSound')?.[0]).toMatchObject({
      number: 1,
      name: 'Finale',
      file: 'Finale.mp3',
      playlist: 'Show',
    });
    expect(connection.last('UpdateStackStep')?.[1]).toMatchObject({ target: 'sound', number: 1 });
  });

  it('while the mixer answers, an empty sound takes a track of its USB stick, edited as a track', async () => {
    connection.mixerPlaylists = ['Show', '~all~', '~root~'];
    connection.mixerTracks = {
      '~all~': ['Intro.mp3', 'Cafe\u0301.mp3', 'Outro.mp3'],
      Show: ['Intro.mp3', 'Cafe\u0301.mp3'],
    };
    await show('/sounds');
    expect(page.querySelector('lm-sounds-view .track')).toBeNull(); // no mixer: nothing of it
    connection.emit('MixerChanged', { state: 'connected', name: 'ui16' });
    await until(
      () => page.querySelectorAll('select[aria-label="Track of the mixer"] option').length === 4,
    );
    expect(page.querySelector('lm-sounds-view .track p')?.textContent).toContain('All tracks'); // no playlist chosen: every track
    expect(page.querySelector('select[aria-label="Playlist of the mixer"]')).toBeNull(); // never one of its own

    connection.emit('MixerChanged', { state: 'connected', name: 'ui16', playlist: 'Show' });
    await until(
      () => page.querySelectorAll('select[aria-label="Track of the mixer"] option').length === 3,
    );
    expect(page.querySelector('lm-sounds-view .track p')?.textContent).toContain('Show');
    const tracks = page.querySelector<HTMLSelectElement>(
      'select[aria-label="Track of the mixer"]',
    )!;
    expect(tracks.options[2].textContent?.trim()).toBe('Caf\u00e9.mp3'); // as letters are usually written
    tracks.value = 'Cafe\u0301.mp3';
    tracks.dispatchEvent(new Event('change'));
    // sent back to the mixer exactly as it gave it
    expect(connection.last('SetSound')?.[0]).toMatchObject({
      number: 1,
      name: 'Caf\u00e9',
      file: 'Cafe\u0301.mp3',
      hash: '',
      playlist: 'Show',
      decibels: 0, // as any sound: the volume of all sounds keeps it down
    });

    // the editor of a track: its name, level and fades, and the track itself
    connection.emit('SoundsChanged', [newTrack(1, 'Show', 'Cafe\u0301.mp3')]);
    await fixture.whenStable();
    expect(page.querySelector('lm-sound-editor .file')?.textContent?.trim()).toBe(
      'Show / Caf\u00e9.mp3',
    );
    expect(page.querySelector('lm-sound-editor #sound-fade-out')).toBeTruthy();
    expect(page.querySelector<HTMLInputElement>('lm-sound-editor #sound-level')?.value).toBe('0'); // PLAYER L/R go there, under the volume
    expect(page.querySelector('lm-sound-editor #sound-pan')).toBeNull();
    expect(page.querySelector('lm-sound-editor #sound-loop')).toBeNull();
    expect(page.querySelector('lm-sounds-view lm-sound-wave')).toBeTruthy(); // empty, with its line
    expect(page.querySelector('lm-sounds-view .summary')?.textContent).toContain(
      'a track of the mixer (Show / Caf\u00e9.mp3)',
    );

    connection.emit('SoundPlaybackChanged', {
      ...quietPlayback(1),
      playing: true,
      level: 255,
      position: 65,
      length: 200,
    });
    await fixture.whenStable();
    expect(page.querySelector('lm-sounds-view .summary')?.textContent).toContain(
      'playing, 1:05.0 of 3:20.0',
    );
    expect(page.querySelector('lm-sounds-view lm-sound-wave .total')?.textContent?.trim()).toBe(
      '3:20.0',
    );
  });

  it('a double click on the wave of a sound that plays plays it from there, and so do the keys on it', async () => {
    await show('/sounds');
    connection.emit('SoundsChanged', [
      newSound(1, {
        hash: 'ab'.repeat(16),
        name: 'rain.wav',
        seconds: 100,
        channels: 2,
        rate: 48000,
      }),
    ]);
    await until(() => !!page.querySelector('lm-sounds-view lm-sound-wave .well'));
    const well = page.querySelector<HTMLElement>('lm-sounds-view lm-sound-wave .well')!;
    expect(well.getAttribute('role')).toBeNull(); // quiet: nothing to move
    connection.emit('SoundPlaybackChanged', {
      ...quietPlayback(1),
      playing: true,
      level: 255,
      position: 10,
    });
    await fixture.whenStable();
    expect(well.getAttribute('role')).toBe('slider');

    well.getBoundingClientRect = () => ({
      left: 0,
      width: 200,
      top: 0,
      height: 56,
      right: 200,
      bottom: 56,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    });
    well.dispatchEvent(new MouseEvent('dblclick', { clientX: 50, bubbles: true }));
    expect(connection.last('SeekSound')).toEqual([1, 25]); // a quarter of 100 s

    well.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    expect(connection.last('SeekSound')).toEqual([1, 15]);
    well.dispatchEvent(new KeyboardEvent('keydown', { key: 'Home', bubbles: true }));
    expect(connection.last('SeekSound')).toEqual([1, 0]);
  });

  it('once a sound played, the Stack view shows its wave over the steps', async () => {
    await show('/stack');
    connection.emit('SoundsChanged', [
      {
        ...newSound(2, {
          hash: 'cd'.repeat(16),
          name: 'Overture.mp3',
          seconds: 90,
          channels: 2,
          rate: 48000,
        }),
        category: 'music',
      },
    ]);
    await fixture.whenStable();
    expect(page.querySelector('lm-stack-view lm-sound-now')).toBeNull();
    connection.emit('SoundPlaybackChanged', {
      ...quietPlayback(2),
      playing: true,
      level: 255,
      position: 3,
    });
    await fixture.whenStable();
    expect(page.querySelector('lm-stack-view lm-sound-now .name')?.textContent?.trim()).toBe(
      'Sound 2: Overture',
    );
    expect(page.querySelector('lm-stack-view')?.classList).toContain('sounding');
  });

  it('Settings shows sound keys in the independents, which start and stop their sounds', async () => {
    TestBed.inject(DeskSettings).setSoundKeys(4);
    await show('/memories');
    await show('/');
    connection.emit('SoundsChanged', [
      newSound(3, {
        hash: 'ef'.repeat(16),
        name: 'Thunder.wav',
        seconds: 5,
        channels: 2,
        rate: 48000,
      }),
    ]);
    await until(
      () => !!page.querySelector('button[aria-label="Independent 23, sound 3, Thunder"]'),
    );
    await press(
      page.querySelector<HTMLButtonElement>(
        'button[aria-label="Independent 23, sound 3, Thunder"]',
      ),
    );
    expect(connection.last('SetSoundControl')).toEqual([3, 'toggle', 0]);
    expect(actionWithId('ind.sound.32')).toBeTruthy();

    // Edit binds a sound key to another sound
    await press(button('Edit the channels, memories, effects and sounds of the independent keys'));
    const field = page.querySelector<HTMLInputElement>(
      'input[aria-label="Sound of independent key 21"]',
    )!;
    field.value = '12';
    field.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetSoundIndependent')).toEqual([1, 12]);
  });

  it('the steps offer files and tracks of the mixer alike, and while it answers its tracks that are no sounds yet', () => {
    const sounds = [
      { number: 1, name: 'Rain' },
      { number: 2, name: 'Intro', playlist: 'Show', file: 'Intro.mp3' },
    ];
    expect(soundChoices(sounds, 3).map((choice) => choice.text)).toEqual([
      '1: Rain',
      '2: Intro',
      '3',
    ]);
    expect(
      soundChoices(sounds, 3, ['Intro.mp3', 'Outro.mp3'], 'Show').map((choice) => choice.text),
    ).toEqual(['1: Rain', '2: Intro', '3', 'Outro.mp3 (new sound)']);
  });

  it('MUTE is on the Stack and Stage views too, and every sound key, page and the Sounds view has a shortcut', async () => {
    await show('/stack');
    // in the sound section, lying beside the master section over the steps
    expect(
      page.querySelector('lm-stack-view .levels > lm-master-section.lying + lm-sound-master.lying'),
    ).toBeTruthy();
    expect(page.querySelector('lm-stack-keys button[aria-label="Mute"]')).toBeNull();
    await press(
      page.querySelector<HTMLButtonElement>(
        'lm-stack-view lm-sound-master button[aria-label="Mute"]',
      ),
    );
    expect(connection.last('SetSoundMute')).toEqual([true]);
    for (const id of [
      'view.sounds',
      'sound.1',
      'sound.64',
      'sound.mute',
      'page.sound.2',
      'page.sound.next',
    ]) {
      expect(actionWithId(id)).toBeTruthy();
    }
    expect(ACTION_GROUPS).toContain('Sound keys');
  });

  // ---------------------------------------------------------------- MIDI controllers
});
