"""Where dmxbox 3's board sits in its case (JLCMC K90-9848-H6-L120), shared by make_case.py (the holes) and
make_3d.py (the pictures). Plain Python, so Blender can use it too.

Millimetres, in the case model's coordinates: x across, y up, z along the tube (0 to 120; the front plate from -1.5
to 0, the back plate from 120 to 121.5). Inside, the tube is 93.2 mm wide (x -46.6 to 46.6) and 44 mm high (y -22.1
to 21.9); its side walls carry board slots and ribs up to y -8.5, and screw channels for the end plates run along
the four corners (at the floor they reach 5.4 mm in from the side walls).
"""
import ast
import os

HERE = os.path.dirname(os.path.abspath(__file__))

FLOOR, CEILING, WALL_X = -22.1, 21.9, 46.6       # the tube's inside
TOP_OUTER, BOTTOM_OUTER = 24.2, -23.9            # its top, and its bottom between the two strips it stands on
SPACER = 15.0                      # nylon M3 spacers: the board's underside 15 mm above the floor, above the ribs
BOARD_T = 1.6
EDGE_X = WALL_X - 0.4              # the board's SW1 edge (its y = 0) 0.4 mm from the side wall
USB_OVERHANG = 0.75                # the USB-C sockets' fronts stand this far beyond the board's edge
EDGE_Z = USB_OVERHANG + 0.3        # the board's USB edge (its x = 0): the sockets end 0.3 mm from the front plate
BOARD_TOP = FLOOR + SPACER + BOARD_T

# The sockets of the back plate (x, y of their middles): 4 mm above the middle, so the XLR's solder cups, 34 mm
# deep, pass above the board's DMX edge
SOCKET_Y = 4.0
XLR_X, DIN_OUT_X, DIN_IN_X = 24.0, -6.0, -29.0


class Printed:
    """The case printed in PETG (make_print_case.py): a base, open at the top, and a lid. Same idea of axes, x across
    (+x the SW1 side), y up, z along the box, but here y = 0 is the base's inner floor and z = 0 the front wall's
    inner face. The board stands on four posts of the floor (M3 heat-set inserts in them) and is held by four M3
    standoffs (male-female, 25 mm) through its holes, on which the lid is screwed: no posts in the corners, where
    the board is. The sockets of the back plate sit behind the board, so nothing of them reaches over it: the XLR's
    solder cups end 4.7 mm behind the standoffs at the board's DMX edge."""
    INNER_W, INNER_L, INNER_H = 81.0, 122.0, 31.6     # inside the base; the lid's underside at INNER_H
    WALL, BACK_WALL, FLOOR_T, LID_T = 2.2, 3.0, 2.0, 2.4
    CORNER_R = 3.0                     # the outer vertical edges
    POST_H, POST_D, INSERT_D, INSERT_DEPTH = 5.0, 8.0, 4.0, 6.0   # M3 x 5.7 heat-set inserts (4.6 mm wide)
    STANDOFF = 25.0                    # from the board's top to the lid's underside
    EDGE_X = INNER_W / 2 - 0.4         # the board's SW1 edge, 0.4 mm from the side wall
    EDGE_Z = USB_OVERHANG + 0.3        # its USB edge: the sockets end 0.3 mm from the front wall
    BOARD_BOTTOM = POST_H
    BOARD_TOP = POST_H + BOARD_T
    # the back wall, seen from behind: MIDI IN, MIDI OUT, DMX OUT (x of their middles), all at one height
    SOCKET_Y = 14.8
    XLR_X, DIN_OUT_X, DIN_IN_X = 22.0, -8.0, -29.5


def board_layout():
    """W, H and PLACE of make_board.py, read without running it (it needs KiCad's pcbnew)."""
    tree = ast.parse(open(os.path.join(HERE, "make_board.py"), encoding="utf-8").read())
    found = {}
    for node in tree.body:
        if isinstance(node, ast.Assign):
            t = node.targets[0]
            if isinstance(t, ast.Name) and t.id == "PLACE":
                found["PLACE"] = ast.literal_eval(node.value)
            elif isinstance(t, ast.Tuple) and [e.id for e in t.elts] == ["W", "H"]:
                found["W"], found["H"] = ast.literal_eval(node.value)
    return found["W"], found["H"], found["PLACE"]


def to_case(xb, yb):
    """A point of the board (mm from its top left corner, as in make_board.py) in the case: (x, z)."""
    return EDGE_X - yb, EDGE_Z + xb
