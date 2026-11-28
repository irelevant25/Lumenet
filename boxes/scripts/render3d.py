"""What the boxes' make_3d.py share, in Blender (5.x): the board from KiCad's GLB, the parts KiCad has no model
of (the DC-DC module, the piano switch), the light pipes, the sockets of the back wall, the printed case, the
studio (light, floor, camera) and the renders. Everything is in metres; the board's top left corner is at (0, 0),
x to the right, y up the page (so a part at y mm on the board is at -y here), the board's top at TOP.

A box's make_3d.py calls use(W, H) first, with its board's size in millimetres.
"""
import math
import os

import bpy
from mathutils import Matrix, Vector

import box as this_box

S = 0.001
TOP = 0.0016          # top of the 1.6 mm board
OUT = os.path.join(this_box.OUT, "3d")
W = H = 0.0
CENTER = (0.0, 0.0, 0.0)


def use(w_mm, h_mm):
    """The board's size in millimetres."""
    global W, H, CENTER
    W, H = w_mm * S, h_mm * S
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


def import_board(glb):
    """The scene emptied, then the board as KiCad exported it (with its parts' models), in kinder materials."""
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.import_scene.gltf(filepath=glb)
    # the board's own layers, by the names KiCad gives their meshes
    layers = {"copper": ((0.72, 0.45, 0.3), 1.0, 0.35, 1.0), "via": ((0.72, 0.45, 0.3), 1.0, 0.35, 1.0),
              "pad": ((0.78, 0.78, 0.76), 1.0, 0.3, 1.0),            # lead-free HASL
              "soldermask": ((0.015, 0.14, 0.05), 0.0, 0.32, 0.9)}
    prefix = this_box.NAME + "_"
    done = set()
    for o in bpy.data.objects:
        if o.type != "MESH" or not o.data.name.startswith(prefix):
            continue
        kind = o.data.name[len(prefix):].split(".")[0]
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


def dcdc_module(coll, ref, cx, cy):
    """The isolated DC-DC module, Mornsun's SMD package: body 13.2 x 8.5 x 7.25 mm, its middle at (cx, cy) mm,
    standing 0.3 mm above the board on its gull-wing leads, the print on its top."""
    black = material("Black plastic", (0.015, 0.015, 0.015), 0.0, 0.45)
    white = material("White print", (0.9, 0.9, 0.9), 0.0, 0.6)
    tin = material("Tin", (0.8, 0.8, 0.8), 1.0, 0.3)
    box(coll, f"{ref} DC-DC module", (cx - 6.6) * S, (cx + 6.6) * S, -(cy + 4.25) * S, -(cy - 4.25) * S,
        TOP + 0.3 * S, TOP + 7.25 * S, black)
    for n, (px, py) in {"1": (-3.81, 5.05), "2": (-1.27, 5.05), "4": (3.81, 5.05), "5": (3.81, -5.05),
                        "8": (-3.81, -5.05)}.items():
        side = 1 if py > 0 else -1
        box(coll, f"{ref} lead {n}", (cx + px - 0.3) * S, (cx + px + 0.3) * S,
            -(cy + side * 4.25) * S, -(cy + side * 5.7) * S, TOP, TOP + 0.15 * S, tin)
    bpy.ops.object.text_add(location=(cx * S, -cy * S, TOP + 7.26 * S))
    t = bpy.context.active_object
    t.name = f"{ref} label"
    t.data.body = "B0505XT-1WR3"
    t.data.align_x, t.data.align_y = "CENTER", "CENTER"
    t.data.size = 1.6 * S
    t.data.materials.append(white)
    move_to(t, coll)


def rkk_module(coll, ref, x0, y0):
    """The isolated DC-DC module Recom RKK-0505S/H (SIP7) by its pin 1 at (x0, y0) mm: body 19.6 x 6.0 x 10.2 mm,
    0.52 mm above the board, pin 1 2.22 mm from its end, the pins 1.24 mm from its side towards -y."""
    black = material("Black plastic", (0.015, 0.015, 0.015), 0.0, 0.45)
    white = material("White print", (0.9, 0.9, 0.9), 0.0, 0.6)
    box(coll, f"{ref} DC-DC module", (x0 - 2.22) * S, (x0 + 17.38) * S, -(y0 + 4.76) * S, -(y0 - 1.24) * S,
        TOP + 0.52 * S, TOP + 10.72 * S, black)
    bpy.ops.object.text_add(location=((x0 + 7.58) * S, -(y0 + 1.76) * S, TOP + 10.73 * S))
    t = bpy.context.active_object
    t.name = f"{ref} label"
    t.data.body = "RKK-0505S/H"
    t.data.align_x, t.data.align_y = "CENTER", "CENTER"
    t.data.size = 2.0 * S
    t.data.materials.append(white)
    move_to(t, coll)


def piano_switch(coll, ref, sx, sy):
    """The box-number switch (kangshen DSPV08LSGET, SMD): body 21.72 x 9.00 mm from 0.85 mm to 7.0 mm above the
    board, its middle at (sx, sy) mm; its white keys reach 1.10 mm beyond the side towards the top edge (no
    numbers on it: kangshen's and JLCPCB's drawings count its switches from opposite ends)."""
    black = material("Black plastic", (0.015, 0.015, 0.015), 0.0, 0.45)
    tin = material("Tin", (0.8, 0.8, 0.8), 1.0, 0.3)
    key = material("White plastic", (0.85, 0.85, 0.83), 0.0, 0.4)
    box(coll, f"{ref} body", (sx - 10.86) * S, (sx + 10.86) * S, -(sy + 4.5) * S, -(sy - 4.5) * S,
        TOP + 0.85 * S, TOP + 7.0 * S, black)
    for k in range(8):
        kx = sx - 8.89 + 2.54 * k
        box(coll, f"{ref} key {k + 1}", (kx - 0.55) * S, (kx + 0.55) * S, -(sy - 3.4) * S, -(sy - 5.6) * S,
            TOP + 5.2 * S, TOP + 7.02 * S, key)
        for lo, hi in ((sy - 4.65, sy - 4.25), (sy + 4.25, sy + 4.65)):
            box(coll, f"{ref} lead {k + 1}", (kx - 0.3) * S, (kx + 0.3) * S, -hi * S, -lo * S, TOP, TOP + 0.9 * S,
                tin)


def leds_at(place, leds=None):
    """Where the three LEDs' middles are on the board, mm: LEDS {ref: (x, y)} where a footprint's place is not its
    middle (a through-hole LED's place is its first leg), else the places."""
    return {ref: (leds or {}).get(ref, place[ref][:2]) for ref in ("D4", "D5", "D6")}


def light_pipes(coll, place, top, start=1.6, leds=None):
    """The three light pipes, from START mm above the board (just above each LED) up to the case's outside at
    height top, each with a head lit in its LED's color. Returns them."""
    pipe = material("Light pipe", (0.92, 0.95, 0.97), 0.0, 0.08)
    try:
        principled(pipe).inputs["Transmission Weight"].default_value = 1.0
    except KeyError:
        pass
    made = []
    at = leds_at(place, leds)
    for ref, glow in (("D4", (0.1, 1.0, 0.2)), ("D5", (1.0, 0.75, 0.05)), ("D6", (1.0, 0.08, 0.05))):
        x, y = at[ref][0] * S, -at[ref][1] * S
        made.append(cylinder(coll, f"Light pipe {ref}", x, y, TOP + start * S, top, 1.5 * S, pipe))
        lit = material(f"Light pipe {ref} lit", glow, 0.0, 0.3)
        principled(lit).inputs["Emission Color"].default_value = (*glow, 1.0)
        principled(lit).inputs["Emission Strength"].default_value = 3.0
        made.append(cylinder(coll, f"Light pipe head {ref}", x, y, top, top + 0.8 * S, 2.1 * S, lit))
    return made


def back_sockets(coll, at, outer, socket_y, sockets, upright=False):
    """The sockets of the back wall, its outside at x = outer in this scene: flanges outside, bodies inside (depths
    from their drawings). sockets: [(kind, x)], "xlr", "xlr_male" or "din" at their middle's x in the case (a male
    XLR has no latch and shows its five pins). The XLRs lie (flange 36 mm wide, latch at the side) or stand upright
    (26.4 mm wide, latch at the top). Returns the bodies."""
    nickel = material("Nickel", (0.75, 0.75, 0.73), 1.0, 0.28)
    dark = material("Socket insert", (0.03, 0.03, 0.03), 0.0, 0.5)
    inside = []
    for kind, cx in sockets:
        _, sy, sz = at(cx, socket_y, 0)
        if kind.startswith("xlr"):
            w, h = (13.2, 18) if upright else (18, 13.2)
            box(coll, "XLR flange", outer, outer + 2.4 * S, sy - w * S, sy + w * S, sz - h * S, sz + h * S, nickel)
            if kind == "xlr":                # the female's latch button and its insert with the holes
                if upright:
                    box(coll, "XLR latch", outer + 2.4 * S, outer + 3.6 * S, sy - 4 * S, sy + 4 * S, sz + 10.5 * S,
                        sz + 14.5 * S, dark)
                else:
                    box(coll, "XLR latch", outer + 2.4 * S, outer + 3.6 * S, sy + 10.5 * S, sy + 14.5 * S,
                        sz - 4 * S, sz + 4 * S, dark)
                cylinder(coll, "XLR insert", sy, sz, outer + 2.4 * S, outer + 3.0 * S, 10 * S, dark, axis="X")
            else:                            # the male's insert, its five pins standing out of it
                cylinder(coll, "XLR insert", sy, sz, outer + 2.4 * S, outer + 2.9 * S, 9.2 * S, dark, axis="X")
                for k in range(5):
                    a = math.radians((180 if upright else 90) + 72 * k)
                    py, pz = sy + 5.3 * math.cos(a) * S, sz + 5.3 * math.sin(a) * S
                    cylinder(coll, "XLR pin", py, pz, outer + 2.4 * S, outer + 5.5 * S, 0.8 * S, nickel, axis="X")
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


def stage():
    """Cycles, a soft studio light, a floor under the board and a camera. Returns the camera."""
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


def shoot(cam, name, loc, target, ortho=None):
    """One picture, OUT/name.png, from loc towards target (orthographic of that width if ortho)."""
    cam.data.type = "ORTHO" if ortho else "PERSP"
    if ortho:
        cam.data.ortho_scale = ortho
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("rendered", name)


def render(cam, views):
    """The board's pictures: views {name: (camera location, target, orthographic width or None)}."""
    os.makedirs(OUT, exist_ok=True)
    for name, (loc, target, ortho) in views.items():
        shoot(cam, name, loc, target, ortho)


# ---------------------------------------------------------------- the printed case (printcase.py's parts)

PRINT_WORK = os.path.join(this_box.WORK, "case_print")


def print_matrix(P):
    """The printed case's millimetres (case_layout.Printed P: y = 0 its inner floor, z = 0 its front wall's inner
    face) in this scene's metres."""
    return Matrix(((0, 0, S, -P.EDGE_Z * S), (S, 0, 0, -P.EDGE_X * S), (0, S, 0, -P.BOARD_BOTTOM * S),
                   (0, 0, 0, 1)))


def print_case_parts(P, place, sockets, leds=None, pipe_start=1.6):
    """The printed case (printcase.py's parts in work/case_print) around the board, in black PETG, with the brass
    standoffs, the light pipes (from pipe_start mm above the board; leds as for light_pipes) and the sockets of the
    back wall ([(kind, x)]). Returns the lid."""
    coll = collection("Printed case")
    petg = material("Black PETG", (0.006, 0.006, 0.007), 0.0, 0.72)
    m = print_matrix(P)
    import_stl(coll, os.path.join(PRINT_WORK, "base.stl"), "Printed base", m, petg)
    lid = import_stl(coll, os.path.join(PRINT_WORK, "lid.stl"), "Printed lid", m, petg)

    def at(x, y, z):
        return tuple(m @ Vector((x, y, z)))

    brass = material("Brass", (0.78, 0.62, 0.32), 1.0, 0.35)
    lid_under = at(0, P.INNER_H, 0)[2]
    for ref in ("H1", "H2", "H3", "H4"):
        x, y = place[ref][0] * S, -place[ref][1] * S
        cylinder(coll, f"Standoff {ref}", x, y, TOP, lid_under, 3.175 * S, brass, vertices=6)
    light_pipes(coll, place, at(0, P.INNER_H + P.LID_T, 0)[2], pipe_start, leds)
    back_sockets(coll, at, at(0, 0, P.INNER_L + P.BACK_WALL)[0], P.SOCKET_Y, sockets)
    return lid


def render_print_case(cam, P, place, sockets, views, leds=None, pipe_start=1.6):
    """The board in the printed case: views {name: (camera location, target)}; a name ending in "_open" is shot
    with the lid off."""
    old = bpy.data.collections.get("Case")
    if old:                                        # an aluminium case of the pictures before
        for o in list(old.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(old)
    knife = bpy.data.objects.get("Cut-away")
    if knife:
        bpy.data.objects.remove(knife, do_unlink=True)
    lid = print_case_parts(P, place, sockets, leds, pipe_start)
    floor = bpy.data.objects.get("Floor")
    if floor:
        floor.location.z = (-P.FLOOR_T - P.BOARD_BOTTOM) * S - 0.0001
        floor.scale = (2.0, 2.0, 1.0)
    for name, (loc, target) in views.items():
        lid.hide_render = name.endswith("_open")
        shoot(cam, name, loc, target)


def save():
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, this_box.NAME + ".blend"), compress=True)
