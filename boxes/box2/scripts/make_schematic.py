"""Writes the KiCad schematic of dmxbox 2 from circuit.py: a title sheet (../dmxbox2.kicad_sch) and two A3 sheets
(the PIC with USB, its supply, the LEDs, the box number and the programming header; DMX). The drawing itself is
boxes/scripts/schematic.py; this file places the parts, the DMX side where box 1's schematic has it.

Run it with any Python 3.10 or later:  python make_schematic.py
"""
import sys

import box  # noqa: F401  (boxes/scripts on the path)
import schematic

ROOT_UUID = "2e7d1b44-8c3f-4a6a-b025-6c9d3e8f2d02"
DATE = "2026-11-28"
ROOT_TITLE = "dmxbox 2: a PIC32, USB to DMX with RDM"
ROOT_LINES = [
    "dmxbox 2: a USB network adapter for the Lumenet desk (and any Art-Net program): a PIC32MX270F256B with one",
    "DMX output with RDM and the switch for its number. Every part is through-hole, and you solder them yourself.",
    "",
    "The PIC takes its first firmware through J2 from a programmer. You make the cable from the pin header J4 to",
    "the XLR socket in the box wall.",
]

SHEETS = [  # key, title, file, page
    ("pic", "PIC32, USB, LEDs, box number", "dmxbox2_pic32.kicad_sch", "2"),
    ("dmx", "DMX with RDM", "dmxbox2_dmx.kicad_sch", "3"),
]

# ref -> (sheet, x, y, rotation): millimetres on an A3 sheet, y down, rotation counter-clockwise
PLACE = {
    # ---- the PIC, USB, its supply and clock, LEDs, box number, programming header
    "J1": ("pic", 35.56, 63.5, 0), "R1": ("pic", 76.2, 50.8, 0), "R2": ("pic", 86.36, 50.8, 0),
    "C1": ("pic", 96.52, 50.8, 0),
    "U2": ("pic", 264.16, 63.5, 0), "C2": ("pic", 279.4, 76.2, 0),
    "U1": ("pic", 200.66, 142.24, 0),
    "C3": ("pic", 137.16, 63.5, 0), "C4": ("pic", 147.32, 63.5, 0), "C5": ("pic", 157.48, 63.5, 0),
    "C6": ("pic", 167.64, 63.5, 0),
    "R3": ("pic", 137.16, 101.6, 0), "R4": ("pic", 152.4, 101.6, 0), "C9": ("pic", 152.4, 111.76, 0),
    "Y1": ("pic", 137.16, 190.5, 0), "C7": ("pic", 127.0, 203.2, 0), "C8": ("pic", 147.32, 203.2, 0),
    "J2": ("pic", 71.12, 226.06, 0),
    "R21": ("pic", 287.02, 172.72, 90), "D4": ("pic", 297.18, 172.72, 180),
    "R22": ("pic", 287.02, 185.42, 90), "D5": ("pic", 297.18, 185.42, 180),
    "R23": ("pic", 287.02, 198.12, 90), "D6": ("pic", 297.18, 198.12, 180),
    "SW1": ("pic", 309.88, 116.84, 0),
    # ---- DMX with RDM (box 1's places)
    "U5": ("dmx", 71.12, 55.88, 0), "C26": ("dmx", 40.64, 68.58, 0),
    "C29": ("dmx", 106.68, 68.58, 0), "R33": ("dmx", 116.84, 68.58, 0),
    "U6": ("dmx", 172.72, 50.8, 0), "R24": ("dmx", 152.4, 48.26, 90), "R25": ("dmx", 203.2, 43.18, 0),
    "C30": ("dmx", 190.5, 27.94, 0),
    "U7": ("dmx", 172.72, 99.06, 0), "R26": ("dmx", 152.4, 96.52, 90), "R27": ("dmx", 203.2, 91.44, 0),
    "C31": ("dmx", 190.5, 76.2, 0),
    "U8": ("dmx", 172.72, 147.32, 0), "R28": ("dmx", 152.4, 144.78, 90), "R29": ("dmx", 203.2, 139.7, 0),
    "C32": ("dmx", 190.5, 124.46, 0),
    "U9": ("dmx", 269.24, 93.98, 0), "C33": ("dmx", 292.1, 73.66, 0),
    "R30": ("dmx", 309.88, 78.74, 0), "R31": ("dmx", 322.58, 101.6, 0), "R32": ("dmx", 335.28, 124.46, 0),
    "D1": ("dmx", 304.8, 147.32, 90), "D2": ("dmx", 345.44, 147.32, 90), "J4": ("dmx", 388.62, 101.6, 0),
    "H1": ("dmx", 330.2, 220.98, 0), "H2": ("dmx", 342.9, 220.98, 0), "H3": ("dmx", 355.6, 220.98, 0),
    "H4": ("dmx", 368.3, 220.98, 0),
}

# The DC-DC module's pins on its symbol (x, y with y up, angle): input left, output right
DCDC_AT = {"1": (-15.24, 5.08, 0), "2": (-15.24, -5.08, 0), "7": (15.24, 5.08, 180), "5": (15.24, -5.08, 180)}

BLOCKS = {
    "pic": [("USB-C: the computer", 15.24, 22.86, 119.38, 101.6),
            ("PIC32MX270F256B, its decoupling, reset and crystal", 124.46, 45.72, 271.78, 215.9),
            ("3.3 V", 243.84, 22.86, 304.8, 88.9),
            ("Power flags", 15.24, 104.14, 119.38, 132.08),
            ("Programming header (PICkit, MPLAB Snap); PGED is the log too", 50.8, 200.66, 124.46, 245.11),
            ("LEDs", 274.32, 160.02, 330.2, 210.82),
            ("Box number", 274.32, 91.44, 345.44, 152.4)],
    "dmx": [("Isolated 5 V", 15.24, 27.94, 129.54, 88.9),
            ("Optocouplers: send, direction, receive", 132.08, 17.78, 228.6, 165.1),
            ("RS-485 driver, RDM line bias, protection; DMX OUT (XLR in the wall: pad n = XLR pin n)", 231.14, 50.8, 406.4, 165.1),
            ("Holes for M3 screws", 320.04, 205.74, 375.92, 233.68),
            ("Power flag", 22.86, 144.78, 124.46, 167.64)],
}

NOTES = {
    "pic": [(17.78, 248.92, "SW1: switch k down adds 2^(k-1) to the box number n. Box n is 10.1.n.1 and Art-Net "
                            "universe n."),
            (17.78, 254.0, "J2: a programmer writes the firmware (it holds MCLR low meanwhile). PGED carries the log "
                           "(UART2, 250000 baud), PGEC the red LED.")],
    "dmx": [(134.62, 172.72, "GND_ISO and +5V_ISO are the isolated side: no copper joins them to GND or +5V."),
            (134.62, 177.8, "U5: Recom RKK-0505S/H; pins 3, 4 and 6 do not exist. R33 keeps its output near 5 V "
                            "while the line rests.")],
}

FLAGS = {"pic": [("GND", 40.64, 119.38), ("+5V", 66.04, 119.38)], "dmx": [("GND_ISO", 50.8, 154.94)]}

# Reference and value texts placed by hand: ref -> ((dx, dy, justify), (dx, dy, justify))
TEXT_AT = {"U2": ((-2.54, -6.35, "right"), (2.54, -6.35, "left")),
           "U6": ((-4.06, -8.89, None), (3.81, 8.89, "left")), "U7": ((-4.06, -8.89, None), (3.81, 8.89, "left")),
           "U8": ((-4.06, -8.89, None), (3.81, 8.89, "left")),
           "D1": ((3.81, -1.27, "left"), (3.81, 1.27, "left")), "D2": ((3.81, -1.27, "left"), (3.81, 1.27, "left"))}

# Pins joined by one straight wire instead of names (each chain lies on one line, in order)
DIRECT = [
    ("R4.2", "C9.1"),
    ("R21.2", "D4.2"), ("R22.2", "D5.2"), ("R23.2", "D6.2"),
    ("R24.2", "U6.2"), ("R26.2", "U7.2"), ("R28.2", "U8.2"),
]


if __name__ == "__main__":
    sys.exit(schematic.main(sys.modules[__name__]))
