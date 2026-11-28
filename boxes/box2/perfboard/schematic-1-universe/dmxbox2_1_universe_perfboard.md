# dmxbox 2 with one output on a perfboard: the lists

Written by `boxes/box2/perfboard/schematic-1-universe/perfboard.py`. Do not change it by hand: change the plan in the script and run it again.

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
| D1 | LED 3 mm | A: H-32, K: H-33 |
| D2 | P6KE6.8CA | 1: AD-13, 2: AD-8 |
| D3 | P6KE6.8CA | 1: AD-21, 2: AD-26 |
| J1 | USB 2.0 Type-C breakout | CC1: K-55, CC2: L-55, D+: J-55, D-: I-55, GND: M-55, VBUS: H-55 |
| J2 | pin header 1x6 | 1: B-3, 2: B-4, 3: B-5, 4: B-6, 5: B-7, 6: B-8 |
| J3 | XLR 3-pin female, panel | 1: AG-4, 2: AG-8, 3: AG-6 |
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
| Y1 | 8 MHz | 1: F-20, 2: F-22 |

## Insulated wires (on the parts side)

Each wire is soldered into the hole at each of its ends; it lies along the holes given between them, turning where the list turns.

| Wire | Net | From | Along | To |
|---|---|---|---|---|
| W1 | MCLR | C-3 | C-5, H-5 | H-8 |
| W2 | +3V3 | P-8 | straight | P-10 |
| W3 | BLACKOUT | L-9 | straight | N-9 |
| W4 | N16 | C-10 | F-10, F-16, G-16 | G-22 |
| W5 | GND2 | X-10 | X-9 | AD-9 |
| W6 | +5V_ISO | AC-11 | straight | AE-11 |
| W7 | BLACKOUT | N-12 | N-13, O-13, O-25, Q-25 | Q-29 |
| W8 | +3V3 | P-12 | straight | P-18 |
| W9 | GND2 | U-12 | V-12, V-15 | U-15 |
| W10 | N4 | E-13 | E-17 | F-17 |
| W11 | GND2 | AC-13 | straight | AC-15 |
| W12 | TX1 | L-14 | L-11 | Q-11 |
| W13 | BLACKOUT | G-15 | G-11, I-11 | I-6 |
| W14 | GND | N-15 | straight | N-17 |
| W15 | GND2 | Z-16 | straight | AB-16 |
| W16 | RO1 | V-17 | V-16, X-16, X-11 | Y-11 |
| W17 | +5V_ISO | X-17 | AF-17, AF-13 | AE-13 |
| W18 | D- | M-18 | N-18 | N-35 |
| W19 | N32 | C-19 | D-19, D-23 | F-23 |
| W20 | GND | C-24 | straight | C-27 |
| W21 | +3V3 | G-26 | straight | J-26 |
| W22 | +5V | O-26 | straight | O-28 |
| W23 | +5V_ISO | U-27 | V-27, V-25 | W-25 |
| W24 | +5V | K-28 | straight | K-38 |
| W25 | BLACKOUT | H-30 | I-30 | I-35 |
| W26 | D+ | J-53 | J-27, L-27 | L-19 |

## Tracks on the solder side

Each run goes in straight lines from hole to hole; every pad on the way belongs to it.

| Net | Runs |
|---|---|
| +3V3 | B-4 → M-4 → M-8; G-24 → F-24 → F-25 → A-25 → A-49 → G-49; G-24 → G-26; G-24 → H-24; J-17 → J-12 → L-12 → L-11 → M-11 → M-8; J-17 → J-26; J-17 → M-17; M-8 → P-8; P-10 → P-12; P-18 → P-19 |
| +5V | H-55 → E-55 → E-50 → H-50 → H-48 → E-48 → E-44 → H-44 → H-38 → K-38; K-25 → K-28 → O-28; O-26 → P-26 → P-24 → Q-24 → Q-23 → R-23 → R-18 → Q-18 |
| +5V_ISO | U-26 → U-27; V-9 → U-9 → U-8 → AA-8 → AA-9 → AC-9 → AC-11 → AB-11 → AB-12; V-22 → W-22; W-13 → W-15 → X-15 → X-22 → W-22; W-22 → W-25; AE-11 → AE-13 |
| A1 | AB-14 → AD-14; AD-13 → AD-14; AD-14 → AD-15 → AE-15; AE-15 → AE-16; AE-15 → AF-15 → AF-6 → AG-6 |
| B1 | AB-13 → AA-13 → AA-18 → AD-18; AD-18 → AD-21; AD-18 → AG-18 → AG-8 |
| BLACKOUT | B-6 → L-6 → L-9; G-15 → H-15; G-30 → H-30; I-35 → J-35 → J-29 → Q-29; N-9 → N-12 |
| CC1 | K-53 → K-55 |
| CC2 | L-53 → L-55 |
| D+ | J-53 → J-55; K-19 → L-19 |
| D- | I-55 → I-56 → N-56 → N-35; K-18 → M-18 |
| DE1 | U-13 → V-13; V-13 → V-12 → X-12 → X-13 → Y-13 → Y-14; V-13 → V-16 → W-16 → W-17 |
| DI1 | U-10 → W-10 → W-9 → Z-9 → Z-15 → Y-15 |
| DIR1 | K-15 → M-15 → M-14 → N-14 |
| DIR1_LED | Q-14 → Q-13 → R-13 |
| GND | A-9 → A-5 → B-5; A-9 → A-16 → B-16 → B-20 → C-20; A-9 → D-9 → D-10 → G-10; B-32 → F-32; B-38 → B-37 → F-37; C-20 → C-24; C-20 → D-20 → D-19 → H-19; C-27 → F-27 → F-32; F-32 → F-33; F-33 → F-37; F-33 → H-33; F-37 → L-37; F-46 → F-47 → K-47; F-51 → M-51; K-13 → M-13; K-21 → M-21; K-47 → K-49 → M-49 → M-51; K-47 → L-47 → L-37; L-37 → L-30 → R-30 → R-27; M-12 → M-13; M-13 → O-13 → O-15; M-19 → N-19; M-21 → M-27 → R-27; M-21 → N-21 → N-19; M-51 → M-55; N-15 → O-15; N-17 → N-19; O-15 → Q-15 → Q-16 → R-16 → R-14; R-24 → R-27 |
| GND2 | U-12 → U-11 → X-11 → X-10; U-14 → U-15; U-24 → U-25 → V-25 → V-26 → Z-26; Z-16 → Z-26; Z-26 → AE-26 → AE-22; AB-16 → AB-15 → AC-15; AC-13 → AC-12 → AD-12 → AD-4 → AG-4 |
| LED | H-25 → H-27 |
| LED_A | H-31 → H-32 |
| MCLR | B-3 → C-3; H-8 → H-12 |
| MCLR_RC | F-9 → F-8 → G-8 |
| N1 | D-11 → G-11 → G-13 → H-13 |
| N16 | C-10 → C-15 → D-15; G-22 → H-22 |
| N2 | D-12 → F-12 → F-14 → H-14 |
| N32 | C-19 → C-16 → D-16; F-23 → H-23 |
| N4 | D-13 → E-13; F-17 → H-17 |
| N8 | D-14 → E-14 → E-18 → H-18 |
| OSC1 | E-20 → H-20 |
| OSC2 | E-22 → F-22 → F-21 → H-21 |
| PRESET | B-7 → I-7 → I-16; G-36 → G-34 → I-34 → I-16; H-16 → I-16 |
| RO1 | U-17 → V-17; Y-11 → Y-12 |
| RO1_LED | U-18 → V-18 |
| RX1 | K-16 → O-16; O-16 → O-23 → P-23; O-16 → P-16 → P-17 → R-17 |
| TX1 | K-14 → L-14; Q-11 → Q-10 → R-10 |
| TX1_LED | O-12 → O-9 → R-9 |
| VCAP | K-20 → M-20 |
