"""A very small SVG writer for the drawings of dmxbox 2, and the PNG and PDF files made from them."""
from __future__ import annotations

from pathlib import Path

FONT = "Arial, Helvetica, sans-serif"
INK, GREY = "#1f2328", "#6b7280"


def esc(text):
    return str(text).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


class Svg:
    def __init__(self, width, height):
        self.width, self.height = width, height
        self.items = [
            f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" '
            f'width="{width}" height="{height}" font-family="{FONT}">',
            f'<rect width="{width}" height="{height}" fill="white"/>',
        ]

    def raw(self, item):
        self.items.append(item)

    def line(self, x1, y1, x2, y2, stroke=INK, width=2.0, dash=None, cap="round"):
        dash = f' stroke-dasharray="{dash}"' if dash else ""
        self.raw(f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{stroke}" '
                 f'stroke-width="{width}" stroke-linecap="{cap}"{dash}/>')

    def path(self, points, stroke=INK, width=2.0, fill="none", dash=None, opacity=None, close=False):
        d = "M" + " L".join(f"{x:.1f},{y:.1f}" for x, y in points) + (" Z" if close else "")
        extra = (f' stroke-dasharray="{dash}"' if dash else "") + (f' opacity="{opacity}"' if opacity else "")
        self.raw(f'<path d="{d}" fill="{fill}" stroke="{stroke}" stroke-width="{width}" '
                 f'stroke-linejoin="round" stroke-linecap="round"{extra}/>')

    def rect(self, x, y, w, h, fill="none", stroke="none", width=1.0, rx=0, opacity=None, dash=None):
        extra = (f' opacity="{opacity}"' if opacity else "") + (f' stroke-dasharray="{dash}"' if dash else "")
        self.raw(f'<rect x="{x:.1f}" y="{y:.1f}" width="{w:.1f}" height="{h:.1f}" rx="{rx}" fill="{fill}" '
                 f'stroke="{stroke}" stroke-width="{width}"{extra}/>')

    def circle(self, x, y, r, fill="none", stroke="none", width=1.0, opacity=None):
        extra = f' opacity="{opacity}"' if opacity else ""
        self.raw(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{r}" fill="{fill}" stroke="{stroke}" '
                 f'stroke-width="{width}"{extra}/>')

    def text(self, x, y, text, size=12.0, anchor="start", weight="normal", fill=INK, italic=False,
             rotate=None, halo=False):
        attrs = f'x="{x:.1f}" y="{y:.1f}" font-size="{size}" text-anchor="{anchor}" font-weight="{weight}"'
        if italic:
            attrs += ' font-style="italic"'
        if rotate is not None:
            attrs += f' transform="rotate({rotate} {x:.1f} {y:.1f})"'
        if halo:
            self.raw(f'<text {attrs} fill="white" stroke="white" stroke-width="3.5" '
                     f'stroke-linejoin="round">{esc(text)}</text>')
        self.raw(f'<text {attrs} fill="{fill}">{esc(text)}</text>')

    def save(self, path):
        Path(path).write_text("\n".join(self.items + ["</svg>"]), encoding="utf-8")


def to_png_and_pdf(folder, names, pdf_name):
    """PNG files of the SVG files, and one PDF with all of them. Needs resvg-py and pillow."""
    try:
        import resvg_py
        from PIL import Image
    except ImportError:
        print("SVG files written. For the PNG and PDF files run it with: --with resvg-py --with pillow")
        return False
    pages = []
    for name in names:
        png = Path(folder) / f"{name}.png"
        png.write_bytes(bytes(resvg_py.svg_to_bytes(svg_path=str(Path(folder) / f"{name}.svg"), zoom=2.0)))
        pages.append(Image.open(png).convert("RGB"))
    pages[0].save(Path(folder) / pdf_name, save_all=True, append_images=pages[1:], resolution=200.0)
    return True
