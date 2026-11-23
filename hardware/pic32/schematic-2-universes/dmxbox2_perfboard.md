# dmxbox 2 on a perfboard: the lists

Written by `hardware/pic32/schematic-2-universes/perfboard.py`. Do not change it by hand: change the plan in the script and run it again.

The board is 90 x 150 mm with 33 x 57 holes: columns A to Z and AA to AG, rows 1 to 57. A hole is written with a dash: `AB-12` is column AB, row 12.

## Parts and their holes

| Part | What | Holes |
|---|---|---|
| C1 | 10 µF | 1: F-44, 2: F-46 |
| C2 | 10 µF | 1: F-49, 2: F-51 |
| C3 | 100 nF | 1: F-25, 2: F-27 |
| C4 | 100 nF | 1: M-10, 2: M-12 |
| C5 | 100 nF | 1: M-17, 2: M-19 |
| C6 | 10 µF | 1: M-20, 2: M-22 |
| C7 | 22 pF | 1: E-20, 2: C-20 |
| C8 | 22 pF | 1: E-22, 2: C-22 |
| C9 | 100 nF | 1: G-8, 2: G-10 |
| C10 | 10 µF | 1: P-26, 2: R-26 |
| C11 | 10 µF | 1: U-26, 2: W-26 |
| C12 | 100 nF | 1: V-9, 2: V-11 |
| C13 | 100 nF | 1: Q-18, 2: Q-16 |
| C14 | 100 nF | 1: AC-10, 2: AC-12 |
| C15 | 100 nF | 1: V-29, 2: V-31 |
| C16 | 100 nF | 1: Q-35, 2: Q-33 |
| C17 | 100 nF | 1: AC-31, 2: AC-33 |
| D1 | LED 3 mm | A: H-32, K: H-33 |
| D2 | P6KE6.8CA | 1: AD-13, 2: AD-8 |
| D3 | P6KE6.8CA | 1: AD-21, 2: AD-26 |
| D4 | P6KE6.8CA | 1: AD-34, 2: AD-29 |
| D5 | P6KE6.8CA | 1: AD-42, 2: AD-47 |
| J1 | USB 2.0 Type-C breakout | CC1: K-55, CC2: L-55, D+: J-55, D-: I-55, GND: M-55, VBUS: H-55 |
| J2 | pin header 1x6 | 1: B-3, 2: B-4, 3: B-5, 4: B-6, 5: B-7, 6: B-8 |
| J3 | XLR 3-pin female, panel | 1: AG-4, 2: AG-8, 3: AG-6 |
| J4 | XLR 3-pin female, panel | 1: AG-25, 2: AG-29, 3: AG-27 |
| R1 | 5.1 kΩ | 1: K-53, 2: K-49 |
| R2 | 5.1 kΩ | 1: L-53, 2: L-49 |
| R3 | 10 kΩ | 1: F-4, 2: F-8 |
| R4 | 1 kΩ, standing: the body over F-9 | 1: F-9, 2: H-9 |
| R5 | 470 Ω | 1: H-27, 2: H-31 |
| R6 | 180 Ω | 1: O-8, 2: O-12 |
| R7 | 1 kΩ, standing: the body over W-8 | 1: W-8, 2: W-10 |
| R8 | 390 Ω | 1: V-22, 2: V-18 |
| R9 | 1 kΩ | 1: P-19, 2: P-23 |
| R10 | 470 Ω | 1: N-14, 2: Q-14 |
| R11 | 10 kΩ | 1: W-13, 2: W-17 |
| R12 | 560 Ω | 1: AE-12, 2: AE-16 |
| R13 | 130 Ω | 1: AD-15, 2: AD-19 |
| R14 | 560 Ω | 1: AE-18, 2: AE-22 |
| R15 | 180 Ω | 1: O-28, 2: O-32 |
| R16 | 1 kΩ, standing: the body over W-28 | 1: W-28, 2: W-30 |
| R17 | 390 Ω | 1: V-39, 2: V-35 |
| R18 | 1 kΩ | 1: M-34, 2: P-34 |
| R19 | 470 Ω | 1: N-38, 2: Q-38 |
| R20 | 10 kΩ | 1: X-38, 2: X-42 |
| R21 | 560 Ω | 1: AE-33, 2: AE-37 |
| R22 | 130 Ω | 1: AD-36, 2: AD-40 |
| R23 | 560 Ω | 1: AE-39, 2: AE-43 |
| SW1 | piano DIP switch, 8 positions (DPL08RGT) | 1: A-11, 10: D-17, 11: D-16, 12: D-15, 13: D-14, 14: D-13, 15: D-12, 16: D-11, 2: A-12, 3: A-13, 4: A-14, 5: A-15, 6: A-16, 7: A-17, 8: A-18, 9: D-18 |
| SW2 | tact switch 12 x 12 mm | a: B-30, b: G-32, gnd: B-32, net: G-30 |
| SW3 | tact switch 12 x 12 mm | a: B-36, b: G-38, gnd: B-38, net: G-36 |
| U1 | PIC32MX270F256B-I/SP | 1: H-12, 10: H-21, 11: H-22, 12: H-23, 13: H-24, 14: H-25, 15: K-25, 16: K-24, 17: K-23, 18: K-22, 19: K-21, 2: H-13, 20: K-20, 21: K-19, 22: K-18, 23: K-17, 24: K-16, 25: K-15, 26: K-14, 27: K-13, 28: K-12, 3: H-14, 4: H-15, 5: H-16, 6: H-17, 7: H-18, 8: H-19, 9: H-20 |
| U2 | MCP1700-3302E/TO | 1: G-47, 2: G-48, 3: G-49 |
| U3 | RKK-0505S/H | 1: Q-24, 2: R-24, 5: U-24, 7: W-24 |
| U4 | 6N137 | 1: R-8, 2: R-9, 3: R-10, 4: R-11, 5: U-11, 6: U-10, 7: U-9, 8: U-8 |
| U5 | 6N137 | 1: U-19, 2: U-18, 3: U-17, 4: U-16, 5: R-16, 6: R-17, 7: R-18, 8: R-19 |
| U6 | PC817 | 1: R-13, 2: R-14, 3: U-14, 4: U-13 |
| U7 | MAX485 | 1: Y-12, 2: Y-13, 3: Y-14, 4: Y-15, 5: AB-15, 6: AB-14, 7: AB-13, 8: AB-12 |
| U8 | 6N137 | 1: R-28, 2: R-29, 3: R-30, 4: R-31, 5: U-31, 6: U-30, 7: U-29, 8: U-28 |
| U9 | 6N137 | 1: U-36, 2: U-35, 3: U-34, 4: U-33, 5: R-33, 6: R-34, 7: R-35, 8: R-36 |
| U10 | PC817 | 1: R-38, 2: R-39, 3: U-39, 4: U-38 |
| U11 | MAX485 | 1: Y-33, 2: Y-34, 3: Y-35, 4: Y-36, 5: AB-36, 6: AB-35, 7: AB-34, 8: AB-33 |
| Y1 | 8 MHz | 1: F-20, 2: F-22 |

## Insulated wires (on the parts side)

Each wire is soldered into the hole at each of its ends; it lies along the holes given between them, turning where the list turns.

| Wire | Net | From | Along | To |
|---|---|---|---|---|
| W1 | MCLR | H-3 | straight | H-8 |
| W2 | GND2 | AD-4 | straight | AD-7 |
| W3 | PRESET | E-9 | E-11, G-11 | G-15 |
| W4 | +5V_ISO | X-9 | straight | X-11 |
| W5 | +3V3 | K-11 | K-9 | N-9 |
| W6 | +3V3 | N-12 | N-13, P-13 | P-18 |
| W7 | DE1 | U-12 | V-12, V-14 | W-14 |
| W8 | N4 | E-13 | F-13 | F-15 |
| W9 | GND2 | AC-13 | straight | AC-15 |
| W10 | N8 | E-14 | E-16, F-16 | F-18 |
| W11 | TX1 | L-14 | L-11, Q-11 | Q-10 |
| W12 | GND2 | U-15 | V-15, V-16, X-16 | X-18 |
| W13 | RO1 | W-15 | X-15 | X-12 |
| W14 | GND2 | AB-16 | straight | AB-21 |
| W15 | GND | N-17 | O-17 | O-15 |
| W16 | N32 | A-19 | straight | C-19 |
| W17 | PRESET | E-21 | G-21 | G-16 |
| W18 | +5V_ISO | AB-22 | AC-22, AC-17, AF-17, AF-13 | AE-13 |
| W19 | N32 | B-23 | straight | F-23 |
| W20 | N16 | A-24 | A-21, D-21, D-19, E-19 | E-18 |
| W21 | PRESET | E-24 | E-28, I-28, I-34 | G-34 |
| W22 | TX2 | P-24 | P-25, Q-25, Q-27, P-27 | P-30 |
| W23 | +3V3 | D-25 | A-25, A-48 | F-48 |
| W24 | DIR2 | N-25 | straight | N-37 |
| W25 | N16 | B-26 | straight | D-26 |
| W26 | N16 | F-26 | G-26 | G-22 |
| W27 | +3V3 | O-27 | O-21, P-21 | P-20 |
| W28 | GND | Q-28 | straight | Q-31 |
| W29 | DI2 | X-30 | straight | X-36 |
| W30 | GND2 | Y-30 | straight | AC-30 |
| W31 | GND2 | AD-30 | straight | AD-33 |
| W32 | B2 | AC-34 | straight | AC-37 |
| W33 | +5V | L-35 | straight | L-37 |
| W34 | GND | O-35 | O-39 | Q-39 |
| W35 | +5V_ISO | X-39 | Y-39, Y-38, AF-38, AF-32 | AE-32 |
| W36 | D- | K-47 | K-28, M-28, M-23, N-23, N-18 | L-18 |
| W37 | D+ | J-49 | J-26, L-26 | L-19 |
| W38 | +3V3 | G-50 | M-50 | M-35 |

## Tracks on the solder side

Each run goes in straight lines from hole to hole; every pad on the way belongs to it.

| Net | Runs |
|---|---|
| +3V3 | B-4 → N-4 → N-8; D-25 → F-25 → F-24 → H-24; F-48 → F-49 → G-49 → G-50; K-11 → K-12 → J-12 → J-17 → M-17; M-10 → N-10; M-35 → M-28 → O-28 → O-27; N-8 → N-10; N-8 → O-8; N-10 → N-12; P-18 → P-20 |
| +5V | F-44 → H-44; G-48 → H-48; H-44 → H-48; H-44 → P-44 → P-39; H-48 → H-55; K-25 → K-26; K-26 → K-35 → L-35; K-26 → Q-26 → Q-19; L-37 → L-39 → P-39; P-39 → P-37 → Q-37 → Q-36; Q-18 → Q-19; Q-19 → R-19 → R-18; Q-35 → Q-36; Q-36 → R-36 → R-35 |
| +5V_ISO | U-9 → U-8 → V-8; U-26 → U-28; U-28 → U-29 → V-29; U-28 → X-28; V-8 → V-9; V-8 → X-8; V-22 → W-22; V-39 → X-39 → X-38; W-13 → W-11 → X-11; W-22 → W-24 → X-24 → X-28; W-22 → AB-22; X-8 → X-9; X-8 → AC-8; X-28 → AB-28 → AB-31; AB-12 → AB-10 → AC-10 → AC-8; AB-31 → AB-32; AB-31 → AC-31; AB-32 → AB-33; AB-32 → AE-32 → AE-33; AC-8 → AC-6 → AE-6 → AE-13 |
| A1 | AB-14 → AD-14; AD-13 → AD-14; AD-14 → AD-16 → AE-16; AD-14 → AF-14 → AF-7 → AG-7 → AG-6 |
| A2 | AB-35 → AD-35; AD-35 → AD-34 → AF-34 → AF-27 → AG-27; AD-35 → AD-36 → AE-36 → AE-37 |
| B1 | AB-13 → AA-13 → AA-18 → AD-18; AD-18 → AD-21; AD-18 → AE-18 → AE-17 → AF-17 → AF-16 → AG-16 → AG-8 |
| B2 | AB-34 → AC-34; AC-37 → AD-37 → AD-38; AD-38 → AD-39; AD-38 → AG-38 → AG-29; AD-39 → AD-42; AD-39 → AE-39 |
| BLACKOUT | B-6 → D-6 → D-7 → J-7 → J-11 → I-11 → I-15; G-30 → I-30 → I-15; H-15 → I-15 |
| CC1 | K-53 → K-55 |
| CC2 | L-53 → L-55 |
| D+ | J-49 → J-55; K-19 → L-19 |
| D- | I-55 → I-47 → K-47; K-18 → L-18 |
| DE1 | U-12 → U-13; W-14 → X-14; W-17 → X-17 → X-14; X-14 → Y-14 → Y-13 |
| DE2 | U-38 → U-37 → W-37; W-37 → W-35 → Y-35 → Y-34; W-37 → Y-37 → Y-42 → X-42 |
| DI1 | U-10 → Y-10 → Y-11 → Z-11 → Z-15 → Y-15 |
| DI2 | U-30 → X-30; X-36 → Y-36 |
| DIR1 | K-15 → N-15 → N-14 |
| DIR1_LED | Q-14 → Q-13 → R-13 |
| DIR2 | K-24 → L-24 → L-25 → N-25; N-37 → N-38 |
| DIR2_LED | Q-38 → R-38 |
| GND | A-11 → A-12; A-12 → A-14 → B-14 → B-15 → A-15 → A-16 → B-16 → B-21 → C-21; A-12 → B-12 → B-10; B-10 → A-10 → A-5 → L-5 → L-12; B-10 → G-10; B-32 → C-32; B-38 → C-38; C-21 → C-20 → D-20 → D-19 → H-19; C-21 → C-32; C-32 → C-33; C-33 → C-38; C-33 → F-33; C-38 → E-38 → E-46; E-46 → E-47; E-46 → F-46; E-47 → E-51 → F-51 → F-56 → M-56 → M-49; E-47 → G-47; F-27 → F-33; F-33 → H-33; K-13 → L-13; K-21 → N-21; K-49 → M-49; L-12 → L-13; L-12 → M-12; L-13 → P-13 → P-15; M-19 → N-19; M-22 → N-22 → N-21; M-49 → R-49 → R-39 → Q-39; N-17 → N-19; N-19 → N-21; O-15 → P-15; O-35 → O-33 → Q-33; P-15 → Q-15; Q-15 → Q-16 → R-16; Q-15 → R-15 → R-14; Q-28 → Q-27 → R-27 → R-24; Q-31 → Q-33; Q-33 → R-33 |
| GND2 | U-11 → V-11 → V-14 → U-14 → U-15; U-31 → Y-31; U-39 → U-43 → AB-43; W-26 → V-26 → V-25 → U-25 → U-21 → X-21; X-18 → X-21; X-21 → AC-21 → AC-22 → AD-22; Y-30 → Y-31; Y-31 → Z-31 → Z-36 → AB-36 → AB-43; AB-16 → AB-15 → AC-15; AB-43 → AD-43; AC-13 → AC-12 → AD-12 → AD-7; AC-30 → AD-30 → AD-25; AC-33 → AD-33; AD-4 → AG-4; AD-22 → AD-25; AD-22 → AE-22; AD-25 → AG-25; AD-43 → AD-47; AD-43 → AE-43 |
| LED | H-25 → H-27 |
| LED_A | H-31 → H-32 |
| MCLR | B-3 → H-3; H-8 → H-12 |
| MCLR_RC | F-9 → F-8 → G-8 |
| N1 | D-11 → G-11 → G-13 → H-13 |
| N16 | A-24 → B-24 → B-26; D-15 → E-15 → E-18; D-26 → F-26; G-22 → H-22 |
| N2 | D-12 → F-12 → F-14 → H-14 |
| N32 | A-19 → A-23 → B-23; C-19 → C-16 → D-16; F-23 → H-23 |
| N4 | D-13 → E-13; F-15 → F-17 → H-17 |
| N8 | D-14 → E-14; F-18 → H-18 |
| OSC1 | E-20 → H-20 |
| OSC2 | E-22 → F-22 → F-21 → H-21 |
| PRESET | B-7 → C-7 → C-9 → E-9; E-21 → D-21 → D-24 → E-24; G-15 → G-16 → H-16; G-34 → G-36 |
| RO1 | U-17 → V-17 → V-15 → W-15; X-12 → Y-12 |
| RO1_LED | U-18 → V-18 |
| RO2 | U-34 → V-34 → V-33 → Y-33 |
| RO2_LED | U-35 → V-35 |
| RX1 | K-16 → O-16; O-16 → O-23 → P-23; O-16 → P-16 → P-17 → R-17 |
| RX2 | K-23 → J-23 → J-36 → P-36 → P-34 → R-34 |
| TX1 | K-14 → L-14; Q-10 → R-10 |
| TX1_LED | O-12 → O-9 → R-9 |
| TX2 | K-22 → L-22 → L-23 → N-23 → N-24 → P-24; P-30 → R-30 |
| TX2_LED | O-32 → O-29 → R-29 |
| VCAP | K-20 → M-20 |
