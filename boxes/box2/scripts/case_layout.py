"""Where dmxbox 2's board sits in its printed case, shared by make_print_case.py (the case) and make_3d.py (the
pictures). Plain Python, so Blender can use it too.

The case is box 1's (the same board size, holes and switch): the USB-C socket in the front wall, and DMX OUT in the
middle of the back wall.
"""
import box  # noqa: F401  (boxes/scripts on the path)
from placement import board_layout  # noqa: F401  (W, H and PLACE of make_board.py)

USB_OVERHANG = 0.7                 # the front of the USB-C socket (USB4085) stands this far beyond the board's edge
USB_HEIGHT = 1.72                  # the middle of its mouth above the board
KEY_HEIGHT = 9.3                   # the middle of SW1's keys above the board
LED_TOP = 8.3                      # the 3 mm LEDs' tops above the board: the light pipes start above them
LEDS = {"D4": (5.27, 19.0), "D5": (10.77, 19.0), "D6": (16.27, 19.0)}   # the LEDs' middles on the board


class Printed:
    """The case printed in PETG: a base, open at the top, and a lid. x across (+x the SW1 side), y up, z along the
    box; y = 0 is the base's inner floor and z = 0 the front wall's inner face. The board stands on four posts of
    the floor (M3 heat-set inserts in them) and is held by four M3 standoffs (male-female, 25 mm) through its
    holes, on which the lid is screwed. The XLR socket of the back wall sits behind the board."""
    INNER_W, INNER_L, INNER_H = 66.0, 136.0, 31.6     # inside the base; the lid's underside at INNER_H
    WALL, BACK_WALL, FLOOR_T, LID_T = 2.2, 3.0, 2.0, 2.4
    CORNER_R = 3.0                     # the outer vertical edges
    POST_H, POST_D, INSERT_D, INSERT_DEPTH = 5.0, 8.0, 4.0, 6.0   # M3 x 5.7 heat-set inserts (4.6 mm wide)
    STANDOFF = 25.0                    # from the board's top to the lid's underside
    EDGE_X = INNER_W / 2 - 0.4         # the board's SW1 edge, 0.4 mm from the side wall
    EDGE_Z = USB_OVERHANG + 0.3        # its USB edge: the socket ends 0.3 mm from the front wall
    BOARD_BOTTOM = POST_H
    BOARD_TOP = POST_H + 1.6
    # the back wall: DMX OUT in the middle
    SOCKET_Y = 14.8
    XLR_X = 0.0


def to_case(xb, yb):
    """A point of the board (mm from its top left corner, as in make_board.py) in the case: (x, z)."""
    return Printed.EDGE_X - yb, Printed.EDGE_Z + xb
