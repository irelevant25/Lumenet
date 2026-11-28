# Box 4: dmxbox 4, the full box

An RP2040 behind a USB hub: IN from the computer, OUT for the next box, POWER for a charger, one DMX output with
RDM, a DMX input (a console plays the desk), MIDI IN and OUT, and the box-number switch. Every part sits on the top side, and JLCPCB solders them all.
Until 2026-11-28 it was called dmxbox 3. How it works: [DEV.md](../../DEV.md#1-hardware).

| File or folder | What |
|---|---|
| [BOM.md](BOM.md) | What to order and buy, how to print the case and put the box together |
| `jlcpcb/` | The Gerber ZIP, the BOM and the placement file for JLCPCB |
| `dmxbox4_schematic.pdf`, `dmxbox4_assembly.pdf`, `dmxbox4_copper.pdf` | The drawings |
| `case_print/` | The case printed in PETG: STL files for the slicer |
| `case/` | JLCMC's aluminium case (K119-12063-H7-L130) with its holes, instead |
| `3d/` | The Blender model and the pictures |
| `firmware/` | `box.h` and `dmxbox4.h` make the shared RP2040 firmware box 4's; `build.cmd` builds `dmxbox4.uf2`, `flash.cmd` puts it onto a box |
| `dmxbox4.kicad_*`, `dmxbox4.pretty/` | The KiCad project, made by the scripts |
| `scripts/` | Make the board, the drawings, the pictures and the cases from `circuit.py` |
