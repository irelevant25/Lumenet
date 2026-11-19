import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';
import { DeskConnection } from './desk/desk-connection';
import { DeskStore } from './desk/desk-store';
import { CHANNEL_COUNT, MEMORY_COUNT, Welcome } from './desk/desk-types';
import { NOTE_LINE_SEPARATOR } from './desk/notes';

/** Stands in for the connection to the backend: answers "Hello" and remembers what the page sent. */
class FakeConnection {
  readonly online = signal(false);
  readonly calls: { method: string; args: unknown[] }[] = [];

  private readonly handlers = new Map<string, (...args: never[]) => void>();
  private readonly connected: (() => void)[] = [];

  readonly welcome: Welcome = {
    version: '0.1.0',
    desk: {
      channels: new Array<number>(CHANNEL_COUNT).fill(0),
      memoryLevels: new Array<number>(MEMORY_COUNT).fill(0),
      memoryStored: new Array<boolean>(MEMORY_COUNT).fill(false),
      channelNotes: new Array<string>(CHANNEL_COUNT).fill(''),
      memoryNotes: new Array<string>(MEMORY_COUNT).fill(''),
      independents: Array.from({ length: 16 }, (_, index) => index + 1),
      master: 255,
      blackout: false,
    },
    box: { connected: false, host: '192.168.7.1', firmware: null },
    project: { name: 'Untitled', path: null, unsaved: false },
  };

  /** What the stored memories hold, as the backend answers "GetMemories". */
  readonly looks: Record<number, number[]> = {};

  start(): void {
    this.online.set(true);
    for (const callback of this.connected) callback();
  }

  whenConnected(callback: () => void): void {
    this.connected.push(callback);
  }

  on(event: string, handler: (...args: never[]) => void): void {
    this.handlers.set(event, handler);
  }

  async invoke(method: string, ...args: unknown[]): Promise<unknown> {
    this.calls.push({ method, args });
    if (method === 'Hello') return this.welcome;
    if (method === 'GetMemories') return this.looks;
    if (method === 'NewProject') return { name: 'Untitled', path: null, unsaved: false };
    return undefined;
  }

  send(method: string, ...args: unknown[]): void {
    this.calls.push({ method, args });
  }

  /** The names of the calls made so far, without the questions the page asks by itself. */
  methods(): string[] {
    return this.calls
      .map((call) => call.method)
      .filter((method) => method !== 'Hello' && method !== 'GetMemories');
  }
}

describe('the desk page', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;

  beforeEach(async () => {
    connection = new FakeConnection();
    connection.welcome.desk.memoryStored[1] = true; // memory 2 holds a look: channel 5 at 200, channel 70 full
    const look = new Array<number>(CHANNEL_COUNT).fill(0);
    look[4] = 200;
    look[69] = 255;
    connection.looks[2] = look;

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes), { provide: DeskConnection, useValue: connection }],
    }).compileComponents();

    fixture = TestBed.createComponent(App);
    page = fixture.nativeElement as HTMLElement;
    store = TestBed.inject(DeskStore);
    await show('/');
  });

  /** Opens a view, as a click on its link in the top bar does. */
  async function show(path: string): Promise<void> {
    await TestBed.inject(Router).navigateByUrl(path);
    await fixture.whenStable();
  }

  function button(label: string): HTMLButtonElement {
    // A key is found by its full name for screen readers, or else by what is printed on it.
    const found = [...page.querySelectorAll('button')].find(
      (candidate) =>
        (candidate.getAttribute('aria-label') ?? candidate.textContent?.trim()) === label,
    );
    if (!found) throw new Error(`no button "${label}"`);
    return found;
  }

  function memoryKey(memory: number): HTMLButtonElement {
    const found = page.querySelector<HTMLButtonElement>(`button[aria-label="Memory ${memory} key"]`);
    if (!found) throw new Error(`no key for memory ${memory}`);
    return found;
  }

  async function press(element: HTMLElement): Promise<void> {
    element.click();
    await fixture.whenStable();
  }

  it('shows the name, 64 channel faders, 32 memory faders and the page buttons', () => {
    expect(page.querySelector('h1')?.textContent).toContain('Lumenet');
    expect(page.querySelectorAll('lm-channel-section lm-fader-strip').length).toBe(64);
    expect(page.querySelectorAll('lm-memory-section lm-fader-strip').length).toBe(32);
    expect(page.querySelectorAll('nav[aria-label="Channel pages"] button').length).toBe(8);
    expect(page.querySelectorAll('nav[aria-label="Memory pages"] button').length).toBe(4);
    expect(button('Channels 1 to 64')).toBeTruthy();
    expect(button('Memories 97 to 128')).toBeTruthy();
  });

  it('moving a fader changes the level at once and tells the backend', async () => {
    const fader = page.querySelector<HTMLElement>('lm-fader[aria-label="Channel 3"]');
    fader?.dispatchEvent(new KeyboardEvent('keydown', { key: 'End' }));
    await fixture.whenStable();

    expect(store.channels()[2]).toBe(255);
    expect(fader?.getAttribute('aria-valuenow')).toBe('255');

    await new Promise((resolve) => setTimeout(resolve, 40)); // fader moves are sent a moment later
    expect(connection.calls).toContainEqual({ method: 'SetChannel', args: [3, 255] });
  });

  it('quick steps on a fader all count, also before the page has drawn the first one', async () => {
    const fader = page.querySelector<HTMLElement>('lm-fader[aria-label="Channel 4"]');
    for (let step = 0; step < 3; step++) {
      fader?.dispatchEvent(new KeyboardEvent('keydown', { key: 'PageUp' })); // 16 each, no waiting between
    }
    fader?.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    await fixture.whenStable();

    expect(store.channels()[3]).toBe(47);
    expect(fader?.getAttribute('aria-valuenow')).toBe('47');
  });

  it('switching the channel page shows the next 64 channels', async () => {
    await press(button('Channels 65 to 128'));
    expect(page.querySelector('lm-fader[aria-label="Channel 65"]')).toBeTruthy();
    expect(page.querySelector('lm-fader[aria-label="Channel 1"]')).toBeNull();
  });

  it('save: a press on a memory key stores the channel faders there', async () => {
    await press(memoryKey(5));
    expect(connection.methods()).toEqual([]); // outside a function the keys do nothing

    await press(button('Save memory'));
    expect(button('Save memory').getAttribute('aria-pressed')).toBe('true');

    await press(memoryKey(5));
    expect(connection.calls).toContainEqual({ method: 'SaveMemory', args: [5] });
    expect(button('Save memory').getAttribute('aria-pressed')).toBe('false');
    expect(store.memoryStored()[4]).toBe(true);
  });

  it('edit: loads the memory, and Edit again stores it back', async () => {
    await press(button('Edit memory'));
    await press(memoryKey(7)); // empty: nothing to edit
    expect(connection.methods()).toEqual([]);

    await press(memoryKey(2));
    expect(connection.calls).toContainEqual({ method: 'LoadMemory', args: [2] });

    await press(button('Edit memory'));
    expect(connection.calls).toContainEqual({ method: 'SaveMemory', args: [2] });
  });

  it('copy and delete act on stored memories', async () => {
    await press(button('Copy memory'));
    await press(memoryKey(2));
    await press(memoryKey(9));
    expect(connection.calls).toContainEqual({ method: 'CopyMemory', args: [2, 9] });

    await press(button('Delete memory'));
    await press(memoryKey(2));
    expect(connection.calls).toContainEqual({ method: 'DeleteMemory', args: [2] });
    expect(store.memoryStored()[1]).toBe(false);
  });

  it('clear cancels an unfinished function and brings every fader to zero', async () => {
    store.setChannel(1, 200);
    store.setMemoryLevel(2, 100);
    await press(button('Copy memory'));

    await press(button('Clear'));
    expect(store.channels()[0]).toBe(0);
    expect(store.memoryLevels()[1]).toBe(0);
    expect(button('Copy memory').getAttribute('aria-pressed')).toBe('false');
    expect(connection.methods()).toEqual(['Clear']); // the fader moves that were waiting are dropped
  });

  it('a note typed under a key is kept and sent to the backend', async () => {
    const field = page.querySelector<HTMLInputElement>('input[aria-label="Note for Channel 5"]');
    if (!field) throw new Error('no note field for channel 5');
    expect(field.value).toBe('');

    field.value = '  Front left ';
    field.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(store.channelNotes()[4]).toBe('Front left');
    expect(field.value).toBe('Front left');
    expect(connection.calls).toContainEqual({ method: 'SetChannelNote', args: [5, 'Front left'] });

    const memoryField = page.querySelector<HTMLInputElement>('input[aria-label="Note for Memory 2"]');
    if (!memoryField) throw new Error('no note field for memory 2');
    memoryField.value = 'Blue wash';
    memoryField.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.calls).toContainEqual({ method: 'SetMemoryNote', args: [2, 'Blue wash'] });

    // the second line is a field of its own; the two lines travel as one text
    const second = page.querySelector<HTMLInputElement>(
      'input[aria-label="Note for Channel 5, second line"]',
    );
    if (!second) throw new Error('no second note line for channel 5');
    second.value = 'PAR';
    second.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.channelNotes()[4]).toBe('Front left\u2028PAR');
    expect(field.value).toBe('Front left');
    expect(second.value).toBe('PAR');
  });

  it('a medium window shows 16 faders in a row and twice as many page keys', async () => {
    store.stripsPerRow.set(16);
    await fixture.whenStable();
    expect(page.querySelectorAll('lm-channel-section lm-fader-strip').length).toBe(32);
    expect(page.querySelectorAll('lm-memory-section lm-fader-strip').length).toBe(16);
    expect(page.querySelectorAll('nav[aria-label="Channel pages"] button').length).toBe(16);
    expect(page.querySelectorAll('nav[aria-label="Memory pages"] button').length).toBe(8);

    await press(button('Channels 33 to 64'));
    expect(page.querySelector('lm-fader[aria-label="Channel 33"]')).toBeTruthy();
    expect(page.querySelector('lm-fader[aria-label="Channel 1"]')).toBeNull();

    // wide again: the page that holds channel 33 is the one from 1 to 64
    store.stripsPerRow.set(32);
    await fixture.whenStable();
    expect(page.querySelector('lm-fader[aria-label="Channel 1"]')).toBeTruthy();
    expect(page.querySelector('lm-fader[aria-label="Channel 64"]')).toBeTruthy();
  });

  it('an independent key switches its channel between zero and full', async () => {
    await press(button('Independent 3, channel 3'));
    expect(store.channels()[2]).toBe(255);
    expect(button('Independent 3, channel 3').getAttribute('aria-pressed')).toBe('true');

    await press(button('Independent 3, channel 3'));
    expect(store.channels()[2]).toBe(0);
  });

  it('edit lets the channel of an independent key be changed', async () => {
    await press(button('Edit the channels of the independent keys'));
    const field = page.querySelector<HTMLInputElement>(
      'input[aria-label="Channel of independent key 2"]',
    );
    if (!field) throw new Error('no field for independent key 2');

    field.value = '600'; // there is no channel 600: the key keeps its channel
    field.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.independents()[1]).toBe(2);
    expect(field.value).toBe('2');

    field.value = '40';
    field.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.independents()[1]).toBe(40);
    expect(connection.calls).toContainEqual({ method: 'SetIndependent', args: [2, 40] });

    await press(button('Edit the channels of the independent keys'));
    await press(button('Independent 2, channel 40'));
    expect(store.channels()[39]).toBe(255);
  });

  it('a new project is asked for from the backend and unfinished functions are dropped', async () => {
    await press(button('Copy memory'));
    await store.newProject();
    await fixture.whenStable();
    expect(connection.methods()).toContain('NewProject');
    expect(button('Copy memory').getAttribute('aria-pressed')).toBe('false');
    expect(button('New project')).toBeTruthy();
  });

  it('blackout is switched on and off', async () => {
    await press(button('Blackout'));
    expect(connection.calls).toContainEqual({ method: 'SetBlackout', args: [true] });
    expect(button('Blackout').getAttribute('aria-pressed')).toBe('true');

    await press(button('Blackout'));
    expect(connection.calls).toContainEqual({ method: 'SetBlackout', args: [false] });
  });

  it('a note typed only into the second line stays there and leaves the first line empty', async () => {
    const first = page.querySelector<HTMLInputElement>('input[aria-label="Note for Channel 7"]');
    const second = page.querySelector<HTMLInputElement>(
      'input[aria-label="Note for Channel 7, second line"]',
    );
    if (!first || !second) throw new Error('no note fields for channel 7');

    second.value = ' Dimmer ';
    second.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(store.channelNotes()[6]).toBe(NOTE_LINE_SEPARATOR + 'Dimmer');
    expect(connection.calls).toContainEqual({
      method: 'SetChannelNote',
      args: [7, NOTE_LINE_SEPARATOR + 'Dimmer'],
    });
    expect(first.value).toBe('');
    expect(second.value).toBe('Dimmer');

    // the first line can be filled in afterwards without touching the second
    first.value = 'Front';
    first.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(store.channelNotes()[6]).toBe('Front' + NOTE_LINE_SEPARATOR + 'Dimmer');
    expect(second.value).toBe('Dimmer');
  });

  it('a memory fader moves the faders of the channels the memory holds', async () => {
    const channel5 = page.querySelector<HTMLElement>('lm-fader[aria-label="Channel 5"]');
    const memory2 = page.querySelector<HTMLElement>('lm-fader[aria-label="Memory 2"]');
    if (!channel5 || !memory2) throw new Error('faders missing');
    expect(channel5.getAttribute('aria-valuenow')).toBe('0');

    memory2.dispatchEvent(new KeyboardEvent('keydown', { key: 'End' }));
    await fixture.whenStable();
    expect(channel5.getAttribute('aria-valuenow')).toBe('200'); // what the memory holds
    expect(store.channels()[4]).toBe(0); // the channel's own level is untouched
    expect(page.querySelector('lm-fader[aria-label="Channel 6"]')?.getAttribute('aria-valuenow')).toBe('0');
    // channel 70 is on another page: its page key is half lit now
    expect(button('Channels 65 to 128').className).toContain('level-2');

    store.setMemoryLevel(2, 128); // half way: 200 x 128 / 255
    await fixture.whenStable();
    expect(channel5.getAttribute('aria-valuenow')).toBe('100');

    // the owner's own fader wins as soon as it is higher
    store.setChannel(5, 150);
    await fixture.whenStable();
    expect(channel5.getAttribute('aria-valuenow')).toBe('150');

    store.setMemoryLevel(2, 0);
    store.setChannel(5, 0);
    await fixture.whenStable();
    expect(channel5.getAttribute('aria-valuenow')).toBe('0');
  });

  it('a fader held up by a memory still takes the level its owner asks for', async () => {
    const channel5 = page.querySelector<HTMLElement>('lm-fader[aria-label="Channel 5"]');
    if (!channel5) throw new Error('no fader for channel 5');
    store.setChannel(5, 40);
    store.setMemoryLevel(2, 255);
    await fixture.whenStable();
    expect(channel5.getAttribute('aria-valuenow')).toBe('200');

    // one step down goes from the owner's 40, not from the 200 the cap is held at
    channel5.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' }));
    await fixture.whenStable();
    expect(store.channels()[4]).toBe(39);
    expect(channel5.getAttribute('aria-valuenow')).toBe('200');

    channel5.dispatchEvent(new KeyboardEvent('keydown', { key: 'End' }));
    await fixture.whenStable();
    expect(store.channels()[4]).toBe(255);
    expect(channel5.getAttribute('aria-valuenow')).toBe('255');
  });

  it('clear, blackout and the master fader stand in the master section of the main view', async () => {
    const inMaster = (selector: string) => page.querySelector(`lm-main-view lm-master-section ${selector}`);
    expect(inMaster('lm-fader[aria-label="Master"]')).toBeTruthy();
    expect(inMaster('button[aria-label="Blackout"]')).toBeTruthy();
    expect(button('Clear').closest('lm-master-section')).toBeTruthy();
    expect(page.querySelector('lm-top-bar lm-fader')).toBeNull();

    // the memories view is for looking only: it has no master section
    await show('/memories');
    expect(page.querySelector('lm-master-section')).toBeNull();
    expect(page.querySelector('lm-fader[aria-label="Master"]')).toBeNull();
  });

  it('the top bar links to the two views and marks the one that is shown', async () => {
    const links = [...page.querySelectorAll<HTMLAnchorElement>('nav[aria-label="Views"] a')];
    expect(links.map((link) => link.textContent?.trim())).toEqual(['Main', 'Memories']);
    expect(links[0].getAttribute('aria-current')).toBe('page');
    expect(links[1].getAttribute('aria-current')).toBeNull();
    expect(page.querySelector('lm-main-view')).toBeTruthy();

    await show('/memories');
    expect(links[0].getAttribute('aria-current')).toBeNull();
    expect(links[1].getAttribute('aria-current')).toBe('page');
    expect(page.querySelector('lm-main-view')).toBeNull();
    expect(page.querySelector('lm-memories-view')).toBeTruthy();
  });

  it('the memories view shows what a memory holds and changes nothing', async () => {
    await show('/memories');
    const cells = () => [...page.querySelectorAll('lm-memories-view .matrix li')];
    const levelOf = (channel: number) => cells()[channel - 1].querySelector('.level')?.textContent?.trim();
    expect(cells().length).toBe(512);
    expect(page.querySelectorAll('lm-memories-view .keys button').length).toBe(32);
    expect(page.querySelectorAll('nav[aria-label="Memory pages"] button').length).toBe(4);

    // memory 1 is looked at first, and it is empty
    expect(page.querySelector('.summary')?.textContent).toContain('Memory 1 is empty');
    expect(levelOf(5)).toBe('0');

    await press(button('Look at memory 2'));
    expect(button('Look at memory 2').getAttribute('aria-pressed')).toBe('true');
    expect(page.querySelector('.summary')?.textContent).toContain('Memory 2: 2 of 512 channels are above zero');
    expect(levelOf(5)).toBe('200');
    expect(levelOf(70)).toBe('255');
    expect(levelOf(6)).toBe('0');
    expect(cells()[4].getAttribute('aria-label')).toBe('Channel 5: 200');

    expect(connection.methods()).toEqual([]); // looking sends nothing to the desk
    expect(store.channels()[4]).toBe(0);
  });
});
