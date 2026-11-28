# dmxbox 2 on two breadboards: the lists

Written by `boxes/box2/perfboard/schematic-2-universes/breadboard.py`. Do not change it by hand: change the plan in the script and run it again.

Board B (the two DMX outputs) lies above board A (the microcontroller), row 1 of both at the left. `A: e30` is hole e30 of board A; a power line is named by its color and the row a leg or wire goes in.

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
| C15 | 100 nF | 1 B: h24, 2 B: h27 |
| C16 | 100 nF | 1 B: b19, 2 B: b16 |
| C17 | 100 nF | 1 B: h52, 2 B: h55 |
| D1 | LED 3 mm | A A: a48, K A: bottom blue line (49) |
| D2 | P6KE6.8CA | 1 B: i42, 2 B: top blue line (43) |
| D3 | P6KE6.8CA | 1 B: i41, 2 B: top blue line (40) |
| D4 | P6KE6.8CA | 1 B: i54, 2 B: top blue line (55) |
| D5 | P6KE6.8CA | 1 B: i53, 2 B: top blue line (52) |
| J1 | USB 2.0 Type-C breakout | CC1 A: j49, CC2 A: j50, D+ A: j48, D- A: j47, GND A: j51, VBUS A: j46 |
| J2 | pin header 1x6 | 1 A: a20, 2 A: a21, 3 A: a22, 4 A: a23, 5 A: a24, 6 A: a25 |
| J3 | XLR 3-pin female, panel | 1 B: top blue line (45), 2 B: j41, 3 B: j42 |
| J4 | XLR 3-pin female, panel | 1 B: top blue line (57), 2 B: j53, 3 B: j54 |
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
| R15 | 180 Ω | 1 A: bottom red line (53), 2 A: b53 |
| R16 | 1 kΩ | 1 B: top red line (25), 2 B: j26 |
| R17 | 390 Ω | 1 B: top red line (18), 2 B: j18 |
| R18 | 1 kΩ | 1 A: i35, 2 A: i41 |
| R19 | 470 Ω | 1 A: i42, 2 A: i45 |
| R20 | 10 kΩ | 1 B: top red line (21), 2 B: j21 |
| R21 | 560 Ω | 1 B: top red line (54), 2 B: h54 |
| R22 | 130 Ω | 1 B: g54, 2 B: g53 |
| R23 | 560 Ω | 1 B: h53, 2 B: top blue line (53) |
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
| U8 | 6N137 | 1 B: e24, 2 B: e25, 3 B: e26, 4 B: e27, 5 B: f27, 6 B: f26, 7 B: f25, 8 B: f24 |
| U9 | 6N137 | 1 B: f19, 2 B: f18, 3 B: f17, 4 B: f16, 5 B: e16, 6 B: e17, 7 B: e18, 8 B: e19 |
| U10 | PC817 | 1 B: e21, 2 B: e22, 3 B: f22, 4 B: f21 |
| U11 | MAX485 | 1 B: e52, 2 B: e53, 3 B: e54, 4 B: e55, 5 B: f55, 6 B: f54, 7 B: f53, 8 B: f52 |
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
| A20 | A: i30 | A: j35 | +3V3 |
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
| B12 | B: a16 | B: bottom blue line (16) | GND |
| B13 | B: g17 | B: d52 | RO2 |
| B14 | B: c18 | B: c19 | +5V |
| B15 | B: a18 | B: bottom red line (18) | +5V |
| B16 | B: g21 | B: d53 | DE2 |
| B17 | B: top blue line (22) | B: j22 | GND2 |
| B18 | B: a22 | B: bottom blue line (22) | GND |
| B19 | B: top red line (24) | B: j24 | +5V_ISO |
| B20 | B: g24 | B: g25 | +5V_ISO |
| B21 | B: g26 | B: d55 | DI2 |
| B22 | B: top blue line (27) | B: j27 | GND2 |
| B23 | B: a28 | B: bottom red line (28) | +5V |
| B24 | B: a29 | B: bottom blue line (29) | GND |
| B25 | B: top blue line (31) | B: d32 | GND2 |
| B26 | B: top red line (34) | B: d34 | +5V_ISO |
| B27 | B: top red line (40) | B: j40 | +5V_ISO |
| B28 | B: c41 | B: c42 | DE1 |
| B29 | B: top blue line (42) | B: j43 | GND2 |
| B30 | B: top red line (52) | B: j52 | +5V_ISO |
| B31 | B: c53 | B: c54 | DE2 |
| B32 | B: top blue line (54) | B: j55 | GND2 |
| X1 | A: i12 | B: bottom blue line (12) | GND |
| X2 | A: top red line (18) | B: b18 | +5V |
| X3 | A: j28 | B: a7 | DIR1_LED |
| X4 | A: j32 | B: a12 | TX1 |
| X5 | A: j34 | B: a3 | RX1 |
| X6 | A: j40 | B: a26 | TX2 |
| X7 | A: j41 | B: a17 | RX2 |
| X8 | A: j45 | B: a21 | DIR2_LED |
| X9 | A: e51 | B: a11 | TX1_LED |
| X10 | A: e53 | B: a25 | TX2_LED |
