"""Writes the drawings of a box for reading and printing, into its folder (the box's make_drawings.py runs it):

  NAME_schematic.pdf      the schematic sheets
  NAME_assembly.pdf       the board from above: where every part goes, with its name (for soldering)
  NAME_copper.pdf         the two copper layers (top, then bottom seen from above)

The board is drawn at 1.5 : 1, in the middle of the A4 sheet: at any scale but 1 : 1 KiCad centres it on the
sheet (at 1 : 1 it would sit where the board's own corner is, at the sheet's corner, across the frame).

Runs with any Python 3 once KiCad is installed.
"""
import os
import subprocess

import box

SCALE = "1.5"
CLI = os.path.join(os.environ["LOCALAPPDATA"], "Programs", "KiCad", "10.0", "bin", "kicad-cli.exe")


def cli(*args):
    r = subprocess.run([CLI, *args], capture_output=True, text=True)
    if r.returncode != 0:
        raise SystemExit(f"kicad-cli {' '.join(args)}:\n{r.stdout}\n{r.stderr}")


def main():
    out, name = box.OUT, box.NAME
    sch = os.path.join(out, name + ".kicad_sch")
    pcb = box.BOARD
    cli("sch", "export", "pdf", "-o", os.path.join(out, name + "_schematic.pdf"), sch)
    cli("pcb", "export", "pdf", "--layers", "F.Fab,F.Silkscreen,Edge.Cuts", "--mode-single", "--include-border-title",
        "--scale", SCALE,
        "-o", os.path.join(out, name + "_assembly.pdf"), pcb)
    cli("pcb", "export", "pdf", "--layers", "F.Cu,B.Cu,Edge.Cuts", "--mode-multipage", "--include-border-title",
        "--scale", SCALE,
        "--cl", "Edge.Cuts", "-o", os.path.join(out, name + "_copper.pdf"), pcb)
    print(f"wrote {name}_schematic.pdf, {name}_assembly.pdf, {name}_copper.pdf")
    return 0
