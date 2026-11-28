"""Builds the 3D model of dmxbox 3 in Blender and renders its pictures into ../3d/: the board, and the board in its
printed case once make_print_case.py has made it. What all boxes share is boxes/scripts/render3d.py.

Run inside Blender (5.x), for example from Blender's text editor or with
    blender --background --python make_3d.py -- GLB
where GLB is the board exported by KiCad:
    kicad-cli pcb export glb --subst-models --include-tracks --include-pads --include-zones
                             --include-silkscreen --include-soldermask -o dmxbox3_board.glb dmxbox3.kicad_pcb

Made by hand, as plain shapes (KiCad has no models of them): the isolated DC-DC module and the box-number switch.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)                     # box.py, case_layout.py
import box  # noqa: E402,F401  (boxes/scripts on the path)
import render3d as r  # noqa: E402
from placement import board_layout  # noqa: E402
from render3d import collection  # noqa: E402

W_MM, H_MM, PLACE = board_layout()
r.use(W_MM, H_MM)
W, H, CENTER = r.W, r.H, r.CENTER

VIEWS = {  # file name -> (camera location, target, orthographic scale or None)
    "dmxbox3_angle": ((0.14, -0.18, 0.105), CENTER, None),
    "dmxbox3_top": ((W / 2, -H / 2, 0.3), (W / 2, -H / 2, 0.0), 0.113),
    "dmxbox3_usb_side": ((-0.15, -0.045, 0.06), (0.03, -0.031, 0.004), None),
    "dmxbox3_dmx_side": ((0.24, -0.006, 0.07), (0.06, -0.031, 0.006), None),
}
PRINT_VIEWS = {  # file name -> (camera location, target)
    "dmxbox3_print_front": ((-0.118, 0.113, 0.092), (0.052, -0.035, 0.008)),
    "dmxbox3_print_back": ((0.238, -0.195, 0.092), (0.066, -0.040, 0.008)),
    "dmxbox3_print_open": ((-0.040, 0.095, 0.165), (0.055, -0.037, 0.000)),
}


def main(glb, save=True):
    from case_layout import Printed as P
    r.import_board(glb)
    coll = collection("Hand-made parts")
    r.dcdc_module(coll, "U5", *PLACE["U5"][:2])
    r.piano_switch(coll, "SW1", *PLACE["SW1"][:2])
    cam = r.stage()
    r.render(cam, VIEWS)
    if os.path.exists(os.path.join(r.PRINT_WORK, "base.stl")):
        r.render_print_case(cam, P, PLACE, [("xlr", P.XLR_X)], PRINT_VIEWS)
    if save:
        r.save()


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if args:
        main(args[0])
