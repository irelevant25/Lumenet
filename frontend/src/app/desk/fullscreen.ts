/**
 * The whole screen for a show: the Stack key asks the browser for it when it takes the stack
 * live, and leaves it when it switches the stack off. The browser grants it only right after a
 * press (a click, a tap, a key) on this device, and asks nothing; where it cannot (an iPhone), or
 * the page is on the whole screen already, nothing happens.
 */
export function enterFullscreen(): void {
  const page = typeof document === 'undefined' ? null : document.documentElement;
  if (!page || document.fullscreenElement || typeof page.requestFullscreen !== 'function') return;
  page.requestFullscreen({ navigationUI: 'hide' }).catch(() => undefined);
}

/** Leaves the whole screen, when the page took it (the browser's own F11 is the owner's). */
export function leaveFullscreen(): void {
  if (
    typeof document === 'undefined' ||
    !document.fullscreenElement ||
    typeof document.exitFullscreen !== 'function'
  )
    return;
  document.exitFullscreen().catch(() => undefined);
}
