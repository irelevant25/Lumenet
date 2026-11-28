"""Makes the 3D-printed case of dmxbox 2 (PETG): a base and a lid, ready for the slicer, in ../case_print/
(boxes/scripts/printcase.py makes them; case_layout.Printed has the sizes).

Run with CadQuery, for example through uv:
    uv run --no-project --python 3.13 --with cadquery python make_print_case.py

The USB-C socket in the front wall, a slot in the side wall in front of SW1's keys, DMX OUT in the back wall
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
USB_X, USB_Y = PLACE["J1"][0], PLACE["J1"][1] + 2.975   # the middle of the USB4085's mouth: 2.975 mm along from its pin A1

SPEC = {
    "usb": [("USB", (USB_X, USB_Y))],
    "usb_height": cl.USB_HEIGHT,
    "key_xs": [PLACE["SW1"][0] - 17.78 + 2.54 * k for k in range(8)],   # the keys from the left: pin 8's first
    "key_height": cl.KEY_HEIGHT,
    "leds": [("USB", cl.LEDS["D4"]), ("DMX", cl.LEDS["D5"]), ("RDM", cl.LEDS["D6"])],
    "back": [("xlr", P.XLR_X, "DMX OUT")],
    "name": (0.0, 75.0),
}

if __name__ == "__main__":
    printcase.main(SPEC)
