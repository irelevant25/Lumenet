# Box 2: dmxbox 2, the PIC32 box

A Microchip PIC32MX270F256B in a DIP-28 socket, one DMX output with RDM, the box-number switch and a through-hole
USB-C socket (GCT USB4085). Every part is through-hole: JLCPCB makes the bare board and you solder it. The PIC
takes its firmware from a programmer on J2. Not built yet.

| File or folder | What |
|---|---|
| [BOM.md](BOM.md) | What to order and buy, soldering, the firmware, the case and putting the box together |
| `jlcpcb/` | The Gerber ZIP for JLCPCB (the board only) |
| `dmxbox2_schematic.pdf`, `dmxbox2_assembly.pdf`, `dmxbox2_copper.pdf` | The drawings |
| `case_print/` | The case printed in PETG: STL files for the slicer |
| `3d/` | The Blender model and the pictures |
| `firmware/` | `box.h`, box 2's settings for its firmware (`firmware/pic32`); `build.cmd` builds `dmxbox2.hex`, `flash.cmd` writes it into the PIC with a programmer |
| `dmxbox2.kicad_*`, `dmxbox2.pretty/` | The KiCad project, made by the scripts |
| `scripts/` | Make the board, the drawings, the pictures and the case from `circuit.py`, with the scripts all boxes share (`boxes/scripts`) |
| [perfboard/](perfboard/README.md) | dmxbox 2 as it was drawn on 2026-11-23 for a perfboard, with one or two outputs and no RDM; never built. Its firmware, compiled but never run, is in `firmware/archive/pic32` |
