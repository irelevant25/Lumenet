"""Places the parts of dmxbox 1 on its board (../dmxbox1.kicad_pcb), from KiCad's own net list of the schematic.
The placing itself is boxes/scripts/board.py; this file says where every part goes.

Run with KiCad's Python (it has pcbnew):  python make_board.py NETLIST
The net list comes from:  kicad-cli sch export netlist --format kicadsexpr -o NETLIST dmxbox1.kicad_sch
route.py routes the result; make_jlcpcb.py writes the Gerbers for JLCPCB (the board comes bare: you solder it).

The board: 100 x 62 mm, two layers, every part through-hole. Bottom left: the Pico in two socket strips, its
micro-USB at the left edge. Top edge: the box-number switch, its keys at the edge. Left: the LEDs. Right: the DMX
side, isolated by a 2.5 mm gap that no track, via or copper pour crosses; only the optocouplers and the DC-DC module
bridge it. Right edge: the pin header of the DMX socket (the socket sits in the box wall, on a cable).
"""
import sys

import box  # noqa: F401  (boxes/scripts on the path)
import board
import pcbnew
import footprintlib

W, H = 100.0, 62.0
GAP_X1, GAP_X2 = 64.06, 66.56        # the isolation gap: logic left, isolated right

# ref -> (x, y, rotation in degrees, counter-clockwise). Millimetres from the board's top left corner. A
# through-hole footprint's place is its pin 1.
PLACE = {
    # the Pico, turned so that its micro-USB looks out of the left edge (0.75 mm beyond it): pins 1 to 20 along
    # y 59, pins 40 to 21 along y 41.22
    "U1": (2.16, 59.0, 90),
    # box number: piano switch at the top edge, keys out; pin 1 at the right
    "SW1": (28.0, 3.75, 270),
    # LEDs at the left, 5.5 mm apart, under 3 mm light pipes in the case's top; their resistors below them
    "D4": (4.0, 22.0, 0), "D5": (9.5, 22.0, 0), "D6": (15.0, 22.0, 0),
    "R21": (5.27, 25.5, 270), "R22": (10.77, 25.5, 270), "R23": (16.27, 25.5, 270),
    # the service pins
    "J7": (36.0, 22.0, 0),
    # DMX: the logic side of the optocouplers, which stand across the gap (U8 turned: its LED is on the DMX side)
    "R24": (50.5, 7.04, 0), "R26": (50.5, 21.04, 0), "R29": (50.5, 35.04, 0), "C32": (52.0, 29.3, 0),
    "U6": (61.5, 4.5, 0), "U7": (61.5, 18.5, 0), "U8": (69.12, 40.12, 180),
    # the DC-DC module across the gap below them: pins 1 and 2 left of it, 5 and 7 right
    "U5": (58.96, 48.5, 0), "C26": (54.0, 43.85, 0),
    # DMX: the isolated side
    "C30": (72.0, 5.0, 0), "R25": (72.0, 9.58, 0), "C31": (72.0, 19.0, 0), "R27": (72.0, 23.58, 0),
    "R28": (72.0, 37.58, 0),
    "U9": (84.0, 14.0, 0), "C33": (84.5, 9.0, 0),
    "R32": (80.5, 26.5, 270), "R30": (84.5, 26.5, 270), "R31": (88.5, 26.5, 270),
    "D1": (79.0, 42.0, 0), "D2": (79.0, 47.0, 0),
    "C29": (80.0, 53.0, 0), "R33": (87.0, 51.0, 270),
    "J4": (97.0, 26.0, 0),
    # holes (none at the bottom left: the Pico is there)
    "H1": (4.0, 4.0, 0), "H2": (96.0, 4.0, 0), "H3": (56.5, 58.0, 0), "H4": (96.0, 58.0, 0),
}

EP_VIAS = []

SILK = [  # (text, x, y, size): labels on the board
    ("USB", 5.27, 35.4, 0.8), ("DMX", 10.77, 35.4, 0.8), ("RDM", 16.27, 35.4, 0.8),
    ("DMX OUT", 93.6, 23.4, 1.0), ("1 COM", 93.4, 26.0, 0.8), ("2 D-", 93.6, 28.54, 0.8), ("3 D+", 93.6, 31.08, 0.8),
    ("GND", 39.6, 22.0, 0.8), ("RUN", 39.6, 24.54, 0.8), ("LOG", 39.6, 27.08, 0.8),
    ("ISOLATED", 76.0, 57.0, 1.2), ("dmxbox 1", 76.0, 60.5, 1.6), ("Lumenet", 87.0, 60.5, 1.0),
] + [  # the box number: under each key of SW1 what it adds, from the left
    (str(2 ** k), 10.22 + 2.54 * k, 14.6, 0.8) for k in range(8)
]
REF_AT = {  # part names away from their footprint's own place, where that is taken: x, y
    "U1": (27.0, 37.4),                         # above the Pico, not beside R29
    "J4": (97.0, 39.2),                         # below the header: above it is "DMX OUT"
}

POWER_NETS = ("+5V", "+5V_ISO", "*/+5V_ISO")
SUPPLY_NETS = ("GND", "GND_ISO", "*/GND_ISO", "+3V3")
FOOTPRINTS = [footprintlib.RKK]


def named(ref):
    """Whether the board prints the part's name: every part, as you solder them by hand (not the holes)."""
    return not ref.startswith("H")


def set_models(fp, ref):
    """3D models for the pictures: the two 8.5 mm socket strips (KiCad's first model of the Pico's footprint) and
    the Pico with its soldered pin headers (the second) standing on them; KiCad shows the Pico H otherwise."""
    if ref == "U1":
        models = fp.Models()
        for i in range(len(models)):
            models[i].m_Show = i in (0, 1)
        offset = models[1].m_Offset
        models[1].m_Offset = pcbnew.VECTOR3D(offset.x, offset.y, 8.5)


if __name__ == "__main__":
    sys.exit(board.main(sys.modules[__name__], sys.argv[1]))
