"""Writes the KiCad schematic of dmxbox 1 from circuit.py: a title sheet (../dmxbox1.kicad_sch) and two A3 sheets
(the Pico with the LEDs, the box number and the service pins; DMX). The drawing itself is
boxes/scripts/schematic.py; this file places the parts, the DMX side where box 4's schematic has it.

Run it with any Python 3.10 or later:  python make_schematic.py
"""
import sys

import box  # noqa: F401  (boxes/scripts on the path)
import schematic

ROOT_UUID = "1d6c0f3a-7b2e-4c59-9e14-5a8b2f7c1d01"
DATE = "2026-11-28"
ROOT_TITLE = "dmxbox 1: a Raspberry Pi Pico, USB to DMX with RDM"
ROOT_LINES = [
    "dmxbox 1: a USB network adapter for the Lumenet desk (and any Art-Net program): a Raspberry Pi Pico with one",
    "DMX output with RDM and the switch for its number. Every part is through-hole, and you solder them yourself.",
    "",
    "The Pico sits in two socket strips; its micro-USB is the box's USB socket. You make the cable from the pin",
    "header J4 to the XLR socket in the box wall.",
]

SHEETS = [  # key, title, file, page
    ("pico", "Pico, LEDs, box number", "dmxbox1_pico.kicad_sch", "2"),
    ("dmx", "DMX with RDM", "dmxbox1_dmx.kicad_sch", "3"),
]

# ref -> (sheet, x, y, rotation): millimetres on an A3 sheet, y down, rotation counter-clockwise
PLACE = {
    # ---- the Pico, LEDs, box number, service pins
    "U1": ("pico", 190.5, 142.24, 0),
    "J7": ("pico", 86.36, 218.44, 180),
    "R21": ("pico", 287.02, 172.72, 90), "D4": ("pico", 297.18, 172.72, 180),
    "R22": ("pico", 287.02, 185.42, 90), "D5": ("pico", 297.18, 185.42, 180),
    "R23": ("pico", 287.02, 198.12, 90), "D6": ("pico", 297.18, 198.12, 180),
    "SW1": ("pico", 309.88, 116.84, 0),
    # ---- DMX with RDM (box 4's places)
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
    "pico": [("Raspberry Pi Pico (in two socket strips)", 124.46, 86.36, 271.78, 200.66),
             ("Power flag", 50.8, 86.36, 121.92, 114.3),
             ("Service pins", 50.8, 200.66, 121.92, 238.76),
             ("LEDs", 274.32, 160.02, 330.2, 210.82),
             ("Box number", 274.32, 91.44, 345.44, 152.4)],
    "dmx": [("Isolated 5 V", 15.24, 27.94, 129.54, 88.9),
            ("Optocouplers: send, direction, receive", 132.08, 17.78, 228.6, 165.1),
            ("RS-485 driver, RDM line bias, protection; DMX OUT (XLR in the wall: pad n = XLR pin n)", 231.14, 50.8, 406.4, 165.1),
            ("Holes for M3 screws", 320.04, 205.74, 375.92, 233.68),
            ("Power flag", 22.86, 144.78, 124.46, 167.64)],
}

NOTES = {
    "pico": [(17.78, 248.92, "SW1: switch k down adds 2^(k-1) to the box number n. Box n is 10.1.n.1 and Art-Net "
                             "universe n."),
             (17.78, 254.0, "The Pico's BOOTSEL button while plugging in starts its USB boot loader; J7 RUN to GND "
                            "restarts it. The Pico measures VSYS itself (GPIO 29).")],
    "dmx": [(134.62, 172.72, "GND_ISO and +5V_ISO are the isolated side: no copper joins them to GND or +5V."),
            (134.62, 177.8, "U5: Recom RKK-0505S/H; pins 3, 4 and 6 do not exist. R33 keeps its output near 5 V "
                            "while the line rests.")],
}

FLAGS = {"pico": [("+5V", 86.36, 106.68)], "dmx": [("GND_ISO", 50.8, 154.94)]}

# Reference and value texts placed by hand: ref -> ((dx, dy, justify), (dx, dy, justify))
TEXT_AT = {"U6": ((-4.06, -8.89, None), (3.81, 8.89, "left")), "U7": ((-4.06, -8.89, None), (3.81, 8.89, "left")),
           "U8": ((-4.06, -8.89, None), (3.81, 8.89, "left")),
           "D1": ((3.81, -1.27, "left"), (3.81, 1.27, "left")), "D2": ((3.81, -1.27, "left"), (3.81, 1.27, "left"))}

# Pins joined by one straight wire instead of names (each chain lies on one line, in order)
DIRECT = [
    ("R21.2", "D4.2"), ("R22.2", "D5.2"), ("R23.2", "D6.2"),
    ("R24.2", "U6.2"), ("R26.2", "U7.2"), ("R28.2", "U8.2"),
]


if __name__ == "__main__":
    sys.exit(schematic.main(sys.modules[__name__]))
