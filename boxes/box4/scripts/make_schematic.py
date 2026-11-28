"""Writes the KiCad schematic of dmxbox 4 from circuit.py: a title sheet (../dmxbox4.kicad_sch) and four A3
sheets (USB and power, RP2040, DMX OUT and MIDI, DMX IN). The drawing itself is boxes/scripts/schematic.py; this file places
the parts.

Run it with any Python 3.10 or later:  python make_schematic.py
"""
import sys

import box  # noqa: F401  (boxes/scripts on the path)
import schematic

ROOT_UUID = "5f1d2c40-6b7e-4f43-9a2e-2a6d1c3b0d01"
DATE = "2026-11-28"
ROOT_TITLE = "dmxbox 4: USB to DMX with RDM, DMX IN, MIDI and a USB hub for chaining"
ROOT_LINES = [
    "dmxbox 4: a USB network adapter for the Lumenet desk (and any Art-Net program) with one DMX output with "
    "RDM, one DMX input,",
    "MIDI IN and OUT, and a USB hub so that boxes can be chained: computer -> box 1 -> box 2 -> ... (up to 5 on one "
    "port).",
    "",
    "JLCPCB solders every part (LCSC numbers in the LCSC field). You make the cables from the pin headers J4, J8,",
    "J5 and J6 to the XLR and DIN sockets in the box wall.",
]

SHEETS = [  # key, title, file, page
    ("power", "USB, power and hub", "dmxbox4_power.kicad_sch", "2"),
    ("mcu", "RP2040, flash, LEDs, box number", "dmxbox4_rp2040.kicad_sch", "3"),
    ("dmx", "DMX with RDM, MIDI", "dmxbox4_dmx_midi.kicad_sch", "4"),
    ("input", "DMX IN", "dmxbox4_dmx_in.kicad_sch", "5"),
]

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
    # ---- DMX IN: the receive half of the DMX side again (U8's and U9's places on the DMX sheet)
    "U12": ("input", 172.72, 101.6, 0), "R37": ("input", 152.4, 99.06, 90), "R38": ("input", 203.2, 93.98, 0),
    "C36": ("input", 190.5, 78.74, 0),
    "U11": ("input", 269.24, 101.6, 0), "C35": ("input", 292.1, 81.28, 0),
    "R39": ("input", 309.88, 86.36, 0), "R40": ("input", 322.58, 109.22, 0), "R41": ("input", 335.28, 132.08, 0),
    "D7": ("input", 320.04, 154.94, 0), "J8": ("input", 388.62, 109.22, 0),
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
    "input": [("Optocoupler: DMX IN to the RP2040 (GPIO 16)", 127.0, 58.42, 228.6, 129.54),
              ("RS-485 receiver, line bias and end, protection; DMX IN (XLR male in the wall: pad n = XLR pin n)",
               231.14, 58.42, 406.4, 172.72)],
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
    "input": [(129.54, 185.42, "DMX IN shares the isolated side of DMX OUT: GND_ISO and +5V_ISO (U5 on the DMX "
                               "sheet)."),
              (129.54, 190.5, "U11 only listens: its DE and /RE are low. R39 to R41 bias and end the console's "
                              "line, so an open socket reads as a resting line.")],
}

FLAGS = {"power": [("GND", 322.58, 160.02), ("+5V", 342.9, 160.02)], "dmx": [("GND_ISO", 50.8, 154.94)]}

# Reference and value texts placed by hand: ref -> ((dx, dy, justify), (dx, dy, justify))
TEXT_AT = {"U4": ((-2.54, -6.35, "right"), (2.54, -6.35, "left")),
           "U6": ((-4.06, -8.89, None), (3.81, 8.89, "left")), "U7": ((-4.06, -8.89, None), (3.81, 8.89, "left")),
           "U8": ((-4.06, -8.89, None), (3.81, 8.89, "left")),
           "U12": ((-4.06, -8.89, None), (3.81, 8.89, "left")),
           "D3": ((-1.52, -0.64, "right"), (1.52, 0.0, "left"))}

# Pins joined by one straight wire instead of names (each chain lies on one line, in order)
DIRECT = [
    ("R21.2", "D4.2"), ("R22.2", "D5.2"), ("R23.2", "D6.2"),
    ("R24.2", "U6.2"), ("R26.2", "U7.2"), ("R28.2", "U8.2"), ("R37.2", "U12.2"),
    ("J5.4", "R33.1"), ("R33.2", "D3.1", "U10.1"), ("D3.2", "U10.2"),
]


if __name__ == "__main__":
    sys.exit(schematic.main(sys.modules[__name__]))
