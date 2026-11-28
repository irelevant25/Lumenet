"""Places the parts of a box on its board (NAME.kicad_pcb), from KiCad's own net list of the schematic, for the
box's make_board.py, which says where every part goes and calls main(). Needs KiCad's Python (pcbnew).

What the box's make_board.py gives (D below): W, H (the board, mm), GAP_X1, GAP_X2 (the isolation gap: logic left,
isolated right), PLACE {ref: (x, y, rotation)}, EP_VIAS [(x, y)] (ground vias in exposed pads), SILK [(text, x, y,
size[, angle])], REF_AT {ref: (x, y)}, named(ref) (whether its name is printed), set_models(fp, ref),
POWER_NETS and SUPPLY_NETS (net name patterns of the wider tracks) and FOOTPRINTS (the box's own footprints).
"""
import json
import os

import pcbnew

import box
import circuit
import footprintlib
from kilib import fp_dir
from sexpr import parse, find, find_all

D = None              # the box's make_board module


def use(design):
    global D
    D = design


def mm(v):
    return pcbnew.FromMM(v)


def pt(x, y):
    return pcbnew.VECTOR2I(mm(x), mm(y))


def read_netlist(path):
    with open(path, encoding="utf-8") as f:
        root = parse(f.read())
    comps = {}
    for c in find_all(find(root, "components"), "comp"):
        ref = str(find(c, "ref")[1])
        tst = find(c, "tstamps")
        sp = find(c, "sheetpath")
        sheet = str(find(sp, "tstamps")[1]) if sp else "/"
        comps[ref] = {"path": sheet + str(tst[1]) if tst else "", "value": str(find(c, "value")[1])}
    pin_net = {}
    for n in find_all(find(root, "nets"), "net"):
        name = str(find(n, "name")[1])
        for node in find_all(n, "node"):
            pin_net[(str(find(node, "ref")[1]), str(find(node, "pin")[1]))] = name
    return comps, pin_net


def add_line(board, x1, y1, x2, y2, layer, width=0.1):
    s = pcbnew.PCB_SHAPE(board)
    s.SetShape(pcbnew.SHAPE_T_SEGMENT)
    s.SetStart(pt(x1, y1))
    s.SetEnd(pt(x2, y2))
    s.SetLayer(layer)
    s.SetWidth(mm(width))
    board.Add(s)


def add_arc(board, cx, cy, sx, sy, angle, layer, width=0.1):
    s = pcbnew.PCB_SHAPE(board)
    s.SetShape(pcbnew.SHAPE_T_ARC)
    s.SetCenter(pt(cx, cy))
    s.SetStart(pt(sx, sy))
    s.SetArcAngleAndEnd(pcbnew.EDA_ANGLE(angle, pcbnew.DEGREES_T), True)
    s.SetLayer(layer)
    s.SetWidth(mm(width))
    board.Add(s)


def outline(board, r=2.0):
    W, H = D.W, D.H
    e = pcbnew.Edge_Cuts
    add_line(board, r, 0, W - r, 0, e)
    add_line(board, W, r, W, H - r, e)
    add_line(board, W - r, H, r, H, e)
    add_line(board, 0, H - r, 0, r, e)
    add_arc(board, r, r, 0, r, 90, e)
    add_arc(board, W - r, r, W - r, 0, 90, e)
    add_arc(board, W - r, H - r, W, H - r, 90, e)
    add_arc(board, r, H - r, r, H, 90, e)


def rule_area(board, name, pts, tracks=True, vias=True, pour=True):
    z = pcbnew.ZONE(board)
    z.SetIsRuleArea(True)
    z.SetZoneName(name)
    ls = pcbnew.LSET()
    ls.AddLayer(pcbnew.F_Cu)
    ls.AddLayer(pcbnew.B_Cu)
    z.SetLayerSet(ls)
    z.SetDoNotAllowTracks(tracks)
    z.SetDoNotAllowVias(vias)
    z.SetDoNotAllowZoneFills(pour)
    z.SetDoNotAllowPads(False)
    z.SetDoNotAllowFootprints(False)
    ol = z.Outline()
    ol.NewOutline()
    for x, y in pts:
        ol.Append(mm(x), mm(y))
    board.Add(z)


def silk_text(board, s, x, y, size, angle=0):
    """A text centred on (x, y); turned by `angle` degrees, it starts at (x, y) instead (90: upwards from there)."""
    t = pcbnew.PCB_TEXT(board)
    t.SetText(s)
    t.SetPosition(pt(x, y))
    t.SetLayer(pcbnew.F_SilkS)
    t.SetTextSize(pcbnew.VECTOR2I(mm(size), mm(size)))
    t.SetTextThickness(mm(max(0.15, size * 0.15)))
    if angle:
        t.SetTextAngleDegrees(angle)
        t.SetHorizJustify(pcbnew.GR_TEXT_H_ALIGN_LEFT)
    board.Add(t)


def add_ep_vias(board, gnd):
    """The vias in the exposed pads (the box's EP_VIAS): free vias, which the router keeps."""
    for x, y in D.EP_VIAS:
        v = pcbnew.PCB_VIA(board)
        v.SetPosition(pt(x, y))
        v.SetDrill(mm(0.3))
        v.SetWidth(mm(0.6))
        v.SetNet(gnd)
        v.SetIsFree(True)
        board.Add(v)


def build(netlist):
    comps, pin_net = read_netlist(netlist)
    board = pcbnew.BOARD()
    board.SetCopperLayerCount(2)
    nets = {}
    for name in sorted(set(pin_net.values())):
        ni = pcbnew.NETINFO_ITEM(board, name)
        board.Add(ni)
        nets[name] = ni
    problems = []
    for ref, d in circuit.PARTS.items():
        if ref not in D.PLACE:
            problems.append(f"{ref}: no place on the board")
            continue
        lib, name = d["fp"].split(":")
        fp = pcbnew.FootprintLoad(fp_dir(lib), name)
        if fp is None:
            problems.append(f"{ref}: footprint {d['fp']} not found")
            continue
        fp.SetFPID(pcbnew.LIB_ID(lib, name))
        fp.SetReference(ref)
        fp.SetValue(d["value"])
        # the same fields as the schematic symbol, so that KiCad's schematic parity check agrees
        fp.SetField("LCSC", d["lcsc"])
        fp.SetField("Fitted by", d["by"])
        fp.SetField("Description", d["desc"])
        if ref in comps and comps[ref]["path"]:
            fp.SetPath(pcbnew.KIID_PATH(comps[ref]["path"]))
        x, y, r = D.PLACE[ref]
        fp.SetPosition(pt(x, y))
        fp.SetOrientationDegrees(r)
        D.set_models(fp, ref)
        # silkscreen: names of the parts you handle and of the chips; no values, no LCSC numbers
        fp.Value().SetVisible(False)
        for field in fp.GetFields():
            if field.GetName() in ("LCSC", "Fitted by", "Description"):
                field.SetVisible(False)
        named = D.named(ref)
        fp.Reference().SetVisible(named)
        if named:
            fp.Reference().SetTextSize(pcbnew.VECTOR2I(mm(1.0), mm(1.0)))
            fp.Reference().SetTextThickness(mm(0.15))
        if d["by"] == "none" and ref.startswith("H"):
            fp.SetExcludedFromBOM(True)
            fp.SetExcludedFromPosFiles(True)
        for pad in fp.Pads():
            num = pad.GetNumber()
            net = pin_net.get((ref, num))
            if net:
                pad.SetNet(nets[net])
        board.Add(fp)
    add_ep_vias(board, nets.get("GND"))
    outline(board)
    # drill and placement files count from the board's bottom left corner
    board.GetDesignSettings().SetAuxOrigin(pt(0, D.H))
    # the isolation gap: no track, via or pour crosses it (pads of the parts that bridge it may)
    x1, x2 = D.GAP_X1, D.GAP_X2
    rule_area(board, "isolation gap", [(x1, -1), (x2, -1), (x2, D.H + 1), (x1, D.H + 1)])
    apply_silk(board)
    return board, problems


# What apply_silk() takes off a board: kept here, because Python would free them while the board still knows them
_removed = []


def apply_silk(board):
    """The free texts on the top silkscreen, from the box's SILK, and the part names placed by its REF_AT (run again
    on a routed board: no new routing needed)."""
    for d in list(board.GetDrawings()):
        if isinstance(d, pcbnew.PCB_TEXT) and d.GetLayer() == pcbnew.F_SilkS:
            board.Remove(d)
            _removed.append(d)
    for s, x, y, size, *angle in D.SILK:
        silk_text(board, s, x, y, size, *angle)
    for fp in board.GetFootprints():
        if fp.GetReference() in D.REF_AT:
            fp.Reference().SetPosition(pt(*D.REF_AT[fp.GetReference()]))


def project_file():
    """Design rules for JLCPCB's standard two-layer boards, and the net classes."""
    return {
        "board": {
            "design_settings": {
                "defaults": {"board_outline_line_width": 0.1, "copper_line_width": 0.2, "silk_line_width": 0.15,
                             "silk_text_size_h": 1.0, "silk_text_size_v": 1.0, "silk_text_thickness": 0.15,
                             "zones": {"min_clearance": 0.2}},
                "rules": {"min_clearance": 0.15, "min_copper_edge_clearance": 0.3, "min_hole_clearance": 0.25,
                          "min_hole_to_hole": 0.25, "min_through_hole_diameter": 0.3, "min_track_width": 0.15,
                          "min_via_annular_width": 0.13, "min_via_diameter": 0.6, "min_text_height": 0.8,
                          "min_text_thickness": 0.15, "min_silk_clearance": 0.0, "max_error": 0.005,
                          "min_resolved_spokes": 1,
                          "allow_blind_buried_vias": False, "allow_microvias": False},
                "rule_severities": {"lib_footprint_issues": "ignore", "lib_footprint_mismatch": "ignore",
                                    "silk_overlap": "ignore", "silk_over_copper": "ignore",
                                    "silk_edge_clearance": "ignore", "text_height": "warning",
                                    "footprint_symbol_mismatch": "ignore"},
            },
        },
        "meta": {"filename": box.NAME + ".kicad_pro", "version": 3},
        "net_settings": {
            "classes": [
                {"name": "Default", "clearance": 0.15, "track_width": 0.2, "via_diameter": 0.6, "via_drill": 0.3,
                 "diff_pair_gap": 0.15, "diff_pair_width": 0.2, "diff_pair_via_gap": 0.25,
                 "microvia_diameter": 0.3, "microvia_drill": 0.1, "bus_width": 12, "wire_width": 6,
                 "line_style": 0, "pcb_color": "rgba(0, 0, 0, 0.000)", "schematic_color": "rgba(0, 0, 0, 0.000)",
                 "priority": 2147483647},
                {"name": "Supply", "clearance": 0.15, "track_width": 0.3, "via_diameter": 0.6, "via_drill": 0.3,
                 "diff_pair_gap": 0.15, "diff_pair_width": 0.2, "diff_pair_via_gap": 0.25,
                 "microvia_diameter": 0.3, "microvia_drill": 0.1, "bus_width": 12, "wire_width": 6,
                 "line_style": 0, "pcb_color": "rgba(0, 0, 0, 0.000)", "schematic_color": "rgba(0, 0, 0, 0.000)",
                 "priority": 1},
                {"name": "Power", "clearance": 0.2, "track_width": 0.4, "via_diameter": 0.8, "via_drill": 0.4,
                 "diff_pair_gap": 0.15, "diff_pair_width": 0.2, "diff_pair_via_gap": 0.25,
                 "microvia_diameter": 0.3, "microvia_drill": 0.1, "bus_width": 12, "wire_width": 6,
                 "line_style": 0, "pcb_color": "rgba(0, 0, 0, 0.000)", "schematic_color": "rgba(0, 0, 0, 0.000)",
                 "priority": 0},
            ],
            "meta": {"version": 5},
            "netclass_assignments": None,
            "netclass_patterns": (
                [{"netclass": "Power", "pattern": p} for p in D.POWER_NETS]
                + [{"netclass": "Supply", "pattern": p} for p in D.SUPPLY_NETS]),
        },
        "pcbnew": {"page_layout_descr_file": ""},
        "sheets": [], "text_variables": {}, "boards": [], "libraries": {"pinned_footprint_libs": [],
                                                                       "pinned_symbol_libs": []},
    }


def main(design, netlist):
    """Places the parts of DESIGN (the box's make_board module) from NETLIST and writes the board and the project."""
    use(design)
    footprintlib.main(D.FOOTPRINTS)
    board, problems = build(netlist)
    if problems:
        print("\n".join(problems))
        return 1
    with open(os.path.join(box.OUT, box.NAME + ".kicad_pro"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(project_file(), f, indent=2)
    pcbnew.SaveBoard(box.BOARD, board)
    print("wrote", box.BOARD, len(list(board.GetFootprints())), "footprints")
    return 0
