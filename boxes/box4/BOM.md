# dmxbox 4: parts and how to order them

dmxbox 4 (box 4) has one DMX output with RDM, one DMX input (a console plays the desk), MIDI IN and OUT, and a
USB hub: boxes can be chained, computer →
first box → second box → … over USB-C cables. Each box is its own USB network adapter on the computer.
Designators (U1, R3, …) match the [schematic](dmxbox4_schematic.pdf) and the [assembly drawing](dmxbox4_assembly.pdf).

The board is 86 × 82 mm, two layers. JLCPCB makes it and solders every part on it, all on the top side. You make
four cables to the sockets in the box wall.

## 1. The board from JLCPCB

Upload [jlcpcb/dmxbox4_gerbers.zip](jlcpcb/dmxbox4_gerbers.zip) on the PCB quote page. The defaults fit:
2 layers, FR-4, 1.6 mm, any color, HASL lead-free, 5 boards. Then switch on **PCB Assembly**:

* Economic, top side, the number of boards to assemble (2 is the minimum).
* BOM: [jlcpcb/dmxbox4_bom.csv](jlcpcb/dmxbox4_bom.csv); placement: [jlcpcb/dmxbox4_cpl.csv](jlcpcb/dmxbox4_cpl.csv).
* Check the placement preview: JLCPCB's pink pin-1 dot of every chip and transistor must sit at the pin-1 mark of
  the silkscreen, the diodes' "−" at their band, the USB sockets' openings at the board edge, J4 to J7 along their
  pads (either end first is fine) and SW1's keys towards the board edge. KiCad and JLCPCB do not agree on every
  part's rotation: the CPL file turns each part the way JLCPCB's preview of 27 November 2026 showed it right.
  Tick **Confirm Parts Placement** and JLCPCB's engineers check it too.

110 parts on 43 lines, all in stock on 28 November 2026. Eleven are extended parts (about $3 each per order):

| Ref | Part | LCSC |
|---|---|---|
| U1 | Raspberry Pi RP2040 | C2040 |
| U2 | WCH CH334F, USB 2.0 hub | C5187527 |
| Y1, Y2 | Abracon ABM8-272-T3, 12 MHz (the crystal Raspberry Pi names for the RP2040) | C20625731 |
| U9, U11 | ST ST485BDR, RS-485 (MAX485 pins, 0.3 mA): DMX OUT, DMX IN | C91197 |
| J1, J2, J3 | HRO TYPE-C-31-M-12, USB-C sockets | C165948 |
| U5 | EVISUN B0505XT-1WR3, isolated 5 V to 5 V, 1 W, 1.5 kV (Mornsun's package and pins) | C19188495 |
| U6, U7, U8, U12 | Lite-On 6N137S-TA1-L, optocoupler: DMX send, direction, receive; DMX IN | C92651 |
| U10 | Everlight H11L1S(TA), optocoupler for MIDI IN (MIDI senders at 3.3 V give too little current for a 6N137) | C78589 |
| J4, J8, J5, J6 | hanxia SMD pin header 1 × 5, 2.54 mm: DMX OUT, DMX IN, MIDI IN, MIDI OUT | C41417362 |
| J7 | hanxia SMD socket strip (female) 1 × 6, 2.54 mm: the service pins | C42380984 |
| SW1 | kangshen DSPV08LSGET, piano DIP switch with 8 keys: the box number | C3293170 |

Everything else is a basic part: W25Q128JVSIQ flash, XC6206 3.3 V regulator, AO3401A and 2N7002 transistors,
SS34, 1N4148W, PSM712 (protection of the DMX lines), LEDs, resistors and capacitors. For 5 boards: about $63 of
parts and $43 of fees, plus the boards and shipping (`scripts/check_parts.py` asks JLCPCB for today's numbers).

## 2. What you buy and fit

| Qty | For | Part | Notes |
|---|---|---|---|
| 1 | DMX OUT | XLR 5-pin **female** panel socket XLR-5G-C | In the back wall. |
| 1 | DMX IN | XLR 5-pin **male** panel socket XLR-5W-C | In the back wall, beside DMX OUT. |
| 2 | MIDI IN, MIDI OUT | REAN NYS325, DIN 5-pin 180° female | In the back wall. |
| 4 | J4, J8, J5, J6 | 5 wires with a 1 × 5 female housing, 2.54 mm (Dupont) | From the pin headers on the board to the sockets. |
| 1 | J7 | jumper wire, male to male | For BOOT and RUN, see [below](#6-the-service-pins-j7). |
| | | the printed case | See [section 4](#4-the-case): about 100 g of PETG. |
| 4 | | M3 heat-set inserts, 5.7 mm long, 4.6 mm wide (for example Ruthex M3 × 5.7) | In the case's four posts. |
| 4 | H1 to H4 | M3 hex standoffs, male-female, 25 mm | Through the board into the inserts; the lid sits on them. |
| 4 | | M3 countersunk screws, 6 mm (ISO 10642) | The lid, into the standoffs. |
| 4 | | M3 screws, 10 mm, and nuts | The two XLRs. |
| 4 | | M3 screws, 8 mm, and nuts | The two DINs. |
| 3 | D4, D5, D6 | round light pipes, 3 mm, 25.4 mm (1 inch) below their head, for a 3.2 mm hole | Pushed into the lid, over the LEDs. |
| 4 | | rubber feet | If you like. |

## 3. Wiring the sockets

Pin n of the header is the socket's pin n on all four (pin 1 is at the "1" on the board):

| Header | Socket | Pin 1 | Pin 2 | Pin 3 | Pin 4 | Pin 5 |
|---|---|---|---|---|---|---|
| J4 | DMX OUT (XLR) | common | Data − | Data + | free | free |
| J8 | DMX IN (XLR, male) | common | Data − | Data + | free | free |
| J5 | MIDI IN (DIN) | free | free (MIDI IN leaves its shield open) | free | current in | current out |
| J6 | MIDI OUT (DIN) | free | shield (ground) | free | current out | current in |

J4 and J8 are on the isolated side: pin 1 is the DMX common, not the box's ground.

## 4. The case

A base and a lid printed in PETG, 123 × 127 × 36 mm closed, wider than the board for the four sockets in a row
(seen from behind: MIDI IN, MIDI OUT, DMX IN, DMX OUT):
[case_print/dmxbox4_base.stl](case_print/dmxbox4_base.stl) and [case_print/dmxbox4_lid.stl](case_print/dmxbox4_lid.stl),
each already turned as it prints (the base standing on its floor, the lid on its top). Pictures:
[from the front](3d/dmxbox4_print_front.png), [from the back](3d/dmxbox4_print_back.png),
[opened](3d/dmxbox4_print_open.png). `scripts/make_print_case.py` makes them from the board's layout;
`case_print/dmxbox4_case_print.step` has both parts for changes in a CAD program.

Printing (Bambu Lab P1S, 0.4 mm nozzle, Bambu Studio):

* Filament: your PETG profile. Plate: textured PEI; on a smooth one, glue stick first (PETG holds too hard).
* Layer height 0.2 mm, 5 walls, 5 top and 5 bottom layers, 20 % gyroid infill. No supports and no brim: the
  round holes in the back wall have a flat top that bridges, under the sockets' flanges.
* The labels are 0.6 mm deep. For them in a second color (AMS): load `dmxbox4_base.stl` together with
  `dmxbox4_base_text.stl`, answer "Yes" to loading them as one object of several parts, and give the text part
  the other filament. The same for the lid.

The holes come out a little small when printed; they are drawn 0.1 to 0.3 mm larger. If a socket or a light
pipe is still tight, a drill or a small file by hand is enough.

The case does not conduct, so the DMX side's isolation cannot be bridged through it, and any screws will do.

The aluminium case of [section 7](#7-the-aluminium-case-instead) is the other way, with the same board.

## 5. Putting it together

1. Press the four heat-set inserts into the posts of the base: a soldering iron at about 240 °C, straight down
   until each one is flush, then let it cool.
2. Solder the four cables to the XLR and DIN sockets (pin n to pin n) and screw the sockets into the back wall,
   the nuts inside.
3. Set the board on the posts, SW1 towards the slot in the side and the USB-C sockets in the front openings.
   Screw the four standoffs through its holes into the inserts. Plug the cables onto J4, J8, J5 and J6.
4. Push the three light pipes into the lid from above, put the lid on (its lip goes inside the walls) and screw
   it to the standoffs.
5. Set the box number with SW1, through the slot with a pen tip: a key pressed down adds the number printed
   next to it (1, 2, 4 … 128; go by these, not by any numbers on the switch itself). With the number n the box
   has the address 10.1.n.1 and Art-Net universe n; every box on one computer needs its own number.
6. Plug IN into a computer. A new box has an empty flash, so the RP2040 starts its USB boot loader and a drive
   called RPI-RP2 appears. Run `firmware\build.cmd` once (see DEV.md), then `firmware\flash.cmd`: it copies the
   firmware onto it. Later updates go the same way; flash.cmd then asks the box to restart into its boot loader
   by itself.

## 6. The service pins (J7)

Normally nothing is plugged in here (the case has to come apart for it). The names are on the board, above
the pins:

| Pin | Name | What it is for |
|---|---|---|
| 1 | GND | ground, for the pins next to it |
| 2 | BOOT | joined to GND while you plug the box in: the USB boot loader starts (drive RPI-RP2), whatever firmware is on the box. A male-male jumper wire from GND to BOOT does it; take it out afterwards. |
| 3 | RUN | joined to GND for a moment (the same wire): the box restarts |
| 4 | SWCLK | debugging with a probe (for example Raspberry Pi's Debug Probe): never to GND |
| 5 | SWDIO | the same |
| 6 | LOG | what the firmware reports, as text: to the RX of a 3.3 V USB-serial adapter (its GND to pin 1), 250000 baud |

## 7. The aluminium case instead

JLCMC (JLCPCB's mechanical shop) sells the aluminium case K119-12063-H7-L130 (120 × 63 × 130 mm, split into a
top and a bottom half, end plates of 1.5 mm) and machines holes into it: order it with
[case/dmxbox4_case.step](case/dmxbox4_case.step) and its drawing [case/dmxbox4_case.pdf](case/dmxbox4_case.pdf)
(same name, as JLCMC asks). Pictures: [from the front](3d/dmxbox4_case_front.png),
[from the back](3d/dmxbox4_case_back.png), [opened](3d/dmxbox4_case_open.png); `scripts/make_case.py` makes them.

The board stands in the bottom half on four M3 nylon hex spacers of 15 mm (female-female), with M3 countersunk
screws of 8 mm from below and M3 **nylon** screws through the board. Two spacers stand where ribs run along the
floor; JLCMC mills the ribs away there (P1 and P2 in the drawing). The XLRs stand upright in the back plate,
their latch at the top. Light pipes of about 43 mm bring the LEDs to the top half. The case conducts: the XLR
sockets' housings must not be joined to pin 1 (check with a meter), or the isolation is bridged through the case.

## Power

One USB 2.0 port gives 500 mA, enough for about two boxes. A 5 V charger on POWER (J3) feeds that box and the
boxes after it, and the box then takes nothing from IN. OUT gets 5 V only while a box is plugged in there.

## Not built yet

This is the first revision: nothing of it has been built or measured. The board passes KiCad's electrical and
design rule checks, and its nets match the schematic.
