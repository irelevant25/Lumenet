"""Builds the 3D model of dmxbox 3 in Blender and renders its pictures into ../3d/: the board, and the board in
its printed case and in the aluminium one, once make_print_case.py and make_case.py have made their files.

Run inside Blender (5.x), for example from Blender's text editor or with
    blender --background --python make_3d.py -- GLB
where GLB is the board exported by KiCad:
    kicad-cli pcb export glb --subst-models --include-tracks --include-pads --include-zones
                             --include-silkscreen --include-soldermask -o dmxbox3_board.glb dmxbox3.kicad_pcb

KiCad's library has 3D models for most parts. Added here by hand, as plain shapes (KiCad has no models of them):
the isolated DC-DC module with its label, and the box-number switch with its keys. The DMX and MIDI sockets sit in the box wall on cables, so the board has
only their pin headers. The USB-C sockets are KiCad's GCT USB4105, which has the same outline as the HRO
TYPE-C-31-M-12 on the board (set in make_board.py). Everything is in metres; the board's top left corner is at (0, 0).
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)                     # case_layout.py
OUT = os.path.normpath(os.path.join(HERE, "..", "3d"))
S = 0.001
TOP = 0.0016          # top of the 1.6 mm board
W, H = 0.086, 0.0745         # the board, metres
CENTER = (W / 2, -H / 2, 0.004)


def principled(m):
    return next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")


def material(name, color, metallic=0.0, rough=0.5):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    p = principled(m)
    p.inputs["Base Color"].default_value = (*color, 1)
    p.inputs["Metallic"].default_value = metallic
    p.inputs["Roughness"].default_value = rough
    return m


def collection(name):
    c = bpy.data.collections.get(name) or bpy.data.collections.new(name)
    if c.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(c)
    return c


def move_to(o, coll):
    for c in o.users_collection:
        c.objects.unlink(o)
    coll.objects.link(o)


def box(coll, name, x1, x2, y1, y2, z1, z2, m):
    bpy.ops.mesh.primitive_cube_add(size=1)
    o = bpy.context.active_object
    o.name = name
    o.scale = (x2 - x1, y2 - y1, z2 - z1)
    o.location = ((x1 + x2) / 2, (y1 + y2) / 2, (z1 + z2) / 2)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(m)
    move_to(o, coll)
    return o


def import_board(glb):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.import_scene.gltf(filepath=glb)
    # the board's own layers, by the names KiCad gives their meshes
    layers = {"copper": ((0.72, 0.45, 0.3), 1.0, 0.35, 1.0), "via": ((0.72, 0.45, 0.3), 1.0, 0.35, 1.0),
              "pad": ((0.78, 0.78, 0.76), 1.0, 0.3, 1.0),            # lead-free HASL
              "soldermask": ((0.015, 0.14, 0.05), 0.0, 0.32, 0.9)}
    done = set()
    for o in bpy.data.objects:
        if o.type != "MESH" or not o.data.name.startswith("dmxbox3_"):
            continue
        kind = o.data.name[len("dmxbox3_"):].split(".")[0]
        if kind in layers:
            color, metal, rough, alpha = layers[kind]
            for s in o.material_slots:
                p = principled(s.material)
                p.inputs["Base Color"].default_value = (*color, 1)
                p.inputs["Metallic"].default_value = metal
                p.inputs["Roughness"].default_value = rough
                p.inputs["Alpha"].default_value = alpha
                done.add(s.material.name)
    # the parts' materials arrive all metallic and fully rough: dark ones are plastic, light ones metal
    for m in bpy.data.materials:
        if m.name in done or not m.name.startswith("mat_") or not m.use_nodes:
            continue
        p = principled(m)
        r, g, b, _ = p.inputs["Base Color"].default_value
        if p.inputs["Roughness"].default_value >= 0.99:
            light = max(r, g, b) > 0.3 and not (g > 0.3 and r < 0.2)
            p.inputs["Metallic"].default_value = 1.0 if light else 0.0
            p.inputs["Roughness"].default_value = 0.3 if light else 0.5


def hand_made_parts():
    coll = collection("Hand-made parts")
    black = material("Black plastic", (0.015, 0.015, 0.015), 0.0, 0.45)
    white = material("White print", (0.9, 0.9, 0.9), 0.0, 0.6)

    # the isolated DC-DC module (U5), Mornsun's SMD package: body 13.2 x 8.5 x 7.25 mm, its middle at
    # (60.2, 53.0) mm, standing 0.3 mm above the board on its gull-wing leads, the print on its top
    tin = material("Tin", (0.8, 0.8, 0.8), 1.0, 0.3)
    cx, cy = 60.2, 53.0
    box(coll, "U5 DC-DC module", (cx - 6.6) * S, (cx + 6.6) * S, -(cy + 4.25) * S, -(cy - 4.25) * S,
        TOP + 0.3 * S, TOP + 7.25 * S, black)
    for n, (px, py) in {"1": (-3.81, 5.05), "2": (-1.27, 5.05), "4": (3.81, 5.05), "5": (3.81, -5.05),
                        "8": (-3.81, -5.05)}.items():
        side = 1 if py > 0 else -1
        box(coll, f"U5 lead {n}", (cx + px - 0.3) * S, (cx + px + 0.3) * S,
            -(cy + side * 4.25) * S, -(cy + side * 5.7) * S, TOP, TOP + 0.15 * S, tin)
    bpy.ops.object.text_add(location=(cx * S, -cy * S, TOP + 7.26 * S))
    t = bpy.context.active_object
    t.name = "U5 label"
    t.data.body = "B0505XT-1WR3"
    t.data.align_x, t.data.align_y = "CENTER", "CENTER"
    t.data.size = 1.6 * S
    t.data.materials.append(white)
    move_to(t, coll)

    # the box-number switch SW1 (kangshen DSPV08LSGET, SMD): body 21.72 x 9.00 mm from 0.85 mm to 7.0 mm above the
    # board, its middle at (32.01, 5.85) mm; its white keys reach 1.10 mm beyond the side towards the top edge (no
    # numbers on it: kangshen's and JLCPCB's drawings count its switches from opposite ends)
    key = material("White plastic", (0.85, 0.85, 0.83), 0.0, 0.4)
    sx, sy = 32.01, 5.85
    box(coll, "SW1 body", (sx - 10.86) * S, (sx + 10.86) * S, -(sy + 4.5) * S, -(sy - 4.5) * S,
        TOP + 0.85 * S, TOP + 7.0 * S, black)
    for k in range(8):
        kx = sx - 8.89 + 2.54 * k
        box(coll, f"SW1 key {k + 1}", (kx - 0.55) * S, (kx + 0.55) * S, -(sy - 3.4) * S, -(sy - 5.6) * S,
            TOP + 5.2 * S, TOP + 7.02 * S, key)
        for lo, hi in ((sy - 4.65, sy - 4.25), (sy + 4.25, sy + 4.65)):
            box(coll, f"SW1 lead {k + 1}", (kx - 0.3) * S, (kx + 0.3) * S, -hi * S, -lo * S, TOP, TOP + 0.9 * S, tin)


CASE_DIR = os.path.normpath(os.path.join(HERE, "..", "case"))


def case_matrix():
    """The case model's millimetres (x across, y up, z along the tube) in this scene's metres: the board's top left
    corner at the origin, its x along the tube, its y across, its underside at height 0."""
    import case_layout as cl
    return Matrix(((0, 0, S, -cl.EDGE_Z * S), (S, 0, 0, -cl.EDGE_X * S),
                   (0, S, 0, -(cl.FLOOR + cl.SPACER) * S), (0, 0, 0, 1)))


def cylinder(coll, name, x, y, z0, z1, r, m, axis="Z", vertices=48):
    """A cylinder in metres: along Z from z0 to z1 at (x, y), or along X from z0 to z1 at (y, z) = (x, y)."""
    if axis == "Z":
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=r, depth=z1 - z0,
                                            location=(x, y, (z0 + z1) / 2))
    else:
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=r, depth=z1 - z0,
                                            location=((z0 + z1) / 2, x, y), rotation=(0, math.pi / 2, 0))
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(m)
    move_to(o, coll)
    return o


def import_stl(coll, path, name, m, mat):
    """An STL file of millimetres placed by the matrix m, smooth where its faces meet at less than 35 degrees."""
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.stl_import(filepath=path, forward_axis="Y", up_axis="Z")
    o = bpy.context.selected_objects[0]
    o.name = name
    o.matrix_world = m
    o.data.materials.clear()
    o.data.materials.append(mat)
    bpy.context.view_layer.objects.active = o
    try:
        bpy.ops.object.shade_auto_smooth(angle=math.radians(35))
    except (RuntimeError, AttributeError, TypeError):
        pass
    move_to(o, coll)
    return o


def light_pipes(coll, place, top):
    """The three light pipes, from 0.6 mm above each LED up to the case's outside at height top, each with a head
    lit in its LED's colour. Returns them."""
    pipe = material("Light pipe", (0.92, 0.95, 0.97), 0.0, 0.08)
    try:
        principled(pipe).inputs["Transmission Weight"].default_value = 1.0
    except KeyError:
        pass
    made = []
    for ref, glow in (("D4", (0.1, 1.0, 0.2)), ("D5", (1.0, 0.75, 0.05)), ("D6", (1.0, 0.08, 0.05))):
        x, y = place[ref][0] * S, -place[ref][1] * S
        made.append(cylinder(coll, f"Light pipe {ref}", x, y, TOP + 1.6 * S, top, 1.5 * S, pipe))
        lit = material(f"Light pipe {ref} lit", glow, 0.0, 0.3)
        principled(lit).inputs["Emission Color"].default_value = (*glow, 1.0)
        principled(lit).inputs["Emission Strength"].default_value = 3.0
        made.append(cylinder(coll, f"Light pipe head {ref}", x, y, top, top + 0.8 * S, 2.1 * S, lit))
    return made


def back_sockets(coll, at, outer, socket_y, xs):
    """DMX OUT and MIDI OUT and IN on the back wall, its outside at x = outer in this scene: flanges outside,
    bodies inside (depths from their drawings). xs: the case x of the XLR and of the two DINs. Returns the bodies."""
    nickel = material("Nickel", (0.75, 0.75, 0.73), 1.0, 0.28)
    dark = material("Socket insert", (0.03, 0.03, 0.03), 0.0, 0.5)
    inside = []
    for kind, cx in zip(("xlr", "din", "din"), xs):
        _, sy, sz = at(cx, socket_y, 0)
        if kind == "xlr":
            box(coll, "XLR flange", outer, outer + 2.4 * S, sy - 18 * S, sy + 18 * S, sz - 13.2 * S, sz + 13.2 * S,
                nickel)
            box(coll, "XLR latch", outer + 2.4 * S, outer + 3.6 * S, sy + 10.5 * S, sy + 14.5 * S, sz - 4 * S,
                sz + 4 * S, dark)
            cylinder(coll, "XLR insert", sy, sz, outer + 2.4 * S, outer + 3.0 * S, 10 * S, dark, axis="X")
            for name, r, d0, d1 in (("XLR body", 11.5, 0, 10.9), ("XLR body 2", 9.35, 10.9, 19.5),
                                    ("XLR rear", 9.65, 22.3, 27.6), ("XLR cups", 6.0, 27.6, 34.0)):
                inside.append(cylinder(coll, name, sy, sz, outer - d1 * S, outer - d0 * S, r * S, nickel, axis="X"))
        else:
            cylinder(coll, "DIN flange", sy, sz, outer, outer + 1.0 * S, 9.5 * S, nickel, axis="X")
            box(coll, "DIN lugs", outer, outer + 1.0 * S, sy - 3.5 * S, sy + 3.5 * S, sz - 14.6 * S, sz + 14.6 * S,
                nickel)
            cylinder(coll, "DIN insert", sy, sz, outer + 1.0 * S, outer + 1.6 * S, 6.5 * S, dark, axis="X")
            inside.append(cylinder(coll, "DIN body", sy, sz, outer - 20.3 * S, outer, 7.5 * S, nickel, axis="X"))
    return inside


def case_parts():
    """The aluminium case (from make_case.py's STL files) around the board, the spacers, the light pipes and the
    sockets of the back plate as plain shapes. Returns the objects a cut-away picture cuts open."""
    import case_layout as cl
    coll = collection("Case")
    alu = material("Anodised aluminium", (0.63, 0.64, 0.66), 1.0, 0.5)
    m = case_matrix()
    cut = [import_stl(coll, os.path.join(CASE_DIR, f"dmxbox3_case_{part}.stl"), f"Case {part}", m, alu)
           for part in ("tube", "front", "back")]

    def at(x, y, z):
        """A point of the case (mm) in this scene (m)."""
        return tuple(m @ Vector((x, y, z)))

    w, h, place = cl.board_layout()
    nylon = material("Nylon", (0.88, 0.87, 0.82), 0.0, 0.55)
    for ref in ("H1", "H2", "H3", "H4"):
        x, y = place[ref][0] * S, -place[ref][1] * S
        cylinder(coll, f"Spacer {ref}", x, y, -cl.SPACER * S, 0, 3.175 * S, nylon, vertices=6)
        cylinder(coll, f"Screw {ref}", x, y, TOP, TOP + 2.0 * S, 2.75 * S, nylon)
    cut += light_pipes(coll, place, at(0, cl.TOP_OUTER, 0)[2])
    cut += back_sockets(coll, at, at(0, 0, 121.5)[0], cl.SOCKET_Y, (cl.XLR_X, cl.DIN_OUT_X, cl.DIN_IN_X))
    return cut


CASE_VIEWS = {  # file name -> (camera location, target)
    "dmxbox3_case_front": ((-0.118, 0.118, 0.100), (0.052, -0.044, 0.004)),
    "dmxbox3_case_back": ((0.238, -0.208, 0.100), (0.066, -0.050, 0.004)),
    "dmxbox3_case_open": ((-0.040, 0.100, 0.175), (0.055, -0.046, -0.002)),
}


def render_case(cam):
    """The board in its case: closed from the front and from the back, and cut open above the board."""
    import case_layout as cl
    cut = case_parts()
    floor = bpy.data.objects.get("Floor")
    if floor:
        floor.location.z = (-24.2 - cl.FLOOR - cl.SPACER) * S - 0.0001
        floor.scale = (2.0, 2.0, 1.0)
    cam.data.type = "PERSP"
    for name, (loc, target) in CASE_VIEWS.items():
        if name.endswith("_open"):
            bpy.ops.mesh.primitive_cube_add(size=1)
            knife = bpy.context.active_object
            knife.name = "Cut-away"
            knife.scale = (0.3, 0.3, 0.1)
            knife.location = (0.05, -0.05, 0.05 + 0.0205)    # everything above 20.5 mm over the board's underside
            knife.hide_render = True
            knife.display_type = "WIRE"
            for o in cut:
                mod = o.modifiers.new("Cut-away", "BOOLEAN")
                mod.operation, mod.object, mod.solver = "DIFFERENCE", knife, "EXACT"
        cam.location = loc
        cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        bpy.context.scene.render.filepath = os.path.join(OUT, name + ".png")
        bpy.ops.render.render(write_still=True)
        print("rendered", name)


def stage():
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        for kind in ("OPTIX", "CUDA", "HIP", "METAL", "ONEAPI"):
            try:
                prefs.compute_device_type = kind
                prefs.get_devices()
                if any(d.type == kind for d in prefs.devices):
                    for d in prefs.devices:
                        d.use = d.type == kind
                    sc.cycles.device = "GPU"
                    break
            except TypeError:
                continue
    except KeyError:
        pass
    sc.cycles.samples = 128
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 1920, 1280
    sc.view_settings.view_transform = "AgX"

    w = sc.world or bpy.data.worlds.new("World")
    sc.world = w
    w.use_nodes = True
    nt = w.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out, bg = nt.nodes.new("ShaderNodeOutputWorld"), nt.nodes.new("ShaderNodeBackground")
    coord, mapping = nt.nodes.new("ShaderNodeTexCoord"), nt.nodes.new("ShaderNodeMapping")
    grad, ramp = nt.nodes.new("ShaderNodeTexGradient"), nt.nodes.new("ShaderNodeValToRGB")
    mapping.inputs["Rotation"].default_value = (0, -math.pi / 2, 0)
    nt.links.new(coord.outputs["Generated"], mapping.inputs["Vector"])
    nt.links.new(mapping.outputs["Vector"], grad.inputs["Vector"])
    nt.links.new(grad.outputs["Fac"], ramp.inputs["Fac"])
    ramp.color_ramp.elements[0].color = (0.55, 0.57, 0.62, 1)
    ramp.color_ramp.elements[1].color = (0.95, 0.96, 0.98, 1)
    nt.links.new(ramp.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 0.45
    nt.links.new(bg.outputs["Background"], out.inputs["Surface"])

    bpy.ops.mesh.primitive_plane_add(size=2, location=(W / 2, -H / 2, -0.0026))
    floor = bpy.context.active_object
    floor.name = "Floor"
    floor.data.materials.append(material("Floor", (0.78, 0.79, 0.81), 0.0, 0.6))

    for name, loc, size, power in (("Key", (0.20, -0.22, 0.30), 0.25, 5.0), ("Fill", (-0.15, -0.05, 0.20), 0.30, 1.6),
                                   ("Rim", (0.10, 0.20, 0.15), 0.20, 2.0)):
        ld = bpy.data.lights.new(name, "AREA")
        ld.size, ld.energy = size, power
        lo = bpy.data.objects.new(name, ld)
        sc.collection.objects.link(lo)
        lo.location = loc
        lo.rotation_euler = (Vector(CENTER) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()

    cd = bpy.data.cameras.new("Camera")
    cd.lens, cd.clip_start = 50, 0.001
    cam = bpy.data.objects.new("Camera", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    return cam


VIEWS = {  # file name -> (camera location, target, orthographic scale or None)
    "dmxbox3_angle": ((0.14, -0.19, 0.105), CENTER, None),
    "dmxbox3_top": ((W / 2, -H / 2, 0.3), (W / 2, -H / 2, 0.0), 0.113),
    "dmxbox3_usb_side": ((-0.15, -0.05, 0.06), (0.03, -0.035, 0.004), None),
    "dmxbox3_dmx_side": ((0.24, -0.01, 0.07), (0.06, -0.035, 0.006), None),
}


def render(cam):
    os.makedirs(OUT, exist_ok=True)
    for name, (loc, target, ortho) in VIEWS.items():
        cam.data.type = "ORTHO" if ortho else "PERSP"
        if ortho:
            cam.data.ortho_scale = ortho
        cam.location = loc
        cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        bpy.context.scene.render.filepath = os.path.join(OUT, name + ".png")
        bpy.ops.render.render(write_still=True)
        print("rendered", name)


PRINT_WORK = os.path.join(HERE, "work", "case_print")
PRINT_VIEWS = {  # file name -> (camera location, target)
    "dmxbox3_print_front": ((-0.118, 0.118, 0.092), (0.052, -0.040, 0.008)),
    "dmxbox3_print_back": ((0.238, -0.200, 0.092), (0.066, -0.045, 0.008)),
    "dmxbox3_print_open": ((-0.040, 0.100, 0.165), (0.055, -0.042, 0.000)),
}


def print_matrix():
    """The printed case's millimetres (case_layout.Printed: y = 0 its inner floor, z = 0 its front wall's inner
    face) in this scene's metres."""
    from case_layout import Printed as P
    return Matrix(((0, 0, S, -P.EDGE_Z * S), (S, 0, 0, -P.EDGE_X * S), (0, S, 0, -P.BOARD_BOTTOM * S),
                   (0, 0, 0, 1)))


def print_case_parts():
    """The printed case (make_print_case.py's parts in work/case_print) around the board, in black PETG, with the
    brass standoffs, the light pipes and the sockets of the back wall. Returns the lid."""
    import case_layout as cl
    from case_layout import Printed as P
    coll = collection("Printed case")
    petg = material("Black PETG", (0.006, 0.006, 0.007), 0.0, 0.72)
    m = print_matrix()
    import_stl(coll, os.path.join(PRINT_WORK, "base.stl"), "Printed base", m, petg)
    lid = import_stl(coll, os.path.join(PRINT_WORK, "lid.stl"), "Printed lid", m, petg)

    def at(x, y, z):
        return tuple(m @ Vector((x, y, z)))

    _, _, place = cl.board_layout()
    brass = material("Brass", (0.78, 0.62, 0.32), 1.0, 0.35)
    lid_under = at(0, P.INNER_H, 0)[2]
    for ref in ("H1", "H2", "H3", "H4"):
        x, y = place[ref][0] * S, -place[ref][1] * S
        cylinder(coll, f"Standoff {ref}", x, y, TOP, lid_under, 3.175 * S, brass, vertices=6)
    light_pipes(coll, place, at(0, P.INNER_H + P.LID_T, 0)[2])
    back_sockets(coll, at, at(0, 0, P.INNER_L + P.BACK_WALL)[0], P.SOCKET_Y, (P.XLR_X, P.DIN_OUT_X, P.DIN_IN_X))
    return lid


def render_print_case(cam):
    """The board in the printed case: closed from the front and from the back, and with its lid off."""
    from case_layout import Printed as P
    old = bpy.data.collections.get("Case")
    if old:                                        # the aluminium case of the pictures before
        for o in list(old.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(old)
    knife = bpy.data.objects.get("Cut-away")
    if knife:
        bpy.data.objects.remove(knife, do_unlink=True)
    lid = print_case_parts()
    floor = bpy.data.objects.get("Floor")
    if floor:
        floor.location.z = (-P.FLOOR_T - P.BOARD_BOTTOM) * S - 0.0001
        floor.scale = (2.0, 2.0, 1.0)
    cam.data.type = "PERSP"
    for name, (loc, target) in PRINT_VIEWS.items():
        lid.hide_render = name.endswith("_open")
        cam.location = loc
        cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        bpy.context.scene.render.filepath = os.path.join(OUT, name + ".png")
        bpy.ops.render.render(write_still=True)
        print("rendered", name)


def main(glb, save=True):
    import_board(glb)
    hand_made_parts()
    cam = stage()
    render(cam)
    if os.path.exists(os.path.join(CASE_DIR, "dmxbox3_case_tube.stl")):
        render_case(cam)
    if os.path.exists(os.path.join(PRINT_WORK, "base.stl")):
        render_print_case(cam)
    if save:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "dmxbox3.blend"), compress=True)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if args:
        main(args[0])
