"""Writes the KiCad schematic of dmxbox 3 from circuit.py: a title sheet (../dmxbox3.kicad_sch) and three A3
sheets (USB and power, RP2040, DMX and MIDI).

Every part is placed by hand (PLACE below). Every pin gets a short wire ending in the name of its net (a label),
a power symbol or a no-connect mark; neighbouring pins of one part on one net share a wire and one name. KiCad
joins all wires that carry the same name, so the drawing needs no long wires. Names used on more than one sheet
are global labels (the flag-shaped ones).

The script refuses to write when two nets would touch, when a pin is neither in a net nor marked free, or when
texts overlap each other or a part. check.py then compares KiCad's own net list with circuit.py.
Run it with any Python 3.10 or later.
"""
import math
import os
import sys
import uuid

import circuit
import kilib
from sexpr import Q, dump, find, find_all

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, ".."))
PROJECT = "dmxbox3"
ROOT_UUID = "5f1d2c40-6b7e-4f43-9a2e-2a6d1c3b0d01"
NS = uuid.UUID(ROOT_UUID)
STUB = 2.54
POWER_SYMBOLS = {"GND": "power:GND", "+3V3": "power:+3V3", "+5V": "power:+5V"}
SMALL = {"Device:R_Small", "Device:C_Small", "Device:LED_Small", "Device:D_Small"}


def uid(*names):
    return str(uuid.uuid5(NS, "/".join(str(n) for n in names)))


SHEETS = [  # key, title, file, page
    ("power", "USB, power and hub", "dmxbox3_power.kicad_sch", "2"),
    ("mcu", "RP2040, flash, LEDs, box number", "dmxbox3_rp2040.kicad_sch", "3"),
    ("dmx", "DMX with RDM, MIDI", "dmxbox3_dmx_midi.kicad_sch", "4"),
]
SHEET_UUID = {key: uid("sheet", key) for key, *_ in SHEETS}

# ref -> (sheet, x, y, rotation): millimetres on an A3 sheet, y down, rotation counter-clockwise
PLACE = {
    # ---- USB, power and hub
    "J1": ("power", 35.56, 63.5, 0), "R1": ("power", 76.2, 50.8, 0), "R2": ("power", 86.36, 50.8, 0),
    "C1": ("power", 96.52, 50.8, 0),
    "J2": ("power", 127.0, 63.5, 0), "R5": ("power", 165.1, 50.8, 0), "R7": ("power", 165.1, 81.28, 0),
    "R6": ("power", 177.8, 50.8, 0), "R8": ("power", 177.8, 81.28, 0), "C5": ("power", 190.5, 50.8, 0),
    "J3": ("power", 228.6, 63.5, 0), "R3": ("power", 269.24, 50.8, 0), "R4": ("power", 279.4, 50.8, 0),
    "C2": ("power", 289.56, 50.8, 0),
    "Q1": ("power", 50.8, 124.46, 90), "R9": ("power", 33.02, 142.24, 90), "R10": ("power", 66.04, 142.24, 0),
    "D1": ("power", 91.44, 121.92, 180), "C3": ("power", 109.22, 129.54, 0), "C4": ("power", 119.38, 129.54, 0),
    "Q2": ("power", 142.24, 124.46, 90), "Q3": ("power", 175.26, 119.38, 270), "R11": ("power", 160.02, 139.7, 0),
    "Q4": ("power", 149.86, 152.4, 0), "R12": ("power", 129.54, 157.48, 0), "R13": ("power", 190.5, 139.7, 0),
    "R14": ("power", 208.28, 124.46, 0), "R15": ("power", 220.98, 142.24, 0), "C6": ("power", 231.14, 142.24, 0),
    "U4": ("power", 264.16, 129.54, 0), "C7": ("power", 251.46, 142.24, 0), "C8": ("power", 279.4, 142.24, 0),
    "U2": ("power", 350.52, 78.74, 0), "Y2": ("power", 317.5, 116.84, 0), "C9": ("power", 368.3, 40.64, 0),
    "C10": ("power", 378.46, 40.64, 0), "C11": ("power", 388.62, 40.64, 0),
    # ---- RP2040, flash, LEDs, box number
    "U1": ("mcu", 190.5, 142.24, 0),
    "C12": ("mcu", 137.16, 63.5, 0), "C13": ("mcu", 147.32, 63.5, 0), "C14": ("mcu", 157.48, 63.5, 0),
    "C15": ("mcu", 167.64, 63.5, 0), "C16": ("mcu", 177.8, 63.5, 0), "C17": ("mcu", 187.96, 63.5, 0),
    "C18": ("mcu", 198.12, 63.5, 0), "C19": ("mcu", 208.28, 63.5, 0), "C20": ("mcu", 218.44, 63.5, 0),
    "C21": ("mcu", 236.22, 63.5, 0), "C22": ("mcu", 246.38, 63.5, 0), "C23": ("mcu", 256.54, 63.5, 0),
    "R17": ("mcu", 137.16, 104.14, 0),
    "R19": ("mcu", 129.54, 124.46, 90), "R20": ("mcu", 129.54, 134.62, 90),
    "U3": ("mcu", 71.12, 134.62, 0), "C27": ("mcu", 93.98, 116.84, 0), "R18": ("mcu", 93.98, 154.94, 90),
    "Y1": ("mcu", 137.16, 182.88, 0), "C24": ("mcu", 127.0, 195.58, 0), "C25": ("mcu", 162.56, 195.58, 0),
    "R16": ("mcu", 152.4, 162.56, 0),
    "J7": ("mcu", 86.36, 218.44, 180),
    "R21": ("mcu", 287.02, 172.72, 90), "D4": ("mcu", 297.18, 172.72, 180),
    "R22": ("mcu", 287.02, 185.42, 90), "D5": ("mcu", 297.18, 185.42, 180),
    "R23": ("mcu", 287.02, 198.12, 90), "D6": ("mcu", 297.18, 198.12, 180),
    "SW1": ("mcu", 309.88, 116.84, 0),
    # ---- DMX with RDM, MIDI
    "U5": ("dmx", 71.12, 55.88, 0), "C28": ("dmx", 40.64, 68.58, 0), "C29": ("dmx", 106.68, 68.58, 0),
    "U6": ("dmx", 172.72, 50.8, 0), "R24": ("dmx", 152.4, 48.26, 90), "R25": ("dmx", 203.2, 43.18, 0),
    "C30": ("dmx", 190.5, 27.94, 0),
    "U7": ("dmx", 172.72, 99.06, 0), "R26": ("dmx", 152.4, 96.52, 90), "R27": ("dmx", 203.2, 91.44, 0),
    "C31": ("dmx", 190.5, 76.2, 0),
    "U8": ("dmx", 172.72, 147.32, 0), "R28": ("dmx", 152.4, 144.78, 90), "R29": ("dmx", 203.2, 139.7, 0),
    "C32": ("dmx", 190.5, 124.46, 0),
    "U9": ("dmx", 269.24, 93.98, 0), "C33": ("dmx", 292.1, 73.66, 0),
    "R30": ("dmx", 309.88, 78.74, 0), "R31": ("dmx", 322.58, 101.6, 0), "R32": ("dmx", 335.28, 124.46, 0),
    "D2": ("dmx", 320.04, 147.32, 0), "J4": ("dmx", 388.62, 101.6, 0),
    "J5": ("dmx", 40.64, 220.98, 180), "R33": ("dmx", 66.04, 218.44, 90), "D3": ("dmx", 76.2, 220.98, 270),
    "U10": ("dmx", 99.06, 220.98, 0), "R34": ("dmx", 142.24, 205.74, 0), "C34": ("dmx", 152.4, 205.74, 0),
    "J6": ("dmx", 213.36, 220.98, 180), "R35": ("dmx", 246.38, 236.22, 90), "R36": ("dmx", 246.38, 205.74, 90),
    "H1": ("dmx", 330.2, 220.98, 0), "H2": ("dmx", 342.9, 220.98, 0), "H3": ("dmx", 355.6, 220.98, 0),
    "H4": ("dmx", 368.3, 220.98, 0),
}

BLOCKS = {
    "power": [("USB-C IN (computer or box before), OUT (next box), POWER (optional charger)", 15.24, 22.86, 304.8, 101.6),
              ("Where the 5 V comes from: POWER before IN; OUT switched on by the RP2040", 15.24, 104.14, 241.3, 170.18),
              ("3.3 V", 243.84, 104.14, 304.8, 170.18),
              ("USB hub: port 1 OUT, port 4 the RP2040", 307.34, 22.86, 406.4, 137.16),
              ("Power flags", 307.34, 139.7, 406.4, 170.18)],
    "mcu": [("RP2040", 124.46, 45.72, 271.78, 200.66),
            ("Flash", 50.8, 106.68, 121.92, 165.1),
            ("Service pads (nothing fitted)", 50.8, 200.66, 121.92, 238.76),
            ("LEDs", 274.32, 160.02, 330.2, 210.82),
            ("Box number", 274.32, 91.44, 345.44, 152.4)],
    "dmx": [("Isolated 5 V", 22.86, 27.94, 124.46, 88.9),
            ("Optocouplers: send, direction, receive", 127.0, 17.78, 228.6, 165.1),
            ("RS-485 driver, RDM line bias, protection; DMX OUT (XLR in the wall: pad n = XLR pin n)", 231.14, 50.8, 406.4, 165.1),
            ("MIDI IN (DIN socket in the wall: pad n = DIN pin n)", 22.86, 187.96, 175.26, 248.92),
            ("MIDI OUT (3.3 V MIDI)", 182.88, 187.96, 269.24, 248.92),
            ("Holes for M3 screws", 320.04, 205.74, 375.92, 233.68),
            ("Power flag", 22.86, 144.78, 124.46, 167.64)],
}

NOTES = {
    "power": [(17.78, 180.34, "IN asks for 5 V (R1, R2). OUT offers 5 V (R5, R6) and is switched on only when "
                              "the RP2040 sees a box on CC1 or CC2 (R7, R8)."),
              (17.78, 185.42, "Q1 lets IN's 5 V through unless a charger is on POWER; D1 lets POWER's through. "
                              "Q2 and Q3 back to back: no current comes back from OUT."),
              (17.78, 190.5, "The hub has its own 3.3 V regulator, load capacitors and 1.5k pull-up; RESET, PSELF "
                             "and OVCUR stay open (self powered, no over-current input).")],
    "mcu": [(17.78, 248.92, "SW1: switch k down adds 2^(k-1) to the box number n. Box n is 10.1.n.1 and Art-Net "
                            "universe n."),
            (17.78, 254.0, "J7 BOOT to GND while plugging in starts the RP2040's USB boot loader (a new box starts "
                           "it by itself).")],
    "dmx": [(129.54, 172.72, "GND_ISO and +5V_ISO are the isolated side: no copper joins them to GND or +5V."),
            (129.54, 177.8, "U5: B0505XT-1WR3 (Mornsun's SMD package): pin 8 stays free, as its maker asks.")],
}

FLAGS = {"power": [("GND", 322.58, 160.02), ("+5V", 342.9, 160.02)], "dmx": [("GND_ISO", 50.8, 154.94)]}

# Reference and value texts placed by hand: ref -> ((dx, dy, justify), (dx, dy, justify))
TEXT_AT = {"U4": ((-2.54, -6.35, "right"), (2.54, -6.35, "left")),
           "U6": ((-4.06, -8.89, None), (3.81, 8.89, "left")), "U7": ((-4.06, -8.89, None), (3.81, 8.89, "left")),
           "U8": ((-4.06, -8.89, None), (3.81, 8.89, "left")),
           "D3": ((-1.52, -0.64, "right"), (1.52, 0.0, "left"))}

# Pins joined by one straight wire instead of names (each chain lies on one line, in order)
DIRECT = [
    ("R21.2", "D4.2"), ("R22.2", "D5.2"), ("R23.2", "D6.2"),
    ("R24.2", "U6.2"), ("R26.2", "U7.2"), ("R28.2", "U8.2"),
    ("J5.4", "R33.1"), ("R33.2", "D3.1", "U10.1"), ("D3.2", "U10.2"),
]


# ---------------------------------------------------------------- geometry

def rot(px, py, r):
    """A point of the symbol (library units, y up) on the sheet (y down), turned r degrees counter-clockwise."""
    x, y = px, -py
    a = math.radians(r)
    c, s = round(math.cos(a)), round(math.sin(a))
    return x * c + y * s, -x * s + y * c


def dir_angle(dx, dy):
    """Angle of a direction on the sheet, counter-clockwise from right, y up."""
    return round(math.degrees(math.atan2(-dy, dx))) % 360


def r2(v):
    return round(v + 0.0, 2)


class Boxes:
    """Rectangles of texts and parts, to find overlaps."""

    def __init__(self):
        self.items = []  # (x1, y1, x2, y2, owner, what)

    def add(self, x1, y1, x2, y2, owner, what):
        self.items.append((min(x1, x2), min(y1, y2), max(x1, x2), max(y1, y2), owner, what))

    def text(self, s, x, y, angle, justify, owner, what, size=1.27, glob=False):
        w = 1.0 * size / 1.27 * len(s) + 0.3 + (2.6 if glob else 0)
        # a label sits on its wire: its letters reach about 1.3 mm above it
        h = (1.3 if justify and justify.startswith("label") else 1.6) * size / 1.27
        if angle in (0, 180):
            if justify == "left":
                self.add(x, y - h / 2, x + w, y + h / 2, owner, what)
            elif justify == "right":
                self.add(x - w, y - h / 2, x, y + h / 2, owner, what)
            elif justify == "label0":
                self.add(x, y - h, x + w, y, owner, what)
            elif justify == "label180":
                self.add(x - w, y - h, x, y, owner, what)
            else:
                self.add(x - w / 2, y - h / 2, x + w / 2, y + h / 2, owner, what)
        else:
            if justify == "label90":
                self.add(x - h, y - w, x, y, owner, what)
            else:  # label270
                self.add(x - h, y, x, y + w, owner, what)

    def overlaps(self):
        out = []
        it = self.items
        for i in range(len(it)):
            a = it[i]
            for j in range(i + 1, len(it)):
                b = it[j]
                if a[4] == b[4] and (a[5] == "body" or b[5] == "body"):
                    continue
                if a[0] < b[2] - 0.05 and b[0] < a[2] - 0.05 and a[1] < b[3] - 0.05 and b[1] < a[3] - 0.05:
                    out.append(f"{a[5]} of {a[4]} overlaps {b[5]} of {b[4]}")
        return out


def body_box(lib_id, x, y, r):
    """The part's drawing (rectangles, lines, circles, arcs of units 0 and 1), on the sheet."""
    s = lib_symbol(lib_id)
    xs, ys = [], []
    for sub in find_all(s, "symbol"):
        for g in sub:
            if not isinstance(g, list) or g[0] not in ("rectangle", "polyline", "circle", "arc"):
                continue
            for k in ("start", "end", "mid", "center"):
                e = find(g, k)
                if e:
                    xs.append(float(e[1]))
                    ys.append(float(e[2]))
            pts = find(g, "pts")
            if pts:
                for xy in find_all(pts, "xy"):
                    xs.append(float(xy[1]))
                    ys.append(float(xy[2]))
            if g[0] == "circle":
                c, rad = find(g, "center"), find(g, "radius")
                if c and rad:
                    xs += [float(c[1]) - float(rad[1]), float(c[1]) + float(rad[1])]
                    ys += [float(c[2]) - float(rad[1]), float(c[2]) + float(rad[1])]
    if not xs:
        return None
    corners = [rot(px, py, r) for px in (min(xs), max(xs)) for py in (min(ys), max(ys))]
    return (x + min(c[0] for c in corners), y + min(c[1] for c in corners),
            x + max(c[0] for c in corners), y + max(c[1] for c in corners))


# ---------------------------------------------------------------- symbols of the library

def effects(size=1.27, justify=None, hide=False, bold=False):
    font = ["font", ["size", size, size]]
    if bold:
        font.append(["bold", "yes"])
    out = ["effects", font]
    if justify:
        out.append(["justify"] + justify.split())
    if hide:
        out.append(["hide", "yes"])
    return out


def prop(name, value, x, y, angle=0, hide=False, justify=None):
    p = ["property", Q(name), Q(value), ["at", r2(x), r2(y), angle]]
    if hide:
        p.append(["hide", "yes"])
    p.append(effects(justify=justify))
    return p


DCDC_AT = {"2": (-15.24, 5.08, 0), "1": (-15.24, -5.08, 0), "8": (-15.24, 0, 0),
           "5": (15.24, 5.08, 180), "4": (15.24, -5.08, 180)}


def dcdc_symbol():
    s = ["symbol", Q("dmxbox3:DCDC_B05XT"), ["pin_names", ["offset", 1.016]],
         ["exclude_from_sim", "no"], ["in_bom", "yes"], ["on_board", "yes"],
         prop("Reference", "U", -12.7, 12.7, justify="left"), prop("Value", "DCDC_B05XT", -12.7, -12.7, justify="left"),
         prop("Footprint", "dmxbox3:Converter_DCDC_Mornsun_B05_XT-1WR3", 0, 0, hide=True),
         prop("Datasheet", "", 0, 0, hide=True),
         prop("Description", "Isolated DC-DC 5 V to 5 V, 1 W, SMD: Mornsun B0505XT-1WR3 and the parts that copy "
              "it", 0, 0, hide=True),
         ["symbol", Q("DCDC_B05XT_0_1"),
          ["rectangle", ["start", -12.7, 10.16], ["end", 12.7, -10.16],
           ["stroke", ["width", 0.254], ["type", "default"]], ["fill", ["type", "background"]]]]]
    pins = ["symbol", Q("DCDC_B05XT_1_1")]
    for num, name, kind in circuit.DCDC_PINS:
        x, y, a = DCDC_AT[num]
        pins.append(["pin", kind, "line", ["at", x, y, a], ["length", 2.54],
                     ["name", Q(name), effects()], ["number", Q(num), effects()]])
    s.append(pins)
    s.append(["embedded_fonts", "no"])
    return s


_cache = {}


def lib_symbol(lib_id):
    if lib_id not in _cache:
        _cache[lib_id] = dcdc_symbol() if lib_id == "dmxbox3:DCDC_B05XT" else kilib.symbol(lib_id)
    return _cache[lib_id]


def lib_pins(lib_id):
    if lib_id == "dmxbox3:DCDC_B05XT":
        return [(n, nm, k, DCDC_AT[n][0], DCDC_AT[n][1], DCDC_AT[n][2], 2.54, 1) for n, nm, k in circuit.DCDC_PINS]
    return kilib.pins(lib_id)


def lib_prop(lib_id, name):
    for p in find_all(lib_symbol(lib_id), "property"):
        if str(p[1]) == name:
            at = find(p, "at")
            just = None
            eff = find(p, "effects")
            if eff:
                j = find(eff, "justify")
                if j:
                    just = " ".join(str(v) for v in j[1:])
            return float(at[1]), float(at[2]), just
    return 0.0, 0.0, None


# ---------------------------------------------------------------- one sheet

class Sheet:
    def __init__(self, key, path):
        self.key = key
        self.path = path            # instance path of the symbols on this sheet
        self.items = []
        self.boxes = Boxes()
        self.points = {}            # (x, y) -> (net, what)
        self.problems = []
        self.power_n = 0

    def touch(self, x, y, net, what):
        other = self.points.get((x, y))
        if other and other[0] != net:
            self.problems.append(f"{what} ({net}) touches {other[1]} ({other[0]}) at {x},{y}")
        self.points[(x, y)] = (net, what)

    def symbol(self, lib_id, ref, value, x, y, r, fields=None, key=None, in_bom=True, on_board=True,
               power=False):
        key = key or ref
        s = ["symbol", ["lib_id", Q(lib_id)], ["at", x, y, r], ["unit", 1],
             ["exclude_from_sim", "no"], ["in_bom", "yes" if in_bom else "no"],
             ["on_board", "yes" if on_board else "no"], ["dnp", "no"], ["uuid", Q(uid("sym", key))]]
        bb = None if power else body_box(lib_id, x, y, r)
        if ref in TEXT_AT:
            (rdx, rdy, rj), (vdx, vdy, vj) = TEXT_AT[ref]
            rpos, vpos = (x + rdx, y + rdy), (x + vdx, y + vdy)
        elif lib_id in SMALL:
            pins = lib_pins(lib_id)
            horizontal = abs(rot(pins[0][3], pins[0][4], r)[0] - rot(pins[1][3], pins[1][4], r)[0]) > 0.1
            if horizontal:
                rpos, vpos, rj, vj = (x, y - 2.03), (x, y + 2.29), None, None
            else:
                rpos, vpos, rj, vj = (x + 1.52, y - 1.02), (x + 1.52, y + 1.27), "left", "left"
        elif r in (90, 270) and bb:
            cx = (bb[0] + bb[2]) / 2
            if r == 90:
                rpos, vpos = (cx, bb[1] - 3.81), (cx, bb[1] - 1.52)
            else:
                rpos, vpos = (cx, bb[3] + 1.52), (cx, bb[3] + 3.81)
            rj = vj = None
        else:
            lx, ly, rj = lib_prop(lib_id, "Reference")
            vx, vy, vj = lib_prop(lib_id, "Value")
            dx, dy = rot(lx, ly, r)
            ex, ey = rot(vx, vy, r)
            rpos, vpos = (x + dx, y + dy), (x + ex, y + ey)
        rpos, vpos = (r2(rpos[0]), r2(rpos[1])), (r2(vpos[0]), r2(vpos[1]))
        # KiCad turns a field with its part: a part turned a quarter needs its texts turned a quarter back
        fa = 90 if r in (90, 270) else 0
        s.append(prop("Reference", ref, *rpos, fa, hide=power, justify=rj))
        s.append(prop("Value", value, *vpos, fa, justify=vj))
        for name, val in (fields or {}).items():
            s.append(prop(name, val, x, y, 0, hide=True))
        for pin in lib_pins(lib_id):
            s.append(["pin", Q(pin[0]), ["uuid", Q(uid("pin", key, pin[0]))]])
        s.append(["instances", ["project", Q(PROJECT), ["path", Q(self.path), ["reference", Q(ref)], ["unit", 1]]]])
        self.items.append(s)
        if not power:
            self.boxes.text(ref, *rpos, 0, rj.split()[0] if rj else None, ref, "reference")
            self.boxes.text(value, *vpos, 0, vj.split()[0] if vj else None, ref, "value")
            if bb:
                self.boxes.add(bb[0] + 0.25, bb[1] + 0.25, bb[2] - 0.25, bb[3] - 0.25, ref, "body")

    def wire(self, x1, y1, x2, y2):
        self.items.append(["wire", ["pts", ["xy", x1, y1], ["xy", x2, y2]],
                           ["stroke", ["width", 0], ["type", "default"]], ["uuid", Q(uid("wire", self.key, x1, y1, x2, y2))]])

    def junction(self, x, y):
        self.items.append(["junction", ["at", x, y], ["diameter", 0], ["color", 0, 0, 0, 0],
                           ["uuid", Q(uid("junction", self.key, x, y))]])

    def label(self, net, x, y, angle, glob):
        just = "left" if angle in (0, 90) else "right"
        if glob:
            self.items.append(["global_label", Q(net), ["shape", "passive"], ["at", x, y, angle],
                               ["fields_autoplaced", "yes"], effects(justify=just),
                               ["uuid", Q(uid("glabel", self.key, net, x, y))],
                               ["property", Q("Intersheetrefs"), Q("${INTERSHEET_REFS}"), ["at", x, y, 0],
                                effects(justify=just, hide=True)]])
        else:
            self.items.append(["label", Q(net), ["at", x, y, angle], effects(justify=just + " bottom"),
                               ["uuid", Q(uid("label", self.key, net, x, y))]])
        self.boxes.text(net, x, y, angle, f"label{angle}", f"label {net}", "label", glob=glob)

    def no_connect(self, x, y):
        self.items.append(["no_connect", ["at", x, y], ["uuid", Q(uid("nc", self.key, x, y))]])

    def power(self, net, x, y, outward):
        lib_id = POWER_SYMBOLS[net]
        base = 270 if net == "GND" else 90
        if outward in (0, 180):
            outward = base   # ground always hangs down, supplies always point up
        r = (outward - base) % 360
        self.power_n += 1
        ref = f"#PWR{self.key[0].upper()}{self.power_n:03d}"
        self.symbol(lib_id, ref, net, x, y, r, key=f"pwr-{self.key}-{self.power_n}", power=True)
        w = 1.0 * len(net) + 0.3
        if outward == 270:
            self.boxes.add(x - max(1.4, w / 2), y, x + max(1.4, w / 2), y + 4.6, f"power {net}", "power symbol")
        elif outward == 90:
            self.boxes.add(x - max(1.4, w / 2), y - 4.6, x + max(1.4, w / 2), y, f"power {net}", "power symbol")
        elif outward == 0:
            self.boxes.add(x, y - 1.4, x + 2.5 + w, y + 1.4, f"power {net}", "power symbol")
        else:
            self.boxes.add(x - 2.5 - w, y - 1.4, x, y + 1.4, f"power {net}", "power symbol")

    def text(self, s, x, y, size=1.27, bold=False):
        self.items.append(["text", Q(s), ["exclude_from_sim", "no"], ["at", x, y, 0],
                           effects(size=size, justify="left bottom", bold=bold), ["uuid", Q(uid("text", self.key, s))]])
        w = 1.0 * size / 1.27 * len(s)
        self.boxes.add(x, y - 1.6 * size / 1.27, x + w, y, f"text {s[:20]}", "text")

    def rect(self, x1, y1, x2, y2):
        self.items.append(["rectangle", ["start", x1, y1], ["end", x2, y2],
                           ["stroke", ["width", 0], ["type", "dash"]], ["fill", ["type", "none"]],
                           ["uuid", Q(uid("rect", self.key, x1, y1))]])


def net_sheets():
    where = {}
    for net, pins in circuit.NETS.items():
        where[net] = {PLACE[p.split(".")[0]][0] for p in pins}
    return where


def draw_sheet(key):
    path = "/" + ROOT_UUID + "/" + SHEET_UUID[key]
    sh = Sheet(key, path)
    net_of = {p: net for net, pins in circuit.NETS.items() for p in pins}
    free = set(circuit.NO_CONNECT)
    sheets_of = net_sheets()

    chains = [c for c in DIRECT if PLACE[c[0].split(".")[0]][0] == key]
    in_chain = {p for c in chains for p in c}
    chain_points = {}

    for ref, d in circuit.PARTS.items():
        sheet, x, y, r = PLACE[ref]
        if sheet != key:
            continue
        fields = {"Footprint": d["fp"], "LCSC": d["lcsc"], "Fitted by": d["by"], "Description": d["desc"]}
        sh.symbol(d["sym"], ref, d["value"], x, y, r, fields=fields)
        # every pin: where it is, where its wire ends, which net
        ends = []  # (net, outward angle, pin point, end point, pin)
        seen = {}
        for num, name, kind, px, py, pa, plen, unit in lib_pins(d["sym"]):
            pin = f"{ref}.{num}"
            ax, ay = rot(px, py, r)
            ax, ay = r2(x + ax), r2(y + ay)
            net = net_of.get(pin)
            if pin in free:
                if net:
                    sh.problems.append(f"{pin} both free and in {net}")
                if (ax, ay) not in seen:
                    sh.no_connect(ax, ay)
                    seen[(ax, ay)] = None
                    sh.touch(ax, ay, f"free {pin}", pin)
                continue
            if net is None:
                sh.problems.append(f"{pin} ({name}) is in no net and not marked free")
                continue
            if (ax, ay) in seen:
                if seen[(ax, ay)] != net:
                    sh.problems.append(f"{pin}: stacked pins with {seen[(ax, ay)]} and {net}")
                continue
            seen[(ax, ay)] = net
            if pin in in_chain:
                chain_points[pin] = (ax, ay, net)
                sh.touch(ax, ay, net, pin)
                continue
            ox, oy = rot(math.cos(math.radians(pa + 180)), math.sin(math.radians(pa + 180)), r)
            ex, ey = r2(ax + ox * STUB), r2(ay + oy * STUB)
            ends.append((net, dir_angle(ox, oy), (ax, ay), (ex, ey), pin))
        # neighbouring pins of one net on one side share one wire and one name
        groups = []
        for e in sorted(ends, key=lambda e: (e[1], e[3][0] if e[1] in (90, 270) else e[3][1],
                                             e[3][1] if e[1] in (90, 270) else e[3][0])):
            g = groups[-1] if groups else None
            same_line = g and g[-1][1] == e[1] and (
                (e[1] in (90, 270) and g[-1][3][1] == e[3][1]) or (e[1] in (0, 180) and g[-1][3][0] == e[3][0]))
            if same_line and g[-1][0] == e[0] and math.dist(g[-1][3], e[3]) <= 5.09:
                g.append(e)
            else:
                groups.append([e])
        for g in groups:
            net, out = g[0][0], g[0][1]
            for _, _, (ax, ay), (ex, ey), pin in g:
                sh.wire(ax, ay, ex, ey)
                sh.touch(ax, ay, net, pin)
                sh.touch(ex, ey, net, pin)
                sh.touch(r2((ax + ex) / 2), r2((ay + ey) / 2), net, pin)
            for a, b in zip(g, g[1:]):
                sh.wire(*a[3], *b[3])
            for e in g[1:-1]:
                sh.junction(*e[3])
            ex, ey = g[0][3]
            if net in POWER_SYMBOLS:
                sh.power(net, ex, ey, out)
            else:
                sh.label(net, ex, ey, out, len(sheets_of[net]) > 1)

    # the straight wires of DIRECT
    for chain in chains:
        pts = [chain_points.get(p) for p in chain]
        if None in pts:
            sh.problems.append(f"chain {chain}: a pin is missing")
            continue
        net = pts[0][2]
        if any(p[2] != net for p in pts):
            sh.problems.append(f"chain {chain} joins different nets")
            continue
        for (x1, y1, _), (x2, y2, _) in zip(pts, pts[1:]):
            if x1 != x2 and y1 != y2:
                sh.problems.append(f"chain {chain}: {x1},{y1} to {x2},{y2} is not straight")
                continue
            sh.wire(x1, y1, x2, y2)
            n = max(1, int(round(math.dist((x1, y1), (x2, y2)) / 1.27)))
            for i in range(1, n):
                sh.touch(r2(x1 + (x2 - x1) * i / n), r2(y1 + (y2 - y1) * i / n), net, f"wire {chain}")
        for p in pts[1:-1]:
            sh.junction(p[0], p[1])
        others = [p for p in circuit.NETS[net] if p not in chain]
        if others:
            (x1, y1, _), (x2, y2, _) = pts[0], pts[1]
            away = dir_angle(x1 - x2, y1 - y2)
            if net in POWER_SYMBOLS:
                sh.power(net, x1, y1, away)
            else:
                sh.label(net, x1, y1, away, len(sheets_of[net]) > 1)

    for net, x, y in FLAGS.get(key, []):
        sh.power_n += 1
        sh.symbol("power:PWR_FLAG", f"#FLG{key[0].upper()}{sh.power_n:02d}", "PWR_FLAG", x, y,
                  180 if net.startswith("+") else 0, key=f"flg-{key}-{net}", power=True)
        if net in POWER_SYMBOLS:
            sh.power(net, x, y, 270 if net == "GND" else 90)
        else:
            sh.label(net, x, y, 270, len(net_sheets()[net]) > 1)
    for title, x1, y1, x2, y2 in BLOCKS.get(key, []):
        sh.rect(x1, y1, x2, y2)
        sh.text(title, x1 + 1.27, y1 + 3.81, 1.524, bold=True)
    for x, y, s in NOTES.get(key, []):
        sh.text(s, x, y)

    sh.problems += sh.boxes.overlaps()
    return sh


def sheet_file(sh, title):
    libs = ["lib_symbols"]
    used = sorted({str(find(i, "lib_id")[1]) for i in sh.items if i[0] == "symbol"})
    libs += [lib_symbol(lid) for lid in used]
    return ["kicad_sch", ["version", 20250610], ["generator", Q("eeschema")], ["generator_version", Q("9.99")],
            ["uuid", Q(uid("file", sh.key))], ["paper", Q("A3")],
            ["title_block", ["title", Q(f"dmxbox 3: {title}")], ["date", Q("2026-11-26")], ["rev", Q("1")],
             ["company", Q("Lumenet")],
             ["comment", 1, Q("Made by hardware/scripts/make_schematic.py from circuit.py: change those, not this file.")]],
            libs] + sh.items + [["embedded_fonts", "no"]]


def root_file():
    items = []
    x, y = 30.48, 60.96
    for key, title, fname, page in SHEETS:
        items.append(["sheet", ["at", x, y], ["size", 76.2, 25.4], ["exclude_from_sim", "no"], ["in_bom", "yes"],
                      ["on_board", "yes"], ["dnp", "no"], ["fields_autoplaced", "yes"],
                      ["stroke", ["width", 0.1524], ["type", "solid"]], ["fill", ["color", 0, 0, 0, 0.0]],
                      ["uuid", Q(SHEET_UUID[key])],
                      ["property", Q("Sheetname"), Q(title), ["at", x, y - 0.71, 0], effects(justify="left bottom")],
                      ["property", Q("Sheetfile"), Q(fname), ["at", x, y + 26.0, 0],
                       effects(justify="left top", hide=True)],
                      ["instances", ["project", Q(PROJECT), ["path", Q("/" + ROOT_UUID), ["page", Q(page)]]]]])
        x += 91.44
    lines = [
        "dmxbox 3: a USB network adapter for the Lumenet desk (and any Art-Net program) with one DMX output with "
        "RDM, MIDI IN and OUT,",
        "and a USB hub so that boxes can be chained: computer -> box 1 -> box 2 -> ... (up to 5 on one port).",
        "",
        "JLCPCB solders every part (LCSC numbers in the LCSC field). You make the cables from the pin headers J4,",
        "J5 and J6 to the XLR and DIN sockets in the box wall.",
        "",
        "Made by hardware/scripts/make_schematic.py from hardware/scripts/circuit.py.",
    ]
    for i, s in enumerate(lines):
        if s:
            items.append(["text", Q(s), ["exclude_from_sim", "no"], ["at", 30.48, 116.84 + i * 5.08, 0],
                          effects(size=1.778, justify="left bottom"), ["uuid", Q(uid("roottext", i))]])
    return ["kicad_sch", ["version", 20250610], ["generator", Q("eeschema")], ["generator_version", Q("9.99")],
            ["uuid", Q(ROOT_UUID)], ["paper", Q("A3")],
            ["title_block", ["title", Q("dmxbox 3: USB to DMX with RDM, MIDI and a USB hub for chaining")],
             ["date", Q("2026-11-26")], ["rev", Q("1")], ["company", Q("Lumenet")],
             ["comment", 1, Q("Made by hardware/scripts/make_schematic.py from circuit.py: change those, not this file.")]],
            ["lib_symbols"]] + items + [["sheet_instances", ["path", Q("/"), ["page", Q("1")]]],
                                        ["embedded_fonts", "no"]]


def write_project_files():
    with open(os.path.join(OUT, "sym-lib-table"), "w", encoding="utf-8", newline="\n") as f:
        f.write('(sym_lib_table\n\t(version 7)\n\t(lib (name "dmxbox3")(type "KiCad")(uri "${KIPRJMOD}/dmxbox3.kicad_sym")'
                '(options "")(descr "dmxbox 3 parts that KiCad does not have"))\n)\n')
    lib = ["kicad_symbol_lib", ["version", 20241209], ["generator", Q("make_schematic")],
           ["generator_version", Q("1")]]
    sym = dcdc_symbol()
    sym[1] = Q("DCDC_B05XT")
    lib.append(sym)
    with open(os.path.join(OUT, "dmxbox3.kicad_sym"), "w", encoding="utf-8", newline="\n") as f:
        f.write(dump(lib) + "\n")


def main():
    problems = [f"{ref} has no place" for ref in circuit.PARTS if ref not in PLACE]
    sheets = {}
    for key, title, fname, page in SHEETS:
        sh = draw_sheet(key)
        problems += [f"[{key}] {p}" for p in sh.problems]
        sheets[key] = (sh, title, fname)
    if problems:
        print("\n".join(problems))
        print(f"{len(problems)} problems: nothing written")
        return 1
    write_project_files()
    for key, (sh, title, fname) in sheets.items():
        with open(os.path.join(OUT, fname), "w", encoding="utf-8", newline="\n") as f:
            f.write(dump(sheet_file(sh, title)) + "\n")
    with open(os.path.join(OUT, PROJECT + ".kicad_sch"), "w", encoding="utf-8", newline="\n") as f:
        f.write(dump(root_file()) + "\n")
    print("wrote", PROJECT + ".kicad_sch and", ", ".join(f for _, f in [(k, v[2]) for k, v in sheets.items()]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
