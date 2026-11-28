"""Makes the 3D-printed case of dmxbox 1 (PETG): a base and a lid, ready for the slicer, in ../case_print/
(boxes/scripts/printcase.py makes them; case_layout.Printed has the sizes).

Run with CadQuery, for example through uv:
    uv run --no-project --python 3.13 --with cadquery python make_print_case.py

The Pico's micro-USB in the front wall, a slot in the side wall in front of SW1's keys, DMX OUT in the back wall
behind the board, three light pipes in the lid over the LEDs.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import box  # noqa: E402,F401  (boxes/scripts on the path)
import case_layout as cl  # noqa: E402
import printcase  # noqa: E402
from case_layout import Printed as P  # noqa: E402

_, _, PLACE = cl.board_layout()
PICO_X, PICO_Y = PLACE["U1"][:2]       # its pin 1; the micro-USB is between its two rows of pins, 17.78 mm apart

SPEC = {
    "usb": [("USB", (PICO_X, PICO_Y - 17.78 / 2))],
    "usb_hole": (12.0, 8.0, 1.5),      # a micro-USB plug's moulding
    "usb_height": cl.USB_HEIGHT,
    "key_xs": [PLACE["SW1"][0] - 17.78 + 2.54 * k for k in range(8)],   # the keys from the left: pin 8's first
    "key_height": cl.KEY_HEIGHT,
    "leds": [("USB", cl.LEDS["D4"]), ("DMX", cl.LEDS["D5"]), ("RDM", cl.LEDS["D6"])],
    "back": [("xlr", P.XLR_X, "DMX OUT")],
    "name": (0.0, 75.0),
}

if __name__ == "__main__":
    printcase.main(SPEC)
