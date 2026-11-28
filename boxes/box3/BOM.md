# dmxbox 3: parts and how to order them

dmxbox 3 (box 3) has one DMX output with RDM and one USB-C socket: box 4 without the hub, the OUT and POWER
sockets and MIDI. It is a USB network adapter on the computer. Designators (U1, R14, …) match the
[schematic](dmxbox3_schematic.pdf) and the [assembly drawing](dmxbox3_assembly.pdf); they are box 4's names for
the same parts, so there are gaps in the numbers.

The board is 86 × 66 mm, two layers. JLCPCB makes it and solders every part on it, all on the top side. You make
one cable to the XLR socket in the box wall.

## 1. The board from JLCPCB

Upload [jlcpcb/dmxbox3_gerbers.zip](jlcpcb/dmxbox3_gerbers.zip) on the PCB quote page. The defaults fit:
2 layers, FR-4, 1.6 mm, any color, HASL lead-free, 5 boards. Then switch on **PCB Assembly**:

* Economic, top side, the number of boards to assemble (2 is the minimum).
* BOM: [jlcpcb/dmxbox3_bom.csv](jlcpcb/dmxbox3_bom.csv); placement: [jlcpcb/dmxbox3_cpl.csv](jlcpcb/dmxbox3_cpl.csv).
* Check the placement preview: JLCPCB's pink pin-1 dot of every chip and transistor must sit at the pin-1 mark of
  the silkscreen, the diode's "−" at its band, the USB socket's opening at the board edge, J4 and J7 along their
  pads (either end first is fine) and SW1's keys towards the board edge. The CPL file turns each part the way
  JLCPCB's preview showed box 4's same parts right on 27 November 2026. Tick **Confirm Parts Placement** and
  JLCPCB's engineers check it too.

65 parts on 30 lines, all in stock on 28 November 2026. Nine are extended parts (about $3 each per order):

| Ref | Part | LCSC |
|---|---|---|
| U1 | Raspberry Pi RP2040 | C2040 |
| Y1 | Abracon ABM8-272-T3, 12 MHz (the crystal Raspberry Pi names for the RP2040) | C20625731 |
| U9 | ST ST485BDR, RS-485 (MAX485 pins, 0.3 mA) | C91197 |
| J1 | HRO TYPE-C-31-M-12, USB-C socket | C165948 |
| U5 | EVISUN B0505XT-1WR3, isolated 5 V to 5 V, 1 W, 1.5 kV (Mornsun's package and pins) | C19188495 |
| U6, U7, U8 | Lite-On 6N137S-TA1-L, optocoupler: DMX send, direction, receive | C92651 |
| J4 | hanxia SMD pin header 1 × 5, 2.54 mm: DMX OUT | C41417362 |
| J7 | hanxia SMD socket strip (female) 1 × 6, 2.54 mm: the service pins | C42380984 |
| SW1 | kangshen DSPV08LSGET, piano DIP switch with 8 keys: the box number | C3293170 |

Everything else is a basic part: W25Q128JVSIQ flash, XC6206 3.3 V regulator, PSM712 (protection of the DMX
line), LEDs, resistors and capacitors. For 5 boards: about $45 of parts and $37 of fees, plus the boards and
shipping (`scripts/check_parts.py` asks JLCPCB for today's numbers).

## 2. What you buy and fit

| Qty | For | Part | Notes |
|---|---|---|---|
| 1 | DMX OUT | XLR 5-pin **female** panel socket XLR-5G-C | In the back wall. |
| 1 | J4 | 5 wires with a 1 × 5 female housing, 2.54 mm (Dupont) | From the pin header on the board to the socket. |
| 1 | J7 | jumper wire, male to male | For BOOT and RUN, see [below](#6-the-service-pins-j7). |
| | | the printed case | See [section 4](#4-the-case): about 65 g of PETG. |
| 4 | | M3 heat-set inserts, 5.7 mm long, 4.6 mm wide (for example Ruthex M3 × 5.7) | In the case's four posts. |
| 4 | H1 to H4 | M3 hex standoffs, male-female, 25 mm | Through the board into the inserts; the lid sits on them. |
| 4 | | M3 countersunk screws, 6 mm (ISO 10642) | The lid, into the standoffs. |
| 2 | | M3 screws, 10 mm, and nuts | The XLR. |
| 3 | D4, D5, D6 | round light pipes, 3 mm, 25.4 mm (1 inch) below their head, for a 3.2 mm hole | Pushed into the lid, over the LEDs. |
| 4 | | rubber feet | If you like. |

## 3. Wiring the socket

Pin n of the header is the socket's pin n (pin 1 is at "1 COM" on the board):

| Header | Socket | Pin 1 | Pin 2 | Pin 3 | Pin 4 | Pin 5 |
|---|---|---|---|---|---|---|
| J4 | DMX OUT (XLR) | common | Data − | Data + | free | free |

J4 is on the isolated side: pin 1 is the DMX common, not the box's ground.

## 4. The case

A base and a lid printed in PETG, 74 × 127 × 36 mm closed: box 4's case, narrower, with one USB-C opening and the
XLR in the middle of the back wall. [case_print/dmxbox3_base.stl](case_print/dmxbox3_base.stl) and
[case_print/dmxbox3_lid.stl](case_print/dmxbox3_lid.stl), each already turned as it prints (the base standing on
its floor, the lid on its top). Pictures: [from the front](3d/dmxbox3_print_front.png),
[from the back](3d/dmxbox3_print_back.png), [opened](3d/dmxbox3_print_open.png). `scripts/make_print_case.py`
makes them from the board's layout; `case_print/dmxbox3_case_print.step` has both parts for changes in a CAD
program.

Print it as box 4's case ([box 4's BOM.md](../box4/BOM.md#4-the-case)): PETG, 0.2 mm layers, 5 walls, 5 top and
5 bottom layers, 20 % gyroid, no supports and no brim; the labels in a second color by loading
`dmxbox3_base.stl` with `dmxbox3_base_text.stl` as one object of several parts (the same for the lid).

## 5. Putting it together

1. Press the four heat-set inserts into the posts of the base: a soldering iron at about 240 °C, straight down
   until each one is flush, then let it cool.
2. Solder the cable to the XLR socket (pin n to pin n) and screw the socket into the back wall, the nuts inside.
3. Set the board on the posts, SW1 towards the slot in the side and the USB-C socket in the front opening. Screw
   the four standoffs through its holes into the inserts. Plug the cable onto J4.
4. Push the three light pipes into the lid from above, put the lid on (its lip goes inside the walls) and screw
   it to the standoffs.
5. Set the box number with SW1, through the slot with a pen tip: a key pressed down adds the number printed
   next to it (1, 2, 4 … 128; go by these, not by any numbers on the switch itself). With the number n the box
   has the address 10.1.n.1 and Art-Net universe n; every box on one computer needs its own number.
6. Plug it into a computer. A new box has an empty flash, so the RP2040 starts its USB boot loader and a drive
   called RPI-RP2 appears. Run `firmware\build.cmd` once (see DEV.md), then `firmware\flash.cmd`: it copies the
   firmware onto it. Later updates go the same way; flash.cmd then asks the box to restart into its boot loader
   by itself.

## 6. The service pins (J7)

As on box 4 ([its BOM.md](../box4/BOM.md#6-the-service-pins-j7)): GND, BOOT (to GND while plugging in: the USB
boot loader), RUN (to GND: a restart), SWCLK and SWDIO (a debug probe), LOG (the firmware's report at
250000 baud). The names are on the board, above the pins.

## Power

The box asks the computer for 250 mA and draws about 150 to 200 mA; any USB port gives that.

## Not built yet

This is the first revision: nothing of it has been built or measured. The board passes KiCad's electrical and
design rule checks, and its nets match the schematic. Its circuit is box 4's, which is not built yet either.
