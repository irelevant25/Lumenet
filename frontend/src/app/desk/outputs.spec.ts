import { BoxView, OutputPlan, OutputView } from './desk-types';
import {
  newPlan,
  outputsSummary,
  planOfNode,
  playsText,
  portAddress,
  portAddressParts,
  portAddressText,
  sharedUniverses,
  unplayedUniverses,
  withNewPort,
  withPlan,
  withPort,
  withoutOutput,
} from './outputs';

const pico: BoxView = {
  address: '192.168.7.1',
  connected: true,
  firmware: '1.0',
  outputs: 1,
  universes: [1],
  kind: 'listed',
};
const missingBox: BoxView = {
  address: '10.1.4.1',
  connected: false,
  firmware: null,
  outputs: 1,
  universes: [3],
  kind: 'listed',
};
const node: OutputPlan = {
  kind: 'artnet',
  target: '192.168.1.50',
  ports: [{ universe: 2, address: 0 }],
  network: '',
  priority: 100,
};
const sacn: OutputPlan = {
  kind: 'sacn',
  target: 'multicast',
  ports: [{ universe: 1, address: 1 }],
  network: '',
  priority: 100,
};

function view(plan: OutputPlan, state: OutputView['state']): OutputView {
  return { plan, state, detail: '' };
}

describe('outputs', () => {
  it('the top bar counts the boxes and the outputs that play, of those the project has', () => {
    expect(outputsSummary([], []).text).toBe('Outputs connected: 0');
    expect(outputsSummary([{ ...pico, kind: 'automatic' }], []).text).toBe('Outputs connected: 1');
    expect(
      outputsSummary([pico, missingBox], [view(node, 'connected'), view(sacn, 'sending')]),
    ).toEqual({
      working: 3,
      missing: 1,
      text: 'Outputs connected: 3 of 4',
    });
    expect(outputsSummary([], [view(node, 'missing'), view(node, 'conflict')])).toEqual({
      working: 0,
      missing: 2,
      text: 'Outputs connected: 0 of 2',
    });
    // A new box, which answers but is not on the list, plays nothing and is not counted.
    expect(outputsSummary([pico, { ...missingBox, connected: true, kind: 'new' }], []).text).toBe(
      'Outputs connected: 1 of 1',
    );
  });

  it('says which universes reach no cable, and which the boxes and the outputs share', () => {
    const outputs = [
      view(node, 'connected'),
      view({ ...sacn, ports: [{ universe: 3, address: 3 }] }, 'missing'),
    ];
    expect(unplayedUniverses([pico, missingBox], outputs, 4)).toEqual([3, 4]);
    expect(sharedUniverses([pico, missingBox], outputs)).toEqual([3]);
    expect(sharedUniverses([pico], [view(sacn, 'sending')])).toEqual([1]);
  });

  it('a new output plays the universes no box or output plays yet', () => {
    expect(newPlan('artnet', '10.0.0.9', [pico], [view(node, 'connected')], 4)).toEqual({
      kind: 'artnet',
      target: '10.0.0.9',
      ports: [
        { universe: 3, address: 2 },
        { universe: 4, address: 3 },
      ],
      network: '',
      priority: 100,
    });
    expect(newPlan('sacn', 'multicast', [], [], 2).ports).toEqual([
      { universe: 1, address: 1 },
      { universe: 2, address: 2 },
    ]);
    expect(newPlan('usb', 'COM3', [pico], [], 1).ports).toEqual([{ universe: 1, address: 0 }]); // all play: U1 again
    expect(newPlan('usb', 'COM3', [], [], 3).ports).toEqual([{ universe: 1, address: 0 }]); // one port
  });

  it('a found node gets a port for each of its own', () => {
    const found = {
      ip: '10.0.0.7',
      shortName: 'Node',
      longName: '',
      firmware: '1.0',
      ports: [16, 17, 18],
      dmxbox: false,
    };
    expect(planOfNode(found, [pico], [], 2).ports).toEqual([
      { universe: 2, address: 16 },
      { universe: 0, address: 17 },
      { universe: 0, address: 18 },
    ]);
    expect(planOfNode({ ...found, ports: [] }, [], [], 1).ports).toEqual([
      { universe: 1, address: 0 },
    ]);
    // Every universe plays already: the first port plays U1 as well, rather than nothing.
    expect(planOfNode(found, [pico], [], 1).ports).toEqual([
      { universe: 1, address: 16 },
      { universe: 0, address: 17 },
      { universe: 0, address: 18 },
    ]);
  });

  it('ports are added at the next address, changed and taken out', () => {
    const two = withNewPort(node, 2);
    expect(two.ports).toEqual([
      { universe: 2, address: 0 },
      { universe: 1, address: 1 },
    ]);
    expect(withNewPort({ ...sacn, ports: [] }, 1).ports).toEqual([{ universe: 1, address: 1 }]);
    expect(withPort(two, 1, { address: 17 }).ports[1]).toEqual({ universe: 1, address: 17 });

    const outputs = [view(node, 'connected'), view(sacn, 'sending')];
    expect(withPlan(outputs, 1, (plan) => ({ ...plan, priority: 150 }))[1].priority).toBe(150);
    expect(withoutOutput(outputs, 0)).toEqual([sacn]);
    expect(outputs[1].plan.priority).toBe(100); // the views stay as they are
  });

  it('an Art-Net Port-Address is its Net, Sub-Net and Universe', () => {
    expect(portAddress(1, 2, 3)).toBe(0x123);
    expect(portAddressParts(0x123)).toEqual({ net: 1, subNet: 2, universe: 3 });
    expect(portAddressText(17)).toBe('0:1:1');
    expect(
      playsText({
        ...node,
        ports: [
          { universe: 2, address: 0 },
          { universe: 0, address: 1 },
        ],
      }),
    ).toBe('U2 at 0:0:0, off');
    expect(playsText({ ...node, kind: 'usb', target: 'COM3' })).toBe('U2');
  });
});
