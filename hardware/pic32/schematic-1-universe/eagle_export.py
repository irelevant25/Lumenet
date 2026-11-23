"""Writes this box as EAGLE files: <NAME>.sch (the schematic) and <NAME>.brd (a printed board with tracks
on both sides), which EAGLE, Fusion Electronics and KiCad open; and <NAME>_board.json, the same board
for the 3D model.

Run from the repository root (it takes a minute: it looks for the tracks):

    uv run --no-project --python 3.13 python hardware/pic32/schematic-1-universe/eagle_export.py

The board has the parts where the perfboard plan has them (perfboard.py, 90 x 150 mm, the same
holes), but it is a printed board: plated holes, copper on both sides and vias. Its tracks are
found by the same script as the perfboard's, with the top side as a second copper layer. The
check of the perfboard plan then goes over them (every net as in circuit.py, nothing in the gap,
no two nets touching), and after writing, the files are read back and compared with the net list.
"""
from __future__ import annotations

import json
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1]))
import eagle  # noqa: E402  (hardware/eagle.py)
import perfboard as pb  # noqa: E402
from circuit import PARTS, PIC_PINS  # noqa: E402

NAME = "dmxbox2_1_universe"
PCB = pb.Mode("pcb", via_cost=4.0, top_step=1.0, pins_on_top=True, top_branches=True, blocked_top=frozenset())
SUPPLIES = {"+5V", "GND", "+3V3", "+5V_ISO", "GND2"}
TRACK, SUPPLY_TRACK = 0.4, 0.8
VIA_DRILL, VIA_PAD = 0.6, 1.2


def xy(cell):
    """A hole on the board, in millimetres from the board's middle, y up."""
    c, r = cell
    return round((c - (len(pb.COLUMNS) - 1) / 2) * 2.54, 3), round(((pb.ROWS + 1) / 2 - r) * 2.54, 3)


def kind_of(ref, pads):
    """What a part is to EAGLE: its kind (parts of one kind share a package and a symbol) and drill."""
    if ref == "U1":
        return "PIC32MX270F256B_DIP28", 0.9
    if ref in ("U4", "U5", "U8", "U9"):
        return "6N137_DIP8", 0.9
    if ref in ("U6", "U10"):
        return "PC817_DIP4", 0.9
    if ref in ("U7", "U11"):
        return "MAX485_DIP8", 0.9
    if ref == "SW1":
        return "PIANO_DIP_SWITCH_8", 0.9
    if ref == "U2":
        return "MCP1700_TO92", 0.8
    if ref == "U3":
        return "RKK0505SH_SIP7", 0.9
    if ref == "J1":
        return "USB_C_BREAKOUT_1X6", 1.0
    if ref == "J2":
        return "HEADER_1X6", 1.0
    if ref in ("J3", "J4"):
        return "TERMINAL_3_5.08", 1.3
    if ref in ("SW2", "SW3"):
        return "BUTTON_12MM", 1.1
    (x1, y1), (x2, y2) = list(pads.values())[:2]
    span = round(max(abs(x2 - x1), abs(y2 - y1)), 2)
    if ref == "D1":
        return "LED_3MM", 0.8
    if ref[0] == "D":
        return f"DO15_{span:.2f}", 1.1
    if ref[0] == "Y":
        return "HC49_CRYSTAL", 0.8
    if ref[0] == "R":
        return f"R_{span:.2f}" + ("_STANDING" if span <= 5.1 else ""), 0.9
    return f"C_{span:.2f}", 0.9


def piano_outline(pads):
    """SW1's outline on the board: the body and its eight keys, which stick out sideways towards the
    board's edge on the side of pins 1 to 8 (sizes of an APEM DPL-08: the body 21.87 x 10.8 mm, the
    keys 1.4 mm wide, their ends 3.36 mm beyond the pin row, 12.57 mm over all)."""
    (x1, y1), (x8, y8), (x16, y16) = pads["1"], pads["8"], pads["16"]
    width, length = math.hypot(x1 - x16, y1 - y16), math.hypot(x8 - x1, y8 - y1)
    ox, oy = (x1 - x16) / width, (y1 - y16) / width          # from the pin 16 row towards the keys
    ax, ay = (x8 - x1) / length, (y8 - y1) / length          # from switch 1 to switch 8

    def spot(along, out):
        return round(x1 + ax * along + ox * out, 3), round(y1 + ay * along + oy * out, 3)

    end, side = (21.87 - length) / 2, (10.8 - width) / 2
    shapes = [[spot(-end, side), spot(length + end, side), spot(length + end, -width - side), spot(-end, -width - side),
               spot(-end, side)]]
    shapes += [[spot(k * 2.54 - 0.7, side), spot(k * 2.54 - 0.7, 3.36), spot(k * 2.54 + 0.7, 3.36),
                spot(k * 2.54 + 0.7, side)] for k in range(8)]
    return [(*a, *b) for shape in shapes for a, b in zip(shape, shape[1:])]


def pin_names(ref, pad_names):
    """The symbol's pins: the PIC's by their function (the number after @ is hidden by EAGLE)."""
    if ref == "U1":
        return {f"{PIC_PINS[int(n)].replace(' ', '/')}@{n}": n for n in pad_names}
    return {n: n for n in pad_names}


def runs(pairs):
    """Joined neighbouring holes as straight pieces of track."""
    pieces = []
    for run in pb.runs_of(pairs):
        pieces += list(zip(run, run[1:]))
    return pieces


def design(routes):
    by_ref = {}
    for pin, hole in pb.LEGS.items():
        ref, name = pin.split(".")
        by_ref.setdefault(ref, {})[name] = xy(pb.at(hole))
    parts = []
    order = lambda name: (0, int(name)) if name.isdigit() else (1, name)  # noqa: E731
    first_pin = {"J1": ["VBUS", "D-", "D+", "CC1", "CC2", "GND"], "SW2": ["a", "net", "gnd", "b"],
                 "SW3": ["a", "net", "gnd", "b"], "D1": ["A", "K"]}
    for ref in sorted(by_ref, key=lambda r: ("".join(c for c in r if not c.isdigit()), int("".join(c for c in r if c.isdigit()) or 0))):
        names = first_pin.get(ref) or sorted(by_ref[ref], key=order)
        pads = {name: by_ref[ref][name] for name in names}
        kind, drill = kind_of(ref, pads)
        value = PARTS[ref][1] if ref in PARTS else ""
        parts.append(eagle.Part(ref=ref, value=value, kind=kind, pads=pads, pins=pin_names(ref, names),
                                drill=drill, diameter=round(drill + 0.9, 2), description=PARTS.get(ref, ("",))[0],
                                outline=piano_outline(pads) if ref == "SW1" else []))
    pin_of = {part.ref: {pad: pin for pin, pad in part.pins.items()} for part in parts}
    nets = {net: [(pin.split(".")[0], pin_of[pin.split(".")[0]][pin.split(".")[1]]) for pin in pins]
            for net, pins in pb.PINS.items()}
    tracks, vias = [], []
    for net, (bottom, top, via_cells) in routes.items():
        width = SUPPLY_TRACK if net in SUPPLIES else TRACK
        for layer, pairs in ((16, bottom), (1, top)):
            for a, b in runs(pairs):
                (x1, y1), (x2, y2) = xy(a), xy(b)
                tracks.append((net, layer, x1, y1, x2, y2, width))
        for cell in via_cells:
            x, y = xy(cell)
            vias.append((net, x, y, VIA_DRILL, VIA_PAD))
    # The left edge where the perfboard is cut, right beside SW1's keys; the screws at the left sit
    # where the perfboard plan drills them out (B-1, B-57), those at the right in the corners.
    left, right, half_h = xy((0, 1))[0] - pb.CUT_LEFT, 45.0, 75.0
    outline = [(left, -half_h), (right, -half_h), (right, half_h), (left, half_h)]
    holes = [(*xy(pb.at(hole)), 3.2) for hole in pb.DRILLED] + [(right - 3.5, y * (half_h - 3.5), 3.2) for y in (-1, 1)]
    texts = [(-33.0, 72.0, 2.2, 21, pb.TITLE), (-33.0, 69.4, 1.2, 21, "USB-C > DMX512, PIC32MX270F256B")]
    return eagle.Design(name=f"{pb.TITLE}: USB to DMX512", parts=parts, nets=nets, outline=outline, holes=holes,
                        tracks=tracks, vias=vias, texts=texts,
                        note=f"Written by {Path(pb.SCRIPT).parent.as_posix()}/eagle_export.py from circuit.py and perfboard.py.")


def main():
    notes = []
    routes = pb.find_tracks(mode=PCB, report=notes)
    if routes is None:
        print("No way found for the tracks of the printed board:", *notes[-6:], sep="\n  ")
        return 1
    problems = [p for p in pb.check(routes, mode=PCB)]
    if problems:
        print("The printed board is wrong:", *problems, sep="\n  - ")
        return 1
    d = design(routes)
    sch, brd = HERE / f"{NAME}.sch", HERE / f"{NAME}.brd"
    eagle.write_sch(d, sch)
    eagle.write_brd(d, brd)
    problems = eagle.check_files(sch, brd, d)
    if problems:
        print("The EAGLE files are wrong:", *problems, sep="\n  - ")
        return 1
    (x0, y0), (x1, y1) = d.outline[0], d.outline[2]
    board = {"note": f"The printed board of {pb.TITLE} for the 3D model, millimetres from the middle of the "
                     "90 x 150 mm perfboard it is drawn on, y up; its left edge is cut off (outline).",
             "size": [round(x1 - x0, 2), round(y1 - y0, 2)], "outline": [x0, y0, x1, y1], "holes": d.holes,
             "pads": [{"pin": pin, "x": xy(pb.at(hole))[0], "y": xy(pb.at(hole))[1], "net": pb.NET_OF.get(pin)}
                      for pin, hole in pb.LEGS.items()],
             "tracks": [{"net": t[0], "layer": "bottom" if t[1] == 16 else "top", "from": [t[2], t[3]],
                         "to": [t[4], t[5]], "width": t[6]} for t in d.tracks],
             "vias": [{"net": v[0], "x": v[1], "y": v[2]} for v in d.vias]}
    (HERE / f"{NAME}_board.json").write_text(json.dumps(board, indent=1), encoding="utf-8")
    top = sum(1 for t in d.tracks if t[1] == 1)
    print(f"EAGLE: {len(d.parts)} parts, {len(d.nets)} nets, {len(d.tracks)} track pieces ({top} on the top), "
          f"{len(d.vias)} vias; written to {sch.name} and {brd.name}, read back and checked")
    return 0


if __name__ == "__main__":
    sys.exit(main())
