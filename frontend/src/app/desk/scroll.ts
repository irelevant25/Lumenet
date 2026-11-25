import { pageZoom } from './zoom';

/*
 * Scrolling an element into sight without moving the page. The browser's scrollIntoView moves every
 * box around the element, the page itself as well, and the desk's page cannot be scrolled back by
 * hand: the top bar would stay out of sight. These move only the box the element scrolls in.
 */

/** Scrolls the box the element scrolls in, and only that one, so that the element stands in its middle. */
export function bringToMiddle(element: HTMLElement): void {
  scrollTo(element, (height, place) => (height - place) / 2);
}

/** Scrolls the box the element scrolls in, and only that one, so that the element stands at its top. */
export function bringToTop(element: HTMLElement): void {
  scrollTo(element, () => 0);
}

/**
 * Scrolls `box` itself, when it scrolls, just so far that the element shows whole in it: as little
 * as needed, and not at all when it shows already. Nothing around the box moves, the page neither.
 */
export function bringIntoBox(box: HTMLElement, element: HTMLElement): void {
  const overflow = getComputedStyle(box).overflowY;
  if ((overflow !== 'auto' && overflow !== 'scroll') || box.scrollHeight <= box.clientHeight)
    return;

  // in the window's pixels, as the rectangles are; scrollTop is in the page's (see zoom.ts)
  const zoom = pageZoom();
  const top = box.getBoundingClientRect().top;
  const bottom = top + box.clientHeight * zoom;
  const place = element.getBoundingClientRect();
  if (place.top < top) box.scrollTop += (place.top - top) / zoom;
  else if (place.bottom > bottom) box.scrollTop += (place.bottom - bottom) / zoom;
}

// Scrolls the element's box so that the element's top stands `from` pixels (of the window) below
// the box's top; `from` gets the box's height and the element's.
function scrollTo(element: HTMLElement, from: (height: number, place: number) => number): void {
  const box = scrollingBox(element);
  if (!box) return;

  // in the window's pixels, as the rectangles are; scrollTop is in the page's (see zoom.ts)
  const zoom = pageZoom();
  const page = box === document.scrollingElement;
  const top = page ? 0 : box.getBoundingClientRect().top;
  const height = page ? window.innerHeight : box.clientHeight * zoom;
  const place = element.getBoundingClientRect();
  box.scrollTop += (place.top - top - from(height, place.height)) / zoom;
}

/**
 * The nearest box around the element that the owner can scroll: a list, the panels of Settings, or
 * on a small screen the page. Null when nothing can be scrolled by hand.
 */
function scrollingBox(element: HTMLElement): Element | null {
  for (let box = element.parentElement; box && box !== document.body; box = box.parentElement) {
    const overflow = getComputedStyle(box).overflowY;
    if ((overflow === 'auto' || overflow === 'scroll') && box.scrollHeight > box.clientHeight)
      return box;
  }
  const pageScrolls = getComputedStyle(document.body).overflowY !== 'hidden';
  return pageScrolls ? document.scrollingElement : null;
}
