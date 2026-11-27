"""Writes the drawings of dmxbox 3 for reading and printing:

  ../dmxbox3_schematic.pdf      the four schematic sheets
  ../dmxbox3_assembly.pdf       the board from above: where every part goes, with its name (for soldering)
  ../dmxbox3_copper.pdf         the two copper layers (top, then bottom seen from above)

The board is drawn at 1.5 : 1, in the middle of the A4 sheet: at any scale but 1 : 1 KiCad centres it on the
sheet (at 1 : 1 it would sit where the board's own corner is, at the sheet's corner, across the frame).

Run with any Python 3 once KiCad is installed:  python make_drawings.py
"""
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
HW = os.path.normpath(os.path.join(HERE, ".."))
SCALE = "1.5"
CLI = os.path.join(os.environ["LOCALAPPDATA"], "Programs", "KiCad", "10.0", "bin", "kicad-cli.exe")


def cli(*args):
    r = subprocess.run([CLI, *args], capture_output=True, text=True)
    if r.returncode != 0:
        raise SystemExit(f"kicad-cli {' '.join(args)}:\n{r.stdout}\n{r.stderr}")


def main():
    sch = os.path.join(HW, "dmxbox3.kicad_sch")
    pcb = os.path.join(HW, "dmxbox3.kicad_pcb")
    cli("sch", "export", "pdf", "-o", os.path.join(HW, "dmxbox3_schematic.pdf"), sch)
    cli("pcb", "export", "pdf", "--layers", "F.Fab,F.Silkscreen,Edge.Cuts", "--mode-single", "--include-border-title",
        "--scale", SCALE,
        "-o", os.path.join(HW, "dmxbox3_assembly.pdf"), pcb)
    cli("pcb", "export", "pdf", "--layers", "F.Cu,B.Cu,Edge.Cuts", "--mode-multipage", "--include-border-title",
        "--scale", SCALE,
        "--cl", "Edge.Cuts", "-o", os.path.join(HW, "dmxbox3_copper.pdf"), pcb)
    print("wrote dmxbox3_schematic.pdf, dmxbox3_assembly.pdf, dmxbox3_copper.pdf")
    return 0


if __name__ == "__main__":
    sys.exit(main())
