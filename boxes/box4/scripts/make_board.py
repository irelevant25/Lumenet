"""Places the parts of dmxbox 4 on its board (../dmxbox4.kicad_pcb), from KiCad's own net list of the schematic.
The placing itself is boxes/scripts/board.py; this file says where every part goes.

Run with KiCad's Python (it has pcbnew):  python make_board.py NETLIST
The net list comes from:  kicad-cli sch export netlist --format kicadsexpr -o NETLIST dmxbox4.kicad_sch
route.py routes the result; make_jlcpcb.py writes the files for JLCPCB.

The board: 86 x 82 mm, two layers. Left edge: the three USB-C sockets (OUT, IN, POWER). Top edge: the
box-number switch, its keys at the edge. Bottom edge: the LEDs and the pin headers of the MIDI sockets; right edge:
the pin headers of the DMX OUT and DMX IN sockets (the sockets sit in the box wall, on cables). DMX IN is the
receive half of the DMX side again, below the DC-DC module (its optocoupler U12 across the gap) and DMX OUT. The isolated DMX side is right of a
2.5 mm gap that no track, via or copper pour crosses; only the optocouplers and the DC-DC converter bridge it.
Every part is on the top side (JLCPCB's economic assembly places one side only).
"""
import sys

import box  # noqa: F401  (boxes/scripts on the path)
import board
import footprintlib

W, H = 86.0, 82.0
GAP_X1, GAP_X2 = 60.2, 62.7          # the isolation gap: logic left, isolated right

# ref -> (x, y, rotation in degrees, counter-clockwise). Millimetres from the board's top left corner.
PLACE = {
    # USB-C sockets at the left edge, mouths out: OUT on top, IN, POWER
    "J2": (3.2, 25.5, 270), "J1": (3.2, 40.5, 270), "J3": (3.2, 58.5, 270),
    # OUT: the switch that gives the next box 5 V, and the CC resistors
    "Q2": (11.0, 13.5, 0), "Q3": (15.2, 13.5, 0), "Q4": (17.6, 17.3, 0), "R11": (12.0, 16.9, 0),
    "R12": (17.6, 20.1, 0), "R13": (12.0, 18.7, 0), "C5": (9.4, 17.1, 90),
    "R5": (10.6, 21.5, 0), "R6": (14.0, 21.5, 0), "R7": (10.6, 23.1, 0), "R8": (14.0, 23.1, 0),
    # IN: CC resistors, the switch Q1 that POWER turns off
    "R1": (11.0, 44.1, 0), "R2": (11.0, 45.7, 0), "Q1": (12.4, 49.1, 0), "R9": (16.2, 48.5, 90),
    "R10": (17.8, 48.5, 90), "C1": (10.0, 51.9, 0),
    # POWER: CC resistors and the diode
    "D1": (13.0, 57.5, 90), "R3": (16.6, 56.5, 0), "R4": (16.6, 58.1, 0), "C2": (16.6, 54.9, 0),
    "C3": (21.0, 54.7, 0), "C4": (21.0, 56.7, 0),
    "R14": (26.0, 51.9, 90), "R15": (27.6, 51.9, 90), "C6": (29.2, 51.9, 90),
    # 3.3 V
    "U4": (20.6, 50.1, 0), "C7": (20.6, 47.1, 0), "C8": (24.0, 50.1, 90),
    # USB hub between OUT and IN
    "U2": (16.0, 32.5, 180), "Y2": (19.4, 37.1, 0), "C9": (12.6, 36.5, 90), "C10": (14.4, 37.1, 90),
    "C11": (16.2, 37.1, 90),
    # RP2040 and around it
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
    # the keys towards the top edge: the pads on their side end 0.35 mm from it, the keys 0.25 mm
    "SW1": (32.01, 5.85, 0),
    # LEDs at the bottom edge, left: 4.4 mm apart, under 3 mm light pipes in the case's top
    "D4": (9.2, 79.4, 0), "D5": (13.6, 79.4, 0), "D6": (18.0, 79.4, 0),
    "R21": (9.2, 76.8, 0), "R22": (13.6, 76.8, 0), "R23": (18.0, 76.8, 0),
    # DMX: the logic side of the optocouplers
    # (the optocouplers by their middle: their gull-wing pads sit left and right of the gap)
    "R24": (54.2, 13.04, 0), "R26": (54.2, 27.5, 0), "R29": (54.2, 39.04, 0), "C32": (54.2, 43.9, 0),
    "U6": (62.01, 14.31, 0), "U7": (62.01, 27.31, 0), "U8": (62.01, 40.31, 180),
    # the DC-DC module along the gap's crossing: pins 1, 2 (and the free 8) left of it, 4 and 5 right
    "U5": (60.2, 53.0, 0), "C28": (52.0, 58.0, 90),
    # DMX: the isolated side
    "C29": (68.4, 53.0, 270), "C30": (69.2, 10.5, 90), "R25": (69.2, 14.9, 90), "C31": (69.2, 23.5, 90),
    "R27": (69.2, 27.9, 90), "R28": (70.2, 41.1, 0),
    "U9": (76.0, 16.5, 0), "C33": (76.0, 21.5, 0),
    "R30": (72.0, 34.5, 90), "R31": (73.2, 39.1, 0), "R32": (72.0, 43.7, 90), "D2": (76.4, 45.7, 0),
    "J4": (79.0, 35.58, 0),
    # DMX IN: its optocoupler below the DC-DC module, the receiver and the line's parts below DMX OUT
    "R38": (53.6, 63.7, 0), "C36": (53.6, 67.6, 0),
    "U12": (62.01, 65.0, 180), "R37": (70.2, 66.3, 0),
    "U11": (72.2, 58.0, 90), "C35": (72.2, 63.3, 0),
    "J8": (79.0, 58.6, 0), "R39": (74.6, 66.6, 90), "R40": (78.5, 67.6, 0), "R41": (74.6, 70.2, 90),
    "D7": (78.5, 71.4, 0),
    # MIDI at the bottom edge
    "U10": (39.81, 59.04, 0), "R33": (31.4, 61.1, 0), "D3": (31.4, 63.5, 0), "R34": (46.6, 58.5, 90),
    "C34": (46.6, 54.7, 90),
    "J5": (29.08, 78.3, 90), "J6": (51.08, 78.3, 90), "R35": (57.6, 73.0, 90), "R36": (55.8, 73.0, 90),
    # holes
    "H1": (4.0, 8.5, 0), "H2": (82.0, 8.5, 0), "H3": (4.0, 78.0, 0), "H4": (82.0, 78.0, 0),
}

# GND vias in the exposed pads of the RP2040 and the hub (0.3 mm holes: no extra charge at JLCPCB)
EP_VIAS = ([(32.0 + dx, 34.5 + dy) for dx in (-0.8, 0.8) for dy in (-0.8, 0.8)]
           + [(16.0 + dx, 32.5 + dy) for dx in (-0.6, 0.6) for dy in (-0.6, 0.6)])

SILK = [  # (text, x, y, size): labels on the board
    ("USB", 9.2, 81.2, 0.8), ("DMX", 13.6, 81.2, 0.8), ("MIDI", 18.0, 81.2, 0.8),
    ("OUT", 4.0, 18.7, 1.0), ("IN", 4.0, 33.9, 1.0), ("POWER", 4.6, 51.9, 1.0),
    ("MIDI IN", 29.08, 74.3, 1.0), ("MIDI OUT", 51.08, 74.3, 1.0),
    ("DMX OUT", 79.0, 26.1, 1.0), ("1 COM", 84.0, 30.5, 0.8), ("2 D-", 83.8, 33.04, 0.8),
    ("3 D+", 83.8, 35.58, 0.8), ("1", 21.9, 78.3, 0.8), ("1", 43.9, 78.3, 0.8),
    ("DMX IN", 79.0, 49.4, 1.0), ("1 COM", 84.0, 53.52, 0.8), ("2 D-", 83.8, 56.06, 0.8), ("3 D+", 83.8, 58.6, 0.8),
    ("ISOLATED", 70.5, 74.5, 1.2), ("dmxbox 4", 70.5, 77.8, 1.6), ("Lumenet", 70.5, 80.6, 1.0),
] + [  # the box number: under each key of SW1 what it adds
    (str(2 ** k), 23.12 + 2.54 * k, 12.3, 0.8) for k in range(8)
] + [  # the service pins J7: each name above its pin, reading upwards
    (name, 41.25 + 2.54 * i, 20.2, 0.8, 90) for i, name in enumerate(("GND", "BOOT", "RUN", "SWCLK", "SWDIO", "LOG"))
]
REF_AT = {  # part names away from their footprint's own place, where that is taken: x, y
    "U5": (52.0, 53.0),                         # left of the DC-DC module: above it is U8's name
    "J5": (36.5, 78.3), "J6": (58.5, 78.3),     # the right ends of the MIDI headers: the "1" marks the left
}

POWER_NETS = ("VBUS_IN", "VBUS_PWR", "VBUS_OUT", "+5V", "*/OUT_S", "OUT_S", "+5V_ISO", "*/+5V_ISO")
SUPPLY_NETS = ("GND", "GND_ISO", "*/GND_ISO", "+3V3", "*/+1V1", "*/HUB_3V3")
FOOTPRINTS = [footprintlib.DCDC, footprintlib.SWITCH, footprintlib.HEADER5, footprintlib.SOCKET6]


def named(ref):
    """Whether the board prints the part's name: the parts you handle and the chips, not the small ones."""
    return not (ref[0] in "RCQ" or ref in ("D1", "D2", "D3", "Y1", "Y2", "H1", "H2", "H3", "H4"))


def set_models(fp, ref):
    """3D models for the pictures: KiCad has none for the HRO USB-C socket (the GCT USB4105 has the same outline
    and stands in for it). The DC-DC module has none at all: make_3d.py makes it."""
    models = fp.Models()
    if ref in ("J1", "J2", "J3"):
        models[0].m_Filename = ("${KICAD10_3DMODEL_DIR}/Connector_USB.3dshapes/"
                                "USB_C_Receptacle_GCT_USB4105-xx-A_16P_TopMnt_Horizontal.step")


if __name__ == "__main__":
    sys.exit(board.main(sys.modules[__name__], sys.argv[1]))
