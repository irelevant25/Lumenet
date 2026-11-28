import { ComponentFixture } from '@angular/core/testing';
import { App } from '../app';
import { DeskStore } from '../desk/desk-store';
import { BoxView, OutputView } from '../desk/desk-types';
import { deskPageHelpers, FakeConnection, startDeskPage } from '../testing/desk-page.testing';

describe('the desk page: universes and outputs', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;
  const { show, button, press, until, textOf, twoUniverses, choose, wait } = deskPageHelpers(
    () => ({ connection, fixture, page, store }),
  );

  beforeEach(async () => {
    ({ connection, fixture, page, store } = await startDeskPage());
  });

  it('Settings sets the universes of the project', async () => {
    await show('/settings');
    const universes = page.querySelector<HTMLSelectElement>('#universes')!;
    expect(universes.value).toBe('1');
    universes.value = '3';
    universes.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(connection.last('SetUniverses')).toEqual([3]);
    expect(universes.value).toBe('1'); // until the desk program has taken it: it may refuse
    connection.emit('DeskChanged', {
      ...twoUniverses(),
      universes: 3,
      channels: new Array<number>(1536).fill(0),
    });
    await fixture.whenStable();
    expect(universes.value).toBe('3');
    expect(page.querySelector('#addresses')).toBeNull(); // the channels are always 1 to 512 in each
  });

  it('the top left counts the boxes, and the Outputs panel of Settings says which universe each output of a box plays', async () => {
    const boxes: BoxView[] = [
      {
        address: '192.168.7.1',
        connected: true,
        firmware: '1.0',
        outputs: 1,
        universes: [1],
        kind: 'automatic',
        model: null,
      },
      {
        address: '10.1.3.1',
        connected: true,
        firmware: '1.0',
        outputs: 2,
        universes: [0, 0],
        kind: 'automatic',
        model: null,
      },
    ];
    connection.emit('BoxesChanged', boxes);
    await fixture.whenStable();
    expect(textOf('lm-outputs-status')).toBe('Outputs connected: 2');
    await show('/settings#outputs');
    const rows = () => [...page.querySelectorAll('lm-outputs-panel tbody tr')];
    expect(rows().map((row) => row.querySelector('.name')?.textContent?.trim())).toEqual([
      'dmxbox 1',
      'dmxbox 2',
      'dmxbox 2, output 2',
    ]);
    expect(rows()[1].querySelector('th')?.textContent).toContain(
      '10.1.3.1, switch 3, firmware 1.0',
    );
    expect(rows()[2].textContent).toContain('Output 2');
    expect(textOf('lm-outputs-panel .remark')).toBeUndefined(); // U1, the only universe, is played

    // The second output of the second box plays U1 as well: the boxes become the project's list.
    await choose('select[aria-label="Universe of output 2 of dmxbox 10.1.3.1"]', '1');
    expect(connection.last('SetBoxes')).toEqual([
      [
        { address: '192.168.7.1', universes: [1] },
        { address: '10.1.3.1', universes: [0, 1] },
      ],
    ]);

    // A box added by hand, before it is there.
    connection.emit('BoxesChanged', [
      { ...boxes[0], kind: 'listed' },
      { ...boxes[1], universes: [0, 1], kind: 'listed' },
    ]);
    await fixture.whenStable();
    await choose('#output-kind', 'dmxbox');
    const address = page.querySelector<HTMLInputElement>(
      'lm-outputs-panel form.add input[aria-label="Address"]',
    )!;
    address.value = '10.1.4.1';
    address.dispatchEvent(new Event('input'));
    page.querySelector('lm-outputs-panel form.add')!.dispatchEvent(new Event('submit'));
    await until(() => connection.methods().filter((method) => method === 'SetBoxes').length === 2);
    expect(connection.last('SetBoxes')).toEqual([
      [
        { address: '192.168.7.1', universes: [1] },
        { address: '10.1.3.1', universes: [0, 1] },
        { address: '10.1.4.1', universes: [0] },
      ],
    ]);

    // It does not answer: the top left counts it, and its light is amber. Auto empties the list.
    connection.emit('BoxesChanged', [
      { ...boxes[0], kind: 'listed' },
      { ...boxes[1], universes: [0, 1], kind: 'listed' },
      {
        address: '10.1.4.1',
        connected: false,
        firmware: null,
        outputs: 1,
        universes: [0],
        kind: 'listed',
        model: null,
      },
    ]);
    await fixture.whenStable();
    expect(textOf('lm-outputs-status')).toBe('Outputs connected: 2 of 3');
    expect(page.querySelector('lm-outputs-status p')?.className).toContain('missing');
    expect(rows()[3].querySelector('.state')?.textContent?.trim()).toBe('Not answering');
    expect(textOf('lm-outputs-panel .remark')).toBe(
      '⚠ U1 goes out twice, on dmxbox 192.168.7.1 and dmxbox 10.1.3.1, as a splitter would give it.',
    );
    await press(button('Automatic: the boxes play by themselves'));
    expect(connection.last('SetBoxes')).toEqual([[]]);
  });

  it('the Outputs panel adds no output that is in the list already', async () => {
    connection.emit('OutputsChanged', [
      {
        plan: {
          kind: 'artnet',
          target: '10.0.0.7',
          ports: [{ universe: 1, address: 0 }],
          network: '',
          priority: 100,
        },
        state: 'connected',
        detail: '',
      },
    ]);
    await show('/settings#outputs');
    const address = page.querySelector<HTMLInputElement>(
      'lm-outputs-panel form.add input[aria-label="Address"]',
    )!;
    const add = page.querySelector<HTMLButtonElement>(
      'lm-outputs-panel form.add button[type="submit"]',
    )!;
    address.value = ' 10.0.0.7';
    address.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(add.disabled).toBe(true);
    expect(textOf('lm-outputs-panel form.add + p.remark')).toBe(
      'The Art-Net node at 10.0.0.7 is in the list already.',
    );
    page.querySelector('lm-outputs-panel form.add')!.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    expect(connection.methods()).not.toContain('SetOutputs');

    address.value = '10.0.0.8';
    address.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    expect(add.disabled).toBe(false);
    expect(page.querySelector('lm-outputs-panel form.add + p.remark')).toBeNull();
  });

  it('a page loaded at Settings asks for the networks and the nodes once it is connected, not before', async () => {
    connection.online.set(false);
    await show('/settings#outputs');
    const asked = (method: string) =>
      connection.calls.filter((call) => call.method === method).length;
    expect(asked('FindArtNetNodes')).toBe(0);
    expect(asked('GetNetworks')).toBe(0);

    connection.online.set(true);
    await fixture.whenStable();
    expect(asked('FindArtNetNodes')).toBe(1);
    expect(asked('GetNetworks')).toBe(1);
    expect(page.querySelector('lm-outputs-panel .error')).toBeNull();
  });

  it('the Outputs panel adds an Art-Net node the search found, sACN and a USB interface, and the top left counts them', async () => {
    connection.foundNodes = [
      {
        ip: '10.0.0.7',
        shortName: 'Node 2',
        longName: 'A node',
        firmware: '1.2',
        ports: [0, 1],
        dmxbox: false,
      },
      {
        ip: '192.168.7.1',
        shortName: 'dmxbox',
        longName: 'dmxbox',
        firmware: '1.0',
        ports: [0],
        dmxbox: true,
      },
    ];
    await show('/settings#outputs');
    // the panel asks for the networks, the serial ports and the nodes at once
    await until(() => page.querySelectorAll('lm-outputs-panel .found li').length === 2);
    expect(connection.calls.map((call) => call.method)).toEqual(
      expect.arrayContaining(['GetNetworks', 'GetSerialPorts', 'FindArtNetNodes']),
    );

    // A dmxbox cannot be added as a node, the other one can.
    expect(page.querySelectorAll('lm-outputs-panel .found li')[1].textContent).toContain(
      'Already played as a dmxbox',
    );
    await press(button('Add Node 2 at 10.0.0.7'));
    const node = {
      kind: 'artnet',
      target: '10.0.0.7',
      ports: [
        { universe: 1, address: 0 },
        { universe: 0, address: 1 },
      ],
      network: '',
      priority: 100,
    };
    expect(connection.last('SetOutputs')).toEqual([[node]]);

    // The desk program answers with the output and its state; the Net of the first port changes.
    const outputs: OutputView[] = [
      { plan: node as OutputView['plan'], state: 'connected', detail: 'Node 2, firmware 1.2' },
    ];
    connection.emit('OutputsChanged', outputs);
    await fixture.whenStable();
    // a line for each port, the node by its name, the kind and the state on its first line
    const rows = [...page.querySelectorAll('lm-outputs-panel tbody tr')];
    expect(rows.map((row) => row.querySelector('.name')?.textContent?.trim())).toEqual([
      'Node 2',
      'Node 2, port 2',
    ]);
    expect(rows[0].querySelector('th small')?.textContent?.trim()).toBe('10.0.0.7, Art-Net');
    expect(rows[0].querySelector('.kind')?.textContent?.trim()).toBe('Art-Net');
    expect(rows[0].querySelector('.state')?.textContent?.trim()).toBe('Connected');
    const net = page.querySelector<HTMLInputElement>(
      'input[aria-label="Net of port 1 of Node 2"]',
    )!;
    net.value = '1';
    net.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect((connection.last('SetOutputs')?.[0] as { ports: unknown }[])[0].ports).toEqual([
      { universe: 1, address: 256 },
      { universe: 0, address: 1 },
    ]);

    // sACN by multicast, and a USB interface at the first free port.
    const add = async (kind: string) => {
      await choose('#output-kind', kind);
      page.querySelector('lm-outputs-panel form.add')!.dispatchEvent(new Event('submit'));
      await fixture.whenStable();
      await wait(0);
    };
    await add('multicast');
    expect((connection.last('SetOutputs')?.[0] as unknown[])[1]).toEqual({
      kind: 'sacn',
      target: 'multicast',
      ports: [{ universe: 1, address: 1 }],
      network: '',
      priority: 100,
    });
    await add('usb');
    expect((connection.last('SetOutputs')?.[0] as { target: string }[])[1].target).toBe('COM3');

    // The top left counts the outputs that play, and a node that does not answer turns it amber.
    connection.emit('OutputsChanged', [
      outputs[0],
      {
        plan: {
          kind: 'sacn',
          target: 'multicast',
          ports: [{ universe: 1, address: 1 }],
          network: '',
          priority: 100,
        },
        state: 'sending',
        detail: '',
      },
      {
        plan: {
          kind: 'artnet',
          target: '10.0.0.9',
          ports: [{ universe: 1, address: 0 }],
          network: '',
          priority: 100,
        },
        state: 'missing',
        detail: '',
      },
    ]);
    await fixture.whenStable();
    expect(textOf('lm-outputs-status')).toBe('Outputs connected: 2 of 3');
    expect(page.querySelector('lm-outputs-status p')?.className).toContain('missing');
    expect(page.querySelector('lm-outputs-status p')?.getAttribute('title')).toContain(
      'Art-Net 10.0.0.9: not answering (U1 at 0:0:0)',
    );
    expect(textOf('lm-outputs-panel .remark')).toBe(
      '⚠ U1 goes out 3 times, on Art-Net 10.0.0.7, sACN multicast and Art-Net 10.0.0.9, as a splitter would give it.',
    );
  });

  // ---------------------------------------------------------------- undo, solo, rate, project settings, autosave, colors
});
