/**
 * The zoom of the page, as Settings sets it on <html>: how many pixels of the window one pixel of
 * the page takes. Rectangles (getBoundingClientRect) and the pointer are in the window's pixels;
 * the page's own lengths (a style in px, scrollTop, offsetHeight) in its pixels. A distance taken
 * from rectangles and used as a length of the page is divided by it.
 */
export function pageZoom(): number {
  if (typeof document === 'undefined') return 1;
  const zoom = parseFloat(document.documentElement.style.getPropertyValue('zoom'));
  return Number.isFinite(zoom) && zoom > 0 ? zoom : 1;
}
