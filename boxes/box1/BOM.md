# dmxbox 1: parts and how to build it

dmxbox 1 (box 1) is a Raspberry Pi Pico with one DMX output with RDM and the switch for its number. The Pico's own
micro-USB is the box's USB socket. Every part is through-hole: JLCPCB makes the bare board and you solder the parts.
Designators (U1, R24, …) match the [schematic](dmxbox1_schematic.pdf) and the
[assembly drawing](dmxbox1_assembly.pdf); the parts that do what box 4's do have box 4's names, so there are gaps.

The board is 100 × 62 mm, two layers.

## 1. The board from JLCPCB

Upload [jlcpcb/dmxbox1_gerbers.zip](jlcpcb/dmxbox1_gerbers.zip) on the PCB quote page. The defaults fit: 2 layers,
FR-4, 1.6 mm, any color, HASL lead-free, 5 boards. No assembly: you solder the parts.

## 2. What you buy

| Qty | Ref | Part | Notes |
|---|---|---|---|
| 1 | U1 | Raspberry Pi Pico | The plain Pico, or the Pico H (its headers already on). |
| 2 | U1 | pin header, male, 1 × 20, 2.54 mm | Only for a plain Pico: solder them to it, pins down. |
| 2 | U1 | socket strip, female, 1 × 20, 2.54 mm, 8.5 mm high | On the board; the Pico plugs in. |
| 3 | U6, U7, U8 | 6N137, optocoupler, DIP-8 | |
| 1 | U9 | MAX485 or SN75176BP, RS-485, DIP-8 | The same pins. |
| 4 | U6 to U9 | IC socket DIP-8, 7.62 mm | |
| 1 | U5 | Recom RKK-0505S/H, isolated DC-DC 5 V to 5 V, 1 W | SIP7 with 4 pins: it fits one way only. |
| 1 | SW1 | piano DIP switch, 8 keys, through-hole, rows 7.62 mm apart (for example ONPOW DPL08RGT) | The keys at the board edge. |
| 1 each | D4, D5, D6 | LED 3 mm: green, yellow, red | |
| 2 | D1, D2 | P6KE6.8CA, TVS diode, DO-15 | "CA": both ways, so either way round. |
| 1 | R21 | 100 Ω | Resistors: 0.25 W, axial (0207), legs bent to 7.62 mm. |
| 2 | R22, R23 | 330 Ω | |
| 2 | R24, R26 | 180 Ω | |
| 3 | R25, R27, R29 | 1 kΩ | |
| 1 | R28 | 390 Ω | |
| 2 | R30, R32 | 470 Ω | |
| 1 | R31 | 120 Ω | |
| 1 | R33 | 270 Ω | |
| 5 | C26, C30 to C33 | 100 nF, ceramic, 5 mm lead spacing | |
| 1 | C29 | 10 µF, electrolytic, 16 V or more, 5 mm wide, 2 mm lead spacing | Mind its "−" stripe. |
| 1 | J4 | pin header, male, 1 × 5, 2.54 mm | To the XLR socket. |
| 1 | J7 | pin header, male, 1 × 3, 2.54 mm | The service pins: GND, RUN, LOG. |
| 1 | DMX OUT | XLR 5-pin **female** panel socket XLR-5G-C | In the back wall. |
| 1 | J4 | 5 wires with a 1 × 5 female housing, 2.54 mm (Dupont) | From J4 to the XLR socket. |
| | | the printed case | See [section 4](#4-the-case): about 70 g of PETG. |
| 4 | | M3 heat-set inserts, 5.7 mm long, 4.6 mm wide (for example Ruthex M3 × 5.7) | In the case's four posts. |
| 4 | H1 to H4 | M3 hex standoffs, male-female, 25 mm | Through the board into the inserts; the lid sits on them. |
| 4 | | M3 countersunk screws, 6 mm (ISO 10642) | The lid, into the standoffs. |
| 2 | | M3 screws, 10 mm, and nuts | The XLR. |
| 3 | D4, D5, D6 | round light pipes, 3 mm, about 19 mm below their head, for a 3.2 mm hole | The LEDs stand 8.3 mm high: a 25.4 mm pipe cut to 19 mm. |
| 4 | | rubber feet | If you like. |
| 1 | | micro-USB cable, for data (not charge-only) | |

## 3. Soldering

Low parts first, so the board lies flat while you solder: the resistors, the diodes D1 and D2, the ceramic
capacitors, the four IC sockets (their notch as on the board), the two socket strips of the Pico, J7 and J4, the
LEDs (the flat side of the rim, the cathode, as on the board; their tops about 8 mm above the board, where the
light pipes end), SW1 (its keys at the board edge), U5 (its pin 1 at the square pad) and C29 (its stripe away from the "+" on the board). Then the chips
into their sockets: the three 6N137 and the MAX485, each with its notch to the notch of the socket.

Test before the chips go in: with the Pico in its sockets and USB plugged in, about 5 V between U5's pins 7 and 5
(the isolated side: 4.6 to 5.5 V).

Wiring the XLR socket: pin n of J4 is the socket's pin n (pin 1 is "1 COM" on the board): 1 common, 2 Data −,
3 Data +, 4 and 5 free. J4 is on the isolated side: pin 1 is the DMX common, not the box's ground.

## 4. The case

A base and a lid printed in PETG, 70 × 141 × 36 mm closed: box 4's case made for this board, with the Pico's
micro-USB in the front wall and the XLR in the middle of the back wall.
[case_print/dmxbox1_base.stl](case_print/dmxbox1_base.stl) and [case_print/dmxbox1_lid.stl](case_print/dmxbox1_lid.stl),
each already turned as it prints. Pictures: [from the front](3d/dmxbox1_print_front.png),
[from the back](3d/dmxbox1_print_back.png), [opened](3d/dmxbox1_print_open.png). Print it as box 4's case
([box 4's BOM.md](../box4/BOM.md#4-the-case)).

## 5. Putting it together

1. Press the four heat-set inserts into the posts of the base: a soldering iron at about 240 °C, straight down
   until each one is flush, then let it cool.
2. Solder the cable to the XLR socket (pin n to pin n) and screw the socket into the back wall, the nuts inside.
3. Set the board on the posts, SW1 towards the slot in the side and the Pico's micro-USB in the front opening.
   Screw the four standoffs through its holes into the inserts. Plug the cable onto J4.
4. Push the three light pipes into the lid from above, put the lid on and screw it to the standoffs.
5. Set the box number with SW1, through the slot with a pen tip: a key pressed down adds the number printed
   next to it (1, 2, 4 … 128 from the left; go by these, not by any numbers on the switch itself). With the
   number n the box has the address 10.1.n.1 and Art-Net universe n; every box on one computer needs its own
   number.
6. Plug it into a computer. A new Pico has an empty flash, so it starts its USB boot loader and a drive called
   RPI-RP2 appears (otherwise hold the Pico's BOOTSEL button while plugging it in). Run `firmware\build.cmd`
   once (see DEV.md), then `firmware\flash.cmd`: it copies the firmware onto it. Later updates go the same way;
   flash.cmd then asks the box to restart into its boot loader by itself.

## 6. The service pins (J7)

| Pin | Name | What it is for |
|---|---|---|
| 1 | GND | ground, for the pins next to it |
| 2 | RUN | joined to GND for a moment: the box restarts |
| 3 | LOG | what the firmware reports, as text: to the RX of a 3.3 V USB-serial adapter (its GND to pin 1), 250000 baud |

The USB boot loader: the Pico's BOOTSEL button while you plug it in.

## Power

The box asks the computer for 250 mA and draws about 150 mA; any USB port gives that.

## The box on its perfboard

The first box 1, on a perfboard, is in [perfboard/](perfboard/README.md): it only sends (no RDM) and has no switch,
answers at 192.168.7.1 and keeps its first firmware (`firmware/archive/pico`); the desk plays it as before.

## Not built yet

This board is the first revision: nothing of it has been built or measured. It passes KiCad's electrical and
design rule checks, and its nets match the schematic.
