import { STANDARD_KEY_COLORS } from '../desk/key-colors';

/**
 * The notes of a project are a little HTML: what the editor of the Notes view writes, with its
 * few marks: bold, underline, a size and a color for a part of the text, and lists. Whatever
 * HTML comes in (a file someone edited by hand, a page of another version) is made into those
 * marks before it is shown or kept: every other element is taken out and its text stays, and
 * what is no text at all (a script, a picture, a form) goes with its content.
 */

/** The sizes the editor offers: the size marks of <font size>, "3" being the usual size. */
export const NOTE_SIZES: readonly { readonly size: string; readonly name: string }[] = [
  { size: '2', name: 'Small' },
  { size: '3', name: 'Normal' },
  { size: '5', name: 'Large' },
  { size: '6', name: 'Huge' },
];

/** The usual size: text without a size mark. */
export const USUAL_SIZE = '3';

/** The colors the editor offers: those of the keys of the desk. */
export const NOTE_COLORS = STANDARD_KEY_COLORS;

/**
 * The editor gives text "the usual color" by giving it this one, which the page shows as the usual
 * color and which the notes never keep: a part of the text without a color of its own.
 */
export const USUAL_COLOR_MARK = '#010203';

// The elements the notes keep, with the attributes each keeps. STRONG is kept as B.
const KEPT: Readonly<Record<string, readonly string[]>> = {
  B: [],
  U: [],
  FONT: ['color', 'size'],
  UL: [],
  OL: [],
  LI: [],
  DIV: [],
  P: [],
  BR: [],
};

// STRONG is bold; a block of another kind (a heading, a quote, a row of a table) is a line of its own.
const RENAMED: Readonly<Record<string, string>> = {
  STRONG: 'B',
  ...Object.fromEntries(
    ['H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'SECTION', 'ARTICLE', 'ASIDE', 'HEADER', 'FOOTER', 'MAIN', 'NAV', 'BLOCKQUOTE', 'PRE', 'FIGURE', 'FIGCAPTION', 'ADDRESS', 'TR', 'DT', 'DD'].map(
      (tag) => [tag, 'DIV'],
    ),
  ),
};

// Elements that are no text to read: they go with all they hold.
const DROPPED = new Set([
  'SCRIPT',
  'STYLE',
  'TEMPLATE',
  'NOSCRIPT',
  'IFRAME',
  'FRAME',
  'OBJECT',
  'EMBED',
  'SVG',
  'MATH',
  'HEAD',
  'TITLE',
  'META',
  'LINK',
  'IMG',
  'PICTURE',
  'VIDEO',
  'AUDIO',
  'CANVAS',
  'INPUT',
  'TEXTAREA',
  'SELECT',
  'BUTTON',
]);

/**
 * The notes with only the marks of the editor: what the page shows and keeps. Empty when they hold
 * no text, as after everything was deleted.
 */
export function cleanNotes(html: string): string {
  if (!html) return '';
  const parsed = new DOMParser().parseFromString(`<!doctype html><body>${html}`, 'text/html');
  const clean = parsed.createElement('div');
  copyInto(parsed.body, clean, parsed);
  return (clean.textContent ?? '').trim() === '' ? '' : clean.innerHTML;
}

// Copies what the notes keep of `source` into `target`.
function copyInto(source: Node, target: Element, document: Document): void {
  for (const child of Array.from(source.childNodes)) {
    if (child.nodeType === Node.TEXT_NODE) {
      target.append(document.createTextNode(child.textContent ?? ''));
      continue;
    }
    if (child.nodeType !== Node.ELEMENT_NODE) continue; // a comment, or the like

    const element = child as Element;
    const tag = element.tagName.toUpperCase();
    if (DROPPED.has(tag)) continue;

    const name = RENAMED[tag] ?? tag;
    const attributes = KEPT[name];
    if (name === 'BR') {
      target.append(document.createElement('BR'));
      continue;
    }

    const copy = attributes === undefined ? null : document.createElement(name);
    for (const attribute of attributes ?? []) {
      const value = cleanAttribute(attribute, element.getAttribute(attribute));
      if (value !== null) copy?.setAttribute(attribute, value);
    }

    if (copy === null || (name === 'FONT' && copy.attributes.length === 0)) {
      // Another element, or a font mark that marks nothing the notes keep: its text stays, with
      // the marks its style gives (a browser writes some of them this way).
      copyInto(element, styleMarks(element, target, document), document);
      continue;
    }
    copyInto(element, styleMarks(element, copy, document), document);
    target.append(copy);
  }
}

// A color or a size as the notes keep them; null for none, or for one they do not keep.
function cleanAttribute(attribute: string, value: string | null): string | null {
  if (value === null) return null;
  if (attribute === 'size') {
    const size = value.trim();
    return /^[1-7]$/.test(size) && size !== USUAL_SIZE ? size : null;
  }
  const color = hexColor(value);
  return color === null || color === USUAL_COLOR_MARK ? null : color;
}

/** A color as "#rrggbb": from "#rgb", "#rrggbb" or "rgb(r, g, b)"; null for anything else. */
export function hexColor(value: string): string | null {
  const text = value.trim().toLowerCase();
  if (/^#[0-9a-f]{6}$/.test(text)) return text;
  if (/^#[0-9a-f]{3}$/.test(text)) return `#${[...text.slice(1)].map((digit) => digit + digit).join('')}`;
  const rgb = /^rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*(?:,\s*[\d.]+\s*)?\)$/.exec(text);
  if (!rgb) return null;
  const parts = rgb.slice(1, 4).map(Number);
  if (parts.some((part) => part > 255)) return null;
  return `#${parts.map((part) => part.toString(16).padStart(2, '0')).join('')}`;
}

// The marks that an element's style gives: bold, underline, a color. They are put around its
// content: the element returned is where that content goes (`target` when there are none).
function styleMarks(element: Element, target: Element, document: Document): Element {
  const style = (element as HTMLElement).style;
  if (!style || style.length === 0) return target;

  let inner = target;
  const wrap = (name: string, attribute?: [string, string]): void => {
    const mark = document.createElement(name);
    if (attribute) mark.setAttribute(attribute[0], attribute[1]);
    inner.append(mark);
    inner = mark;
  };

  const weight = style.fontWeight;
  if (weight === 'bold' || weight === 'bolder' || Number(weight) >= 600) wrap('B');
  const line = `${style.textDecorationLine ?? ''} ${style.textDecoration ?? ''}`;
  if (line.includes('underline')) wrap('U');
  const color = style.color ? hexColor(style.color) : null;
  if (color !== null && color !== USUAL_COLOR_MARK) wrap('FONT', ['color', color]);
  return inner;
}
