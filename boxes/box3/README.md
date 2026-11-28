# Box 3: dmxbox 3, the small RP2040 box

An RP2040 with one USB-C socket, one DMX output with RDM and the box-number switch: box 4 without the hub, MIDI
and the other two USB-C sockets. Every part sits on the top side, and JLCPCB solders them all. Not built yet.

| File or folder | What |
|---|---|
| [BOM.md](BOM.md) | What to order and buy, the case and putting the box together |
| `jlcpcb/` | The Gerber ZIP, the BOM and the placement file for JLCPCB |
| `dmxbox3_schematic.pdf`, `dmxbox3_assembly.pdf`, `dmxbox3_copper.pdf` | The drawings |
| `case_print/` | The case printed in PETG: STL files for the slicer |
| `3d/` | The Blender model and the pictures |
| `firmware/` | `box.h` and `dmxbox3.h` make the shared RP2040 firmware box 3's; `build.cmd` builds `dmxbox3.uf2`, `flash.cmd` puts it onto a box |
| `dmxbox3.kicad_*`, `dmxbox3.pretty/` | The KiCad project, made by the scripts |
| `scripts/` | Make the board, the drawings, the pictures and the case from `circuit.py`, with the scripts all boxes share (`boxes/scripts`) |
