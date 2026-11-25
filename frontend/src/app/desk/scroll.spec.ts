import { bringIntoBox } from './scroll';

/** A box 100 pixels high at 50 pixels from the top of the window, with 400 pixels of keys in it. */
function column(): { box: HTMLElement; key: HTMLElement; place: (top: number) => void } {
  const box = document.createElement('nav');
  box.style.overflowY = 'auto';
  const key = document.createElement('button');
  box.append(key);
  document.body.append(box);
  Object.defineProperty(box, 'scrollHeight', { value: 400 });
  Object.defineProperty(box, 'clientHeight', { value: 100 });
  Object.defineProperty(box, 'scrollTop', { value: 0, writable: true });
  box.getBoundingClientRect = () => ({ top: 50, bottom: 150 }) as DOMRect;
  let top = 0;
  key.getBoundingClientRect = () => ({ top, bottom: top + 30 }) as DOMRect;
  return { box, key, place: (at) => (top = at) };
}

describe('scrolling a column of keys', () => {
  afterEach(() => document.body.replaceChildren());

  it('scrolls the column just so far that the key shows whole, down or up', () => {
    const { box, key, place } = column();
    place(180); // below: its bottom 60 pixels under the column's
    bringIntoBox(box, key);
    expect(box.scrollTop).toBe(60);

    box.scrollTop = 200;
    place(20); // above: its top 30 pixels over the column's
    bringIntoBox(box, key);
    expect(box.scrollTop).toBe(170);
  });

  it('leaves a key that shows, and a column that does not scroll, as they are', () => {
    const { box, key, place } = column();
    place(80);
    bringIntoBox(box, key);
    expect(box.scrollTop).toBe(0);

    box.style.overflowY = 'visible'; // on a small screen the keys lie in lines and the page scrolls
    place(300);
    bringIntoBox(box, key);
    expect(box.scrollTop).toBe(0);
  });
});
