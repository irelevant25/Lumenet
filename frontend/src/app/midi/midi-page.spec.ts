import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { App } from '../app';
import { DeskStore } from '../desk/desk-store';
import { IDLE } from '../desk/function-mode';
import { Keyboard } from '../desk/keyboard';
import { type MidiLamp, type MidiMapping, type MidiState } from './midi';
import {
  deskPageHelpers,
  FakeConnection,
  LIVE,
  MIDI,
  startDeskPage,
} from '../testing/desk-page.testing';

describe('the desk page: MIDI controllers', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;
  const { show, button, buttonIn, press, stackStands, pick, check, until, textOf } =
    deskPageHelpers(() => ({ connection, fixture, page, store }));

  beforeEach(async () => {
    ({ connection, fixture, page, store } = await startDeskPage());
  });

  /** The desk program knows MIDI: it says what the PC has, as after a change. */
  async function withMidi(state: Partial<MidiState> = {}): Promise<void> {
    connection.midi = { ...MIDI, ...state };
    connection.emit('MidiChanged', connection.midi);
    await fixture.whenStable();
  }

  /** A key of a controller, pressed or let go, as the desk program sends it to the page that presses the keys. */
  async function midiKey(key: string, down: boolean): Promise<void> {
    connection.emit('MidiKey', key, down);
    await fixture.whenStable();
  }

  it('a key of a MIDI controller presses the key of the desk, as its shortcut would', async () => {
    await midiKey('master.clear', true);
    await midiKey('master.clear', false);
    expect(connection.methods().filter((method) => method === 'Clear')).toEqual(['Clear']);

    await midiKey('view.stage', true);
    expect(TestBed.inject(Router).url).toBe('/stage');

    // a key of the pages shown presses the key under that fader of the page shown
    await midiKey('slot.memory.2', true);
    await midiKey('slot.memory.2', false);
    expect(store.memoryLevels()[1]).toBe(255);
    store.showChannelPage(store.channelsPerPage() + 1);
    await midiKey('slot.channel.5', true);
    expect(store.channels()[store.channelsPerPage() + 4]).toBe(255);
    expect(store.channels()[4]).toBe(0);

    // the next and the one before of the page keys
    await midiKey('page.channel.previous', true);
    expect(store.firstChannel()).toBe(1);
    await midiKey('page.channel.previous', true);
    expect(store.firstChannel()).toBe(1); // there is none before the first
    await midiKey('page.memory.next', true);
    expect(store.firstMemory()).toBe(store.memoriesPerPage() + 1);
  });

  it('while a solo is lit, a key of a controller held down holds its fader until it is let go', async () => {
    await midiKey('master.solo', true);
    await midiKey('master.solo', false);
    expect(store.soloMode()).toBe('up');

    await midiKey('slot.memory.2', true);
    expect(connection.last('HoldSolo')).toEqual([true, 'memory', 2, true]);
    expect(store.memoryLevels()[1]).toBe(255);

    // the page moves on meanwhile: the key let go is still that of memory 2
    store.showMemoryPage(store.memoriesPerPage() + 1);
    await midiKey('slot.memory.2', false);
    expect(connection.last('HoldSolo')).toEqual([true, 'memory', 2, false]);
    expect(store.memoryLevels()[1]).toBe(0);
  });

  it('a locked key stays locked for a controller, and the keys rest while a key is being picked', async () => {
    const keyboard = TestBed.inject(Keyboard);
    keyboard.listen('midi');
    await fixture.whenStable();
    await midiKey('master.clear', true);
    expect(connection.methods()).not.toContain('Clear');
    keyboard.stopListening();
    await fixture.whenStable();

    // while the stack is live, the Record keys are locked: Save mem does nothing
    await stackStands(LIVE);
    await midiKey('record.save', true);
    expect(store.mode()).toEqual(IDLE);
  });

  it('the page that presses the keys tells the desk program how they are lit, and which pages it shows', async () => {
    const lamps = async (): Promise<MidiLamp[]> => {
      await fixture.whenStable();
      await new Promise((resolve) => setTimeout(resolve, 60));
      return (connection.last('ReportMidiLamps')?.[0] as MidiLamp[] | undefined) ?? [];
    };
    connection.emit('MidiRole', {
      controls: true,
      keys: ['master.clear', 'master.blackout', 'memory.2', 'slot.memory.2', 'record.save'],
    });
    const first = await lamps();
    expect(first).toContainEqual({ key: 'master.clear', level: 3, blink: false });
    expect(first).toContainEqual({ key: 'master.blackout', level: 1, blink: false });
    expect(first).toContainEqual({ key: 'memory.2', level: 2, blink: false }); // it holds a look, its fader is down
    expect(first).toContainEqual({ key: 'slot.memory.2', level: 2, blink: false });
    expect(connection.last('ReportMidiPages')).toEqual([
      {
        firstChannel: 1,
        channelsPerPage: store.channelsPerPage(),
        firstMemory: 1,
        memoriesPerPage: store.memoriesPerPage(),
        firstEffect: 1,
        effectsPerPage: store.stripsPerRow(),
      },
    ]);

    // Save mem waits for a memory: it is lit, the memory keys blink; only what changed goes again
    store.pressFunction('save');
    const changed = await lamps();
    expect(changed).toContainEqual({ key: 'record.save', level: 3, blink: false });
    expect(changed).toContainEqual({ key: 'memory.2', level: 2, blink: true });
    expect(changed.map((lamp) => lamp.key)).not.toContain('master.clear');

    // another page is used now: this one tells nothing more
    connection.emit('MidiRole', { controls: false, keys: [] });
    const told = connection.calls.length;
    store.pressFunction('save');
    await lamps();
    expect(connection.calls.slice(told).map((call) => call.method)).not.toContain(
      'ReportMidiLamps',
    );
  });

  it('the MIDI view lists the devices, the messages that came in, and every key and fader with its controls', async () => {
    await show('/midi');
    const panel = page.querySelector<HTMLElement>('lm-midi-view')!;
    expect(panel.textContent).toContain(
      'The desk program is older than this page and knows no MIDI.',
    );

    await withMidi();
    expect(connection.last('WatchMidi')).toEqual([true]);
    expect(panel.querySelector('lm-midi-devices')?.textContent).toContain('APC mini mk2');
    expect(panel.querySelector('lm-midi-devices')?.textContent).toContain('Not plugged in');
    expect(panel.querySelector('#midi-preset-device')).toBeTruthy();

    // a message came in: the list shows it, with what it did
    connection.emit('MidiMessages', [
      {
        at: Date.now(),
        device: 'APC mini mk2',
        message: 'Note On 56, velocity 127, channel 1',
        target: 'stack.play',
        result: 'down',
        value: null,
        desk: null,
        reason: null,
      },
    ]);
    await fixture.whenStable();
    expect(panel.querySelector('lm-midi-monitor tbody tr')?.textContent).toContain('GO pressed');

    // the kinds of keys and faders: Stack has Play with its control; Learn waits for Pause
    // (Stack live, Back, GO, Pause, Next, Add cue and Learn of the Stack view)
    const head = (name: string) =>
      [...panel.querySelectorAll<HTMLButtonElement>('lm-midi-targets .head')].find(
        (found) => found.querySelector('.name')?.textContent === name,
      )!;
    const row = (id: string) => panel.querySelector<HTMLElement>(`tr[data-row="${id}"]`)!;
    expect(head('Stack').getAttribute('aria-label')).toBe('Stack: 7 targets, 1 with a control');
    await press(head('Stack'));
    expect(row('stack.play').textContent).toContain('APC mini mk2: Note 56, channel 1');
    await press(row('stack.pause').querySelector<HTMLButtonElement>('button.learn'));
    expect(connection.last('LearnMidi')).toEqual(['stack.pause']);
    expect(row('stack.pause').classList).toContain('learning');
    expect(panel.querySelector('.learning[role="status"]')?.textContent).toContain('Pause');
    // Esc stops it
    panel.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    await fixture.whenStable();
    expect(connection.last('LearnMidi')).toEqual([null]);

    // ✕ takes the controls of Play away
    await press(row('stack.play').querySelector<HTMLButtonElement>('button.delete'));
    expect(
      (connection.last('SetMidiMappings')?.[0] as MidiMapping[]).map((mapping) => mapping.target),
    ).toEqual(['fader.slot.channel.1']);

    // a fader's control has its options: as an endless encoder it takes no pickup
    await press(head('Faders of the pages shown'));
    const options = () => row('fader.slot.channel.1').nextElementSibling as HTMLElement;
    const mode = options().querySelector('select')!;
    mode.value = 'relative-twos';
    mode.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect((connection.last('SetMidiMappings')?.[0] as MidiMapping[])[0]).toMatchObject({
      target: 'fader.slot.channel.1',
      mode: 'relative-twos',
    });
    expect(options().querySelector<HTMLInputElement>('input[type="checkbox"]')!.disabled).toBe(
      true,
    );

    // the search finds a control by what it is
    const search = panel.querySelector<HTMLInputElement>('lm-midi-targets input[type="search"]')!;
    search.value = 'cc 48';
    search.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(
      [...panel.querySelectorAll('lm-midi-targets tr[data-row]')].map((found) =>
        found.getAttribute('data-row'),
      ),
    ).toEqual(['fader.slot.channel.1']);
  });

  it('OSC: the MIDI view switches it on, on a port, and every key and fader shows its address', async () => {
    await show('/midi');
    await withMidi({ osc: { on: false, port: 8000, state: 'off', problem: null } });
    const osc = page.querySelector<HTMLElement>('lm-osc-panel')!;
    const state = () => osc.querySelector('.state')?.textContent?.trim();
    expect(osc.getAttribute('aria-labelledby')).toBe('osc-title');
    expect(state()).toBe('Off.');

    await press(osc.querySelector<HTMLInputElement>('.switch input'));
    expect(connection.last('SetOsc')).toEqual([true, 8000]);
    expect(state()).toBe('Listening on UDP port 8000.');
    expect(osc.querySelector('.hint')?.textContent).toContain(
      'LAN is off: only programs on this PC reach the desk, at 127.0.0.1, port 8000.',
    );

    // another port; a number that is no port is put back as it was
    const port = osc.querySelector<HTMLInputElement>('.port input')!;
    port.value = '9000';
    port.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetOsc')).toEqual([true, 9000]);
    port.value = '70000';
    port.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(port.value).toBe('9000');
    expect(connection.methods().filter((method) => method === 'SetOsc')).toHaveLength(2);

    // with LAN on, other computers send to the address of this PC
    store.lan.set({ local: true, on: true, addresses: ['192.168.7.23:5080'], password: 'x' });
    await fixture.whenStable();
    expect(osc.querySelector('.hint')?.textContent).toContain(
      'Programs on other computers send to 192.168.7.23, port 9000',
    );

    // while OSC listens, each key and fader shows its address, and the search finds it by that
    const row = (id: string) => page.querySelector<HTMLElement>(`lm-midi-targets tr[data-row="${id}"]`);
    const search = page.querySelector<HTMLInputElement>('lm-midi-targets input.search')!;
    search.value = '/lumenet/stack/pa';
    search.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(row('stack.pause')?.querySelector('.osc')?.textContent?.trim()).toBe('/lumenet/stack/pause');
    expect(row('stack.play')).toBeNull();

    // a port another program has: said in red, and no addresses while nothing listens
    await withMidi({
      osc: {
        on: true,
        port: 9000,
        state: 'failed',
        problem: 'UDP port 9000 is taken by another program: choose another port, in both programs.',
      },
    });
    expect(osc.querySelector('.state.failed')?.textContent).toContain('taken by another program');
    expect(row('stack.pause')).toBeNull(); // the search found it by its address only

    // a message of OSC in the list: a key the desk does not have is said so
    connection.emit('MidiMessages', [
      {
        at: Date.now(),
        device: 'OSC from 127.0.0.1',
        message: '/lumenet/stack/ply',
        target: 'stack.ply',
        result: 'down',
        value: null,
        desk: null,
        reason: null,
      },
    ]);
    await fixture.whenStable();
    expect(page.querySelector('lm-midi-monitor tbody tr')?.textContent).toContain(
      'The desk has no key "stack.ply"',
    );
  });

  it('Listen in the MIDI view shows the key or fader double-clicked on the desk, as Listen of the Keyboard panel does', async () => {
    const router = TestBed.inject(Router);
    const twice = async (element: Element) => {
      for (let press = 0; press < 2; press++) {
        element.dispatchEvent(new MouseEvent('pointerdown', { bubbles: true, cancelable: true }));
        (element as HTMLElement).click();
      }
      element.dispatchEvent(new MouseEvent('dblclick', { bubbles: true, cancelable: true }));
      await fixture.whenStable();
    };
    const picked = async (id: string) => {
      await until(() => page.querySelector(`lm-midi-targets tr[data-row="${id}"]`) !== null);
      const row = page.querySelector<HTMLElement>(`lm-midi-targets tr[data-row="${id}"]`)!;
      await until(() => document.activeElement === row.querySelector('button.learn'));
      return row;
    };
    await withMidi();
    await show('/midi');

    // a key: its kind opens, its row is marked and its Learn has the focus; nothing is learned or pressed
    const sent = connection.methods().length;
    await press(button('Listen for a key or a fader of the desk'));
    expect(router.url).toBe('/');
    await twice(page.querySelector('[data-action="master.clear"]')!);
    expect(router.url).toBe('/midi');
    const clear = await picked('master.clear');
    expect(clear.classList).toContain('picked');
    expect(connection.calls.map((call) => call.method)).not.toContain('LearnMidi');
    expect(connection.methods().slice(sent)).toEqual([]); // not Clear, nor anything else

    // a fader: two presses pick it and do not move it; the double click of the rate fader does not
    // put the tempo back to 120 either
    await press(button('Listen for a key or a fader of the desk'));
    await twice(page.querySelector('[data-fader="fader.master"]')!);
    expect(router.url).toBe('/midi');
    expect((await picked('fader.master')).classList).toContain('picked');
    await press(button('Listen for a key or a fader of the desk'));
    await twice(page.querySelector('[data-fader="fader.rate"]')!);
    await picked('fader.rate');

    // faders with a number of two digits, as every other (until 2026-11-25 only 1 to 9 were picked)
    for (const fader of ['fader.channel.12', 'fader.memory.10']) {
      await press(button('Listen for a key or a fader of the desk'));
      await twice(page.querySelector(`[data-fader="${fader}"]`)!);
      expect(router.url, fader).toBe('/midi');
      await picked(fader);
    }
    expect(connection.methods().slice(sent)).toEqual([]);
    expect(connection.calls.map((call) => call.method)).not.toContain('LearnMidi');
  });

  it('Export and Import take the MIDI of the PC along; a phone cannot, and its MIDI panel says so', async () => {
    (globalThis as { showSaveFilePicker?: unknown }).showSaveFilePicker = async (options: {
      suggestedName: string;
    }) => ({
      name: options.suggestedName,
      createWritable: async () => new WritableStream<Uint8Array>(),
    });
    // the desk program packs the text of Export into the ZIP
    const posted: string[] = [];
    const realFetch = globalThis.fetch;
    globalThis.fetch = (async (_url: string, init?: RequestInit) => {
      posted.push(init?.body as string);
      return new Response(new Uint8Array([80, 75, 3, 4]), { status: 200 });
    }) as typeof fetch;
    try {
      await withMidi();
      await press(button('Export'));
      await check('lm-export-dialog', 'MIDI');
      await press(button('Export the file'));
      await until(
        () => posted.length > 0 && page.querySelector('lm-export-dialog .report') !== null,
      );
      const file = JSON.parse(posted[0]);
      expect(file.midi).toEqual({
        devices: ['APC mini mk2', 'nanoKONTROL2'],
        mappings: MIDI.mappings,
      });
      await press(buttonIn('lm-export-dialog', 'OK'));

      // the same text as a file of its own, as Export wrote it before
      await press(button('Import'));
      await pick(new File([posted[0]], 'Lumenet - midi.json'));
      await until(() => page.querySelector('lm-import-dialog .parts') !== null);
      await check('lm-import-dialog', 'MIDI');
      await press(button('Import the file'));
      await until(() => page.querySelector('lm-import-dialog .report') !== null);
      expect(connection.last('SetMidiSetup')).toEqual([file.midi]);
      expect(textOf('lm-import-dialog .report')).toContain('The MIDI controllers of this PC');
      await press(buttonIn('lm-import-dialog', 'OK'));
    } finally {
      globalThis.fetch = realFetch;
      delete (globalThis as { showSaveFilePicker?: unknown }).showSaveFilePicker;
    }

    // a phone
    connection.emit('LanAccessChanged', { ...connection.lan, local: false });
    await fixture.whenStable();
    await press(button('Export'));
    const box = [...page.querySelectorAll('lm-export-dialog .parts label')].find((label) =>
      label.textContent?.trim().startsWith('MIDI'),
    )!;
    expect(box.querySelector('input')!.disabled).toBe(true);
    expect(box.textContent).toContain('only the PC');
    await press(buttonIn('lm-export-dialog', 'Cancel'));
    await show('/midi');
    expect(page.querySelector('lm-midi-view')?.textContent).toContain('only the PC sets them');
    expect(page.querySelector('lm-midi-targets')).toBeNull();
  });
});
