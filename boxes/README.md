# The dmxboxes

Four boxes that bring the Lumenet desk to DMX. Each is a USB network adapter for the computer with one DMX
output on a 5-pin XLR socket, and the desk finds it by itself; box 4 also takes a console on a DMX input.

| Box | Chip | Board | What it has | Where it stands |
|---|---|---|---|---|
| [Box 1](box1/README.md) | Raspberry Pi Pico | through hole, a JLCPCB board you solder | the Pico's micro-USB | board and case drawn, not built yet; the first box 1, on a perfboard, works |
| [Box 2](box2/README.md) | PIC32MX270 (DIP-28) | through hole, a JLCPCB board you solder, USB-C too | one USB-C | board and case drawn, not built yet; its firmware needs a programmer once |
| [Box 3](box3/README.md) | RP2040 | SMD, soldered by JLCPCB | one USB-C | board and case drawn, not built yet |
| [Box 4](box4/README.md) | RP2040 and a USB hub | SMD, soldered by JLCPCB | IN, OUT for the next box, POWER; MIDI IN and OUT; DMX IN | board, printed case and aluminium case drawn, not built yet |

All four get RDM (the desk sets the address, mode and name of a light) and the 8-key switch for the box number:
the box with the number n answers at 10.1.n.1 and plays universe n. Boxes 1 and 2 are made for soldering by hand,
on boards from JLCPCB.

## What is where

| Folder | What |
|---|---|
| `boxN/` | One box: its board design, JLCPCB files, drawings, 3D pictures, case and parts list, and in `firmware/` its settings for the shared firmware with `build.cmd` and `flash.cmd` |
| `datasheets/` | Datasheets of the parts of all boxes |
| `scripts/` | The code the boxes' scripts share: the schematic, the board, the routing, the checks, the JLCPCB files, the drawings, the printed case and the pictures; and `eagle.py`, which writes the EAGLE files of the perfboard designs |
| `archive/` | The designs with an Ethernet port, until 2026-11-12 |

The firmware is in `firmware/` at the top of the repository: `rp2040/` is one program for the RP2040 boxes,
built for a box with its settings (`build.cmd 4`), `pic32/` is box 2's, and `common/` is the code and the
settings (`config.h`) all boxes, the simulator and the tests share. The first firmware of boxes 1 and 2 is in
`firmware/archive/`.
