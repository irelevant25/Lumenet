"""Makes the 3D-printed case of dmxbox 4 (PETG): a base and a lid, ready for the slicer, in ../case_print/
(boxes/scripts/printcase.py makes them; case_layout.Printed has the sizes).

Run with CadQuery, for example through uv:
    uv run --no-project --python 3.13 --with cadquery python make_print_case.py

The three USB-C sockets in the front wall, a slot in the side wall in front of SW1's keys, DMX OUT, DMX IN and
MIDI IN and OUT in the back wall behind the board, three light pipes in the lid over the LEDs.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import box  # noqa: E402,F401  (boxes/scripts on the path)
import printcase  # noqa: E402
from case_layout import Printed as P  # noqa: E402

SPEC = {
    "usb": [("OUT", "J2"), ("IN", "J1"), ("POWER", "J3")],
    "leds": [("USB", "D4"), ("DMX", "D5"), ("MIDI", "D6")],
    "back": [("xlr", P.XLR_X, "DMX OUT"), ("xlr_male", P.XLR_IN_X, "DMX IN"), ("din", P.DIN_OUT_X, "MIDI OUT"),
             ("din", P.DIN_IN_X, "MIDI IN")],
    "name": (-4.0, 62.0),
}

if __name__ == "__main__":
    printcase.main(SPEC)
