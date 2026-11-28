"""Builds the 3D model of dmxbox 4 in Blender and renders its pictures into ../3d/: the board, and the board in
its printed case and in the aluminium one, once make_print_case.py and make_case.py have made their files. What all
boxes share is boxes/scripts/render3d.py.

Run inside Blender (5.x), for example from Blender's text editor or with
    blender --background --python make_3d.py -- GLB
where GLB is the board exported by KiCad:
    kicad-cli pcb export glb --subst-models --include-tracks --include-pads --include-zones
                             --include-silkscreen --include-soldermask -o dmxbox4_board.glb dmxbox4.kicad_pcb

KiCad's library has 3D models for most parts. Added here by hand, as plain shapes (KiCad has no models of them):
the isolated DC-DC module with its label, and the box-number switch with its keys. The DMX and MIDI sockets sit in
the box wall on cables, so the board has only their pin headers. The USB-C sockets are KiCad's GCT USB4105, which
has the same outline as the HRO TYPE-C-31-M-12 on the board (set in make_board.py).
"""
import os
import sys

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)                     # box.py, case_layout.py
import box  # noqa: E402,F401  (boxes/scripts on the path)
import render3d as r  # noqa: E402
from placement import board_layout  # noqa: E402
from render3d import S, TOP, collection, cylinder, import_stl, material  # noqa: E402

W_MM, H_MM, PLACE = board_layout()
r.use(W_MM, H_MM)
W, H, CENTER = r.W, r.H, r.CENTER

VIEWS = {  # file name -> (camera location, target, orthographic scale or None)
    "dmxbox4_angle": ((0.14, -0.19, 0.105), CENTER, None),
    "dmxbox4_top": ((W / 2, -H / 2, 0.3), (W / 2, -H / 2, 0.0), 0.113),
    "dmxbox4_usb_side": ((-0.15, -0.05, 0.06), (0.03, -0.035, 0.004), None),
    "dmxbox4_dmx_side": ((0.24, -0.01, 0.07), (0.06, -0.035, 0.006), None),
}
PRINT_VIEWS = {  # file name -> (camera location, target)
    "dmxbox4_print_front": ((-0.143, 0.128, 0.156), (0.052, -0.053, 0.002)),
    "dmxbox4_print_back": ((0.238, -0.200, 0.092), (0.066, -0.045, 0.008)),
    "dmxbox4_print_open": ((-0.040, 0.100, 0.165), (0.055, -0.042, 0.000)),
}


def hand_made_parts():
    coll = collection("Hand-made parts")
    r.dcdc_module(coll, "U5", *PLACE["U5"][:2])
    r.piano_switch(coll, "SW1", *PLACE["SW1"][:2])


# ---------------------------------------------------------------- the aluminium case (make_case.py's parts)

CASE_DIR = os.path.normpath(os.path.join(HERE, "..", "case"))
CASE_VIEWS = {  # file name -> (camera location, target); "_open" is shot without the top half
    "dmxbox4_case_front": ((-0.140, 0.139, 0.127), (0.064, -0.055, 0.012)),
    "dmxbox4_case_back": ((0.281, -0.250, 0.127), (0.075, -0.060, 0.012)),
    "dmxbox4_case_open": ((-0.045, 0.113, 0.204), (0.064, -0.055, 0.000)),
}


def case_matrix():
    """The case model's millimetres (x across, y up, z along the tube) in this scene's metres: the board's top left
    corner at the origin, its x along the tube, its y across, its underside at height 0."""
    import case_layout as cl
    return Matrix(((0, 0, S, -cl.EDGE_Z * S), (S, 0, 0, -cl.EDGE_X * S),
                   (0, S, 0, -(cl.FLOOR + cl.SPACER) * S), (0, 0, 0, 1)))


def case_parts():
    """The aluminium case (from make_case.py's STL files) around the board, the spacers, the light pipes and the
    sockets of the back plate as plain shapes. Returns what is lifted off with the top half."""
    import case_layout as cl
    coll = collection("Case")
    alu = material("Anodised aluminium", (0.63, 0.64, 0.66), 1.0, 0.5)
    m = case_matrix()
    lid = []
    for part in ("top", "bottom", "front", "back"):
        o = import_stl(coll, os.path.join(CASE_DIR, f"dmxbox4_case_{part}.stl"), f"Case {part}", m, alu)
        if part == "top":
            lid.append(o)

    def at(x, y, z):
        """A point of the case (mm) in this scene (m)."""
        return tuple(m @ Vector((x, y, z)))

    nylon = material("Nylon", (0.88, 0.87, 0.82), 0.0, 0.55)
    for ref in ("H1", "H2", "H3", "H4"):
        x, y = PLACE[ref][0] * S, -PLACE[ref][1] * S
        cylinder(coll, f"Spacer {ref}", x, y, -cl.SPACER * S, 0, 3.175 * S, nylon, vertices=6)
        cylinder(coll, f"Screw {ref}", x, y, TOP, TOP + 2.0 * S, 2.75 * S, nylon)
    lid += r.light_pipes(coll, PLACE, at(0, cl.TOP_OUTER, 0)[2])
    r.back_sockets(coll, at, at(0, 0, cl.TUBE_L + cl.PLATE_T)[0], cl.SOCKET_Y,
                   [("xlr", cl.XLR_X), ("xlr_male", cl.XLR_IN_X), ("din", cl.DIN_OUT_X), ("din", cl.DIN_IN_X)],
                   upright=True)
    return lid


def render_case(cam):
    """The board in its case: closed from the front and from the back, and open (the top half lifted off)."""
    import case_layout as cl
    lid = case_parts()
    floor = bpy.data.objects.get("Floor")
    if floor:
        floor.location.z = (cl.BOTTOM_OUTER - cl.FLOOR - cl.SPACER) * S - 0.0001
        floor.scale = (2.0, 2.0, 1.0)
    for name, (loc, target) in CASE_VIEWS.items():
        for o in lid:
            o.hide_render = name.endswith("_open")
        r.shoot(cam, name, loc, target)
    for o in lid:
        o.hide_render = False


def main(glb, save=True):
    import case_layout as cl
    r.import_board(glb)
    hand_made_parts()
    cam = r.stage()
    r.render(cam, VIEWS)
    if os.path.exists(os.path.join(CASE_DIR, "dmxbox4_case_bottom.stl")):
        render_case(cam)
    if os.path.exists(os.path.join(r.PRINT_WORK, "base.stl")):
        P = cl.Printed
        r.render_print_case(cam, P, PLACE, [("xlr", P.XLR_X), ("xlr_male", P.XLR_IN_X), ("din", P.DIN_OUT_X),
                                            ("din", P.DIN_IN_X)], PRINT_VIEWS)
    if save:
        r.save()


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if args:
        main(args[0])
