import { ComponentFixture } from '@angular/core/testing';
import { App } from '../app';
import { DeskStore } from '../desk/desk-store';
import type { RdmBoxView, RdmLightView } from '../desk/hub-contract';
import { deskPageHelpers, FakeConnection, startDeskPage } from '../testing/desk-page.testing';

const BOX = '10.1.3.1';

const PAR: RdmLightView = {
  box: BOX,
  id: '7FF1:00000001',
  maker: 'Lumenet test',
  model: 'LED Par RGBW',
  label: 'Front left',
  address: 1,
  channels: 4,
  mode: 1,
  modes: [
    { channels: 4, name: '4 channels: R G B W' },
    { channels: 8, name: '8 channels, dimmer and strobe' },
  ],
  identify: false,
  error: null,
};

const HEAD: RdmLightView = {
  box: BOX,
  id: '7FF1:00000002',
  maker: 'Lumenet test',
  model: 'Moving head',
  label: '',
  address: 3,
  channels: 16,
  mode: 1,
  modes: [
    { channels: 16, name: '16 channels' },
    { channels: 11, name: '11 channels, basic' },
  ],
  identify: false,
  error: null,
};

describe('the desk page: the RDM tab of Settings', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;
  const { show, until, textOf, choose } = deskPageHelpers(() => ({
    connection,
    fixture,
    page,
    store,
  }));

  const rows = () => [
    ...page.querySelectorAll<HTMLTableRowElement>('lm-rdm-panel tbody tr:not(.refusal)'),
  ];
  const field = <T extends HTMLElement>(label: string) =>
    page.querySelector<T>(`lm-rdm-panel [aria-label="${label}"]`)!;

  function box(more: Partial<RdmBoxView> = {}): RdmBoxView {
    return {
      address: BOX,
      number: 3,
      rdm: true,
      searching: false,
      lights: [PAR.id, HEAD.id],
      error: null,
      ...more,
    };
  }

  // Waits in real time: the panel asks a searching box again after 600 ms.
  async function within(ms: number, happened: () => boolean): Promise<void> {
    for (let waited = 0; waited < ms && !happened(); waited += 25) {
      await new Promise((resolve) => setTimeout(resolve, 25));
      await fixture.whenStable();
    }
    expect(happened()).toBe(true);
  }

  async function change(element: HTMLInputElement, value: string): Promise<void> {
    element.value = value;
    element.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    await until(() => !element.disabled);
  }

  beforeEach(async () => {
    ({ connection, fixture, page, store } = await startDeskPage());
    connection.rdmBoxes = [box()];
    connection.rdmLights = { [`${BOX} ${PAR.id}`]: { ...PAR }, [`${BOX} ${HEAD.id}`]: { ...HEAD } };
  });

  it('lists the lights of each box, read one after the other, and says where two take the same channels', async () => {
    await show('/settings#rdm');
    await until(
      () => rows().length === 2 && !rows().some((row) => row.classList.contains('reading')),
    );

    expect(textOf('lm-rdm-panel h4')).toBe('dmxbox at 10.1.3.1, box 3');
    expect(textOf('lm-rdm-panel .count')).toBe('2 lights');
    expect(rows().map((row) => row.querySelector('.name')?.textContent?.trim())).toEqual([
      'Front left',
      'Moving head',
    ]);
    expect(rows()[0].querySelector('th small')?.textContent?.trim()).toBe(
      'Lumenet test LED Par RGBW, 7FF1:00000001',
    );
    expect(field<HTMLInputElement>('DMX address of Front left').value).toBe('1');
    expect(rows()[1].textContent).toContain('3 to 18');
    const modes = [...field<HTMLSelectElement>('Mode of Moving head').options].map((option) =>
      option.text.trim(),
    );
    expect(modes).toEqual(['1: 16 channels', '2: 11 channels, basic']);
    expect(textOf('lm-rdm-panel .remark')).toBe(
      'Channels 3 to 4: both Front left and Moving head take them.',
    );
    expect(
      connection.calls.filter((call) => call.method === 'GetRdmLight').map((call) => call.args),
    ).toEqual([
      [BOX, PAR.id],
      [BOX, HEAD.id],
    ]);
  });

  it('changes the address, the mode and the name on the light itself', async () => {
    await show('/settings#rdm');
    await until(
      () => rows().length === 2 && !rows().some((row) => row.classList.contains('reading')),
    );

    await change(field<HTMLInputElement>('DMX address of Moving head'), '5');
    expect(connection.last('SetRdmLight')).toEqual([BOX, HEAD.id, { address: 5 }]);
    expect(textOf('lm-rdm-panel .remark')).toBeUndefined(); // 1 to 4 and 5 to 20 no longer meet

    await choose('lm-rdm-panel [aria-label="Mode of Front left"]', '2');
    expect(connection.last('SetRdmLight')).toEqual([BOX, PAR.id, { mode: 2 }]);
    await until(() => rows()[0].textContent!.includes('1 to 8'));
    expect(textOf('lm-rdm-panel .remark')).toBe(
      'Channels 5 to 8: both Front left and Moving head take them.',
    );

    await change(field<HTMLInputElement>('Name of Lumenet test, 7FF1:00000002'), ' Spot right ');
    expect(connection.last('SetRdmLight')).toEqual([BOX, HEAD.id, { label: 'Spot right' }]);
    await until(() => rows()[1].querySelector('.name')?.textContent?.trim() === 'Spot right');
  });

  it('says what a light refuses under it, and its field shows the light again', async () => {
    await show('/settings#rdm');
    await until(
      () => rows().length === 2 && !rows().some((row) => row.classList.contains('reading')),
    );
    connection.rdmRefusal = 'The light 7FF1:00000001 refused: the value is out of its range.';

    const address = field<HTMLInputElement>('DMX address of Front left');
    await change(address, '509');
    await until(() => page.querySelector('lm-rdm-panel tr.refusal') !== null);
    expect(textOf('lm-rdm-panel tr.refusal')).toBe(
      'The light 7FF1:00000001 refused: the value is out of its range.',
    );
    expect(address.value).toBe('1');

    await change(address, '0'); // not an address at all: the desk program is not asked
    expect(connection.last('SetRdmLight')).toEqual([BOX, PAR.id, { address: 509 }]);
    expect(textOf('lm-rdm-panel tr.refusal')).toBe('A DMX address is 1 to 512.');

    connection.rdmRefusal = null; // a change that goes through clears the refusal
    await change(address, '9');
    expect(page.querySelector('lm-rdm-panel tr.refusal')).toBeNull();
  });

  it('Identify lights its key, and the light stops when the tab is left', async () => {
    await show('/settings#rdm');
    await until(
      () => rows().length === 2 && !rows().some((row) => row.classList.contains('reading')),
    );
    const key = () => field<HTMLButtonElement>('Identify Moving head: it shows where it is');

    key().click();
    await until(() => key().getAttribute('aria-pressed') === 'true');
    expect(connection.last('SetRdmLight')).toEqual([BOX, HEAD.id, { identify: true }]);

    await show('/settings#outputs');
    await until(() => connection.rdmLights[`${BOX} ${HEAD.id}`].identify === false);
    expect(connection.last('SetRdmLight')).toEqual([BOX, HEAD.id, { identify: false }]);
  });

  it('a box without RDM, a box that does not answer, a line without lights, and Search', async () => {
    connection.rdmBoxes = [
      { address: '10.1.1.1', number: 1, rdm: false, searching: false, lights: [], error: null },
      {
        address: '10.1.2.1',
        number: null,
        rdm: false,
        searching: false,
        lights: [],
        error: 'The box does not answer.',
      },
      box({ lights: [] }),
    ];
    await show('/settings#rdm');
    await until(() => page.querySelectorAll('lm-rdm-panel section.box').length === 3);
    const sections = [...page.querySelectorAll('lm-rdm-panel section.box')];
    expect(sections[0].textContent).toContain(
      'This box has no RDM: its firmware is older than 3.0.',
    );
    expect(sections[0].querySelector('button')).toBeNull();
    expect(sections[1].querySelector('.error')?.textContent?.trim()).toBe(
      'The box does not answer.',
    );
    expect(sections[2].textContent).toContain('No light with RDM answers on its line.');

    // Search: the box looks again; while it searches the panel asks again until the list is complete.
    connection.rdmBoxes = [box({ searching: true, lights: [PAR.id] })];
    sections[2].querySelector('button')!.click();
    await until(() => textOf('lm-rdm-panel .count') === 'searching, 1 light so far');
    expect(connection.last('SearchRdm')).toEqual([BOX]);
    connection.rdmBoxes = [box()];
    await within(2000, () => textOf('lm-rdm-panel .count') === '2 lights' && rows().length === 2);
  });

  it('opened before the page is connected, it asks once the connection is there', async () => {
    connection.online.set(false); // a reload straight onto /settings#rdm
    await show('/settings#rdm');
    expect(connection.calls.some((call) => call.method === 'GetRdmBoxes')).toBe(false);
    connection.online.set(true);
    await until(() => rows().length === 2 && !rows().some((row) => row.classList.contains('reading')));
  });

  it('without a desk program that does RDM, it says why', async () => {
    connection.invoke = (async (method: string, ...args: unknown[]) => {
      if (method === 'GetRdmBoxes')
        throw new Error("The desk program has no method 'GetRdmBoxes'.");
      return FakeConnection.prototype.invoke.call(connection, method, ...args);
    }) as typeof connection.invoke;
    await show('/settings#rdm');
    await until(() => page.querySelector('lm-rdm-panel .error') !== null);
    expect(textOf('lm-rdm-panel .error')).toBe("The desk program has no method 'GetRdmBoxes'.");
  });
});
