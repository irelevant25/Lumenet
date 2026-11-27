# dmxbox 2: parts list

Everything needed to build one dmxbox 2: the box with a PIC32 microcontroller and **two DMX outputs**, powered
and controlled over USB-C. Designators (U1, R3, ...) match the [schematic](schematic-2-universes/dmxbox2_schematic.png),
the [breadboard plan](schematic-2-universes/dmxbox2_breadboard.png) and the [perfboard plan](schematic-2-universes/dmxbox2_perfboard.png).

## Chips

| Qty | Ref | Part | What to look for |
|---|---|---|---|
| 1 | U1 | Microchip PIC32MX270F256B-I/SP | DIP-28, 7.62 mm wide. **The "2" in MX2 matters:** PIC32MX1xx chips look the same but have no USB. The -50I/SP (50 MHz) works too; the firmware runs it at 40 MHz. |
| 1 | U2 | Microchip MCP1700-3302E/TO | 3.3 V regulator in TO-92 (legs: 1 ground, 2 in, 3 out). It feeds U1 and the LEDs of the send optocouplers, about 70 mA. |
| 1 | U3 | Recom RKK-0505S/H | Isolated DC-DC converter, 5 V to 5 V, 1 W. Seven pin positions, four pins: 1 = input plus, 2 = input minus, 5 = output minus, 7 = output plus. The same part as on the Pico box. |
| 4 | U4, U5, U8, U9 | 6N137 | Fast optocoupler, DIP-8, any maker. Two per output: one sends, one receives (for RDM later). |
| 2 | U6, U10 | PC817 | Optocoupler, DIP-4. It switches the line driver between sending and receiving. |
| 2 | U7, U11 | MAX485 (MAX485CPA+ or MAX485EPA+) | RS-485 transceiver, DIP-8. **Not the SN75176BP here:** it draws about 40 mA more per output, and with two outputs that is more than the 1 W converter gives. |

## Sockets and headers

| Qty | For | Part | What to look for |
|---|---|---|---|
| 1 | U1 | IC socket DIP-28 | 7.62 mm row spacing (the narrow kind, "300 mil") |
| 6 | U4, U5, U7, U8, U9, U11 | IC socket DIP-8 | 7.62 mm row spacing |
| 2 | U6, U10 | IC socket DIP-4 | If you cannot get one, use two 2-pin pieces cut from a turned-pin socket strip. |
| 1 | U3 | Female pin header 1 × 7 | 2.54 mm pitch. Pull the contacts at positions 3 and 4 out of the strip before soldering it: they would stand in the isolation gap. |
| 1 | J2 | Male pin header 1 × 6 | 2.54 mm pitch, straight. The programmer plugs onto it. |

## Resistors, capacitors, crystal, diodes

All resistors are axial, 0.25 W, carbon or metal film.

| Qty | Ref | Value | Job |
|---|---|---|---|
| 2 | R1, R2 | 5.1 kΩ | Tell a USB-C charger or computer that the box is a device. **Only if your USB-C breakout has no 5.1 kΩ resistors of its own** (most say so in their description). |
| 1 | R3 | 10 kΩ | Pull-up on the PIC's reset pin (MCLR) |
| 1 | R4 | 1 kΩ | Keeps C9 away from the programmer |
| 1 | R5 | 470 Ω | The LED D1 |
| 2 | R6, R15 | 180 Ω | LED current of the send optocouplers (about 10 mA) |
| 4 | R7, R9, R16, R18 | 1 kΩ | Pull-ups on the optocoupler outputs |
| 2 | R8, R17 | 390 Ω | LED current of the receive optocouplers |
| 2 | R10, R19 | 470 Ω | LED current of the direction optocouplers |
| 2 | R11, R20 | 10 kΩ | Keep the line drivers sending while the direction optocoupler is off |
| 4 | R12, R14, R21, R23 | 560 Ω | Line bias: the line rests in a defined state when nobody sends (needed for RDM) |
| 2 | R13, R22 | 130 Ω | Line termination at the box's end, together with the bias resistors |
| 5 | C1, C2, C6, C10, C11 | 10 µF | Ceramic, X7R, 16 V or more, radial, 5 mm lead spacing. C6 is the PIC's own core supply (VCAP): it must be a ceramic one. |
| 10 | C3, C4, C5, C9, C12 to C17 | 100 nF | Ceramic, X7R, 50 V, 5 mm lead spacing |
| 2 | C7, C8 | 22 pF | Ceramic, C0G (NP0), 5 mm lead spacing, for the crystal |
| 1 | Y1 | 8 MHz crystal | HC-49/US (the low case), 18 or 20 pF load capacitance |
| 1 | D1 | LED 3 mm | Any color; the longer leg is the plus leg (anode) |
| 4 | D2 to D5 | P6KE6.8CA | Protection diode (TVS), DO-15. The "CA" matters: it works in both directions. |

## Switches and connectors

| Qty | Ref | Part | What to look for |
|---|---|---|---|
| 1 | SW1 | Piano DIP switch with long keys, 8 positions: ONPOW DPL08RGT | Switches 1 to 6 give the box its number (see below); 7 and 8 are spare. Through-hole, the DIP-16 layout, its keys sticking out sideways. It sits at the left edge of the board, which is cut off right beside it, so that its keys reach into the box's wall and the number can be set from outside. Solder it straight into the board. |
| 2 | SW2, SW3 | Tactile push button 12 × 12 mm | Through-hole, 4 legs. SW2 is blackout, SW3 recalls preset 1. |
| 1 | J1 | USB-C socket on a small breakout board | With USB 2.0 data: six pins in one row, 2.54 mm pitch, marked VBUS, GND, D+, D−, CC1 and CC2. Breakouts with only VBUS and GND (power only) do not work. The perfboard plan assumes the order VBUS, D−, D+, CC1, CC2, GND; if yours differs, follow the marks on the breakout and change the plan. |
| 2 | J3, J4 | XLR 3-pin **female** chassis jack | D-size flange with solder cups (e.g. Neutrik NC3FD-L-1) |
| 2 | J3, J4 (on the board) | Screw terminal block, 3 poles | 5.08 mm pitch |

## Board, tools and small parts

| Qty | Part | Notes |
|---|---|---|
| 1 | Perfboard 90 × 150 mm | At least 33 × 57 holes, 2.54 mm pitch, **every hole with a pad of its own** (not strips). The box is too big for the 70 × 90 mm board of the Pico box. Its left edge is cut off 2 mm from the middle of column A's holes, and B-1 and B-57 are drilled out to 3.2 mm (see the perfboard plan). |
| 4 | M3 screws, about 6 mm | They hold the board on the posts of the box: two in B-1 and B-57, two in the board's right corners |
| 1 | Programmer: Microchip MPLAB Snap (PG164100), PICkit 4 or PICkit 5 | **Needed once**, to put the firmware onto the new PIC: unlike the Pico, it has no USB drive to copy the firmware to. It plugs onto J2. The free MPLAB IPE writes the file. |
| | Bare tinned wire, about 0.6 mm | For the tracks on the solder side |
| | Insulated wire, about 0.6 mm (about 1.5 m) | For the insulated wires on the parts side (W1, W2 ... in the perfboard plan: 38 with two outputs, 26 with one) |
| 6 | Hook-up wire, about 10 cm each | Three colors, from the screw terminals to the two XLR jacks |
| 1 | USB-C cable | It powers the box and carries the data, so it must be a **data cable**, not a charge-only one. The box draws about 0.25 A and asks the computer for 0.4 A. |
| 2 | DMX terminator (optional) | XLR 3-pin male plug with a 120 Ω resistor between pins 2 and 3, for the last fixture of each line |

## The box number

Switches 1 to 6 of SW1 give the box its number n, 0 to 63: switch 1 counts 1, switch 2 counts 2, switch 3
counts 4, and so on to switch 6, which counts 32 (ON counts). Box n is 10.1.n.1 on its USB network and plays
Art-Net universes 2n and 2n + 1 (the box with one output plays universe n). With all switches OFF the box is
number 0. Give each box of a show its own number. The box reads the switches when it starts: unplug it and
plug it in again after changing them.

## The box with one output

Built after `schematic-1-universe/`, it leaves out everything of output 2: **U8, U9, U10, U11, R15 to R23,
C15 to C17, D4, D5 and J4**, with their sockets (three DIP-8 and one DIP-4), one screw terminal and one XLR
jack. Everything else is the same. Its firmware is built with `build.cmd 1`.

## Count check

On the board: 10 socketed chips (U1, U4 to U11, and U3 in its strip), U2, 23 resistors, 17 capacitors, the
crystal, 5 diodes, 3 switches, the USB-C breakout, the programming header and 2 screw terminals. Off the
board: the two XLR jacks on three wires each. With one output: 6 socketed chips, U2, 14 resistors, 14
capacitors, the crystal, 3 diodes, 3 switches, the breakout, the header, 1 screw terminal and 1 XLR jack.
