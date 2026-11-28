"""Routes the board of a box with FreeRouting, then pours the ground planes. The box's route.py runs it:

  python route.py FREEROUTING_CLI [PASSES]      (with KiCad's Python, in the box's scripts folder)

FreeRouting (https://github.com/freerouting/freerouting, the "freerouting-cli" build) reads the board as a
Specctra DSN file and writes the tracks back as a SES file. The pours come afterwards: GND on both layers left of
the isolation gap, GND_ISO on both layers right of it, so every part has a ground plane under it, and vias that join
each net's two pours (stitching vias).
"""
import os
import subprocess

import pcbnew

import board as layout
import box
import make_board
from board import mm

layout.use(make_board)
BOARD, WORK = box.BOARD, box.WORK
W, H, GAP_X1, GAP_X2 = make_board.W, make_board.H, make_board.GAP_X1, make_board.GAP_X2


def net_named(board, short):
    for name, ni in board.GetNetsByName().items():
        if str(name) == short or str(name).endswith("/" + short):
            return ni
    return None


def pour(board, netinfo, layer, pts, name, priority=0):
    z = pcbnew.ZONE(board)
    z.SetLayer(layer)
    z.SetNetCode(netinfo.GetNetCode())
    z.SetZoneName(name)
    z.SetAssignedPriority(priority)
    z.SetLocalClearance(mm(0.25))
    z.SetMinThickness(mm(0.2))
    z.SetThermalReliefGap(mm(0.3))
    z.SetThermalReliefSpokeWidth(mm(0.4))
    z.SetPadConnection(pcbnew.ZONE_CONNECTION_THERMAL)
    z.SetIslandRemovalMode(pcbnew.ISLAND_REMOVAL_MODE_ALWAYS)
    ol = z.Outline()
    ol.NewOutline()
    for x, y in pts:
        ol.Append(mm(x), mm(y))
    board.Add(z)


def add_pours(board):
    for z in list(board.Zones()):
        if not z.GetIsRuleArea():
            board.Remove(z)
    gnd, iso = net_named(board, "GND"), net_named(board, "GND_ISO")
    left = [(0, 0), (GAP_X1, 0), (GAP_X1, H), (0, H)]
    right = [(GAP_X2, 0), (W, 0), (W, H), (GAP_X2, H)]
    for layer, lname in ((pcbnew.F_Cu, "top"), (pcbnew.B_Cu, "bottom")):
        pour(board, gnd, layer, left, f"GND {lname}")
        pour(board, iso, layer, right, f"GND_ISO {lname}")
    pcbnew.ZONE_FILLER(board).Fill(board.Zones())
    # the two layers' pours of a net joined by vias: the DMX side has no part with pins through the board, so
    # without them its bottom pour would join nothing
    added = stitch(board, gnd) + stitch(board, iso)
    pcbnew.ZONE_FILLER(board).Fill(board.Zones())
    return added


def silk_texts(board, margin):
    """The boxes of every text printed on the board (its own and the parts' names), MARGIN mm larger: a via's hole
    there would cut a letter."""
    silk = (pcbnew.F_SilkS, pcbnew.B_SilkS)
    items = [d for d in board.GetDrawings() if isinstance(d, pcbnew.PCB_TEXT) and d.GetLayer() in silk]
    for fp in board.GetFootprints():
        items += [f for f in fp.GetFields() if f.IsVisible() and f.GetLayer() in silk]
        items += [g for g in fp.GraphicalItems() if isinstance(g, pcbnew.PCB_TEXT) and g.GetLayer() in silk]
    boxes = []
    for t in items:
        box = t.GetBoundingBox()
        box.Inflate(mm(margin))
        boxes.append(box)
    return boxes


def stitch(board, netinfo, apart=8.0):
    """Vias of a net, at least APART mm from each other and from its other vias, wherever a whole via (with 0.05 mm
    to spare) lies inside the net's pours on both layers, outside every part's courtyard and off the printed
    texts."""
    sizes = {c["name"]: (c["via_diameter"], c["via_drill"]) for c in layout.project_file()["net_settings"]["classes"]}
    code = netinfo.GetNetCode()
    fills = [z.GetFilledPolysList(z.GetLayer()) for z in board.Zones()
             if not z.GetIsRuleArea() and z.GetNetCode() == code]
    if len(fills) < 2:
        return 0
    probe = pcbnew.PCB_VIA(board)
    probe.SetNet(netinfo)
    diameter, drill = sizes.get(probe.GetNetClassName(), sizes["Default"])
    inside = []
    for fill in fills:
        room = pcbnew.SHAPE_POLY_SET(fill)
        room.Deflate(mm(diameter / 2 + 0.05), pcbnew.CORNER_STRATEGY_ROUND_ALL_CORNERS, mm(0.005))
        inside.append(room)
    parts = []
    for fp in board.GetFootprints():
        for layer in (pcbnew.F_CrtYd, pcbnew.B_CrtYd):
            yard = pcbnew.SHAPE_POLY_SET(fp.GetCourtyard(layer))
            if yard.OutlineCount():
                yard.Inflate(mm(diameter / 2), pcbnew.CORNER_STRATEGY_ROUND_ALL_CORNERS, mm(0.005))
                parts.append(yard)
    texts = silk_texts(board, diameter / 2 + 0.1)
    taken = [t.GetPosition() for t in board.GetTracks() if t.GetClass() == "PCB_VIA" and t.GetNetCode() == code]
    added = 0
    step = 0.5
    for j in range(int(H / step)):
        for i in range(int(W / step)):
            p = pcbnew.VECTOR2I(mm(i * step), mm(j * step))
            if not all(room.Contains(p) for room in inside) or any(yard.Contains(p) for yard in parts):
                continue
            if any(box.Contains(p) for box in texts):
                continue
            if any((p - q).EuclideanNorm() < mm(apart) for q in taken):
                continue
            v = pcbnew.PCB_VIA(board)
            v.SetPosition(p)
            v.SetWidth(mm(diameter))
            v.SetDrill(mm(drill))
            v.SetNet(netinfo)
            board.Add(v)
            taken.append(p)
            added += 1
    return added


def too_close(board, item):
    """Whether a track comes nearer to another net's track, via or pad than the clearance of the two."""
    layer = item.GetLayer()
    shape = item.GetEffectiveShape(layer)
    box = item.GetBoundingBox()
    box.Inflate(mm(1))
    others = [t for t in board.GetTracks() if t.IsOnLayer(layer)]
    others += [p for fp in board.GetFootprints() for p in fp.Pads() if p.IsOnLayer(layer)]
    for o in others:
        if o.GetNetCode() == item.GetNetCode() or not box.Intersects(o.GetBoundingBox()):
            continue
        if shape.Collide(o.GetEffectiveShape(layer), max(item.GetOwnClearance(layer), o.GetOwnClearance(layer))):
            return True
    return False


def widen_narrow_tracks(board):
    """FreeRouting narrows a track now and then where it squeezes past fine-pitch pads (the RP2040's are 0.4 mm
    apart). Each track under the rules' 0.15 mm gets 0.15 mm where it then keeps its clearance from every other
    net; returns (how many were widened, how many could not be)."""
    least = mm(0.15)
    widened = left = 0
    for t in [t for t in board.GetTracks() if t.GetClass() == "PCB_TRACK" and t.GetWidth() < least]:
        old = t.GetWidth()
        t.SetWidth(least)
        if too_close(board, t):
            t.SetWidth(old)
            left += 1
        else:
            widened += 1
    return widened, left


def vias_to_the_rules(board):
    """FreeRouting now and then gives a via the hole of one net class and the ring of another: every via gets
    its own class's size."""
    vias = {c["name"]: (c["via_diameter"], c["via_drill"])
            for c in layout.project_file()["net_settings"]["classes"]}
    for t in board.GetTracks():
        if t.GetClass() == "PCB_VIA":
            diameter, drill = vias.get(t.GetNetClassName(), vias["Default"])
            t.SetWidth(pcbnew.FromMM(diameter))
            t.SetDrill(pcbnew.FromMM(drill))


# What unrouted() takes off a board: kept here, because Python would free them while the board still knows them
_removed = []


def unrouted(board):
    """The board without tracks, vias and pours, with the free vias in the exposed pads made anew: a routed board's
    have lost their "free" mark, and the router would move them."""
    for t in list(board.GetTracks()):
        board.Remove(t)
        _removed.append(t)
    for z in list(board.Zones()):
        if not z.GetIsRuleArea():
            board.Remove(z)
            _removed.append(z)
    layout.add_ep_vias(board, net_named(board, "GND"))
    return board


def main(cli, passes="60"):
    """Routes the box's board with the FreeRouting program CLI, PASSES passes at most."""
    os.makedirs(WORK, exist_ok=True)
    dsn, ses = os.path.join(WORK, box.NAME + ".dsn"), os.path.join(WORK, box.NAME + ".ses")
    if not pcbnew.ExportSpecctraDSN(unrouted(pcbnew.LoadBoard(BOARD)), dsn):
        print("DSN export failed")
        return 1
    # FreeRouting does not finish the same way every time: try again when a connection is left open, or a
    # track came out narrower than the rules allow and cannot be widened
    for attempt in range(1, 9):
        if os.path.exists(ses):
            os.remove(ses)
        r = subprocess.run([cli, "-de", dsn, "-do", ses, "-mp", passes], capture_output=True, text=True)
        done = [ln for ln in r.stdout.splitlines() if "Auto-routing stage completed" in ln]
        print(f"attempt {attempt}:", done[-1].split("]")[-1].strip() if done else r.stdout[-1500:])
        if not (done and "(0 unrouted" in done[-1] and os.path.exists(ses)):
            continue
        board = unrouted(pcbnew.LoadBoard(BOARD))
        if not pcbnew.ImportSpecctraSES(board, ses):
            print("SES import failed")
            return 1
        widened, narrow = widen_narrow_tracks(board)
        if widened:
            print(f"   {widened} track(s) under 0.15 mm widened to 0.15 mm")
        if narrow == 0:
            break
        print(f"   {narrow} track(s) under 0.15 mm, too close to others to be widened: once more")
    else:
        print("no routing within the rules in eight attempts: nothing saved")
        return 1
    vias_to_the_rules(board)
    print("stitching vias between the pours:", add_pours(board))
    pcbnew.SaveBoard(BOARD, board)
    print("routed and saved", BOARD)
    return 0

