"""Where dmxbox 4's board sits in its case (JLCMC K119-12063-H7-L130), shared by make_case.py (the holes) and
make_3d.py (the pictures). Plain Python, so Blender can use it too.

Millimetres, in the case model's coordinates: x across (+x the SW1 side), y up, z along the case (0 to 130; the
front plate from -1.5 to 0, the back plate from 130 to 131.5). The case is 120 x 63 mm, split at y = 0 into a top
and a bottom half. Inside, it is 110.3 mm wide (x -55.15 to 55.15) and 59 mm high (y -29.5 to 29.5). Ribs 2.3 mm
high run along the floor and the ceiling (RIBS), board slots along the side walls, and screw channels for the end
plates' screws along the four corners.
"""
import box  # noqa: F401  (boxes/scripts on the path)
from placement import board_layout  # noqa: F401  (W, H and PLACE of make_board.py)

FLOOR, CEILING, WALL_X = -29.5, 29.5, 55.15      # the inside: the floor and ceiling between the ribs, the side walls
TOP_OUTER, BOTTOM_OUTER = 31.5, -31.5
TUBE_L, PLATE_T = 130.0, 1.5
RIBS = [(-50.65, -48.65), (-46.65, -44.65), (-4.95, -2.95), (-0.95, 1.05), (3.05, 4.95), (44.65, 46.65),
        (48.65, 50.65)]                # the floor's ribs (x from, to), up to y = -27.2
RIB_H = 2.3
SPACER = 15.0                      # nylon M3 spacers: the board's underside 15 mm above the floor
BOARD_T = 1.6
EDGE_X = WALL_X - 0.4              # the board's SW1 edge (its y = 0) 0.4 mm from the side wall
USB_OVERHANG = 0.75                # the USB-C sockets' fronts stand this far beyond the board's edge
EDGE_Z = USB_OVERHANG + 0.3        # the board's USB edge (its x = 0): the sockets end 0.3 mm from the front plate
BOARD_TOP = FLOOR + SPACER + BOARD_T

# The sockets of the back plate (x, y of their middles), behind the board: one row in the middle, the XLRs upright
# (latch at the top, flange 26.4 mm wide), the four flanges 4 mm apart
SOCKET_Y = 0.0
XLR_X, XLR_IN_X, DIN_OUT_X, DIN_IN_X = 38.2, 7.8, -18.9, -41.9


class Printed:
    """The case printed in PETG (make_print_case.py): a base, open at the top, and a lid. Same idea of axes, x across
    (+x the SW1 side), y up, z along the box, but here y = 0 is the base's inner floor and z = 0 the front wall's
    inner face. The board stands on four posts of the floor (M3 heat-set inserts in them) and is held by four M3
    standoffs (male-female, 25 mm) through its holes, on which the lid is screwed: no posts in the corners, where
    the board is. The sockets of the back plate sit behind the board, so nothing of them reaches over it: the XLR's
    solder cups end 4.7 mm behind the standoffs at the board's DMX edge. The back wall holds four sockets in a row
    (the XLRs' flanges are 36 mm wide), so the case is wider than the board."""
    INNER_W, INNER_L, INNER_H = 119.0, 122.0, 31.6    # inside the base; the lid's underside at INNER_H
    WALL, BACK_WALL, FLOOR_T, LID_T = 2.2, 3.0, 2.0, 2.4
    CORNER_R = 3.0                     # the outer vertical edges
    POST_H, POST_D, INSERT_D, INSERT_DEPTH = 5.0, 8.0, 4.0, 6.0   # M3 x 5.7 heat-set inserts (4.6 mm wide)
    STANDOFF = 25.0                    # from the board's top to the lid's underside
    EDGE_X = INNER_W / 2 - 0.4         # the board's SW1 edge, 0.4 mm from the side wall
    EDGE_Z = USB_OVERHANG + 0.3        # its USB edge: the sockets end 0.3 mm from the front wall
    BOARD_BOTTOM = POST_H
    BOARD_TOP = POST_H + BOARD_T
    # the back wall, seen from behind: MIDI IN, MIDI OUT, DMX IN, DMX OUT (x of their middles), all at one height,
    # 2.5 mm between the flanges
    SOCKET_Y = 14.8
    XLR_X, XLR_IN_X, DIN_OUT_X, DIN_IN_X = 41.0, 2.5, -27.5, -49.0


def to_case(xb, yb):
    """A point of the board (mm from its top left corner, as in make_board.py) in the case: (x, z)."""
    return EDGE_X - yb, EDGE_Z + xb
