"""Makes the 3D-printed case of dmxbox 3 (PETG): a base, open at the top, and a lid, ready for the slicer.

  ../case_print/dmxbox3_base.stl          the base, standing as it is printed (floor on the plate)
  ../case_print/dmxbox3_lid.stl           the lid, upside down as it is printed (its top on the plate)
  ../case_print/dmxbox3_base_text.stl     the labels of the base and of the lid as bodies of their own, in the same
  ../case_print/dmxbox3_lid_text.stl      place: load each with its part as one object for a second colour (AMS)
  ../case_print/dmxbox3_case_print.step   both parts in place, for changes in a CAD program
  work/case_print/*.stl                   both parts in place for the pictures (make_3d.py)

Run with CadQuery, for example through uv:
    uv run --no-project --python 3.13 --with cadquery python make_print_case.py

Where everything goes comes from case_layout.Printed and from make_board.py's PLACE, as for the aluminium case
(make_case.py): the USB-C sockets in the front wall, a slot in the side wall in front of SW1's keys, DMX OUT and
MIDI IN and OUT in the back wall behind the board, three light pipes in the lid over the LEDs, the board on four
posts with heat-set inserts. The labels are 0.6 mm deep in the surfaces.
"""
import os
import sys

import cadquery as cq

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from case_layout import Printed as P, board_layout   # noqa: E402

HW = os.path.normpath(os.path.join(HERE, ".."))
OUT_DIR = os.path.join(HW, "case_print")
WORK = os.path.join(HERE, "work", "case_print")

DEPTH = 0.6                                   # the labels
FONT = "Arial"
USB_W, USB_H, USB_R = 12.4, 6.5, 3.0          # openings for the USB-C plugs' mouldings
XLR_D, XLR_SCREWS = 24.3, [(-13.05, -8.25), (13.05, 8.25)]   # XLR-5G-C: body 23 mm, M3 at the flange's corners
DIN_D, DIN_SCREWS = 15.5, [(0, -11.1), (0, 11.1)]            # NYS325: body 15 mm, screws 22.2 mm apart
SCREW_D = 3.4                                 # printed holes come out a little small
SLOT_H = 4.0
PIPE_D = 3.2                                  # 3 mm light pipes

OW = P.INNER_W + 2 * P.WALL                   # outside of the base
Z0, Z1 = -P.WALL, P.INNER_L + P.BACK_WALL     # front and back faces
Y0, Y1 = -P.FLOOR_T, P.INNER_H                # bottom and top of the base
LID_TOP = P.INNER_H + P.LID_T


def to_case(xb, yb):
    """A point of the board (mm from its top left corner, as in make_board.py) in the case: (x, z)."""
    return P.EDGE_X - yb, P.EDGE_Z + xb


def plane(origin, x_dir, normal):
    return cq.Workplane(cq.Plane(cq.Vector(*origin), cq.Vector(*x_dir), cq.Vector(*normal)))


def label(text, size, origin, x_dir, normal, halign="center"):
    """A label as a solid DEPTH deep behind a face: its letters read from outside, x_dir to their right."""
    return plane(origin, x_dir, normal).text(text, size, -DEPTH, combine=False, font=FONT,
                                             kind="bold", halign=halign, valign="center").val()


def teardrop(x, y, r, cap, z0, depth):
    """A round hole through a wall across z that prints standing without support: its top rises at 45 degrees to a
    short flat at y + cap, low enough to stay under the socket's flange."""
    a = r / 2 ** 0.5
    roof = [(x - a, y + a), (x - (2 * a - cap), y + cap), (x + (2 * a - cap), y + cap), (x + a, y + a), (x, y)]
    wp = cq.Workplane("XY", origin=(0, 0, z0))
    return wp.center(x, y).circle(r).extrude(depth).union(
        cq.Workplane("XY", origin=(0, 0, z0)).polyline(roof).close().extrude(depth))


def rounded_box(x0, x1, y0, y1, z0, z1, r):
    """A box with its four edges along y rounded."""
    return (cq.Workplane("XY").box(x1 - x0, y1 - y0, z1 - z0).edges("|Y").fillet(r)
            .translate(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)))


def layout():
    _, _, place = board_layout()
    sw_x = place["SW1"][0]
    keys = [to_case(sw_x - 8.89 + 2.54 * k, 0)[1] for k in range(8)]
    return {
        "usb": [(label, *to_case(*place[ref][:2])) for label, ref in (("OUT", "J2"), ("IN", "J1"), ("POWER", "J3"))],
        "keys": keys,
        "leds": [(label, *to_case(*place[ref][:2])) for label, ref in (("USB", "D4"), ("DMX", "D5"), ("MIDI", "D6"))],
        "posts": [to_case(*place[ref][:2]) for ref in ("H1", "H2", "H3", "H4")],
    }


def base(lay):
    body = rounded_box(-OW / 2, OW / 2, Y0, Y1, Z0, Z1, P.CORNER_R)
    body = body.faces("<Y").edges().chamfer(0.6)
    body = body.cut(cq.Workplane("XY").box(P.INNER_W, P.INNER_H + 2, P.INNER_L, centered=(True, False, False)))
    # the board's posts, with the holes of the heat-set inserts
    for x, z in lay["posts"]:
        post = cq.Workplane("XZ", origin=(x, 0, z)).circle(P.POST_D / 2).extrude(-P.POST_H)
        body = body.union(post)
        body = body.cut(cq.Workplane("XZ", origin=(x, P.POST_H, z)).circle(P.INSERT_D / 2).extrude(P.INSERT_DEPTH))
    # front wall: the USB-C sockets
    usb_y = P.BOARD_TOP + 1.63
    for _, x, _ in lay["usb"]:
        body = body.cut(cq.Workplane("XY", origin=(0, 0, Z0 - 1)).center(x, usb_y).sketch()
                        .rect(USB_W, USB_H).vertices().fillet(USB_R).finalize().extrude(P.WALL + 2))
    # side wall: the slot in front of SW1's keys
    keys = lay["keys"]
    key_y = P.BOARD_TOP + 5.7
    z0, z1 = keys[0] - 1.27 - 0.6, keys[-1] + 1.27 + 0.6
    body = body.cut(cq.Workplane("YZ", origin=(P.INNER_W / 2 - 1, 0, 0)).center(key_y, (z0 + z1) / 2)
                    .slot2D(z1 - z0, SLOT_H, 90).extrude(P.WALL + 2))
    # back wall: DMX OUT and MIDI OUT and IN, with the holes of their screws
    for kind, x in (("xlr", P.XLR_X), ("din", P.DIN_OUT_X), ("din", P.DIN_IN_X)):
        d, screws = (XLR_D, XLR_SCREWS) if kind == "xlr" else (DIN_D, DIN_SCREWS)
        body = body.cut(teardrop(x, P.SOCKET_Y, d / 2, d / 2 + 1.0, P.INNER_L - 1, P.BACK_WALL + 2))
        for dx, dy in screws:
            body = body.cut(cq.Workplane("XY", origin=(0, 0, P.INNER_L - 1)).center(x + dx, P.SOCKET_Y + dy)
                            .circle(SCREW_D / 2).extrude(P.BACK_WALL + 2))
    # labels: above the USB-C sockets, and the value of each key above the slot
    texts = []
    for name, x, _ in lay["usb"]:
        texts.append(label(name, 3.6, (x, usb_y + 8.5, Z0), (-1, 0, 0), (0, 0, -1)))
    for k, z in enumerate(keys):            # reading upwards, as the keys are only 2.54 mm apart
        texts.append(label(str(2 ** k), 2.4, (OW / 2, key_y + SLOT_H / 2 + 1.2, z), (0, 1, 0), (1, 0, 0),
                           halign="left"))
    texts.append(label("BOX", 2.6, (OW / 2, key_y, z1 + 7.0), (0, 0, -1), (1, 0, 0)))
    for t in texts:
        body = body.cut(cq.Workplane("XY").add(t))
    return body.val(), texts


def lid(lay):
    clearance, lip_t, lip_h = 0.3, 1.2, 2.0
    body = rounded_box(-OW / 2, OW / 2, P.INNER_H, LID_TOP, Z0, Z1, P.CORNER_R)
    body = body.faces(">Y").edges().chamfer(0.6)
    # the lip that sits inside the base's walls
    outer = (P.INNER_W - 2 * clearance, P.INNER_L - 2 * clearance)
    inner = (outer[0] - 2 * lip_t, outer[1] - 2 * lip_t)
    lip = (cq.Workplane("XZ", origin=(0, P.INNER_H, P.INNER_L / 2)).rect(*outer).extrude(lip_h)
           .cut(cq.Workplane("XZ", origin=(0, P.INNER_H, P.INNER_L / 2)).rect(*inner).extrude(lip_h)))
    body = body.union(lip)
    # countersunk M3 screws into the standoffs
    for x, z in lay["posts"]:
        hole = cq.Solid.makeCylinder(1.7, P.LID_T + 2, cq.Vector(x, P.INNER_H - 1, z), cq.Vector(0, 1, 0))
        sink = cq.Solid.makeCone(1.7, 3.3, 1.6, cq.Vector(x, LID_TOP - 1.6, z), cq.Vector(0, 1, 0))
        top = cq.Solid.makeCylinder(3.3, 1.0, cq.Vector(x, LID_TOP - 0.001, z), cq.Vector(0, 1, 0))
        body = body.cut(cq.Workplane("XY").add(hole.fuse(sink).fuse(top)))
    # light pipes over the LEDs, their names beside them
    texts = []
    for name, x, z in lay["leds"]:
        body = body.cut(cq.Workplane("XY").add(cq.Solid.makeCylinder(PIPE_D / 2, P.LID_T + 2,
                                                                     cq.Vector(x, P.INNER_H - 1, z),
                                                                     cq.Vector(0, 1, 0))))
        texts.append(label(name, 3.0, (x + 3.0, LID_TOP, z), (-1, 0, 0), (0, 1, 0), halign="right"))
    # the name on top, and over the back wall what each socket is
    texts.append(label("dmxbox 3", 9.0, (-4.0, LID_TOP, 62.0), (-1, 0, 0), (0, 1, 0)))
    texts.append(label("Lumenet", 4.5, (-4.0, LID_TOP, 52.0), (-1, 0, 0), (0, 1, 0)))
    for name, x in (("DMX OUT", P.XLR_X), ("MIDI OUT", P.DIN_OUT_X), ("MIDI IN", P.DIN_IN_X)):
        texts.append(label(name, 3.2, (x, LID_TOP, P.INNER_L - 6.0), (-1, 0, 0), (0, 1, 0)))
    for t in texts:
        body = body.cut(cq.Workplane("XY").add(t))
    return body.val(), texts


def placed(shapes, upside_down):
    """What brings the shapes, together, from the case's axes (y up) to the printer's (z up), standing on z = 0:
    the base upright, the lid upside down."""
    angle = -90 if upside_down else 90
    turned = cq.Compound.makeCompound(shapes).rotate(cq.Vector(0, 0, 0), cq.Vector(1, 0, 0), angle)
    bb = turned.BoundingBox()
    shift = cq.Vector(-(bb.xmin + bb.xmax) / 2, -(bb.ymin + bb.ymax) / 2, -bb.zmin)
    return lambda shape: shape.rotate(cq.Vector(0, 0, 0), cq.Vector(1, 0, 0), angle).translate(shift)


def export_stl(shape, path):
    cq.exporters.export(cq.Workplane("XY").add(shape), path, tolerance=0.01, angularTolerance=0.1)


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    os.makedirs(WORK, exist_ok=True)
    lay = layout()
    b, b_texts = base(lay)
    l, l_texts = lid(lay)
    b_text = cq.Compound.makeCompound(b_texts)
    l_text = cq.Compound.makeCompound(l_texts)
    # for the printer: each part with its labels moved alike, so they stay together in the slicer
    for name, part, text, upside_down in (("base", b, b_text, False), ("lid", l, l_text, True)):
        move = placed([part, text], upside_down)
        export_stl(move(part), os.path.join(OUT_DIR, f"dmxbox3_{name}.stl"))
        export_stl(move(text), os.path.join(OUT_DIR, f"dmxbox3_{name}_text.stl"))
        export_stl(part, os.path.join(WORK, f"{name}.stl"))
    assy = cq.Assembly(name="dmxbox3 printed case")
    assy.add(b, name="base", color=cq.Color(0.05, 0.05, 0.05))
    assy.add(l, name="lid", color=cq.Color(0.05, 0.05, 0.05))
    assy.export(os.path.join(OUT_DIR, "dmxbox3_case_print.step"))
    bb_b, bb_l = b.BoundingBox(), l.BoundingBox()
    print(f"base {bb_b.xlen:.1f} x {bb_b.zlen:.1f} x {bb_b.ylen:.1f} mm, lid {bb_l.xlen:.1f} x {bb_l.zlen:.1f} x "
          f"{bb_l.ylen:.1f} mm; volume base {b.Volume() / 1000:.0f} cm3, lid {l.Volume() / 1000:.0f} cm3")
    print("wrote", OUT_DIR)


if __name__ == "__main__":
    main()
