"""Writes the box with the Pico as EAGLE files: dmxbox.sch (the schematic) and dmxbox.brd (the carrier
board of hardware/pico/3d, with its tracks). EAGLE, Fusion Electronics and KiCad open them.

Run from the repository root:

    uv run --no-project --python 3.13 python hardware/pico/schematic-1-universe/eagle_export.py

The board comes from dmxbox_board.json next to this file: every pad with its net, the tracks, the
outline and the mounting holes, as the 3D model has them (100 x 66 mm, every track on the bottom).
After writing, the script reads both files back and checks that they join exactly the nets of the
board, and stops with a message if not.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parents[1]))
import eagle  # noqa: E402  (hardware/eagle.py)

# ref -> (value, kind, drill)
PARTS = {
    "U1": ("Raspberry Pi Pico", "PICO", 1.0),
    "U3": ("6N137", "6N137", 0.9),
    "U4": ("RKK-0505S/H", "RKK_SIP7", 0.9),
    "U5": ("SN75176BP", "SN75176", 0.9),
    "R2": ("270 Ω", "R_10.16", 0.9),
    "R3": ("180 Ω", "R_10.16", 0.9),
    "R4": ("1 kΩ", "R_10.16", 0.9),
    "C1": ("100 nF", "C_5.00", 0.9),
    "C2": ("100 nF", "C_5.00", 0.9),
    "C3": ("10 µF", "CP_2.00", 0.9),
    "C4": ("4.7 µF", "CP_2.00", 0.9),
    "D1": ("P6KE6.8CA", "DO15_10.16", 1.1),
    "D3": ("P6KE6.8CA", "DO15_10.16", 1.1),
    "J1": ("terminal 3 pole 5.08 mm", "TERMINAL_3", 1.3),
    "SW1": ("tact switch 12 mm: blackout", "BUTTON_12MM", 1.1),
    "SW2": ("tact switch 12 mm: preset 1", "BUTTON_12MM", 1.1),
}
LAYER = {"bottom": 16, "top": 1}


def pad_name(name):
    return {"+": "P", "-": "N"}.get(name, name)


def design(board):
    pads_of = {}
    for pad in board["pads"]:
        ref, name = pad["ref"].split(".", 1)
        pads_of.setdefault(ref, []).append((pad_name(name), (pad["x"], pad["y"]), pad["net"], pad["radius"]))
    parts = []
    for ref, pads in pads_of.items():
        value, kind, drill = PARTS[ref]
        radius = max(r for _, _, _, r in pads)
        parts.append(eagle.Part(ref=ref, value=value, kind=kind, pads={name: xy for name, xy, _, _ in pads},
                                pins={name: name for name, _, _, _ in pads}, drill=drill, diameter=round(2 * radius, 2)))
    nets = {}
    for pad in board["pads"]:
        if pad["net"]:
            ref, name = pad["ref"].split(".", 1)
            nets.setdefault(pad["net"], []).append((ref, pad_name(name)))
    tracks = []
    for track in board["tracks"]:
        points = track["points"]
        for (x1, y1), (x2, y2) in zip(points, points[1:]):
            tracks.append((track["net"], LAYER[track["layer"]], x1, y1, x2, y2, track["width"]))
    texts = [(-24.0, 27.0, 2.0, 21, "dmxbox"), (-24.0, 23.5, 1.2, 21, "USB > DMX512, Pico, one universe")]
    return eagle.Design(name="dmxbox: the box with the Pico (one DMX output)", parts=parts, nets=nets,
                        outline=[tuple(p) for p in board["outline"]], holes=[tuple(h) for h in board["holes"]],
                        tracks=tracks, texts=texts,
                        note="Written by hardware/pico/schematic-1-universe/eagle_export.py from dmxbox_board.json.")


def main():
    board = json.loads((HERE / "dmxbox_board.json").read_text(encoding="utf-8"))
    d = design(board)
    sch, brd = HERE / "dmxbox.sch", HERE / "dmxbox.brd"
    eagle.write_sch(d, sch)
    eagle.write_brd(d, brd)
    problems = eagle.check_files(sch, brd, d)
    if problems:
        print("The EAGLE files are wrong:", *problems, sep="\n  - ")
        return 1
    print(f"EAGLE: {len(d.parts)} parts, {len(d.nets)} nets, {len(d.tracks)} track pieces; "
          f"written to {sch.name} and {brd.name}, read back and checked")
    return 0


if __name__ == "__main__":
    sys.exit(main())
