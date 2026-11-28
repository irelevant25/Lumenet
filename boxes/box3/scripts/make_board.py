"""Places the parts of dmxbox 3 on its board (../dmxbox3.kicad_pcb), from KiCad's own net list of the schematic.
The placing itself is boxes/scripts/board.py; this file says where every part goes.

Run with KiCad's Python (it has pcbnew):  python make_board.py NETLIST
The net list comes from:  kicad-cli sch export netlist --format kicadsexpr -o NETLIST dmxbox3.kicad_sch
route.py routes the result; make_jlcpcb.py writes the files for JLCPCB.

The board: 86 x 66 mm, two layers: box 4's board without the hub, OUT, POWER and MIDI, its other parts where box 4
has them. Left edge: the USB-C socket, level with the RP2040's USB resistors. Top edge: the box-number switch, its
keys at the edge. Bottom left: the LEDs; right edge: the pin header of the DMX socket (the socket sits in the box
wall, on a cable). The isolated DMX side is right of a 2.5 mm gap that no track, via or copper pour crosses; only
the optocouplers and the DC-DC converter bridge it. Every part is on the top side.
"""
import sys

import box  # noqa: F401  (boxes/scripts on the path)
import board
import footprintlib

W, H = 86.0, 66.0
GAP_X1, GAP_X2 = 60.2, 62.7          # the isolation gap: logic left, isolated right

# ref -> (x, y, rotation in degrees, counter-clockwise). Millimetres from the board's top left corner.
PLACE = {
    # the USB-C socket at the left edge, mouth out; CC1's resistor above its pads, CC2's below
    "J1": (3.2, 32.5, 270), "R1": (10.8, 29.0, 0), "R2": (10.8, 36.4, 0), "C1": (10.8, 27.0, 0),
    "C3": (21.0, 54.7, 0), "C4": (21.0, 56.7, 0),
    "R14": (26.0, 51.9, 90), "R15": (27.6, 51.9, 90), "C6": (29.2, 51.9, 90),
    # 3.3 V
    "U4": (20.6, 50.1, 0), "C7": (20.6, 47.1, 0), "C8": (24.0, 50.1, 90),
    # RP2040 and around it (as on box 4)
    "U1": (32.0, 34.5, 90),
    "C16": (29.4, 28.1, 90), "C19": (27.8, 28.1, 90), "C20": (26.2, 28.1, 90), "C21": (24.6, 28.1, 90),
    "R19": (23.6, 31.7, 180), "R20": (23.6, 33.3, 180),
    "C18": (24.0, 35.9, 0), "C17": (24.0, 37.5, 0), "C23": (24.0, 39.1, 0),
    "U3": (21.2, 43.3, 0), "C27": (27.4, 43.1, 90), "R18": (27.4, 47.1, 90),
    "C15": (36.6, 28.9, 90), "C14": (36.8, 40.1, 90), "C22": (38.4, 40.1, 90),
    "C12": (29.2, 40.9, 90), "C13": (34.6, 41.1, 90),
    "Y1": (39.8, 33.3, 90), "C24": (42.8, 35.1, 90), "C25": (42.8, 31.5, 90), "R16": (39.0, 29.9, 0),
    "R17": (41.6, 28.1, 0),
    "J7": (47.6, 23.3, 90),
    # box number: piano switch at the top edge, keys out, above the RP2040
    "SW1": (32.01, 5.85, 0),
    # LEDs at the bottom left, 4.4 mm apart, under 3 mm light pipes in the case's top
    "D4": (9.2, 62.4, 0), "D5": (13.6, 62.4, 0), "D6": (18.0, 62.4, 0),
    "R21": (9.2, 59.8, 0), "R22": (13.6, 59.8, 0), "R23": (18.0, 59.8, 0),
    # DMX: the logic side of the optocouplers (the optocouplers by their middle: their pads left and right of the gap)
    "R24": (54.2, 13.04, 0), "R26": (54.2, 27.5, 0), "R29": (54.2, 39.04, 0), "C32": (54.2, 43.9, 0),
    "U6": (62.01, 14.31, 0), "U7": (62.01, 27.31, 0), "U8": (62.01, 40.31, 180),
    # the DC-DC module along the gap's crossing: pins 1, 2 (and the free 8) left of it, 4 and 5 right
    "U5": (60.2, 53.0, 0), "C28": (57.66, 61.1, 180),
    # DMX: the isolated side
    "C29": (68.4, 53.0, 270), "C30": (69.2, 10.5, 90), "R25": (69.2, 14.9, 90), "C31": (69.2, 23.5, 90),
    "R27": (69.2, 27.9, 90), "R28": (70.2, 41.1, 0),
    "U9": (76.0, 16.5, 0), "C33": (76.0, 21.5, 0),
    "R30": (72.0, 34.5, 90), "R31": (73.2, 39.1, 0), "R32": (72.0, 43.7, 90), "D2": (76.4, 45.7, 0),
    "J4": (79.0, 35.58, 0),
    # holes
    "H1": (4.0, 8.5, 0), "H2": (82.0, 8.5, 0), "H3": (4.0, 62.0, 0), "H4": (82.0, 62.0, 0),
}

# GND vias in the exposed pad of the RP2040 (0.3 mm holes: no extra charge at JLCPCB)
EP_VIAS = [(32.0 + dx, 34.5 + dy) for dx in (-0.8, 0.8) for dy in (-0.8, 0.8)]

SILK = [  # (text, x, y, size): labels on the board
    ("USB", 9.2, 64.2, 0.8), ("DMX", 13.6, 64.2, 0.8), ("RDM", 18.0, 64.2, 0.8),
    ("USB", 4.0, 25.9, 1.0),
    ("DMX OUT", 79.0, 26.1, 1.0), ("1 COM", 84.0, 30.5, 0.8), ("2 D-", 83.8, 33.04, 0.8),
    ("3 D+", 83.8, 35.58, 0.8),
    ("ISOLATED", 73.0, 57.0, 1.2), ("dmxbox 3", 73.0, 60.8, 1.6), ("Lumenet", 73.0, 63.8, 1.0),
] + [  # the box number: under each key of SW1 what it adds
    (str(2 ** k), 23.12 + 2.54 * k, 12.3, 0.8) for k in range(8)
] + [  # the service pins J7: each name above its pin, reading upwards
    (name, 41.25 + 2.54 * i, 20.2, 0.8, 90) for i, name in enumerate(("GND", "BOOT", "RUN", "SWCLK", "SWDIO", "LOG"))
]
REF_AT = {  # part names away from their footprint's own place, where that is taken: x, y
    "U5": (52.0, 53.0),                         # left of the DC-DC module: above it is U8's name
    "U4": (16.6, 50.1),                         # left of the regulator: above it are C7's pads
}

POWER_NETS = ("+5V", "+5V_ISO", "*/+5V_ISO")
SUPPLY_NETS = ("GND", "GND_ISO", "*/GND_ISO", "+3V3", "*/+1V1")
FOOTPRINTS = [footprintlib.DCDC, footprintlib.SWITCH, footprintlib.HEADER5, footprintlib.SOCKET6]


def named(ref):
    """Whether the board prints the part's name: the parts you handle and the chips, not the small ones."""
    return not (ref[0] in "RCQ" or ref in ("D2", "Y1", "H1", "H2", "H3", "H4"))


def set_models(fp, ref):
    """3D models for the pictures: KiCad has none for the HRO USB-C socket (the GCT USB4105 has the same outline
    and stands in for it). The DC-DC module has none at all: make_3d.py makes it."""
    if ref == "J1":
        fp.Models()[0].m_Filename = ("${KICAD10_3DMODEL_DIR}/Connector_USB.3dshapes/"
                                     "USB_C_Receptacle_GCT_USB4105-xx-A_16P_TopMnt_Horizontal.step")


if __name__ == "__main__":
    sys.exit(board.main(sys.modules[__name__], sys.argv[1]))
