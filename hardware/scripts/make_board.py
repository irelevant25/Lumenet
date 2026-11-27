"""Places the parts of dmxbox 3 on its board (../dmxbox3.kicad_pcb), from KiCad's own net list of the schematic.

Run with KiCad's Python (it has pcbnew):  python make_board.py NETLIST
The net list comes from:  kicad-cli sch export netlist --format kicadsexpr -o NETLIST dmxbox3.kicad_sch
route.py routes the result; make_jlcpcb.py writes the files for JLCPCB.

The board: 86 x 74.5 mm, two layers. Left edge: the three USB-C sockets (OUT, IN, POWER). Top edge: the
box-number switch, its keys at the edge. Bottom edge: the LEDs and the pin headers of the MIDI sockets; right edge:
the pin header of the DMX socket (the sockets sit in the box wall, on cables). The isolated DMX side is right of a
2.5 mm gap that no track, via or copper pour crosses; only the optocouplers and the DC-DC converter bridge it.
Every part is on the top side (JLCPCB's economic assembly places one side only).
"""
import json
import os
import sys

import pcbnew

import circuit
import footprints
from kilib import fp_dir
from sexpr import parse, find, find_all

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, ".."))
BOARD = os.path.join(OUT, "dmxbox3.kicad_pcb")
W, H = 86.0, 74.5
GAP_X1, GAP_X2 = 60.2, 62.7          # the isolation gap: logic left, isolated right
ISO_NETS = circuit.ISOLATED

# ref -> (x, y, rotation in degrees, counter-clockwise). Millimetres from the board's top left corner.
PLACE = {
    # USB-C sockets at the left edge, mouths out: OUT on top, IN, POWER
    "J2": (3.2, 25.5, 270), "J1": (3.2, 40.5, 270), "J3": (3.2, 58.5, 270),
    # OUT: the switch that gives the next box 5 V, and the CC resistors
    "Q2": (11.0, 13.5, 0), "Q3": (15.2, 13.5, 0), "Q4": (17.6, 17.3, 0), "R11": (12.0, 16.9, 0),
    "R12": (17.6, 20.1, 0), "R13": (12.0, 18.7, 0), "C5": (9.4, 17.1, 90),
    "R5": (10.6, 21.5, 0), "R6": (14.0, 21.5, 0), "R7": (10.6, 23.1, 0), "R8": (14.0, 23.1, 0),
    # IN: CC resistors, the switch Q1 that POWER turns off
    "R1": (11.0, 44.1, 0), "R2": (11.0, 45.7, 0), "Q1": (12.4, 49.1, 0), "R9": (16.2, 48.5, 90),
    "R10": (17.8, 48.5, 90), "C1": (10.0, 51.9, 0),
    # POWER: CC resistors and the diode
    "D1": (13.0, 57.5, 90), "R3": (16.6, 56.5, 0), "R4": (16.6, 58.1, 0), "C2": (16.6, 54.9, 0),
    "C3": (21.0, 54.7, 0), "C4": (21.0, 56.7, 0),
    "R14": (26.0, 51.9, 90), "R15": (27.6, 51.9, 90), "C6": (29.2, 51.9, 90),
    # 3.3 V
    "U4": (20.6, 50.1, 0), "C7": (20.6, 47.1, 0), "C8": (24.0, 50.1, 90),
    # USB hub between OUT and IN
    "U2": (16.0, 32.5, 180), "Y2": (19.4, 37.1, 0), "C9": (12.6, 36.5, 90), "C10": (14.4, 37.1, 90),
    "C11": (16.2, 37.1, 90),
    # RP2040 and around it
    "U1": (32.0, 34.5, 90),
    "C16": (29.4, 28.1, 90), "C19": (27.8, 28.1, 90), "C20": (26.2, 28.1, 90), "C21": (24.6, 28.1, 90),
    "R19": (23.6, 31.7, 180), "R20": (23.6, 33.3, 180),
    "C18": (24.0, 35.9, 0), "C17": (24.0, 37.5, 0), "C23": (24.0, 39.1, 0),
    "U3": (21.2, 43.3, 0), "C27": (27.4, 43.1, 90), "R18": (27.4, 47.1, 90),
    "C15": (36.6, 28.9, 90), "C14": (36.8, 40.1, 90), "C22": (38.4, 40.1, 90),
    "C12": (29.2, 40.9, 90), "C13": (34.6, 41.1, 90),
    "Y1": (39.8, 33.3, 90), "C24": (42.8, 35.1, 90), "C25": (42.8, 31.5, 90), "R16": (39.0, 29.9, 0),
    "R17": (41.6, 28.1, 0),
    "J7": (47.6, 23.3, 90),
    # box number: piano switch at the top edge, keys out, above the RP2040
    # the keys towards the top edge: the pads on their side end 0.35 mm from it, the keys 0.25 mm
    "SW1": (32.01, 5.85, 0),
    # LEDs at the bottom edge, left: 4.4 mm apart, under 3 mm light pipes in the case's top
    "D4": (9.2, 71.9, 0), "D5": (13.6, 71.9, 0), "D6": (18.0, 71.9, 0),
    "R21": (9.2, 69.3, 0), "R22": (13.6, 69.3, 0), "R23": (18.0, 69.3, 0),
    # DMX: the logic side of the optocouplers
    # (the optocouplers by their middle: their gull-wing pads sit left and right of the gap)
    "R24": (54.2, 13.04, 0), "R26": (54.2, 27.5, 0), "R29": (54.2, 39.04, 0), "C32": (54.2, 43.9, 0),
    "U6": (62.01, 14.31, 0), "U7": (62.01, 27.31, 0), "U8": (62.01, 40.31, 180),
    # the DC-DC module along the gap's crossing: pins 1, 2 (and the free 8) left of it, 4 and 5 right
    "U5": (60.2, 53.0, 0), "C28": (57.66, 61.1, 180),
    # DMX: the isolated side
    "C29": (68.4, 53.0, 270), "C30": (69.2, 10.5, 90), "R25": (69.2, 14.9, 90), "C31": (69.2, 23.5, 90),
    "R27": (69.2, 27.9, 90), "R28": (70.2, 41.1, 0),
    "U9": (76.0, 16.5, 0), "C33": (76.0, 21.5, 0),
    "R30": (72.0, 34.5, 90), "R31": (73.2, 39.1, 0), "R32": (72.0, 43.7, 90), "D2": (76.4, 45.7, 0),
    "J4": (79.0, 35.58, 0),
    # MIDI at the bottom edge
    "U10": (39.81, 59.04, 0), "R33": (31.4, 61.1, 0), "D3": (31.4, 63.5, 0), "R34": (46.6, 58.5, 90),
    "C34": (46.6, 54.7, 90),
    "J5": (29.08, 70.8, 90), "J6": (51.08, 70.8, 90), "R35": (57.6, 65.5, 90), "R36": (55.8, 65.5, 90),
    # holes
    "H1": (4.0, 8.5, 0), "H2": (82.0, 8.5, 0), "H3": (4.0, 70.5, 0), "H4": (82.0, 70.5, 0),
}

# GND vias in the exposed pads of the RP2040 and the hub (0.3 mm holes: no extra charge at JLCPCB)
EP_VIAS = [(32.0 + dx, 34.5 + dy) for dx in (-0.8, 0.8) for dy in (-0.8, 0.8)] +           [(16.0 + dx, 32.5 + dy) for dx in (-0.6, 0.6) for dy in (-0.6, 0.6)]

SILK = [  # (text, x, y, size): labels on the board
    ("USB", 9.2, 73.7, 0.8), ("DMX", 13.6, 73.7, 0.8), ("MIDI", 18.0, 73.7, 0.8),
    ("OUT", 4.0, 18.7, 1.0), ("IN", 4.0, 33.9, 1.0), ("POWER", 4.6, 51.9, 1.0),
    ("MIDI IN", 29.08, 66.8, 1.0), ("MIDI OUT", 51.08, 66.8, 1.0),
    ("DMX OUT", 79.0, 26.1, 1.0), ("1 COM", 84.0, 30.5, 0.8), ("2 D-", 83.8, 33.04, 0.8),
    ("3 D+", 83.8, 35.58, 0.8), ("1", 21.9, 70.8, 0.8), ("1", 43.9, 70.8, 0.8),
    ("ISOLATED", 73.0, 61.5, 1.2), ("dmxbox 3", 73.0, 66.5, 1.6), ("Lumenet", 73.0, 70.0, 1.0),
] + [  # the box number: under each key of SW1 what it adds
    (str(2 ** k), 23.12 + 2.54 * k, 12.3, 0.8) for k in range(8)
] + [  # the service pins J7: each name above its pin, reading upwards
    (name, 41.25 + 2.54 * i, 20.2, 0.8, 90) for i, name in enumerate(("GND", "BOOT", "RUN", "SWCLK", "SWDIO", "LOG"))
]
REF_AT = {  # part names away from their footprint's own place, where that is taken: x, y
    "U5": (52.0, 53.0),                         # left of the DC-DC module: above it is U8's name
    "J5": (36.5, 70.8), "J6": (58.5, 70.8),     # the right ends of the MIDI headers: the "1" marks the left
}


def set_models(fp, ref):
    """3D models for the pictures: KiCad has none for the HRO USB-C socket (the GCT USB4105 has the same outline
    and stands in for it). The DC-DC module has none at all: make_3d.py makes it."""
    models = fp.Models()
    if ref in ("J1", "J2", "J3"):
        models[0].m_Filename = "${KICAD10_3DMODEL_DIR}/Connector_USB.3dshapes/"                                "USB_C_Receptacle_GCT_USB4105-xx-A_16P_TopMnt_Horizontal.step"


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
    """The vias in the exposed pads of the RP2040 and the hub (EP_VIAS): free vias, which the router keeps."""
    for x, y in EP_VIAS:
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
        if ref not in PLACE:
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
        x, y, r = PLACE[ref]
        fp.SetPosition(pt(x, y))
        fp.SetOrientationDegrees(r)
        set_models(fp, ref)
        # silkscreen: names of the parts you handle and of the chips; no values, no LCSC numbers
        fp.Value().SetVisible(False)
        for field in fp.GetFields():
            if field.GetName() in ("LCSC", "Fitted by", "Description"):
                field.SetVisible(False)
        small = ref[0] in "RCQ" or ref in ("D1", "D2", "D3", "Y1", "Y2", "H1", "H2", "H3", "H4")
        fp.Reference().SetVisible(not small)
        if not small:
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
    board.GetDesignSettings().SetAuxOrigin(pt(0, H))
    # the isolation gap: no track, via or pour crosses it (pads of the parts that bridge it may)
    rule_area(board, "isolation gap", [(GAP_X1, -1), (GAP_X2, -1), (GAP_X2, H + 1), (GAP_X1, H + 1)])
    apply_silk(board)
    return board, problems


# What apply_silk() takes off a board: kept here, because Python would free them while the board still knows them
_removed = []


def apply_silk(board):
    """The free texts on the top silkscreen, from SILK, and the part names placed by REF_AT (run again on a routed
    board: no new routing needed)."""
    for d in list(board.GetDrawings()):
        if isinstance(d, pcbnew.PCB_TEXT) and d.GetLayer() == pcbnew.F_SilkS:
            board.Remove(d)
            _removed.append(d)
    for s, x, y, size, *angle in SILK:
        silk_text(board, s, x, y, size, *angle)
    for fp in board.GetFootprints():
        if fp.GetReference() in REF_AT:
            fp.Reference().SetPosition(pt(*REF_AT[fp.GetReference()]))


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
        "meta": {"filename": "dmxbox3.kicad_pro", "version": 3},
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
                [{"netclass": "Power", "pattern": p} for p in ("VBUS_IN", "VBUS_PWR", "VBUS_OUT", "+5V",
                                                              "*/OUT_S", "OUT_S", "+5V_ISO", "*/+5V_ISO")]
                + [{"netclass": "Supply", "pattern": p} for p in ("GND", "GND_ISO", "*/GND_ISO", "+3V3",
                                                               "*/+1V1", "*/HUB_3V3")]),
        },
        "pcbnew": {"page_layout_descr_file": ""},
        "sheets": [], "text_variables": {}, "boards": [], "libraries": {"pinned_footprint_libs": [],
                                                                       "pinned_symbol_libs": []},
    }


def main():
    footprints.main()
    board, problems = build(sys.argv[1])
    if problems:
        print("\n".join(problems))
        return 1
    with open(os.path.join(OUT, "dmxbox3.kicad_pro"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(project_file(), f, indent=2)
    pcbnew.SaveBoard(BOARD, board)
    print("wrote", BOARD, len(list(board.GetFootprints())), "footprints")
    return 0


if __name__ == "__main__":
    sys.exit(main())
