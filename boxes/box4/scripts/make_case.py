"""Makes the case of dmxbox 4: JLCMC's aluminium case K119-12063-H7-L130 (120 x 63 mm, 130 mm long, split into a
top and a bottom half, end plates of 1.5 mm) with every hole the box needs, as JLCMC wants it for their machining:

  ../case/dmxbox4_case.step       the case with the holes: the two halves and the two end plates
  ../case/dmxbox4_case.pdf        the same as a drawing with every hole's size and place (same name as the STEP)
  ../case/dmxbox4_case_*.stl      the four parts for the pictures (make_3d.py)

Run with CadQuery (https://cadquery.readthedocs.io), for example through uv:
    uv run --no-project --python 3.13 --with cadquery --with matplotlib python make_case.py

The case model is boxes/datasheets/K119-12063-H7-L130.stp, from JLCMC; its inside is described in case_layout.py.
The board stands on four 15 mm spacers in the bottom half, its USB-C edge at the front plate and its SW1 edge 0.4 mm
from the right side wall (x +55.15), so the switch's keys are reached through a slot in that wall. Where the holes go
is worked out from make_board.py (its PLACE table), so they follow the board:
  front plate   the three USB-C sockets
  back plate    MIDI IN, MIDI OUT, DMX IN (male XLR) and DMX OUT (female XLR) in a row, behind the board
  bottom half   a slot in its right side in front of SW1's keys, and four countersunk holes for the spacers' screws;
                where a spacer would stand on the floor's ribs, a pocket from inside takes them away
  top half      three 3 mm light pipes above the LEDs
"""
import os
import sys

import cadquery as cq

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)                    # case_drawing.py, also when Python runs isolated (-I)
HW = os.path.normpath(os.path.join(HERE, ".."))
CASE_IN = os.path.normpath(os.path.join(HW, "..", "datasheets", "K119-12063-H7-L130.stp"))
OUT_DIR = os.path.join(HW, "case")
NAME = "dmxbox4_case"

from case_layout import (BOARD_TOP, BOTTOM_OUTER, CEILING, DIN_IN_X, DIN_OUT_X, FLOOR, RIB_H, RIBS,  # noqa: E402
                         SOCKET_Y, SPACER, TUBE_L, WALL_X, XLR_IN_X, XLR_X, board_layout, to_case)

SPACER_R = 3.5                      # a 5.5 mm hex spacer's corners, and some room


def on_ribs(x):
    """Whether a spacer standing at x would touch one of the floor's ribs."""
    return any(lo - SPACER_R < x < hi + SPACER_R for lo, hi in RIBS)


def holes():
    """Every hole, by part of the case: (kind, parameters). Plates are in the case's x, y; the halves by x, z (top,
    bottom) or y, z (side)."""
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
        "back": [("xlr", XLR_X, SOCKET_Y, "DMX OUT"), ("xlr_male", XLR_IN_X, SOCKET_Y, "DMX IN"),
                 ("din", DIN_OUT_X, SOCKET_Y, "MIDI OUT"), ("din", DIN_IN_X, SOCKET_Y, "MIDI IN")],
        "side": [("slot", key_y, keys[0] - 1.27 - 0.6, keys[-1] + 1.27 + 0.6, keys)],
        "top": [("pipe", x, z, label) for label, (ref, x, z) in zip(("USB", "DMX", "MIDI"), leds)],
        "bottom": [("screw", x, z) for x, z in mounts],
        "pockets": [(x, z) for x, z in mounts if on_ribs(x)],
        "board": {"W": W, "H": H},
    }


# Sizes of the openings
USB_W, USB_H, USB_R = 12.4, 6.5, 3.0          # big enough for most USB-C plugs' mouldings to go in
XLR_D = 24.0                                   # XLR-5G-C and XLR-5W-C: body 23 mm
XLR_SCREWS = [(8.25, -13.05), (-8.25, 13.05)]  # upright (latch at the top): M3 at two corners of the flange
DIN_D, DIN_SCREWS = 15.2, [(0, -11.1), (0, 11.1)]              # NYS325: body 15 mm, screws 22.2 mm apart
SCREW_D = 3.2
SLOT_H = 4.0
PIPE_D = 3.2                                   # 3 mm light pipes
CSK_D, CSK_HOLE = 6.5, 3.4                     # M3 countersunk screws from below (90 degrees)
POCKET_D = 10.0                                # the floor's ribs milled away under a spacer, down to the floor


def union(solids):
    result = solids[0]
    for s in solids[1:]:
        result = result.union(s)
    return result


def plate_cutters(items, z0, z1):
    cuts = []
    for kind, x, y, *_ in items:
        if kind == "usb":
            cuts.append(cq.Workplane("XY", origin=(0, 0, z0)).center(x, y).sketch()
                        .rect(USB_W, USB_H).vertices().fillet(USB_R).finalize().extrude(z1 - z0))
            continue
        d, screws = (XLR_D, XLR_SCREWS) if kind.startswith("xlr") else (DIN_D, DIN_SCREWS)
        cuts.append(cq.Workplane("XY", origin=(0, 0, z0)).center(x, y).circle(d / 2).extrude(z1 - z0))
        for dx, dy in screws:
            cuts.append(cq.Workplane("XY", origin=(0, 0, z0)).center(x + dx, y + dy)
                        .circle(SCREW_D / 2).extrude(z1 - z0))
    return union(cuts)


def bottom_cutters(h):
    cuts = []
    # the slot in front of SW1's keys, through the right side wall
    _, y, z0, z1, _ = h["side"][0]
    cuts.append(cq.Workplane("YZ", origin=(WALL_X - 1.0, 0, 0)).center(y, (z0 + z1) / 2)
                .slot2D(z1 - z0, SLOT_H, 90).extrude(8.0))
    # countersunk holes in the bottom, the screw heads from below
    for _, x, z in h["bottom"]:
        hole = cq.Solid.makeCylinder(CSK_HOLE / 2, 4.0, cq.Vector(x, BOTTOM_OUTER - 1, z), cq.Vector(0, 1, 0))
        sink = (CSK_D - CSK_HOLE) / 2
        cone = cq.Solid.makeCone(CSK_D / 2 + 0.1, CSK_HOLE / 2, sink + 0.1, cq.Vector(x, BOTTOM_OUTER - 0.1, z),
                                 cq.Vector(0, 1, 0))
        cuts.append(cq.Workplane("XY").add(hole.fuse(cone)))
    # the pockets: the ribs away where a spacer stands, the floor left as it is
    for x, z in h["pockets"]:
        cuts.append(cq.Workplane("XY").add(cq.Solid.makeCylinder(POCKET_D / 2, RIB_H + 1.0, cq.Vector(x, FLOOR, z),
                                                                 cq.Vector(0, 1, 0))))
    return union(cuts)


def top_cutters(h):
    # the light pipes through the top
    return union([cq.Workplane("XY").add(cq.Solid.makeCylinder(PIPE_D / 2, 4.0, cq.Vector(x, CEILING - 1, z),
                                                                cq.Vector(0, 1, 0)))
                  for _, x, z, _ in h["top"]])


def parts():
    shape = cq.importers.importStep(CASE_IN)
    solids = shape.solids().vals()
    halves = [s for s in solids if s.BoundingBox().zlen > TUBE_L - 1]
    top = next(s for s in halves if s.BoundingBox().ymax > CEILING)
    bottom = next(s for s in halves if s.BoundingBox().ymin < FLOOR)
    front = next(s for s in solids if s.BoundingBox().zmax < 0.01)
    back = next(s for s in solids if s.BoundingBox().zmin > TUBE_L - 0.01)
    h = holes()

    def cut(solid, cutter):
        return cq.Workplane("XY").add(solid).cut(cutter).val()

    made = {"top": cut(top, top_cutters(h)), "bottom": cut(bottom, bottom_cutters(h)),
            "front": cut(front, plate_cutters(h["front"], -3, 1)),
            "back": cut(back, plate_cutters(h["back"], TUBE_L - 1, TUBE_L + 3))}
    return made, h


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    made, h = parts()
    assy = cq.Assembly(name="dmxbox4 case")
    color = cq.Color(0.78, 0.78, 0.80)
    for name, label in (("top", "top half"), ("bottom", "bottom half"), ("front", "front plate"),
                        ("back", "back plate")):
        assy.add(made[name], name=label, color=color)
    step = os.path.join(OUT_DIR, NAME + ".step")
    assy.export(step)
    for name, solid in made.items():
        cq.exporters.export(cq.Workplane("XY").add(solid), os.path.join(OUT_DIR, f"{NAME}_{name}.stl"),
                            tolerance=0.02, angularTolerance=0.1)
    import case_drawing
    case_drawing.write(os.path.join(OUT_DIR, NAME + ".pdf"), h, sizes=dict(
        usb=(USB_W, USB_H, USB_R), xlr=(XLR_D, XLR_SCREWS), din=(DIN_D, DIN_SCREWS), screw=SCREW_D, slot=SLOT_H,
        pipe=PIPE_D, csk=(CSK_D, CSK_HOLE), spacer=SPACER, pocket=(POCKET_D, RIB_H)))
    print("wrote", step, "and the drawing and STL files in", OUT_DIR)


if __name__ == "__main__":
    main()
