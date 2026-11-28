"""Draws the schematic of dmxbox 2, the PIC32 box with two DMX outputs.

Run from the repository root:

    uv run --no-project --python 3.13 --with resvg-py --with pillow python boxes/box2/perfboard/schematic-2-universes/draw.py

It writes dmxbox2_schematic.svg/.png next to this file. The net list is circuit.py. While the
script draws, it notes which pin each wire and each label touches; before it writes anything it
checks that the drawing joins exactly the nets of circuit.py, and stops with a message if not.
The breadboard plan is breadboard.py, the perfboard plan perfboard.py.
"""
from __future__ import annotations

import sys
from pathlib import Path

from circuit import BUTTONS, FREE_PINS, NETS, OUTPUTS, PARTS, check_list
from svg import GREY, INK, Svg, to_png_and_pdf

HERE = Path(__file__).resolve().parent
WIRE, PART, ISO = "#16794a", "#8b1e1e", "#1d4ed8"

# The look of the labels: supplies in their own colors, signals white.
TAG_STYLE = {
    "+5V": ("#fee2e2", "#b91c1c"), "GND": ("#f3f4f6", "#374151"), "+3V3": ("#ffedd5", "#c2410c"),
    "+5V_ISO": ("#fce7f3", "#be185d"), "GND2": ("#dbeafe", "#1d4ed8"),
}


class Sheet:
    """An SVG drawing that remembers what it joins: pins, wires and labels."""

    def __init__(self, width, height):
        self.svg = Svg(width, height)
        self.parent = {}
        self.pins = {}                       # pin -> the point where wires meet it
        self.names = set()                   # net names of the labels drawn
        self.segments = []                   # every straight piece of wire
        self.dots = []                       # junction dots: they join whatever wire they sit on

    # ---- joining
    def _find(self, item):
        self.parent.setdefault(item, item)
        while self.parent[item] != item:
            self.parent[item] = self.parent[self.parent[item]]
            item = self.parent[item]
        return item

    def _join(self, a, b):
        self.parent[self._find(a)] = self._find(b)

    @staticmethod
    def key(point):
        return round(point[0]), round(point[1])

    def term(self, pin, point):
        if pin in self.pins:
            raise ValueError(f"{pin} drawn twice")
        self.pins[pin] = self.key(point)
        self._find(self.key(point))

    # ---- drawing that joins
    def wire(self, *points):
        self.svg.path(points, stroke=WIRE, width=2)
        for a, b in zip(points, points[1:]):
            self._join(self.key(a), self.key(b))
            self.segments.append((self.key(a), self.key(b)))

    def dot(self, point):
        self.svg.circle(point[0], point[1], 4, fill=WIRE)
        self.dots.append(self.key(point))

    def _join_dots(self):
        for x, y in self.dots:
            for (x1, y1), (x2, y2) in self.segments:
                on_line = (x1 == x2 == x and min(y1, y2) <= y <= max(y1, y2)) or \
                          (y1 == y2 == y and min(x1, x2) <= x <= max(x1, x2))
                if on_line:
                    self._join((x, y), (x1, y1))

    def tag(self, point, net, side="right"):
        """A label: every label of one name is the same wire. `side`: where it stands from the point."""
        x, y = point
        fill, stroke = TAG_STYLE.get(net, ("#ffffff", INK))
        shown = net.replace("D-", "D−")
        w, h = 14 + 7.6 * len(net), 20
        if side in ("right", "left"):
            sign = 1 if side == "right" else -1
            tip, end = x + sign * 8, x + sign * (8 + w)
            shape = [(x, y), (tip, y - h / 2), (end, y - h / 2), (end, y + h / 2), (tip, y + h / 2)]
            tx = (tip + end) / 2
            ty = y + 4.5
        else:
            sign = -1 if side == "up" else 1
            tip, end = y + sign * 8, y + sign * (8 + h)
            shape = [(x, y), (x - w / 2, tip), (x - w / 2, end), (x + w / 2, end), (x + w / 2, tip)]
            tx, ty = x, (tip + end) / 2 + 4.5
        self.svg.path(shape, stroke=stroke, width=1.5, fill=fill, close=True)
        self.svg.text(tx, ty, shown, 12, "middle", "bold", stroke)
        self.names.add(net)
        self._join(self.key(point), ("net", net))

    def no_connect(self, point):
        x, y = point
        self.svg.path([(x - 5, y - 5), (x + 5, y + 5)], stroke="#b91c1c", width=2)
        self.svg.path([(x - 5, y + 5), (x + 5, y - 5)], stroke="#b91c1c", width=2)

    # ---- the check
    def problems(self):
        self._join_dots()
        expected = {pin: net for net, pins in NETS.items() for pin in pins}
        for ref, net in BUTTONS.items():
            expected[f"{ref}.1"], expected[f"{ref}.2"] = net, "GND"
        groups = {}
        for pin, point in self.pins.items():
            groups.setdefault(self._find(point), {"pins": set(), "names": set()})["pins"].add(pin)
        for name in self.names:
            groups.setdefault(self._find(("net", name)), {"pins": set(), "names": set()})["names"].add(name)

        found = []
        for group in groups.values():
            nets = {expected[pin] for pin in group["pins"] if pin in expected}
            strangers = {pin for pin in group["pins"] if pin not in expected}
            unknown = strangers - set(FREE_PINS)
            if unknown:
                found.append(f"pins that are in no net: {sorted(unknown)}")
            if len(nets) > 1:
                found.append(f"nets joined to each other: {sorted(nets)}")
            if strangers & set(FREE_PINS) and len(group["pins"]) > 1:
                found.append(f"a pin that must stay free is joined: {sorted(group['pins'])}")
            if group["names"] and nets and group["names"] != nets:
                found.append(f"labels {sorted(group['names'])} on the wire of {sorted(nets)}")
            if group["names"] and not group["pins"]:
                found.append(f"label {sorted(group['names'])} joins no pin")
        for pin, net in expected.items():
            if pin not in self.pins:
                found.append(f"{pin} ({net}) is not drawn")
        for net in set(expected.values()):
            roots = {self._find(self.pins[pin]) for pin, n in expected.items() if n == net and pin in self.pins}
            if len(roots) > 1:
                found.append(f"net {net} is in {len(roots)} pieces")
        return sorted(set(found))


# =====================================================================================
# Symbols. Each draws a part and tells the sheet where its pins are.
# =====================================================================================
def ic(sh, ref, x, y, w, h, left, right, title, subtitle="", stub=40, top=(), bottom=()):
    """A chip as a box. left/right: (pin, name, y); top/bottom: (pin, name, x). Returns the pin ends."""
    s = sh.svg
    s.rect(x, y, w, h, fill="#fffaf0", stroke=PART, width=2.2, rx=3)
    ends = {}
    def number(pin):                                      # pin numbers only; named pins say their name inside
        return pin if pin.isdigit() else ""

    for pin, name, py in left:
        s.line(x - stub, py, x, py, PART)
        s.text(x + 7, py + 4, name, 11.5)
        s.text(x - stub / 2, py - 5, number(pin), 10.5, "middle", fill=GREY)
        ends[pin] = (x - stub, py)
    for pin, name, py in right:
        s.line(x + w, py, x + w + stub, py, PART)
        s.text(x + w - 7, py + 4, name, 11.5, "end")
        s.text(x + w + stub / 2, py - 5, number(pin), 10.5, "middle", fill=GREY)
        ends[pin] = (x + w + stub, py)
    for pin, name, px in top:
        s.line(px, y - stub, px, y, PART)
        s.text(px, y + 15, name, 11.5, "middle")
        s.text(px + 5, y - stub / 2, pin, 10.5, fill=GREY)
        ends[pin] = (px, y - stub)
    for pin, name, px in bottom:
        s.line(px, y + h, px, y + h + stub, PART)
        s.text(px, y + h - 7, name, 11.5, "middle")
        s.text(px + 5, y + h + stub / 2 + 4, pin, 10.5, fill=GREY)
        ends[pin] = (px, y + h + stub)
    for pin, end in ends.items():
        sh.term(f"{ref}.{pin}", end)
    return ends


def _between(p1, p2, body):
    """The two ends of a part's body of length `body`, centred between p1 and p2."""
    (x1, y1), (x2, y2) = p1, p2
    mx, my = (x1 + x2) / 2, (y1 + y2) / 2
    if abs(x1 - x2) >= abs(y1 - y2):
        return (mx - body / 2, my), (mx + body / 2, my), "h"
    return (mx, my - body / 2), (mx, my + body / 2), "v"


def _leads(sh, p1, p2, a, b):
    sh.svg.path([p1, a], stroke=WIRE, width=2)
    sh.svg.path([b, p2], stroke=WIRE, width=2)


def _label(sh, ref, value, mid, direction, side=1):
    s = sh.svg
    if direction == "h":
        s.text(mid[0], mid[1] - 13, f"{ref}  {value}", 11.5, "middle", "bold", PART)
    else:
        anchor = "start" if side > 0 else "end"
        s.text(mid[0] + side * 13, mid[1] - 1, ref, 11.5, anchor, "bold", PART)
        s.text(mid[0] + side * 13, mid[1] + 12, value, 10.5, anchor, fill=PART)


def resistor(sh, ref, p1, p2, side=1):
    a, b, d = _between(p1, p2, 44)
    _leads(sh, p1, p2, a, b)
    mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
    if d == "h":
        sh.svg.rect(a[0], a[1] - 7, 44, 14, fill="white", stroke=PART, width=2)
    else:
        sh.svg.rect(a[0] - 7, a[1], 14, 44, fill="white", stroke=PART, width=2)
    _label(sh, ref, PARTS[ref][1], mid, d, side)
    sh.term(f"{ref}.1", p1)
    sh.term(f"{ref}.2", p2)


def capacitor(sh, ref, p1, p2, side=1):
    a, b, d = _between(p1, p2, 8)
    _leads(sh, p1, p2, a, b)
    mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
    if d == "h":
        for x in (a[0], b[0]):
            sh.svg.line(x, a[1] - 11, x, a[1] + 11, PART, 2.4)
    else:
        for y in (a[1], b[1]):
            sh.svg.line(a[0] - 11, y, a[0] + 11, y, PART, 2.4)
    _label(sh, ref, PARTS[ref][1], mid, d, side)
    sh.term(f"{ref}.1", p1)
    sh.term(f"{ref}.2", p2)


def crystal(sh, ref, p1, p2):
    a, b, d = _between(p1, p2, 30)
    _leads(sh, p1, p2, a, b)
    x1, x2, y = a[0], b[0], a[1]
    sh.svg.line(x1 + 3, y - 12, x1 + 3, y + 12, PART, 2.2)
    sh.svg.line(x2 - 3, y - 12, x2 - 3, y + 12, PART, 2.2)
    sh.svg.rect(x1 + 8, y - 9, x2 - x1 - 16, 18, fill="white", stroke=PART, width=2)
    sh.svg.text((x1 + x2) / 2, y - 17, f"{ref}  {PARTS[ref][1]}", 11.5, "middle", "bold", PART)
    sh.term(f"{ref}.1", p1)
    sh.term(f"{ref}.2", p2)


def diode_body(sh, a, b, d, fill="white", double=False):
    """A diode symbol from a (anode side) to b (cathode side)."""
    s = sh.svg
    if d == "h":
        (x1, y), x2 = a, b[0]
        if double:
            mid = (x1 + x2) / 2
            s.path([(x1, y - 8), (x1, y + 8), (mid, y)], stroke=PART, width=2, fill=fill, close=True)
            s.path([(x2, y - 8), (x2, y + 8), (mid, y)], stroke=PART, width=2, fill=fill, close=True)
            s.path([(mid - 4, y - 12), (mid, y - 9), (mid, y + 9), (mid + 4, y + 12)], stroke=PART, width=2)
        else:
            s.path([(x1, y - 8), (x1, y + 8), (x2, y)], stroke=PART, width=2, fill=fill, close=True)
            s.line(x2, y - 9, x2, y + 9, PART, 2.2)
    else:
        (x, y1), y2 = a, b[1]
        if double:
            mid = (y1 + y2) / 2
            s.path([(x - 8, y1), (x + 8, y1), (x, mid)], stroke=PART, width=2, fill=fill, close=True)
            s.path([(x - 8, y2), (x + 8, y2), (x, mid)], stroke=PART, width=2, fill=fill, close=True)
            s.path([(x - 12, mid - 4), (x - 9, mid), (x + 9, mid), (x + 12, mid + 4)], stroke=PART, width=2)
        else:
            s.path([(x - 8, y1), (x + 8, y1), (x, y2)], stroke=PART, width=2, fill=fill, close=True)
            s.line(x - 9, y2, x + 9, y2, PART, 2.2)


def led(sh, ref, anode, cathode):
    a, b, d = _between(anode, cathode, 18)
    _leads(sh, anode, cathode, a, b)
    diode_body(sh, a, b, d, fill="#fde68a")
    mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
    for k in (0, 7):                                     # the two little arrows of light
        sh.svg.path([(mx + 2 + k, my - 12), (mx + 10 + k, my - 20)], stroke=PART, width=1.5)
        sh.svg.path([(mx + 6 + k, my - 20), (mx + 10 + k, my - 20), (mx + 10 + k, my - 16)], stroke=PART, width=1.5)
    sh.svg.text(mx, my + 26, f"{ref}  LED", 11.5, "middle", "bold", PART)
    sh.term(f"{ref}.A", anode)
    sh.term(f"{ref}.K", cathode)


def tvs(sh, ref, line_end, ground_end, side=1):
    a, b, d = _between(line_end, ground_end, 26)
    _leads(sh, line_end, ground_end, a, b)
    diode_body(sh, a, b, d, double=True)
    mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
    _label(sh, ref, "TVS", mid, d, side)
    sh.term(f"{ref}.1", line_end)
    sh.term(f"{ref}.2", ground_end)


def button(sh, ref, p1, p2, job):
    a, b, d = _between(p1, p2, 46)
    _leads(sh, p1, p2, a, b)
    s = sh.svg
    s.circle(a[0], a[1], 3.2, fill="white", stroke=PART, width=2)
    s.circle(b[0], b[1], 3.2, fill="white", stroke=PART, width=2)
    s.line(a[0] + 3, a[1] - 3, b[0] - 4, b[1] - 15, PART, 2.2)
    s.text((a[0] + b[0]) / 2, a[1] - 22, f"{ref}  {job}", 11.5, "middle", "bold", PART)
    sh.term(f"{ref}.1", p1)
    sh.term(f"{ref}.2", p2)


def section(sh, x, y, w, h, title, fill="#ffffff"):
    sh.svg.rect(x, y, w, h, fill=fill, stroke="#d1d5db", width=1.2, rx=8)
    sh.svg.text(x + 12, y + 20, title, 13, weight="bold", fill=GREY)


# =====================================================================================
# The schematic
# =====================================================================================
WIDTH, HEIGHT = 2620, 1530
GAP_X = 1648                                   # the isolation gap


def draw_power(sh):
    section(sh, 20, 92, 600, 345, "USB-C and the supplies of the logic side")
    ends = ic(sh, "J1", 50, 150, 100, 230, [], [
        ("VBUS", "VBUS", 175), ("D+", "D+", 210), ("D-", "D−", 245), ("CC1", "CC1", 280), ("CC2", "CC2", 320),
        ("GND", "GND", 360)], "J1", stub=40)
    sh.svg.text(85, 298, "J1", 15, "middle", "bold", PART)
    sh.svg.text(85, 316, "USB-C", 12, "middle", fill=PART)
    sh.svg.text(85, 331, "breakout", 11, "middle", fill=GREY, italic=True)
    sh.wire(ends["VBUS"], (250, 175), (380, 175), (400, 175))
    sh.dot((250, 175))
    sh.wire((250, 175), (250, 150))
    sh.tag((250, 150), "+5V", "up")
    capacitor(sh, "C1", (380, 175), (380, 245), side=-1)
    sh.dot((380, 175))
    sh.tag((380, 245), "GND", "down")
    sh.tag(ends["D+"], "D+", "right")
    sh.tag(ends["D-"], "D-", "right")
    resistor(sh, "R1", ends["CC1"], (285, 280))
    sh.tag((285, 280), "GND", "right")
    resistor(sh, "R2", ends["CC2"], (285, 320))
    sh.tag((285, 320), "GND", "right")
    sh.tag(ends["GND"], "GND", "right")
    sh.svg.text(40, 420, "R1, R2: many breakouts have them already; then leave them out.", 11, fill=GREY)

    ends = ic(sh, "U2", 430, 150, 90, 70, [("2", "VIN", 175)], [("3", "VOUT", 175)], "U2", stub=30,
              bottom=[("1", "GND", 475)])
    sh.svg.text(475, 140, "U2  MCP1700-3302E", 12, "middle", "bold", PART)
    sh.wire((400, 175), ends["2"])
    sh.tag(ends["1"], "GND", "down")
    sh.wire(ends["3"], (585, 175))
    sh.dot((585, 175))
    capacitor(sh, "C2", (585, 175), (585, 245), side=-1)
    sh.tag((585, 245), "GND", "down")
    sh.wire((585, 175), (585, 150))
    sh.tag((585, 150), "+3V3", "up")


def draw_programming(sh):
    section(sh, 20, 452, 600, 240, "Reset and the programming header")
    sh.wire((70, 510), (70, 530))
    sh.tag((70, 510), "+3V3", "up")
    resistor(sh, "R3", (70, 530), (70, 610))
    sh.wire((70, 610), (130, 610))
    sh.dot((130, 610))
    capacitor(sh, "C9", (130, 610), (130, 650), side=-1)
    sh.tag((130, 650), "GND", "down")
    resistor(sh, "R4", (130, 610), (250, 610))
    sh.tag((250, 610), "MCLR", "right")
    ends = ic(sh, "J2", 470, 490, 120, 190, [
        ("1", "1 MCLR", 510), ("2", "2 VDD", 538), ("3", "3 GND", 566), ("4", "4 PGED", 594),
        ("5", "5 PGEC", 622), ("6", "6", 650)], [], "J2", stub=40)
    sh.svg.text(530, 482, "J2  PICkit / Snap", 11.5, "middle", "bold", PART)
    for pin, net in (("1", "MCLR"), ("2", "+3V3"), ("3", "GND"), ("4", "BLACKOUT"), ("5", "PRESET")):
        sh.tag(ends[pin], net, "left")
    sh.no_connect(ends["6"])


def draw_controls(sh):
    section(sh, 20, 707, 600, 543, "Box number, buttons and LED")
    # SW1: a DIP switch of 8. Switch k joins pin k and pin 17 - k.
    s = sh.svg
    x, y0, pitch = 250, 775, 34
    s.rect(x, y0 - 22, 110, pitch * 8 + 10, fill="#fffaf0", stroke=PART, width=2.2, rx=3)
    s.text(x + 55, y0 - 32, "SW1  piano DIP switch, 8", 12, "middle", "bold", PART)
    for k in range(1, 9):
        py = y0 + (k - 1) * pitch
        s.line(x - 40, py, x, py, PART)
        s.line(x + 110, py, x + 150, py, PART)
        s.text(x - 20, py - 5, k, 10.5, "middle", fill=GREY)
        s.text(x + 130, py - 5, 17 - k, 10.5, "middle", fill=GREY)
        s.circle(x + 30, py, 3, fill="white", stroke=PART, width=1.8)
        s.circle(x + 80, py, 3, fill="white", stroke=PART, width=1.8)
        s.line(x + 33, py - 2, x + 76, py - 12, PART, 2)
        s.line(x, py, x + 27, py, PART, 1.5)
        s.line(x + 83, py, x + 110, py, PART, 1.5)
        s.text(x + 55, py + 13, f"{k}", 9.5, "middle", fill=GREY)
        sh.term(f"SW1.{k}", (x - 40, py))
        sh.term(f"SW1.{17 - k}", (x + 150, py))
    bus_x = 170
    for k in range(1, 7):
        py = y0 + (k - 1) * pitch
        sh.wire((x - 40, py), (bus_x, py))
        if k > 1:
            sh.wire((bus_x, y0 + (k - 2) * pitch), (bus_x, py))
            if k < 6:
                sh.dot((bus_x, py))
    sh.wire((bus_x, y0 + 5 * pitch), (bus_x, y0 + 5 * pitch + 40))
    sh.tag((bus_x, y0 + 5 * pitch + 40), "GND", "down")
    for k, net in zip(range(1, 7), ("N1", "N2", "N4", "N8", "N16", "N32")):
        sh.tag((x + 150, y0 + (k - 1) * pitch), net, "right")
    for k in (7, 8):
        sh.no_connect((x - 40, y0 + (k - 1) * pitch))
        sh.no_connect((x + 150, y0 + (k - 1) * pitch))
    s.text(60, y0 + 2, "ON joins the", 11, fill=GREY)
    s.text(60, y0 + 16, "pin to GND:", 11, fill=GREY)
    s.text(60, y0 + 30, "switch 1 counts 1,", 11, fill=GREY)
    s.text(60, y0 + 44, "2 counts 2, 3: 4,", 11, fill=GREY)
    s.text(60, y0 + 58, "4: 8, 5: 16, 6: 32.", 11, fill=GREY)
    s.text(60, y0 + 76, "Box n is 10.1.n.1", 11, weight="bold", fill=GREY)
    s.text(60, y0 + 92, "7 and 8: unused", 11, fill=GREY)

    # the buttons and the LED
    for ref, net, py in (("SW2", "BLACKOUT", 1085), ("SW3", "PRESET", 1140)):
        sh.tag((180, py), net, "left")
        button(sh, ref, (180, py), (330, py), {"SW2": "blackout", "SW3": "preset 1"}[ref])
        sh.tag((330, py), "GND", "right")
    sh.tag((130, 1200), "LED", "left")
    resistor(sh, "R5", (130, 1200), (260, 1200))
    led(sh, "D1", (260, 1200), (370, 1200))
    sh.tag((370, 1200), "GND", "right")
    sh.svg.text(430, 1085, "SW2 and SW3 share pins 4 and 5 with", 11, fill=GREY)
    sh.svg.text(430, 1100, "the programmer: do not press them", 11, fill=GREY)
    sh.svg.text(430, 1115, "while it writes the chip.", 11, fill=GREY)


def draw_pic(sh):
    section(sh, 640, 92, 580, 650, "The microcontroller")
    pitch, top = 36, 150
    left, right = [], []
    for k in range(1, 15):
        left.append((str(k), _pic_name(k), top + (k - 1) * pitch))
        right.append((str(29 - k), _pic_name(29 - k), top + (k - 1) * pitch))
    ends = ic(sh, "U1", 800, 125, 220, pitch * 14 - 10, left, right, "U1", stub=44)
    s = sh.svg
    s.text(910, top + 6.4 * pitch, "U1", 18, "middle", "bold", PART)
    s.text(910, top + 6.4 * pitch + 20, "PIC32MX270F256B", 12.5, "middle", fill=PART)
    s.text(910, top + 6.4 * pitch + 36, "-I/SP, DIP-28", 11.5, "middle", fill=GREY, italic=True)
    for pin, net in PIC_NET.items():
        side = "left" if pin <= 14 else "right"
        sh.tag(ends[str(pin)], net, side)


PIC_NET = {
    1: "MCLR", 2: "N1", 3: "N2", 4: "BLACKOUT", 5: "PRESET", 6: "N4", 7: "N8", 8: "GND", 9: "OSC1", 10: "OSC2",
    11: "N16", 12: "N32", 13: "+3V3", 14: "LED", 15: "+5V", 16: "DIR2", 17: "RX2", 18: "TX2", 19: "GND",
    20: "VCAP", 21: "D+", 22: "D-", 23: "+3V3", 24: "RX1", 25: "DIR1", 26: "TX1", 27: "GND", 28: "+3V3",
}


def _pic_name(pin):
    from circuit import PIC_PINS
    return PIC_PINS[pin].replace("D-", "D−")


def draw_pic_parts(sh):
    section(sh, 640, 760, 580, 480, "Crystal and the capacitors at the microcontroller")
    s = sh.svg
    sh.tag((700, 830), "OSC1", "left")
    sh.wire((700, 830), (790, 830))
    sh.dot((790, 830))
    crystal(sh, "Y1", (790, 830), (900, 830))
    sh.wire((900, 830), (990, 830))
    sh.dot((900, 830))
    sh.tag((990, 830), "OSC2", "right")
    capacitor(sh, "C7", (790, 830), (790, 900), side=-1)
    sh.tag((790, 900), "GND", "down")
    capacitor(sh, "C8", (900, 830), (900, 900))
    sh.tag((900, 900), "GND", "down")
    s.text(1080, 830, "Y1, C7, C8 close to", 11, fill=GREY)
    s.text(1080, 845, "pins 9 and 10", 11, fill=GREY)

    # decoupling: one capacitor at each supply pin
    y1, y2 = 1010, 1090
    for i, (ref, pins) in enumerate((("C3", "13 and 8"), ("C4", "28 and 27"), ("C5", "23 and 19"))):
        x = 720 + i * 130
        sh.tag((x, y1), "+3V3", "up")
        capacitor(sh, ref, (x, y1), (x, y2))
        sh.tag((x, y2), "GND", "down")
        s.text(x, y2 + 50, f"at pins {pins}", 10.5, "middle", fill=GREY)
    x = 1110
    sh.tag((x, y1), "VCAP", "up")
    capacitor(sh, "C6", (x, y1), (x, y2))
    sh.tag((x, y2), "GND", "down")
    s.text(x, y2 + 50, "at pins 20 and 19", 10.5, "middle", fill=GREY)
    s.text(700, 1200, "C6 on VCAP is needed: the chip makes its own core voltage. Never join VCAP to 3.3 V.",
           11, fill=GREY)


def draw_isolated_supply(sh):
    s = sh.svg
    x0 = 1250
    section(sh, x0, 92, 1350, 180, "The isolated 5 V for the DMX side: U3 bridges the gap", fill="#ffffff")
    ends = ic(sh, "U3", GAP_X - 70, 135, 140, 100, [("1", "+Vin", 160), ("2", "−Vin", 210)],
              [("7", "+Vout", 160), ("5", "−Vout", 210)], "U3", stub=50)
    s.text(GAP_X, 255, "U3  RKK-0505S/H", 12, "middle", "bold", PART)
    sh.wire(ends["1"], (1500, 160), (1440, 160))
    sh.tag((1440, 160), "+5V", "left")
    capacitor(sh, "C10", (1500, 160), (1500, 210), side=-1)
    sh.dot((1500, 160))
    sh.dot((1500, 210))
    sh.wire(ends["2"], (1500, 210), (1440, 210))
    sh.tag((1440, 210), "GND", "left")
    sh.wire(ends["7"], (1800, 160), (1860, 160))
    sh.tag((1860, 160), "+5V_ISO", "right")
    capacitor(sh, "C11", (1800, 160), (1800, 210))
    sh.dot((1800, 160))
    sh.dot((1800, 210))
    sh.wire(ends["5"], (1800, 210), (1860, 210))
    sh.tag((1860, 210), "GND2", "right")
    s.text(2080, 170, "No other part reaches across the gap", 11.5, fill=ISO, italic=True)
    s.text(2080, 186, "except the six optocouplers below.", 11.5, fill=ISO, italic=True)
    s.text(2080, 210, "Pins 3, 4 and 6 of U3 do not exist.", 11.5, fill=GREY)


def draw_output(sh, n, oy):
    """One DMX output: three optocouplers across the gap, the line driver, the XLR socket."""
    p = OUTPUTS[n]
    s = sh.svg
    tx, dr, rx = (p[k].split(".")[1] for k in ("tx", "dir", "rx"))
    section(sh, 1250, oy, 1350, 500, f"Output {n}: U1 pins {tx} (send), {dr} (direction), {rx} (receive)  →  {p['xlr']}")
    gx, cy = GAP_X, oy + 62
    # ---- receive (RDM, later): the line driver's RO through an optocoupler to the PIC
    u = ic(sh, p["receive"], gx - 80, cy, 160, 118,
           [("8", "VCC", cy + 20), ("7", "VE", cy + 46), ("6", "VO", cy + 72), ("5", "GND", cy + 98)],
           [("2", "anode", cy + 20), ("3", "cathode", cy + 72)], p["receive"])
    s.text(gx, cy + 136, f"{p['receive']}  6N137  receive", 11.5, "middle", "bold", PART, halo=True)
    sh.wire(u["8"], (gx - 150, cy + 20), (gx - 150, cy - 2))
    sh.dot((gx - 150, cy + 20))
    sh.tag((gx - 150, cy - 2), "+5V", "up")
    sh.wire(u["7"], (gx - 150, cy + 46), (gx - 150, cy + 20))
    sh.wire(u["6"], (gx - 240, cy + 72), (gx - 330, cy + 72))
    sh.dot((gx - 240, cy + 72))
    sh.tag((gx - 330, cy + 72), f"RX{n}", "left")
    resistor(sh, p["receive_up"], (gx - 240, cy + 2), (gx - 240, cy + 72), side=-1)
    sh.tag((gx - 240, cy + 2), "+3V3", "up")
    sh.tag(u["5"], "GND", "left")
    resistor(sh, p["receive_led"], (gx + 250, cy + 20), u["2"])
    sh.tag((gx + 250, cy + 20), "+5V_ISO", "right")
    ro = u["3"]

    # ---- direction: the PIC lights the LED to listen; dark, the driver sends
    u = ic(sh, p["direction"], gx - 60, cy + 150, 120, 80, [("1", "anode", cy + 170), ("2", "cathode", cy + 210)],
           [("4", "C", cy + 170), ("3", "E", cy + 210)], p["direction"])
    s.text(gx, cy + 248, f"{p['direction']}  PC817  direction", 11.5, "middle", "bold", PART, halo=True)
    resistor(sh, p["direction_led"], (gx - 330, cy + 170), u["1"])
    sh.tag((gx - 330, cy + 170), f"DIR{n}", "left")
    sh.tag(u["2"], "GND", "left")
    de = u["4"]
    sh.tag(u["3"], "GND2", "right")

    # ---- send: the PIC's UART through an optocoupler to the line driver
    u = ic(sh, p["send"], gx - 80, cy + 262, 160, 118,
           [("2", "anode", cy + 282), ("3", "cathode", cy + 334)],
           [("8", "VCC", cy + 282), ("7", "VE", cy + 308), ("6", "VO", cy + 334), ("5", "GND", cy + 360)], p["send"])
    s.text(gx, cy + 398, f"{p['send']}  6N137  send", 11.5, "middle", "bold", PART, halo=True)
    resistor(sh, p["send_led"], (gx - 330, cy + 282), u["2"])
    sh.tag((gx - 330, cy + 282), "+3V3", "left")
    sh.wire(u["3"], (gx - 330, cy + 334))
    sh.tag((gx - 330, cy + 334), f"TX{n}", "left")
    sh.wire(u["8"], (gx + 150, cy + 282), (gx + 200, cy + 282), (gx + 260, cy + 282))
    sh.dot((gx + 150, cy + 282))
    sh.wire(u["7"], (gx + 150, cy + 308), (gx + 150, cy + 282))
    sh.wire((gx + 200, cy + 282), (gx + 200, cy + 254))
    sh.dot((gx + 200, cy + 282))
    sh.tag((gx + 200, cy + 254), "+5V_ISO", "up")
    di = u["6"]
    sh.tag(u["5"], "GND2", "right")

    # ---- the line driver
    dx = gx + 390
    u = ic(sh, p["driver"], dx, cy + 50, 150, 340,
           [("1", "RO", cy + 72), ("2", "/RE", cy + 170), ("3", "DE", cy + 195), ("4", "DI", cy + 334)],
           [("8", "VCC", cy + 72), ("6", "A", cy + 170), ("7", "B", cy + 270), ("5", "GND", cy + 360)], p["driver"])
    s.text(dx + 75, cy + 245, p["driver"], 15, "middle", "bold", PART)
    s.text(dx + 75, cy + 263, "MAX485", 12, "middle", fill=PART)
    sh.wire(ro, u["1"])
    sh.wire(de, (dx - 120, cy + 170), (dx - 60, cy + 170), u["2"])
    sh.dot((dx - 120, cy + 170))
    sh.wire(u["3"], (dx - 60, cy + 195), (dx - 60, cy + 170))
    sh.dot((dx - 60, cy + 170))
    resistor(sh, p["direction_up"], (dx - 120, cy + 105), (dx - 120, cy + 170))
    sh.tag((dx - 120, cy + 105), "+5V_ISO", "up")
    sh.wire(di, (gx + 260, cy + 334), u["4"])
    sh.dot((gx + 260, cy + 334))
    sh.dot((gx + 260, cy + 282))
    resistor(sh, p["send_up"], (gx + 260, cy + 282), (gx + 260, cy + 334))
    sh.wire(u["8"], (dx + 215, cy + 72), (dx + 262, cy + 72), (dx + 262, cy + 46))
    sh.dot((dx + 215, cy + 72))
    sh.tag((dx + 262, cy + 46), "+5V_ISO", "up")
    capacitor(sh, p["c_driver"], (dx + 215, cy + 72), (dx + 215, cy + 15), side=-1)
    sh.tag((dx + 215, cy + 15), "GND2", "up")
    sh.tag(u["5"], "GND2", "right")

    # ---- the line: bias, termination, protection, the XLR socket
    sh.wire(u["6"], *[(dx + x, cy + 170) for x in (215, 290, 360, 430)])
    sh.wire(u["7"], *[(dx + x, cy + 270) for x in (215, 290, 360, 430)])
    resistor(sh, p["termination"], (dx + 215, cy + 170), (dx + 215, cy + 270), side=-1)
    sh.dot((dx + 215, cy + 170))
    sh.dot((dx + 215, cy + 270))
    resistor(sh, p["bias_up"], (dx + 290, cy + 112), (dx + 290, cy + 170))
    sh.dot((dx + 290, cy + 170))
    sh.tag((dx + 290, cy + 112), "+5V_ISO", "up")
    tvs(sh, p["tvs_a"], (dx + 360, cy + 170), (dx + 360, cy + 222))
    sh.dot((dx + 360, cy + 170))
    sh.tag((dx + 360, cy + 222), "GND2", "down")
    resistor(sh, p["bias_down"], (dx + 290, cy + 270), (dx + 290, cy + 335))
    sh.dot((dx + 290, cy + 270))
    sh.tag((dx + 290, cy + 335), "GND2", "down")
    tvs(sh, p["tvs_b"], (dx + 360, cy + 270), (dx + 360, cy + 322))
    sh.dot((dx + 360, cy + 270))
    sh.tag((dx + 360, cy + 322), "GND2", "down")
    jx = dx + 470
    j = ic(sh, p["xlr"], jx, cy + 140, 96, 170, [("3", "3  Data +", cy + 170), ("2", "2  Data −", cy + 270)], [],
           p["xlr"], stub=40, bottom=[("1", "1  common", jx + 48)])
    s.text(jx + 48, cy + 128, f"{p['xlr']}  XLR", 12, "middle", "bold", PART)
    sh.tag(j["1"], "GND2", "down")

    # ---- the capacitors next to the optocouplers: one on each side of the gap
    y = cy + 425
    sh.tag((gx - 330, y), "+5V", "left")
    capacitor(sh, p["c_receive"], (gx - 330, y), (gx - 230, y))
    sh.tag((gx - 230, y), "GND", "right")
    s.text(gx - 140, y + 4, f"next to {p['receive']}", 10.5, fill=GREY)
    sh.tag((gx + 260, y), "+5V_ISO", "left")
    capacitor(sh, p["c_send"], (gx + 260, y), (gx + 360, y))
    sh.tag((gx + 360, y), "GND2", "right")
    s.text(gx + 440, y + 4, f"next to {p['send']}", 10.5, fill=GREY)


def draw_notes(sh):
    s = sh.svg
    y = 1352
    s.text(30, y, "Reading the drawing", 14, weight="bold")
    for i, line in enumerate((
        "Labels with the same name are one wire: every +5V is joined, every GND, every TX1 and so on.",
        "GND (logic side) and GND2 (DMX side) are never joined. Only U3 and the optocouplers reach across the gap.",
        "Every capacitor is ceramic. 100 nF ones sit right next to the pins they serve.",
        "The two DMX outputs share GND2 and the isolated 5 V. Each XLR: pin 3 Data +, pin 2 Data −, pin 1 common.",
        "U5 and U9 (receive), U6 and U10 (direction), R12 to R14 and R21 to R23 make the box ready for RDM:",
        "the firmware sends only, for now, and keeps U6 and U10 dark, so both drivers send.",
        "Not connected: pins 1 and 4 of every 6N137, positions 7 and 8 of SW1, pin 6 of J2.",
    )):
        s.text(38, y + 24 + i * 19, "•  " + line, 12.5)
    x = 1300
    s.text(x, y, "Pins of the chips, seen from above", 14, weight="bold")
    s.text(x, y + 24, "Pin 1 is next to the notch (or dot); the numbers go round anticlockwise.", 12)
    s.text(x, y + 43, "U1: pins 1 to 14 down the left side, 15 to 28 up the right side.", 12)
    s.text(x, y + 62, "6N137 and MAX485: 1 to 4 down the left, 5 to 8 up the right. PC817: 1, 2 left; 3, 4 right.", 12)
    s.text(x, y + 81, "U2 (MCP1700), flat side towards you, legs down: 1 GND, 2 VIN, 3 VOUT.", 12)
    s.text(x, y + 100, "U3 (RKK-0505S/H): pins 1 and 2 sit together at one end; pin 7 stands alone at the other.", 12)
    s.text(x, y + 119, "SW1: switch k joins pin k and pin 17 − k.", 12)
    s.text(30, HEIGHT - 22, "dmxbox 2 schematic: drawn by boxes/box2/perfboard/schematic-2-universes/draw.py from circuit.py, "
                             "2026-11-23", 11, fill=GREY)


def draw_schematic(path):
    sh = Sheet(WIDTH, HEIGHT)
    s = sh.svg
    s.rect(GAP_X, 80, WIDTH - 10 - GAP_X, 1235, fill="#eef5ff")
    s.text(30, 45, "dmxbox 2: USB to two DMX512 outputs", 24, weight="bold")
    s.text(30, 68, "PIC32MX270F256B as a USB network adapter, two isolated RS-485 outputs, ready for RDM  ·  "
                   "box number by DIP switch  ·  every capacitor ceramic", 13.5, fill=GREY)
    s.text(GAP_X + 20, 45, "ISOLATED DMX SIDE  ·  GND2, +5V_ISO", 14, weight="bold", fill=ISO)
    s.text(GAP_X - 20, 45, "LOGIC SIDE  ·  GND, +5V, +3V3", 14, "end", "bold", GREY)
    draw_power(sh)
    draw_programming(sh)
    draw_controls(sh)
    draw_pic(sh)
    draw_pic_parts(sh)
    draw_isolated_supply(sh)
    draw_output(sh, 1, 290)
    draw_output(sh, 2, 805)
    s.line(GAP_X, 80, GAP_X, 1315, ISO, 1.6, dash="8 6")
    draw_notes(sh)
    problems = sh.problems()
    if problems:
        return problems
    s.save(path)
    return []


def main():
    problems = check_list()
    if problems:
        print("The net list is wrong:")
        for problem in problems:
            print("  -", problem)
        return 1
    problems = draw_schematic(HERE / "dmxbox2_schematic.svg")
    if problems:
        print("The schematic does not match the net list:")
        for problem in problems:
            print("  -", problem)
        return 1
    print(f"schematic: {len(NETS)} nets, every pin drawn and joined as the net list says")
    if to_png_and_pdf(HERE, ["dmxbox2_schematic"], "dmxbox2_schematic.pdf"):
        print("wrote the SVG, PNG and PDF files to", HERE)
    return 0


if __name__ == "__main__":
    sys.exit(main())
