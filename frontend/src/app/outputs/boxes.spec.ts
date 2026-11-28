import { plansOf, switchNumber, withBox, withOutput, withoutBox } from './boxes';
import { BoxView } from '../desk/desk-types';

const pico: BoxView = {
  address: '192.168.7.1',
  connected: true,
  firmware: '1.0',
  outputs: 1,
  universes: [1],
  kind: 'automatic',
  model: null,
};
const second: BoxView = {
  address: '10.1.3.1',
  connected: true,
  firmware: '1.0',
  outputs: 2,
  universes: [2, 3],
  kind: 'automatic',
  model: null,
};

describe('boxes', () => {
  it('the first change makes the boxes that answer the list, with what they play now', () => {
    expect(plansOf([pico, second])).toEqual([
      { address: '192.168.7.1', universes: [1] },
      { address: '10.1.3.1', universes: [2, 3] },
    ]);
    expect(withOutput([pico, second], '10.1.3.1', 1, 0)).toEqual([
      { address: '192.168.7.1', universes: [1] },
      { address: '10.1.3.1', universes: [2, 0] },
    ]);
  });

  it('a new box joins the list with the universes no box plays yet', () => {
    const boxes: BoxView[] = [{ ...pico, kind: 'listed' }, { ...second, universes: [0, 0], kind: 'new' }];
    expect(withBox(boxes, '10.1.3.1', 2, 2)).toEqual([
      { address: '192.168.7.1', universes: [1] },
      { address: '10.1.3.1', universes: [2, 0] },
    ]);
    expect(withOutput(boxes, '10.1.3.1', 1, 1)).toEqual([
      { address: '192.168.7.1', universes: [1] },
      { address: '10.1.3.1', universes: [0, 1] },
    ]);
    expect(withoutBox(boxes, '192.168.7.1')).toEqual([]);
  });

  it('a box of the 10.1.n.1 network has the number of its switch', () => {
    expect(switchNumber('10.1.3.1')).toBe(3);
    expect(switchNumber('10.1.63.1')).toBe(63);
    expect(switchNumber('192.168.7.1')).toBeNull();
    expect(switchNumber('10.1.3.2')).toBeNull();
  });
});
