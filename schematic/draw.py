"""Draws the hardware drawings of the USB dmxbox: the schematic and the breadboard plan.

Run from the repository root:

    uv run --no-project --python 3.13 --with resvg-py --with pillow python schematic/draw.py

It writes dmxbox_schematic.svg/.png, dmxbox_breadboard.svg/.png and dmxbox_drawings.pdf next to
this file. Add --md to also print the wire list as a Markdown table (the one in DEV.md).

Before it draws the breadboard, the script checks the plan: every leg and every wire end sits in
a hole of its own, no wire end is under a part, and the holes joined by the board and the wires
give exactly the nets of the schematic (NETS below). A mistake stops the script with a message.
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
FONT = "Arial, Helvetica, sans-serif"
INK, GREY = "#1f2328", "#6b7280"


def esc(text):
    return str(text).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


class Svg:
    def __init__(self, width, height):
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

    def rect(self, x, y, w, h, fill="none", stroke="none", width=1.0, rx=0, opacity=None):
        extra = f' opacity="{opacity}"' if opacity else ""
        self.raw(f'<rect x="{x:.1f}" y="{y:.1f}" width="{w:.1f}" height="{h:.1f}" rx="{rx}" fill="{fill}" '
                 f'stroke="{stroke}" stroke-width="{width}"{extra}/>')

    def circle(self, x, y, r, fill="none", stroke="none", width=1.0):
        self.raw(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{r}" fill="{fill}" stroke="{stroke}" stroke-width="{width}"/>')

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


# =====================================================================================
# The circuit: which pins belong to one wire ("net"). Both drawings follow this list.
# =====================================================================================
NETS = {
    "VSYS":    ["U1.39", "U4.1", "C4.+"],
    "GND":     ["U1.3", "U1.8", "U1.13", "U1.18", "U1.23", "U1.28", "U1.33", "U1.38", "U4.2", "C4.-"],
    "3V3":     ["U1.36", "R3.1"],
    "LED_A":   ["R3.2", "U3.2"],
    "DMX_TX":  ["U1.1", "U3.3"],
    "BTN1":    ["U1.24"],            # plus button SW1, checked separately
    "BTN2":    ["U1.25"],            # plus button SW2
    "+5V_ISO": ["U4.7", "C3.+", "R2.1", "U3.8", "U3.7", "C1.1", "R4.1", "U5.8", "U5.2", "U5.3", "C2.1"],
    "GND2":    ["U4.5", "C3.-", "R2.2", "U3.5", "C1.2", "U5.5", "C2.2", "D1.2", "D3.2", "J1.1"],
    "DI":      ["U3.6", "R4.2", "U5.4"],
    "A":       ["U5.6", "D1.1", "J1.3"],
    "B":       ["U5.7", "D3.1", "J1.2"],
}
BUTTONS = {"SW1": "BTN1", "SW2": "BTN2"}          # each button joins its net to GND when pressed


# =====================================================================================
# Schematic
# =====================================================================================
def draw_schematic(path):
    width, height = 1560, 880
    s = Svg(width, height)
    wire_c, part_c, iso_c = "#16794a", "#8b1e1e", "#1d4ed8"
    gap_x = 770

    s.rect(gap_x, 78, width - 10 - gap_x, 617, fill="#f1f7ff")
    s.rect(10, 10, width - 20, height - 20, stroke="#9ca3af", width=1.5)
    s.text(30, 45, "dmxbox: USB to DMX512 interface", 22, weight="bold")
    s.text(30, 64, "USB version  ·  Raspberry Pi Pico as a USB network adapter, isolated RS-485 driver  ·  "
                   "drawn 2026-11-18", 13, fill=GREY)
    s.text(400, 103, "PICO SIDE  ·  GND", 13, "middle", "bold", GREY)
    s.text(1180, 103, "ISOLATED DMX SIDE  ·  GND2", 13, "middle", "bold", iso_c)
    s.line(gap_x, 78, gap_x, 695, stroke=iso_c, width=1.5, dash="7 6")
    s.text(gap_x, 318, "isolation gap", 11, "middle", fill=iso_c, italic=True, halo=True)
    s.text(gap_x, 332, "only U3 and U4 cross it", 11, "middle", fill=iso_c, italic=True, halo=True)

    def wire(*points):
        s.path(points, stroke=wire_c, width=2)

    def dot(x, y):
        s.circle(x, y, 4, fill=wire_c)

    def gnd(x, y, iso=False, label="right"):
        color = iso_c if iso else INK
        name = "GND2" if iso else "GND"
        s.line(x, y, x, y + 10, wire_c)
        for i, half in enumerate((11, 7, 3)):
            s.line(x - half, y + 10 + 4 * i, x + half, y + 10 + 4 * i, color, 2)
        if label == "right":
            s.text(x + 15, y + 18, name, 11, weight="bold", fill=color)
        elif label == "left":
            s.text(x - 15, y + 18, name, 11, "end", "bold", color)
        elif label == "below":
            s.text(x, y + 34, name, 11, "middle", "bold", color)

    def flag(x, y, side="up"):
        w, h = 62, 18
        if side == "up":
            s.line(x, y, x, y - 14, wire_c)
            bx, by = x - w / 2, y - 14 - h
        else:
            s.line(x, y, x + 14, y, wire_c)
            bx, by = x + 14, y - h / 2
        s.rect(bx, by, w, h, fill="#fff4e5", stroke="#a2470a", width=1.4, rx=2)
        s.text(bx + w / 2, by + 13, "+5V_ISO", 11.5, "middle", "bold", "#a2470a")

    def res_h(x1, x2, y, ref, value):
        mid = (x1 + x2) / 2
        wire((x1, y), (mid - 24, y))
        wire((mid + 24, y), (x2, y))
        s.rect(mid - 24, y - 8, 48, 16, fill="white", stroke=part_c, width=2)
        s.text(mid, y - 14, f"{ref}  {value}", 12, "middle", "bold", part_c)

    def res_v(x, y1, y2, ref, value, side="right"):
        mid = (y1 + y2) / 2
        wire((x, y1), (x, mid - 24))
        wire((x, mid + 24), (x, y2))
        s.rect(x - 8, mid - 24, 16, 48, fill="white", stroke=part_c, width=2)
        tx, anchor = (x + 15, "start") if side == "right" else (x - 15, "end")
        s.text(tx, mid - 2, ref, 12, anchor, "bold", part_c)
        s.text(tx, mid + 12, value, 11, anchor, fill=part_c)

    def cap_v(x, y1, y2, ref, value, polar=False):
        mid = (y1 + y2) / 2
        wire((x, y1), (x, mid - 4))
        wire((x, mid + 4), (x, y2))
        s.line(x - 12, mid - 4, x + 12, mid - 4, part_c, 2.4)
        s.line(x - 12, mid + 4, x + 12, mid + 4, part_c, 2.4)
        if polar:
            s.text(x - 16, mid - 7, "+", 14, "end", "bold", part_c)
        s.text(x + 18, mid - 2, ref, 12, weight="bold", fill=part_c)
        s.text(x + 18, mid + 12, value, 11, fill=part_c)

    def tvs_v(x, y1, y2, ref):                      # protection diode that works in both directions
        mid = (y1 + y2) / 2
        wire((x, y1), (x, mid - 12))
        wire((x, mid + 12), (x, y2))
        s.path([(x - 8, mid - 12), (x + 8, mid - 12), (x, mid)], stroke=part_c, width=2, fill="white", close=True)
        s.path([(x - 8, mid + 12), (x + 8, mid + 12), (x, mid)], stroke=part_c, width=2, fill="white", close=True)
        s.path([(x - 13, mid - 4), (x - 10, mid), (x + 10, mid), (x + 13, mid + 4)], stroke=part_c, width=2)
        s.text(x + 15, mid + 4, ref, 12, weight="bold", fill=part_c)

    def switch_h(x1, x2, y, label):
        a, b = (x1 + x2) / 2 - 25, (x1 + x2) / 2 + 25
        wire((x1, y), (a, y))
        wire((b, y), (x2, y))
        s.line(a, y, b - 3, y - 13, part_c, 2.2)
        s.circle(a, y, 3.2, fill="white", stroke=part_c, width=2)
        s.circle(b, y, 3.2, fill="white", stroke=part_c, width=2)
        s.text((x1 + x2) / 2, y - 20, label, 12, "middle", "bold", part_c)

    def box(x, y, w, h, cx, cy, ref, name, note):
        s.rect(x, y, w, h, fill="#fffaf0", stroke=part_c, width=2.2, rx=3)
        s.text(cx, cy, ref, 15, "middle", "bold", part_c)
        s.text(cx, cy + 16, name, 13, "middle", fill=part_c)
        s.text(cx, cy + 33, note, 11, "middle", fill=GREY, italic=True)

    def pin(side, x, y, number, name, unused=False):
        if side == "R":
            s.line(x, y, x + 40, y, part_c)
            s.text(x - 6, y + 4, name, 12, "end")
            s.text(x + 20, y - 5, number, 11, "middle", fill=GREY)
            end = (x + 40, y)
        elif side == "L":
            s.line(x - 40, y, x, y, part_c)
            s.text(x + 6, y + 4, name, 12)
            s.text(x - 20, y - 5, number, 11, "middle", fill=GREY)
            end = (x - 40, y)
        elif side == "T":
            s.line(x, y - 30, x, y, part_c)
            s.text(x, y + 15, name, 12, "middle")
            s.text(x + 6, y - 10, number, 11, fill=GREY)
            end = (x, y - 30)
        else:
            s.line(x, y, x, y + 30, part_c)
            s.text(x, y - 7, name, 12, "middle")
            s.text(x + 6, y + 18, number, 11, fill=GREY)
            end = (x, y + 30)
        if unused:
            ex, ey = end
            s.path([(ex - 5, ey - 5), (ex + 5, ey + 5)], stroke="#b91c1c", width=2)
            s.path([(ex - 5, ey + 5), (ex + 5, ey - 5)], stroke="#b91c1c", width=2)

    # ---- U1: the Pico and its USB socket
    box(130, 150, 200, 490, 230, 300, "U1", "Raspberry Pi Pico", "plain Pico, RP2040")
    s.rect(70, 226, 60, 48, fill="#e5e7eb", stroke=INK, width=1.5, rx=3)
    s.rect(80, 238, 40, 24, fill="white", stroke=INK, width=1.2)
    s.text(100, 254, "USB", 11, "middle", "bold")
    s.text(80, 218, "micro-USB", 11, "middle", fill=GREY)
    s.text(76, 292, "to the computer:", 11, "middle", fill=GREY)
    s.text(76, 306, "5 V and data", 11, "middle", fill=GREY)
    pin("R", 330, 190, "39", "VSYS")
    pin("R", 330, 390, "36", "3V3(OUT)")
    pin("R", 330, 450, "1", "GP0")
    pin("R", 330, 540, "24", "GP18")
    pin("R", 330, 600, "25", "GP19")
    pin("B", 230, 640, "any GND pin: 3, 8, 13, 18, 23, 28, 33, 38", "GND")
    gnd(230, 670)

    # ---- U4: isolated DC-DC converter
    box(680, 150, 180, 130, 770, 212, "U4", "RKK-0505S/H", "")
    s.text(770, 268, "isolated 5 V supply", 11, "middle", fill=GREY, italic=True)
    pin("L", 680, 190, "1", "+Vin")
    pin("L", 680, 240, "2", "−Vin")
    pin("R", 860, 190, "7", "+Vout")
    pin("R", 860, 240, "5", "−Vout")
    wire((370, 190), (640, 190))
    s.text(445, 182, "about 4.7 V", 11, "middle", fill=GREY)
    dot(520, 190)
    cap_v(520, 190, 256, "C4", "4.7 µF", polar=True)
    gnd(520, 256)
    wire((640, 240), (600, 240), (600, 256))
    gnd(600, 256)
    wire((900, 190), (1075, 190))
    dot(965, 190)
    cap_v(965, 190, 256, "C3", "10 µF", polar=True)
    gnd(965, 256, iso=True, label="below")
    dot(1035, 190)
    res_v(1035, 190, 256, "R2", "270 Ω")
    gnd(1035, 256, iso=True, label="below")
    flag(1075, 190, "right")
    wire((900, 240), (918, 240), (918, 256))
    gnd(918, 256, iso=True, label="below")

    # ---- U3: optocoupler
    box(680, 360, 180, 170, 770, 482, "U3", "6N137", "optocoupler")
    pin("L", 680, 390, "2", "anode")
    pin("L", 680, 450, "3", "cathode")
    pin("R", 860, 390, "8", "VCC")
    pin("R", 860, 420, "7", "VE")
    pin("R", 860, 450, "5", "GND")
    pin("R", 860, 500, "6", "VO")
    res_h(370, 640, 390, "R3", "180 Ω")
    wire((370, 450), (640, 450))
    s.text(505, 442, "DMX data: low = LED lit", 11, "middle", fill=GREY)
    wire((900, 390), (985, 390))
    wire((900, 420), (930, 420), (930, 390))
    dot(930, 390)
    flag(930, 390)
    cap_v(985, 390, 450, "C1", "100 nF")
    wire((900, 450), (985, 450))
    gnd(985, 450, iso=True, label="left")
    wire((900, 500), (1100, 500))

    # ---- R4 and U5: RS-485 driver
    box(1140, 380, 170, 210, 1225, 478, "U5", "SN75176BP", "or MAX485")
    pin("L", 1140, 410, "3", "DE")
    pin("L", 1140, 440, "2", "/RE")
    pin("L", 1140, 500, "4", "DI")
    pin("L", 1140, 555, "1", "RO", unused=True)
    pin("T", 1225, 380, "8", "VCC")
    pin("B", 1225, 590, "5", "GND")
    pin("R", 1310, 450, "6", "A")
    pin("R", 1310, 550, "7", "B")
    wire((1100, 410), (1060, 410))
    wire((1100, 440), (1085, 440), (1085, 410))
    dot(1085, 410)
    flag(1060, 410)
    res_v(1060, 410, 500, "R4", "1 kΩ", side="left")
    dot(1060, 500)
    dot(1225, 350)
    flag(1225, 350)
    wire((1225, 350), (1345, 350))
    cap_v(1345, 350, 410, "C2", "100 nF")
    gnd(1345, 410, iso=True)
    gnd(1225, 620, iso=True)

    # ---- output: protection diodes and the XLR jack
    wire((1350, 450), (1425, 450))
    wire((1350, 550), (1425, 550))
    wire((1362, 500), (1425, 500))
    tvs_v(1385, 450, 500, "D1")
    tvs_v(1385, 500, 550, "D3")
    for y in (450, 500, 550):
        dot(1385, y)
    gnd(1362, 500, iso=True, label="left")
    s.text(1400, 606, "D1, D3: TVS diodes P6KE6.8CA", 11, "middle", fill=part_c)
    s.rect(1425, 420, 115, 160, fill="#f8fafc", stroke=INK, width=1.8, rx=8)
    s.text(1482, 396, "J1", 13, "middle", "bold", part_c)
    s.text(1482, 411, "XLR 3-pin female", 11, "middle", fill=part_c)
    for y, label in ((450, "3   Data +"), (500, "1   common"), (550, "2   Data −")):
        s.circle(1425, y, 4, fill="white", stroke=INK, width=1.6)
        s.text(1438, y + 4, label, 12, weight="bold")

    # ---- buttons
    switch_h(370, 570, 540, "SW1  blackout")
    switch_h(370, 570, 600, "SW2  preset 1")
    wire((570, 540), (570, 632))
    dot(570, 600)
    gnd(570, 632)

    # ---- notes
    y = 724
    s.text(30, y, "Reading the drawing", 13, weight="bold")
    for line in (
        "Points with the same name are one wire: all +5V_ISO are joined, all GND2 are joined, all GND are joined.",
        "GND (Pico side) and GND2 (isolated side) are never joined. Only U3 and U4 reach across the gap.",
        "Not connected: U3 pins 1 and 4, U5 pin 1, Pico pin 40 (VBUS). Pico pins 21 and 22 carry an optional serial log.",
        "C1 sits right next to U3 and C2 right next to U5. C3 and C4 have a + leg: the longer one.",
        "D1 and D3 are the same part: the TVS diode P6KE6.8CA. It fits either way round. There is no D2 and no Schottky diode.",
        "XLR: pin 3 is Data +, pin 2 is Data −. Do not swap them.",
    ):
        y += 20
        s.text(38, y, "•  " + line, 12)

    s.text(900, 724, "Which pin is which", 13, weight="bold")
    s.text(900, 744, "U3 and U5, seen from above:", 12)
    s.rect(925, 768, 96, 40, fill="#374151", rx=3)
    s.circle(925, 788, 7, fill="white")
    s.circle(937, 799, 3, fill="#d1d5db")
    for i in range(4):
        s.text(940 + i * 22, 763, str(8 - i), 11, "middle", "bold")
        s.text(940 + i * 22, 823, str(1 + i), 11, "middle", "bold")
    s.text(900, 845, "The notch (or dot) marks the pin 1 end.", 11, fill=GREY)
    s.text(1130, 744, "U4, pins pointing down:", 12)
    s.rect(1140, 760, 176, 34, fill="#374151", rx=2)
    for number in (1, 2, 5, 7):
        x = 1156 + (number - 1) * 24
        s.line(x, 794, x, 812, "#6b7280", 3)
        s.text(x, 827, str(number), 11, "middle", "bold")
    s.text(1130, 845, "Pins 1 and 2 are the two that sit together. Pin 7 stands alone.", 11, fill=GREY)
    s.text(1345, 786, "J1: the numbers 1, 2, 3", 11, fill=GREY)
    s.text(1345, 800, "are moulded into the jack", 11, fill=GREY)
    s.text(1345, 814, "next to the solder cups.", 11, fill=GREY)
    s.save(path)


# =====================================================================================
# Breadboard plan
# =====================================================================================
ROWS = 63                                                       # numbered rows of a full-size board
RAIL_ROWS = [row for row in range(3, 62) if (row - 3) % 6 != 5]   # power-line holes come in groups of five
LETTER_Y = {"j": 4, "i": 5, "h": 6, "g": 7, "f": 8, "e": 11, "d": 12, "c": 13, "b": 14, "a": 15}
RAIL_Y = {"T+": 0, "T-": 1}                                     # the two power lines at the top edge

RED, BLACK, AMBER, VIOLET = "#dc2626", "#111827", "#f59e0b", "#7c3aed"
TEAL, LIME, PINK, BLUE, GREEN, BROWN = "#0d9488", "#65a30d", "#db2777", "#2563eb", "#16a34a", "#a16207"

LEGS = {f"U1.{n}": (f"c{n}" if n <= 20 else f"h{41 - n}") for n in range(1, 41)}


def dip8(ref, row):
    """A DIP-8 across the middle gap, notch towards row 1: pins 1-4 in e, pins 5-8 in f."""
    for n in range(1, 5):
        LEGS[f"{ref}.{n}"] = f"e{row + n - 1}"
    for n in range(5, 9):
        LEGS[f"{ref}.{n}"] = f"f{row + 8 - n}"


dip8("U3", 45)
dip8("U5", 52)
LEGS.update({
    "U4.1": "f35", "U4.2": "f36", "U4.5": "f39", "U4.7": "f41",
    "C4.+": "h35", "C4.-": "h36",
    "C3.+": "T+43", "C3.-": "T-43",
    "R2.1": "i45", "R2.2": "i39",
    "C1.1": "h45", "C1.2": "h48",
    "R4.1": "T+47", "R4.2": "i47",
    "R3.1": "b41", "R3.2": "b46",
    "C2.1": "g52", "C2.2": "g55",
    "D1.1": "h57", "D1.2": "T-57",
    "D3.1": "h59", "D3.2": "T-59",
    "J1.3": "g57", "J1.2": "g59", "J1.1": "T-61",              # the three wires of the XLR jack
})
SWITCHES = {"SW1": 24, "SW2": 30}                               # legs in d and g of this row and the row + 2
for _ref, _row in SWITCHES.items():
    for _letter in "dg":
        LEGS[f"{_ref}.{_letter}{_row}"] = f"{_letter}{_row}"
        LEGS[f"{_ref}.{_letter}{_row + 2}"] = f"{_letter}{_row + 2}"

# number, from, to, color, what it does, corner points (row, height), where the number tag sits
WIRES = [
    (1, "j2", "j35", RED, "Pico pin 39 (VSYS, 5 V) to U4 pin 1", [(2, 2.0), (35, 2.0)], (9, 2.0)),
    (2, "j3", "j36", BLACK, "Pico pin 38 (GND) to U4 pin 2", [(3, 2.45), (36, 2.45)], (12, 2.45)),
    (3, "i5", "a41", AMBER, "Pico pin 36 (3V3) to R3", [(5, 4.5), (21.5, 4.5), (21.5, 16.3), (41, 16.3)], (21.5, 9.5)),
    (4, "a1", "a47", VIOLET, "Pico pin 1 (GP0, the DMX data) to U3 pin 3", [(1, 17.1), (47, 17.1)], (10, 17.1)),
    (5, "j17", "j24", TEAL, "Pico pin 24 (GP18) to button SW1", [(17, 3.35), (24, 3.35)], (20.4, 3.35)),
    (6, "j16", "j32", LIME, "Pico pin 25 (GP19) to button SW2", [(16, 2.9), (32, 2.9)], (28, 2.9)),
    (7, "b18", "b26", BLACK, "Pico pin 18 (GND) to button SW1", [(18, 14.5), (26, 14.5)], (23.7, 14.5)),
    (8, "a26", "a30", BLACK, "GND on to button SW2", [(26, 15.6), (30, 15.6)], (28, 15.6)),
    (9, "j39", "T-39", BLUE, "U4 pin 5: isolated ground (GND2)", [], (39, -1.15)),
    (10, "j41", "T+41", PINK, "U4 pin 7: isolated 5 V", [], (41, -1.15)),
    (11, "j45", "T+45", PINK, "U3 pin 8", [], (45, -1.15)),
    (12, "j46", "T+46", PINK, "U3 pin 7", [], (46, -1.15)),
    (13, "j48", "T-48", BLUE, "U3 pin 5", [], (48, -1.15)),
    (14, "g47", "a55", VIOLET, "U3 pin 6 to U5 pin 4 (the DMX data)",
     [(47.5, 7.5), (49.5, 7.5), (49.5, 16.3), (55, 16.3)], (49.5, 12.5)),
    (15, "T+51", "b53", PINK, "isolated 5 V to U5 pin 2", [(50.5, 0.5), (50.5, 14.5), (53, 14.5)], (50.5, 3.0)),
    (16, "c53", "c54", PINK, "U5 pin 2 on to pin 3", [], (55.0, 13.0)),
    (17, "j52", "T+52", PINK, "U5 pin 8", [], (52, -1.15)),
    (18, "j55", "T-55", BLUE, "U5 pin 5", [], (55, -1.15)),
    (19, "i54", "f57", GREEN, "U5 pin 6 (A, Data +) to D1", [(56.4, 5), (56.4, 8)], (55.2, 5)),
    (20, "h53", "f59", BROWN, "U5 pin 7 (B, Data −) to D3", [(55.75, 6), (55.75, 9.3), (59, 9.3)], (57.5, 9.3)),
]
XLR_WIRES = [          # number, hole, XLR pin, color, what, corner points
    (21, "g57", 3, GREEN, "Data +", [(57.6, 7.5), (64.3, 7.5)]),
    (22, "g59", 2, BROWN, "Data −", [(59.6, 6.5), (64.3, 6.5)]),
    (23, "T-61", 1, BLUE, "common", [(64.3, 1.0)]),
]

# Holes that end up under a part. No wire may end there.
BLOCKED = {f"{letter}{row}" for letter in "defg" for row in range(1, 21)}                   # under the Pico
BLOCKED |= {f"{letter}{row}" for start in SWITCHES.values() for letter in "ef" for row in range(start - 1, start + 4)}
BLOCKED |= {"g35", "g36", "i35", "i36"}                                                     # C4
BLOCKED |= {f"i{row}" for row in range(40, 45)} | {"h46", "h47", "T-47", "j47"}             # R2, C1, R4
BLOCKED |= {f"b{row}" for row in range(42, 46)} | {"g53", "g54"}                            # R3, C2
BLOCKED |= {"i57", "j57", "i59", "j59"}                                                     # D1, D3


def strip_of(hole):
    """The contact a hole belongs to: a power line, or five holes in one row above or below the gap."""
    if hole[0] == "T":
        return hole[:2]
    return ("top" if hole[0] in "fghij" else "bottom", int(hole[1:]))


def check_plan():
    problems = []
    used = {}
    for who, hole in list(LEGS.items()) + [(f"wire {w[0]}", end) for w in WIRES for end in w[1:3]]:
        if hole[0] == "T":
            if int(hole[2:]) not in RAIL_ROWS:
                problems.append(f"{who}: the power line has no hole at row {hole[2:]}")
            if int(hole[2:]) < 33:
                problems.append(f"{who}: uses the power line left of the middle (split lines would break it)")
        elif hole[0] not in LETTER_Y or not 1 <= int(hole[1:]) <= ROWS:
            problems.append(f"{who}: no such hole {hole}")
        if hole in used:
            problems.append(f"{who} and {used[hole]} share hole {hole}")
        if hole in BLOCKED:
            problems.append(f"{who}: hole {hole} is under a part")
        used[hole] = who

    button_legs = {pin for pin in LEGS if pin.split(".")[0] in SWITCHES}
    listed = {pin for pins in NETS.values() for pin in pins}
    for pin in listed - set(LEGS):
        problems.append(f"{pin} is in the net list but not on the board")

    # Two ways a 4-leg button can be joined inside. The plan has to work with both.
    for model in ("legs in the same row are joined", "legs on the same side of the gap are joined"):
        parent = {}

        def find(item):
            parent.setdefault(item, item)
            while parent[item] != item:
                parent[item] = parent[parent[item]]
                item = parent[item]
            return item

        def join(a, b):
            parent[find(a)] = find(b)

        for wire in WIRES:
            join(strip_of(wire[1]), strip_of(wire[2]))
        for pin in NETS["GND"]:                                 # the Pico joins its ground pins itself
            if pin.startswith("U1."):
                join(strip_of(LEGS[pin]), strip_of(LEGS["U1.38"]))
        for row in SWITCHES.values():
            if model.startswith("legs in the same row"):
                join(("top", row), ("bottom", row))
                join(("top", row + 2), ("bottom", row + 2))
            else:
                join(("top", row), ("top", row + 2))
                join(("bottom", row), ("bottom", row + 2))

        groups = {}
        for pin, hole in LEGS.items():
            if pin not in button_legs:
                groups.setdefault(find(strip_of(hole)), set()).add(pin)
        for name, pins in NETS.items():
            roots = {find(strip_of(LEGS[pin])) for pin in pins if pin in LEGS}
            if len(roots) != 1:
                problems.append(f"[{model}] net {name} is in {len(roots)} pieces")
            elif groups[roots.pop()] != set(pins):
                problems.append(f"[{model}] net {name} is joined to something else")
        for root, pins in groups.items():
            if not pins & listed and len(pins) > 1:
                problems.append(f"[{model}] unconnected pins are joined: {sorted(pins)}")
        for ref, net in BUTTONS.items():
            signal = find(strip_of(LEGS[NETS[net][0]]))
            ground = find(strip_of(LEGS["U1.38"]))
            legs = {find(strip_of(LEGS[pin])) for pin in button_legs if pin.startswith(ref + ".")}
            if signal == ground or legs != {signal, ground}:
                problems.append(f"[{model}] button {ref} does not sit between {net} and GND")
    return problems


def hole_name(hole):
    if hole.startswith("T+"):
        return "red line"
    if hole.startswith("T-"):
        return "blue line"
    return hole


def draw_breadboard(path):
    pitch, x0, y0 = 20, 140, 172
    width, height = 1630, 980
    s = Svg(width, height)

    def X(row):
        return x0 + (row - 1) * pitch

    def Y(units):
        return y0 + units * pitch

    def at(spot):
        if isinstance(spot, tuple):
            return X(spot[0]), Y(spot[1])
        if spot[0] == "T":
            return X(int(spot[2:])), Y(RAIL_Y[spot[:2]])
        return X(int(spot[1:])), Y(LETTER_Y[spot[0]])

    s.text(30, 45, "dmxbox on a breadboard", 22, weight="bold")
    s.text(30, 66, "Full-size board: 830 holes, rows numbered 1 to 63. Lay it with row 1 at the left and the letters "
                   "a to e at the bottom. The five holes of one row, above or below the middle gap, are one contact.",
           13, fill=GREY)
    s.text(30, 84, "If the letters on your board run the other way, go by the picture. "
                   "Wire colors only tell the wires apart in the picture: any wire will do.", 13, fill=GREY)

    # ---- the board
    s.rect(X(1) - 30, Y(0) - 18, X(ROWS) - X(1) + 60, Y(19) - Y(0) + 36, fill="#f6f5ef", stroke="#c9c8bf",
           width=1.5, rx=10)
    zone = [(X(0.2), Y(0) - 16), (X(63.8), Y(0) - 16), (X(63.8), Y(15) + 13), (X(49.0), Y(15) + 13),
            (X(49.0), Y(9.5)), (X(37.5), Y(9.5)), (X(37.5), Y(1) + 16), (X(0.2), Y(1) + 16)]
    s.path(zone, stroke="none", fill="#cfe1ff", opacity=0.6, close=True)
    s.path(zone[3:7], stroke="#1d4ed8", width=1.6, dash="7 5")
    s.line(X(0.5), Y(0) - 10, X(63.5), Y(0) - 10, "#ef4444", 2)
    s.line(X(0.5), Y(1) + 10, X(63.5), Y(1) + 10, "#3b82f6", 2)
    s.line(X(0.5), Y(18) - 10, X(63.5), Y(18) - 10, "#f3b5b5", 2)
    s.line(X(0.5), Y(19) + 10, X(63.5), Y(19) + 10, "#b9cdf5", 2)
    s.text(X(1) - 14, Y(0) + 4, "+", 13, "middle", "bold", "#ef4444")
    s.text(X(1) - 14, Y(1) + 5, "−", 13, "middle", "bold", "#3b82f6")
    for row in range(1, ROWS + 1):
        for letter, units in LETTER_Y.items():
            s.rect(X(row) - 2.5, Y(units) - 2.5, 5, 5, fill="#a8a79f", rx=1)
        if row in RAIL_ROWS:
            for units, color in ((0, "#a8a79f"), (1, "#a8a79f"), (18, "#d4d3cc"), (19, "#d4d3cc")):
                s.rect(X(row) - 2.5, Y(units) - 2.5, 5, 5, fill=color, rx=1)
        if row == 1 or row % 5 == 0:                            # row numbers: in the gap and as rulers outside
            s.text(X(row), Y(9.5) + 4, str(row), 10, "middle", fill="#8b8a82")
            for ruler_y, tick_y in ((Y(0) - 40, Y(0) - 36), (Y(19) + 33, Y(19) + 19)):
                s.text(X(row), ruler_y, str(row), 10.5, "middle", "bold", "#6b6a63")
                s.line(X(row), tick_y, X(row), tick_y + 4, "#6b6a63", 1.2)
    for letter, units in LETTER_Y.items():
        s.text(X(ROWS) + 17, Y(units) + 4, letter, 11, "middle", fill="#8b8a82")
        if letter in "jihcba":
            s.text(X(1) - 17, Y(units) + 4, letter, 11, "middle", fill="#8b8a82")
    s.text(X(2), Y(18.5) + 4, "bottom power lines: leave them empty", 11, fill="#8b8a82", halo=True)
    s.text(X(7), Y(0.5) + 4, "top power lines = isolated side:  red (+) is +5V_ISO,  blue (−) is GND2.  "
                             "Nothing from the Pico goes in here.", 11, fill="#1d4ed8", weight="bold", halo=True)

    # ---- U1: the Pico
    s.rect(X(0.46), Y(5.37), X(20.54) - X(0.46), Y(13.63) - Y(5.37), fill="#1e7d4f", stroke="#145a38", width=1.5, rx=4)
    s.rect(X(0.46) - 8, Y(8.0), 34, Y(11.0) - Y(8.0), fill="#c3c7cc", stroke="#6b7280", width=1.2, rx=2)
    s.path([(X(0.46) - 8, Y(9.5)), (30, Y(9.5))], stroke="#4b5563", width=9)
    s.text(30, Y(9.5) - 12, "USB cable", 11, fill=GREY)
    s.text(30, Y(9.5) + 24, "to the computer", 11, fill=GREY)
    for n in range(1, 41):
        px, py = at(LEGS[f"U1.{n}"])
        s.circle(px, py, 4.2, fill="#e2c15a", stroke="#8a6d1c", width=1)
    s.text(X(10.5), Y(9.5) - 4, "U1  Raspberry Pi Pico", 14, "middle", "bold", "white")
    s.text(X(10.5), Y(9.5) + 13, "pin 1 next to the USB socket, at the bottom", 10.5, "middle", fill="#d7f0e2")
    for n, name in ((39, "VSYS"), (38, "GND"), (36, "3V3"), (25, "GP19"), (24, "GP18")):
        px, py = at(LEGS[f"U1.{n}"])
        s.text(px - 4, py + 9, f"{n} {name}", 9.5, fill="white", rotate=90)
    for n, name in ((1, "GP0"), (18, "GND")):
        px, py = at(LEGS[f"U1.{n}"])
        s.text(px + 4, py - 9, f"{n} {name}", 9.5, fill="white", rotate=-90)

    # ---- buttons
    for ref, row in SWITCHES.items():
        cx, cy, half = X(row + 1), Y(9.5), 2.36 * pitch
        for leg in (f"d{row}", f"g{row}", f"d{row + 2}", f"g{row + 2}"):
            px, py = at(leg)
            s.rect(px - 3, py - 4, 6, 8, fill="#c3c7cc", stroke="#6b7280", width=0.8)
        s.rect(cx - half, cy - half, 2 * half, 2 * half, fill="#2b2b2b", stroke="#111", width=1, rx=4)
        s.circle(cx, cy, 1.5 * pitch, fill="#565656", stroke="#111", width=1)
        s.text(cx, cy + 4, ref, 12, "middle", "bold", "white")

    # ---- chips
    def chip(ref, name, row):
        for n in range(1, 9):
            px, py = at(LEGS[f"{ref}.{n}"])
            s.rect(px - 3, min(py, Y(9.5)) if n > 4 else Y(10.75), 6, abs(py - Y(8.25 if n > 4 else 10.75)) + 3,
                   fill="#c3c7cc", stroke="#6b7280", width=0.8)
        s.rect(X(row - 0.43), Y(8.25), X(row + 3.43) - X(row - 0.43), Y(10.75) - Y(8.25), fill="#2a2a2a", rx=2)
        s.circle(X(row - 0.43), Y(9.5), 6, fill="#f6f5ef")
        s.circle(X(row - 0.05), Y(10.4), 2.6, fill="#d1d5db")
        s.text(X(row + 1.6), Y(9.5) - 2, ref, 12, "middle", "bold", "white")
        s.text(X(row + 1.6), Y(9.5) + 12, name, 9.5, "middle", fill="#e5e7eb")

    chip("U3", "6N137", 45)
    chip("U5", "SN75176", 52)

    # ---- U4: its pins sit near one long edge, the body hangs over the middle gap
    s.rect(X(34.13), Y(7.51), X(41.87) - X(34.13), Y(9.87) - Y(7.51), fill="#1b1b1b", rx=2)
    for n in (1, 2, 5, 7):
        px, py = at(LEGS[f"U4.{n}"])
        s.circle(px, py, 4, fill="#e2c15a", stroke="#8a6d1c", width=1)
        s.text(px, py + 16, str(n), 10, "middle", "bold", "white")
    s.text(X(38), Y(9.55), "U4  RKK-0505S/H", 10.5, "middle", "bold", "white")

    # ---- parts with two legs
    def two_legs(a, b, kind, label):
        (x1, y1), (x2, y2) = at(a), at(b)
        s.line(x1, y1, x2, y2, "#8d8d8d", 2.4)
        angle = math.degrees(math.atan2(y2 - y1, x2 - x1))
        if angle > 90 or angle <= -90:
            angle += 180
        length, thick, fill, ink, shift = {
            "resistor": (50, 17, "#e3cfa4", "#3b2f12", 0),
            "ceramic": (30, 17, "#e9a23b", "#3b2f12", 0),
            "diode": (52, 24, "#3d3d3d", "white", -10),       # drawn a little towards the power line
        }[kind]
        mx, my = (x1 + x2) / 2, (y1 + y2) / 2 + shift
        s.raw(f'<g transform="translate({mx:.1f} {my:.1f}) rotate({angle:.1f})">'
              f'<rect x="{-length / 2}" y="{-thick / 2}" width="{length}" height="{thick}" rx="{thick / 2.6:.1f}" '
              f'fill="{fill}" stroke="#222" stroke-width="1"/>'
              f'<text x="0" y="3.5" font-size="9.5" font-weight="bold" text-anchor="middle" fill="{ink}">'
              f'{esc(label)}</text></g>')
        for px, py in ((x1, y1), (x2, y2)):
            s.circle(px, py, 3.2, fill="#8d8d8d", stroke="#444", width=0.8)

    def electrolytic(plus, minus, label):
        (x1, y1), (x2, y2) = at(plus), at(minus)
        cx, cy, r = (x1 + x2) / 2, (y1 + y2) / 2, 0.98 * pitch
        angle = math.atan2(y2 - y1, x2 - x1)                     # points at the minus leg
        s.circle(cx, cy, r, fill="#22344b", stroke="#111", width=1)
        a1, a2 = angle - math.radians(52), angle + math.radians(52)
        s.raw(f'<path d="M{cx + r * math.cos(a1):.1f},{cy + r * math.sin(a1):.1f} A{r:.1f},{r:.1f} 0 0 1 '
              f'{cx + r * math.cos(a2):.1f},{cy + r * math.sin(a2):.1f} Z" fill="#cbd5e1"/>')
        s.circle(x1, y1, 3, fill="#e2c15a")
        s.circle(x2, y2, 3, fill="#6b7280")
        s.text(cx - 13 * math.cos(angle), cy - 13 * math.sin(angle) + 4, "+", 12, "middle", "bold", "white")
        s.text(cx - 26 * math.sin(angle) * 0 + (r + 4) * math.cos(angle - math.pi / 2),
               cy + (r + 4) * math.sin(angle - math.pi / 2) + 4, label, 10, "middle", "bold", "#22344b", halo=True)

    two_legs(LEGS["R2.2"], LEGS["R2.1"], "resistor", "R2 270Ω")
    two_legs(LEGS["R3.1"], LEGS["R3.2"], "resistor", "R3 180Ω")
    two_legs(LEGS["R4.1"], LEGS["R4.2"], "resistor", "R4 1kΩ")
    two_legs(LEGS["C1.1"], LEGS["C1.2"], "ceramic", "C1")
    two_legs(LEGS["C2.1"], LEGS["C2.2"], "ceramic", "C2")
    two_legs(LEGS["D1.2"], LEGS["D1.1"], "diode", "D1")
    two_legs(LEGS["D3.2"], LEGS["D3.1"], "diode", "D3")
    electrolytic(LEGS["C4.+"], LEGS["C4.-"], "C4")
    electrolytic(LEGS["C3.+"], LEGS["C3.-"], "C3")

    # ---- the XLR jack, off the board
    jack_x, pins_y = 1470, {1: Y(4.2), 2: Y(6.4), 3: Y(8.2)}
    s.rect(jack_x, Y(3.2), 130, Y(9.2) - Y(3.2), fill="#f1f5f9", stroke=INK, width=1.8, rx=12)
    s.text(jack_x + 65, Y(3.2) - 22, "J1  XLR jack", 13, "middle", "bold")
    s.text(jack_x + 65, Y(3.2) - 8, "solder the three wires on", 11, "middle", fill=GREY)
    for number, label in ((1, "common"), (2, "Data −"), (3, "Data +")):
        s.text(jack_x + 16, pins_y[number] + 4, f"{number}   {label}", 12, weight="bold")

    # ---- wires
    def tag(number, spot, color):
        tx, ty = at(spot)
        s.circle(tx, ty, 7.5, fill="white", stroke=color, width=1.6)
        s.text(tx, ty + 3.4, str(number), 9.5, "middle", "bold")

    for number, a, b, color, _, corners, tag_at in WIRES:
        points = [at(a)] + [at(corner) for corner in corners] + [at(b)]
        s.path(points, stroke="#1f2328", width=4.6)
        s.path(points, stroke=color, width=3)
        for px, py in (points[0], points[-1]):
            s.circle(px, py, 4.3, fill=color, stroke="#1f2328", width=1)
    for number, a, pin_number, color, _, corners in XLR_WIRES:
        points = [at(a)] + [at(corner) for corner in corners] + [(jack_x, pins_y[pin_number])]
        s.path(points, stroke="#1f2328", width=4.6)
        s.path(points, stroke=color, width=3)
        s.circle(*points[0], 4.3, fill=color, stroke="#1f2328", width=1)
        s.circle(*points[-1], 4.3, fill=color, stroke="#1f2328", width=1)
    for number, a, b, color, _, corners, tag_at in WIRES:
        tag(number, tag_at, color)
    for number, a, pin_number, color, _, corners in XLR_WIRES:
        tag(number, (64.9, corners[-1][1]), color)

    # ---- what is what
    y = Y(19) + 60
    s.rect(30, y - 11, 22, 13, fill="#cfe1ff", stroke="#1d4ed8", width=1)
    s.text(60, y, "Isolated side (GND2): the two top power lines and everything right of the dashed line. "
                  "Never join it to the Pico side. Only U3 and U4 sit on both.", 12.5, weight="bold", fill="#1d4ed8")

    y += 38
    columns = [(30, "1  Put the parts in"), (640, "2  Wires on the Pico side"), (1040, "3  Wires on the isolated side")]
    for x, title in columns:
        s.text(x, y, title, 14, weight="bold")
    parts = [
        ("U1", "Pico, USB socket at the left: pin 1 in c1, pin 20 in c20, pin 21 in h20, pin 40 in h1"),
        ("SW1", "across the gap: d24, g24, d26, g26.   SW2: d30, g30, d32, g32"),
        ("U4", "the end with two pins together points at the Pico: f35, f36, then f39, f41"),
        ("C4", "4.7 µF: long leg (+) in h35, the other leg in h36"),
        ("R2", "270 Ω: i39 and i45"),
        ("C3", "10 µF: long leg (+) in the red line, the other leg in the blue line, at row 43"),
        ("U3", "6N137, notch towards the Pico: pins 1 to 4 in e45 to e48, pins 5 to 8 in f48 to f45"),
        ("C1", "100 nF: h45 and h48.   R4, 1 kΩ: red line (at row 47) and i47.   R3, 180 Ω: b41 and b46"),
        ("U5", "SN75176BP, notch towards the Pico: pins 1 to 4 in e52 to e55, pins 5 to 8 in f55 to f52"),
        ("C2", "100 nF: g52 and g55"),
        ("D1", "TVS diode P6KE6.8CA, either way round: h57 and blue line.   D3, the same part: h59 and blue line"),
    ]
    for i, (ref, text) in enumerate(parts):
        s.text(30, y + 22 + i * 18, ref, 12, weight="bold")
        s.text(66, y + 22 + i * 18, text, 12)

    def wire_line(x, line_y, number, color, ends, what, what_x=150):
        s.circle(x + 8, line_y - 4, 7.5, fill="white", stroke=color, width=1.6)
        s.text(x + 8, line_y - 0.6, str(number), 9.5, "middle", "bold")
        s.text(x + 24, line_y, ends, 12, weight="bold")
        s.text(x + what_x, line_y, what, 12)

    for i, (number, a, b, color, what, _, _) in enumerate(WIRES):
        x, row = (640, i) if number <= 8 else (1040, i - 8)
        wire_line(x, y + 22 + row * 18, number, color, f"{hole_name(a)} → {hole_name(b)}", what)
    xlr_y = y + 22 + 12 * 18 + 16
    s.text(1040, xlr_y, "4  The XLR jack", 14, weight="bold")
    for i, (number, a, pin_number, color, what, _) in enumerate(XLR_WIRES):
        wire_line(1040, xlr_y + 22 + i * 18, number, color, f"{hole_name(a)} → XLR pin {pin_number}", what, 190)

    tips_y = y + 22 + len(parts) * 18 + 16
    s.text(30, tips_y, "Before you plug the USB cable in", 14, weight="bold")
    for i, text in enumerate((
        "Chips U3 and U5: the notch points at the Pico. A chip put in backwards can be destroyed.",
        "U4: the two pins that sit together are at the Pico end. C3 and C4: the long leg is +.",
        "The buttons only work across the middle gap, as drawn.",
        "If your power lines have a break in the middle (a gap in the red and blue lines): this plan only uses the right half.",
    )):
        s.text(38, tips_y + 22 + i * 18, "•  " + text, 12)
    s.save(path)


def wire_table_markdown():
    lines = ["| Wire | From | To | What it does |", "|---|---|---|---|"]
    for number, a, b, _, what, _, _ in WIRES:
        lines.append(f"| {number} | {hole_name(a)} | {hole_name(b)} | {what} |")
    for number, a, pin_number, _, what, _ in XLR_WIRES:
        lines.append(f"| {number} | {hole_name(a)} | XLR pin {pin_number} | {what} |")
    return "\n".join(lines)


def main():
    problems = check_plan()
    if problems:
        print("The breadboard plan is wrong:")
        for problem in problems:
            print("  -", problem)
        return 1
    print(f"breadboard plan: {len(LEGS)} legs, {len(WIRES) + len(XLR_WIRES)} wires, nets match the schematic")

    draw_schematic(HERE / "dmxbox_schematic.svg")
    draw_breadboard(HERE / "dmxbox_breadboard.svg")
    if "--md" in sys.argv:
        print(wire_table_markdown())
    try:
        import resvg_py
        from PIL import Image
    except ImportError:
        print("SVG files written. For the PNG and PDF files run it with: --with resvg-py --with pillow")
        return 0
    pages = []
    for name in ("dmxbox_schematic", "dmxbox_breadboard"):
        png = HERE / f"{name}.png"
        png.write_bytes(bytes(resvg_py.svg_to_bytes(svg_path=str(HERE / f"{name}.svg"), zoom=2.0)))
        pages.append(Image.open(png).convert("RGB"))
    pages[0].save(HERE / "dmxbox_drawings.pdf", save_all=True, append_images=pages[1:], resolution=200.0)
    print("wrote the SVG, PNG and PDF files to", HERE)
    return 0


if __name__ == "__main__":
    sys.exit(main())
