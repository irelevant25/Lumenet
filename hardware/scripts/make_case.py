"""Makes the case of dmxbox 3: JLCMC's aluminium case K90-9848-H6-L120 (98 x 48 mm, 120 mm long, end plates of
1.5 mm) with every hole the box needs, as JLCMC wants it for their machining:

  ../case/dmxbox3_case.step       the case with the holes: the tube and the two end plates
  ../case/dmxbox3_case.pdf        the same as a drawing with every hole's size and place (same name as the STEP)
  ../case/dmxbox3_case_*.stl      the three parts for the pictures (make_3d.py)

Run with CadQuery (https://cadquery.readthedocs.io), for example through uv:
    uv run --no-project --python 3.13 --with cadquery --with matplotlib python make_case.py

The case model (../datasheets/K90-9848-H6-L120.stp, from JLCMC) has x across, y up, z along the tube (0 to 120 mm,
the front plate from -1.5 to 0, the back plate from 120 to 121.5). Inside, the tube is 93.2 mm wide (x -46.6 to
46.6) and 44 mm high (y -22.1 to 21.9); its side walls carry board slots and ribs up to y -8.5, and screw channels
for the end plates run along the four corners.

The board sits on four 15 mm spacers, its USB-C edge at the front plate and its SW1 edge 0.4 mm from the right
side wall (x +46.6), so the switch's keys are reached through a slot in that wall. Where the holes go is worked out
from make_board.py (its PLACE table), so they follow the board:
  front plate   the three USB-C sockets
  back plate    DMX OUT (5-pin XLR, XLR-xG-C) and MIDI IN and OUT (REAN NYS325), 4 mm above the middle so the
                XLR's solder cups pass above the board's DMX edge
  right side    a slot in front of SW1's keys
  top           three 3 mm light pipes above the LEDs
  bottom        four countersunk holes for the spacers' screws
"""
import ast
import os
import sys

import cadquery as cq

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)                    # case_drawing.py, also when Python runs isolated (-I)
HW = os.path.normpath(os.path.join(HERE, ".."))
CASE_IN = os.path.join(HW, "datasheets", "K90-9848-H6-L120.stp")
OUT_DIR = os.path.join(HW, "case")
NAME = "dmxbox3_case"

from case_layout import (BOARD_TOP, BOTTOM_OUTER, CEILING, DIN_IN_X, DIN_OUT_X, SOCKET_Y, SPACER, WALL_X,  # noqa: E402
                         XLR_X, board_layout, to_case)


def holes():
    """Every hole, by part of the case: (kind, parameters). Plates are in the case's x, y; the tube's walls by
    x, z (top, bottom) or y, z (side)."""
    W, H, place = board_layout()
    usb = [(ref, *to_case(*place[ref][:2])) for ref in ("J2", "J1", "J3")]
    sw_x, sw_y = place["SW1"][:2]
    keys = [to_case(sw_x - 8.89 + 2.54 * k, 0)[1] for k in range(8)]            # z of each key's middle
    leds = [(ref, *to_case(*place[ref][:2])) for ref in ("D4", "D5", "D6")]
    mounts = [to_case(*place[ref][:2]) for ref in ("H1", "H2", "H3", "H4")]
    usb_y = BOARD_TOP + 1.63                                                     # the sockets' middle height
    key_y = BOARD_TOP + 5.7                                                      # the keys' middle height
    return {
        "front": [("usb", x, usb_y, label) for label, (ref, x, z) in zip(("OUT", "IN", "POWER"), usb)],
        "back": [("xlr", XLR_X, SOCKET_Y, "DMX OUT"), ("din", DIN_OUT_X, SOCKET_Y, "MIDI OUT"),
                 ("din", DIN_IN_X, SOCKET_Y, "MIDI IN")],
        "side": [("slot", key_y, keys[0] - 1.27 - 0.6, keys[-1] + 1.27 + 0.6, keys)],
        "top": [("pipe", x, z, label) for label, (ref, x, z) in zip(("USB", "DMX", "MIDI"), leds)],
        "bottom": [("screw", x, z) for x, z in mounts],
        "board": {"W": W, "H": H},
    }


# Sizes of the openings
USB_W, USB_H, USB_R = 12.4, 6.5, 3.0          # big enough for most USB-C plugs' mouldings to go in
XLR_D, XLR_SCREWS = 24.0, [(-13.05, -8.25), (13.05, 8.25)]     # XLR-xG-C: body 23 mm, M3 at the flange's corners
DIN_D, DIN_SCREWS = 15.2, [(0, -11.1), (0, 11.1)]              # NYS325: body 15 mm, screws 22.2 mm apart
SCREW_D = 3.2
SLOT_H = 4.0
PIPE_D = 3.2                                   # 3 mm light pipes
CSK_D, CSK_HOLE = 6.5, 3.4                     # M3 countersunk screws from below (90 degrees)


def plate_cutters(items, z0, z1):
    cut = None
    for kind, x, y, *_ in items:
        if kind == "usb":
            s = (cq.Workplane("XY", origin=(0, 0, z0)).center(x, y).sketch()
                 .rect(USB_W, USB_H).vertices().fillet(USB_R).finalize().extrude(z1 - z0))
        elif kind == "xlr":
            s = cq.Workplane("XY", origin=(0, 0, z0)).center(x, y).circle(XLR_D / 2).extrude(z1 - z0)
            for dx, dy in XLR_SCREWS:
                s = s.union(cq.Workplane("XY", origin=(0, 0, z0)).center(x + dx, y + dy)
                            .circle(SCREW_D / 2).extrude(z1 - z0))
        else:
            s = cq.Workplane("XY", origin=(0, 0, z0)).center(x, y).circle(DIN_D / 2).extrude(z1 - z0)
            for dx, dy in DIN_SCREWS:
                s = s.union(cq.Workplane("XY", origin=(0, 0, z0)).center(x + dx, y + dy)
                            .circle(SCREW_D / 2).extrude(z1 - z0))
        cut = s if cut is None else cut.union(s)
    return cut


def tube_cutters(h):
    cuts = []
    # the slot in front of SW1's keys, through the right side wall
    _, y, z0, z1, _ = h["side"][0]
    cuts.append(cq.Workplane("YZ", origin=(WALL_X - 1.0, 0, 0)).center(y, (z0 + z1) / 2)
                .slot2D(z1 - z0, SLOT_H, 90).extrude(4.0))
    # the light pipes through the top
    for _, x, z, _ in h["top"]:
        cuts.append(cq.Workplane("XY").add(cq.Solid.makeCylinder(PIPE_D / 2, 4.0, cq.Vector(x, CEILING - 1, z),
                                                                 cq.Vector(0, 1, 0))))
    # countersunk holes in the bottom, the screw heads from below
    bottom = BOTTOM_OUTER
    for _, x, z in h["bottom"]:
        hole = cq.Solid.makeCylinder(CSK_HOLE / 2, 4.0, cq.Vector(x, bottom - 1, z), cq.Vector(0, 1, 0))
        sink = (CSK_D - CSK_HOLE) / 2
        cone = cq.Solid.makeCone(CSK_D / 2 + 0.1, CSK_HOLE / 2, sink + 0.1, cq.Vector(x, bottom - 0.1, z),
                                 cq.Vector(0, 1, 0))
        cuts.append(cq.Workplane("XY").add(hole.fuse(cone)))
    result = cuts[0]
    for c in cuts[1:]:
        result = result.union(c)
    return result


def parts():
    shape = cq.importers.importStep(CASE_IN)
    solids = shape.solids().vals()
    tube = next(s for s in solids if s.BoundingBox().zmin > -0.01 and s.BoundingBox().zmax > 119)
    front = next(s for s in solids if s.BoundingBox().zmax < 0.01)
    back = next(s for s in solids if s.BoundingBox().zmin > 119.99)
    h = holes()
    front = cq.Workplane("XY").add(front).cut(plate_cutters(h["front"], -3, 1)).val()
    back = cq.Workplane("XY").add(back).cut(plate_cutters(h["back"], 119, 123)).val()
    tube = cq.Workplane("XY").add(tube).cut(tube_cutters(h)).val()
    return tube, front, back, h


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    tube, front, back, h = parts()
    assy = cq.Assembly(name="dmxbox3 case")
    color = cq.Color(0.78, 0.78, 0.80)
    assy.add(tube, name="tube", color=color)
    assy.add(front, name="front plate", color=color)
    assy.add(back, name="back plate", color=color)
    step = os.path.join(OUT_DIR, NAME + ".step")
    assy.export(step)
    for name, solid in (("tube", tube), ("front", front), ("back", back)):
        cq.exporters.export(cq.Workplane("XY").add(solid), os.path.join(OUT_DIR, f"{NAME}_{name}.stl"),
                            tolerance=0.02, angularTolerance=0.1)
    import case_drawing
    case_drawing.write(os.path.join(OUT_DIR, NAME + ".pdf"), h, sizes=dict(
        usb=(USB_W, USB_H, USB_R), xlr=(XLR_D, XLR_SCREWS), din=(DIN_D, DIN_SCREWS), screw=SCREW_D, slot=SLOT_H,
        pipe=PIPE_D, csk=(CSK_D, CSK_HOLE), spacer=SPACER))
    print("wrote", step, "and the drawing and STL files in", OUT_DIR)


if __name__ == "__main__":
    main()
