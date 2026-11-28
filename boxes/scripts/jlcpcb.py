"""Writes what JLCPCB needs to make and assemble a box, into its jlcpcb/ folder (the box's make_jlcpcb.py runs it):

  NAME_gerbers.zip     the board: Gerbers and drill files (upload this first)
  NAME_bom.csv         parts JLCPCB solders: Comment, Designator, Footprint, LCSC Part #
  NAME_cpl.csv         where they go: Designator, Mid X, Mid Y, Layer, Rotation

Only the parts marked "jlc" in the box's circuit.py are in the BOM and the placement file; the BOM and the
placement file are left out when there are none (a box you solder yourself).

Rotations: KiCad and JLCPCB's library do not always agree on where a part's pin 1 is. The corrections below are
what JLCPCB's placement preview of 27 November 2026 showed for these parts (their pink pin-1 dots against the board's
pin 1): the JLCKicadTools list had -90 for SOT-23 and 270 for the RP2040, both wrong by then, and nothing for the
optocouplers, the pin headers and the switch. Check the preview on every order anyway ("Confirm Parts Placement"
lets JLCPCB's engineers check it too).
"""
import csv
import io
import os
import re
import subprocess
import zipfile

import box
import circuit

OUT = os.path.join(box.OUT, "jlcpcb")
WORK = os.path.join(box.WORK, "jlcpcb")
BOARD = box.BOARD
KICAD_CLI = os.path.join(os.environ["LOCALAPPDATA"], "Programs", "KiCad", "10.0", "bin", "kicad-cli.exe")

ROTATION = [  # footprint name pattern, degrees added to KiCad's rotation (checked parts in the comments)
    (r"^SOT-23", 180),                                  # Q1 to Q4, U4, D2
    (r"^SOIC-", 270),                                   # U3, U9
    (r"^QFN-24-", 270),                                 # U2, the CH334F
    (r"^QFN-56-", 0),                                   # U1, the RP2040: pin 1 where KiCad has it
    (r"^USB_C_Receptacle_HRO_TYPE-C-31-M-12", 180),     # J1 to J3
    (r"^SMDIP-", 270),                                  # U6, U7, U8, U10
    (r"^Pin(Header|Socket)_1x0\d_P2\.54mm_Vertical_SMD", 270),  # J4 to J7, along their pads
    (r"^SW_DIP_SPSTx08_Piano_Kangshen", 180),           # SW1, its keys towards the board edge
]
COMMENT = {"C165948": "USB-C 16P TYPE-C-31-M-12",   # one BOM line for parts whose values differ
           "C41417362": "Pin header 1x5 SMD 2.54mm"}
LAYERS = "F.Cu,B.Cu,F.Paste,B.Paste,F.Silkscreen,B.Silkscreen,F.Mask,B.Mask,Edge.Cuts"


def cli(*args):
    r = subprocess.run([KICAD_CLI, *args], capture_output=True, text=True)
    if r.returncode != 0:
        raise SystemExit(f"kicad-cli {' '.join(args)} failed:\n{r.stdout}\n{r.stderr}")
    return r.stdout


def gerbers():
    g = os.path.join(WORK, "gerbers")
    os.makedirs(g, exist_ok=True)
    for f in os.listdir(g):
        os.remove(os.path.join(g, f))
    cli("pcb", "export", "gerbers", "--layers", LAYERS, "--no-x2", "--no-netlist", "--subtract-soldermask",
        "--check-zones", "--use-drill-file-origin", "-o", g, BOARD)
    cli("pcb", "export", "drill", "--format", "excellon", "--excellon-units", "mm", "--excellon-separate-th",
        "--excellon-oval-format", "alternate", "--drill-origin", "plot", "-o", g, BOARD)
    path = os.path.join(OUT, box.NAME + "_gerbers.zip")
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as z:
        for f in sorted(os.listdir(g)):
            z.write(os.path.join(g, f), f)
    return path, sorted(os.listdir(g))


def placements():
    pos = os.path.join(WORK, "pos.csv")
    cli("pcb", "export", "pos", "--format", "csv", "--units", "mm", "--side", "front", "--use-drill-file-origin",
        "-o", pos, BOARD)
    with open(pos, encoding="utf-8") as f:
        return {r["Ref"]: r for r in csv.DictReader(f)}


def rotation(fp_name, rot):
    for pat, add in ROTATION:
        if re.search(pat, fp_name):
            return (rot + add) % 360
    return rot % 360


def bom_and_cpl(pos):
    jlc = {ref: d for ref, d in circuit.PARTS.items() if d["by"] == "jlc"}
    groups = {}
    for ref, d in jlc.items():
        groups.setdefault((COMMENT.get(d["lcsc"], d["value"]), d["fp"].split(":")[1], d["lcsc"]), []).append(ref)
    bom = os.path.join(OUT, box.NAME + "_bom.csv")
    with open(bom, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f)
        w.writerow(["Comment", "Designator", "Footprint", "LCSC Part #"])
        for (value, fp, lcsc), refs in sorted(groups.items(), key=lambda g: natural(g[1][0])):
            w.writerow([value, ",".join(sorted(refs, key=natural)), fp, lcsc])
    cpl = os.path.join(OUT, box.NAME + "_cpl.csv")
    missing = []
    with open(cpl, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f)
        w.writerow(["Designator", "Mid X", "Mid Y", "Layer", "Rotation"])
        for ref in sorted(jlc, key=natural):
            p = pos.get(ref)
            if not p:
                missing.append(ref)
                continue
            fp = jlc[ref]["fp"].split(":")[1]
            w.writerow([ref, f"{float(p['PosX']):.3f}mm", f"{float(p['PosY']):.3f}mm", "Top",
                        f"{rotation(fp, float(p['Rot'])):g}"])
    return bom, cpl, len(groups), len(jlc), missing


def natural(ref):
    m = re.match(r"([A-Z]+)(\d+)", ref)
    return (m.group(1), int(m.group(2))) if m else (ref, 0)


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(WORK, exist_ok=True)
    z, files = gerbers()
    print(f"{z}: {', '.join(files)}")
    if not any(d["by"] == "jlc" for d in circuit.PARTS.values()):
        return 0
    bom, cpl, lines, parts, missing = bom_and_cpl(placements())
    if missing:
        print("not on the board:", missing)
        return 1
    print(f"{bom}: {lines} lines, {parts} parts")
    print(f"{cpl}")
    return 0

