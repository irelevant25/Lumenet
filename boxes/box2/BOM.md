# dmxbox 2: parts and how to build it

dmxbox 2 (box 2) is a Microchip PIC32MX270F256B with one DMX output with RDM and the switch for its number, and a
USB-C socket. Every part is through-hole: JLCPCB makes the bare board and you solder the parts. The PIC gets its
firmware from a programmer on J2. Designators (U1, R24, …) match the [schematic](dmxbox2_schematic.pdf) and the
[assembly drawing](dmxbox2_assembly.pdf); the DMX side is box 1's, part for part and with the same names.

The board is 100 × 62 mm, two layers.

## 1. The board from JLCPCB

Upload [jlcpcb/dmxbox2_gerbers.zip](jlcpcb/dmxbox2_gerbers.zip) on the PCB quote page. The defaults fit: 2 layers,
FR-4, 1.6 mm, any color, HASL lead-free, 5 boards. No assembly: you solder the parts.

## 2. What you buy

| Qty | Ref | Part | Notes |
|---|---|---|---|
| 1 | U1 | PIC32MX270F256B-I/SP, DIP-28 | The narrow DIP ("SP"). |
| 1 | U1 | IC socket DIP-28, 7.62 mm (narrow) | |
| 1 | Y1 | crystal 8 MHz, HC-49/US | |
| 1 | U2 | MCP1700-3302E/TO, 3.3 V regulator, TO-92 | Its flat side as on the board. |
| 1 | J1 | GCT USB4085-GF-A, USB-C socket, through-hole | |
| 3 | U6, U7, U8 | 6N137, optocoupler, DIP-8 | |
| 1 | U9 | MAX485 or SN75176BP, RS-485, DIP-8 | The same pins. |
| 4 | U6 to U9 | IC socket DIP-8, 7.62 mm | |
| 1 | U5 | Recom RKK-0505S/H, isolated DC-DC 5 V to 5 V, 1 W | SIP7 with 4 pins: it fits one way only. |
| 1 | SW1 | piano DIP switch, 8 keys, through-hole, rows 7.62 mm apart (for example ONPOW DPL08RGT) | The keys at the board edge. |
| 1 each | D4, D5, D6 | LED 3 mm: green, yellow, red | |
| 2 | D1, D2 | P6KE6.8CA, TVS diode, DO-15 | "CA": both ways, so either way round. |
| 2 | R1, R2 | 5.1 kΩ | Resistors: 0.25 W, axial (0207), legs bent to 7.62 mm. |
| 1 | R3 | 10 kΩ | |
| 4 | R4, R25, R27, R29 | 1 kΩ | |
| 2 | R21, R22 | 330 Ω | |
| 1 | R23 | 1.5 kΩ | |
| 2 | R24, R26 | 180 Ω | |
| 1 | R28 | 390 Ω | |
| 2 | R30, R32 | 470 Ω | |
| 1 | R31 | 120 Ω | |
| 1 | R33 | 270 Ω | |
| 9 | C3, C4, C5, C9, C26, C30 to C33 | 100 nF, ceramic, 5 mm lead spacing | |
| 2 | C7, C8 | 22 pF, ceramic C0G (NP0), 5 mm lead spacing | |
| 3 | C1, C2, C6 | 10 µF, ceramic X5R or X7R, 10 V or more, 5 mm lead spacing | C6 is the PIC's core supply (VCAP): not an electrolytic. |
| 1 | C29 | 10 µF, electrolytic, 16 V or more, 5 mm wide, 2 mm lead spacing | Mind its "−" stripe. |
| 1 | J2 | pin header, male, 1 × 6, 2.54 mm | For the programmer. |
| 1 | J4 | pin header, male, 1 × 5, 2.54 mm | To the XLR socket. |
| 1 | DMX OUT | XLR 5-pin **female** panel socket XLR-5G-C | In the back wall. |
| 1 | J4 | 5 wires with a 1 × 5 female housing, 2.54 mm (Dupont) | From J4 to the XLR socket. |
| | | the printed case | See [section 4](#4-the-case): about 70 g of PETG. |
| 4 | | M3 heat-set inserts, 5.7 mm long, 4.6 mm wide (for example Ruthex M3 × 5.7) | In the case's four posts. |
| 4 | H1 to H4 | M3 hex standoffs, male-female, 25 mm | Through the board into the inserts; the lid sits on them. |
| 4 | | M3 countersunk screws, 6 mm (ISO 10642) | The lid, into the standoffs. |
| 2 | | M3 screws, 10 mm, and nuts | The XLR. |
| 3 | D4, D5, D6 | round light pipes, 3 mm, about 19 mm below their head, for a 3.2 mm hole | The LEDs stand 8.3 mm high: a 25.4 mm pipe cut to 19 mm. |
| 4 | | rubber feet | If you like. |
| 1 | | USB-C cable, for data (not charge-only) | |

To put the firmware on the PIC once: a programmer, an MPLAB Snap (PG164100, the cheapest) or a PICkit 4 or 5, and
MPLAB X IDE with IPE from microchip.com (free).

## 3. Soldering

Low parts first, so the board lies flat while you solder: the resistors, the diodes D1 and D2, the ceramic
capacitors, the crystal Y1, the five IC sockets (their notch as on the board), U2 (its flat side as on the board),
J2 and J4, the USB-C socket J1, the LEDs (the flat side of the rim, the cathode, as on the board; their tops about
8 mm above the board, where the light pipes end), SW1 (its keys at the board edge), U5 (its pin 1 at the square pad)
and C29 (its stripe away from the "+" on the board). Then the chips into their sockets, each with its notch to the
notch of the socket: the PIC, the three 6N137 and the MAX485.

Test before the chips go in: with USB plugged in, 3.3 V between pins 2 (VDD) and 3 (GND) of J2, and about 5 V
between U5's pins 7 and 5 (the isolated side: 4.6 to 5.5 V).

Wiring the XLR socket: pin n of J4 is the socket's pin n (pin 1 is "1 COM" on the board): 1 common, 2 Data −,
3 Data +, 4 and 5 free. J4 is on the isolated side: pin 1 is the DMX common, not the box's ground.

## 4. The case

A base and a lid printed in PETG, 70 × 141 × 36 mm closed: box 1's case with an opening for the USB-C socket in
the front wall, the XLR in the middle of the back wall.
[case_print/dmxbox2_base.stl](case_print/dmxbox2_base.stl) and [case_print/dmxbox2_lid.stl](case_print/dmxbox2_lid.stl),
each already turned as it prints. Pictures: [from the front](3d/dmxbox2_print_front.png),
[from the back](3d/dmxbox2_print_back.png), [opened](3d/dmxbox2_print_open.png). Print it as box 4's case
([box 4's BOM.md](../box4/BOM.md#4-the-case)).

## 5. The firmware

The PIC comes empty and takes its firmware through J2:

1. Build it once: `firmware\build.cmd` makes `dmxbox2.hex` next to it (XC32 and the rest: see DEV.md,
   "dmxbox 2: the PIC32 firmware").
2. Plug the box into USB: the PIC gets its 3.3 V from the box, not from the programmer.
3. Join the programmer to J2, pin n to pin n: 1 MCLR (the arrow on the programmer), 2 VDD, 3 GND, 4 PGD, 5 PGC.
4. Run `firmware\flash.cmd` for an MPLAB Snap, `flash.cmd PK4` or `flash.cmd PK5` for a PICkit. Or in MPLAB IPE:
   device PIC32MX270F256B, the tool, the file, Program.

The box starts as soon as the programmer lets go of it; new firmware later goes the same way. Programming erases
the presets stored in the box.

## 6. Putting it together

1. Press the four heat-set inserts into the posts of the base: a soldering iron at about 240 °C, straight down
   until each one is flush, then let it cool.
2. Solder the cable to the XLR socket (pin n to pin n) and screw the socket into the back wall, the nuts inside.
3. Set the board on the posts, SW1 towards the slot in the side and the USB-C socket in the front opening. Screw
   the four standoffs through its holes into the inserts. Plug the cable onto J4.
4. Push the three light pipes into the lid from above, put the lid on and screw it to the standoffs.
5. Set the box number with SW1, through the slot with a pen tip: a key pressed down adds the number printed next
   to it (1, 2, 4 … 128 from the left; go by these, not by any numbers on the switch itself). With the number n
   the box has the address 10.1.n.1 and Art-Net universe n; every box on one computer needs its own number.
6. Plug it into a computer: a new network adapter appears, the green LED stops blinking and stays on.

## 7. J2: the programmer and the log

| Pin | Name | What it is for |
|---|---|---|
| 1 | MCLR | the programmer holds the PIC in reset while it writes |
| 2 | VDD | 3.3 V from the box: the programmer measures it |
| 3 | GND | ground |
| 4 | PGD | programming data; otherwise what the firmware reports, as text: to the RX of a 3.3 V USB-serial adapter (its GND to pin 3), 250000 baud |
| 5 | PGC | programming clock; otherwise the red LED |
| 6 | | free |

## Power

The box asks the computer for 250 mA and draws about 150 mA; any USB port gives that. Unlike the other boxes it
does not measure its supply voltage.

## Not built yet

This board is the first revision: nothing of it has been built or measured. It passes KiCad's electrical and
design rule checks, and its nets match the schematic. Its firmware builds without a warning; its DMX line (frames,
and the RDM search and requests through it) passes its test on a PC (`test_line`), but has never run on a PIC.
