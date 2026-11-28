"""Builds the 3D model of dmxbox 1 in Blender and renders its pictures into ../3d/: the board, and the board in its
printed case once make_print_case.py has made it. What all boxes share is boxes/scripts/render3d.py.

Run inside Blender (5.x), for example from Blender's text editor or with
    blender --background --python make_3d.py -- GLB
where GLB is the board exported by KiCad:
    kicad-cli pcb export glb --subst-models --include-tracks --include-pads --include-zones
                             --include-silkscreen --include-soldermask -o dmxbox1_board.glb dmxbox1.kicad_pcb

KiCad's models: the Pico on its socket strips (make_board.py chooses them), the switch, the sockets of the chips
(the chips are not drawn) and the rest. Made by hand: the DC-DC module.
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
    "dmxbox1_angle": ((0.16, -0.18, 0.115), CENTER, None),
    "dmxbox1_top": ((W / 2, -H / 2, 0.3), (W / 2, -H / 2, 0.0), 0.128),
    "dmxbox1_usb_side": ((-0.15, -0.06, 0.07), (0.03, -0.035, 0.006), None),
    "dmxbox1_dmx_side": ((0.27, -0.006, 0.08), (0.07, -0.031, 0.006), None),
}
PRINT_VIEWS = {  # file name -> (camera location, target)
    "dmxbox1_print_front": ((-0.132, 0.118, 0.142), (0.056, -0.033, 0.003)),
    "dmxbox1_print_back": ((0.26, -0.20, 0.095), (0.076, -0.040, 0.008)),
    "dmxbox1_print_open": ((-0.045, 0.100, 0.175), (0.062, -0.036, 0.000)),
}


def main(glb, save=True):
    import case_layout as cl
    P = cl.Printed
    r.import_board(glb)
    r.rkk_module(collection("Hand-made parts"), "U5", *PLACE["U5"][:2])
    cam = r.stage()
    r.render(cam, VIEWS)
    if os.path.exists(os.path.join(r.PRINT_WORK, "base.stl")):
        r.render_print_case(cam, P, PLACE, [("xlr", P.XLR_X)], PRINT_VIEWS, leds=cl.LEDS,
                            pipe_start=cl.LED_TOP + 0.4)
    if save:
        r.save()


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if args:
        main(args[0])
