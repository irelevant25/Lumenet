"""Places the parts of dmxbox 2 on its board (../dmxbox2.kicad_pcb), from KiCad's own net list of the schematic.
The placing itself is boxes/scripts/board.py; this file says where every part goes.

Run with KiCad's Python (it has pcbnew):  python make_board.py NETLIST
The net list comes from:  kicad-cli sch export netlist --format kicadsexpr -o NETLIST dmxbox2.kicad_sch
route.py routes the result; make_jlcpcb.py writes the Gerbers for JLCPCB (the board comes bare: you solder it).

The board: 100 x 62 mm, two layers, every part through-hole: box 1's board with the PIC instead of the Pico. Left
edge: the USB-C socket. Middle: the PIC in a DIP-28 socket, its crystal and its programming header J2 above it.
Top edge: the box-number switch, its keys at the edge. Left: the LEDs. Right: box 1's DMX side, isolated by a 2.5 mm
gap that no track, via or copper pour crosses. Right edge: the pin header of the DMX socket.
"""
import sys

import box  # noqa: F401  (boxes/scripts on the path)
import board
import footprintlib

W, H = 100.0, 62.0
GAP_X1, GAP_X2 = 64.06, 66.56        # the isolation gap: logic left, isolated right

# ref -> (x, y, rotation in degrees, counter-clockwise). Millimetres from the board's top left corner. A
# through-hole footprint's place is its pin 1.
PLACE = {
    # the USB-C socket at the left edge, its mouth 0.75 mm beyond it; the 3.3 V below it
    "J1": (7.91, 44.0, 270), "R1": (1.6, 38.0, 0), "R2": (1.6, 23.5, 270),
    "U2": (3.0, 57.5, 0), "C1": (8.5, 59.0, 0), "C2": (8.5, 54.8, 0),
    # the PIC, turned: pins 1 to 14 along y 41 from x 45 to the left, pins 28 to 15 along y 48.62
    "U1": (45.0, 41.0, 270),
    "C3": (12.8, 37.9, 0), "C6": (22.0, 53.0, 0), "C5": (31.0, 53.0, 0), "C4": (41.0, 53.0, 0),
    "Y1": (24.0, 31.0, 0), "C7": (20.0, 36.0, 0), "C8": (27.5, 36.0, 0),
    "R3": (47.0, 30.0, 270), "R4": (43.5, 30.0, 270), "C9": (40.3, 32.0, 270),
    "J2": (37.0, 21.5, 0),
    # box number: piano switch at the top edge, keys out; pin 1 at the right (box 1's)
    "SW1": (28.0, 3.75, 270),
    # LEDs at the left, under 3 mm light pipes in the case's top (box 1's, 3 mm higher up)
    "D4": (4.0, 19.0, 0), "D5": (9.5, 19.0, 0), "D6": (15.0, 19.0, 0),
    "R21": (5.27, 22.5, 270), "R22": (10.77, 22.5, 270), "R23": (16.27, 22.5, 270),
    # DMX: box 1's places
    "R24": (50.5, 7.04, 0), "R26": (50.5, 21.04, 0), "R29": (50.5, 35.04, 0), "C32": (52.0, 29.3, 0),
    "U6": (61.5, 4.5, 0), "U7": (61.5, 18.5, 0), "U8": (69.12, 40.12, 180),
    "U5": (58.96, 48.5, 0), "C26": (54.0, 43.85, 0),
    "C30": (72.0, 5.0, 0), "R25": (72.0, 9.58, 0), "C31": (72.0, 19.0, 0), "R27": (72.0, 23.58, 0),
    "R28": (72.0, 37.58, 0),
    "U9": (84.0, 14.0, 0), "C33": (84.5, 9.0, 0),
    "R32": (80.5, 26.5, 270), "R30": (84.5, 26.5, 270), "R31": (88.5, 26.5, 270),
    "D1": (79.0, 42.0, 0), "D2": (79.0, 47.0, 0),
    "C29": (80.0, 53.0, 0), "R33": (87.0, 51.0, 270),
    "J4": (97.0, 26.0, 0),
    # holes (box 1's)
    "H1": (4.0, 4.0, 0), "H2": (96.0, 4.0, 0), "H3": (56.5, 58.0, 0), "H4": (96.0, 58.0, 0),
}

EP_VIAS = []

SILK = [  # (text, x, y, size): labels on the board
    ("USB", 5.27, 32.4, 0.8), ("DMX", 10.77, 32.4, 0.8), ("RDM", 16.27, 32.4, 0.8),
    ("DMX OUT", 93.6, 23.4, 1.0), ("1 COM", 93.4, 26.0, 0.8), ("2 D-", 93.6, 28.54, 0.8), ("3 D+", 93.6, 31.08, 0.8),
    ("MCLR", 33.6, 21.5, 0.8), ("VDD", 33.9, 24.04, 0.8), ("GND", 33.9, 26.58, 0.8), ("PGD", 33.9, 29.12, 0.8),
    ("PGC", 33.9, 31.66, 0.8),
    ("ISOLATED", 76.0, 57.0, 1.2), ("dmxbox 2", 76.0, 60.5, 1.6), ("Lumenet", 87.0, 60.5, 1.0),
] + [  # the box number: under each key of SW1 what it adds, from the left
    (str(2 ** k), 10.22 + 2.54 * k, 14.6, 0.8) for k in range(8)
]
REF_AT = {  # part names away from their footprint's own place, where that is taken: x, y
    "J4": (97.0, 39.2),                         # below the header: above it is "DMX OUT"
}

# +5V in the supply class: the USB4085's pins are 0.85 mm apart, too close for the power class's clearance
POWER_NETS = ("+5V_ISO", "*/+5V_ISO")
SUPPLY_NETS = ("+5V", "GND", "GND_ISO", "*/GND_ISO", "+3V3", "*/VCAP")
FOOTPRINTS = [footprintlib.RKK]


def named(ref):
    """Whether the board prints the part's name: every part, as you solder them by hand (not the holes)."""
    return not ref.startswith("H")


def set_models(fp, ref):
    """KiCad's own 3D models do for every part here (the DC-DC module is made in make_3d.py)."""


if __name__ == "__main__":
    sys.exit(board.main(sys.modules[__name__], sys.argv[1]))
