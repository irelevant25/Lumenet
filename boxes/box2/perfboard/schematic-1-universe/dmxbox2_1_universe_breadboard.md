# dmxbox 2 with one output, on two breadboards: the lists

Written by `boxes/box2/perfboard/schematic-1-universe/breadboard.py`. Do not change it by hand: change the plan in the script and run it again.

Board B (the DMX output) lies above board A (the microcontroller), row 1 of both at the left. `A: e30` is hole e30 of board A; a power line is named by its color and the row a leg or wire goes in.

## Parts

| Part | What | Holes |
|---|---|---|
| C1 | 10 µF | 1 A: top red line (43), 2 A: top blue line (43) |
| C2 | 10 µF | 1 A: bottom red line (5), 2 A: bottom blue line (5) |
| C3 | 100 nF | 1 A: c42, 2 A: bottom blue line (43) |
| C4 | 100 nF | 1 A: g30, 2 A: g31 |
| C5 | 100 nF | 1 A: h35, 2 A: top blue line (35) |
| C6 | 10 µF | 1 A: h38, 2 A: top blue line (37) |
| C7 | 22 pF | 1 A: b38, 2 A: bottom blue line (37) |
| C8 | 22 pF | 1 A: b39, 2 A: bottom blue line (39) |
| C9 | 100 nF | 1 A: b27, 2 A: bottom blue line (28) |
| C10 | 10 µF | 1 B: bottom red line (27), 2 B: bottom blue line (27) |
| C11 | 10 µF | 1 B: top red line (37), 2 B: top blue line (37) |
| C12 | 100 nF | 1 B: h10, 2 B: h13 |
| C13 | 100 nF | 1 B: b5, 2 B: b2 |
| C14 | 100 nF | 1 B: h40, 2 B: h43 |
| D1 | LED 3 mm | A A: a48, K A: bottom blue line (49) |
| D2 | P6KE6.8CA | 1 B: i42, 2 B: top blue line (43) |
| D3 | P6KE6.8CA | 1 B: i41, 2 B: top blue line (40) |
| J1 | USB 2.0 Type-C breakout | CC1 A: j49, CC2 A: j50, D+ A: j48, D- A: j47, GND A: j51, VBUS A: j46 |
| J2 | pin header 1x6 | 1 A: a20, 2 A: a21, 3 A: a22, 4 A: a23, 5 A: a24, 6 A: a25 |
| J3 | XLR 3-pin female, panel | 1 B: top blue line (45), 2 B: j41, 3 B: j42 |
| R1 | 5.1 kΩ | 1 A: i49, 2 A: top blue line (53) |
| R2 | 5.1 kΩ | 1 A: g50, 2 A: top blue line (54) |
| R3 | 10 kΩ | 1 A: bottom red line (27), 2 A: c27 |
| R4 | 1 kΩ | 1 A: d27, 2 A: d30 |
| R5 | 470 Ω | 1 A: c43, 2 A: c48 |
| R6 | 180 Ω | 1 A: bottom red line (51), 2 A: b51 |
| R7 | 1 kΩ | 1 B: top red line (12), 2 B: j12 |
| R8 | 390 Ω | 1 B: top red line (4), 2 B: j4 |
| R9 | 1 kΩ | 1 A: h30, 2 A: h34 |
| R10 | 470 Ω | 1 A: i33, 2 A: i28 |
| R11 | 10 kΩ | 1 B: top red line (7), 2 B: j7 |
| R12 | 560 Ω | 1 B: top red line (42), 2 B: h42 |
| R13 | 130 Ω | 1 B: g42, 2 B: g41 |
| R14 | 560 Ω | 1 B: h41, 2 B: top blue line (41) |
| SW1 | piano DIP switch, 8 positions (DPL08RGT) | 1 A: f17, 10 A: e11, 11 A: e12, 12 A: e13, 13 A: e14, 14 A: e15, 15 A: e16, 16 A: e17, 2 A: f16, 3 A: f15, 4 A: f14, 5 A: f13, 6 A: f12, 7 A: f11, 8 A: f10, 9 A: e10 |
| SW2 | tact switch 12 x 12 mm | a A: g56, b A: d58, gnd A: g58, net A: d56 |
| SW3 | tact switch 12 x 12 mm | a A: g60, b A: d62, gnd A: g62, net A: d60 |
| U1 | PIC32MX270F256B-I/SP | 1 A: e30, 10 A: e39, 11 A: e40, 12 A: e41, 13 A: e42, 14 A: e43, 15 A: f43, 16 A: f42, 17 A: f41, 18 A: f40, 19 A: f39, 2 A: e31, 20 A: f38, 21 A: f37, 22 A: f36, 23 A: f35, 24 A: f34, 25 A: f33, 26 A: f32, 27 A: f31, 28 A: f30, 3 A: e32, 4 A: e33, 5 A: e34, 6 A: e35, 7 A: e36, 8 A: e37, 9 A: e38 |
| U2 | MCP1700-3302E/TO | 1 A: h52, 2 A: h53, 3 A: h54 |
| U3 | RKK-0505S/H | 1 B: e28, 2 B: e29, 5 B: e32, 7 B: e34 |
| U4 | 6N137 | 1 B: e10, 2 B: e11, 3 B: e12, 4 B: e13, 5 B: f13, 6 B: f12, 7 B: f11, 8 B: f10 |
| U5 | 6N137 | 1 B: f5, 2 B: f4, 3 B: f3, 4 B: f2, 5 B: e2, 6 B: e3, 7 B: e4, 8 B: e5 |
| U6 | PC817 | 1 B: e7, 2 B: e8, 3 B: f8, 4 B: f7 |
| U7 | MAX485 | 1 B: e40, 2 B: e41, 3 B: e42, 4 B: e43, 5 B: f43, 6 B: f42, 7 B: f41, 8 B: f40 |
| Y1 | 8 MHz | 1 A: a38, 2 A: a39 |

## Wires

| Wire | From | To | Net |
|---|---|---|---|
| A1 | A: top blue line (12) | A: j12 | GND |
| A2 | A: h12 | A: h13 | GND |
| A3 | A: c12 | A: c41 | N32 |
| A4 | A: g13 | A: g14 | GND |
| A5 | A: c13 | A: c40 | N16 |
| A6 | A: h14 | A: h15 | GND |
| A7 | A: c14 | A: c36 | N8 |
| A8 | A: g15 | A: g16 | GND |
| A9 | A: c15 | A: c35 | N4 |
| A10 | A: h16 | A: h17 | GND |
| A11 | A: c16 | A: c32 | N2 |
| A12 | A: c17 | A: c31 | N1 |
| A13 | A: c20 | A: c30 | MCLR |
| A14 | A: b21 | A: bottom red line (21) | +3V3 |
| A15 | A: top blue line (22) | A: e22 | GND |
| A16 | A: b22 | A: bottom blue line (22) | GND |
| A17 | A: c23 | A: c33 | BLACKOUT |
| A18 | A: c24 | A: c34 | PRESET |
| A19 | A: j30 | A: bottom red line (30) | +3V3 |
| A20 | A: i30 | A: i35 | +3V3 |
| A21 | A: top blue line (31) | A: j31 | GND |
| A22 | A: d33 | A: c56 | BLACKOUT |
| A23 | A: d34 | A: c60 | PRESET |
| A24 | A: h36 | A: h47 | D- |
| A25 | A: bottom blue line (36) | A: a37 | GND |
| A26 | A: h37 | A: h48 | D+ |
| A27 | A: top blue line (39) | A: j39 | GND |
| A28 | A: top red line (42) | A: j43 | +5V |
| A29 | A: a42 | A: bottom red line (42) | +3V3 |
| A30 | A: h43 | A: h46 | +5V |
| A31 | A: i51 | A: top blue line (55) | GND |
| A32 | A: h51 | A: g52 | GND |
| A33 | A: top red line (53) | A: j53 | +5V |
| A34 | A: f54 | A: bottom red line (54) | +3V3 |
| A35 | A: top blue line (58) | A: j58 | GND |
| A36 | A: top blue line (61) | A: j62 | GND |
| B1 | B: a2 | B: bottom blue line (3) | GND |
| B2 | B: g3 | B: d40 | RO1 |
| B3 | B: c4 | B: c5 | +5V |
| B4 | B: a4 | B: bottom red line (4) | +5V |
| B5 | B: top blue line (7) | B: j8 | GND2 |
| B6 | B: g7 | B: d41 | DE1 |
| B7 | B: bottom blue line (7) | B: a8 | GND |
| B8 | B: top red line (10) | B: j10 | +5V_ISO |
| B9 | B: g10 | B: g11 | +5V_ISO |
| B10 | B: g12 | B: d43 | DI1 |
| B11 | B: top blue line (13) | B: j13 | GND2 |
| B12 | B: a28 | B: bottom red line (28) | +5V |
| B13 | B: a29 | B: bottom blue line (29) | GND |
| B14 | B: top blue line (31) | B: d32 | GND2 |
| B15 | B: top red line (34) | B: d34 | +5V_ISO |
| B16 | B: top red line (40) | B: j40 | +5V_ISO |
| B17 | B: c41 | B: c42 | DE1 |
| B18 | B: top blue line (42) | B: j43 | GND2 |
| X1 | A: i12 | B: bottom blue line (12) | GND |
| X2 | A: top red line (28) | B: b28 | +5V |
| X3 | A: j28 | B: a7 | DIR1_LED |
| X4 | A: j32 | B: a12 | TX1 |
| X5 | A: j34 | B: a3 | RX1 |
| X6 | A: e51 | B: a11 | TX1_LED |
