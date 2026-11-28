# Box 1: dmxbox 1, the box with the Pico

A Raspberry Pi Pico in socket strips, one DMX output with RDM and the box-number switch; the Pico's micro-USB is
the box's USB socket. Every part is through-hole: JLCPCB makes the bare board and you solder it. Not built yet.

| File or folder | What |
|---|---|
| [BOM.md](BOM.md) | What to order and buy, soldering, the case and putting the box together |
| `jlcpcb/` | The Gerber ZIP for JLCPCB (the board only) |
| `dmxbox1_schematic.pdf`, `dmxbox1_assembly.pdf`, `dmxbox1_copper.pdf` | The drawings |
| `case_print/` | The case printed in PETG: STL files for the slicer |
| `3d/` | The Blender model and the pictures |
| `firmware/` | `box.h` and `dmxbox1.h` make the shared RP2040 firmware box 1's; `build.cmd` builds `dmxbox1.uf2`, `flash.cmd` puts it onto a box |
| `dmxbox1.kicad_*`, `dmxbox1.pretty/` | The KiCad project, made by the scripts |
| `scripts/` | Make the board, the drawings, the pictures and the case from `circuit.py`, with the scripts all boxes share (`boxes/scripts`) |
| [perfboard/](perfboard/README.md) | The first box 1, built on a perfboard (2026-11-19): it only sends, has no switch and answers at 192.168.7.1; its firmware is in `firmware/archive/pico` |
